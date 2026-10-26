using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.Player;
using UnityEngine;

namespace HuntHappens.Shop
{
    [DefaultExecutionOrder(200), DisallowMultipleComponent]
    public sealed class ShopManager : MonoBehaviour
    {
        [SerializeField] private DeliveryManager wallet;
        [SerializeField] private UpgradeManager upgrades;
        [SerializeField] private ShopUI ui;
        [SerializeField] private ShopInteractable kiosk;
        [SerializeField] private PrototypeInput input;
        [SerializeField, Min(0.1f)] private float purchaseCooldown = 0.4f;
        public const int DefaultChocolatePrice = 750;
        [SerializeField, Min(1)] private int chocolatePrice = DefaultChocolatePrice;
        public int ChocolatePrice => chocolatePrice;
        private bool changingProgress;
        private bool worldTrade;
        public bool CanTradeSupplies => isActiveAndEnabled && Time.unscaledTime >= nextPurchase && (IsOpen || worldTrade);
        private float nextPurchase;
        public bool IsOpen { get; private set; }
        public string Message { get; private set; } = "";
        public DeliveryManager Wallet => wallet;
        public UpgradeManager Upgrades => upgrades;
        public ShopInteractable Kiosk => kiosk;
        public bool CanPurchaseNow => IsOpen && Time.unscaledTime >= nextPurchase;
        public void Configure(DeliveryManager money, UpgradeManager effects, ShopUI panel, ShopInteractable worldShop, PrototypeInput controls)
        { wallet = money; upgrades = effects; ui = panel; kiosk = worldShop; input = controls; }
        private void Awake()
        {
            // Batch checks never read, overwrite or delete the player's real progress.
            if (Application.isBatchMode && !ShopProgress.PersistenceProbe) ShopProgress.Reset();
            ReloadProgress();
        }
        private void OnEnable() { if (wallet != null) wallet.BalanceChanged += OnBalanceChanged; }
        private void OnDisable()
        {
            if (wallet != null) wallet.BalanceChanged -= OnBalanceChanged;
            Close();
        }
        public void ReloadProgress()
        {
            if (wallet == null || upgrades == null) return;
            changingProgress = true;
            var data = ShopProgress.Load();
            wallet.RestoreBalance(data.balance);
            upgrades.RestoreLevels(data.levels);
            foreach (var cart in FindObjectsByType<HuntHappens.Transport.DeerCart>(FindObjectsSortMode.None)) cart.ReloadOwnership();
            changingProgress = false;
        }
        private void OnBalanceChanged() { if (!changingProgress) SaveProgress(); }
        public void SaveProgress() { if (wallet != null && upgrades != null) ShopProgress.Save(wallet.Balance, upgrades.CopyLevels()); }
        public bool TryOpen()
        {
            if (!isActiveAndEnabled || IsOpen || input == null || input.GameplayBlocked || upgrades == null || !upgrades.IsConfigured ||
                input.GetComponent<ItemHolder>().IsHolding || kiosk == null || !kiosk.CanUse(input.transform, input.GetComponentInChildren<Camera>().transform.position)) return false;
            IsOpen = true;
            Message = "Выберите улучшение";
            input.SetGameplayBlocked(true);
            if (ui != null) ui.Show(true);
            return true;
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (ui != null) ui.Show(false);
            if (input != null) input.SetGameplayBlocked(false);
        }
        public bool TryTradeWorld(WorldShopItem product, PrototypeInput player)
        {
            if (!isActiveAndEnabled || IsOpen || player != input || input.GameplayBlocked ||
                input.GetComponent<ItemHolder>().IsHolding || product == null || product.Shop != this ||
                WorldShopItem.FindTarget(input.GetComponentInChildren<Camera>(), input.transform) != product) return false;
            if (Time.unscaledTime < nextPurchase) { Message = "Подождите перед следующей покупкой"; return false; }
            worldTrade = true;
            try { return BuySupply(product.Kind); }
            finally { worldTrade = false; }
        }
        public bool BuySupply(int kind)
        {
            var supplies = input.GetComponent<HuntHappens.SmallGame.RatHunting>();
            if (!CanTradeSupplies || supplies == null) return false;
            supplies.RefreshCarriedRats();
            int reward = supplies.SaleTotal;
            bool success = kind == 3 ? supplies.Sell() : supplies.Buy(kind);
            Message = success ? (kind == 3 ? "Крысы сданы: +$" + reward : kind == 2 ? "Боезапас пополнен: " + supplies.Darts : "Предмет в руках: выберите пустую ячейку 1–4") :
                kind != 2 && !input.GetComponent<PlayerInventory>().CanReceive ? "Сначала уберите предмет в пустую ячейку или нажмите Q" : "Недостаточно денег, нет крыс или достигнут лимит боезапаса";
            if (success) nextPurchase = Time.unscaledTime + purchaseCooldown;
            return success;
        }
        public bool TryBuy(UpgradeKind kind)
        {
            if (!CanPurchaseNow || changingProgress || upgrades == null || !upgrades.IsConfigured) return false;
            UpgradeDefinition definition = upgrades.Definition(kind);
            if (definition == null) return false;
            int level = upgrades.Level(kind);
            if (level >= definition.MaximumLevel) { Message = "Уже куплен максимальный уровень"; return false; }
            int price = definition.NextPrice(level);
            if (price <= 0 || wallet.Balance < price) { Message = "Недостаточно денег"; return false; }
            changingProgress = true;
            try
            {
                if (!wallet.TrySpend(price)) return false;
                upgrades.Increase(kind);
                nextPurchase = Time.unscaledTime + purchaseCooldown;
                Message = "Куплено: " + definition.Title + ", уровень " + upgrades.Level(kind);
                SaveProgress();
                return true;
            }
            finally { changingProgress = false; }
        }
        public void ResetProgress()
        {
            ShopProgress.Reset();
            changingProgress = true;
            wallet.RestoreBalance(ShopProgress.StartingBalance);
            upgrades.RestoreLevels(null);
            foreach (var cart in FindObjectsByType<HuntHappens.Transport.DeerCart>(FindObjectsSortMode.None)) cart.ReloadOwnership();
            changingProgress = false;
            input.GetComponent<HuntHappens.SmallGame.RatHunting>()?.Reload();
            input.GetComponent<PlayerInventory>()?.Reload();
            nextPurchase = 0;
            Message = "Прогресс сброшен";
        }
        private void OnApplicationPause(bool paused) { if (paused) SaveProgress(); }
        private void OnApplicationQuit() { if (Application.isBatchMode && !ShopProgress.PersistenceProbe) ShopProgress.Reset(); else SaveProgress(); }
    }
}
