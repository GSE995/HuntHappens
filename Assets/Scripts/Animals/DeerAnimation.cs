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
        [Header("Gallop")]
        [SerializeField, Min(.5f)] private float gallopStride = 4.8f;
        [SerializeField, Min(.5f)] private float maximumGaitFrequency = 3.4f;
        [SerializeField, Min(0)] private float gallopLift = .10f;
        [SerializeField, Min(0)] private float gallopPitch = 3f;
        [Header("Leader attack poses")]
        [SerializeField, Range(0, 60)] private float attackHeadAngle = 32f;
        [SerializeField, Min(0)] private float preparationCrouch = .12f;
        [SerializeField, Min(0)] private float preparationLeanBack = .12f;
        [SerializeField, Range(0, 20)] private float chargeLean = 8f;
        [SerializeField, Min(.1f)] private float impactPoseDuration = .45f;
        [SerializeField, Range(0, 30)] private float impactRecoilAngle = 12f;
        [SerializeField, Min(0)] private float impactRecoilDistance = .18f;
        private NavMeshAgent agent;
        private Vector3 restPosition;
        private Quaternion headRest;
        private Quaternion[] legRest;
        private float speed;
        private float phase;
        private float idlePhase;
        private DeerSedation sedation;
        private DeerLeader leader;
        private Vector3 previousRootPosition;
        private float headAngle, bodyAngle, crouch, leanBack, preparation;
        public float AnimationSpeed => speed;
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
            leader = GetComponent<DeerLeader>();
            previousRootPosition = transform.position;
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
            Vector3 displacement = Vector3.ProjectOnPlane(transform.position - previousRootPosition, Vector3.up);
            previousRootPosition = transform.position;
            if (visual == null) return;
            if (sedation != null && sedation.State != DeerSedation.SedationState.Awake)
            {
                speed = headAngle = bodyAngle = crouch = leanBack = preparation = 0;
                AnimateSedation();
                return;
            }
            // Agent.Move does not reliably populate agent.velocity. Measure the root, never the animated mesh.
            // Ignore warps, enclosure placement and the first frame after a disabled component.
            if (Time.deltaTime <= 0) return;
            float measuredSpeed = displacement.magnitude / Time.deltaTime;
            if (displacement.magnitude > Mathf.Max(2, 40 * Time.deltaTime) || !agent.enabled || !agent.isOnNavMesh) measuredSpeed = 0;
            speed = Mathf.Lerp(speed, measuredSpeed, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
            float moving = Mathf.Clamp01(speed / 0.7f);
            float running = Mathf.InverseLerp(2f, 6f, speed);
            float cycles = Mathf.Min(maximumGaitFrequency, speed / Mathf.Lerp(1.8f, gallopStride, running));
            phase = Mathf.Repeat(phase + cycles * Time.deltaTime * Mathf.PI * 2, Mathf.PI * 2);
            float idle = Time.time * 1.7f + idlePhase;
            bool warning = leader != null && leader.State == DeerLeader.LeaderState.Warning;
            bool charging = leader != null && leader.State == DeerLeader.LeaderState.Charge;
            bool eating = leader != null && leader.State == DeerLeader.LeaderState.Eating;
            float windup = warning ? Mathf.SmoothStep(0, 1, leader.StateElapsed / Mathf.Max(.1f, leader.warningDuration * .75f)) : 0;
            float impact = leader != null ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((Time.time - leader.LastImpactTime) / impactPoseDuration)) : 0;
            float blend = 1 - Mathf.Exp(-12 * Time.deltaTime);
            preparation = Mathf.Lerp(preparation, windup, blend);
            headAngle = Mathf.Lerp(headAngle, (charging || eating ? attackHeadAngle : attackHeadAngle * windup) - impact * 16, blend);
            bodyAngle = Mathf.Lerp(bodyAngle, (charging ? chargeLean : -5 * windup) - impact * impactRecoilAngle, blend);
            crouch = Mathf.Lerp(crouch, windup * preparationCrouch, blend);
            leanBack = Mathf.Lerp(leanBack, windup * preparationLeanBack + impact * impactRecoilDistance, blend);
            float stomp = leader != null ? leader.StompLift : 0;
            bool stomping = leader != null && leader.State == DeerLeader.LeaderState.Stomp;
            if (stomping) moving = 0;
            float gallop = running * moving * (1 - preparation);
            visual.localRotation = restRotation * Quaternion.Euler(bodyAngle - stomp * 24 + Mathf.Sin(phase) * gallopPitch * gallop, 0, 0);
            visual.localPosition = restPosition + new Vector3(0, -crouch, -leanBack);
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                // Existing rig: 0/1 front, 2/3 rear. Stagger left/right contacts within each pair.
                float walkOffset = i == 0 || i == 3 ? 0 : Mathf.PI;
                float gallopOffset = (i < 2 ? 0 : Mathf.PI + .35f) + (i % 2) * .45f;
                float swing = Mathf.Lerp(Mathf.Sin(phase + walkOffset) * walkSwing,
                    Mathf.Sin(phase + gallopOffset) * runSwing, running) * moving * (1 - preparation);
                swing += (i < 2 ? -20 : 20) * preparation + (i < 2 ? 12 : -8) * impact;
                swing += (i < 2 ? -65 : 12) * stomp;
                legs[i].localRotation = legRest[i] * Quaternion.Euler(swing, 0, 0);
            }
            GroundHooves();
            // Ground correction supplies the contact baseline; add the gallop's brief flight afterwards.
            float lift = Mathf.Pow(Mathf.Max(0, Mathf.Sin(phase)), 2) * gallopLift * gallop;
            visual.localPosition += Vector3.up * (lift + (Mathf.Sin(idle) + 1) * .009f * (1 - moving));
            if (headPivot != null)
                headPivot.localRotation = headRest * Quaternion.Euler(headAngle + Mathf.Sin(idle * .6f) * 3 * (1 - moving) * (1 - preparation),
                    Mathf.Sin(idle * .37f) * 14 * (1 - moving) * (warning || charging ? 0 : 1), 0);
        }

        private void OnEnable() { previousRootPosition = transform.position; speed = 0; }

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
            if (!agent.enabled || !agent.isOnNavMesh || hoofPoints == null) return;
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
