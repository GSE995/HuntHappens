using System;
using System.Linq;
using System.Reflection;
using HuntHappens.Interaction;
using HuntHappens.Shop;
using HuntHappens.SmallGame;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class RatBaitChecks
    {
        private static void Check(bool ok, string message)
        { if (!ok) throw new Exception("Rat bait: " + message); }

        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode");
            var inventory = Object.FindAnyObjectByType<PlayerInventory>();
            var hunt = inventory.GetComponent<RatHunting>();
            var menu = Object.FindAnyObjectByType<HuntHappens.UI.GameMenu>();
            bool menuOpen = menu.IsOpen, previousChecks = ShopProgress.InventoryChecksRunning;
            bool playerExists = PlayerPrefs.HasKey(ShopProgress.PlayerKey), testExists = PlayerPrefs.HasKey(ShopProgress.TestKey);
            string playerSave = PlayerPrefs.GetString(ShopProgress.PlayerKey), testSave = PlayerPrefs.GetString(ShopProgress.TestKey);
            var trade = typeof(ShopManager).GetField("worldTrade", BindingFlags.NonPublic | BindingFlags.Instance);
            var start = typeof(RatAI).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
            RatCage cage = null;
            RatAI rat = null;
            WorldInventoryItem dropped = null;
            int successes = 0;
            try
            {
                ShopProgress.InventoryChecksRunning = true; menu.Continue();
                for (int bait = 0; bait < 3; bait++)
                {
                    var baitKind = RatBait.ForRat(bait);
                    for (int species = 0; species < 3; species++)
                    {
                        ShopProgress.Reset(); inventory.Reload(); hunt.Reload();
                        var source = Object.FindObjectsByType<RatAI>(FindObjectsSortMode.None).First(r => r.Kind == species && !r.Caught);
                        rat = Object.Instantiate(source, source.transform.position, source.transform.rotation);
                        start.Invoke(rat, null);
                        cage = Object.Instantiate(hunt.cagePrefab, rat.transform.position, Quaternion.identity); cage.Owner = hunt;
                        Check(inventory.Receive(new InventoryEntry { kind = baitKind, quantity = 2 }) && inventory.SelectSlot(0), "stock bait");
                        Check(cage.Interact(hunt) && cage.BaitKind == baitKind && inventory.Count(baitKind) == 1, "load exactly one " + baitKind);
                        Check(cage.Prompt.Contains(RatBait.Name(baitKind)), "installed bait named");
                        bool expected = species == bait;
                        Check(cage.Accepts(rat) == expected, "AI target filter " + species + "/" + bait);
                        Check(cage.TryCapture(rat) == expected, "capture matrix " + species + "/" + bait);
                        Check(rat.Caught == expected && cage.Baited == !expected && cage.bait.activeSelf == !expected, "capture/bait state");
                        if (expected) { successes++; Check(!cage.TryCapture(rat), "no second capture"); }
                        else Check(!cage.TryCapture(rat) && cage.BaitKind == baitKind, "repeated wrong rat leaves bait");
                        Check(cage.Interact(hunt), "retrieve");
                        Check(inventory.Hand.CageBait == (expected ? InventoryKind.None : baitKind) && inventory.Count(baitKind) == 1, "no second charge");
                        Check(inventory.Hand.cageRat == (expected ? species + 1 : 0), "cargo species");
                        Object.DestroyImmediate(cage.gameObject); cage = null;
                        Object.DestroyImmediate(rat.gameObject); rat = null;
                    }

                    ShopProgress.Reset(); inventory.Reload(); hunt.Reload(); hunt.shop.ReloadProgress();
                    trade.SetValue(hunt.shop, true);
                    int product = bait == 0 ? 1 : bait == 1 ? 7 : 8;
                    int balance = hunt.shop.Wallet.Balance;
                    Check(hunt.Buy(product) && inventory.Hand.kind == baitKind && hunt.shop.Wallet.Balance == balance - WorldShopItem.PriceFor(product), "purchase " + baitKind);
                    trade.SetValue(hunt.shop, false);
                    inventory.Reload();
                    Check(inventory.Hand?.kind == baitKind, "loose bait hand save/load");
                    Check(inventory.SelectSlot(0), "stow bait"); inventory.Reload();
                    Check(inventory.Slot(0)?.kind == baitKind, "loose bait slot save/load");
                    cage = Object.Instantiate(hunt.cagePrefab); cage.Owner = hunt;
                    Check(cage.Interact(hunt) && inventory.Count(baitKind) == 0, "consume last loose bait");
                    Check(cage.Interact(hunt), "retrieve prepared cage");
                    inventory.Reload(); Check(inventory.Hand.CageBait == baitKind, "prepared hand save/load");
                    Check(inventory.SelectSlot(1), "stow cage"); inventory.Reload();
                    Check(inventory.Slot(1).CageBait == baitKind && inventory.SelectSlot(1), "prepared slot save/load");
                    var entry = inventory.Hand;
                    Check(inventory.DropHand(), "Q prepared cage");
                    dropped = Object.FindObjectsByType<WorldInventoryItem>(FindObjectsSortMode.None).Single(i => i.Entry == entry);
                    Check(dropped.Take(inventory) && inventory.Hand.CageBait == baitKind, "E preserves bait");
                    Object.DestroyImmediate(dropped.gameObject); dropped = null;
                    CageCargoChecks.PlaceAndRetrieve(hunt, inventory, 0);
                    Check(inventory.Hand.CageBait == baitKind, "LMB/E placement preserves type");
                    Object.DestroyImmediate(cage.gameObject); cage = null;
                }
                Check(successes == 3, "exactly three successes");
                ShopProgress.Reset(); inventory.Reload(); hunt.Reload();
                Check(inventory.Receive(new InventoryEntry { kind = InventoryKind.Bread }) && inventory.SelectSlot(0), "fallback bread");
                Check(inventory.Receive(new InventoryEntry { kind = InventoryKind.Latte }) && hunt.SelectedBait == InventoryKind.Latte, "hand priority over bread slot");
                Check(inventory.SelectSlot(1) && hunt.SelectedBait == InventoryKind.Bread, "first slot fallback");
                var old = new ShopProgress.Data { inventoryVersion = 2, hand = new InventoryEntry { kind = InventoryKind.Cage, cageBaited = true } };
                string legacyJson = JsonUtility.ToJson(old).Replace(",\"cageBait\":0", "");
                PlayerPrefs.SetString(ShopProgress.Key, legacyJson);
                inventory.Reload();
                Check(inventory.Hand.CageBait == InventoryKind.Bread && ShopProgress.Load().hand.CageBait == InventoryKind.Bread, "legacy baited cage becomes bread");
                old.hand.cageBaited = false; PlayerPrefs.SetString(ShopProgress.Key, JsonUtility.ToJson(old)); inventory.Reload();
                Check(inventory.Hand.CageBait == InventoryKind.None, "legacy empty cage stays empty");
                Check(PlayerPrefs.GetString(ShopProgress.PlayerKey) == playerSave, "player save untouched");
                return "Rat bait checks PASSED: 9 combinations, exactly 3 captures; wrong bait ignored/preserved; load once, consume on capture; all 3 purchases, hand/slot save-load, Q/E, placement restore, selection priority, legacy saves.";
            }
            finally
            {
                trade.SetValue(hunt.shop, false);
                if (cage != null) Object.DestroyImmediate(cage.gameObject);
                if (rat != null) Object.DestroyImmediate(rat.gameObject);
                if (dropped != null) Object.DestroyImmediate(dropped.gameObject);
                if (testExists) PlayerPrefs.SetString(ShopProgress.TestKey, testSave); else PlayerPrefs.DeleteKey(ShopProgress.TestKey);
                ShopProgress.InventoryChecksRunning = previousChecks;
                inventory.Reload(); hunt.Reload(); hunt.shop.ReloadProgress();
                if (playerExists) PlayerPrefs.SetString(ShopProgress.PlayerKey, playerSave); else PlayerPrefs.DeleteKey(ShopProgress.PlayerKey);
                PlayerPrefs.Save(); if (menuOpen) menu.Open();
            }
        }
    }
}
