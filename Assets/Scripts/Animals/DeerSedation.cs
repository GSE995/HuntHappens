using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Animals
{
    [DisallowMultipleComponent, RequireComponent(typeof(DeerAI), typeof(DeerPhysics))]
    public sealed class DeerSedation : MonoBehaviour
    {
        public enum SedationState { Awake, Sedated, Unconscious, Recovering }
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
        public SedationState State => currentState;
        public float RecoveryProgress => Mathf.Clamp01(elapsed / recoveryDuration);
        public float SedationProgress => Mathf.Clamp01(elapsed / sedationDuration);
        public string StatusText => currentState == SedationState.Unconscious ? "Олень — спит" :
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

        public bool ApplyDose(float strength)
        {
            if (!isActiveAndEnabled || strength <= 0 || currentState == SedationState.Recovering) return false;
            if (currentState == SedationState.Unconscious)
            { elapsed = Mathf.Max(0, elapsed - strength * 2f); return true; }
            if (currentState == SedationState.Sedated)
            { elapsed = Mathf.Min(sedationDuration - 0.1f, elapsed + strength * 0.5f); return true; }
            dose += strength;
            if (dose < requiredDose) return true;
            speedBeforeHit = agent.enabled && agent.isOnNavMesh ? agent.speed : 0;
            currentState = SedationState.Sedated;
            ai.SetSedationState(DeerAI.DeerState.Sedated);
            elapsed = 0;
            return true;
        }

        private void Update()
        {
            if (currentState == SedationState.Awake) return;
            elapsed += Time.deltaTime;
            if (currentState == SedationState.Sedated)
            {
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.speed = Mathf.Lerp(speedBeforeHit, 0, Mathf.Clamp01(SedationProgress * 1.5f));
                    if (SedationProgress > 0.65f) agent.isStopped = true;
                }
                if (elapsed >= sedationDuration)
                {
                    currentState = SedationState.Unconscious;
                    ai.SetSedationState(DeerAI.DeerState.Unconscious);
                    physicsBody.FallAsleep();
                    elapsed = 0;
                }
            }
            else if (currentState == SedationState.Unconscious && elapsed >= sleepDuration && physicsBody.TryBeginRecovery())
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
