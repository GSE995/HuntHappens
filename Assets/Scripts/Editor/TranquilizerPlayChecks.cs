using System;
using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Interaction;
using HuntHappens.Player;
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
    public static class TranquilizerPlayChecks
    {
        private const string Pending = "HuntHappens.TranquilizerPlayChecks";
        private static int stage;
        private static float began;
        private static double deadline;
        private static TranquilizerGun gun;
        private static TranquilizerDart dart;
        private static DeerSedation deer;
        private static DeerPhysics physicsBody;
        private static DeerAI ai;
        private static NavMeshAgent agent;
        private static ItemHolder holder;
        private static FirstPersonMotor motor;
        private static Camera view;
        private static bool sawRecovery;
        private static Vector3 heldStart;

        static TranquilizerPlayChecks() => EditorApplication.playModeStateChanged += OnPlay;
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
        private static void OnPlay(PlayModeStateChange value)
        {
            if (value != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            stage = 0;
            sawRecovery = false;
            began = Time.time;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "Timed out at stage " + stage);
                float elapsed = Time.time - began;
                if (deer != null) sawRecovery |= deer.State == DeerSedation.SedationState.Recovering;
                if (stage == 0 && elapsed > 0.3f)
                {
                    gun = UnityEngine.Object.FindAnyObjectByType<TranquilizerGun>();
                    Check(gun != null, "Gun exists");
                    holder = gun.GetComponent<ItemHolder>();
                    motor = gun.GetComponent<FirstPersonMotor>();
                    motor.enabled = false; // Tests aim the camera directly, without changing saved player settings.
                    view = gun.GetComponentInChildren<Camera>();
                    deer = GameObject.Find("Prototype Deer/Deer 1").GetComponent<DeerSedation>();
                    physicsBody = deer.GetComponent<DeerPhysics>();
                    ai = deer.GetComponent<DeerAI>();
                    agent = deer.GetComponent<NavMeshAgent>();
                    ai.enabled = false;
                    agent.isStopped = true;
                    agent.ResetPath();
                    deer.SetDurations(1.5f, 3f, 0.8f);
                    Check(!deer.GetComponent<PickupItem>().CanPickUp(holder.MaximumMass), "Awake deer cannot be picked up");
                    view.transform.LookAt(deer.transform.position + Vector3.up * 1.1f);
                    gun.SelectTool(false);
                    Check(!gun.TryFire(), "Interaction mode cannot fire");
                    TestToolBindings();
                    gun.SelectTool(true);
                    Check(gun.TryFire(), "Selected gun fires");
                    dart = gun.LastDart;
                    Check(dart != null && dart.GetComponent<Rigidbody>().linearVelocity.magnitude > 20, "Dart has physical velocity");
                    Check(!gun.TryFire(), "Cooldown rejects immediate second shot");
                    Next();
                }
                else if (stage == 1)
                {
                    if (deer.State == DeerSedation.SedationState.Sedated)
                    {
                        Check(dart != null && dart.HasImpacted, "Physical projectile reports one impact");
                        Check(agent.enabled && physicsBody.Body.isKinematic, "Sedation does not instantly enable dynamic physics");
                        Check(ai.State == DeerAI.DeerState.Sedated, "AI reports Sedated");
                        Next();
                    }
                    else Check(elapsed < 3, "Dart must hit deer");
                }
                else if (stage == 2)
                {
                    if (deer.State == DeerSedation.SedationState.Unconscious)
                    {
                        Check(!agent.enabled && !physicsBody.Body.isKinematic && physicsBody.Body.useGravity, "Sleep hands control to physics");
                        Check(deer.GetComponent<PickupItem>().enabled, "Sleep enables pickup");
                        Check(deer.GetComponent<BoxCollider>().enabled && !deer.GetComponent<CapsuleCollider>().enabled, "Only sleep collider is active");
                        Next();
                    }
                    else Check(elapsed < 3, "Sedated transitions to Unconscious");
                }
                else if (stage == 3 && elapsed > 0.5f)
                {
                    Check(physicsBody.Body.position.y > -0.4f, "Sleeping deer collides with ground");
                    var controller = motor.GetComponent<CharacterController>();
                    controller.enabled = false;
                    Vector3 position = physicsBody.Body.worldCenterOfMass - Vector3.forward * 2.5f;
                    position.y = 0.1f;
                    motor.transform.position = position;
                    motor.transform.rotation = Quaternion.identity;
                    view.transform.localRotation = Quaternion.identity;
                    controller.enabled = true;
                    Check(holder.TryPickUp(deer.GetComponent<PickupItem>()), "Heavy deer is carried by existing holder");
                    Check(!gun.TryFire(), "Held animal prevents firing");
                    heldStart = physicsBody.Body.worldCenterOfMass;
                    Next();
                }
                else if (stage == 4 && elapsed > 3.5f)
                {
                    Check(holder.IsHolding && deer.State == DeerSedation.SedationState.Unconscious, "Wake timer is deferred while held");
                    Check(physicsBody.Body.worldCenterOfMass.y > heldStart.y + 0.4f, "Carry lifts deer physically");
                    motor.GetComponent<CharacterController>().Move(Vector3.right * 0.8f);
                    Next();
                }
                else if (stage == 5 && elapsed > 0.8f)
                {
                    Check(holder.IsHolding && !agent.enabled, "Carried animal follows player without agent conflict");
                    holder.Drop();
                    Check(!holder.IsHolding && physicsBody.Body.useGravity, "Drop restores gravity");
                    Check(holder.TryPickUp(deer.GetComponent<PickupItem>()), "Dropped deer can be picked up again");
                    holder.Throw();
                    Next();
                }
                else if (stage == 6 && elapsed > 0.12f)
                {
                    Check(!holder.IsHolding && physicsBody.Body.linearVelocity.z > 0.5f, "Animal receives a forward throw impulse");
                    Next();
                }
                else if (stage == 7)
                {
                    if (deer.State == DeerSedation.SedationState.Awake)
                    {
                        Check(sawRecovery && agent.enabled && agent.isOnNavMesh && physicsBody.Body.isKinematic, "Recovery returns control to navigation");
                        Check(!deer.GetComponent<PickupItem>().enabled && deer.GetComponent<CapsuleCollider>().enabled, "Recovery restores standing collision and disables pickup");
                        Check(Vector3.Dot(deer.transform.up, Vector3.up) > 0.99f, "Recovered deer is upright");
                        ai.enabled = true;
                        Next();
                    }
                    else Check(elapsed < 15, "Deer should recover on clear ground after throw");
                }
                else if (stage == 8 && elapsed > 0.6f)
                {
                    Check(ai.State != DeerAI.DeerState.Unconscious && ai.State != DeerAI.DeerState.Recovering, "Normal AI resumed");
                    Debug.Log("Tranquilizer Play checks PASSED: tool modes, cooldown, physical dart hit, delayed sedation, dynamic sleep, heavy pickup/carry/drop/throw, deferred wake while held, recovery and NavMesh resume.");
                    Finish(0);
                }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private static void TestToolBindings()
        {
#if ENABLE_INPUT_SYSTEM
            Cursor.lockState = CursorLockMode.Locked;
            bool captured = Cursor.lockState == CursorLockMode.Locked;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                PrototypeInput input = gun.GetComponent<PrototypeInput>();
                // Verify the actual runtime actions even when the batch window cannot lock a cursor.
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var selectGun = (InputAction)typeof(PrototypeInput).GetField("selectTranquilizer", flags).GetValue(input);
                var selectItems = (InputAction)typeof(PrototypeInput).GetField("selectInteraction", flags).GetValue(input);
                Check(selectGun.controls.Contains(keyboard.digit1Key), "Gun action resolves to keyboard 1");
                Check(selectItems.controls.Contains(keyboard.digit2Key), "Interaction action resolves to keyboard 2");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
                InputSystem.Update();
                Check(selectGun.WasPressedThisFrame() && !selectItems.WasPressedThisFrame(), "Keyboard 1 triggers gun action only");
                if (captured)
                {
                    input.ReadFrame();
                    Check(input.SelectTranquilizerPressed, "Input System digit1 binding");
                    gun.SendMessage("LateUpdate");
                    Check(gun.Selected, "Digit1 selects gun");
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
                InputSystem.Update();
                Check(selectItems.WasPressedThisFrame() && !selectGun.IsPressed(), "Keyboard 2 triggers interaction action");
                if (captured)
                {
                    input.ReadFrame();
                    Check(input.SelectInteractionPressed, "Input System digit2 binding");
                    gun.SendMessage("LateUpdate");
                    Check(!gun.Selected, "Digit2 selects interaction");
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                input.ReadFrame();
                Debug.Log("Tool Input System bindings PASSED: keyboard 1/2 resolved and triggered without requiring cursor capture.");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            }
#endif
        }
        private static void Next() { stage++; began = Time.time; }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("Tranquilizer Play check failed: " + message); }
        private static void Finish(int code) { EditorApplication.update -= Tick; EditorApplication.Exit(code); }
    }
}
