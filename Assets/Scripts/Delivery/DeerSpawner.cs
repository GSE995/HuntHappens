using HuntHappens.Animals;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Delivery
{
    [DisallowMultipleComponent]
    public sealed class DeerSpawner : MonoBehaviour
    {
        [SerializeField] private DeerSedation deerPrefab;
        [SerializeField] private Transform player;
        [SerializeField] private Transform animalRoot;
        [SerializeField, Min(1)] private int maximumPopulation = 3;
        [SerializeField] private Vector3 spawnCenter;
        [SerializeField, Min(1)] private float spawnRadius = 38f;
        [SerializeField, Min(1)] private float minimumPlayerDistance = 20f;
        [SerializeField, Min(1)] private float minimumAnimalSpacing = 6f;
        [SerializeField, Min(0.1f)] private float spawnDelay = 3f;
        [SerializeField, Min(0.1f)] private float retryInterval = 2f;
        [SerializeField, Range(1, 100)] private int attemptsPerRetry = 32;
        private int pending;
        private float nextAttempt;
        public int PendingReplacements => pending;
        public int MaximumPopulation => maximumPopulation;
        public float MinimumPlayerDistance => minimumPlayerDistance;
        public int ActivePopulation
        {
            get
            {
                int count = 0;
                if (animalRoot != null)
                    foreach (DeerSedation deer in animalRoot.GetComponentsInChildren<DeerSedation>())
                        if (deer.isActiveAndEnabled && !deer.IsDelivered) count++;
                return count;
            }
        }
        public void Configure(DeerSedation prefab, Transform target, Transform parent)
        { deerPrefab = prefab; player = target; animalRoot = parent; }

        public void RequestReplacement()
        {
            if (pending == 0) nextAttempt = Time.time + spawnDelay;
            pending = Mathf.Min(pending + 1, maximumPopulation);
        }

        private void Update()
        {
            if (pending == 0 || Time.time < nextAttempt || deerPrefab == null || player == null || animalRoot == null) return;
            nextAttempt = Time.time + retryInterval;
            if (ActivePopulation >= maximumPopulation) return;
            if (!TryFindSpawn(out Vector3 position)) return; // Keep the request and retry, never spawn at an invalid fallback.
            DeerSedation deer = Instantiate(deerPrefab, position, Quaternion.Euler(0, Random.Range(0, 360), 0), animalRoot);
            deer.name = "Deer Replacement";
            deer.GetComponent<DeerPerception>().SetPlayer(player);
            pending--;
            nextAttempt = Time.time + spawnDelay;
        }

        private bool TryFindSpawn(out Vector3 point)
        {
            NavMeshAgent template = deerPrefab.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = template.agentTypeID, areaMask = template.areaMask };
            DeerSedation[] existing = animalRoot.GetComponentsInChildren<DeerSedation>();
            for (int attempt = 0; attempt < attemptsPerRetry; attempt++)
            {
                Vector2 circle = Random.insideUnitCircle * spawnRadius;
                Vector3 candidate = spawnCenter + new Vector3(circle.x, 0, circle.y);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, filter) ||
                    Vector3.Distance(hit.position, player.position) < minimumPlayerDistance) continue;
                bool nearAnimal = false;
                foreach (DeerSedation deer in existing)
                    if (!deer.IsDelivered && Vector3.Distance(hit.position, deer.transform.position) < minimumAnimalSpacing) nearAnimal = true;
                if (nearAnimal) continue;
                if (Physics.OverlapCapsule(hit.position + Vector3.up * (template.radius + 0.1f),
                        hit.position + Vector3.up * (template.height - template.radius), template.radius, ~0,
                        QueryTriggerInteraction.Ignore).Length != 0) continue;
                point = hit.position;
                return true;
            }
            point = default;
            return false;
        }
    }
}
