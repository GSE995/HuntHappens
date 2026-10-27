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
                CheckBait(hunt, inventory, arms);
                for (int kind = 0; kind < 3; kind++)
                {
                    ShopProgress.Reset(); inventory.Reload(); hunt.Reload(); hunt.shop.ReloadProgress();
                    var rat = UnityEngine.Object.FindObjectsByType<RatAI>(FindObjectsSortMode.None).First(r => r.Kind == kind && !r.Caught);
                    cage = UnityEngine.Object.Instantiate(hunt.cagePrefab, rat.transform.position, Quaternion.identity);
                    cage.Owner = hunt;
                    Check(inventory.Receive(new InventoryEntry { kind = RatBait.ForRat(kind) }), "receive bait");
                    Check(inventory.SelectSlot(0) && cage.Interact(hunt) && RatFearChecks.WhenCalm(cage, () => cage.TryCapture(rat)), "capture species " + kind);
                    Check(cage.Interact(hunt) && inventory.Hand?.cageRat == kind + 1 && hunt.Rats == 1, "collect cage and exactly one rat");
                    Draw(arms);
                    VerifyVisual(arms.MiniatureCage, hunt.cagePrefab, true);
                    Check(inventory.SelectSlot(1) && inventory.SelectSlot(1), "store and equip filled cage");
                    inventory.Reload(); hunt.Reload();
                    Check(inventory.Slot(1)?.cageRat == kind + 1 && hunt.Rats == 1, "saved species and catch survive reload");
                    Check(inventory.SelectSlot(1), "equip restored cage");
                    for (int cycle = 0; cycle < 2; cycle++) PlaceAndRetrieve(hunt, inventory, kind + 1);
                    var entry = inventory.Hand;
                    Check(inventory.DropHand(), "drop cage");
                    dropped = UnityEngine.Object.FindObjectsByType<WorldInventoryItem>(FindObjectsSortMode.None).Single(i => i.Entry == entry);
                    VerifyVisual(dropped.transform, hunt.cagePrefab, true);
                    Check(dropped.Take(inventory) && inventory.Hand.cageRat == kind + 1 && hunt.Rats == 1, "pick up dropped cage without duplicating catch");
                    Check(inventory.DropHand(), "drop filled cage again");
                    dropped = UnityEngine.Object.FindObjectsByType<WorldInventoryItem>(FindObjectsSortMode.None).Single(i => i.Entry == entry);
                    VerifyVisual(dropped.transform, hunt.cagePrefab, true);
                    Check(dropped.Take(inventory) && inventory.Hand.cageRat == kind + 1 && hunt.Rats == 1, "repeated Q and pickup preserve cage cargo");
                    trade.SetValue(hunt.shop, true);
                    int before = hunt.shop.Wallet.Balance;
                    if (kind == 2) PlaceAndRetrieve(hunt, inventory, kind + 1, true);
                    else if (kind == 1)
                    {
                        Check(inventory.DropHand(), "drop before sale");
                        dropped = UnityEngine.Object.FindObjectsByType<WorldInventoryItem>(FindObjectsSortMode.None).Single(i => i.Entry == entry);
                        Check(SemenychChecks.SellAtNpc(hunt), "sell catch while Q cage lies on ground");
                        VerifyVisual(dropped.transform, hunt.cagePrefab, false);
                        Check(dropped.Take(inventory), "recover empty dropped cage after sale");
                    }
                    else Check(SemenychChecks.SellAtNpc(hunt), "sell held catch");
                    Check(hunt.shop.Wallet.Balance == before + (kind == 0 ? 10 : kind == 1 ? 15 : 25), "correct sale reward");
                    Check(inventory.Hand.cageRat == 0 && hunt.Rats == 0, "sale empties cage");
                    Draw(arms); VerifyVisual(arms.MiniatureCage, hunt.cagePrefab, false);
                    trade.SetValue(hunt.shop, false);
                }
                ShopProgress.Reset(); inventory.Reload(); hunt.Reload();
                cage = UnityEngine.Object.Instantiate(hunt.cagePrefab); cage.Owner = hunt;
                Check(cage.Interact(hunt) && inventory.Hand.cageRat == 0, "empty cage stays empty");
                Draw(arms); VerifyVisual(arms.MiniatureCage, hunt.cagePrefab, false);
                Check(PlayerPrefs.GetString(ShopProgress.PlayerKey) == playerSave, "player save untouched during checks");
                return "Cage cargo checks PASSED: bait paid once, preserved in hand/slots/save/Q/LMB, active after placement and consumed on capture; all 3 species; closed door and render-only rat; exact catch/reward; repeated LMB/E and Q/E without duplicate catch; sale clears display; empty cage unchanged; player save preserved";
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

        private static void CheckBait(RatHunting hunt, PlayerInventory inventory, FirstPersonArms arms)
        {
            ShopProgress.Reset(); inventory.Reload(); hunt.Reload(); hunt.shop.ReloadProgress();
            var cage = UnityEngine.Object.Instantiate(hunt.cagePrefab); cage.Owner = hunt;
            try
            {
                Check(inventory.Receive(new InventoryEntry { kind = InventoryKind.Bread, quantity = 3 }) && inventory.SelectSlot(0), "stock three bread");
                Check(cage.Interact(hunt) && cage.Baited && hunt.Bread == 2, "load exactly one bread");
                Check(inventory.Receive(new InventoryEntry { kind = InventoryKind.Trap }), "occupy hands before retrieval");
                Check(!cage.Interact(hunt) && cage.Baited && hunt.Bread == 2, "failed pickup preserves bait");
                Check(inventory.SelectSlot(2), "stow blocking item");
                Check(cage.Interact(hunt) && inventory.Hand.cageBaited && inventory.Hand.cageRat == 0 && hunt.Bread == 2, "retrieve baited cage without losing bread");
                Check(!cage.Interact(hunt), "repeated pickup cannot duplicate baited cage");
                Draw(arms); VerifyBait(arms.MiniatureCage, hunt.cagePrefab);
                Check(ShopProgress.Load().hand.cageBaited, "save bait in unassigned hand");
                Check(inventory.SelectSlot(1), "store baited cage");
                inventory.Reload(); hunt.Reload();
                Check(inventory.Slot(1).cageBaited && inventory.SelectSlot(1) && hunt.Bread == 2, "restore baited slot without consuming bread");
                inventory.ClearCageContents();
                Check(inventory.Hand.cageBaited, "selling catch does not clear unused bait");
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    var entry = inventory.Hand;
                    Check(inventory.DropHand(), "drop baited cage");
                    var dropped = UnityEngine.Object.FindObjectsByType<WorldInventoryItem>(FindObjectsSortMode.None).Single(i => i.Entry == entry);
                    VerifyBait(dropped.transform, hunt.cagePrefab);
                    Check(dropped.Take(inventory) && inventory.Hand.cageBaited && hunt.Bread == 2, "Q/pickup preserves bait");
                    PlaceAndRetrieve(hunt, inventory, 0);
                }
                var rat = UnityEngine.Object.FindObjectsByType<RatAI>(FindObjectsSortMode.None).First(r => !r.Caught && r.Kind == 0);
                PlaceAndRetrieve(hunt, inventory, 0, capture: rat);
                Check(!inventory.Hand.cageBaited && inventory.Hand.cageRat == rat.Kind + 1 && hunt.Rats == 1 && hunt.Bread == 2, "capture consumes bait without a second bread charge");
                Draw(arms); VerifyVisual(arms.MiniatureCage, hunt.cagePrefab, true);
            }
            finally { if (cage != null) UnityEngine.Object.Destroy(cage.gameObject); }
        }

        private static void VerifyBait(Transform root, RatCage template)
        {
            root.GetComponent<CageCargoVisual>()?.Refresh();
            var bait = root.GetComponentsInChildren<Transform>(true).First(t => t.name == template.bait.name);
            var door = root.GetComponentsInChildren<Transform>(true).First(t => t.name == template.door.name);
            Check(bait.gameObject.activeSelf && bait.GetComponentsInChildren<Renderer>().Length > 0, "bread visible inside cage");
            Check(Vector3.Distance(door.localPosition, template.door.localPosition) < .001f, "baited cage remains open");
        }

        internal static void PlaceAndRetrieve(RatHunting hunt, PlayerInventory inventory, int kind, bool sell = false, RatAI capture = null)
        {
            var camera = inventory.GetComponentInChildren<Camera>();
            var controller = inventory.GetComponent<CharacterController>();
            var position = inventory.transform.position;
            var rotation = camera.transform.rotation;
            bool controllerEnabled = controller.enabled;
            RatCage placed = null;
            try
            {
                controller.enabled = false;
                bool found = false;
                for (int x = -24; x <= 24 && !found; x += 4)
                    for (int z = -24; z <= 24 && !found; z += 4)
                    {
                        var target = new Vector3(x, 0, z);
                        inventory.transform.position = target + new Vector3(0, .1f, -2.8f);
                        camera.transform.LookAt(target); Physics.SyncTransforms();
                        found = hunt.Evaluate(out _);
                    }
                Check(found, "find placement ground");
                int count = hunt.Rats, deployed = hunt.Deployed;
                placed = hunt.TryPlace();
                if (kind == 0)
                {
                    Check(placed != null && placed.Baited && placed.StoredCargo == null && inventory.Hand == null && hunt.Deployed == deployed + 1, "LMB places active prebaited cage");
                    VerifyBait(placed.transform, hunt.cagePrefab);
                    int bread = hunt.Bread;
                    if (capture != null)
                    {
                        Check(capture.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(placed.transform.position) && RatFearChecks.WhenCalm(placed, () => placed.TryCapture(capture)), "prebaited cage captures rat");
                        Check(!placed.Baited && !placed.bait.activeSelf, "capture consumes and hides bait");
                    }
                    Check(placed.Interact(hunt) && inventory.Hand.cageBaited == (capture == null) && hunt.Bread == bread && hunt.Deployed == deployed, "retrieve preserves remaining bait and never charges again");
                    Check(hunt.Rats == count + (capture != null ? 1 : 0), "bait handling does not duplicate catch");
                    return;
                }
                Check(placed != null && placed.StoredCargo?.cageRat == kind && inventory.Hand == null && hunt.Deployed == deployed + 1, "LMB places same closed cargo");
                VerifyVisual(placed.transform, hunt.cagePrefab, true);
                Check(placed.Prompt.Contains("забрать") && !placed.Prompt.Contains("положить хлеб") && !placed.Baited, "occupied cage cannot be baited");
                if (sell)
                {
                    Check(SemenychChecks.SellAtNpc(hunt) && placed.StoredCargo == null && hunt.Rats == 0, "sale empties placed cargo");
                    VerifyVisual(placed.transform, hunt.cagePrefab, false);
                    Check(placed.Interact(hunt) && inventory.Hand?.cageRat == 0 && hunt.Rats == 0 && hunt.Deployed == deployed, "recover empty placed cage after sale");
                    return;
                }
                Check(inventory.Receive(new InventoryEntry { kind = InventoryKind.Bread }) && inventory.SelectSlot(0), "carry spare bread");
                Check(placed.Interact(hunt) && inventory.Hand?.cageRat == kind && hunt.Rats == count && hunt.Deployed == deployed && hunt.Bread == 1, "retrieve occupied cage without consuming bread or duplicating rat");
                Check(!placed.Interact(hunt) && hunt.Rats == count, "repeat E cannot duplicate cage or rat");
                Check(inventory.Consume(InventoryKind.Bread), "clear test bread");
            }
            finally
            {
                if (placed != null) UnityEngine.Object.Destroy(placed.gameObject);
                inventory.transform.position = position; camera.transform.rotation = rotation;
                controller.enabled = controllerEnabled; Physics.SyncTransforms();
            }
        }

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
