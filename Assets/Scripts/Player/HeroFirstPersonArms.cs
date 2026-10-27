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
        private Vector3 leftCageContact, rightCageContact;
        private bool cageContactsReady;
        public void Configure(FirstPersonArms source, Transform lu, Transform ll, Transform lw, Transform ru, Transform rl, Transform rw)
        { poses = source; leftUpper = lu; leftLower = ll; leftWrist = lw; rightUpper = ru; rightLower = rl; rightWrist = rw; }
        private void Awake()
        {
            rest = new[] { leftUpper.localRotation, leftLower.localRotation, leftWrist.localRotation,
                rightUpper.localRotation, rightLower.localRotation, rightWrist.localRotation };
            leftWristRest = leftWrist.localPosition;
            rightWristRest = rightWrist.localPosition;
        }
        public Transform RightWrist => rightWrist;
        private Vector3 baitPalmContact;
        private bool baitContactReady;
        public Vector3 RightPalmContact
        {
            get
            {
                if (!baitContactReady)
                {
                    baitContactReady = true;
                    baitPalmContact = new Vector3(-.06f,.12f,0);
                    var skin = GetComponentInChildren<SkinnedMeshRenderer>();
                    int bone = skin != null ? System.Array.IndexOf(skin.bones,rightWrist) : -1;
                    if (bone >= 0)
                    {
                        float best = float.PositiveInfinity;
                        var vertices = skin.sharedMesh.vertices; var weights = skin.sharedMesh.boneWeights;
                        for (int i=0;i<vertices.Length;i++)
                            if (weights[i].boneIndex0==bone && weights[i].weight0>.8f)
                            {
                                Vector3 point = skin.sharedMesh.bindposes[bone].MultiplyPoint3x4(vertices[i]);
                                float distance = (point-new Vector3(-.06f,.12f,0)).sqrMagnitude;
                                if (distance<best) { best=distance; baitPalmContact=point; }
                            }
                    }
                }
                return baitPalmContact;
            }
        }
        public static Quaternion BaitWristRotation => Quaternion.AngleAxis(-20, Vector3.right) * Quaternion.LookRotation(Vector3.down, Vector3.forward);
        private void LateUpdate()
        {
            Solve(leftUpper, leftLower, leftWrist, poses.LeftHand, -1, 0);
            Solve(rightUpper, rightLower, rightWrist, poses.RightHand, 1, 3);
        }
        private void Solve(Transform upper, Transform lower, Transform wrist, Transform target, float side, int index)
        {
            bool holdingCage = poses.Mode == FirstPersonArms.VisualMode.Cage && poses.MiniatureCage != null && poses.MiniatureCage.gameObject.activeInHierarchy;
            Vector3 targetPosition = target.position;
            Quaternion cageRotation = Quaternion.identity;
            if (holdingCage)
            {
                if (!cageContactsReady)
                {
                    leftCageContact = CageContact(leftWrist);
                    rightCageContact = CageContact(rightWrist);
                    cageContactsReady = true;
                }
                // The Hero hand's length runs along local Y. Aim it along the side rail,
                // with the hand's inner surface touching the rail and the palm outside it.
                // Both Hero wrist bones use the same roll convention; the right mesh
                // is already mirrored. Flipping its roll again turns the palm outward.
                cageRotation = poses.MiniatureCage.rotation * Quaternion.LookRotation(Vector3.down, Vector3.forward);
                Vector3 contact = side < 0 ? leftCageContact : rightCageContact;
                targetPosition = poses.GetCageGrip(side < 0).position - cageRotation * Vector3.Scale(contact, wrist.lossyScale);
            }
            upper.localRotation = rest[index]; lower.localRotation = rest[index+1]; wrist.localRotation = rest[index+2];
            wrist.localPosition = side < 0 ? leftWristRest : rightWristRest;
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, wrist.position);
            Vector3 delta = targetPosition - upper.position;
            float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a-b) + 0.001f, a+b-0.001f);
            Vector3 direction = delta.normalized;
            Vector3 pole = poses.transform.TransformDirection(new Vector3(side * 0.6f, -1, -0.3f));
            pole = Vector3.ProjectOnPlane(pole, direction).normalized;
            float along = (a*a + d*d - b*b) / (2*d);
            Vector3 elbow = upper.position + direction * along + pole * Mathf.Sqrt(Mathf.Max(0, a*a-along*along));
            upper.rotation = Quaternion.FromToRotation(lower.position-upper.position, elbow-upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(wrist.position-lower.position, targetPosition-lower.position) * lower.rotation;
            if (holdingCage)
            {
                wrist.SetPositionAndRotation(targetPosition, cageRotation);
                return;
            }
            if (poses.Mode == FirstPersonArms.VisualMode.Bait && side > 0)
            {
                wrist.SetPositionAndRotation(targetPosition, poses.transform.rotation * BaitWristRotation);
                return;
            }
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
        private Vector3 CageContact(Transform wrist)
        {
            var skin = GetComponentInChildren<SkinnedMeshRenderer>();
            Vector3 fallback = new Vector3(wrist == rightWrist ? -0.05f : 0.05f, 0.12f, 0);
            if (skin == null || skin.sharedMesh == null) return fallback;
            int bone = System.Array.IndexOf(skin.bones, wrist);
            if (bone < 0) return fallback;
            var mesh = skin.sharedMesh;
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            var bind = mesh.bindposes[bone];
            Bounds bounds = default;
            bool found = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (weights[i].boneIndex0 != bone || weights[i].weight0 < 0.8f) continue;
                Vector3 point = bind.MultiplyPoint3x4(vertices[i]);
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
            return found ? new Vector3(wrist == rightWrist ? bounds.min.x - 0.006f : bounds.max.x + 0.006f, bounds.center.y * 0.7f, bounds.center.z) : fallback;
        }
    }
}
