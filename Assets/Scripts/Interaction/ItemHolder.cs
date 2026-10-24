using System.Collections.Generic;
using UnityEngine;

namespace HuntHappens.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ItemHolder : MonoBehaviour
    {
        [SerializeField] private Transform view;
        [SerializeField, Min(0.5f)] private float holdDistance = 2f;
        [SerializeField, Range(0.25f, 1f)] private float carryDistanceMultiplier = 0.7f;
        [SerializeField] private float carryVerticalOffset = -0.25f;
        [SerializeField, Min(0.1f)] private float maximumMass = 20f;
        [SerializeField, Min(0f)] private float throwImpulse = 10f;
        [SerializeField, Range(1f, 20f)] private float followSmoothness = 6f;
        [SerializeField, Min(1f)] private float maximumAcceleration = 70f;
        [SerializeField, Min(1f)] private float maximumSpeed = 8f;
        [SerializeField, Min(1f)] private float breakDistance = 4f;
        [SerializeField, Min(0.1f)] private float stuckTimeout = 1.5f;
        private PickupItem held;
        private bool originalGravity;
        private RigidbodyInterpolation originalInterpolation;
        private CollisionDetectionMode originalCollisionMode;
        private float stuckTime;
        private Vector3 previousTarget;
        private readonly List<CollisionPair> ignored = new List<CollisionPair>();

        private struct CollisionPair
        {
            public Collider Item;
            public Collider Player;
        }

        public PickupItem HeldItem => held;
        public bool IsHolding => held != null;
        public event System.Action<bool> Released;
        private float massMultiplier = 1;
        public float CarryResponseMultiplier { get; private set; } = 1;
        public float MaximumMass => maximumMass * massMultiplier;
        public void SetCarryUpgrade(float multiplier)
        {
            massMultiplier = Mathf.Clamp(multiplier, 1, 2);
            CarryResponseMultiplier = Mathf.Lerp(1, 1.2f, Mathf.InverseLerp(1, 1.75f, massMultiplier));
        }
        public void EnsureMaximumMass(float value) => maximumMass = Mathf.Max(maximumMass, value);
        private float TargetDistance => Mathf.Max(0.5f,
            (held != null && held.CarryDistance > 0 ? held.CarryDistance : holdDistance) * carryDistanceMultiplier);
        public Vector3 HoldTarget => view != null
            ? view.position + view.forward * TargetDistance + view.up * carryVerticalOffset
            : transform.position;
        public void SetView(Transform cameraTransform) => view = cameraTransform;

        public bool TryPickUp(PickupItem item)
        {
            if (GetComponent<HuntHappens.Player.PrototypeInput>()?.GameplayBlocked == true ||
                view == null || item == null || !item.CanPickUp(MaximumMass)) return false;
            var inventory = GetComponent<PlayerInventory>();
            if (inventory != null && !inventory.ChangingPhysical && !inventory.PreparePhysicalPickup()) return false;
            if (IsHolding || !item.TryClaim(this, MaximumMass)) return false;
            held = item;
            Rigidbody body = held.Body;
            originalGravity = body.useGravity;
            originalInterpolation = body.interpolation;
            originalCollisionMode = body.collisionDetectionMode;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, maximumSpeed);
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            stuckTime = 0;
            previousTarget = HoldTarget;
            foreach (Collider itemCollider in item.GetComponentsInChildren<Collider>())
            {
                if (itemCollider.attachedRigidbody != body) continue;
                foreach (Collider playerCollider in GetComponentsInChildren<Collider>())
                {
                    if (playerCollider.attachedRigidbody == body || Physics.GetIgnoreCollision(itemCollider, playerCollider)) continue;
                    Physics.IgnoreCollision(itemCollider, playerCollider, true);
                    ignored.Add(new CollisionPair { Item = itemCollider, Player = playerCollider });
                }
            }
            if (inventory != null && !inventory.ChangingPhysical) inventory.RegisterPhysical(item);
            return true;
        }

        private void FixedUpdate()
        {
            RestoreSeparatedCollisions(false);
            if (!IsHolding) return;
            Rigidbody body = held.Body;
            if (view == null || body.isKinematic || !held.isActiveAndEnabled ||
                Vector3.Distance(view.position, body.worldCenterOfMass) > breakDistance)
            {
                Drop();
                return;
            }
            Vector3 target = HoldTarget;
            Vector3 targetVelocity = Vector3.ClampMagnitude((target - previousTarget) / Time.fixedDeltaTime, maximumSpeed);
            previousTarget = target;
            Vector3 error = target - body.worldCenterOfMass;
            stuckTime = error.magnitude > 0.8f ? stuckTime + Time.fixedDeltaTime : 0f;
            if (stuckTime >= stuckTimeout) { Drop(); return; }

            // A damped spring: finite acceleration and speed, no teleporting through colliders.
            float response = followSmoothness * held.FollowMultiplier;
            response *= Mathf.Lerp(1, CarryResponseMultiplier, Mathf.InverseLerp(10, 45, body.mass));
            Vector3 acceleration = error * (response * response)
                + (targetVelocity - body.linearVelocity) * (2f * response);
            body.AddForce(Vector3.ClampMagnitude(acceleration, maximumAcceleration), ForceMode.Acceleration);
            body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, maximumSpeed);
            body.angularVelocity *= Mathf.Exp(-6f * Time.fixedDeltaTime);
        }

        public void Drop() => Release(false);
        public void Throw() => Release(true);

        private void Release(bool throwing)
        {
            if (!IsHolding) return;
            PickupItem item = held;
            Rigidbody body = item.Body;
            held = null;
            item.ReleaseClaim(this);
            body.useGravity = originalGravity;
            body.interpolation = originalInterpolation;
            body.collisionDetectionMode = originalCollisionMode;
            if (!body.isKinematic)
            {
                // Do not add a throw impulse on top of accumulated spring velocity.
                body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, 2f);
                if (throwing && view != null)
                {
                    // A newly grabbed item may still be travelling toward the hands.
                    // Cancel that inward component before applying the forward throw.
                    float inward = Vector3.Dot(body.linearVelocity, view.forward);
                    if (inward < 0) body.linearVelocity -= view.forward * inward;
                    body.AddForce(view.forward * (throwImpulse * item.ThrowMultiplier), ForceMode.Impulse);
                }
                body.WakeUp();
            }
            // Player collision returns only after separation, avoiding a launch when dropping nearby.
            RestoreSeparatedCollisions(false);
            Released?.Invoke(throwing);
        }

        private void RestoreSeparatedCollisions(bool force)
        {
            for (int i = ignored.Count - 1; i >= 0; i--)
            {
                CollisionPair pair = ignored[i];
                if (pair.Item != null && pair.Player != null)
                {
                    if (!force && IsHolding && pair.Item.attachedRigidbody == held.Body) continue;
                    if (!force && pair.Item.bounds.Intersects(pair.Player.bounds)) continue;
                    Physics.IgnoreCollision(pair.Item, pair.Player, false);
                }
                ignored.RemoveAt(i);
            }
        }

        private void OnDisable()
        {
            Drop();
            RestoreSeparatedCollisions(true);
        }
    }
}
