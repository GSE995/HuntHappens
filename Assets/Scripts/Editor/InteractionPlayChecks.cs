using System;
using HuntHappens.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HuntHappens.Editor
{
    // Batch-only smoke test in real Play Mode. All test movements/obstacles are discarded on exit.
    [InitializeOnLoad]
    public static class InteractionPlayChecks
    {
        private const string PendingKey = "HuntHappens.InteractionPlayChecks";
        private static ItemHolder holder;
        private static PickupItem item;
        private static Transform view;
        private static int stage;
        private static float stageStart;
        private static double deadline;
        private static float dropHeight;

        static InteractionPlayChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only");
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.SetBool(PendingKey, false);
            deadline = EditorApplication.timeSinceStartup + 60;
            stage = 0;
            stageStart = Time.time;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "Play checks timed out");
                if (Time.time - stageStart < 0.2f) return;
                if (stage == 0)
                {
                    holder = UnityEngine.Object.FindAnyObjectByType<ItemHolder>();
                    Check(holder != null, "Player has ItemHolder");
                    view = holder.GetComponentInChildren<Camera>().transform;
                    foreach (PickupItem candidate in UnityEngine.Object.FindObjectsByType<PickupItem>())
                        if (candidate.name == "Pickup Crate Small") item = candidate;
                    Check(item != null, "Test crate exists");
                    PlaceAtTarget();
                    Check(holder.TryPickUp(item), "Pickup accepted in Play Mode");
                    Next();
                }
                else if (stage == 1 && Time.time - stageStart > 1f)
                {
                    Check(holder.IsHolding && TargetError() < 0.4f, "Dynamic body follows hold target");
                    holder.GetComponent<CharacterController>().Move(Vector3.right);
                    Next();
                }
                else if (stage == 2 && Time.time - stageStart > 1f)
                {
                    Check(holder.IsHolding && TargetError() < 0.4f, "Item follows moving player");
                    dropHeight = item.Body.position.y;
                    holder.Drop();
                    Check(!holder.IsHolding && item.Body.useGravity, "Drop restores gravity");
                    Next();
                }
                else if (stage == 3 && Time.time - stageStart > 1f)
                {
                    Check(item.Body.position.y < dropHeight - 0.4f && item.Body.position.y > 0,
                        "Dropped item falls and collides with ground");
                    PlaceAtTarget();
                    Check(holder.TryPickUp(item), "Dropped item can be picked up again");
                    holder.Throw();
                    Next();
                }
                else if (stage == 4 && Time.time - stageStart > 0.2f)
                {
                    Check(!holder.IsHolding && Vector3.Dot(item.Body.linearVelocity, view.forward) > 1f,
                        "Throw produces forward physical velocity");
                    PlaceAtTarget();
                    Check(holder.TryPickUp(item), "Pickup before stuck test");
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "Temporary Play Check Wall";
                    wall.transform.position = view.position + view.forward * 1.2f;
                    wall.transform.localScale = new Vector3(4, 4, 0.3f);
                    item.Body.position = view.position + view.forward * 0.6f;
                    item.Body.linearVelocity = Vector3.zero;
                    Next();
                }
                else if (stage == 5 && Time.time - stageStart > 2.2f)
                {
                    Check(!holder.IsHolding && item.Body.useGravity, "Blocked item automatically releases");
                    Check(UnityEngine.Object.FindAnyObjectByType<TMPro.TextMeshProUGUI>() != null,
                        "HUD instantiates TMP label in Play Mode");
                    Debug.Log("Interaction Play checks PASSED: pickup, dynamic follow, player movement, drop/gravity/ground, re-pickup, throw impulse, stuck release, HUD initialization.");
                    EditorApplication.update -= Tick;
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(1);
            }
        }

        private static void PlaceAtTarget()
        {
            item.Body.position = view.position + view.forward * 2;
            item.Body.linearVelocity = Vector3.zero;
            item.Body.angularVelocity = Vector3.zero;
        }

        private static float TargetError() => Vector3.Distance(item.Body.worldCenterOfMass, view.position + view.forward * 2);
        private static void Next() { stage++; stageStart = Time.time; }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Interaction Play check failed: " + message);
        }
    }
}
