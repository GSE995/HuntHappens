using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Characters
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class HeroWander : MonoBehaviour
    {
        private NavMeshAgent agent;
        private Animator animator;
        private NavMeshPath path;
        private Vector3 home;
        private Vector3 progress;
        private float waitUntil;
        private float progressAt;

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            path = new NavMeshPath();
            home = progress = transform.position;
            Pause();
        }

        private void Update()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            animator.speed = Mathf.Clamp(agent.velocity.magnitude / 1.6f, 0, 1.5f);
            if (Time.time < waitUntil || agent.pathPending) return;
            if (agent.hasPath)
            {
                if (agent.remainingDistance <= agent.stoppingDistance + 0.15f) Pause();
                else if (Time.time - progressAt > 2f)
                {
                    if (Vector3.Distance(progress, transform.position) < 0.3f) Pause();
                    progress = transform.position;
                    progressAt = Time.time;
                }
                return;
            }
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            for (int i = 0; i < 16; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 9f;
                Vector3 candidate = transform.position + new Vector3(offset.x, 0, offset.y);
                candidate = Vector3.MoveTowards(home, candidate, 24f);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, filter) ||
                    Vector3.Distance(hit.position, transform.position) < 2f ||
                    !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                agent.SetPath(path);
                progress = transform.position;
                progressAt = Time.time;
                return;
            }
            waitUntil = Time.time + 1f;
        }

        private void Pause()
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            waitUntil = Time.time + Random.Range(2f, 5f);
        }
    }
}
