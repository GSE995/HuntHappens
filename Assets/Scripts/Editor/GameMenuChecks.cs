using System;
using System.Collections;
using System.Linq;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Shop;
using HuntHappens.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class GameMenuChecks
    {
        private const string Key = "HuntHappens.GameMenuChecks";
        static GameMenuChecks()
        {
            if (SessionState.GetBool(Key, false)) ShopProgress.InventoryChecksRunning = true;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                    new GameObject("Game Menu Checks").AddComponent<GameMenuCheckRunner>();
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    if (SessionState.GetBool(Key + "testExists", false))
                        PlayerPrefs.SetString(ShopProgress.TestKey, SessionState.GetString(Key + "testSave", ""));
                    else PlayerPrefs.DeleteKey(ShopProgress.TestKey);
                    PlayerPrefs.Save();
                    ShopProgress.InventoryChecksRunning = false;
                    SessionState.SetBool(Key, false);
                }
            };
        }
        public static void Begin()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Start in Edit Mode");
            SessionState.SetString(Key + "playerSave", PlayerPrefs.GetString(ShopProgress.PlayerKey, ""));
            SessionState.SetBool(Key + "testExists", PlayerPrefs.HasKey(ShopProgress.TestKey));
            SessionState.SetString(Key + "testSave", PlayerPrefs.GetString(ShopProgress.TestKey, ""));
            SessionState.SetString(Key + "result", "RUNNING");
            SessionState.SetBool(Key, true);
            ShopProgress.InventoryChecksRunning = true;
            ShopProgress.Reset();
            EditorApplication.EnterPlaymode();
        }
        public static string Result() => SessionState.GetString(Key + "result", "Not run");
        public static void Finish(string result)
        {
            SessionState.SetString(Key + "result", result);
            Debug.Log(result);
            EditorApplication.ExitPlaymode();
        }
        public static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Game menu: " + message);
        }
        public static void CheckPlayerSave() => Check(
            PlayerPrefs.GetString(ShopProgress.PlayerKey, "") == SessionState.GetString(Key + "playerSave", ""),
            "Player save unchanged");
    }

    public sealed class GameMenuCheckRunner : MonoBehaviour
    {
        private void Start() { DontDestroyOnLoad(gameObject); StartCoroutine(Safe()); }
        private IEnumerator Safe()
        {
            var routine = Run();
            while (true)
            {
                object next;
                try { if (!routine.MoveNext()) break; next = routine.Current; }
                catch (Exception error) { GameMenuChecks.Finish("Game menu checks FAILED: " + error); yield break; }
                yield return next;
            }
            GameMenuChecks.Finish("Game menu checks PASSED: $15 start, modal pause, button raycasts/clicks, continue, inventory conflict, scene restart, purchases/inventory/quest/world reset, scene reload persistence, player save unchanged.");
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(.5f);
            var menu = FindAnyObjectByType<GameMenu>();
            var shop = FindAnyObjectByType<ShopManager>();
            var input = menu.GetComponent<InteractionHUD>().PlayerInput;
            GameMenuChecks.Check(shop.Wallet.Balance == 15, "Initial wallet is exactly $15 without advance");
            GameMenuChecks.Check(FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One EventSystem");
            shop.Wallet.RestoreBalance(137);
            var sleeping = ShopProgress.Load();
            sleeping.rats = 1;
            double wakeAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 + 60;
            sleeping.carriedRats = new[] { new ShopProgress.CarriedRat { kind = 0, home = Vector3.one * 99999, wakeAt = wakeAt } };
            PlayerPrefs.SetString(ShopProgress.Key, JsonUtility.ToJson(sleeping));
            input.GetComponent<HuntHappens.SmallGame.RatHunting>().Reload();
            menu.Open();
            GameMenuChecks.Check(menu.IsOpen && input.GameplayBlocked && Time.timeScale == 0, "Menu pauses and blocks input");
            float pausedTime = Time.time;
            Vector3 position = input.transform.position;
            yield return new WaitForSecondsRealtime(.35f);
            GameMenuChecks.Check(Time.time == pausedTime && input.transform.position == position, "Simulation and player stay paused");
            var field = menu.GetComponent<InteractionHUD>().Field;
            field.SetOpen(true);
            GameMenuChecks.Check(!field.IsOpen && !input.ThrowPressed, "Inventory and shooting blocked");
            Click(menu.ContinueButton);
            GameMenuChecks.Check(!menu.IsOpen && !input.GameplayBlocked && Time.timeScale == 1 && shop.Wallet.Balance == 137, "Continue preserves wallet and resumes");
            GameMenuChecks.Check(ShopProgress.Load().carriedRats[0].wakeAt >= wakeAt + .3, "Carried rat sleep deadline shifts by menu duration");
            field.SetOpen(true); menu.Toggle();
            GameMenuChecks.Check(field.IsOpen && !menu.IsOpen, "Menu does not steal inventory's Escape");
            field.SetOpen(false);
            shop.Upgrades.RestoreLevels(new[] { 1, 2, 1, 1 }); shop.SaveProgress();
            var data = ShopProgress.Load();
            data.rats = 2; data.cartPurchased = true; data.returnedDeerIds = new[] { "test-deer" };
            data.inventoryVersion = 2;
            data.inventory = new[] { new InventoryEntry { kind = InventoryKind.Gun } };
            data.darts = 9;
            PlayerPrefs.SetString(ShopProgress.Key, JsonUtility.ToJson(data)); PlayerPrefs.Save();
            input.GetComponent<PlayerHealth>().TakeDamage(10);
            new GameObject("Menu Reset World Probe");
            menu.Open();
            yield return null; // GraphicRaycaster depth becomes valid after the canvas renders.
            Click(menu.RestartButton);
            yield return new WaitForSecondsRealtime(.6f);
            menu = FindAnyObjectByType<GameMenu>(); shop = FindAnyObjectByType<ShopManager>();
            input = menu.GetComponent<InteractionHUD>().PlayerInput;
            GameMenuChecks.Check(menu != null && !menu.IsOpen && Time.timeScale == 1 && !input.GameplayBlocked, "Restart starts gameplay");
            GameMenuChecks.Check(shop.Wallet.Balance == 15 && shop.Upgrades.CopyLevels().All(level => level == 0), "Restart resets money and upgrades");
            GameMenuChecks.Check(input.GetComponent<PlayerHealth>().Current == 100 && GameObject.Find("Menu Reset World Probe") == null, "Restart resets health and world");
            data = ShopProgress.Load();
            GameMenuChecks.Check(!data.cartPurchased && data.rats == 0 && data.returnedDeerIds.Length == 0 && data.darts == 0 &&
                input.GetComponent<PlayerInventory>().Count(InventoryKind.Gun) == 0 && shop.Wallet.DeliveredCount == 0, "Restart clears ownership, inventory and quest");
            shop.SaveProgress();
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
            yield return new WaitForSecondsRealtime(.6f);
            shop = FindAnyObjectByType<ShopManager>();
            GameMenuChecks.Check(shop.Wallet.Balance == 15, "Reload keeps exactly $15 without advance");
            GameMenuChecks.CheckPlayerSave();
        }

        private static void Click(UnityEngine.UI.Button button)
        {
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position), button = PointerEventData.InputButton.Left };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            GameMenuChecks.Check(hits.Count > 0 && hits[0].gameObject == button.gameObject, button.name + " receives topmost raycast");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
