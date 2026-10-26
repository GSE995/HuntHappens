using System;
using UnityEngine;

namespace HuntHappens.Shop
{
    // One snapshot prevents a saved wallet and saved upgrade levels from diverging.
    public static class ShopProgress
    {
        public const int StartingBalance = 15;
        public const string PlayerKey = "HuntHappens.Shop.Progress.v1";
        public const string TestKey = "HuntHappens.Shop.AutomatedChecks.v1";
        public static bool InventoryChecksRunning { get; set; }
        public static string Key => Application.isBatchMode || InventoryChecksRunning ? TestKey : PlayerKey;
        public static bool PersistenceProbe => Application.isBatchMode && Array.IndexOf(Environment.GetCommandLineArgs(), "-huntShopPersistenceProbe") >= 0;
        [Serializable]
        public sealed class CarriedRat
        {
            public int kind;
            public Vector3 home;
            public double wakeAt;
            [NonSerialized] public HuntHappens.SmallGame.RatAI animal;
        }
        [Serializable]
        public sealed class Data
        {
            public int cages, bread, rats, darts;
            public int inventoryVersion;
            public HuntHappens.Interaction.InventoryEntry[] inventory;
            public HuntHappens.Interaction.InventoryEntry hand;
            public int greyRats;
            public int largeRats;
            public CarriedRat[] carriedRats = new CarriedRat[0];
            public int version = 1;
            public int balance = StartingBalance;
            public int acceptedDeer;
            public string[] returnedDeerIds = new string[0];
            public bool deerAdvanceGranted;
            public bool cartPurchased;
            public int[] levels = new int[4];
        }
        public static Data Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return new Data();
            try
            {
                var data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key));
                if (data == null || data.version != 1 || data.levels == null || data.levels.Length != 4)
                    throw new ArgumentException("Unsupported or incomplete shop save");
                data.balance = Mathf.Max(0, data.balance);
                for (int i = 0; i < 4; i++) data.levels[i] = Mathf.Clamp(data.levels[i], 0, 3);
                return data;
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("Hunt Happens: shop save is invalid; using starting balance and base upgrades.");
                return new Data();
            }
        }
        public static void Save(int balance, int[] levels)
        {
            try
            {
                var data = Load(); data.balance = Mathf.Max(0, balance); data.levels = levels;
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
                PlayerPrefs.Save();
            }
            catch (PlayerPrefsException exception) { Debug.LogWarning("Cannot save shop progress: " + exception.Message); }
        }
        public static void SaveSupplies(int cages, int bread, int rats, int darts, int greyRats = 0, int largeRats = 0, CarriedRat[] carriedRats = null)
        {
            var data = Load(); data.cages = cages; data.bread = bread; data.rats = rats; data.darts = darts; data.greyRats = greyRats; data.largeRats = largeRats;
            if (carriedRats != null) data.carriedRats = carriedRats;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data)); PlayerPrefs.Save();
        }
        public static void Reset() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
        public static void SaveInventory(HuntHappens.Interaction.InventoryEntry[] slots, HuntHappens.Interaction.InventoryEntry hand, int darts)
        {
            var data = Load(); data.inventoryVersion = 2; data.darts = darts;
            // JsonUtility's inline class serialization cannot represent null entries reliably.
            // Copy into explicit empty records so serialization never mutates the live inventory.
            data.inventory = new HuntHappens.Interaction.InventoryEntry[slots.Length];
            for (int i = 0; i < slots.Length; i++) data.inventory[i] = SavedEntry(slots[i]);
            data.hand = SavedEntry(hand);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data)); PlayerPrefs.Save();
        }
        private static HuntHappens.Interaction.InventoryEntry SavedEntry(HuntHappens.Interaction.InventoryEntry entry)
        {
            if (entry == null || entry.kind == HuntHappens.Interaction.InventoryKind.Physical)
                return new HuntHappens.Interaction.InventoryEntry { kind = HuntHappens.Interaction.InventoryKind.None, quantity = 0 };
            return new HuntHappens.Interaction.InventoryEntry { kind = entry.kind, quantity = entry.quantity, cageRat = entry.kind == HuntHappens.Interaction.InventoryKind.Cage ? Mathf.Clamp(entry.cageRat, 0, 3) : 0 };
        }
    }
}
