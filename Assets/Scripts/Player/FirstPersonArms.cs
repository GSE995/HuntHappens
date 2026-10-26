using HuntHappens.Interaction;
using HuntHappens.Traps;
using HuntHappens.Weapons;
using UnityEngine;
using UnityEngine.Rendering;

namespace HuntHappens.Player
{
    // Pure presentation: no colliders, input actions, physical grabbing or weapon transform writes.
    [DisallowMultipleComponent]
    public sealed class FirstPersonArms : MonoBehaviour
    {
        public enum VisualMode { Empty, Tranquilizer, Carry, Trap, Hidden, Cage }
        [SerializeField] private Camera view;
        [SerializeField] private PrototypeInput input;
        [SerializeField] private CharacterController motor;
        [SerializeField] private ItemHolder holder;
        [SerializeField] private TranquilizerGun gun;
        [SerializeField] private TrapPlacement placement;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform leftSleeve;
        [SerializeField] private Transform rightSleeve;
        [SerializeField] private Transform miniatureTrap;
        [Header("Sleeves (local to FirstPersonArms)")]
        [SerializeField] private Vector3 leftElbow = new Vector3(-0.42f, -0.48f, 0.24f);
        [SerializeField] private Vector3 rightElbow = new Vector3(0.42f, -0.48f, 0.24f);
        [SerializeField, Min(0.01f)] private float sleeveWidth = 0.135f;
        private Renderer[] renderers;
        private Renderer[] weaponRenderers;
        private bool[] originalWeaponVisibility;
        private HuntHappens.SmallGame.RatHunting hunting;
        private Transform miniatureCage;
        private Transform leftCageGrip, rightCageGrip;
        private Vector3 leftCageContact, rightCageContact;
        private Quaternion leftCageRotation, rightCageRotation;
        private bool cageGripsReady;
        public HuntHappens.SmallGame.RatHunting Hunting => hunting != null ? hunting : hunting = GetComponentInParent<HuntHappens.SmallGame.RatHunting>();
        public Transform MiniatureCage => miniatureCage;
        public Transform GetCageGrip(bool left) => left ? leftCageGrip : rightCageGrip;
        public Camera View => view;
        public PrototypeInput Input => input;
        public CharacterController Motor => motor;
        public ItemHolder Holder => holder;
        public TranquilizerGun Gun => gun;
        public TrapPlacement Placement => placement;
        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;
        public Transform MiniatureTrap => miniatureTrap;
        public VisualMode Mode => input == null || input.GameplayBlocked ? VisualMode.Hidden :
            holder != null && (holder.IsHolding || holder.GetComponent<HuntHappens.Transport.CartDriver>()?.IsDriving == true) ? VisualMode.Carry :
            gun != null && gun.Selected ? VisualMode.Tranquilizer :
            gun != null && gun.TrapSelected ? VisualMode.Trap :
            hunting != null && hunting.Selected ? VisualMode.Cage : VisualMode.Empty;
        public void Configure(Camera camera, PrototypeInput controls, CharacterController controller, ItemHolder carry,
            TranquilizerGun weapon, TrapPlacement traps, Transform left, Transform right, Transform leftArm, Transform rightArm, Transform smallTrap)
        {
            view = camera; input = controls; motor = controller; holder = carry; gun = weapon; placement = traps;
            leftHand = left; rightHand = right; leftSleeve = leftArm; rightSleeve = rightArm; miniatureTrap = smallTrap;
        }
        private void Awake()
        {
            hunting = GetComponentInParent<HuntHappens.SmallGame.RatHunting>();
            EnsureCageVisual();
            renderers = GetComponentsInChildren<Renderer>(true);
            weaponRenderers = gun != null && gun.WeaponVisual != null ? gun.WeaponVisual.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            originalWeaponVisibility = new bool[weaponRenderers.Length];
            for (int i = 0; i < weaponRenderers.Length; i++) originalWeaponVisibility[i] = weaponRenderers[i].enabled;
            foreach (Renderer renderer in renderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
        }
        private void EnsureCageVisual()
        {
            if (hunting == null) hunting = GetComponentInParent<HuntHappens.SmallGame.RatHunting>();
            if (hunting == null || hunting.cagePrefab == null) return;
            if (miniatureCage == null)
            {
                miniatureCage = new GameObject("Held Cage").transform;
                miniatureCage.SetParent(transform, false);
                miniatureCage.gameObject.SetActive(false);
                CopyCageMeshes(hunting.cagePrefab.transform, miniatureCage);
                renderers = GetComponentsInChildren<Renderer>(true);
            }
            if (cageGripsReady || leftHand == null || rightHand == null) return;
            leftCageGrip = CageGrip("Left Grip", -1);
            rightCageGrip = CageGrip("Right Grip", 1);
            CageHandPose(leftHand, -1, out leftCageContact, out leftCageRotation);
            CageHandPose(rightHand, 1, out rightCageContact, out rightCageRotation);
            cageGripsReady = true;
        }
        private Transform CageGrip(string name, float side)
        {
            var grip = miniatureCage.Find(name);
            if (grip == null) { grip = new GameObject(name).transform; grip.SetParent(miniatureCage, false); }
            grip.localPosition = new Vector3(side * 0.32f, 0.25f, -0.15f);
            return grip;
        }
        private static void CageHandPose(Transform hand, float side, out Vector3 contact, out Quaternion rotation)
        {
            var bones = hand.GetComponentsInChildren<Transform>(true);
            var wrist = System.Array.Find(bones, bone => bone.name == "Wrist");
            var forward = System.Array.Find(bones, bone => bone.name == "Guide_Forward");
            var palm = System.Array.Find(bones, bone => bone.name == "Guide_Palm");
            var index = System.Array.Find(bones, bone => bone.name == "Index_01");
            var little = System.Array.Find(bones, bone => bone.name == "Little_01");
            contact = new Vector3(0, 0.04f, 0.08f);
            rotation = Quaternion.Euler(-18, -side * 25, side * 15);
            if (wrist == null || forward == null || palm == null || index == null || little == null) return;
            Vector3 localForward = hand.InverseTransformDirection(forward.position - wrist.position).normalized;
            Vector3 localPalm = hand.InverseTransformDirection(palm.position - wrist.position).normalized;
            contact = hand.InverseTransformPoint((index.position + little.position) * 0.5f) + localPalm * 0.012f;
            // Fingers run along the cage; both palms face inward onto its side rails.
            rotation = Quaternion.LookRotation(Vector3.forward, Vector3.right * -side) * Quaternion.Inverse(Quaternion.LookRotation(localForward, localPalm));
        }
        private static void CopyCageMeshes(Transform source, Transform parent)
        {
            var copy = new GameObject(source.name).transform;
            copy.SetParent(parent, false);
            copy.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            copy.localScale = source.localScale;
            copy.gameObject.layer = 2; // Same non-interactive layer as the existing first-person hands.
            copy.gameObject.SetActive(source.gameObject.activeSelf);
            var mesh = source.GetComponent<MeshFilter>();
            var renderer = source.GetComponent<MeshRenderer>();
            if (mesh != null && renderer != null)
            {
                copy.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var visual = copy.gameObject.AddComponent<MeshRenderer>();
                visual.sharedMaterials = renderer.sharedMaterials;
                visual.shadowCastingMode = ShadowCastingMode.Off;
                visual.receiveShadows = false;
                visual.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
            foreach (Transform child in source) CopyCageMeshes(child, copy);
        }
        public void SuppressWeaponRenderers(bool suppress)
        {
            if (weaponRenderers == null) return;
            for (int i = 0; i < weaponRenderers.Length; i++)
                if (weaponRenderers[i] != null) weaponRenderers[i].enabled = originalWeaponVisibility[i] && !suppress;
        }
        public void Draw(Vector3 leftPosition, Quaternion leftRotation, Vector3 rightPosition, Quaternion rightRotation,
            bool visible, float miniatureBlend)
        {
            if (leftHand == null || rightHand == null) return;
            EnsureCageVisual();
            if (miniatureCage != null)
            {
                bool holdingCage = Mode == VisualMode.Cage && hunting.Available > 0 && miniatureBlend > 0.01f;
                miniatureCage.gameObject.SetActive(visible && holdingCage);
                miniatureCage.localPosition = (leftPosition + rightPosition) * 0.5f + new Vector3(0, -0.025f, 0);
                miniatureCage.localRotation = Quaternion.identity;
                // Keep the prop and the grip spacing fixed while the shared pose blends into view.
                miniatureCage.localScale = Vector3.one * 0.45f;
                if (holdingCage && cageGripsReady)
                {
                    leftRotation = leftCageRotation;
                    rightRotation = rightCageRotation;
                    leftPosition = transform.InverseTransformPoint(leftCageGrip.position) - leftRotation * Vector3.Scale(leftCageContact, leftHand.localScale);
                    rightPosition = transform.InverseTransformPoint(rightCageGrip.position) - rightRotation * Vector3.Scale(rightCageContact, rightHand.localScale);
                }
            }
            foreach (Renderer renderer in renderers) if (renderer != null) renderer.enabled = visible;
            leftHand.SetLocalPositionAndRotation(leftPosition, leftRotation);
            rightHand.SetLocalPositionAndRotation(rightPosition, rightRotation);
            Sleeve(leftSleeve, leftElbow, leftPosition);
            Sleeve(rightSleeve, rightElbow, rightPosition);
            if (miniatureTrap != null)
            {
                miniatureTrap.gameObject.SetActive(visible && Mode == VisualMode.Trap && miniatureBlend > 0.01f);
                miniatureTrap.localScale = Vector3.one * miniatureBlend;
            }
        }
        private void Sleeve(Transform sleeve, Vector3 elbow, Vector3 wrist)
        {
            if (sleeve == null) return;
            Vector3 delta = wrist - elbow;
            sleeve.localPosition = (elbow + wrist) * 0.5f;
            sleeve.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            sleeve.localScale = new Vector3(sleeveWidth, delta.magnitude * 0.5f, sleeveWidth);
        }
        private void OnDisable()
        {
            SuppressWeaponRenderers(false);
            if (renderers != null) foreach (Renderer renderer in renderers) if (renderer != null) renderer.enabled = false;
        }
    }
}
