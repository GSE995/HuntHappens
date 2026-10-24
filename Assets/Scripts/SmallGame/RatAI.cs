using UnityEngine;
using UnityEngine.AI;
namespace HuntHappens.SmallGame
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class RatAI : MonoBehaviour
    {
        public Transform visual;
        public bool isGrey;
        public int SalePrice => isGrey ? 15 : 10;
        private NavMeshAgent agent;
        private RatHunting player;
        private Vector3 home;
        private float nextDecision;
        private float respawnAt;
        private RatGatheringSpot[] gatheringSpots;
        private float restUntil;
        public bool Caught { get; private set; }
        private void Start() { agent = GetComponent<NavMeshAgent>(); home = transform.position; player = FindAnyObjectByType<RatHunting>(); gatheringSpots = FindObjectsByType<RatGatheringSpot>(FindObjectsSortMode.None); }
        public void Capture(RatCage cage)
        {
            Caught = true; agent.enabled = false;
            transform.SetPositionAndRotation(cage.transform.position + new Vector3(0, 0.04f, 0), cage.transform.rotation);
        }
        public void ReleaseForRespawn() { visual.gameObject.SetActive(false); respawnAt = Time.time + 20; }
        private void Update()
        {
            if (Caught)
            {
                if (respawnAt > 0 && Time.time >= respawnAt && NavMesh.SamplePosition(home, out var spawn, 3, NavMesh.AllAreas))
                { transform.position = spawn.position; agent.enabled = true; Caught = false; respawnAt = 0; visual.gameObject.SetActive(true); }
                return;
            }
            if (!agent.isOnNavMesh || player == null) return;
            // NavMesh can sit slightly above the ground. Keep the small body on the actual surface.
            if (player != null && player.ground != null && player.ground.Raycast(new Ray(transform.position + Vector3.up, Vector3.down), out var floor, 2f))
                visual.position = new Vector3(transform.position.x, floor.point.y, transform.position.z);
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (Time.time < nextDecision) return;
            nextDecision = Time.time + 0.4f;
            if (distance < 3)
            { restUntil = 0; Move(transform.position + (transform.position - player.transform.position).normalized * 4, 4.5f); return; }
            RatCage closest = null; float best = 14;
            foreach (var cage in FindObjectsByType<RatCage>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(transform.position, cage.transform.position);
                if (cage.Baited && d < best) { best = d; closest = cage; }
            }
            if (closest != null)
            { restUntil = 0; if (!closest.TryCapture(this)) Move(closest.transform.position, 1.2f); return; }
            if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance < 0.3f))
            {
                if (restUntil == 0) { restUntil = Time.time + Random.Range(1f, 3f); return; }
                if (Time.time < restUntil) return;
                restUntil = 0;
                Wander();
            }
        }
        private void Wander()
        {
            // Keep each rat near its original patch, so the population spreads across bags.
            RatGatheringSpot closest = null;
            float best = float.PositiveInfinity;
            foreach (var spot in gatheringSpots)
            {
                if (spot == null || !spot.isActiveAndEnabled) continue;
                float distance = (spot.transform.position - home).sqrMagnitude;
                if (distance < best) { best = distance; closest = spot; }
            }
            Vector3 center = closest != null ? closest.transform.position : home;
            float radius = closest != null ? closest.radius : 5f;
            var path = new NavMeshPath();
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                if (closest != null && offset.magnitude < 0.65f) continue;
                if (!NavMesh.SamplePosition(center + new Vector3(offset.x, 0, offset.y), out var hit, 0.4f, agent.areaMask)) continue;
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                agent.speed = 0.8f;
                agent.SetPath(path);
                return;
            }
        }
        private void Move(Vector3 point, float speed)
        { if (NavMesh.SamplePosition(point, out var hit, 2, NavMesh.AllAreas)) { agent.speed = speed; agent.SetDestination(hit.position); } }
    }
}
