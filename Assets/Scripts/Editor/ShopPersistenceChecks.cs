using System;
using System.Linq;
using HuntHappens.Interaction;
using HuntHappens.Shop;
using HuntHappens.Traps;
using HuntHappens.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class ShopPersistenceChecks
    {
        private const string Pending = "HuntHappens.ShopPersistenceChecks";
        private static bool reading;
        private static double deadline;
        static ShopPersistenceChecks() => EditorApplication.playModeStateChanged += OnPlay;
        public static void WriteBatch() => Start(false);
        public static void ReadBatch() => Start(true);
        private static void Start(bool read)
        {
            if (!ShopProgress.PersistenceProbe) throw new InvalidOperationException("Batch only; pass -huntShopPersistenceProbe");
            if (!read) ShopProgress.Reset();
            SessionState.SetString(Pending, read ? "read" : "write");
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            EditorApplication.EnterPlaymode();
        }
        private static void OnPlay(PlayModeStateChange state)
        {
            string mode = SessionState.GetString(Pending, "");
            if (state != PlayModeStateChange.EnteredPlayMode || mode == "") return;
            reading = mode == "read";
            SessionState.EraseString(Pending);
            deadline = EditorApplication.timeSinceStartup + 30;
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Shop persistence check timed out");
                if (Time.timeSinceLevelLoad < 0.6f) return;
                ShopManager shop = UnityEngine.Object.FindAnyObjectByType<ShopManager>();
                if (!reading)
                {
                    shop.Wallet.RestoreBalance(4321);
                    shop.Upgrades.RestoreLevels(new[] { 1, 2, 3, 1 });
                    shop.SaveProgress();
                    Debug.Log("Shop persistence WRITE PASSED: isolated PlayerPrefs snapshot saved for next Unity process.");
                }
                else
                {
                    if (shop.Wallet.Balance != 4321 || !shop.Upgrades.CopyLevels().SequenceEqual(new[] { 1, 2, 3, 1 }) ||
                        shop.Wallet.RewardPerDeer != 125 || UnityEngine.Object.FindAnyObjectByType<TrapManager>().MaximumTraps != 5 ||
                        Mathf.Abs(UnityEngine.Object.FindAnyObjectByType<ItemHolder>().MaximumMass - 140) > 0.01f ||
                        Mathf.Abs(UnityEngine.Object.FindAnyObjectByType<TranquilizerGun>().SedationDurationMultiplier - 0.8f) > 0.01f)
                        throw new InvalidOperationException("Startup failed to reload saved balance, levels or effects across processes");
                    shop.ResetProgress();
                    Debug.Log("Shop persistence READ PASSED: a new Unity process loaded balance, levels and all effects; player save untouched.");
                }
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.update -= Tick; EditorApplication.Exit(1); }
        }
    }
}
