using System;
using UnityEngine;

namespace HuntHappens.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(FirstPersonArms)), DefaultExecutionOrder(60)]
    public sealed class FirstPersonArmsAnimator : MonoBehaviour
    {
        [Serializable]
        public struct Pose
        {
            public Vector3 leftPosition, rightPosition, leftEuler, rightEuler;
            public Pose(Vector3 left, Vector3 right, Vector3 leftAngles, Vector3 rightAngles)
            { leftPosition = left; rightPosition = right; leftEuler = leftAngles; rightEuler = rightAngles; }
        }
        [Header("Local hand poses")]
        [SerializeField] private Pose empty = new Pose(new Vector3(-0.28f, -0.27f, 0.57f), new Vector3(0.28f, -0.27f, 0.57f), new Vector3(10, -12, -12), new Vector3(10, 12, 12));
        [SerializeField] private Pose carry = new Pose(new Vector3(-0.32f, -0.21f, 0.73f), new Vector3(0.32f, -0.21f, 0.73f), new Vector3(-18, 25, -15), new Vector3(-18, -25, 15));
        [SerializeField] private Pose trap = new Pose(new Vector3(-0.30f, -0.25f, 0.61f), new Vector3(0.29f, -0.29f, 0.56f), new Vector3(0, -10, -4), new Vector3(10, 15, 10));
        [Header("Hand framing")]
        [SerializeField, Min(0)] private float restingHandLowering = 0.18f;
        [SerializeField, Min(0)] private float carryingHandLowering = 0.25f;
        [Header("Existing weapon grip offsets (weapon-local)")]
        [SerializeField] private Vector3 rightGrip = new Vector3(0.16f, -0.035f, -0.19f);
        [SerializeField] private Vector3 leftSupport = new Vector3(-0.19f, -0.07f, 0.27f);
        [SerializeField] private Vector3 supportUnderBarrelOffset = new Vector3(0.14f, -0.045f, -0.05f);
        [SerializeField] private Vector3 rightGripEuler = new Vector3(-45, 0, -10);
        [SerializeField] private Vector3 leftSupportEuler = new Vector3(-12, 65, -15);
        [Header("Short FPS hand framing")]
        [SerializeField] private bool keepWristsAtBottom;
        [SerializeField, Range(0, 0.1f)] private float wristViewportHeight = 0.015f;
        [Header("Motion")]
        [SerializeField, Min(1)] private float blendSpeed = 15;
        [SerializeField, Min(0)] private float breathingAmount = 0.004f;
        [SerializeField, Min(0)] private float walkingAmount = 0.015f;
        [SerializeField, Min(0)] private float walkingFrequency = 3;
        [SerializeField, Range(0, 0.2f)] private float mouseSway = 0.018f;
        [SerializeField, Min(0)] private float jumpLift = 0.045f;
        [SerializeField, Min(0)] private float landingDip = 0.055f;
        [SerializeField, Min(0.1f)] private float throwDuration = 0.28f;
        [SerializeField, Min(0.1f)] private float placementDuration = 0.32f;
        [Header("Nearby obstacles (hands only)")]
        [SerializeField, Min(0.2f)] private float obstructionDistance = 0.85f;
        [SerializeField, Min(0.02f)] private float obstructionRadius = 0.13f;
        [SerializeField] private LayerMask obstacleLayers = ~0;
        private FirstPersonArms arms;
        private Vector3 left, right;
        private Quaternion leftRotation, rightRotation;
        private Vector3 sway;
        private float walkPhase, speed, airborneOffset, landing, miniatureBlend;
        private float lastThrow = float.NegativeInfinity, lastPlacement = float.NegativeInfinity, lastShot = float.NegativeInfinity;
        private bool wasGrounded;
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private float obstruction;
        [SerializeField] private FirstPersonArms.VisualMode currentMode;
        public FirstPersonArms.VisualMode CurrentMode => currentMode;
        public float ThrowPulse => Pulse(lastThrow, throwDuration);
        public float PlacementPulse => Pulse(lastPlacement, placementDuration);
        public float ShotPulse => Mathf.Exp(-14 * (Time.time - lastShot));
        public float WalkSpeed => speed;
        public float JumpOffset => airborneOffset - landing;
        public float Obstruction => obstruction;
        private static float Pulse(float began, float duration)
        { float t = (Time.time - began) / duration; return t >= 0 && t < 1 ? Mathf.Sin(t * Mathf.PI) : 0; }
        private void Awake()
        {
            arms = GetComponent<FirstPersonArms>();
            left = empty.leftPosition; right = empty.rightPosition;
            leftRotation = Quaternion.Euler(empty.leftEuler); rightRotation = Quaternion.Euler(empty.rightEuler);
            wasGrounded = arms.Motor != null && arms.Motor.isGrounded;
        }
        private void OnEnable()
        {
            if (arms == null) arms = GetComponent<FirstPersonArms>();
            if (arms.Gun != null) arms.Gun.Fired += OnFired;
            if (arms.Holder != null) arms.Holder.Released += OnReleased;
            if (arms.Placement != null) arms.Placement.Placed += OnPlaced;
        }
        private void OnDisable()
        {
            if (arms == null) return;
            arms.SuppressWeaponRenderers(false);
            if (arms.Gun != null) arms.Gun.Fired -= OnFired;
            if (arms.Holder != null) arms.Holder.Released -= OnReleased;
            if (arms.Placement != null) arms.Placement.Placed -= OnPlaced;
        }
        private void OnFired() { lastShot = Time.time; lastThrow = float.NegativeInfinity; }
        private void OnReleased(bool thrown) { if (thrown) lastThrow = Time.time; }
        private void OnPlaced() => lastPlacement = Time.time;
        private void LateUpdate()
        {
            if (!arms.isActiveAndEnabled) return;
            var mode = arms.Mode;
            if (mode == FirstPersonArms.VisualMode.Hidden) lastThrow = float.NegativeInfinity;
            bool throwing = Time.time - lastThrow < throwDuration;
            if (throwing && mode != FirstPersonArms.VisualMode.Hidden) mode = FirstPersonArms.VisualMode.Carry;
            currentMode = mode;
            arms.SuppressWeaponRenderers(throwing);
            float blend = 1 - Mathf.Exp(-blendSpeed * Time.deltaTime);
            bool grounded = arms.Motor != null && arms.Motor.isGrounded;
            float velocity = arms.Motor != null ? Vector3.ProjectOnPlane(arms.Motor.velocity, Vector3.up).magnitude : 0;
            speed = Mathf.Lerp(speed, grounded ? velocity : 0, blend);
            walkPhase += speed * walkingFrequency * Time.deltaTime;
            airborneOffset = Mathf.Lerp(airborneOffset, !grounded && arms.Motor != null && arms.Motor.velocity.y > 0 ? jumpLift : 0, blend);
            if (grounded && !wasGrounded) landing = landingDip;
            landing *= Mathf.Exp(-12 * Time.deltaTime);
            wasGrounded = grounded;
            Vector2 mouse = arms.Input != null ? Vector2.ClampMagnitude(arms.Input.Look, 20) : Vector2.zero;
            sway = Vector3.Lerp(sway, new Vector3(-mouse.x, -mouse.y, 0) * (mouseSway / 20), blend);
            Pose pose = mode == FirstPersonArms.VisualMode.Carry ? carry : mode == FirstPersonArms.VisualMode.Trap ? trap : empty;
            Vector3 targetLeft = pose.leftPosition, targetRight = pose.rightPosition;
            Quaternion targetLeftRotation = Quaternion.Euler(pose.leftEuler), targetRightRotation = Quaternion.Euler(pose.rightEuler);
            bool weaponPose = mode == FirstPersonArms.VisualMode.Tranquilizer && arms.Gun.WeaponVisual != null;
            if (weaponPose)
            {
                Transform weapon = arms.Gun.WeaponVisual;
                targetLeft = transform.InverseTransformPoint(weapon.TransformPoint(leftSupport + supportUnderBarrelOffset));
                targetRight = transform.InverseTransformPoint(weapon.TransformPoint(rightGrip));
                targetLeftRotation = Quaternion.Inverse(transform.rotation) * weapon.rotation * Quaternion.Euler(leftSupportEuler);
                targetRightRotation = Quaternion.Inverse(transform.rotation) * weapon.rotation * Quaternion.Euler(rightGripEuler);
                Vector3 subtleMotion = Vector3.up * (JumpOffset * 0.4f + Mathf.Sin(Time.time * 1.7f) * breathingAmount * 0.35f);
                targetLeft += subtleMotion;
                targetRight += subtleMotion;
            }
            Vector3 motion = sway + Vector3.up * (Mathf.Sin(Time.time * 1.7f) * breathingAmount + JumpOffset);
            float walking = Mathf.Clamp01(speed / 3);
            Vector3 leftWalk = new Vector3(Mathf.Sin(walkPhase) * 0.3f, Mathf.Sin(walkPhase), 0) * walkingAmount * walking;
            Vector3 rightWalk = new Vector3(-leftWalk.x, -leftWalk.y, 0);
            // The gun remains owned by its existing script; hands track its animated grip points.
            if (!weaponPose)
            {
                // Place wrists near the bottom of the view, leaving mostly hands visible.
                float lowering = mode == FirstPersonArms.VisualMode.Carry ? carryingHandLowering : restingHandLowering;
                targetLeft.y -= lowering;
                targetRight.y -= lowering;
                targetLeft += motion + leftWalk + Vector3.forward * (ThrowPulse * 0.13f);
                targetRight += motion + rightWalk + Vector3.forward * (ThrowPulse * 0.13f);
                targetLeft += new Vector3(0, -0.10f, 0.13f) * PlacementPulse;
            }
            float nearest = obstructionDistance;
            if (arms.View != null)
                for (int side = -1; side <= 1; side++)
                {
                    Vector3 origin = arms.View.transform.position + arms.View.transform.right * (side * 0.22f);
                    int count = Physics.SphereCastNonAlloc(origin, obstructionRadius, arms.View.transform.forward, hits,
                        obstructionDistance, obstacleLayers, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count; i++)
                    {
                        Transform hit = hits[i].collider.transform;
                        if (hit.IsChildOf(arms.Input.transform)) continue;
                        if (arms.Holder.IsHolding && hit.IsChildOf(arms.Holder.HeldItem.transform)) continue;
                        nearest = Mathf.Min(nearest, hits[i].distance);
                    }
                }
            obstruction = Mathf.Lerp(obstruction, Mathf.InverseLerp(obstructionDistance, 0.17f, nearest), blend);
            Vector3 retract = new Vector3(0, -0.14f, -0.16f) * obstruction;
            targetLeft += retract; targetRight += retract;
            left = Vector3.Lerp(left, targetLeft, blend);
            right = Vector3.Lerp(right, targetRight, blend);
            if (nearest < obstructionDistance - 0.01f)
            {
                // Keep the leading edge behind the surface even while the smoothing catches up.
                float safeDepth = Mathf.Max(0.18f, nearest + obstructionRadius - 0.26f);
                left.z = Mathf.Min(left.z, safeDepth);
                right.z = Mathf.Min(right.z, safeDepth);
            }
            leftRotation = Quaternion.Slerp(leftRotation, targetLeftRotation, blend);
            rightRotation = Quaternion.Slerp(rightRotation, targetRightRotation, blend);
            miniatureBlend = Mathf.Lerp(miniatureBlend, mode == FirstPersonArms.VisualMode.Trap ? 1 : 0, blend);
            if (keepWristsAtBottom && !weaponPose && arms.View != null)
            {
                KeepWristAtBottom(ref left);
                KeepWristAtBottom(ref right);
            }
            arms.Draw(left, leftRotation, right, rightRotation, mode != FirstPersonArms.VisualMode.Hidden && nearest > 0.25f, throwing ? 0 : miniatureBlend);
        }
        private void KeepWristAtBottom(ref Vector3 position)
        {
            // Use the camera projection so the cuff stays below the frame across FOVs.
            Vector3 viewport = arms.View.WorldToViewportPoint(transform.TransformPoint(position));
            if (viewport.z <= arms.View.nearClipPlane || viewport.y <= wristViewportHeight) return;
            viewport.y = wristViewportHeight;
            position = transform.InverseTransformPoint(arms.View.ViewportToWorldPoint(viewport));
        }
    }
}
