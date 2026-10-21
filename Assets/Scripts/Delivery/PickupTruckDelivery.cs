using HuntHappens.Interaction;
using UnityEngine;

namespace HuntHappens.Delivery
{
    [DisallowMultipleComponent]
    public sealed class PickupTruckDelivery : MonoBehaviour
    {
        [SerializeField] private DeliveryZone zone;
        [SerializeField] private DeliveryManager manager;
        [SerializeField] private Transform rearInteractionPoint;
        [SerializeField, Min(0.5f)] private float useDistance = 2.8f;
        public DeliveryZone Zone => zone;
        public Transform RearPoint => rearInteractionPoint;
        public void Configure(DeliveryZone value, Transform rear, DeliveryManager deliveryManager)
        { zone = value; rearInteractionPoint = rear; manager = deliveryManager; }

        public bool CanUse(Transform player, Vector3 eye)
        {
            if (!isActiveAndEnabled || zone == null || manager == null || !manager.isActiveAndEnabled ||
                rearInteractionPoint == null || zone.ReadyCount == 0 ||
                Vector3.Distance(eye, rearInteractionPoint.position) > useDistance) return false;
            Vector3 localPlayer = transform.InverseTransformPoint(player.position);
            Vector3 localRear = transform.InverseTransformPoint(rearInteractionPoint.position);
            if (localPlayer.z > localRear.z + 0.8f) return false;
            Vector3 direction = rearInteractionPoint.position - eye;
            foreach (RaycastHit hit in Physics.RaycastAll(eye, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(player) && !hit.collider.transform.IsChildOf(transform)) return false;
            return true;
        }

        public int Submit(Transform player, Vector3 eye, ItemHolder holder)
        {
            if (holder == null || holder.IsHolding || !CanUse(player, eye)) return 0;
            int count = 0;
            foreach (var deer in zone.GetReadyAnimals()) if (manager.TryDeliver(deer, zone)) count++;
            return count;
        }
    }
}
