using HuntHappens.Interaction;
using HuntHappens.Player;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Animals
{
    [DisallowMultipleComponent]
    public sealed class DeerLeader : MonoBehaviour
    {
        public enum LeaderState { Calm, Warning, Charge, Impact, Recovery, MovingToBait, Eating, Returning }
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
        [Header("Chocolate")]
        [Min(1)] public float chocolateRadius = 15;
        [Min(.1f)] public float eatingDuration = 8;
        [Min(1)] public float baitApproachTimeout = 15;
        [SerializeField] private LeaderState state;
        public LeaderState State => state;
        public bool LowerHead => state == LeaderState.Warning || state == LeaderState.Charge || state == LeaderState.Eating;
        public string Status => state == LeaderState.Warning ? "Вожак: берегись! Сейчас рывок!" : state == LeaderState.Charge ? "Вожак атакует — в сторону!" :
            state == LeaderState.Eating ? "Вожак ест шоколад — стадо без охраны" : state == LeaderState.MovingToBait ? "Вожак идёт к шоколаду" :
            state == LeaderState.Recovery ? "Вожак восстанавливается" : state == LeaderState.Returning ? "Вожак возвращается к стаду" : "Вожак охраняет стадо";
        private NavMeshAgent agent;
        private DeerSedation sedation;
        private PlayerHealth player;
        private Transform home;
        private Vector3 origin, chargeDirection, previousPosition;
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
            if (sedation.IsDelivered || sedation.State != DeerSedation.SedationState.Awake || state == LeaderState.Charge || state == LeaderState.Impact) return;
            bool self = victim == sedation;
            if (!self && (state == LeaderState.Eating || state == LeaderState.MovingToBait || Vector3.Distance(victim.transform.position, transform.position) > protectionRadius || !Visible(victim.transform, victim.transform.position + Vector3.up))) return;
            if (player == null) return;
            if (!PlayerInTerritory)
            { if (self) { bait = null; Enter(LeaderState.Returning); } return; }
            angryUntil = Time.time + warningDuration + recoveryDuration + 2;
            bait = null;
            if (state != LeaderState.Warning && state != LeaderState.Recovery) Enter(LeaderState.Warning);
        }
        private void Update()
        {
            if (sedation.IsDelivered || sedation.State != DeerSedation.SedationState.Awake)
            {
                bait = null;
                if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
                state = LeaderState.Recovery; elapsed = 0; return;
            }
            if (player == null || !agent.enabled || !agent.isOnNavMesh) return;
            elapsed += Time.deltaTime;
            bool playerInTerritory = PlayerInTerritory;
            // Combat ends when the player leaves the territory, not when knockback or a missed charge
            // separates the two animals. Height from knockback does not count as leaving the zone.
            if (!playerInTerritory && (state == LeaderState.Warning || state == LeaderState.Charge))
            { bait = null; Enter(LeaderState.Returning); }
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
                    if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 240 * Time.deltaTime);
                    if (elapsed >= warningDuration)
                    {
                        chargeDirection = transform.forward; previousPosition = transform.position; hit = false; Enter(LeaderState.Charge);
                    }
                    break;
                case LeaderState.Charge:
                    Vector3 before = transform.position;
                    agent.Move(chargeDirection * chargeSpeed * Time.deltaTime);
                    CheckImpact(previousPosition, transform.position); previousPosition = transform.position;
                    if (state == LeaderState.Charge && (elapsed >= chargeDuration || (elapsed > .15f && (transform.position - before).sqrMagnitude < .0001f))) Enter(LeaderState.Recovery);
                    break;
                case LeaderState.Impact: if (elapsed >= .12f) Enter(LeaderState.Recovery); break;
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
                if (player.TakeDamage(player.Maximum / 2) && !lethal)
                    player.GetComponent<FirstPersonMotor>().ApplyImpulse(chargeDirection * knockbackSpeed + Vector3.up * upwardSpeed);
                Enter(LeaderState.Impact); break;
            }
        }
        private void Enter(LeaderState value)
        {
            state = value; elapsed = 0;
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
