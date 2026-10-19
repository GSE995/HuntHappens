using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Animals
{
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class DeerAnimation : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Transform headPivot;
        [SerializeField] private Transform[] legs = new Transform[4];
        [SerializeField, Min(0)] private float walkSwing = 28f;
        [SerializeField, Min(0)] private float runSwing = 48f;
        [SerializeField, Min(0)] private float blendSpeed = 6f;
        private NavMeshAgent agent;
        private Vector3 restPosition;
        private Quaternion headRest;
        private Quaternion[] legRest;
        private float speed;
        private float phase;
        private float idlePhase;

        public void Configure(Transform model, Transform head, Transform[] legPivots)
        { visual = model; headPivot = head; legs = legPivots; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (visual != null) restPosition = visual.localPosition;
            if (headPivot != null) headRest = headPivot.localRotation;
            legRest = new Quaternion[legs.Length];
            for (int i = 0; i < legs.Length; i++) if (legs[i] != null) legRest[i] = legs[i].localRotation;
            idlePhase = Random.value * 6.28f;
        }

        private void LateUpdate()
        {
            if (visual == null) return;
            speed = Mathf.Lerp(speed, agent.velocity.magnitude, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
            float moving = Mathf.Clamp01(speed / 0.7f);
            float running = Mathf.InverseLerp(2f, 6f, speed);
            phase += speed * Time.deltaTime * Mathf.Lerp(5f, 4f, running);
            float idle = Time.time * 1.7f + idlePhase;
            float bob = Mathf.Sin(idle) * 0.018f * (1f - moving) + Mathf.Abs(Mathf.Sin(phase)) * Mathf.Lerp(0.025f, 0.11f, running) * moving;
            visual.localPosition = restPosition + Vector3.up * bob;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float offset = i == 0 || i == 3 ? 0 : Mathf.PI;
                float swing = Mathf.Sin(phase + offset) * Mathf.Lerp(walkSwing, runSwing, running) * moving;
                legs[i].localRotation = legRest[i] * Quaternion.Euler(swing, 0, 0);
            }
            if (headPivot != null)
                headPivot.localRotation = headRest * Quaternion.Euler(Mathf.Sin(idle * 0.6f) * 3f * (1 - moving),
                    Mathf.Sin(idle * 0.37f) * 14f * (1 - moving), 0);
        }
    }
}
