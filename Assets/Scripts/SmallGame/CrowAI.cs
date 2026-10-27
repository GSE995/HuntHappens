using UnityEngine;

namespace HuntHappens.SmallGame
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class CrowAI : MonoBehaviour
    {
        public enum CrowState { Perched, Approaching, Returning, Sleeping }
        [Min(1)] public float detectionRadius = 24f;
        [Min(0.1f)] public float attemptInterval = 6f;
        [Min(0.1f)] public float flightSpeed = 5f;
        [Min(1)] public float sleepDuration = 25f;
        public bool IsSedated => State == CrowState.Sleeping;
        public float RemainingSleep => IsSedated ? Mathf.Max(0, wakeAt - Time.time) : 0;
        public Transform perch;
        public RatGatheringSpot feedingSpot;
        public bool CanTarget(RatCage cage) => cage != null && cage.CanStealBait &&
            feedingSpot != null && feedingSpot.AllowsCrowBait(cage.transform.position);
        public Transform leftWing, rightWing;
        public CrowState State { get; private set; }
        public RatCage Target => target;
        private RatCage target;
        private Rigidbody body;
        private Vector3 home, departure;
        private Quaternion homeRotation;
        private float nextAttempt, cruiseHeight;
        private int flightLeg;
        private float wakeAt;
        private bool sleepingOnPerch;

        public bool ApplyDose(float strength, float durationMultiplier = 1)
        {
            if (!isActiveAndEnabled || strength <= 0) return false;
            wakeAt = Mathf.Max(wakeAt, Time.time + sleepDuration);
            if (IsSedated) return true;
            sleepingOnPerch = State == CrowState.Perched;
            target = null;
            State = CrowState.Sleeping;
            AnimateWings(false);
            if (!sleepingOnPerch)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            transform.rotation *= Quaternion.Euler(0, 0, 65);
            return true;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }
        private void Start()
        {
            home = perch != null ? perch.position : transform.position;
            homeRotation = perch != null ? perch.rotation : transform.rotation;
            nextAttempt = Time.time + attemptInterval;
        }
        private void Update()
        {
            if (Time.deltaTime <= 0) return;
            if (State == CrowState.Sleeping)
            {
                if (Time.time < wakeAt) return;
                if (!body.isKinematic) body.linearVelocity = body.angularVelocity = Vector3.zero;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.isKinematic = true;
                body.useGravity = false;
                transform.rotation = homeRotation;
                if (sleepingOnPerch)
                {
                    State = CrowState.Perched;
                    nextAttempt = Time.time + Mathf.Max(.1f, attemptInterval);
                }
                else ReturnHome();
                return;
            }
            if (State == CrowState.Perched)
            {
                if (Time.time < nextAttempt) return;
                nextAttempt = Time.time + Mathf.Max(0.1f, attemptInterval);
                RatCage closest = null;
                float best = detectionRadius * detectionRadius;
                foreach (var cage in FindObjectsByType<RatCage>(FindObjectsSortMode.None))
                {
                    float distance = (cage.CrowApproachPoint - transform.position).sqrMagnitude;
                    if (CanTarget(cage) && distance < best) { closest = cage; best = distance; }
                }
                if (closest != null) { target = closest; BeginFlight(CrowState.Approaching); }
                return;
            }
            if (State == CrowState.Approaching && !CanTarget(target)) ReturnHome();
            Vector3 destination = State == CrowState.Approaching ? target.CrowApproachPoint : home;
            Vector3 waypoint = flightLeg == 0 ? new Vector3(departure.x, cruiseHeight, departure.z) :
                flightLeg == 1 ? new Vector3(destination.x, cruiseHeight, destination.z) : destination;
            Vector3 delta = waypoint - transform.position;
            if (delta.sqrMagnitude > 0.0001f)
            {
                Vector3 facing = Vector3.ProjectOnPlane(delta, Vector3.up);
                if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(facing);
                transform.position = Vector3.MoveTowards(transform.position, waypoint, flightSpeed * Time.deltaTime);
            }
            AnimateWings(true);
            if ((transform.position - waypoint).sqrMagnitude > .0001f) return;
            if (flightLeg < 2) { flightLeg++; return; }
            if (State == CrowState.Approaching)
            {
                // Recheck at arrival, never reserve food for the duration of a flight.
                if (CanTarget(target)) target.TryStealBait();
                ReturnHome();
            }
            else
            {
                State = CrowState.Perched;
                transform.SetPositionAndRotation(home, homeRotation);
                nextAttempt = Time.time + Mathf.Max(.1f, attemptInterval);
                AnimateWings(false);
            }
        }
        private void BeginFlight(CrowState state)
        {
            State = state;
            departure = transform.position;
            cruiseHeight = Mathf.Max(home.y, departure.y, target != null ? target.CrowApproachPoint.y : home.y) + 3f;
            flightLeg = 0;
        }
        private void ReturnHome() { target = null; BeginFlight(CrowState.Returning); }
        private void AnimateWings(bool flying)
        {
            float angle = flying ? Mathf.Sin(Time.time * 18f) * 40f : 0;
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0, 0, angle);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0, 0, -angle);
        }
    }
}
