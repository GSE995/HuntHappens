using System;
using HuntHappens.Interaction;
using HuntHappens.Shop;
using HuntHappens.SmallGame;
using HuntHappens.Weapons;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class InventoryPlayChecks
    {
        private static int passed;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Inventory check failed: " + message);
            passed++;
        }
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            var inventory = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
            if (inventory.Hand?.kind == InventoryKind.Physical) throw new InvalidOperationException("Stow or drop physical cargo before checks.");
            for (int i = 0; i < PlayerInventory.Capacity; i++)
                if (inventory.Slot(i)?.kind == InventoryKind.Physical) throw new InvalidOperationException("Run checks before collecting physical cargo.");
            var existingDrops = new System.Collections.Generic.HashSet<WorldInventoryItem>(UnityEngine.Object.FindObjectsByType<WorldInventoryItem>(FindObjectsSortMode.None));
            var hunt = inventory.GetComponent<RatHunting>();
            var gun = inventory.GetComponent<TranquilizerGun>();
            var input = inventory.GetComponent<HuntHappens.Player.PrototypeInput>();
            var holder = inventory.GetComponent<ItemHolder>();
            var shop = hunt.shop;
            var trade = typeof(ShopManager).GetField("worldTrade", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            GameObject physical = null;
            passed = 0;
            ShopProgress.InventoryChecksRunning = true;
            try
            {
                var legacy = new ShopProgress.Data { inventoryVersion = 1, darts = 50,
                    inventory = new[] { new InventoryEntry { kind = InventoryKind.Gun }, new InventoryEntry {kind = InventoryKind.Darts, quantity = 4} },
                    hand = new InventoryEntry {kind = InventoryKind.Darts, quantity = 2} };
                PlayerPrefs.SetString(ShopProgress.Key, JsonUtility.ToJson(legacy));
                inventory.Reload();
                Check(inventory.Darts == 6 && inventory.Hand == null && inventory.Slot(1) == null && inventory.Slot(0).kind == InventoryKind.Gun, "migrate slot and hand ammo without double counting old mirror");
                inventory.Reload();
                Check(inventory.Darts == 6 && ShopProgress.Load().inventoryVersion == 2, "ammo migration only runs once");
                ShopProgress.Reset(); inventory.Reload(); hunt.Reload(); shop.ReloadProgress();
                Check(inventory.Hand == null && inventory.Count(InventoryKind.Gun) == 0 && hunt.Cages == 0, "empty start, no free tools");
                Check(hunt.Bread == 0 && hunt.Darts == 0 && !gun.Selected && !gun.TrapSelected, "no implicit supplies or selected tools");
                for (int i = 0; i < 4; i++) Check(inventory.Slot(i) == null, "empty slot " + i);
                trade.SetValue(shop, true); shop.Wallet.RestoreBalance(200);
                Check(hunt.Buy(4) && shop.Wallet.Balance == 150, "buy gun with correct price");
                Check(inventory.HasUnassignedItem && gun.Selected && inventory.HandSlot == -1, "purchase goes into hands");
                Check(!hunt.Buy(0) && shop.Wallet.Balance == 150, "blocked purchase does not charge");
                Check(inventory.SelectSlot(2) && inventory.Hand == null && inventory.Slot(2).kind == InventoryKind.Gun, "user chooses slot 3");
                Check(inventory.SelectSlot(2) && gun.Selected && inventory.HandSlot == 2 && inventory.Slot(2) != null, "equip retains slot");
                Check(inventory.SelectSlot(0) && inventory.Slot(2) == null && inventory.Slot(0).kind == InventoryKind.Gun, "move into empty slot");
                Check(hunt.Buy(0) && inventory.IsEquipped(InventoryKind.Cage), "buy cage into hands");
                Check(!inventory.SelectSlot(0) && inventory.IsEquipped(InventoryKind.Cage), "occupied slot blocked for unassigned item");
                Check(inventory.SelectSlot(1) && inventory.Slot(1).kind == InventoryKind.Cage, "store cage");
                Check(inventory.SelectSlot(0) && gun.Selected, "equip gun");
                Check(inventory.SelectSlot(1) && hunt.Selected && inventory.Slot(0).kind == InventoryKind.Gun, "switch assigned items without losing gun");
                Check(inventory.DropHand() && inventory.Slot(1) == null && inventory.Hand == null, "Q frees slot");
                var dropped = Array.Find(UnityEngine.Object.FindObjectsByType<WorldInventoryItem>(FindObjectsSortMode.None), i => !existingDrops.Contains(i) && i.enabled && i.Entry?.kind == InventoryKind.Cage);
                Check(dropped != null && dropped.GetComponent<Rigidbody>() != null, "dropped cage exists in world");
                Check(dropped.Take(inventory) && inventory.HasUnassignedItem && hunt.Cages == 1, "pickup restores exactly one cage");
                Check(inventory.SelectSlot(1), "restore picked item to slot");
                Check(hunt.Buy(2) && inventory.Hand == null && inventory.Slot(2) == null && hunt.Darts == 1, "ammunition goes to gun reserve without using hands or slots");
                Check(hunt.UseDart() && hunt.Darts == 0 && inventory.Slot(0).kind == InventoryKind.Gun && !hunt.UseDart(), "empty ammunition retains gun, no underflow");
                Check(hunt.CollectBread() && !hunt.CollectBread(), "free pickup obeys hands rule");
                Check(hunt.UseBread() && inventory.Hand == null, "consume unassigned bread");
                inventory.Receive(new InventoryEntry {kind = InventoryKind.Trap}); inventory.SelectSlot(2);
                inventory.Receive(new InventoryEntry {kind = InventoryKind.Darts, quantity = 3});
                inventory.Receive(new InventoryEntry {kind = InventoryKind.Gun}); inventory.SelectSlot(3);
                Check(hunt.CollectBread() && inventory.HasUnassignedItem, "can carry with all four slots full");
                var carried = inventory.Hand;
                Check(hunt.Buy(2) && hunt.Darts == 4 && inventory.Hand == carried, "buy ammo with full slots and occupied hands");
                Check(hunt.UseDart() && hunt.Darts == 3 && inventory.Hand == carried, "ammo consumption does not affect carried item");
                for (int i = 0; i < 4; i++) Check(!inventory.SelectSlot(i), "all occupied slots blocked " + i);
                inventory.Save(); inventory.Reload();
                Check(inventory.HasUnassignedItem && inventory.Hand.kind == InventoryKind.Bread && inventory.Slot(0).kind == InventoryKind.Gun && hunt.Darts == 3, "save/reload preserves slots and unassigned hand");
                Check(inventory.DropHand() && inventory.Slot(3).kind == InventoryKind.Gun && hunt.Darts == 3, "drop hand leaves inventory and ammunition intact");
                Check(inventory.SelectSlot(0) && inventory.SelectSlot(0) && inventory.Hand == null && inventory.Slot(0) != null, "toggle holsters without freeing slot");
                inventory.SelectSlot(1); inventory.DropHand();
                physical = GameObject.CreatePrimitive(PrimitiveType.Cube);
                physical.name = "Inventory check physical item"; physical.transform.position = inventory.transform.position + Vector3.up;
                physical.AddComponent<Rigidbody>(); var pickup = physical.AddComponent<PickupItem>();
                Check(holder.TryPickUp(pickup) && inventory.HasUnassignedItem, "physical pickup shares hands state");
                Check(!inventory.SelectSlot(0), "physical hand blocks occupied slot");
                Check(inventory.SelectSlot(1) && !holder.IsHolding && !physical.activeSelf, "physical item stows");
                Check(inventory.SelectSlot(1) && holder.HeldItem == pickup && inventory.HandSlot == 1, "physical equip retains slot");
                Check(inventory.DropHand() && inventory.Slot(1) == null && physical.activeSelf && pickup.Holder == null, "physical Q release restores world object");
                physical.SetActive(false);
                CheckPlacement(inventory, hunt, holder);
                input.SetGameplayBlocked(true);
                Check(!inventory.SelectSlot(0), "menu blocks inventory gameplay");
                input.SetGameplayBlocked(false);
                return "PASS: " + passed + " inventory checks";
            }
            finally
            {
                input.SetGameplayBlocked(false); holder.Drop();
                if (physical != null) UnityEngine.Object.Destroy(physical);
                foreach (var item in UnityEngine.Object.FindObjectsByType<WorldInventoryItem>(FindObjectsSortMode.None))
                    if (item.enabled && !existingDrops.Contains(item)) UnityEngine.Object.Destroy(item.gameObject);
                trade.SetValue(shop, false);
                ShopProgress.Reset(); ShopProgress.InventoryChecksRunning = false;
                inventory.Reload(); hunt.Reload(); shop.ReloadProgress();
            }
        }
        private static void CheckPlacement(PlayerInventory inventory, RatHunting hunt, ItemHolder holder)
        {
            var placement = inventory.GetComponent<HuntHappens.Traps.TrapPlacement>();
            var camera = inventory.GetComponentInChildren<Camera>();
            var controller = inventory.GetComponent<CharacterController>();
            var position = inventory.transform.position; var rotation = camera.transform.rotation;
            bool controllerEnabled = controller.enabled;
            HuntHappens.Traps.AnimalTrap trap = null; RatCage cage = null;
            try
            {
                controller.enabled = false;
                bool found = false;
                for (int x = -24; x <= 24 && !found; x += 4)
                    for (int z = -24; z <= 24 && !found; z += 4)
                    {
                        var target = new Vector3(x, 0, z);
                        inventory.transform.position = target + new Vector3(0, 0.1f, -2.8f);
                        camera.transform.LookAt(target); Physics.SyncTransforms();
                        found = placement.Evaluate(out _, out _);
                    }
                Check(found, "find valid placement site");
                inventory.SelectSlot(2);
                int before = placement.Manager.PlacedCount;
                Check(placement.TryPlace() && inventory.Slot(2) == null && inventory.Count(InventoryKind.Trap) == 0, "place consumes owned trap");
                Check(!placement.TryPlace() && placement.Manager.PlacedCount == before + 1, "cannot place infinite traps");
                trap = Array.Find(placement.Manager.GetComponentsInChildren<HuntHappens.Traps.AnimalTrap>(), t => t.isActiveAndEnabled);
                Check(trap != null && trap.TryInteract(holder) && inventory.HasUnassignedItem, "retrieve trap into hands");
                Check(placement.Manager.PlacedCount == before, "retrieval frees deployed capacity");
                inventory.SelectSlot(2); Physics.SyncTransforms();
                inventory.Receive(new InventoryEntry {kind = InventoryKind.Cage}); inventory.SelectSlot(1); inventory.SelectSlot(1);
                cage = hunt.TryPlace();
                Check(cage != null && inventory.Slot(1) == null && hunt.Available == 0, "place consumes owned cage");
                Check(hunt.TryPlace() == null, "cannot duplicate deployed cage");
                Check(cage.Interact(hunt) && inventory.HasUnassignedItem && hunt.Available == 1, "retrieve cage into hands");
            }
            finally
            {
                if (trap != null) UnityEngine.Object.Destroy(trap.gameObject);
                if (cage != null) UnityEngine.Object.Destroy(cage.gameObject);
                inventory.transform.position = position; camera.transform.rotation = rotation;
                controller.enabled = controllerEnabled; Physics.SyncTransforms();
            }
        }
    }
}
