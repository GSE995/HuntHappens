using UnityEngine;

namespace HuntHappens.Player
{
    // Keep the camera independent of the animated skeleton to avoid head bob and clipping.
    public sealed class PlayerBodyAnimation : MonoBehaviour
    {
        private CharacterController motor;
        private Animator animator;
        private void Awake()
        {
            motor = GetComponentInParent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        private void Update()
        {
            Vector3 velocity = motor.velocity;
            velocity.y = 0;
            // The supplied asset has a walking clip only. Freeze it while idle or airborne.
            animator.speed = motor.isGrounded ? Mathf.Clamp(velocity.magnitude / 2.5f, 0, 3.2f) : 0;
        }
    }
}
