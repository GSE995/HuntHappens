using UnityEngine;
namespace HuntHappens.Player
{
    // Runs after hand pose blending and Hero arm IK. Keep the prop's side on the actual palm.
    [DefaultExecutionOrder(120)]
    public sealed class BaitHandAttachment : MonoBehaviour
    {
        private FirstPersonArms arms;
        private HeroFirstPersonArms hero;
        private Vector3 itemContact;
        public Vector3 ItemContact => itemContact;
        public Vector3 PalmPosition => hero != null ? hero.RightWrist.TransformPoint(hero.RightPalmContact) : arms.RightHand.TransformPoint(new Vector3(0, .04f, .08f));
        public void Configure(FirstPersonArms owner, Vector3 center, Vector3 size, Transform authoredGrip = null)
        {
            arms = owner; hero = owner.GetComponentInChildren<HeroFirstPersonArms>(true);
            // Slight overlap hides a hairline gap between two independently shaded meshes.
            itemContact = authoredGrip != null ? authoredGrip.localPosition : center + Vector3.right * (size.x * .5f - .008f);
        }
        public void UpdatePose()
        {
            if (arms == null || arms.RightHand == null) return;
            Quaternion rotation = hero != null ? hero.RightWrist.rotation * Quaternion.Inverse(HeroFirstPersonArms.BaitWristRotation) : arms.RightHand.rotation;
            transform.SetPositionAndRotation(PalmPosition - rotation * Vector3.Scale(itemContact, transform.lossyScale), rotation);
        }
        private void LateUpdate() => UpdatePose();
    }
}
