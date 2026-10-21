using System;
using UnityEngine;

namespace HuntHappens.Shop
{
    // One snapshot prevents a saved wallet and saved upgrade levels from diverging.
    public static class ShopProgress
    {
        public const string PlayerKey = "HuntHappens.Shop.Progress.v1";
        public const string TestKey = "HuntHappens.Shop.AutomatedChecks.v1";
        public static string Key => Application.isBatchMode ? TestKey : PlayerKey;
        public static bool PersistenceProbe => Application.isBatchMode && Array.IndexOf(Environment.GetCommandLineArgs(), "-huntShopPersistenceProbe") >= 0;
        [Serializable]
        public sealed class Data
        {
            public int version = 1;
            public int balance;
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
                Debug.LogWarning("Hunt Happens: shop save is invalid; using zero balance and base upgrades.");
                return new Data();
            }
        }
        public static void Save(int balance, int[] levels)
        {
            try
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(new Data { balance = Mathf.Max(0, balance), levels = levels }));
                PlayerPrefs.Save();
            }
            catch (PlayerPrefsException exception) { Debug.LogWarning("Cannot save shop progress: " + exception.Message); }
        }
        public static void Reset() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
    }
}
