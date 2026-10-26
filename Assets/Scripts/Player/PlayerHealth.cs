using UnityEngine;
namespace HuntHappens.Player
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        public int Maximum => 100;
        public int Current { get; private set; } = 100;
        public float LastHitAt { get; private set; } = -100;
        private Vector3 spawn;
        private float immuneUntil;
        private float returnedUntil;
        private int medicalCharge;
        private void Start() => spawn = transform.position;
        public string Status => Time.time < returnedUntil ? "Вы потеряли сознание и вернулись к старту — 100 HP. Медицинские услуги: −$" + medicalCharge :
            (Time.time - LastHitAt < 0.6f ? "<color=#FF6655>" : "<color=#FFFFFF>") + "Здоровье: " + Current + "/100</color>";
        public bool TakeDamage(int damage)
        {
            if (damage <= 0 || Time.time < immuneUntil || GetComponent<PrototypeInput>().GameplayBlocked) return false;
            Current = Mathf.Max(0, Current - damage); LastHitAt = Time.time;
            if (Current == 0)
            {
                var wallet = FindAnyObjectByType<HuntHappens.Delivery.DeliveryManager>();
                medicalCharge = wallet != null ? Mathf.Min(50, wallet.Balance) : 0;
                if (medicalCharge > 0) wallet.TrySpend(medicalCharge);
                GetComponent<HuntHappens.Interaction.PlayerInventory>()?.DropAll(transform.position);
                var controller = GetComponent<CharacterController>();
                bool enabledBefore = controller.enabled; controller.enabled = false;
                transform.position = spawn; controller.enabled = enabledBefore;
                GetComponent<FirstPersonMotor>()?.ApplyImpulse(Vector3.zero);
                Current = Maximum; immuneUntil = Time.time + 3; returnedUntil = Time.time + 4;
            }
            return true;
        }
    }
}
