using UnityEngine;

namespace HuntHappens.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PickupItem : MonoBehaviour
    {
        [SerializeField] private string pickupPrompt = "E — Поднять";
        [SerializeField, Min(0)] private float carryDistance;
        [SerializeField, Range(0.2f, 2)] private float followMultiplier = 1f;
        [SerializeField, Range(0, 12)] private float throwMultiplier = 1f;
        public string PickupPrompt => GetComponent<HuntHappens.SmallGame.CrowAI>() != null ? "E — Поднять спящую ворону · $10 у Семёныча" : GetComponent<HuntHappens.Animals.DeerSedation>()?.State == HuntHappens.Animals.DeerSedation.SedationState.Trapped ? "E — Забрать оленя" : pickupPrompt;
        public float CarryDistance => carryDistance;
        public float FollowMultiplier => followMultiplier;
        public float ThrowMultiplier => throwMultiplier;
        public void ConfigureAnimal()
        {
            pickupPrompt = "E — Поднять оленя";
            carryDistance = 2.8f;
            followMultiplier = 0.7f;
            throwMultiplier = 8f;
        }
        public Rigidbody Body => GetComponent<Rigidbody>();
        public ItemHolder Holder { get; private set; }
        public ItemHolder LastHolder { get; private set; }
        public float LastReleasedTime { get; private set; } = float.NegativeInfinity;

        public bool CanPickUp(float maximumMass)
        {
            Rigidbody body = Body;
            var crow = GetComponent<HuntHappens.SmallGame.CrowAI>();
            if (crow != null && !crow.CanPickUp) return false;
            var deer = GetComponent<HuntHappens.Animals.DeerSedation>();
            if (deer != null && (!deer.isActiveAndEnabled || deer.IsDelivered || deer.WakingFromCarry || deer.Cart != null || deer.DeliveryReserved ||
                (deer.State != HuntHappens.Animals.DeerSedation.SedationState.Unconscious &&
                 deer.State != HuntHappens.Animals.DeerSedation.SedationState.Trapped))) return false;
            if (!isActiveAndEnabled || Holder != null || body.isKinematic ||
                !body.detectCollisions || (deer == null && body.mass > maximumMass)) return false;
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger && collider.attachedRigidbody == body)
                    return true;
            return false;
        }

        public bool TryClaim(ItemHolder holder, float maximumMass)
        {
            if (holder == null || !CanPickUp(maximumMass)) return false;
            GetComponent<HuntHappens.Animals.DeerSedation>()?.ReleaseTrapForCarry();
            Holder = holder;
            LastHolder = holder;
            var deer = GetComponent<HuntHappens.Animals.DeerSedation>();
            if (deer != null)
                foreach (var leader in FindObjectsByType<HuntHappens.Animals.DeerLeader>()) leader.NoticeAttack(deer);
            return true;
        }

        public void ReleaseClaim(ItemHolder holder)
        {
            if (Holder == holder) { Holder = null; LastReleasedTime = Time.time; }
        }

        private void OnDisable()
        {
            if (Holder != null) Holder.Drop();
        }
    }
}
