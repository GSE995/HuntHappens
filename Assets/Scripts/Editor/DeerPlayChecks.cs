using System;
using HuntHappens.Animals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Editor
{
    // Drives a temporary threat in Play Mode; never saves test positions back into the scene.
    [InitializeOnLoad]
    public static class DeerPlayChecks
    {
        private const string Pending = "HuntHappens.DeerPlayChecks";
        private static DeerAI deer;
        private static DeerPerception perception;
        private static NavMeshAgent agent;
        private static Transform threat;
        private static GameObject wall;
        private static int stage;
        private static float started;
        private static float stageStarted;
        private static Vector3 initialPosition;
        private static Vector3 fleeStart;
        private static Quaternion initialLegRotation;
        private static bool legsMoved;
        private static bool observedIdle;
        private static bool observedWander;
        private static double deadline;

        static DeerPlayChecks() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only");
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        public static void RenderPreviewBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only");
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            Transform model = GameObject.Find("Prototype Deer/Deer 1").transform;
            var cameraObject = new GameObject("Temporary Deer Preview Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camera.transform.position = model.TransformPoint(new Vector3(3.2f, 2.3f, 3.8f));
            camera.transform.LookAt(model.position + Vector3.up * 1.45f);
            camera.fieldOfView = 42;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.48f, 0.76f, 0.94f);
            var target = new RenderTexture(960, 960, 24);
            var pixels = new Texture2D(960, 960, TextureFormat.RGB24, false);
            int frames = 0;
            target.Create();
            EditorApplication.update += RenderFrame;
            void RenderFrame()
            {
                RenderTexture previous = RenderTexture.active;
                try
                {
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                        new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    // Allow shader compilation and GPU material uploads to finish before reading back.
                    if (++frames < 20) return;
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 960, 960), 0, 0);
                    pixels.Apply();
                    System.IO.File.WriteAllBytes("Logs/Deer-preview.png", pixels.EncodeToPNG());
                    Debug.Log("Deer preview rendered to Logs/Deer-preview.png");
                    EditorApplication.update -= RenderFrame;
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                    UnityEngine.Object.DestroyImmediate(pixels);
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                    EditorApplication.Exit(0);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    EditorApplication.update -= RenderFrame;
                    EditorApplication.Exit(1);
                }
                finally { RenderTexture.active = previous; }
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            stage = 0;
            started = stageStarted = Time.time;
            deadline = EditorApplication.timeSinceStartup + 100;
            observedIdle = observedWander = legsMoved = false;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline && Time.time - started < 70, "Timed out at stage " + stage);
                float elapsed = Time.time - stageStarted;
                if (stage == 0 && elapsed > 0.2f)
                {
                    DeerAI[] deerList = UnityEngine.Object.FindObjectsByType<DeerAI>();
                    Check(deerList.Length == 3, "Three deer are present");
                    foreach (DeerAI candidate in deerList)
                    {
                        Check(candidate.GetComponent<NavMeshAgent>().isOnNavMesh, "All three agents are on NavMesh");
                        if (candidate.name == "Deer 1") deer = candidate;
                    }
                    Check(deer != null, "Deer 1 exists");
                    agent = deer.GetComponent<NavMeshAgent>();
                    perception = deer.GetComponent<DeerPerception>();
                    threat = new GameObject("Temporary AI Check Threat").transform;
                    threat.position = new Vector3(100, 0, 100);
                    perception.SetPlayer(threat);
                    initialPosition = deer.transform.position;
                    initialLegRotation = deer.transform.Find("Visual/Leg Pivot 0").localRotation;
                    Next();
                }
                else if (stage == 1)
                {
                    observedIdle |= deer.State == DeerAI.DeerState.Idle;
                    observedWander |= deer.State == DeerAI.DeerState.Wander;
                    legsMoved |= Quaternion.Angle(initialLegRotation, deer.transform.Find("Visual/Leg Pivot 0").localRotation) > 5;
                    if (observedIdle && observedWander && legsMoved && Vector3.Distance(initialPosition, deer.transform.position) > 1)
                    {
                        Check(agent.pathStatus == NavMeshPathStatus.PathComplete, "Wander has complete path");
                        Check(Vector3.Distance(agent.nextPosition, deer.transform.position) < 0.2f, "Animation leaves agent root synchronized");
                        PlaceThreat(9);
                        Next();
                    }
                    else Check(elapsed < 18, "Natural Idle/Wander and leg animation observed");
                }
                else if (stage == 2 && elapsed > 0.12f)
                {
                    Check(deer.State != DeerAI.DeerState.Alert && deer.State != DeerAI.DeerState.Flee, "Reaction is delayed");
                    Next();
                }
                else if (stage == 3 && elapsed > 0.9f)
                {
                    Check(deer.State == DeerAI.DeerState.Alert, "Visible player at 9m causes Alert");
                    Check(agent.velocity.magnitude < 0.2f, "Alert stops movement");
                    PlaceThreat(4);
                    fleeStart = deer.transform.position;
                    Next();
                }
                else if (stage == 4 && elapsed > 1.5f)
                {
                    Check(deer.State == DeerAI.DeerState.Flee, "Close danger causes Flee");
                    Check(Vector3.Distance(deer.transform.position, fleeStart) > 1, "Flee moves the animal");
                    Check(agent.isOnNavMesh && agent.pathStatus == NavMeshPathStatus.PathComplete, "Flee has complete navigable path");
                    threat.position = deer.transform.position + deer.transform.forward * 7;
                    Next();
                }
                else if (stage == 5 && elapsed > 0.25f)
                {
                    Check(deer.State == DeerAI.DeerState.Flee, "Crossing fright radius boundary does not oscillate to Alert");
                    threat.position = new Vector3(100, 0, 100);
                    Next();
                }
                else if (stage == 6 && elapsed > 3.8f)
                {
                    Check(deer.State != DeerAI.DeerState.Flee, "Safe distance plus calm delay ends Flee");
                    PlaceThreat(9);
                    Vector3 eye = deer.transform.TransformPoint(new Vector3(0, 2, 0.65f));
                    Vector3 target = threat.position + Vector3.up * 1.1f;
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "Temporary Sight Check Wall";
                    wall.transform.position = (eye + target) * 0.5f;
                    wall.transform.rotation = Quaternion.LookRotation(target - eye);
                    wall.transform.localScale = new Vector3(6, 4, 0.5f);
                    Physics.SyncTransforms();
                    Next();
                }
                else if (stage == 7 && elapsed > 0.7f)
                {
                    Check(!perception.HasSight && !perception.Noticed, "Solid wall occludes perception");
                    UnityEngine.Object.Destroy(wall);
                    Next();
                }
                else if (stage == 8 && elapsed > 0.9f)
                {
                    Check(deer.State == DeerAI.DeerState.Alert, "Removing wall restores Alert");
                    threat.position = new Vector3(100, 0, 100);
                    Next();
                }
                else if (stage == 9 && elapsed > 2.5f)
                {
                    Check(deer.State != DeerAI.DeerState.Alert, "Losing player ends Alert after memory delay");
                    foreach (DeerAI candidate in UnityEngine.Object.FindObjectsByType<DeerAI>())
                        Check(candidate.GetComponent<NavMeshAgent>().isOnNavMesh, "All agents remain on NavMesh");
                    Debug.Log("Deer Play checks PASSED: three agents, natural Idle/Wander, movement and legs, root sync, delayed Alert, Flee, hysteresis, calm recovery, line-of-sight occlusion and recovery.");
                    Finish(0);
                }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private static void PlaceThreat(float distance)
        {
            // Choose a clear sight line in the actual forest, independent of its tree distribution.
            for (int i = 0; i < 16; i++)
            {
                Vector3 direction = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
                Vector3 candidate = deer.transform.position + direction * distance;
                Vector3 eye = deer.transform.position + Vector3.up * 2;
                Vector3 delta = candidate + Vector3.up * 1.1f - eye;
                bool blocked = false;
                foreach (RaycastHit hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.collider.transform.IsChildOf(deer.transform)) blocked = true;
                if (blocked) continue;
                threat.position = candidate;
                deer.transform.rotation = Quaternion.LookRotation(direction);
                perception.SetPlayer(threat);
                return;
            }
            throw new InvalidOperationException("No clear threat position for test");
        }

        private static void Next() { stage++; stageStarted = Time.time; }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Deer Play check failed: " + message); }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}
