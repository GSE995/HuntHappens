using System;
using HuntHappens.Player;
using HuntHappens.Shop;
using UnityEngine;

namespace HuntHappens.Interaction
{
    public enum InventoryKind { None, Gun, Trap, Cage, Bread, Darts, Physical }

    [Serializable]
    public sealed class InventoryEntry
    {
        public InventoryKind kind;
        public int quantity = 1;
        [NonSerialized] public PickupItem physical;
        public string Title => kind == InventoryKind.Gun ? "Транквилизатор" : kind == InventoryKind.Trap ? "Ловушка" :
            kind == InventoryKind.Cage ? "Клетка для крыс" : kind == InventoryKind.Bread ? "Хлеб" :
            kind == InventoryKind.Darts ? "Дротики" : physical != null ? physical.name : "Предмет";
        public int Icon => kind == InventoryKind.Gun ? 0 : kind == InventoryKind.Trap ? 2 : kind == InventoryKind.Cage ? 3 :
            kind == InventoryKind.Bread ? 4 : kind == InventoryKind.Darts ? 5 : 1;
    }

    // The hand references an entry; equipping never transfers ownership out of its slot.
    [DefaultExecutionOrder(10), DisallowMultipleComponent]
    [RequireComponent(typeof(ItemHolder), typeof(PrototypeInput))]
    public sealed class PlayerInventory : MonoBehaviour
    {
        public const int Capacity = 4;
        private InventoryEntry[] slots = new InventoryEntry[Capacity];
        public InventoryEntry Hand { get; private set; }
        public int Darts { get; private set; }
        public const int MaximumDarts = 999;
        public int HandSlot => Hand == null ? -1 : Array.IndexOf(slots, Hand);
        public bool HasUnassignedItem => Hand != null && HandSlot < 0;
        public bool CanReceive => !HasUnassignedItem;
        public string Notice { get; private set; }
        private float noticeUntil;
        public string Hint => Time.unscaledTime < noticeUntil ? Notice : Hand == null ? "1–4 — выбрать предмет" :
            Hand.Title + (HasUnassignedItem ? " · в руках\n1–4 — в пустую ячейку | Q — выбросить" : "\n1–4 — выбрать / переместить | Q — выбросить");
        private ItemHolder holder;
        private PrototypeInput input;
        private bool changing;
        private bool loaded;
        private InventoryEntry visualEntry;
        private GameObject handVisual;
        public InventoryEntry Slot(int index) => index >= 0 && index < Capacity ? slots[index] : null;
        public bool IsEquipped(InventoryKind kind) => Hand != null && Hand.kind == kind;
        private void Awake()
        {
            holder = GetComponent<ItemHolder>(); input = GetComponent<PrototypeInput>();
            holder.Released += OnPhysicalReleased;
        }
        private void Start() => Reload();
        public void Reload()
        {
            Stow(); slots = new InventoryEntry[Capacity]; Hand = null;
            var data = ShopProgress.Load();
            Darts = Mathf.Clamp(data.darts, 0, MaximumDarts);
            if (data.inventoryVersion == 1)
            {
                // Version 1 stored ammunition as items; its darts counter was only a stale mirror.
                long total = data.hand?.kind == InventoryKind.Darts ? Math.Max(0, data.hand.quantity) : 0;
                foreach (var entry in data.inventory ?? Array.Empty<InventoryEntry>())
                    if (entry?.kind == InventoryKind.Darts) total += Math.Max(0, entry.quantity);
                Darts = (int)Math.Min(MaximumDarts, total);
            }
            if (data.inventoryVersion >= 1)
            {
                for (int i = 0; i < Capacity; i++)
                    if (data.inventory != null && i < data.inventory.Length && Valid(data.inventory[i])) slots[i] = data.inventory[i];
                if (Valid(data.hand)) Hand = data.hand;
            }
            else if (PlayerPrefs.HasKey(ShopProgress.Key))
            {
                // Preserve previously saved supplies once, without reserving any slot by type.
                int index = 0;
                if (data.cages > 0) slots[index++] = new InventoryEntry { kind = InventoryKind.Cage, quantity = data.cages };
                if (data.bread > 0) slots[index++] = new InventoryEntry { kind = InventoryKind.Bread, quantity = data.bread };
            }
            loaded = true; Save();
        }
        private static bool Valid(InventoryEntry entry) => entry != null && entry.quantity > 0 && entry.kind >= InventoryKind.Gun && entry.kind < InventoryKind.Physical && entry.kind != InventoryKind.Darts;
        public void Save()
        {
            if (!loaded) return;
            ShopProgress.SaveInventory(slots, HasUnassignedItem ? Hand : null, Darts);
        }
        public bool Receive(InventoryEntry entry)
        {
            if (entry?.kind == InventoryKind.Darts) return AddDarts(entry.quantity);
            if (entry == null || entry.quantity <= 0 || !CanReceive) { Warn(); return false; }
            Stow(); Hand = entry; Save(); return true;
        }
        public bool PreparePhysicalPickup()
        {
            if (!CanReceive) { Warn(); return false; }
            Stow(); return true;
        }
        public void RegisterPhysical(PickupItem item)
        {
            Hand = new InventoryEntry { kind = InventoryKind.Physical, physical = item }; Save();
        }
        private void Warn() { Notice = "Сначала уберите предмет в пустую ячейку или нажмите Q"; noticeUntil = Time.unscaledTime + 2; }
        public bool SelectSlot(int index)
        {
            if (index < 0 || index >= Capacity || input.GameplayBlocked) return false;
            if (slots[index] == null && Hand != null)
            {
                int previous = HandSlot;
                if (previous >= 0) slots[previous] = null;
                slots[index] = Hand;
                Stow(); Save(); return true;
            }
            if (HasUnassignedItem) { Warn(); return false; }
            var next = slots[index]; bool same = next != null && next == Hand;
            Stow();
            if (!same && next != null)
            {
                if (next.kind == InventoryKind.Physical)
                {
                    if (next.physical == null) { slots[index] = null; Save(); return false; }
                    var camera = GetComponentInChildren<Camera>();
                    next.physical.transform.position = camera.transform.position + camera.transform.forward * 1.2f;
                    next.physical.gameObject.SetActive(true);
                    changing = true;
                    bool picked = holder.TryPickUp(next.physical);
                    changing = false;
                    if (!picked) { next.physical.gameObject.SetActive(false); return false; }
                }
                Hand = next;
            }
            Save(); return true;
        }
        public bool ChangingPhysical => changing;
        private void Stow()
        {
            if (Hand?.physical != null && holder.HeldItem == Hand.physical)
            {
                changing = true; holder.Drop(); changing = false;
                Hand.physical.gameObject.SetActive(false);
            }
            Hand = null;
        }
        private void OnPhysicalReleased(bool thrown)
        {
            if (changing || Hand == null || Hand.kind != InventoryKind.Physical) return;
            RemoveHand();
        }
        private void RemoveHand()
        {
            int slot = HandSlot; if (slot >= 0) slots[slot] = null;
            Hand = null; Save();
        }
        public bool DropHand()
        {
            if (Hand == null || input.GameplayBlocked) return false;
            if (Hand.kind == InventoryKind.Physical) { holder.Drop(); return true; }
            var camera = GetComponentInChildren<Camera>();
            if (camera == null) return false;
            WorldInventoryItem.Spawn(this, Hand, camera.transform.position + camera.transform.forward * 0.75f, camera.transform.rotation);
            RemoveHand(); return true;
        }
        public int Count(InventoryKind kind)
        {
            if (kind == InventoryKind.Darts) return Darts;
            int total = HasUnassignedItem && Hand.kind == kind ? Hand.quantity : 0;
            foreach (var entry in slots) if (entry != null && entry.kind == kind) total += entry.quantity;
            return total;
        }
        public bool Consume(InventoryKind kind)
        {
            if (kind == InventoryKind.Darts)
            {
                if (Darts <= 0) return false;
                Darts--; Save(); return true;
            }
            InventoryEntry entry = IsEquipped(kind) ? Hand : Array.Find(slots, e => e != null && e.kind == kind);
            if (entry == null) return false;
            if (--entry.quantity == 0)
            {
                int index = Array.IndexOf(slots, entry); if (index >= 0) slots[index] = null;
                if (Hand == entry) Hand = null;
            }
            Save(); return true;
        }
        public bool AddDarts(int quantity)
        {
            if (quantity <= 0 || quantity > MaximumDarts - Darts) return false;
            Darts += quantity; Save(); return true;
        }
        private void LateUpdate()
        {
            if (!input.GameplayBlocked)
            {
                if (input.DropPressed) DropHand();
                else if (input.SlotPressed >= 0) SelectSlot(input.SlotPressed);
            }
            if (visualEntry != Hand)
            {
                if (handVisual != null) Destroy(handVisual);
                visualEntry = Hand;
                // The hands rig lives under the camera, not on the inventory's GameObject.
                bool existingArmsVisual = GetComponentInChildren<FirstPersonArms>(true) != null &&
                    (Hand?.kind == InventoryKind.Cage || Hand?.kind == InventoryKind.Trap);
                if (Hand != null && Hand.kind != InventoryKind.Gun && Hand.kind != InventoryKind.Physical && !existingArmsVisual)
                {
                    var camera = GetComponentInChildren<Camera>();
                    var visual = WorldInventoryItem.Spawn(this, Hand, camera.transform.position, Quaternion.identity);
                    handVisual = visual.gameObject; visual.enabled = false;
                    handVisual.GetComponent<Rigidbody>().isKinematic = true;
                    var box = handVisual.GetComponent<BoxCollider>(); box.enabled = false;
                    handVisual.transform.SetParent(camera.transform, false);
                    float size = Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z));
                    handVisual.transform.localScale = Vector3.one * (0.4f / Mathf.Max(0.1f, size));
                    handVisual.transform.localPosition = new Vector3(0.25f, -0.25f, 0.65f) - box.center * handVisual.transform.localScale.x;
                    handVisual.transform.localRotation = Quaternion.identity;
                }
            }
            if (handVisual != null) handVisual.SetActive(!input.GameplayBlocked);
        }
        private void OnDestroy() { if (holder != null) holder.Released -= OnPhysicalReleased; }
    }
}
