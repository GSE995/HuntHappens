using UnityEngine;
using UnityEngine.AI;
namespace HuntHappens.SmallGame
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class RatAI : MonoBehaviour
    {
        public Transform visual;
        public bool isGrey;
        public bool isAggressive;
        [Min(0)] public float territoryRadius;
        private Vector3 normalVisualScale;
        private Quaternion normalVisualRotation;
        private float fallAsleepAt, wakeAt, sleepPose;
        public bool IsSedated { get; private set; }
        public bool CanPickUp => isActiveAndEnabled && !Caught && IsSedated && Time.time >= fallAsleepAt && Time.time < wakeAt;
        public float RemainingSleep => IsSedated ? Mathf.Max(0, wakeAt - Time.time) : 0;
        public int Kind => isAggressive ? 2 : isGrey ? 1 : 0;
        public Vector3 Home => home;
        private float escapeUntil;
        public string PickupPrompt => (isAggressive ? "Крупная крыса — $25" : isGrey ? "Серая крыса — $15" : "Чёрная крыса — $10") + "\nE — подобрать спящую крысу";
        private float nextBite;
        private HuntHappens.Player.PlayerHealth health;
        public int SalePrice => isAggressive ? 25 : isGrey ? 15 : 10;
        private NavMeshAgent agent;
        private RatHunting player;
        private Vector3 home;
        private float nextDecision;
        private float respawnAt;
        private RatGatheringSpot[] gatheringSpots;
        private float restUntil;
        public bool Caught { get; private set; }
        private void Start() { agent = GetComponent<NavMeshAgent>(); home = transform.position; normalVisualScale = visual.localScale; normalVisualRotation = visual.localRotation; EnsureHitCollider(); player = FindAnyObjectByType<RatHunting>(); gatheringSpots = FindObjectsByType<RatGatheringSpot>(FindObjectsSortMode.None); if (player != null) health = player.GetComponent<HuntHappens.Player.PlayerHealth>(); }
        private void EnsureHitCollider()
        {
            if (GetComponentInChildren<Collider>() != null) return;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = new Bounds();
            bool initialized = false;
            foreach (var renderer in renderers)
            {
                Bounds mesh = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = mesh.center + Vector3.Scale(mesh.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    point = visual.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(point);
                }
            }
            var hitbox = visual.gameObject.AddComponent<BoxCollider>();
            hitbox.center = bounds.center;
            hitbox.size = bounds.size;
        }
        public bool ApplyDose(float strength, float durationMultiplier = 1)
        {
            if (!isActiveAndEnabled || Caught || strength <= 0 || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            if (IsSedated)
            {
                wakeAt = Mathf.Max(wakeAt, Time.time + 25f);
                return true;
            }
            IsSedated = true;
            fallAsleepAt = Time.time + Mathf.Max(0.5f, Mathf.Clamp(durationMultiplier, 0.25f, 1f));
            wakeAt = fallAsleepAt + 25f;
            restUntil = 0;
            agent.ResetPath();
            agent.isStopped = true;
            return true;
        }
        private void UpdateSedation()
        {
            float target = Time.time < wakeAt ? Mathf.InverseLerp(fallAsleepAt - 0.5f, fallAsleepAt, Time.time) : 0;
            sleepPose = Mathf.MoveTowards(sleepPose, target, Time.deltaTime * 2f);
            visual.localRotation = normalVisualRotation * Quaternion.Euler(0, 0, 80f * Mathf.SmoothStep(0, 1, sleepPose));
            if (Time.time < wakeAt || sleepPose > 0) return;
            IsSedated = false;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
            nextBite = Time.time + 1.5f;
            nextDecision = 0;
        }
        public void Capture(RatCage cage)
        {
            IsSedated = false; sleepPose = 0; visual.localRotation = normalVisualRotation;
            Caught = true; agent.enabled = false;
            // Fold the larger animal into the existing cage without clipping through its bars.
            if (isAggressive) visual.localScale = normalVisualScale / 1.4f;
            transform.SetPositionAndRotation(cage.transform.position + new Vector3(0, 0.04f, 0), cage.transform.rotation);
        }
        public void ReleaseForRespawn() { visual.gameObject.SetActive(false); respawnAt = Time.time + 20; }
        public bool TryCollectSleeping()
        {
            if (!CanPickUp) return false;
            HoldForCarrier();
            return true;
        }
        public void HoldForCarrier()
        {
            Caught = true;
            IsSedated = false;
            sleepPose = 0;
            respawnAt = 0;
            visual.localRotation = normalVisualRotation;
            agent.enabled = false;
            visual.gameObject.SetActive(false);
        }
        public bool TryEscapeFromCarrier(Transform carrier)
        {
            if (carrier == null || !Caught) return false;
            if (!NavMesh.SamplePosition(carrier.position + carrier.right * 1.5f, out var spawn, 2f, agent.areaMask)) return false;
            Vector3 previous = transform.position;
            transform.position = spawn.position;
            agent.enabled = true;
            if (!agent.isOnNavMesh || !agent.Warp(spawn.position))
            { agent.enabled = false; transform.position = previous; return false; }
            agent.isStopped = false;
            visual.localScale = normalVisualScale;
            visual.localRotation = normalVisualRotation;
            visual.gameObject.SetActive(true);
            Caught = false;
            IsSedated = false;
            respawnAt = 0;
            escapeUntil = Time.time + 5f;
            nextDecision = 0;
            nextBite = escapeUntil + 1.5f;
            return true;
        }
        private void Update()
        {
            if (Caught)
            {
                if (respawnAt > 0 && Time.time >= respawnAt && NavMesh.SamplePosition(home, out var spawn, 3, NavMesh.AllAreas))
                { transform.position = spawn.position; agent.enabled = true; agent.isStopped = false; visual.localScale = normalVisualScale; nextBite = Time.time + 1.5f; Caught = false; respawnAt = 0; visual.gameObject.SetActive(true); }
                return;
            }
            if (IsSedated) { UpdateSedation(); return; }
            if (!agent.isOnNavMesh || player == null) return;
            // NavMesh can sit slightly above the ground. Keep the small body on the actual surface.
            if (player != null && player.ground != null && player.ground.Raycast(new Ray(transform.position + Vector3.up, Vector3.down), out var floor, 2f))
                visual.position = new Vector3(transform.position.x, floor.point.y, transform.position.z);
            float distance = Vector3.Distance(transform.position, player.transform.position);
            // Guards check foot contact every frame instead of waiting for the wander tick.
            if (!isAggressive && Time.time < nextDecision) return;
            nextDecision = Time.time + 0.4f;
            if (Time.time < escapeUntil)
            {
                Vector3 away = transform.position - player.transform.position; away.y = 0;
                if (away.sqrMagnitude < 0.01f) away = transform.forward;
                Move(transform.position + away.normalized * 6f, 6f);
                return;
            }
            if (!isAggressive && distance < 3)
            { restUntil = 0; Move(transform.position + (transform.position - player.transform.position).normalized * 4, 4.5f); return; }
            RatCage closest = null; float best = 14;
            foreach (var cage in FindObjectsByType<RatCage>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(transform.position, cage.transform.position);
                if (cage.Baited && d < best) { best = d; closest = cage; }
            }
            if (closest != null)
            { restUntil = 0; if (!closest.TryCapture(this)) Move(closest.transform.position, 1.2f); return; }
            if (isAggressive && HuntPlayer(distance)) return;
            if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance < 0.3f))
            {
                if (restUntil == 0) { restUntil = Time.time + Random.Range(1f, 3f); return; }
                if (Time.time < restUntil) return;
                restUntil = 0;
                Wander();
            }
        }
        private bool HuntPlayer(float distance)
        {
            bool inTerritory = Vector3.Distance(player.transform.position, home) < 16f;
            if (!inTerritory || distance > 8f || health == null || player.GetComponent<HuntHappens.Player.PrototypeInput>().GameplayBlocked)
            {
                if (Vector3.Distance(transform.position, home) > 2.5f) { Move(home, 1.2f); return true; }
                return false;
            }
            restUntil = 0;
            var controller = player.GetComponent<CharacterController>();
            Vector3 feet = controller != null
                ? player.transform.TransformPoint(controller.center - Vector3.up * (controller.height * 0.5f))
                : player.transform.position;
            Vector3 delta = feet - transform.position; delta.y = 0;
            if (delta.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(delta);
            if (delta.sqrMagnitude > 1.1f * 1.1f || Mathf.Abs(feet.y - transform.position.y) > 0.6f)
            { Move(feet, 6f, 0.95f); return true; }
            agent.ResetPath();
            // First contact bites immediately; repeated bites still respect the cooldown.
            if (Time.time < nextBite) return true;
            nextBite = Time.time + 1.5f;
            Vector3 from = transform.position + Vector3.up * 0.25f;
            Vector3 to = feet + Vector3.up * 0.25f;
            foreach (var hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(player.transform)) return true;
            health.TakeDamage(20);
            return true;
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
                agent.stoppingDistance = 0;
                agent.SetPath(path);
                return;
            }
        }
        private void Move(Vector3 point, float speed, float stoppingDistance = 0)
        {
            if (territoryRadius > 0) point = home + Vector3.ClampMagnitude(point - home, territoryRadius);
            if (NavMesh.SamplePosition(point, out var hit, 2, NavMesh.AllAreas) &&
                (territoryRadius <= 0 || Vector3.Distance(hit.position, home) <= territoryRadius + 0.5f))
            { agent.speed = speed; agent.stoppingDistance = stoppingDistance; agent.SetDestination(hit.position); }
        }
    }
}
