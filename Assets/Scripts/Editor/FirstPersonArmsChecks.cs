using System;
using System.IO;
using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Shop;
using HuntHappens.Traps;
using HuntHappens.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class FirstPersonArmsChecks
    {
        private const string Pending = "HuntHappens.ArmsChecks";
        private static int stage;
        private static float began;
        private static double deadline;
        private static FirstPersonArms arms;
        private static FirstPersonArmsAnimator animator;
        private static FirstPersonMotor motor;
        private static CharacterController controller;
        private static Camera view;
        private static ItemHolder holder;
        private static TranquilizerGun gun;
        private static TrapPlacement placement;
        private static PickupItem crate;
        private static DeerSedation deer;
        private static ShopManager shop;
        private static Vector3 sampleHand;
        private static GameObject wall;
        private static bool walked;
        private static int previousFrame;
        private static PlayerBodyAnimation playerBody;
        static FirstPersonArmsChecks() => EditorApplication.playModeStateChanged += OnPlay;
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            EditorApplication.EnterPlaymode();
        }
        private static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            stage = 0; walked = false;
            previousFrame = -1;
            began = Time.time;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "Timeout stage " + stage);
                if (Time.frameCount == previousFrame) return;
                previousFrame = Time.frameCount;
                float elapsed = Time.time - began;
                if (stage == 0 && elapsed > 0.7f)
                {
                    arms = UnityEngine.Object.FindAnyObjectByType<FirstPersonArms>();
                    animator = arms.GetComponent<FirstPersonArmsAnimator>();
                    motor = UnityEngine.Object.FindAnyObjectByType<FirstPersonMotor>();
                    controller = motor.GetComponent<CharacterController>();
                    playerBody = motor.GetComponentInChildren<PlayerBodyAnimation>();
                    Check(playerBody != null && playerBody.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>().Length == 0,
                        "Hero body belongs to player without autonomous navigation");
                    view = arms.View; holder = arms.Holder; gun = arms.Gun; placement = arms.Placement;
                    shop = UnityEngine.Object.FindAnyObjectByType<ShopManager>();
                    motor.enabled = false;
                    gun.SelectTool(false);
                    Check(FindMovementSpot(), "Clear ground for movement checks");
                    Check(arms.transform.parent == view.transform && arms.GetComponentsInChildren<Collider>(true).Length == 0 &&
                        arms.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Camera child with no physical bodies");
                    foreach (Renderer renderer in arms.GetComponentsInChildren<Renderer>(true))
                        Check(renderer.shadowCastingMode == ShadowCastingMode.Off, "No world shadows from arms");
                    sampleHand = arms.LeftHand.localPosition;
                    Next();
                }
                else if (stage == 1 && elapsed > 0.4f)
                {
                    Check(arms.Mode == FirstPersonArms.VisualMode.Empty && Vector3.Distance(sampleHand, arms.LeftHand.localPosition) > 0.0001f,
                        "Idle breathing moves empty hands");
                    Check(VisibleHands(), "Both hands visible at startup");
                    Capture("Arms-Idle");
                    var look = motor.GetComponent<FirstPersonLook>();
                    look.ApplyLook(new Vector2(0, -10000));
                    Check(Mathf.Abs(Mathf.DeltaAngle(0, view.transform.localEulerAngles.x) - 55f) < 0.01f,
                        "Downward view stops at 55 degrees");
                    look.ApplyLook(new Vector2(0, -10000));
                    Check(Mathf.Abs(Mathf.DeltaAngle(0, view.transform.localEulerAngles.x) - 55f) < 0.01f,
                        "Further mouse input cannot look through the body");
                    Capture("Player-Body-LookDown");
                    look.ApplyLook(new Vector2(0, 10000));
                    Check(Mathf.Abs(Mathf.DeltaAngle(0, view.transform.localEulerAngles.x) + 85f) < 0.01f,
                        "Upward view retains its original range");
                    look.ApplyLook(new Vector2(0, -85f / 0.12f));
                    sampleHand = arms.LeftHand.localPosition;
                    Next();
                }
                else if (stage == 2)
                {
                    controller.Move((motor.transform.forward * 4 + Vector3.down * 2) * Time.deltaTime);
                    walked |= animator.WalkSpeed > 1 && Vector3.Distance(sampleHand, arms.LeftHand.localPosition) > 0.003f;
                    if (elapsed > 0.65f)
                    {
                        Check(walked && playerBody.GetComponentInChildren<Animator>().speed > 0.1f, "Movement drives hands and body animation");
                        Next();
                    }
                }
                else if (stage == 3)
                {
                    controller.Move(Vector3.up * (3 * Time.deltaTime));
                    if (elapsed > 0.2f)
                    {
                        playerBody.GetComponent<HuntHappens.Characters.HeroGrounding>().Measure(out float feet, out _);
                        Check(Mathf.Abs(feet - motor.transform.position.y) < 0.2f, "Body follows player during jump");
                        Check(animator.JumpOffset > 0.015f, "Ascent lifts hands"); Next();
                    }
                }
                else if (stage == 4)
                {
                    controller.Move(Vector3.down * (5 * Time.deltaTime));
                    if (controller.isGrounded && animator.JumpOffset < -0.005f)
                    { gun.SelectTool(true); Next(); }
                    else Check(elapsed < 4, "Landing produces a short dip");
                }
                else if (stage == 5 && elapsed > 0.45f)
                {
                    Check(arms.Mode == FirstPersonArms.VisualMode.Tranquilizer && gun.WeaponVisual.gameObject.activeSelf && VisibleHands(), "One pair holds original gun");
                    Check(gun.WeaponVisual.parent == view.transform && UnityEngine.Object.FindObjectsByType<TranquilizerGun>().Length == 1,
                        "No duplicate weapon or parent change");
                    Capture("Arms-Gun");
                    Transform muzzle = gun.WeaponVisual.Find("Muzzle");
                    Vector3 spawn = muzzle.position;
                    Check(gun.TryFire() && animator.ShotPulse > 0.9f, "Successful shot event reaches animation");
                    Check(Vector3.Distance(gun.LastDart.transform.position, spawn) < 0.001f, "Dart spawn=" + gun.LastDart.transform.position +
                        " expected muzzle=" + spawn + " eye=" + view.transform.position);
                    sampleHand = arms.RightHand.localPosition;
                    Next();
                }
                else if (stage == 6 && elapsed > 0.08f)
                {
                    Check(animator.ShotPulse > 0.1f && Vector3.Distance(sampleHand, arms.RightHand.localPosition) > 0.002f, "Hands follow original weapon recoil");
                    crate = UnityEngine.Object.FindObjectsByType<PickupItem>().First(p => p.name == "Pickup Crate Small");
                    crate.Body.position = view.transform.position + view.transform.forward * 2;
                    crate.Body.linearVelocity = Vector3.zero;
                    Physics.SyncTransforms();
                    Check(holder.TryPickUp(crate), "Existing crate pickup");
                    Next();
                }
                else if (stage == 7 && elapsed > 1f)
                {
                    Check(arms.Mode == FirstPersonArms.VisualMode.Carry && !gun.WeaponVisual.gameObject.activeSelf && arms.LeftHand.localPosition.z > 0.65f,
                        "Carry takes priority over selected gun and extends hands");
                    Vector3 carryPosition = view.transform.InverseTransformPoint(crate.Body.worldCenterOfMass);
                    Check(carryPosition.z > 1.25f && carryPosition.z < 1.6f && carryPosition.y < -0.1f,
                        "Carried crate leaves clearance for hands and stays below eye level");
                    Capture("Arms-Crate");
                    holder.Drop();
                    crate.Body.position = view.transform.position + view.transform.right * 4;
                    deer = UnityEngine.Object.FindObjectsByType<DeerSedation>().First();
                    deer.GetComponent<DeerAI>().enabled = false;
                    deer.SetDurations(0.5f, 120, 1);
                    deer.ApplyDose(1);
                    Next();
                }
                else if (stage == 8 && elapsed > 0.8f)
                {
                    Check(deer.State == DeerSedation.SedationState.Unconscious, "Deer sleeping");
                    var body = deer.GetComponent<Rigidbody>();
                    body.rotation = Quaternion.Euler(0, motor.transform.eulerAngles.y + 90, 0);
                    body.position = view.transform.position + view.transform.forward * 2.8f - body.rotation * deer.GetComponent<BoxCollider>().center;
                    body.linearVelocity = body.angularVelocity = Vector3.zero;
                    Check(holder.TryPickUp(deer.GetComponent<PickupItem>()), "Existing animal carry");
                    Next();
                }
                else if (stage == 9 && elapsed > 0.35f)
                {
                    Check(arms.Mode == FirstPersonArms.VisualMode.Carry && VisibleHands() && !gun.WeaponVisual.gameObject.activeSelf, "Deer uses same carry pose");
                    Capture("Arms-Deer");
                    holder.Throw();
                    Next();
                }
                else if (stage == 10 && elapsed > 0.07f)
                {
                    Check(animator.ThrowPulse > 0.2f && animator.CurrentMode == FirstPersonArms.VisualMode.Carry, "Throw animation survives returning to selected gun");
                    Check(gun.WeaponVisual.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), "Weapon renderers hidden during throw without touching muzzle");
                    Check(!holder.IsHolding && Vector3.Dot(deer.GetComponent<Rigidbody>().linearVelocity, view.transform.forward) > 0.3f, "Physical throw retained");
                    Next();
                }
                else if (stage == 11 && elapsed > 0.4f)
                {
                    Check(animator.CurrentMode == FirstPersonArms.VisualMode.Tranquilizer && gun.WeaponVisual.GetComponentsInChildren<Renderer>(true).All(r => r.enabled),
                        "Gun grip and renderer visibility restored after throw");
                    gun.SelectTrapTool();
                    Check(FindTrapSite(), "Supported location for trap");
                    Next();
                }
                else if (stage == 12 && elapsed > 0.35f)
                {
                    Check(arms.Mode == FirstPersonArms.VisualMode.Trap && arms.MiniatureTrap.gameObject.activeInHierarchy, "Small trap appears in hand");
                    Check(arms.MiniatureTrap.GetComponentInChildren<AnimalTrap>() == null, "Miniature is presentation only");
                    Capture("Arms-Trap");
                    Check(placement.TryPlace(), "Original placement and preview logic still work");
                    Next();
                }
                else if (stage == 13 && elapsed > 0.08f)
                {
                    Check(animator.PlacementPulse > 0.2f && placement.Manager.PlacedCount == 1, "Only successful placement starts hand animation");
                    view.transform.localRotation = Quaternion.identity;
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = view.transform.position + view.transform.forward * 0.55f;
                    wall.transform.rotation = view.transform.rotation;
                    wall.transform.localScale = new Vector3(2, 2, 0.1f);
                    Physics.SyncTransforms();
                    Next();
                }
                else if (stage == 14 && elapsed > 0.4f)
                {
                    Check(animator.Obstruction > 0.5f && arms.LeftHand.localPosition.z < 0.35f, "Near wall retracts hands safely");
                    wall.SetActive(false);
                    UnityEngine.Object.Destroy(wall);
                    MovePlayer(shop.Kiosk.InteractionPoint.position - shop.Kiosk.transform.forward * 1.6f - Vector3.up * 1.2f);
                    Physics.SyncTransforms();
                    Check(shop.TryOpen(), "Open original shop");
                    Next();
                }
                else if (stage == 15 && elapsed > 0.2f)
                {
                    Check(arms.Mode == FirstPersonArms.VisualMode.Hidden && arms.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), "Shop hides all hands/miniature renderers");
                    shop.Close();
                    gun.SelectTool(false);
                    Next();
                }
                else if (stage == 16 && elapsed > 0.4f)
                {
                    Check(arms.Mode == FirstPersonArms.VisualMode.Empty && VisibleHands() && !arms.Input.GameplayBlocked, "Shop closing restores hands and input");
                    Check(UnityEngine.Object.FindObjectsByType<Camera>().Length == 1 && Time.timeScale == 1, "No extra camera or time pause");
                    Debug.Log("First person arms Play checks PASSED: idle/walk/ascent/landing, original gun grip and recoil, unchanged dart spawn, crate/deer carry priority, throw and weapon visibility restore, trap miniature and successful placement event, wall protection, shop hiding/resume, no colliders or extra cameras. Screenshots generated when running with graphics; player transport is driven by the test.");
                    Finish(0);
                }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static bool VisibleHands()
        {
            var heroArms = arms.GetComponentInChildren<HeroFirstPersonArms>();
            return heroArms != null && heroArms.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.enabled && r.sharedMesh == AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Hero/FirstPersonArms.asset"));
        }
        private static void MovePlayer(Vector3 position)
        { controller.enabled = false; motor.transform.position = position; controller.enabled = true; }
        private static bool FindMovementSpot()
        {
            for (int x = 12; x <= 32; x += 4)
                for (int z = -24; z < 20; z += 4)
                {
                    if (!NavMesh.SamplePosition(new Vector3(x, 0, z), out NavMeshHit point, 0.6f, NavMesh.AllAreas)) continue;
                    if (Physics.OverlapBox(point.position + new Vector3(0, 1.5f, 2), new Vector3(1.5f, 1.4f, 5), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Length != 0) continue;
                    MovePlayer(point.position + Vector3.up * 0.1f);
                    motor.transform.rotation = Quaternion.identity;
                    view.transform.localRotation = Quaternion.identity;
                    controller.Move(Vector3.down * 0.5f);
                    Physics.SyncTransforms();
                    return true;
                }
            return false;
        }
        private static bool FindTrapSite()
        {
            for (int x = -24; x <= 24; x += 4)
                for (int z = -24; z <= 24; z += 4)
                {
                    Vector3 point = new Vector3(x, 0, z);
                    MovePlayer(point + new Vector3(0, 0.1f, -2.8f));
                    motor.transform.rotation = Quaternion.identity;
                    view.transform.rotation = Quaternion.LookRotation(point - view.transform.position);
                    Physics.SyncTransforms();
                    if (placement.Evaluate(out _, out _)) return true;
                }
            return false;
        }
        private static void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(view, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image); }
        }
        private static void Next() { stage++; began = Time.time; }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Arms check failed: " + message); }
        private static void Finish(int code) { EditorApplication.update -= Tick; EditorApplication.Exit(code); }
    }
}
