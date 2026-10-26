using System;
using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Shop;
using HuntHappens.Traps;
using HuntHappens.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class ShopPlayChecks
    {
        private const string Pending = "HuntHappens.ShopPlayChecks";
        private static int stage;
        private static float began;
        private static float hitAt;
        private static double deadline;
        private static ShopManager shop;
        private static ShopUI ui;
        private static PrototypeInput input;
        private static PlayerInteraction interaction;
        private static ItemHolder holder;
        private static TranquilizerGun gun;
        private static TrapPlacement placement;
        private static Camera view;
        private static DeerSedation[] deer;
        private static PickupTruckDelivery truck;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static string playerSaveBefore;
        private static int purchaseIndex;
        private static InputSettings.BackgroundBehavior background;
        private static InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private static readonly UpgradeKind[] Purchases =
        {
            UpgradeKind.StrongerDarts, UpgradeKind.StrongerDarts,
            UpgradeKind.MoreTraps, UpgradeKind.MoreTraps,
            UpgradeKind.StrongerArms, UpgradeKind.StrongerArms, UpgradeKind.StrongerArms,
            UpgradeKind.BetterContracts, UpgradeKind.BetterContracts, UpgradeKind.BetterContracts
        };
        static ShopPlayChecks() => EditorApplication.playModeStateChanged += OnPlay;
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
            stage = purchaseIndex = 0;
            began = Time.time;
            deadline = EditorApplication.timeSinceStartup + 150;
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "Timeout stage " + stage);
                float elapsed = Time.time - began;
                if (stage == 0 && elapsed > 0.6f)
                {
                    shop = UnityEngine.Object.FindAnyObjectByType<ShopManager>();
                    ui = shop.GetComponent<ShopUI>();
                    interaction = UnityEngine.Object.FindAnyObjectByType<PlayerInteraction>();
                    input = interaction.GetComponent<PrototypeInput>();
                    holder = interaction.GetComponent<ItemHolder>();
                    gun = interaction.GetComponent<TranquilizerGun>();
                    placement = interaction.GetComponent<TrapPlacement>();
                    view = interaction.GetComponentInChildren<Camera>();
                    interaction.GetComponent<FirstPersonMotor>().enabled = false;
                    truck = UnityEngine.Object.FindAnyObjectByType<PickupTruckDelivery>();
                    deer = UnityEngine.Object.FindObjectsByType<DeerSedation>();
                    foreach (var animal in deer) animal.GetComponent<DeerAI>().enabled = false;
                    playerSaveBefore = PlayerPrefs.GetString(ShopProgress.PlayerKey, "");
                    Check(shop.Wallet.Balance == ShopProgress.StartingBalance && shop.Upgrades.CopyLevels().All(l => l == 0), "Missing save starts at $15");
                    shop.Wallet.RestoreBalance(0); // Exercise insufficient funds before funding through deliveries.
                    int[][] prices = { new[] { 200, 400, 700 }, new[] { 150, 300, 600 }, new[] { 200, 350, 600 }, new[] { 300, 500, 800 } };
                    for (int i = 0; i < 4; i++)
                        for (int level = 0; level < 3; level++) Check(shop.Upgrades.Definition((UpgradeKind)i).NextPrice(level) == prices[i][level], "Configured price table");
                    Check(shop.Upgrades.Definition(UpgradeKind.MoreTraps).NextPrice(3) == -1, "No price above maximum");
                    MoveToShop();
                    Check(interaction.TryOpenShop() && ui.Visible && input.GameplayBlocked, "Existing E opens modal shop");
                    Check(Cursor.visible && Cursor.lockState == CursorLockMode.None && Time.timeScale == 1, "Cursor unlocked without pausing world");
                    var uiInput = UnityEngine.Object.FindAnyObjectByType<InputSystemUIInputModule>();
                    Check(uiInput != null && uiInput.point.action != null && uiInput.leftClick.action != null, "UI uses configured Input System pointer/click actions");
                    Check(!shop.TryBuy(UpgradeKind.MoreTraps) && shop.Wallet.Balance == 0 && shop.Message == "Недостаточно денег", "Insufficient funds do not charge");
                    gun.SelectTool(true);
                    Check(!gun.TryFire(), "Shop blocks direct firing");
                    gun.SelectTrapTool();
                    Check(!placement.TryPlace(), "Shop blocks trap placement");
                    var crate = UnityEngine.Object.FindObjectsByType<PickupItem>().First(p => p.name == "Pickup Crate Small");
                    Check(!holder.TryPickUp(crate) && !interaction.TrySubmitDelivery(), "Shop blocks pickup and delivery");
                    background = InputSystem.settings.backgroundBehavior;
                    editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    mouse = InputSystem.AddDevice<Mouse>();
                    Quaternion rotation = view.transform.rotation;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space, Key.Digit3));
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100, 60), buttons = 1 });
                    InputSystem.Update();
                    interaction.GetComponent<FirstPersonMotor>().SendMessage("Update");
                    Check(input.Move == Vector2.zero && input.Look == Vector2.zero && !input.ThrowPressed && !input.SelectTrapPressed,
                        "Gameplay actions neutral while menu receives input");
                    Check(Quaternion.Angle(rotation, view.transform.rotation) < 0.01f, "Mouse does not rotate camera in shop");
                    ClearDevices();
                    Next();
                }
                else if (stage == 1 && elapsed > 0.15f)
                {
                    PressClose(Key.Escape);
                    Check(!shop.IsOpen && !ui.Visible && !input.GameplayBlocked && !input.ThrowPressed, "Escape closes and clears gameplay frame");
                    Next();
                }
                else if (stage == 2 && elapsed > 0.15f)
                {
                    Check(interaction.TryOpenShop(), "Shop reopens");
                    Next();
                }
                else if (stage == 3 && elapsed > 0.15f)
                {
                    PressClose(Key.E);
                    Check(!shop.IsOpen && !input.GameplayBlocked && !holder.IsHolding && placement.Manager.PlacedCount == 0, "E closes without pickup/placement");
                    foreach (var animal in deer.Take(2)) { animal.SetDurations(0.5f, 120, 1); animal.ApplyDose(1); }
                    Next();
                }
                else if (stage == 4 && elapsed > 0.8f)
                {
                    MovePlayer(truck.transform.TransformPoint(new Vector3(0, 0.1f, -5.8f)));
                    interaction.transform.rotation = truck.transform.rotation;
                    view.transform.localRotation = Quaternion.identity;
                    for (int i = 0; i < 2; i++)
                    {
                        Check(holder.TryPickUp(deer[i].GetComponent<PickupItem>()), "Existing carry before delivery");
                        holder.Drop();
                        var body = deer[i].GetComponent<Rigidbody>();
                        Quaternion rotation = truck.transform.rotation * Quaternion.Euler(0, 90, 0);
                        body.rotation = rotation;
                        body.position = truck.transform.TransformPoint(new Vector3(i == 0 ? -1.1f : 1.1f, 1.3f, -2.65f)) - rotation * deer[i].GetComponent<BoxCollider>().center;
                        body.linearVelocity = body.angularVelocity = Vector3.zero;
                    }
                    Physics.SyncTransforms();
                    Next();
                }
                else if (stage == 5)
                {
                    if (truck.Zone.ReadyCount == 2)
                    {
                        Check(interaction.TrySubmitDelivery() && shop.Wallet.Balance == 200, "Actual delivery earns shop money");
                        Check(ShopProgress.Load().balance == 200, "Reward saved immediately");
                        Next();
                    }
                    else Check(elapsed < 10, "Delivered bodies settle");
                }
                else if (stage == 6 && elapsed > 0.15f)
                {
                    MoveToShop();
                    Check(interaction.TryOpenShop(), "Open after delivery");
                    ClickBuy(UpgradeKind.MoreTraps);
                    Check(shop.Wallet.Balance == 50 && shop.Upgrades.Level(UpgradeKind.MoreTraps) == 1 && placement.Manager.MaximumTraps == 4,
                        "UI purchase spends earned 150 and immediately grants fourth trap");
                    shop.Wallet.RestoreBalance(10000); // Test funding for remaining levels; same real wallet, isolated test save.
                    ClickBuy(UpgradeKind.MoreTraps);
                    Check(shop.Wallet.Balance == 10000 && shop.Upgrades.Level(UpgradeKind.MoreTraps) == 1, "Rapid repeat cannot buy a second level even with sufficient money");
                    Next();
                }
                else if (stage == 7 && elapsed > 0.5f)
                {
                    Check(shop.TryBuy(UpgradeKind.StrongerDarts), "Buy stronger darts");
                    Check(Mathf.Approximately(gun.SedationDurationMultiplier, 0.8f), "Dart duration reduced 20 percent");
                    shop.Close();
                    Next();
                }
                else if (stage == 8 && elapsed > 0.15f)
                {
                    Check(PositionShot(deer[2]), "Clear position for real upgraded dart");
                    gun.SelectTool(true);
                    Check(gun.TryFire(), "Hunting resumes after closing shop");
                    Next();
                }
                else if (stage == 9)
                {
                    if (deer[2].State == DeerSedation.SedationState.Sedated)
                    {
                        Check(Mathf.Abs(deer[2].CurrentSedationDuration - 2.4f) < 0.01f, "Physical upgraded dart passes 2.4 second duration to deer");
                        hitAt = Time.time;
                        Next();
                    }
                    else Check(elapsed < 3, "Physical dart hits target");
                }
                else if (stage == 10)
                {
                    if (deer[2].State == DeerSedation.SedationState.Unconscious)
                    {
                        Check(Time.time - hitAt > 2 && Time.time - hitAt < 3, "Level one sleep is faster but not instantaneous");
                        MoveToShop();
                        Check(interaction.TryOpenShop(), "Return to shop after shot");
                        Next();
                    }
                    else Check(elapsed < 4, "Upgraded dart puts deer to sleep");
                }
                else if (stage == 11 && elapsed > 0.5f)
                {
                    if (purchaseIndex < Purchases.Length)
                    {
                        UpgradeKind kind = Purchases[purchaseIndex++];
                        int level = shop.Upgrades.Level(kind);
                        int price = shop.Upgrades.Definition(kind).NextPrice(level);
                        int balance = shop.Wallet.Balance;
                        Check(shop.TryBuy(kind) && shop.Upgrades.Level(kind) == level + 1 && shop.Wallet.Balance == balance - price,
                            "Each purchase advances exactly one level at configured price");
                        began = Time.time;
                    }
                    else
                    {
                        Check(shop.Upgrades.CopyLevels().All(l => l == 3), "All upgrades capped at 3");
                        Check(placement.Manager.MaximumTraps == 6 && Mathf.Abs(holder.MaximumMass - 140) < 0.01f && holder.CarryResponseMultiplier > 1,
                            "Capacity and safe carry stability actually upgraded");
                        Check(shop.Wallet.RewardPerDeer == 200 && Mathf.Approximately(gun.SedationDurationMultiplier, 0.5f), "Contracts and darts reach level three effects");
                        int balance = shop.Wallet.Balance;
                        foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind))) Check(!shop.TryBuy(kind), "Maximum level rejects purchase");
                        Check(balance == shop.Wallet.Balance && !shop.Wallet.TrySpend(-100), "Invalid and max-level purchases never charge");
                        string snapshot = PlayerPrefs.GetString(ShopProgress.Key);
                        shop.ResetProgress();
                        Check(shop.Wallet.Balance == ShopProgress.StartingBalance && placement.Manager.MaximumTraps == 3 && Mathf.Abs(holder.MaximumMass - 80) < 0.01f, "Reset restores base effects");
                        PlayerPrefs.SetString(ShopProgress.Key, snapshot);
                        PlayerPrefs.Save();
                        shop.ReloadProgress();
                        Check(shop.Wallet.Balance == balance && shop.Upgrades.CopyLevels().All(l => l == 3) && placement.Manager.MaximumTraps == 6,
                            "Saved balance/levels/effects reload from PlayerPrefs");
                        PlayerPrefs.SetString(ShopProgress.Key, "{broken save");
                        shop.ReloadProgress();
                        Check(shop.Wallet.Balance == ShopProgress.StartingBalance && shop.Upgrades.CopyLevels().All(l => l == 0), "Corrupt data falls back safely");
                        Check(PlayerPrefs.GetString(ShopProgress.PlayerKey, "") == playerSaveBefore, "Player progress untouched by batch test");
                        Check(UnityEngine.Object.FindObjectsByType<DeliveryManager>().Length == 1 && Time.timeScale == 1, "Single wallet and live world");
                        Debug.Log("Shop Play checks PASSED: all prices, real delivery funding, existing E open/E and Escape close, UI button purchase, insufficient funds, cooldown against double charge, all 12 levels, actual upgraded physical dart and non-instant sleep, real trap/carry/reward parameters, reset and PlayerPrefs round-trip/corrupt-data fallback, input/camera gating, hunting resume, player's save untouched. Transport into truck is positioned by test; layout needs visual review.");
                        Finish(0);
                    }
                }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void PressClose(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            input.ReadFrame();
            Check(input.MenuClosePressed, "Input System close key action");
            interaction.SendMessage("LateUpdate");
            ClearDevices();
        }
        private static void ClickBuy(UpgradeKind kind)
        {
            Canvas.ForceUpdateCanvases();
            var button = ui.BuyButton(kind);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position)
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            // Batch Game View may not render overlay canvases (depth remains -1).
            // ShopVisualChecks verifies the real raycast after explicitly rendering through URP.
            if (button.GetComponent<CanvasRenderer>().absoluteDepth >= 0)
                Check(hits.Count > 0 && hits[0].gameObject == button.gameObject, "Buy UI raycast at " + pointer.position +
                    ", depth " + button.GetComponent<CanvasRenderer>().absoluteDepth + ", first hit " + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static void ClearDevices()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update();
            input.ReadFrame();
        }
        private static void MoveToShop()
        {
            MovePlayer(shop.Kiosk.InteractionPoint.position - shop.Kiosk.transform.forward * 1.6f - Vector3.up * 1.2f);
            view.transform.rotation = Quaternion.LookRotation(shop.Kiosk.InteractionPoint.position - view.transform.position);
            Physics.SyncTransforms();
        }
        private static void MovePlayer(Vector3 position)
        {
            var controller = interaction.GetComponent<CharacterController>();
            controller.enabled = false;
            interaction.transform.position = position;
            controller.enabled = true;
        }
        private static bool PositionShot(DeerSedation animal)
        {
            for (int x = 15; x < 35; x += 4)
                for (int z = -25; z < 25; z += 4)
                {
                    if (!NavMesh.SamplePosition(new Vector3(x, 0, z), out NavMeshHit point, 0.6f, NavMesh.AllAreas)) continue;
                    if (Physics.OverlapBox(point.position + new Vector3(0, 1.5f, -2), new Vector3(1, 1.4f, 3.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Length != 0) continue;
                    animal.GetComponent<NavMeshAgent>().Warp(point.position);
                    MovePlayer(point.position - Vector3.forward * 5 + Vector3.up * 0.1f);
                    interaction.transform.rotation = Quaternion.identity;
                    view.transform.rotation = Quaternion.LookRotation(point.position + Vector3.up * 1.1f - view.transform.position);
                    Physics.SyncTransforms();
                    return true;
                }
            return false;
        }
        private static void Next() { stage++; began = Time.time; }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Shop check failed: " + message); }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            EditorApplication.Exit(code);
        }
    }
}
