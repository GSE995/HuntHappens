using HuntHappens.Animals;
using UnityEngine;

namespace HuntHappens.Delivery
{
    [DisallowMultipleComponent]
    public sealed class DeliveryManager : MonoBehaviour
    {
        [SerializeField, Min(1)] private int rewardPerDeer = 100;
        [SerializeField, Min(1)] private int goal = 3;
        [SerializeField] private DeerSpawner spawner;
        private readonly System.Collections.Generic.HashSet<string> roster = new System.Collections.Generic.HashSet<string>();
        private readonly System.Collections.Generic.HashSet<string> returned = new System.Collections.Generic.HashSet<string>();
        private void Start()
        {
            var zones = FindObjectsByType<DeerCaptureZone>();
            if (zones.Length == 0) return;
            foreach (var zone in zones) for (int i = 0; i < zone.Count; i++) roster.Add(zone.AnimalId(i));
            goal = roster.Count;
            var data = HuntHappens.Shop.ShopProgress.Load();
            foreach (string id in data.returnedDeerIds ?? new string[0]) if (roster.Contains(id)) returned.Add(id);
            DeliveredCount = returned.Count;
        }
        public int Balance { get; private set; }
        public event System.Action BalanceChanged;
        public event System.Action<int> DeerRewarded;
        private int upgradedReward;
        public int RewardPerDeer => upgradedReward > 0 ? upgradedReward : rewardPerDeer;
        public void SetReward(int value) => upgradedReward = Mathf.Max(1, value);
        public void RestoreBalance(int value) { Balance = Mathf.Max(0, value); BalanceChanged?.Invoke(); }
        public bool TrySpend(int amount)
        {
            if (amount <= 0 || Balance < amount) return false;
            Balance -= amount;
            BalanceChanged?.Invoke();
            return true;
        }
        public int DeliveredCount { get; private set; }
        public int Goal => goal;
        public bool QuestComplete => DeliveredCount >= goal;
        public string Notification { get; private set; } = "";
        public float LastRewardTime { get; private set; } = float.NegativeInfinity;
        private int notificationFrame = -1;
        public void SetSpawner(DeerSpawner value) => spawner = value;

        public bool TryDeliver(DeerSedation deer, DeliveryZone zone)
        {
            if (!isActiveAndEnabled || zone == null || !zone.IsReady(deer)) return false;
            if (zone.AutomaticReception) deer.GetComponent<HuntHappens.Interaction.PickupItem>().Holder?.Drop();
            if (roster.Count > 0 && (string.IsNullOrEmpty(deer.QuestId) || !roster.Contains(deer.QuestId) || returned.Contains(deer.QuestId))) return false;
            if (!deer.TryMarkDelivered()) return false;
            deer.Cart?.ReleaseDelivered(deer);
            if (roster.Count > 0)
            {
                returned.Add(deer.QuestId);
                var data = HuntHappens.Shop.ShopProgress.Load();
                data.returnedDeerIds = new System.Collections.Generic.List<string>(returned).ToArray();
                PlayerPrefs.SetString(HuntHappens.Shop.ShopProgress.Key, JsonUtility.ToJson(data));
            }
            // Claim on the animal before paying: even another zone/manager cannot pay for it twice.
            int previousBalance = Balance;
            Balance = (int)System.Math.Min(int.MaxValue, (long)Balance + RewardPerDeer);
            DeliveredCount++;
            if (notificationFrame != Time.frameCount) Notification = "";
            Notification += (Notification.Length == 0 ? "" : "\n") + "Олень сдан: +" + RewardPerDeer;
            notificationFrame = Time.frameCount;
            LastRewardTime = Time.time;
            if (zone.Enclosure != null) zone.Enclosure.Accept(deer);
            else { deer.gameObject.SetActive(false); Destroy(deer.gameObject); }
            BalanceChanged?.Invoke();
            if (Balance > previousBalance) DeerRewarded?.Invoke(Balance - previousBalance);
            if (spawner != null && roster.Count == 0) spawner.RequestReplacement();
            return true;
        }
    }
}
