using System.Collections.Generic;
using HuntHappens.Animals;
using HuntHappens.Interaction;
using UnityEngine;

namespace HuntHappens.Delivery
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class DeliveryZone : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float settleDuration = 1.2f;
        [SerializeField, Min(0)] private float maximumSpeed = 0.6f;
        [SerializeField, Min(0)] private float maximumAngularSpeed = 1f;
        [SerializeField, Range(0, 0.1f)] private float containmentTolerance = 0.04f;
        [Header("Automatic farm reception (off for truck beds)")]
        [SerializeField] private bool automaticReception;
        [SerializeField] private ItemHolder player;
        [SerializeField] private DeliveryManager manager;
        [SerializeField] private DeerEnclosure enclosure;
        public bool AutomaticReception => automaticReception;
        public DeerEnclosure Enclosure => enclosure;
        public void ConfigureReception(ItemHolder owner, DeliveryManager wallet, DeerEnclosure pen)
        { automaticReception = true; player = owner; manager = wallet; enclosure = pen; }
        private bool Inside(Vector3 point)
        {
            Vector3 p = transform.InverseTransformPoint(point) - Volume.center;
            Vector3 h = Volume.size * 0.5f;
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }
        private BoxCollider volume;
        private readonly HashSet<DeerSedation> touching = new HashSet<DeerSedation>();
        private readonly HashSet<DeerSedation> current = new HashSet<DeerSedation>();
        private readonly Dictionary<DeerSedation, float> settledSince = new Dictionary<DeerSedation, float>();
        private readonly List<DeerSedation> remove = new List<DeerSedation>();
        public BoxCollider Volume => volume != null ? volume : GetComponent<BoxCollider>();
        public float SettleDuration => settleDuration;
        public int ReadyCount => GetReadyAnimals().Count;

        private void Awake()
        {
            volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other) => Track(other);
        private void OnTriggerStay(Collider other) => Track(other);
        private void Track(Collider other)
        {
            DeerSedation deer = other.GetComponentInParent<DeerSedation>();
            if (deer != null) touching.Add(deer);
        }

        private void FixedUpdate()
        {
            if (automaticReception)
            {
                if (manager == null || player == null) return;
                // Carried bodies can lag behind the player's collider at the gate.
                if (Inside(player.transform.position) && player.HeldItem != null)
                    manager.TryDeliver(player.HeldItem.GetComponent<DeerSedation>(), this);
                foreach (var deer in FindObjectsByType<DeerSedation>())
                    if (IsReady(deer)) manager.TryDeliver(deer, this);
                return;
            }
            current.Clear();
            // Reconcile trigger membership: collider swaps, disable/destruction and multiple colliders
            // must not leave stale candidates or accidentally remove a still-overlapping animal.
            Vector3 scale = transform.lossyScale;
            Vector3 half = Vector3.Scale(volume.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            foreach (Collider collider in Physics.OverlapBox(transform.TransformPoint(volume.center), half,
                         transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                DeerSedation deer = collider.GetComponentInParent<DeerSedation>();
                if (deer != null) { touching.Add(deer); current.Add(deer); }
            }
            remove.Clear();
            foreach (DeerSedation deer in touching)
            {
                if (deer == null || !current.Contains(deer) || !Eligible(deer))
                {
                    if (deer != null) deer.ReleaseDeliveryReservation(this);
                    settledSince.Remove(deer);
                    remove.Add(deer);
                    continue;
                }
                // Pause waking only while a released, unconscious animal is fully inside the bed.
                deer.ReserveForDelivery(this);
                Rigidbody body = deer.GetComponent<Rigidbody>();
                if (body.linearVelocity.magnitude <= maximumSpeed && body.angularVelocity.magnitude <= maximumAngularSpeed)
                {
                    if (!settledSince.ContainsKey(deer)) settledSince.Add(deer, Time.time);
                }
                else settledSince.Remove(deer);
            }
            foreach (DeerSedation deer in remove) touching.Remove(deer);
        }

        private bool Eligible(DeerSedation deer)
        {
            if (!isActiveAndEnabled || deer == null || !deer.isActiveAndEnabled || deer.IsDelivered ||
                deer.State != DeerSedation.SedationState.Unconscious) return false;
            PickupItem pickup = deer.GetComponent<PickupItem>();
            Rigidbody body = deer.GetComponent<Rigidbody>();
            BoxCollider collider = deer.GetComponent<BoxCollider>();
            if (pickup == null || pickup.Holder != null || float.IsNegativeInfinity(pickup.LastReleasedTime) ||
                body == null || body.isKinematic || !body.useGravity || collider == null || !collider.enabled) return false;
            // All corners of the physical sleeping body must fit, not just its center or antlers.
            Vector3 half = collider.size * 0.5f;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 point = collider.transform.TransformPoint(collider.center + Vector3.Scale(half, new Vector3(x, y, z)));
                        Vector3 local = transform.InverseTransformPoint(point) - Volume.center;
                        Vector3 bounds = Volume.size * 0.5f;
                        // A few centimeters account for contact offsets at the bed floor.
                        if (Mathf.Abs(local.x) > bounds.x + containmentTolerance || Mathf.Abs(local.y) > bounds.y + containmentTolerance || Mathf.Abs(local.z) > bounds.z + containmentTolerance)
                            return false;
                    }
            return true;
        }

        public bool IsReady(DeerSedation deer)
        {
            if (automaticReception)
            {
                if (!isActiveAndEnabled || player == null || deer == null || !deer.isActiveAndEnabled ||
                    deer.IsDelivered || deer.State != DeerSedation.SedationState.Unconscious) return false;
                if (deer.Cart != null)
                    return deer.Cart.Cargo == deer && deer.Cart.LoadedBy == player &&
                        Inside(deer.Cart.transform.position) && Time.time - deer.Cart.LastMovedAt >= settleDuration;
                var pickup = deer.GetComponent<PickupItem>();
                return pickup != null && pickup.LastHolder == player &&
                    (pickup.Holder == null || pickup.Holder == player) &&
                    (Inside(deer.transform.position) || (pickup.Holder == player && Inside(player.transform.position)));
            }
            if (!Eligible(deer) || !settledSince.TryGetValue(deer, out float since)) return false;
            Rigidbody body = deer.GetComponent<Rigidbody>();
            return Time.time - deer.GetComponent<PickupItem>().LastReleasedTime >= settleDuration &&
                body.linearVelocity.magnitude <= maximumSpeed && body.angularVelocity.magnitude <= maximumAngularSpeed &&
                Time.time - since >= settleDuration;
        }

        public List<DeerSedation> GetReadyAnimals()
        {
            var result = new List<DeerSedation>();
            foreach (DeerSedation deer in touching) if (IsReady(deer)) result.Add(deer);
            return result;
        }

        private void OnDisable()
        {
            foreach (DeerSedation deer in touching) if (deer != null) deer.ReleaseDeliveryReservation(this);
            touching.Clear();
            settledSince.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider box = Volume;
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
