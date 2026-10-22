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
        public enum VisualMode { Empty, Tranquilizer, Carry, Trap, Hidden }
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
            holder != null && holder.IsHolding ? VisualMode.Carry :
            gun != null && gun.Selected ? VisualMode.Tranquilizer :
            gun != null && gun.TrapSelected ? VisualMode.Trap : VisualMode.Empty;
        public void Configure(Camera camera, PrototypeInput controls, CharacterController controller, ItemHolder carry,
            TranquilizerGun weapon, TrapPlacement traps, Transform left, Transform right, Transform leftArm, Transform rightArm, Transform smallTrap)
        {
            view = camera; input = controls; motor = controller; holder = carry; gun = weapon; placement = traps;
            leftHand = left; rightHand = right; leftSleeve = leftArm; rightSleeve = rightArm; miniatureTrap = smallTrap;
        }
        private void Awake()
        {
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
