using HuntHappens.Interaction;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Animals
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent), typeof(PickupItem))]
    public sealed class DeerPhysics : MonoBehaviour
    {
        [SerializeField] private CapsuleCollider standingCollider;
        [SerializeField] private BoxCollider sleepingCollider;
        [SerializeField, Min(1)] private float sleepingMass = 120f;
        [SerializeField, Min(0.2f)] private float recoverySearchRadius = 2f;
        private Rigidbody body;
        private NavMeshAgent agent;
        private PickupItem pickup;
        private Vector3 recoveryStart;
        private Vector3 recoveryTarget;
        private Quaternion rotationStart;
        private Quaternion rotationTarget;
        private bool recovering;
        public bool IsHeld => pickup != null && pickup.Holder != null;
        public Rigidbody Body => body;

        public void Configure(CapsuleCollider upright, BoxCollider asleep)
        { standingCollider = upright; sleepingCollider = asleep; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            agent = GetComponent<NavMeshAgent>();
            pickup = GetComponent<PickupItem>();
            pickup.enabled = false;
        }

        public void FallAsleep()
        {
            recovering = false;
            if (agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
            agent.enabled = false;
            standingCollider.enabled = false;
            sleepingCollider.enabled = true;
            body.mass = sleepingMass;
            body.linearDamping = 0.7f;
            body.angularDamping = 2f;
            body.maxAngularVelocity = 4f;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.useGravity = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            pickup.enabled = true;
            body.WakeUp();
        }

        public bool TryBeginRecovery()
        {
            // AddForce(Impulse) is applied at the next physics step. Do not wake in the same frame as a throw.
            if (GetComponent<DeerSedation>().Cart != null || IsHeld || Time.time - pickup.LastReleasedTime < 1f ||
                body.linearVelocity.magnitude > 1.2f || body.angularVelocity.magnitude > 1.5f) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, recoverySearchRadius, filter)) return false;
            // Never stand up inside a tree, a carried crate, the player or another animal.
            if (!HasStandingRoom(hit.position)) return false;
            // A short recovery correction must not cross a solid obstacle to reach the NavMesh.
            Vector3 from = body.worldCenterOfMass;
            Vector3 to = hit.position + Vector3.up * 0.6f;
            foreach (RaycastHit obstacle in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
                if (!obstacle.collider.transform.IsChildOf(transform)) return false;
            pickup.enabled = false;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.useGravity = false;
            sleepingCollider.enabled = false;
            recoveryStart = body.position;
            recoveryTarget = hit.position;
            rotationStart = body.rotation;
            rotationTarget = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            recovering = true;
            return true;
        }

        public void UpdateRecovery(float progress)
        {
            if (!recovering) return;
            body.MovePosition(Vector3.Lerp(recoveryStart, recoveryTarget, Mathf.SmoothStep(0, 1, progress)));
            body.MoveRotation(Quaternion.Slerp(rotationStart, rotationTarget, Mathf.SmoothStep(0, 1, progress)));
        }

        public bool FinishRecovery()
        {
            if (!recovering) return false;
            if (!HasStandingRoom(recoveryTarget)) { FallAsleep(); return false; }
            // The last fixed step has already placed the kinematic body on this point.
            standingCollider.enabled = true;
            agent.enabled = true;
            if (!agent.isOnNavMesh)
            {
                agent.enabled = false;
                FallAsleep();
                return false;
            }
            agent.isStopped = false;
            recovering = false;
            return true;
        }

        private bool HasStandingRoom(Vector3 point)
        {
            // Match the actual standing capsule, including the animal's root scale.
            Vector3 scale = transform.lossyScale;
            float radius = standingCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfSegment = Mathf.Max(0, standingCollider.height * Mathf.Abs(scale.y) * .5f - radius);
            Vector3 center = point + Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.Scale(standingCollider.center, scale);
            // Leave a small clearance from the floor so ground contact does not block recovery.
            radius = Mathf.Max(.01f, radius - .01f);
            foreach (Collider obstacle in Physics.OverlapCapsule(center - Vector3.up * halfSegment,
                         center + Vector3.up * halfSegment, radius, ~0, QueryTriggerInteraction.Ignore))
                if (!obstacle.transform.IsChildOf(transform)) return false;
            return true;
        }
    }
}
