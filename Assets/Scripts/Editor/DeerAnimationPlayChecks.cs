using System;
using System.IO;
using System.Reflection;
using HuntHappens.Animals;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    // A disposable specimen: never changes the player's save, inventory, health or position.
    public static class DeerAnimationPlayChecks
    {
        static DeerSedation specimen;
        static DeerLeader leader;
        static DeerAnimation animation;
        static NavMeshAgent agent;
        static Camera camera;
        static int stage, frame;
        static float started, maximumSpeed, maximumSwing;
        static bool background;
        static bool capturedCharge;
        static Quaternion legBefore;
        static Vector3 direction, positionBefore;
        static float travelled;
        static string report;
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Enter(DeerLeader.LeaderState state) => typeof(DeerLeader).GetMethod("Enter", Flags).Invoke(leader, new object[] { state });
        static float Value(string field) => (float)typeof(DeerAnimation).GetField(field, Flags).GetValue(animation);
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); report += "PASS " + message + "\n"; }
        public static void Begin()
        {
            if (!Application.isPlaying || specimen != null) throw new InvalidOperationException("Requires Play Mode and no active animation check");
            report = ""; stage = 0; maximumSpeed = maximumSwing = travelled = 0; capturedCharge = false;
            var zone = Array.Find(Object.FindObjectsByType<DeerCaptureZone>(), x => x.hasLeader);
            specimen = Object.Instantiate(zone.leaderPrefab, zone.transform.position, Quaternion.identity);
            specimen.name = "Animation Check Specimen";
            leader = specimen.GetComponent<DeerLeader>(); leader.enabled = false; leader.SetHome(zone.transform);
            animation = specimen.GetComponent<DeerAnimation>(); agent = specimen.GetComponent<NavMeshAgent>();
            direction = Vector3.forward; float best = 0;
            for (int i = 0; i < 16; i++)
            {
                Vector3 candidate = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
                agent.Raycast(specimen.transform.position + candidate * 14, out var hit);
                float distance = Vector3.Distance(hit.position, specimen.transform.position);
                if (distance > best) { best = distance; direction = candidate; }
            }
            specimen.transform.rotation = Quaternion.LookRotation(direction);
            camera = new GameObject("Animation Check Camera").AddComponent<Camera>(); camera.enabled = false;
            camera.fieldOfView = 42; camera.nearClipPlane = .1f; camera.farClipPlane = 150;
            background = Application.runInBackground; Application.runInBackground = true;
            Enter(DeerLeader.LeaderState.Warning); started = Time.time; frame = Time.frameCount;
            EditorApplication.update += Tick;
        }
        static void Tick()
        {
            if (!Application.isPlaying || specimen == null) { Finish("Interrupted"); return; }
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            try
            {
                float elapsed = Time.time - started;
                if (stage == 0)
                {
                    typeof(DeerLeader).GetField("elapsed", Flags).SetValue(leader, elapsed);
                    if (elapsed < 1.2f) return;
                    Check(Value("preparation") > .8f && Value("headAngle") > 25 && Value("leanBack") > .08f, "warning smoothly lowers head and shifts weight backwards");
                    Capture("warning"); Enter(DeerLeader.LeaderState.Charge); Next();
                    positionBefore = specimen.transform.position; legBefore = animation.LegPivots[0].localRotation;
                }
                else if (stage == 1)
                {
                    maximumSpeed = Mathf.Max(maximumSpeed, animation.AnimationSpeed);
                    maximumSwing = Mathf.Max(maximumSwing, Quaternion.Angle(legBefore, animation.LegPivots[0].localRotation));
                    travelled += Vector3.Distance(positionBefore, specimen.transform.position); positionBefore = specimen.transform.position;
                    if (elapsed > .35f && !capturedCharge) { Capture("charge-a"); capturedCharge = true; }
                    if (elapsed < .8f) { agent.Move(direction * leader.chargeSpeed * Time.deltaTime); return; }
                    Check(travelled > 3 && maximumSpeed > 5 && maximumSwing > 25, "Agent.Move drives measured speed and moving legs");
                    Check(Quaternion.Angle(animation.LegPivots[0].localRotation, animation.LegPivots[2].localRotation) > 10, "front and rear legs have different gallop phases");
                    Capture("charge-b"); Enter(DeerLeader.LeaderState.Impact); Next();
                }
                else if (stage == 2)
                {
                    if (elapsed < .23f) return;
                    Check(Value("leanBack") > .07f && Value("bodyAngle") < 0, "impact produces backwards body recoil");
                    Capture("impact"); Enter(DeerLeader.LeaderState.Recovery); Next();
                }
                else if (stage == 3)
                {
                    if (elapsed < .9f) return;
                    Check(Mathf.Abs(Value("bodyAngle")) < 1 && Value("leanBack") < .01f && animation.AnimationSpeed < .3f, "recovery settles without continued leg cycling");
                    Capture("recovery"); specimen.ApplyDose(3); Next();
                }
                else if (stage == 4)
                {
                    if (specimen.State != DeerSedation.SedationState.Unconscious)
                    { if (elapsed > 7) throw new Exception("Sedation timeout"); return; }
                    Check(!agent.enabled && specimen.GetComponent<HuntHappens.Interaction.PickupItem>().enabled, "sedation still hands control to carry physics");
                    Check(animation.AnimationSpeed == 0, "sedation overrides attack animation");
                    Capture("sleeping"); Finish("PASS");
                }
            }
            catch (Exception exception) { Finish("FAIL " + exception); }
        }
        static void Next() { stage++; started = Time.time; }
        static void Capture(string name)
        {
            camera.transform.position = specimen.transform.position + specimen.transform.right * 6 - specimen.transform.forward * 3 + Vector3.up * 3;
            camera.transform.LookAt(specimen.transform.position + Vector3.up * 1.5f);
            var target = new RenderTexture(960, 720, 24); var previous = RenderTexture.active;
            var texture = new Texture2D(960, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); texture.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/DeerAnimation-" + name + ".png", texture.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; Object.DestroyImmediate(texture); target.Release(); Object.DestroyImmediate(target); }
        }
        static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/DeerAnimationChecks.txt", report + result);
            Application.runInBackground = background;
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
            if (specimen != null) Object.DestroyImmediate(specimen.gameObject);
        }
    }
}
