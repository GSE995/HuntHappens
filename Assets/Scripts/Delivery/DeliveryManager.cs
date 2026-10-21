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
        public int Balance { get; private set; }
        public int DeliveredCount { get; private set; }
        public int Goal => goal;
        public bool QuestComplete => DeliveredCount >= goal;
        public string Notification { get; private set; } = "";
        public float LastRewardTime { get; private set; } = float.NegativeInfinity;
        private int notificationFrame = -1;
        public void SetSpawner(DeerSpawner value) => spawner = value;

        public bool TryDeliver(DeerSedation deer, DeliveryZone zone)
        {
            if (!isActiveAndEnabled || zone == null || !zone.IsReady(deer) || !deer.TryMarkDelivered()) return false;
            // Claim on the animal before paying: even another zone/manager cannot pay for it twice.
            Balance += rewardPerDeer;
            DeliveredCount++;
            if (notificationFrame != Time.frameCount) Notification = "";
            Notification += (Notification.Length == 0 ? "" : "\n") + "Олень доставлен! +$" + rewardPerDeer;
            notificationFrame = Time.frameCount;
            LastRewardTime = Time.time;
            deer.gameObject.SetActive(false);
            Destroy(deer.gameObject);
            if (spawner != null) spawner.RequestReplacement();
            return true;
        }
    }
}
