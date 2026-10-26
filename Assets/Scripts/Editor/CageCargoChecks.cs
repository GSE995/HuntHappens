using System;
using System.Linq;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Shop;
using HuntHappens.SmallGame;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class CageCargoChecks
    {
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Cage cargo: " + message); }

        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode");
            var inventory = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
            var hunt = inventory.GetComponent<RatHunting>();
            var arms = inventory.GetComponentInChildren<FirstPersonArms>(true);
            var input = inventory.GetComponent<PrototypeInput>();
            var menu = UnityEngine.Object.FindAnyObjectByType<HuntHappens.UI.GameMenu>();
            bool menuOpen = menu.IsOpen;
            bool playerExists = PlayerPrefs.HasKey(ShopProgress.PlayerKey), testExists = PlayerPrefs.HasKey(ShopProgress.TestKey);
            string playerSave = PlayerPrefs.GetString(ShopProgress.PlayerKey), testSave = PlayerPrefs.GetString(ShopProgress.TestKey);
            bool previousChecks = ShopProgress.InventoryChecksRunning;
            var trade = typeof(ShopManager).GetField("worldTrade", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            RatCage cage = null;
            WorldInventoryItem dropped = null;
            try
            {
                ShopProgress.InventoryChecksRunning = true;
                menu.Continue();
                for (int kind = 0; kind < 3; kind++)
                {
                    ShopProgress.Reset(); inventory.Reload(); hunt.Reload(); hunt.shop.ReloadProgress();
                    var rat = UnityEngine.Object.FindObjectsByType<RatAI>(FindObjectsSortMode.None).First(r => r.Kind == kind);
                    cage = UnityEngine.Object.Instantiate(hunt.cagePrefab, rat.transform.position, Quaternion.identity);
                    cage.Owner = hunt;
                    Check(inventory.Receive(new InventoryEntry { kind = InventoryKind.Bread }), "receive bait");
                    Check(inventory.SelectSlot(0) && cage.Interact(hunt) && cage.TryCapture(rat), "capture species " + kind);
                    Check(cage.Interact(hunt) && inventory.Hand?.cageRat == kind + 1 && hunt.Rats == 1, "collect cage and exactly one rat");
                    Draw(arms);
                    VerifyVisual(arms.MiniatureCage, hunt.cagePrefab, true);
                    Check(inventory.SelectSlot(1) && inventory.SelectSlot(1), "store and equip filled cage");
                    inventory.Reload(); hunt.Reload();
                    Check(inventory.Slot(1)?.cageRat == kind + 1 && hunt.Rats == 1, "saved species and catch survive reload");
                    Check(inventory.SelectSlot(1), "equip restored cage");
                    var entry = inventory.Hand;
                    Check(inventory.DropHand(), "drop cage");
                    dropped = UnityEngine.Object.FindObjectsByType<WorldInventoryItem>(FindObjectsSortMode.None).Single(i => i.Entry == entry);
                    VerifyVisual(dropped.transform, hunt.cagePrefab, true);
                    Check(dropped.Take(inventory) && inventory.Hand.cageRat == kind + 1 && hunt.Rats == 1, "pick up dropped cage without duplicating catch");
                    trade.SetValue(hunt.shop, true);
                    int before = hunt.shop.Wallet.Balance;
                    Check(hunt.Sell() && hunt.shop.Wallet.Balance == before + (kind == 0 ? 10 : kind == 1 ? 15 : 25), "correct sale reward");
                    Check(inventory.Hand.cageRat == 0 && hunt.Rats == 0, "sale empties cage");
                    Draw(arms); VerifyVisual(arms.MiniatureCage, hunt.cagePrefab, false);
                    trade.SetValue(hunt.shop, false);
                }
                ShopProgress.Reset(); inventory.Reload(); hunt.Reload();
                cage = UnityEngine.Object.Instantiate(hunt.cagePrefab); cage.Owner = hunt;
                Check(cage.Interact(hunt) && inventory.Hand.cageRat == 0, "empty cage stays empty");
                Draw(arms); VerifyVisual(arms.MiniatureCage, hunt.cagePrefab, false);
                Check(PlayerPrefs.GetString(ShopProgress.PlayerKey) == playerSave, "player save untouched during checks");
                return "Cage cargo checks PASSED: all 3 species; closed door and render-only rat; exact catch/reward; slot switch; save/reload; drop/pickup; sale clears display; empty cage unchanged; player save preserved";
            }
            finally
            {
                trade.SetValue(hunt.shop, false);
                if (cage != null) UnityEngine.Object.Destroy(cage.gameObject);
                if (dropped != null) UnityEngine.Object.Destroy(dropped.gameObject);
                if (testExists) PlayerPrefs.SetString(ShopProgress.TestKey, testSave); else PlayerPrefs.DeleteKey(ShopProgress.TestKey);
                ShopProgress.InventoryChecksRunning = previousChecks;
                inventory.Reload(); hunt.Reload(); hunt.shop.ReloadProgress();
                if (playerExists) PlayerPrefs.SetString(ShopProgress.PlayerKey, playerSave); else PlayerPrefs.DeleteKey(ShopProgress.PlayerKey);
                PlayerPrefs.Save();
                if (menuOpen) menu.Open();
            }
        }

        private static void Draw(FirstPersonArms arms) => arms.Draw(new Vector3(-.25f, -.3f, .7f), Quaternion.identity,
            new Vector3(.25f, -.3f, .7f), Quaternion.identity, true, 1);

        private static void VerifyVisual(Transform root, RatCage template, bool filled)
        {
            var cargo = root.GetComponent<CageCargoVisual>(); cargo.Refresh();
            var door = root.GetComponentsInChildren<Transform>(true).First(t => t.name == template.door.name);
            Check(Vector3.Distance(door.localPosition, filled ? new Vector3(0, .25f, .44f) : template.door.localPosition) < .001f, "door pose");
            var rat = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Caged Rat" && t.gameObject.activeSelf);
            Check(filled == (rat != null && rat.gameObject.activeSelf), "rat visibility: " + root.name + ", filled=" + filled);
            if (filled) Check(rat.GetComponentsInChildren<Renderer>().Length > 0 && rat.GetComponentsInChildren<Collider>().Length == 0 && rat.GetComponentsInChildren<RatAI>().Length == 0, "render-only rat");
        }
    }
}
