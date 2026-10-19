using UnityEngine;

namespace HuntHappens.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PickupItem : MonoBehaviour
    {
        public Rigidbody Body => GetComponent<Rigidbody>();
        public ItemHolder Holder { get; private set; }

        public bool CanPickUp(float maximumMass)
        {
            Rigidbody body = Body;
            if (!isActiveAndEnabled || Holder != null || body.isKinematic ||
                !body.detectCollisions || body.mass > maximumMass) return false;
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger && collider.attachedRigidbody == body)
                    return true;
            return false;
        }

        public bool TryClaim(ItemHolder holder, float maximumMass)
        {
            if (holder == null || !CanPickUp(maximumMass)) return false;
            Holder = holder;
            return true;
        }

        public void ReleaseClaim(ItemHolder holder)
        {
            if (Holder == holder) Holder = null;
        }

        private void OnDisable()
        {
            if (Holder != null) Holder.Drop();
        }
    }
}
