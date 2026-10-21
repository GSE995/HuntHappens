using HuntHappens.Player;
using HuntHappens.Animals;
using HuntHappens.Delivery;
using UnityEngine;

namespace HuntHappens.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypeInput), typeof(ItemHolder))]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private Camera view;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        private PrototypeInput input;
        private ItemHolder holder;
        [SerializeField] private PickupTruckDelivery deliveryTruck;
        public PickupTruckDelivery DeliveryTarget { get; private set; }
        public void ConfigureDelivery(PickupTruckDelivery truck) => deliveryTruck = truck;
        public bool TrySubmitDelivery()
        {
            if (holder == null || holder.IsHolding || view == null || deliveryTruck == null || !deliveryTruck.CanUse(transform, view.transform.position)) return false;
            LastHandledFrame = Time.frameCount;
            return deliveryTruck.Submit(transform, view.transform.position, holder) > 0;
        }
        public PickupItem Target { get; private set; }
        public DeerSedation ObservedDeer { get; private set; }
        public int LastHandledFrame { get; private set; } = -1;
        public bool IsHolding => holder != null && holder.IsHolding;
        public void SetView(Camera cameraComponent)
        {
            view = cameraComponent;
            GetComponent<ItemHolder>().SetView(view.transform);
        }

        private void Awake()
        {
            input = GetComponent<PrototypeInput>();
            holder = GetComponent<ItemHolder>();
            if (view == null) view = GetComponentInChildren<Camera>();
            if (view != null) holder.SetView(view.transform);
        }

        // The motor reads PrototypeInput in Update; consume the same frame after camera rotation.
        private void LateUpdate()
        {
            Target = null;
            ObservedDeer = null;
            DeliveryTarget = null;
            if (view == null || Cursor.lockState != CursorLockMode.Locked) return;
            if (holder.IsHolding)
            {
                if (input.InteractPressed || input.ThrowPressed) LastHandledFrame = Time.frameCount;
                if (input.InteractPressed) holder.Drop();
                else if (input.ThrowPressed) holder.Throw();
                return;
            }
            if (deliveryTruck != null && deliveryTruck.CanUse(transform, view.transform.position))
            {
                DeliveryTarget = deliveryTruck;
                if (input.InteractPressed) TrySubmitDelivery();
                return;
            }
            // First solid hit blocks selection: items cannot be picked up through trees or walls.
            RaycastHit[] hits = Physics.RaycastAll(view.transform.position, view.transform.forward,
                Mathf.Max(interactionDistance, 60f), interactionLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                PickupItem item = hit.rigidbody != null ? hit.rigidbody.GetComponent<PickupItem>() : null;
                ObservedDeer = hit.collider.GetComponentInParent<DeerSedation>();
                if (hit.distance <= interactionDistance && item != null && item.CanPickUp(holder.MaximumMass)) Target = item;
                break;
            }
            if (input.InteractPressed && Target != null)
            {
                LastHandledFrame = Time.frameCount;
                holder.TryPickUp(Target);
            }
        }

        private void OnDisable()
        {
            Target = null;
            ObservedDeer = null;
            DeliveryTarget = null;
            if (holder != null) holder.Drop();
        }
    }
}
