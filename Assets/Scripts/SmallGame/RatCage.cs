using UnityEngine;
using HuntHappens.Interaction;
namespace HuntHappens.SmallGame
{
    public sealed class RatCage : MonoBehaviour
    {
        public Transform door;
        public GameObject bait;
        public RatHunting Owner { get; set; }
        public RatAI Captive { get; private set; }
        public InventoryKind BaitKind { get; private set; }
        public bool Baited => RatBait.IsBait(BaitKind);
        public const float ScareRadius = 3f;
        public const float ScareDelay = 10f;
        public bool CanStealBait => isActiveAndEnabled && !recovered && Captive == null && StoredCargo == null &&
            (BaitKind == InventoryKind.Bread || BaitKind == InventoryKind.CheeseSandwich);
        public Vector3 CrowApproachPoint => transform.TransformPoint(new Vector3(0, 0.2f, 0.65f));
        // Both consumers run synchronously on Unity's main thread. Clearing BaitKind
        // before returning prevents a second bird or rat from consuming the same bait.
        public bool TryStealBait()
        {
            if (!CanStealBait) return false;
            BaitKind = InventoryKind.None;
            bait.SetActive(false);RefreshBaitVisual();
            foreach (var rat in FindObjectsByType<RatAI>(FindObjectsSortMode.None)) rat.CancelBaitTarget(this);
            return true;
        }
        private float scaredUntil;
        public bool PlayerNearby => Owner != null && Vector3.Distance(Owner.transform.position, transform.position) < ScareRadius;
        public float FearRemaining
        {
            get { RefreshFear(); return Mathf.Max(0, scaredUntil - Time.time); }
        }
        private void Update() => RefreshFear();
        private void RefreshBaitVisual() => GetComponent<CageBaitVisual>()?.Show(BaitKind);
        private void RefreshFear()
        {
            if (Baited && PlayerNearby) scaredUntil = Time.time + ScareDelay;
        }
        public bool Accepts(RatAI rat) => !recovered && StoredCargo == null && Captive == null && Baited && rat != null && !rat.Caught && BaitKind == RatBait.ForRat(rat.Kind) && FearRemaining <= 0;
        private string WaitingPrompt => PlayerNearby ? "Крысы боятся. Отойдите на 3 м" : FearRemaining > 0 ? "Крысы пока боятся: " + Mathf.CeilToInt(FearRemaining) + " с" : "Ждём крысу";
        public HuntHappens.Interaction.InventoryEntry StoredCargo { get; private set; }
        private bool recovered;
        public string Prompt => StoredCargo != null ? StoredCargo.Title + "\nE — забрать крысу и клетку" : Captive != null ? (Captive.isAggressive ? "Крупная крыса — $25" : Captive.isGrey ? "Серая крыса — $15" : "Чёрная крыса — $10") + "\nE — забрать крысу и клетку" : Baited ? "Приманка: " + RatBait.Name(BaitKind) + "\n" + WaitingPrompt + " | E — забрать клетку" : (Owner != null && RatBait.IsBait(Owner.SelectedBait) ? "E — положить: " + RatBait.Name(Owner.SelectedBait) : "E — забрать клетку") + "\nПриманка: выберите предмет в руках или первую ячейку с приманкой\n" + RatBait.Preferences;
        public void RestoreCargo(int kind, RatCage template, bool baited = false, InventoryKind baitKind = InventoryKind.None)
        {
            if (kind < 1 || kind > 3)
            {
                BaitKind = baited ? (RatBait.IsBait(baitKind) ? baitKind : InventoryKind.Bread) : InventoryKind.None; bait.SetActive(Baited);RefreshBaitVisual();
                if (Baited) scaredUntil = Time.time + ScareDelay;
                return;
            }
            StoredCargo = new HuntHappens.Interaction.InventoryEntry { kind = HuntHappens.Interaction.InventoryKind.Cage, cageRat = kind };
            BaitKind = InventoryKind.None; bait.SetActive(false);RefreshBaitVisual();
            var visual = gameObject.AddComponent<CageCargoVisual>();
            visual.Configure(template); visual.Bind(StoredCargo);
        }
        public void ClearStoredCargo()
        {
            if (StoredCargo == null) return;
            StoredCargo.cageRat = 0;
            StoredCargo = null;
            GetComponent<CageCargoVisual>()?.Refresh();
        }
        public bool Interact(RatHunting player)
        {
            if (recovered || player == null || player != Owner) return false;
            if (StoredCargo != null)
            {
                if (!player.GetComponent<HuntHappens.Interaction.PlayerInventory>().Receive(StoredCargo)) return false;
                recovered = true;
                player.Recover(false); // This rat was already credited when first collected.
                gameObject.SetActive(false); Destroy(gameObject); return true;
            }
            if (!Baited && Captive == null && RatBait.IsBait(player.SelectedBait))
            {
                var selected = player.SelectedBait;
                if (!player.UseBait(selected)) return false;
                BaitKind = selected; scaredUntil = Time.time + ScareDelay; bait.SetActive(true);RefreshBaitVisual(); return true;
            }
            if (!player.GetComponent<HuntHappens.Interaction.PlayerInventory>().Receive(new HuntHappens.Interaction.InventoryEntry {
                kind = HuntHappens.Interaction.InventoryKind.Cage, cageRat = Captive != null ? Captive.Kind + 1 : 0,
                cageBaited = Baited && Captive == null, cageBait = BaitKind })) return false;
            recovered = true;
            bool caught = Captive != null;
            if (caught) Captive.ReleaseForRespawn();
            player.Recover(caught, caught && Captive.isGrey, caught && Captive.isAggressive); gameObject.SetActive(false); Destroy(gameObject); return true;
        }
        public bool TryCapture(RatAI rat)
        {
            if (!Accepts(rat) || Vector3.Distance(rat.transform.position, transform.position) > 0.45f) return false;
            Captive = rat; BaitKind = InventoryKind.None; bait.SetActive(false);RefreshBaitVisual();
            door.localPosition = new Vector3(0, 0.25f, 0.44f);
            rat.Capture(this); return true;
        }
    }
}
