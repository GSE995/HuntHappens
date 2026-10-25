using System.Collections.Generic;
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
        private DeerSedation sedation;
        private Quaternion restRotation;
        private Vector3[][] hoofPoints;
        private readonly RaycastHit[] groundHits = new RaycastHit[32];
        public Transform[] LegPivots => legs;

        public void Configure(Transform model, Transform head, Transform[] legPivots)
        { visual = model; headPivot = head; legs = legPivots; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            sedation = GetComponent<DeerSedation>();
            if (visual != null) restRotation = visual.localRotation;
            if (visual != null) restPosition = visual.localPosition;
            if (headPivot != null) headRest = headPivot.localRotation;
            legRest = new Quaternion[legs.Length];
            for (int i = 0; i < legs.Length; i++) if (legs[i] != null) legRest[i] = legs[i].localRotation;
            if (visual != null) CacheHooves();
            idlePhase = Random.value * 6.28f;
        }

        private void LateUpdate()
        {
            if (visual == null) return;
            if (sedation != null && sedation.State != DeerSedation.SedationState.Awake)
            {
                AnimateSedation();
                return;
            }
            visual.localRotation = restRotation;
            speed = Mathf.Lerp(speed, agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
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
            GroundHooves();
            if (headPivot != null)
                headPivot.localRotation = headRest * Quaternion.Euler(Mathf.Sin(idle * 0.6f) * 3f * (1 - moving),
                    Mathf.Sin(idle * 0.37f) * 14f * (1 - moving), 0);
        }

        // Cache only sole vertices, in bone space, rather than baking the entire skin every frame.
        private void CacheHooves()
        {
            var points = new List<Vector3>[legs.Length];
            for (int i = 0; i < legs.Length; i++) points[i] = new List<Vector3>();
            foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Mesh mesh = skin.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                Vector3[] vertices = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;
                Transform[] bones = skin.bones;
                Matrix4x4[] bindPoses = mesh.bindposes;
                for (int v = 0; v < weights.Length; v++)
                {
                    BoneWeight weight = weights[v];
                    if (weight.weight0 < 0.999f) continue;
                    int leg = System.Array.IndexOf(legs, bones[weight.boneIndex0]);
                    if (leg < 0) continue;
                    points[leg].Add(bindPoses[weight.boneIndex0].MultiplyPoint3x4(vertices[v]));
                }
            }
            hoofPoints = new Vector3[legs.Length][];
            for (int i = 0; i < legs.Length; i++)
            {
                float bottom = float.PositiveInfinity;
                foreach (Vector3 point in points[i]) bottom = Mathf.Min(bottom, point.y);
                // Retain the sole's real outline, including toe and heel during rotation.
                points[i].RemoveAll(p => p.y > bottom + 0.06f);
                hoofPoints[i] = points[i].ToArray();
            }
        }

        private void GroundHooves()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                Matrix4x4 matrix = legs[i].localToWorldMatrix;
                foreach (Vector3 point in hoofPoints[i])
                    lowest = Mathf.Min(lowest, matrix.MultiplyPoint3x4(point).y);
            }
            if (float.IsPositiveInfinity(lowest)) return;
            int count = Physics.RaycastNonAlloc(transform.position + Vector3.up, Vector3.down,
                groundHits, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float distance = float.PositiveInfinity;
            float groundY = 0;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.rigidbody != null || hit.normal.y < 0.5f) continue;
                if (hit.distance >= distance) continue;
                distance = hit.distance;
                groundY = hit.point.y;
            }
            if (!float.IsPositiveInfinity(distance))
                visual.position += Vector3.up * (groundY + 0.005f - lowest);
        }

        private void AnimateSedation()
        {
            if (sedation.State == DeerSedation.SedationState.Sedated)
            {
                float amount = sedation.SedationProgress;
                visual.localPosition = restPosition + Vector3.up * Mathf.Sin(Time.time * 5f) * 0.025f;
                visual.localRotation = restRotation * Quaternion.Euler(Mathf.Sin(Time.time * 4) * 4, 0, Mathf.Sin(Time.time * 7) * (3 + amount * 10));
            }
            else
            {
                float asleep = sedation.State == DeerSedation.SedationState.Unconscious || sedation.State == DeerSedation.SedationState.Trapped ? 1 : 1 - Mathf.SmoothStep(0, 1, sedation.RecoveryProgress);
                visual.localPosition = restPosition + new Vector3(1.15f, 0.55f, 0) * asleep;
                visual.localRotation = restRotation * Quaternion.Euler(0, 0, 90f * asleep);
            }
            for (int i = 0; i < legs.Length; i++)
                if (legs[i] != null)
                    legs[i].localRotation = legRest[i] * Quaternion.Euler(Mathf.Sin(Time.time * 3f + i) * 9, 0, 0);
            if (headPivot != null) headPivot.localRotation = headRest * Quaternion.Euler(10, Mathf.Sin(Time.time * 2) * 5, 0);
        }
    }
}
