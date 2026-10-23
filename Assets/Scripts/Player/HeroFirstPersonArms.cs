using UnityEngine;

namespace HuntHappens.Player
{
    [DefaultExecutionOrder(80)]
    public sealed class HeroFirstPersonArms : MonoBehaviour
    {
        [SerializeField] private FirstPersonArms poses;
        [SerializeField] private Transform leftUpper, leftLower, leftWrist, rightUpper, rightLower, rightWrist;
        private Quaternion[] rest;
        private Vector3 leftWristRest, rightWristRest;
        public void Configure(FirstPersonArms source, Transform lu, Transform ll, Transform lw, Transform ru, Transform rl, Transform rw)
        { poses = source; leftUpper = lu; leftLower = ll; leftWrist = lw; rightUpper = ru; rightLower = rl; rightWrist = rw; }
        private void Awake()
        {
            rest = new[] { leftUpper.localRotation, leftLower.localRotation, leftWrist.localRotation,
                rightUpper.localRotation, rightLower.localRotation, rightWrist.localRotation };
            leftWristRest = leftWrist.localPosition;
            rightWristRest = rightWrist.localPosition;
        }
        private void LateUpdate()
        {
            Solve(leftUpper, leftLower, leftWrist, poses.LeftHand, -1, 0);
            Solve(rightUpper, rightLower, rightWrist, poses.RightHand, 1, 3);
        }
        private void Solve(Transform upper, Transform lower, Transform wrist, Transform target, float side, int index)
        {
            upper.localRotation = rest[index]; lower.localRotation = rest[index+1]; wrist.localRotation = rest[index+2];
            wrist.localPosition = side < 0 ? leftWristRest : rightWristRest;
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, wrist.position);
            Vector3 delta = target.position - upper.position;
            float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a-b) + 0.001f, a+b-0.001f);
            Vector3 direction = delta.normalized;
            Vector3 pole = poses.transform.TransformDirection(new Vector3(side * 0.6f, -1, -0.3f));
            pole = Vector3.ProjectOnPlane(pole, direction).normalized;
            float along = (a*a + d*d - b*b) / (2*d);
            Vector3 elbow = upper.position + direction * along + pole * Mathf.Sqrt(Mathf.Max(0, a*a-along*along));
            upper.rotation = Quaternion.FromToRotation(lower.position-upper.position, elbow-upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(wrist.position-lower.position, target.position-lower.position) * lower.rotation;
            // Preserve the model's wrist orientation, adding the existing pose rotation.
            wrist.rotation = target.rotation * Quaternion.Inverse(poses.transform.rotation) * wrist.rotation;
            if (poses.Mode == FirstPersonArms.VisualMode.Tranquilizer && side > 0)
                wrist.rotation = Quaternion.AngleAxis(80, poses.transform.right) * wrist.rotation;
            else if (poses.Mode == FirstPersonArms.VisualMode.Tranquilizer)
            {
                wrist.rotation = Quaternion.AngleAxis(160, (wrist.position - lower.position).normalized) * wrist.rotation;
                wrist.rotation = Quaternion.AngleAxis(90, poses.transform.right) * wrist.rotation;
                wrist.rotation = Quaternion.AngleAxis(90, poses.transform.up) * wrist.rotation;
                wrist.position = target.position;
            }
        }
    }
}
