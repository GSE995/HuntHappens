using System;
using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Traps;
using HuntHappens.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class TrapPlayChecks
    {
        private const string Pending = "HuntHappens.TrapPlayChecks";
        private static int stage;
        private static float began;
        private static double deadline;
        private static TrapPlacement placement;
        private static TrapManager manager;
        private static AnimalTrap trap;
        private static DeerSedation[] deer;
        private static ItemHolder holder;
        private static PlayerInteraction interaction;
        private static TranquilizerGun gun;
        private static Camera view;
        private static Vector3 site;
        private static PickupTruckDelivery truck;
        static TrapPlayChecks() => EditorApplication.playModeStateChanged += OnPlay;
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
        private static void OnPlay(PlayModeStateChange value)
        {
            if (value != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            stage = 0;
            began = Time.time;
            deadline = EditorApplication.timeSinceStartup + 100;
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "Timeout at stage " + stage);
                float elapsed = Time.time - began;
                if (stage == 0 && elapsed > 0.5f)
                {
                    placement = UnityEngine.Object.FindAnyObjectByType<TrapPlacement>();
                    manager = placement.Manager;
                    gun = placement.GetComponent<TranquilizerGun>();
                    holder = placement.GetComponent<ItemHolder>();
                    interaction = placement.GetComponent<PlayerInteraction>();
                    view = placement.GetComponentInChildren<Camera>();
                    placement.GetComponent<FirstPersonMotor>().enabled = false;
                    deer = UnityEngine.Object.FindObjectsByType<DeerSedation>();
                    foreach (var d in deer) d.GetComponent<DeerAI>().enabled = false;
                    truck = UnityEngine.Object.FindAnyObjectByType<PickupTruckDelivery>();
                    TestBinding();
                    gun.SelectTool(true);
                    Check(gun.Selected && !gun.TrapSelected, "Tool 1 selects gun");
                    gun.SelectTrapTool();
                    Check(!gun.Selected && gun.TrapSelected && !gun.TryFire(), "Trap tool never fires a dart");
                    gun.SelectTool(false);
                    Check(!gun.Selected && !gun.TrapSelected, "Tool 2 clears trap mode");
                    gun.SelectTrapTool();
                    Check(FindSite(), "Clear supported NavMesh placement exists");
                    var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obstacle.transform.position = site + Vector3.up * 0.4f + Vector3.right;
                    obstacle.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
                    Physics.SyncTransforms();
                    Check(!placement.Evaluate(out _, out _), "Obstacle beside centre ray rejects full footprint");
                    obstacle.SetActive(false);
                    UnityEngine.Object.Destroy(obstacle);
                    Physics.SyncTransforms();
                    Check(placement.TryPlace() && manager.PlacedCount == 1, "Place through actual camera ray");
                    trap = manager.GetComponentsInChildren<AnimalTrap>().Single();
                    Check(!placement.Evaluate(out _, out _), "Cannot overlap another trap");
                    Check(!trap.TryCatch(placement.GetComponent<CharacterController>()), "Player cannot trigger trap");
                    var crate = UnityEngine.Object.FindObjectsByType<PickupItem>().First(p => p.name == "Pickup Crate Small");
                    Check(!trap.TryCatch(crate.GetComponent<Collider>()), "Crate cannot trigger trap");
                    view.transform.rotation = Quaternion.LookRotation(Vector3.up);
                    Check(!placement.Evaluate(out _, out _) && !placement.TryPlace(), "Air rejected");
                    Check(FindSite(), "Second free site");
                    Check(placement.TryPlace(), "Second trap placed");
                    Check(FindSite() && placement.TryPlace(), "Third trap placed");
                    Check(manager.PlacedCount == 3 && manager.TryPlace(site + Vector3.right * 4, Quaternion.identity) == null, "Authoritative cap rejects fourth");
                    Check(!placement.TryPlace(), "Placement also rejects fourth");
                    AnimalTrap empty = manager.GetComponentsInChildren<AnimalTrap>().First(t => t != trap);
                    Check(empty.TryInteract(holder) && manager.PlacedCount == 2, "E retrieves empty trap and frees capacity");
                    Check(FindSite() && placement.TryPlace() && manager.PlacedCount == 3, "Retrieved trap can be reinstalled");
                    Check(manager.GetComponentsInChildren<AnimalTrap>().All(t => t.State == AnimalTrap.TrapState.Armed), "Reinstalled trap is armed");
                    // Leave one trap for the navigation test: the approach must not cross a different test trap.
                    foreach (AnimalTrap extra in manager.GetComponentsInChildren<AnimalTrap>())
                        if (extra != trap) Check(extra.TryInteract(holder), "Retrieve spare test traps");
                    Check(manager.PlacedCount == 1, "One trap remains for navigation check");
                    NavMeshAgent agent = deer[0].GetComponent<NavMeshAgent>();
                    bool walking = false;
                    foreach (Vector3 direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                    {
                        if (!NavMesh.SamplePosition(trap.transform.position + direction * 3, out NavMeshHit start, 0.8f, NavMesh.AllAreas)) continue;
                        var path = new NavMeshPath();
                        if (!NavMesh.CalculatePath(start.position, trap.transform.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                        agent.Warp(start.position);
                        agent.speed = 3;
                        agent.isStopped = false;
                        agent.SetDestination(trap.transform.position);
                        walking = true;
                        break;
                    }
                    Check(walking, "Navigation path to trap available");
                    Next();
                }
                else if (stage == 1)
                {
                    if (deer[0].State == DeerSedation.SedationState.Trapped)
                    {
                        Check(trap.State == AnimalTrap.TrapState.Used && trap.CaughtDeer == deer[0], "Actual trigger catches navigating deer");
                        Check(!deer[0].GetComponent<NavMeshAgent>().enabled && deer[0].GetComponent<DeerAI>().State == DeerAI.DeerState.Trapped, "AI and agent stopped");
                        Check(deer[0].GetComponent<Rigidbody>().constraints == RigidbodyConstraints.FreezeAll, "Caught body cannot escape");
                        Check(!trap.TryCatch(deer[1].GetComponent<CapsuleCollider>()), "One trap cannot catch two deer");
                        Check(!deer[0].ApplyDose(1), "Dart cannot corrupt trapped state");
                        Check(manager.Notification == "Олень попался в ловушку!", "Capture notification");
                        Check(trap.TryInteract(holder), "E path uses existing holder");
                        Check(holder.IsHolding && deer[0].State == DeerSedation.SedationState.Unconscious && trap.CaughtDeer == null,
                            "Release converts caught animal to carryable sleeping body");
                        Check(deer[0].GetComponent<Rigidbody>().constraints != RigidbodyConstraints.FreezeAll, "Carry restores constraints");
                        deer[0].SetDurations(0.5f, 120, 1);
                        Next();
                    }
                    else Check(elapsed < 8, "Navigating deer enters real trigger");
                }
                else if (stage == 2 && elapsed > 0.15f)
                {
                    Check(holder.IsHolding, "Holder retains caught deer");
                    MovePlayer(truck.transform.TransformPoint(new Vector3(0, 0.1f, -5.8f)));
                    placement.transform.rotation = truck.transform.rotation;
                    view.transform.localRotation = Quaternion.identity;
                    holder.Drop();
                    Rigidbody body = deer[0].GetComponent<Rigidbody>();
                    Quaternion rotation = truck.transform.rotation * Quaternion.Euler(0, 90, 0);
                    body.rotation = rotation;
                    body.position = truck.transform.TransformPoint(new Vector3(0, 1.3f, -2.65f)) - rotation * deer[0].GetComponent<BoxCollider>().center;
                    body.linearVelocity = body.angularVelocity = Vector3.zero;
                    Physics.SyncTransforms();
                    Check(trap.TryInteract(holder) && manager.PlacedCount == 0, "Used empty trap can be retrieved");
                    Next();
                }
                else if (stage == 3)
                {
                    if (truck.Zone.IsReady(deer[0]))
                    {
                        Check(interaction.TrySubmitDelivery(), "Caught deer submits through existing E delivery path");
                        var rewards = UnityEngine.Object.FindAnyObjectByType<DeliveryManager>();
                        Check(rewards.Balance == 100 && rewards.DeliveredCount == 1, "Caught animal pays usual 100");
                        Check(!interaction.TrySubmitDelivery() && rewards.Balance == 100, "No duplicate reward");
                        Next();
                    }
                    else Check(elapsed < 10, "Caught animal settles in truck");
                }
                else if (stage == 4 && elapsed > 0.05f)
                {
                    // E delivery deliberately blocks another action in the same frame.
                    Check(FindSite() && placement.TryPlace(), "Used trap can be reinstalled after delivery");
                    trap = manager.GetComponentsInChildren<AnimalTrap>().Single();
                    Check(trap.State == AnimalTrap.TrapState.Armed, "Reinstalled used trap resets to Armed");
                    Check(deer[1].GetComponent<NavMeshAgent>().Warp(trap.transform.position), "Second animal enters reinstalled trap");
                    Next();
                }
                else if (stage == 5)
                {
                    if (trap.CaughtDeer == deer[1])
                    {
                        Check(trap.State == AnimalTrap.TrapState.Used && deer[1].State == DeerSedation.SedationState.Trapped, "Reinstalled used trap catches again");
                        Debug.Log("Trap Play checks PASSED: Input System key 3, tool exclusivity, valid camera-ray placement, air/obstacle/overlap rejection, cap 3, empty/used retrieval and rearming, real NavMesh trigger catch, frozen AI, single catch, capture HUD notification, existing pickup/drop and delivery +100 without duplicates, second catch after reinstalling a used trap. Delivery transport is positioned by the test, not manually walked.");
                        Finish(0);
                    }
                    else Check(elapsed < 3, "Reinstalled trap activates again");
                }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static bool FindSite()
        {
            for (int x = -24; x <= 24; x += 4)
                for (int z = -24; z <= 24; z += 4)
                {
                    Vector3 target = new Vector3(x, 0, z);
                    MovePlayer(target + new Vector3(0, 0.1f, -2.8f));
                    view.transform.rotation = Quaternion.LookRotation(target - view.transform.position);
                    Physics.SyncTransforms();
                    if (!placement.Evaluate(out Vector3 position, out _)) continue;
                    site = position;
                    return true;
                }
            return false;
        }
        private static void MovePlayer(Vector3 position)
        {
            CharacterController controller = placement.GetComponent<CharacterController>();
            controller.enabled = false;
            placement.transform.position = position;
            controller.enabled = true;
        }
        private static void TestBinding()
        {
#if ENABLE_INPUT_SYSTEM
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var action = (InputAction)typeof(PrototypeInput).GetField("selectTrap", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(placement.GetComponent<PrototypeInput>());
                Check(action.controls.Contains(keyboard.digit3Key), "Key 3 binding resolves");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit3));
                InputSystem.Update();
                Check(action.WasPressedThisFrame(), "Key 3 triggers actual runtime action");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                placement.GetComponent<PrototypeInput>().ReadFrame();
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
#endif
        }
        private static void Next() { stage++; began = Time.time; }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Trap check failed: " + message); }
        private static void Finish(int code) { EditorApplication.update -= Tick; EditorApplication.Exit(code); }
    }
}
