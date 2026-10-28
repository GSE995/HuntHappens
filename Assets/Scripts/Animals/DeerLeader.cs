using HuntHappens.Interaction;
using HuntHappens.Player;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Animals
{
    [DisallowMultipleComponent]
    public sealed class DeerLeader : MonoBehaviour
    {
        public enum LeaderState { Calm, Warning, Charge, Impact, Recovery, MovingToBait, Eating, Returning, Stomp }
        [Header("Guarding")]
        [Min(1)] public float reactionRadius = 12;
        [Min(1)] public float protectionRadius = 12;
        [Min(2)] public float leashRadius = 24;
        [Header("Attack")]
        [Min(.2f)] public float warningDuration = 1.4f;
        [Min(1)] public float chargeSpeed = 13;
        [Min(.1f)] public float chargeDuration = 1.3f;
        [Min(.1f)] public float recoveryDuration = 3;
        [Min(0)] public float knockbackSpeed = 9;
        [Min(0)] public float upwardSpeed = 3;
        [Header("Stomp wave")]
        [Min(.1f)] public float stompWindup = .65f;
        [Min(.1f)] public float stompRecovery = .25f;
        [Min(1)] public float waveRadius = 8;
        [Min(1)] public float waveSpeed = 10;
        [Min(1)] public int waveDamage = 25;
        private LineRenderer wave;
        private Material waveMaterial;
        private Vector3 waveOrigin;
        private float waveDistance;
        private bool waveHit, stompReleased;
        public float StompLift => state != LeaderState.Stomp ? 0 :
            Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(.01f, stompWindup * .65f)) *
            (1 - Mathf.SmoothStep(0, 1, (elapsed - stompWindup * .65f) / Mathf.Max(.01f, stompWindup * .35f)));
        [Header("Chocolate")]
        [Min(1)] public float chocolateRadius = 15;
        [Min(.1f)] public float eatingDuration = 8;
        [Min(1)] public float baitApproachTimeout = 15;
        [SerializeField] private LeaderState state;
        public LeaderState State => state;
        public float StateElapsed => elapsed;
        public float LastImpactTime { get; private set; } = float.NegativeInfinity;
        public bool LowerHead => state == LeaderState.Warning || state == LeaderState.Charge || state == LeaderState.Eating;
        public string Status => state == LeaderState.Stomp ? "Вожак бьёт копытами — отойди!" : state == LeaderState.Warning ? "Вожак: берегись! Сейчас рывок!" : state == LeaderState.Charge ? "Вожак атакует — в сторону!" :
            state == LeaderState.Eating ? "Вожак ест шоколад — стадо без охраны" : state == LeaderState.MovingToBait ? "Вожак идёт к шоколаду" :
            state == LeaderState.Recovery ? "Вожак восстанавливается" : state == LeaderState.Returning ? "Вожак возвращается к стаду" : "Вожак охраняет стадо";
        private NavMeshAgent agent;
        private DeerSedation sedation;
        private PlayerHealth player;
        private Transform home;
        private Vector3 origin, chargeDirection, chargeStart, previousPosition;
        private const float ChargeOvershoot = 2f;
        private const float AttackTurnSpeed = 720f;
        private float chargeDistance;
        private WorldInventoryItem bait;
        private float elapsed, nextScan, angryUntil;
        private bool hit;
        public void SetHome(Transform value) { home = value; origin = value.position; }
        private Vector3 Home => home != null ? home.position : origin;
        private bool PlayerInTerritory => player != null &&
            Vector3.ProjectOnPlane(player.transform.position - Home, Vector3.up).sqrMagnitude <= leashRadius * leashRadius;
        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>(); sedation = GetComponent<DeerSedation>();
            GetComponent<DeerAI>().enabled = false;
            origin = transform.position;
        }
        private void Start() { player = FindAnyObjectByType<PlayerHealth>(); Enter(LeaderState.Calm); }
        public void NoticeAttack(DeerSedation victim)
        {
            if (sedation.IsDelivered || sedation.State != DeerSedation.SedationState.Awake || state == LeaderState.Charge || state == LeaderState.Impact || state == LeaderState.Stomp) return;
            bool self = victim == sedation;
            if (!self && (state == LeaderState.Eating || state == LeaderState.MovingToBait || Vector3.Distance(victim.transform.position, transform.position) > protectionRadius || !Visible(victim.transform, victim.transform.position + Vector3.up))) return;
            if (player == null) return;
            if (!PlayerInTerritory)
            { if (self) { bait = null; ClearWave(); Enter(LeaderState.Returning); } return; }
            angryUntil = Time.time + warningDuration + recoveryDuration + 2;
            bait = null;
            if (state != LeaderState.Warning && state != LeaderState.Recovery) Enter(LeaderState.Warning);
        }
        private void Update()
        {
            if (sedation.IsDelivered || sedation.State != DeerSedation.SedationState.Awake)
            {
                bait = null; ClearWave();
                if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
                state = LeaderState.Recovery; elapsed = 0; return;
            }
            if (player == null || !agent.enabled || !agent.isOnNavMesh) return;
            UpdateWave();
            elapsed += Time.deltaTime;
            bool playerInTerritory = PlayerInTerritory;
            // Combat ends when the player leaves the territory, not when knockback or a missed charge
            // separates the two animals. Height from knockback does not count as leaving the zone.
            if (!playerInTerritory && (state == LeaderState.Warning || state == LeaderState.Charge || state == LeaderState.Stomp))
            { bait = null; ClearWave(); Enter(LeaderState.Returning); }
            if ((state == LeaderState.Calm || state == LeaderState.Warning) && Time.time >= angryUntil && Time.time >= nextScan)
            {
                nextScan = Time.time + .4f;
                WorldInventoryItem nearest = null; float distance = chocolateRadius;
                foreach (var item in FindObjectsByType<WorldInventoryItem>())
                    if (Accessible(item, out var point) && Vector3.Distance(transform.position, point) < distance)
                    { nearest = item; distance = Vector3.Distance(transform.position, point); }
                if (nearest != null) { bait = nearest; Enter(LeaderState.MovingToBait); }
            }
            switch (state)
            {
                case LeaderState.Calm:
                    if (playerInTerritory && Vector3.Distance(transform.position, player.transform.position) <= reactionRadius && Visible(player.transform, player.transform.position + Vector3.up)) Enter(LeaderState.Warning);
                    break;
                case LeaderState.Warning:
                    Vector3 direction = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
                    if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), AttackTurnSpeed * Time.deltaTime);
                    if (elapsed >= warningDuration)
                    {
                        chargeDirection = transform.forward;
                        chargeStart = previousPosition = transform.position;
                        // Commit to this aim point so a dodge cannot drag the charge across the arena.
                        chargeDistance = Mathf.Max(0, Vector3.Dot(direction, chargeDirection)) + ChargeOvershoot;
                        hit = false; Enter(LeaderState.Charge);
                    }
                    break;
                case LeaderState.Charge:
                    Vector3 before = transform.position;
                    float remaining = Mathf.Max(0, chargeDistance - Vector3.Dot(before - chargeStart, chargeDirection));
                    agent.Move(chargeDirection * Mathf.Min(chargeSpeed * Time.deltaTime, remaining));
                    CheckImpact(previousPosition, transform.position); previousPosition = transform.position;
                    if (state == LeaderState.Charge && (Vector3.Dot(transform.position - chargeStart, chargeDirection) >= chargeDistance - .01f ||
                        elapsed >= chargeDuration || (elapsed > .15f && (transform.position - before).sqrMagnitude < .0001f))) Enter(LeaderState.Stomp);
                    break;
                case LeaderState.Impact: if (elapsed >= .12f) Enter(LeaderState.Stomp); break;
                case LeaderState.Stomp:
                    if (!stompReleased && elapsed >= stompWindup) { stompReleased = true; ReleaseWave(); }
                    if (elapsed >= stompWindup + stompRecovery) Enter(LeaderState.Recovery);
                    break;
                case LeaderState.Recovery:
                    if (elapsed >= recoveryDuration)
                        Enter(playerInTerritory ? LeaderState.Warning : LeaderState.Returning);
                    break;
                case LeaderState.Returning:
                    if (playerInTerritory) { Enter(LeaderState.Warning); break; }
                    MoveTo(Home);
                    if (Vector3.Distance(transform.position, Home) < 2) Enter(LeaderState.Calm);
                    break;
                case LeaderState.MovingToBait:
                    if (elapsed > baitApproachTimeout || !Accessible(bait, out var target)) { bait = null; Enter(LeaderState.Returning); break; }
                    MoveTo(target);
                    if (Vector3.Distance(transform.position, target) < 1.7f) Enter(LeaderState.Eating);
                    break;
                case LeaderState.Eating:
                    if (bait == null || !bait.isActiveAndEnabled || Vector3.Distance(transform.position, bait.transform.position) > 2.5f) { bait = null; Enter(LeaderState.Returning); break; }
                    if (elapsed >= eatingDuration) { bait.ConsumeOne(); bait = null; Enter(LeaderState.Returning); }
                    break;
            }
        }
        private void CheckImpact(Vector3 from, Vector3 to)
        {
            if (hit) return;
            foreach (var collider in Physics.OverlapCapsule(from + Vector3.up, to + Vector3.up, agent.radius + .45f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider.GetComponentInParent<PlayerHealth>() != player || !Visible(player.transform, player.transform.position + Vector3.up)) continue;
                hit = true;
                bool lethal = player.Current <= player.Maximum / 2;
                if (player.TakeDamage(player.Maximum / 2))
                {
                    var holder = player.GetComponent<ItemHolder>();
                    if (holder != null && holder.IsHoldingDeer) holder.Drop();
                    if (!lethal)
                        player.GetComponent<FirstPersonMotor>().ApplyImpulse(chargeDirection * knockbackSpeed + Vector3.up * upwardSpeed);
                }
                Enter(LeaderState.Impact); break;
            }
        }
        private void ReleaseWave()
        {
            ClearWave();
            waveOrigin = transform.position + transform.forward * .8f;
            waveDistance = 0; waveHit = false;
            var ring = new GameObject("Leader hoof shockwave");
            wave = ring.AddComponent<LineRenderer>();
            wave.useWorldSpace = true; wave.loop = true; wave.positionCount = 96;
            wave.widthMultiplier = .22f;
            waveMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            waveMaterial.SetColor("_BaseColor", new Color(1, .65f, .12f));
            wave.sharedMaterial = waveMaterial;
            wave.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wave.receiveShadows = false;
            DrawWave();
        }
        private void UpdateWave()
        {
            if (wave == null || Time.deltaTime <= 0) return;
            if (!PlayerInTerritory) { ClearWave(); return; }
            float previous = waveDistance;
            waveDistance = Mathf.Min(waveRadius, waveDistance + waveSpeed * Time.deltaTime);
            DrawWave();
            var controller = player.GetComponent<CharacterController>();
            Vector3 feet = controller != null ? controller.bounds.min : player.transform.position;
            float distance = Vector3.ProjectOnPlane(player.transform.position - waveOrigin, Vector3.up).magnitude;
            float padding = (controller != null ? controller.radius : .4f) + .11f;
            if (!waveHit && distance >= previous - padding && distance <= waveDistance + padding &&
                Mathf.Abs(feet.y - WaveGround(player.transform.position).y) < .8f &&
                Visible(player.transform, player.transform.position + Vector3.up))
            {
                waveHit = true;
                if (player.TakeDamage(waveDamage))
                {
                    var holder = player.GetComponent<ItemHolder>();
                    if (holder != null && holder.IsHoldingDeer) holder.Drop();
                }
            }
            if (waveDistance >= waveRadius) ClearWave();
        }
        private Vector3 WaveGround(Vector3 point)
        {
            point.y = waveOrigin.y;
            float best = float.PositiveInfinity;
            Vector3 ground = point;
            foreach (var contact in Physics.RaycastAll(point + Vector3.up * 2, Vector3.down, 4, ~0, QueryTriggerInteraction.Ignore))
            {
                if (contact.transform.IsChildOf(transform) || contact.rigidbody != null ||
                    contact.collider is CharacterController || contact.normal.y < .5f || contact.distance >= best) continue;
                best = contact.distance; ground = contact.point;
            }
            return ground;
        }
        private void DrawWave()
        {
            for (int i = 0; i < wave.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / wave.positionCount;
                wave.SetPosition(i, WaveGround(waveOrigin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * waveDistance) + Vector3.up * .12f);
            }
        }
        private void ClearWave()
        {
            if (wave != null) Destroy(wave.gameObject);
            if (waveMaterial != null) Destroy(waveMaterial);
            wave = null; waveMaterial = null;
        }
        private void OnDisable() => ClearWave();
        private void Enter(LeaderState value)
        {
            state = value; elapsed = 0;
            if (value == LeaderState.Stomp) stompReleased = false;
            if (value == LeaderState.Impact) LastImpactTime = Time.time;
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.ResetPath(); agent.isStopped = true; agent.updateRotation = value != LeaderState.Warning && value != LeaderState.Charge;
        }
        private void MoveTo(Vector3 point) { agent.isStopped = false; agent.speed = 2.5f; agent.SetDestination(point); }
        private bool Accessible(WorldInventoryItem item, out Vector3 point)
        {
            point = default;
            if (item == null || !item.isActiveAndEnabled || item.Entry?.kind != InventoryKind.Chocolate || item.Entry.quantity <= 0 ||
                item.GetComponent<Rigidbody>().isKinematic || Vector3.Distance(item.transform.position, transform.position) > chocolateRadius ||
                Vector3.Distance(item.transform.position, Home) > leashRadius || !Visible(item.transform, item.transform.position)) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(item.transform.position, out var nav, .7f, filter)) return false;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(nav.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            point = nav.position; return true;
        }
        private bool Visible(Transform target, Vector3 point)
        {
            Vector3 eye = transform.position + Vector3.up * 1.6f, delta = point - eye;
            foreach (var contact in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!contact.transform.IsChildOf(transform) && !contact.transform.IsChildOf(target)) return false;
            return true;
        }
    }
}
