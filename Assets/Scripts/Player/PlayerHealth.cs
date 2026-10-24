using UnityEngine;
namespace HuntHappens.Player
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        public int Current { get; private set; } = 100;
        public float LastHitAt { get; private set; } = -100;
        private Vector3 spawn;
        private float immuneUntil;
        private float returnedUntil;
        private void Start() => spawn = transform.position;
        public string Status => Time.time < returnedUntil ? "Вы потеряли сознание и вернулись к старту — 100 HP" :
            (Time.time - LastHitAt < 0.6f ? "<color=#FF6655>" : "<color=#FFFFFF>") + "Здоровье: " + Current + "/100</color>";
        public bool TakeDamage(int damage)
        {
            if (damage <= 0 || Time.time < immuneUntil || GetComponent<PrototypeInput>().GameplayBlocked) return false;
            Current = Mathf.Max(0, Current - damage); LastHitAt = Time.time;
            if (Current == 0)
            {
                var controller = GetComponent<CharacterController>();
                bool enabledBefore = controller.enabled; controller.enabled = false;
                transform.position = spawn; controller.enabled = enabledBefore;
                Current = 100; immuneUntil = Time.time + 3; returnedUntil = Time.time + 4;
            }
            return true;
        }
    }
}
