using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HuntHappens.Editor
{
    // Author an ordinary looping clip, with the adapted FBX rest pose as its reference.
    public static class SemenychIdleSetup
    {
        public const float Duration = 6f;
        private const string Folder = "Assets/Art/MouseCatcher/";

        public static void Configure(GameObject visual)
        {
            var clip = new AnimationClip { name = "SemenychIdle", frameRate = 30 };
            var bones = visual.GetComponentsInChildren<Transform>();
            foreach (var bone in bones)
            {
                bool arm = bone.name == "LeftArm" || bone.name == "RightArm";
                bool forearm = bone.name == "LeftForeArm" || bone.name == "RightForeArm";
                bool spine = bone.name == "Spine02" || bone.name == "Spine01" || bone.name == "Spine";
                bool head = bone.name == "Head";
                if (!arm && !forearm && !spine && !head) continue;
                var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                var rest = bone.localRotation;
                var reference = visual.transform.parent;
                Vector3 right = bone.parent.InverseTransformDirection(reference.right);
                Vector3 forward = bone.parent.InverseTransformDirection(reference.forward);
                Vector3 up = bone.parent.InverseTransformDirection(reference.up);
                float side = Mathf.Sign(reference.InverseTransformPoint(bone.position).x);
                for (int frame = 0; frame <= 180; frame++)
                {
                    float time = frame / 30f;
                    float phase = time / Duration * Mathf.PI * 2;
                    float breath = Mathf.Sin(phase);
                    Quaternion delta;
                    if (arm)
                        delta = Quaternion.AngleAxis(-side * (14f + .7f * breath), forward)
                            * Quaternion.AngleAxis(.6f * Mathf.Sin(phase + side * .4f), right);
                    else if (forearm)
                        delta = Quaternion.AngleAxis(-3f + .5f * breath, right);
                    else if (head)
                        delta = Quaternion.AngleAxis(1.5f * Mathf.Sin(phase), up)
                            * Quaternion.AngleAxis(.6f * (Mathf.Cos(phase) - 1), right);
                    else
                        delta = Quaternion.AngleAxis(.35f * breath, right)
                            * Quaternion.AngleAxis(.2f * Mathf.Sin(phase), forward);
                    Quaternion rotation = delta * rest;
                    curves[0].AddKey(time, rotation.x);
                    curves[1].AddKey(time, rotation.y);
                    curves[2].AddKey(time, rotation.z);
                    curves[3].AddKey(time, rotation.w);
                }
                string path = AnimationUtility.CalculateTransformPath(bone, visual.transform);
                string[] axes = { "x", "y", "z", "w" };
                for (int i = 0; i < 4; i++) clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + axes[i], curves[i]);
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string clipPath = Folder + "SemenychIdle.anim";
            var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (saved == null) { AssetDatabase.CreateAsset(clip, clipPath); saved = clip; }
            else { EditorUtility.CopySerialized(clip, saved); Object.DestroyImmediate(clip); }
            string controllerPath = Folder + "SemenychIdle.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.FirstOrDefault().state;
            if (state == null) state = machine.AddState("Quiet standing");
            state.motion = saved;
            machine.defaultState = state;
            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.speed = 1;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(saved);
            EditorUtility.SetDirty(controller);
        }
    }
}
