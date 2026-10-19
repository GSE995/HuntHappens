using HuntHappens.Player;
using UnityEngine;

namespace HuntHappens.Animals
{
    [DisallowMultipleComponent]
    public sealed class DeerPerception : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField, Min(0.5f)] private float detectionRadius = 12f;
        [SerializeField, Min(0.5f)] private float frightRadius = 6f;
        [SerializeField, Range(30, 360)] private float fieldOfView = 240f;
        [SerializeField] private bool requireLineOfSight = true;
        [SerializeField] private LayerMask sightLayers = ~0;
        [SerializeField] private Vector3 eyeOffset = new Vector3(0, 2f, 0.65f);
        [SerializeField, Min(0)] private float reactionDelay = 0.4f;
        private float visibleTime;
        public Transform Player => player;
        public float DetectionRadius => detectionRadius;
        public float FrightRadius => frightRadius;
        public float Distance { get; private set; } = float.PositiveInfinity;
        public bool HasSight { get; private set; }
        public bool Noticed => HasSight && visibleTime >= reactionDelay;
        public bool Frightened => Noticed && Distance <= frightRadius;

        public void SetPlayer(Transform value) { player = value; visibleTime = 0; }

        private void Start()
        {
            if (player == null)
            {
                var motor = FindAnyObjectByType<FirstPersonMotor>();
                if (motor != null) player = motor.transform;
            }
        }

        public void Observe(float deltaTime)
        {
            HasSight = false;
            Distance = player != null ? Vector3.Distance(transform.position, player.position) : float.PositiveInfinity;
            if (player != null && Distance <= detectionRadius)
            {
                Vector3 direction = player.position - transform.position;
                direction.y = 0;
                // Nearby danger can be noticed behind the animal, but still requires line of sight.
                bool inView = Distance <= frightRadius || Vector3.Angle(transform.forward, direction) <= fieldOfView * 0.5f;
                HasSight = inView && (!requireLineOfSight || ClearSight());
            }
            visibleTime = HasSight ? visibleTime + Mathf.Max(0, deltaTime) : 0;
        }

        private bool ClearSight()
        {
            Vector3 eye = transform.TransformPoint(eyeOffset);
            Vector3 target = player.position + Vector3.up * 1.1f;
            Vector3 direction = target - eye;
            foreach (RaycastHit hit in Physics.RaycastAll(eye, direction.normalized, direction.magnitude,
                         sightLayers, QueryTriggerInteraction.Ignore))
            {
                Transform obstacle = hit.collider.transform;
                if (obstacle.IsChildOf(transform) || obstacle.IsChildOf(player)) continue;
                return false;
            }
            return true;
        }

        private void OnValidate()
        {
            detectionRadius = Mathf.Max(0.5f, detectionRadius);
            frightRadius = Mathf.Clamp(frightRadius, 0.5f, detectionRadius);
        }
    }
}
