using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Animals
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(DeerPerception))]
    public sealed class DeerAI : MonoBehaviour
    {
        public enum DeerState { Idle, Wander, Alert, Flee }
        [Header("State (runtime)")]
        [SerializeField] private DeerState currentState;
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.6f;
        [SerializeField, Min(0.1f)] private float runSpeed = 6.5f;
        [SerializeField, Min(1)] private float wanderRadius = 9f;
        [SerializeField, Min(1)] private float roamRadius = 24f;
        [SerializeField] private Vector2 idleDuration = new Vector2(2f, 5f);
        [SerializeField, Min(1)] private float fleeStep = 13f;
        [SerializeField, Min(0.1f)] private float repathInterval = 0.8f;
        [Header("Calming / hysteresis")]
        [SerializeField, Min(0.1f)] private float alertPause = 1.2f;
        [SerializeField, Min(0.1f)] private float alertMemory = 2f;
        [SerializeField, Min(1)] private float safeDistance = 18f;
        [SerializeField, Min(0.1f)] private float calmDuration = 3f;
        [SerializeField, Min(0.1f)] private float minimumFleeDuration = 2.5f;
        [Header("Debug")]
        [SerializeField] private bool showDebug = true;
        private NavMeshAgent agent;
        private DeerPerception perception;
        private NavMeshPath path;
        private Vector3 home;
        private Vector3 lastThreat;
        private Vector3 fleeDirection;
        private Vector3 movementTarget;
        private Vector3 progressPosition;
        private float stateTime;
        private float idleUntil;
        private float unseenTime;
        private float calmTime;
        private float nextRepath;
        private float progressTime;
        private bool warned;
        public DeerState State => currentState;
        public Vector3 MovementTarget => movementTarget;
        public Vector3 FleeDirection => fleeDirection;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            path = new NavMeshPath();
            perception = GetComponent<DeerPerception>();
            home = transform.position;
            progressPosition = home;
        }

        private void Start()
        {
            if (!agent.isOnNavMesh)
            {
                Debug.LogError("Deer is not on a NavMesh. Exit Play Mode and run Tools > Hunt Happens > Add Deer.", this);
                warned = true;
                return;
            }
            Enter(DeerState.Idle);
        }

        private void Update()
        {
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                if (!warned) Debug.LogWarning("Deer navigation unavailable; check its NavMesh.", this);
                warned = true;
                return;
            }
            stateTime += Time.deltaTime;
            perception.Observe(Time.deltaTime);
            unseenTime = perception.HasSight ? 0 : unseenTime + Time.deltaTime;
            if (perception.Noticed) lastThreat = perception.Player.position;

            if (currentState != DeerState.Flee && perception.Frightened) Enter(DeerState.Flee);
            else if ((currentState == DeerState.Idle || currentState == DeerState.Wander) && perception.Noticed)
                Enter(DeerState.Alert);

            switch (currentState)
            {
                case DeerState.Idle:
                    if (stateTime >= idleUntil)
                    {
                        if (ChooseWander()) Enter(DeerState.Wander);
                        else idleUntil = stateTime + 1f;
                    }
                    break;
                case DeerState.Wander:
                    if (Arrived() || IsStuck()) Enter(DeerState.Idle);
                    break;
                case DeerState.Alert:
                    Vector3 look = lastThreat - transform.position;
                    look.y = 0;
                    if (look.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), agent.angularSpeed * Time.deltaTime);
                    if (stateTime >= alertPause && unseenTime >= alertMemory) Enter(DeerState.Idle);
                    break;
                case DeerState.Flee:
                    bool safe = perception.Distance >= Mathf.Max(safeDistance, perception.DetectionRadius + 2f);
                    calmTime = safe ? calmTime + Time.deltaTime : 0;
                    if (calmTime >= calmDuration && stateTime >= minimumFleeDuration) { Enter(DeerState.Idle); break; }
                    if (Time.time >= nextRepath)
                    {
                        ChooseFlee();
                        nextRepath = Time.time + repathInterval;
                    }
                    break;
            }
        }

        private void Enter(DeerState state)
        {
            currentState = state;
            stateTime = calmTime = 0;
            progressTime = Time.time;
            progressPosition = transform.position;
            agent.isStopped = state == DeerState.Idle || state == DeerState.Alert;
            agent.updateRotation = state != DeerState.Alert;
            agent.speed = state == DeerState.Flee ? runSpeed : walkSpeed;
            if (agent.isStopped) agent.ResetPath();
            if (state == DeerState.Idle) idleUntil = Random.Range(idleDuration.x, idleDuration.y);
            if (state == DeerState.Flee)
            {
                ChooseFlee();
                nextRepath = Time.time + repathInterval;
            }
        }

        private bool ChooseWander()
        {
            for (int i = 0; i < 16; i++)
            {
                Vector2 circle = Random.insideUnitCircle * wanderRadius;
                Vector3 candidate = transform.position + new Vector3(circle.x, 0, circle.y);
                if (Vector3.Distance(candidate, home) > roamRadius) candidate = Vector3.MoveTowards(home, candidate, roamRadius);
                if (TryPath(candidate, out Vector3 point) && Vector3.Distance(point, transform.position) >= 2f)
                {
                    movementTarget = point;
                    return agent.SetPath(path);
                }
            }
            return false;
        }

        private void ChooseFlee()
        {
            Vector3 away = transform.position - lastThreat;
            away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();
            fleeDirection = away;
            float bestScore = float.NegativeInfinity;
            Vector3 best = transform.position;
            // Fan search permits a sideways escape near the perimeter or behind obstacles.
            for (int i = 0; i < 15; i++)
            {
                float angle = i == 0 ? 0 : (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 22f;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * away;
                Vector3 candidate = transform.position + direction * (i < 9 ? fleeStep : fleeStep * 0.55f);
                if (!TryPath(candidate, out Vector3 point) || Vector3.Distance(point, transform.position) < 2f) continue;
                float score = Vector3.Distance(point, lastThreat) + Vector3.Dot(direction, away) * 3f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = point;
            }
            if (bestScore > float.NegativeInfinity && TryPath(best, out movementTarget))
            {
                agent.SetPath(path);
                progressTime = Time.time;
                progressPosition = transform.position;
            }
            // No valid path: stay in Flee and retry later; never warp through obstacles.
        }

        private bool TryPath(Vector3 candidate, out Vector3 point)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, filter) &&
                agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
            { point = hit.position; return true; }
            point = default;
            return false;
        }

        private bool Arrived() => !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.15f);

        private bool IsStuck()
        {
            if (Time.time - progressTime < 2f) return false;
            bool stuck = Vector3.Distance(progressPosition, transform.position) < 0.3f;
            progressPosition = transform.position;
            progressTime = Time.time;
            return stuck;
        }

        private void OnDisable()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        }

        private void OnValidate()
        {
            idleDuration.x = Mathf.Max(0.1f, idleDuration.x);
            idleDuration.y = Mathf.Max(idleDuration.x, idleDuration.y);
            roamRadius = Mathf.Max(wanderRadius, roamRadius);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebug) return;
            var senses = GetComponent<DeerPerception>();
            if (senses != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, senses.DetectionRadius);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, senses.FrightRadius);
            }
            if (!Application.isPlaying) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(movementTarget, 0.3f);
            Gizmos.DrawLine(transform.position, movementTarget);
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position + Vector3.up, fleeDirection * 4f);
        }
    }
}
