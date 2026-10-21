using UnityEngine;

namespace HuntHappens.Shop
{
    [DisallowMultipleComponent]
    public sealed class ShopInteractable : MonoBehaviour
    {
        [SerializeField] private Transform interactionPoint;
        [SerializeField, Min(0.5f)] private float interactionDistance = 3f;
        public Transform InteractionPoint => interactionPoint;
        public void Configure(Transform point) => interactionPoint = point;
        public bool CanUse(Transform player, Vector3 eye)
        {
            if (!isActiveAndEnabled || player == null || interactionPoint == null ||
                Vector3.Distance(eye, interactionPoint.position) > interactionDistance) return false;
            Vector3 delta = interactionPoint.position - eye;
            foreach (RaycastHit hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(player) && !hit.collider.transform.IsChildOf(transform)) return false;
            return true;
        }
    }
}
