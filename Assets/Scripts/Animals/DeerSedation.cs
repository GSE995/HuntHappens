using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Animals
{
    [DisallowMultipleComponent, RequireComponent(typeof(DeerAI), typeof(DeerPhysics))]
    public sealed class DeerSedation : MonoBehaviour
    {
        public enum SedationState { Awake, Sedated, Unconscious, Recovering, Trapped }
        [SerializeField] private SedationState currentState;
        [SerializeField, Min(0.1f)] private float requiredDose = 1f;
        [SerializeField, Min(0.5f)] private float sedationDuration = 3f;
        [SerializeField, Min(1)] private float sleepDuration = 25f;
        [SerializeField, Min(0.5f)] private float recoveryDuration = 1.2f;
        [SerializeField] private TMP_Text sleepIndicator;
        private DeerAI ai;
        private DeerPhysics physicsBody;
        private NavMeshAgent agent;
        private float dose;
        private float elapsed;
        private float speedBeforeHit;
        private float activeSedationDuration;
        public float CurrentSedationDuration => activeSedationDuration > 0 ? activeSedationDuration : sedationDuration;
        private MonoBehaviour deliveryReservation;
        private RigidbodyConstraints beforeTrapConstraints;
        public HuntHappens.Traps.AnimalTrap Trap { get; private set; }
        public bool TryTrap(HuntHappens.Traps.AnimalTrap trap)
        {
            if (trap == null || !isActiveAndEnabled || IsDelivered || currentState != SedationState.Awake || physicsBody.IsHeld) return false;
            Trap = trap;
            foreach (var leader in FindObjectsByType<DeerLeader>()) leader.NoticeAttack(this);
            beforeTrapConstraints = physicsBody.Body.constraints;
            physicsBody.FallAsleep();
            physicsBody.Body.constraints = RigidbodyConstraints.FreezeAll;
            currentState = SedationState.Trapped;
            ai.SetSedationState(DeerAI.DeerState.Trapped);
            elapsed = 0;
            return true;
        }
        public void ReleaseTrapForCarry()
        {
            if (currentState != SedationState.Trapped) return;
            physicsBody.Body.constraints = beforeTrapConstraints;
            currentState = SedationState.Unconscious;
            ai.SetSedationState(DeerAI.DeerState.Unconscious);
            elapsed = 0;
            var previous = Trap;
            Trap = null;
            previous?.Release(this);
        }
        public HuntHappens.Transport.DeerCart Cart { get; internal set; }
        public void RestartSleepTimer() { if (currentState == SedationState.Unconscious) elapsed = 0; }
        public string QuestId { get; set; }
        public bool IsDelivered { get; private set; }
        public void PlaceInEnclosure(Vector3 position, Quaternion rotation)
        {
            Cart?.ReleaseDelivered(this);
            GetComponent<HuntHappens.Interaction.PickupItem>().Holder?.Drop();
            IsDelivered = true;
            deliveryReservation = null;
            currentState = SedationState.Awake;
            ai.enabled = false;
            agent.enabled = false;
            physicsBody.enabled = false;
            var body = GetComponent<Rigidbody>();
            if (!body.isKinematic) body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.useGravity = false;
            GetComponent<HuntHappens.Interaction.PickupItem>().enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            if (sleepIndicator != null) sleepIndicator.gameObject.SetActive(false);
        }
        private void OnDestroy() { if (Cart != null) Cart.ReleaseDelivered(this); }
        public bool DeliveryReserved => deliveryReservation != null && deliveryReservation.isActiveAndEnabled;
        public void ReserveForDelivery(MonoBehaviour owner) { if (deliveryReservation == null || deliveryReservation == owner) deliveryReservation = owner; }
        public void ReleaseDeliveryReservation(MonoBehaviour owner) { if (deliveryReservation == owner) deliveryReservation = null; }
        public bool TryMarkDelivered()
        {
            if (IsDelivered || !isActiveAndEnabled || currentState != SedationState.Unconscious || physicsBody.IsHeld) return false;
            IsDelivered = true;
            return true;
        }
        public SedationState State => currentState;
        public float RecoveryProgress => Mathf.Clamp01(elapsed / recoveryDuration);
        public float SedationProgress => Mathf.Clamp01(elapsed / CurrentSedationDuration);
        public string StatusText => currentState == SedationState.Trapped ? "Олень — в ловушке" : currentState == SedationState.Unconscious ? "Олень — спит" :
            currentState == SedationState.Sedated ? "Олень — засыпает" :
            currentState == SedationState.Recovering ? "Олень — просыпается" : "Олень — бодрствует";

        public void SetIndicator(TMP_Text value) => sleepIndicator = value;
        public void SetDurations(float sedate, float sleep, float recover)
        { sedationDuration = Mathf.Max(0.5f, sedate); sleepDuration = Mathf.Max(1, sleep); recoveryDuration = Mathf.Max(0.5f, recover); }

        private void Awake()
        {
            ai = GetComponent<DeerAI>();
            physicsBody = GetComponent<DeerPhysics>();
            agent = GetComponent<NavMeshAgent>();
        }

        public bool ApplyDose(float strength, float durationMultiplier = 1)
        {
            if (IsDelivered || !isActiveAndEnabled || strength <= 0 || currentState == SedationState.Recovering || currentState == SedationState.Trapped) return false;
            if (currentState == SedationState.Unconscious)
            { elapsed = Mathf.Max(0, elapsed - strength * 2f); return true; }
            if (currentState == SedationState.Sedated)
            { elapsed = Mathf.Min(CurrentSedationDuration - 0.1f, elapsed + strength * 0.5f); return true; }
            foreach (var leader in FindObjectsByType<DeerLeader>()) leader.NoticeAttack(this);
            dose += strength;
            if (dose < requiredDose) return true;
            speedBeforeHit = agent.enabled && agent.isOnNavMesh ? agent.speed : 0;
            currentState = SedationState.Sedated;
            activeSedationDuration = Mathf.Max(0.5f, sedationDuration * Mathf.Clamp(durationMultiplier, 0.25f, 1));
            ai.SetSedationState(DeerAI.DeerState.Sedated);
            elapsed = 0;
            return true;
        }

        private void Update()
        {
            if (currentState == SedationState.Awake || currentState == SedationState.Trapped) return;
            elapsed += Time.deltaTime;
            if (currentState == SedationState.Sedated)
            {
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.speed = Mathf.Lerp(speedBeforeHit, 0, Mathf.Clamp01(SedationProgress * 1.5f));
                    if (SedationProgress > 0.65f) agent.isStopped = true;
                }
                if (elapsed >= CurrentSedationDuration)
                {
                    currentState = SedationState.Unconscious;
                    ai.SetSedationState(DeerAI.DeerState.Unconscious);
                    physicsBody.FallAsleep();
                    elapsed = 0;
                }
            }
            else if (currentState == SedationState.Unconscious && !DeliveryReserved && elapsed >= sleepDuration && physicsBody.TryBeginRecovery())
            {
                currentState = SedationState.Recovering;
                ai.SetSedationState(DeerAI.DeerState.Recovering);
                elapsed = 0;
            }
        }

        private void FixedUpdate()
        {
            if (currentState != SedationState.Recovering) return;
            physicsBody.UpdateRecovery(RecoveryProgress);
            // One extra physics step ensures MovePosition has completed before enabling the agent.
            if (elapsed < recoveryDuration + Time.fixedDeltaTime * 2) return;
            if (physicsBody.FinishRecovery())
            {
                currentState = SedationState.Awake;
                dose = elapsed = 0;
                ai.ResumeAfterSedation();
            }
            else
            {
                currentState = SedationState.Unconscious;
                ai.SetSedationState(DeerAI.DeerState.Unconscious);
                elapsed = sleepDuration - 1f;
            }
        }

        private void LateUpdate()
        {
            if (sleepIndicator == null) return;
            bool asleep = currentState == SedationState.Unconscious;
            sleepIndicator.gameObject.SetActive(asleep);
            if (!asleep) return;
            sleepIndicator.transform.position = physicsBody.Body.worldCenterOfMass + Vector3.up * (1.3f + Mathf.Sin(Time.time * 2) * 0.06f);
            if (Camera.main != null) sleepIndicator.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
