using System;
using System.Linq;
using HuntHappens.Characters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class SemenychRigChecks
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new Exception("Run rig sampling in Edit Mode");
            var npc = Object.FindAnyObjectByType<SemenychDialogue>();
            var animator = npc.GetComponentInChildren<Animator>();
            var skin = npc.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null || animator == null || animator.applyRootMotion || animator.speed != 1)
                throw new Exception("Missing rig or invalid Animator settings");
            var clip = animator.runtimeAnimatorController.animationClips.Single();
            if (!clip.isLooping || Mathf.Abs(clip.length - 6f) > .001f)
                throw new Exception("Expected six-second looping idle");
            foreach (var w in skin.sharedMesh.boneWeights)
                if (Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1) > .001f)
                    throw new Exception("Unnormalized vertex weights");
            var mesh = new Mesh();
            var rootPosition = npc.transform.position;
            float movement = 0, feetDrift = 0, seam = 0;
            try
            {
                AnimationMode.StartAnimationMode();
                Sample(animator, clip, 0);
                skin.BakeMesh(mesh, true);
                var start = mesh.vertices.Select(skin.transform.TransformPoint).ToArray();
                for (int sample = 1; sample <= 24; sample++)
                {
                    Sample(animator, clip, sample / 4f);
                    skin.BakeMesh(mesh, true);
                    var vertices = mesh.vertices;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var p = skin.transform.TransformPoint(vertices[i]);
                        if (float.IsNaN(p.x) || float.IsInfinity(p.x)) throw new Exception("Invalid skinned vertex");
                        float delta = Vector3.Distance(p, start[i]);
                        movement = Mathf.Max(movement, delta);
                        if (start[i].y < rootPosition.y + .15f) feetDrift = Mathf.Max(feetDrift, delta);
                        if (sample == 24) seam = Mathf.Max(seam, delta);
                    }
                }
                if (movement < .001f || movement > .15f || feetDrift > .003f || seam > .001f || npc.transform.position != rootPosition)
                    throw new Exception($"Unexpected deformation: movement={movement}, feet={feetDrift}, seam={seam}");
                return $"PASSED: {skin.bones.Length} skin bones, {skin.sharedMesh.vertexCount} vertices, normalized weights; " +
                    $"24 idle samples, max motion {movement:F4} m, feet drift {feetDrift:F5} m, loop seam {seam:F5} m, fixed NPC root.";
            }
            finally { AnimationMode.StopAnimationMode(); Object.DestroyImmediate(mesh); }
        }

        private static void Sample(Animator animator, AnimationClip clip, float time)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, time);
            AnimationMode.EndSampling();
        }

        public static string Capture()
        {
            var npc = Object.FindAnyObjectByType<SemenychDialogue>();
            var animator = npc.GetComponentInChildren<Animator>();
            var clip = animator.runtimeAnimatorController.animationClips.Single();
            var go = new GameObject("Semenych rig preview");
            var camera = go.AddComponent<Camera>();
            var rt = new RenderTexture(640, 800, 24);
            var image = new Texture2D(640, 800, TextureFormat.RGB24, false);
            camera.targetTexture = rt;
            camera.nearClipPlane = .1f;
            var oldTarget = RenderTexture.active;
            System.IO.Directory.CreateDirectory("Logs/SemenychRig");
            try
            {
                AnimationMode.StartAnimationMode();
                for (int i = 0; i < 4; i++)
                {
                    Sample(animator, clip, i * 1.5f);
                    Vector3 direction = i < 2 ? npc.transform.forward : i == 2 ? npc.transform.right : -npc.transform.forward;
                    camera.transform.position = npc.transform.position + direction * 3.6f + Vector3.up * 1.3f;
                    camera.transform.LookAt(npc.transform.position + Vector3.up * 1.12f);
                    camera.Render();
                    RenderTexture.active = rt;
                    image.ReadPixels(new Rect(0, 0, 640, 800), 0, 0); image.Apply();
                    System.IO.File.WriteAllBytes($"Logs/SemenychRig/pose-{i}.png", image.EncodeToPNG());
                }
                return "Logs/SemenychRig/pose-0.png through pose-3.png";
            }
            finally
            {
                AnimationMode.StopAnimationMode(); RenderTexture.active = oldTarget;
                Object.DestroyImmediate(go); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
            }
        }
    }
}
