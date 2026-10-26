using HuntHappens.Player;
using HuntHappens.Interaction;
using HuntHappens.Shop;
using HuntHappens.Weapons;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.SmallGame
{
    [DefaultExecutionOrder(40)]
    public sealed class RatHunting : MonoBehaviour
    {
        public RatCage cagePrefab;
        public Collider ground;
        public ShopManager shop;
        private PrototypeInput input;
        private ItemHolder holder;
        private Camera view;
        private TranquilizerGun gun;
        private readonly System.Collections.Generic.List<ShopProgress.CarriedRat> carriedRats = new System.Collections.Generic.List<ShopProgress.CarriedRat>();
        public string EscapeNotice { get; private set; }
        public float EscapeNoticeUntil { get; private set; }
        private static double Now => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        private double menuPausedAt;
        public void SetMenuPaused(bool paused)
        {
            if (paused)
            {
                if (menuPausedAt == 0) menuPausedAt = Now;
                return;
            }
            if (menuPausedAt == 0) return;
            double duration = System.Math.Max(0, Now - menuPausedAt);
            menuPausedAt = 0;
            foreach (var record in carriedRats) record.wakeAt += duration;
            if (carriedRats.Count > 0) Save();
        }
        private PlayerInventory Inventory => GetComponent<PlayerInventory>();
        public int Cages => Inventory.Count(InventoryKind.Cage) + Deployed;
        public int Bread => Inventory.Count(InventoryKind.Bread);
        public InventoryKind SelectedBait
        {
            get
            {
                if (Inventory.Hand != null && RatBait.IsBait(Inventory.Hand.kind)) return Inventory.Hand.kind;
                for (int i = 0; i < PlayerInventory.Capacity; i++)
                    if (Inventory.Slot(i) != null && RatBait.IsBait(Inventory.Slot(i).kind)) return Inventory.Slot(i).kind;
                return InventoryKind.None;
            }
        }
        public bool UseBait(InventoryKind kind) => RatBait.IsBait(kind) && Inventory.Consume(kind);
        public int Rats { get; private set; }
        public int GreyRats { get; private set; }
        public int LargeRats { get; private set; }
        public int SaleTotal => (Rats - GreyRats - LargeRats) * 10 + GreyRats * 15 + LargeRats * 25;
        public int Darts => Inventory.Count(InventoryKind.Darts);
        public int Deployed { get; private set; }
        public int Available => Cages - Deployed;
        public bool Selected => Inventory.IsEquipped(InventoryKind.Cage);
        public event System.Action Placed;
        public string Hint { get; private set; } = "";
        public string Stock => "Клетки: " + Available + " | Хлеб: " + Bread + " | Крысы: " + (Rats - GreyRats - LargeRats) + " чёрн., " + GreyRats + " сер., " + LargeRats + " крупн." + " | Дротики: " + Darts;
        public void Reload()
        {
            var data = ShopProgress.Load();
            Rats = Mathf.Clamp(data.rats, 0, 999);
            GreyRats = Mathf.Clamp(data.greyRats, 0, Rats);
            LargeRats = Mathf.Clamp(data.largeRats, 0, Rats - GreyRats);
            carriedRats.Clear();
            int[] remaining = { Rats - GreyRats - LargeRats, GreyRats, LargeRats };
            foreach (var record in data.carriedRats ?? new ShopProgress.CarriedRat[0])
            {
                if (record == null || record.kind < 0 || record.kind > 2 || remaining[record.kind] <= 0 || double.IsNaN(record.wakeAt) || double.IsInfinity(record.wakeAt)) continue;
                remaining[record.kind]--;
                carriedRats.Add(record);
            }
            BindCarriedRats();
            RefreshCarriedRats();
        }
        private void Start()
        {
            input = GetComponent<PrototypeInput>(); holder = GetComponent<ItemHolder>();
            view = GetComponentInChildren<Camera>(); gun = GetComponent<TranquilizerGun>(); Reload();
        }
        private void Save() => ShopProgress.SaveSupplies(Cages, Bread, Rats, Darts, GreyRats, LargeRats, carriedRats.ToArray());
        private void BindCarriedRats()
        {
            if (carriedRats.Count == 0) return;
            var animals = FindObjectsByType<RatAI>(FindObjectsSortMode.None);
            var assigned = new System.Collections.Generic.HashSet<RatAI>();
            foreach (var record in carriedRats)
            {
                if (record.animal != null) { assigned.Add(record.animal); continue; }
                foreach (var animal in animals)
                {
                    if (assigned.Contains(animal) || animal.Kind != record.kind || (animal.Home - record.home).sqrMagnitude > 0.01f) continue;
                    record.animal = animal;
                    animal.HoldForCarrier();
                    assigned.Add(animal);
                    break;
                }
            }
        }
        public void RefreshCarriedRats()
        {
            if (menuPausedAt != 0) return;
            bool changed = false;
            for (int i = carriedRats.Count - 1; i >= 0; i--)
            {
                var record = carriedRats[i];
                if (Now < record.wakeAt) continue;
                if (record.animal == null) BindCarriedRats();
                if (record.animal == null || !record.animal.TryEscapeFromCarrier(transform)) continue;
                Rats = Mathf.Max(0, Rats - 1);
                if (record.kind == 2) LargeRats = Mathf.Max(0, LargeRats - 1);
                else if (record.kind == 1) GreyRats = Mathf.Max(0, GreyRats - 1);
                carriedRats.RemoveAt(i);
                EscapeNotice = "Крыса проснулась и убежала!";
                EscapeNoticeUntil = Time.unscaledTime + 4f;
                changed = true;
            }
            if (changed) Save();
        }
        private void Update() => RefreshCarriedRats();
        public bool Buy(int kind)
        {
            if (shop == null || !shop.CanTradeSupplies) return false;
            int price = kind == 6 ? shop.ChocolatePrice : WorldShopItem.PriceFor(kind);
            if (kind < 0 || kind > 8 || kind == 3 ||
                (kind == 2 ? Darts >= PlayerInventory.MaximumDarts : !Inventory.CanReceive)) return false;
            if (!shop.Wallet.TrySpend(price)) return false;
            if (kind == 2) { Inventory.AddDarts(1); Save(); return true; }
            Inventory.Receive(new InventoryEntry { kind = kind == 0 ? InventoryKind.Cage : kind == 1 ? InventoryKind.Bread :
                kind == 4 ? InventoryKind.Gun : kind == 6 ? InventoryKind.Chocolate : kind == 7 ? InventoryKind.CheeseSandwich : kind == 8 ? InventoryKind.Latte : InventoryKind.Trap });
            Save(); return true;
        }
        public bool Sell()
        {
            RefreshCarriedRats();
            if (shop == null || !shop.CanTradeSupplies || Rats == 0) return false;
            // A rat already awake cannot be sold while waiting for a safe NavMesh spawn.
            foreach (var record in carriedRats) if (Now >= record.wakeAt) return false;
            foreach (var record in carriedRats) if (record.animal != null) record.animal.ReleaseForRespawn();
            carriedRats.Clear();
            int reward = SaleTotal; Rats = 0; GreyRats = 0; LargeRats = 0; Save();
            Inventory.ClearCageContents();
            shop.Wallet.RestoreBalance((int)System.Math.Min(int.MaxValue, (long)shop.Wallet.Balance + reward));
            return true;
        }
        public bool UseBread() => Inventory.Consume(InventoryKind.Bread);
        public bool UseDart() => Inventory.Consume(InventoryKind.Darts);
        public bool CollectBread() => Inventory.Receive(new InventoryEntry { kind = InventoryKind.Bread });
        public bool TryCollectSleepingRat(RatAI rat)
        {
            if (!isActiveAndEnabled || input == null || input.GameplayBlocked || holder == null || holder.IsHolding || Rats >= 999 || rat == null || !rat.CanPickUp) return false;
            var record = new ShopProgress.CarriedRat { kind = rat.Kind, home = rat.Home, wakeAt = Now + rat.RemainingSleep, animal = rat };
            if (!rat.TryCollectSleeping()) return false;
            carriedRats.Add(record);
            Rats++;
            if (rat.isAggressive) LargeRats++; else if (rat.isGrey) GreyRats++;
            Save();
            return true;
        }
        public void Recover(bool caught, bool grey = false, bool large = false) { Deployed = Mathf.Max(0, Deployed - 1); if (caught) { Rats++; if (large) LargeRats++; else if (grey) GreyRats++; } Save(); }
        public bool Evaluate(out Vector3 point)
        {
            point = default;
            if (view == null || ground == null || holder.IsHolding || Available <= 0) return false;
            var hits = Physics.RaycastAll(view.transform.position, view.transform.forward, 4f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider != ground || hit.normal.y < 0.95f) return false;
                point = hit.point;
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                        if (!ground.Raycast(new Ray(point + new Vector3(x * 0.36f, 0.3f, z * 0.5f), Vector3.down), out var support, 0.5f) || Mathf.Abs(support.point.y - point.y) > 0.05f) return false;
                if (!NavMesh.SamplePosition(point, out var nav, 0.3f, NavMesh.AllAreas)) return false;
                foreach (var obstacle in Physics.OverlapBox(point + Vector3.up * 0.25f, new Vector3(0.36f, 0.23f, 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    if (obstacle != ground) return false;
                return true;
            }
            return false;
        }
        public void SelectCageTool()
        {
            if (Selected) return;
            for (int i = 0; i < PlayerInventory.Capacity; i++)
                if (Inventory.Slot(i)?.kind == InventoryKind.Cage) { Inventory.SelectSlot(i); return; }
        }
        public RatCage TryPlace()
        {
            if (!Selected || input.GameplayBlocked || !Evaluate(out var point)) return null;
            var cage = Instantiate(cagePrefab, point, Quaternion.identity); cage.Owner = this;
            cage.RestoreCargo(Inventory.Hand.cageRat, cagePrefab, Inventory.Hand.cageBaited, Inventory.Hand.CageBait);
            Inventory.Consume(InventoryKind.Cage); Deployed++; Placed?.Invoke(); return cage;
        }
        private void LateUpdate()
        {
            Hint = Available <= 0 ? "Все клетки установлены — заберите их клавишей E" : Evaluate(out _) ? (Inventory.Hand?.cageRat > 0 ? "ЛКМ — поставить закрытую клетку с крысой" : Inventory.Hand?.cageBaited == true ? "ЛКМ — поставить клетку: " + RatBait.Name(Inventory.Hand.CageBait) : "ЛКМ — поставить клетку. Затем E — положить приманку") : "Наведите на свободную землю рядом с собой";
            if (Selected && input.ThrowPressed && GetComponent<PlayerInteraction>().LastHandledFrame != Time.frameCount) TryPlace();
        }
    }
}
