using HuntHappens.Animals;
using UnityEngine;

namespace HuntHappens.Weapons
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class TranquilizerDart : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private float gravityScale = 0.2f;
        [SerializeField, Min(0.1f)] private float stuckLifetime = 3f;
        [SerializeField] private Material impactMaterial;
        private Rigidbody body;
        private Transform owner;
        private float strength;
        private bool impacted;
        private Vector3 previousPosition;
        public bool HasImpacted => impacted;

        private void Awake() => body = GetComponent<Rigidbody>();

        public void Launch(Transform shooter, Vector3 velocity, float dose, float lifetime)
        {
            owner = shooter;
            strength = dose;
            previousPosition = transform.position;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            foreach (Collider own in GetComponentsInChildren<Collider>())
                foreach (Collider player in shooter.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(own, player);
            Destroy(gameObject, Mathf.Max(0.1f, lifetime));
        }

        public void SetImpactMaterial(Material value) => impactMaterial = value;

        private void FixedUpdate()
        {
            if (impacted || owner == null) return;
            Vector3 delta = body.position - previousPosition;
            if (delta.sqrMagnitude > 0.000001f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(previousPosition, 0.035f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit hit in hits)
                {
                    if (Ignore(hit.collider)) continue;
                    Impact(hit.collider, hit.point);
                    break;
                }
            }
            previousPosition = body.position;
            if (!impacted)
            {
                body.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
                if (body.linearVelocity.sqrMagnitude > 0.1f) body.MoveRotation(Quaternion.LookRotation(body.linearVelocity));
            }
        }

        private bool Ignore(Collider collider) => collider.transform.IsChildOf(transform) ||
            (owner != null && collider.transform.IsChildOf(owner)) || collider.GetComponentInParent<TranquilizerDart>() != null;

        private void OnCollisionEnter(Collision collision)
        {
            if (!impacted && !Ignore(collision.collider))
                Impact(collision.collider, collision.contactCount > 0 ? collision.GetContact(0).point : body.position);
        }

        private void Impact(Collider collider, Vector3 point)
        {
            if (impacted) return;
            impacted = true; // Set before applying the effect: collision and sweep can report the same hit.
            DeerSedation deer = collider.GetComponentInParent<DeerSedation>();
            if (deer != null) deer.ApplyDose(strength);
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.detectCollisions = false;
            foreach (Collider own in GetComponentsInChildren<Collider>()) own.enabled = false;
            body.position = point;
            transform.SetParent(collider.transform, true);
            var effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effect.name = "Dart Impact";
            effect.transform.position = point;
            effect.transform.localScale = Vector3.one * 0.17f;
            Collider effectCollider = effect.GetComponent<Collider>();
            effectCollider.enabled = false;
            Destroy(effectCollider);
            if (impactMaterial != null) effect.GetComponent<Renderer>().sharedMaterial = impactMaterial;
            Destroy(effect, 0.2f);
            Destroy(gameObject, stuckLifetime);
        }
    }
}
