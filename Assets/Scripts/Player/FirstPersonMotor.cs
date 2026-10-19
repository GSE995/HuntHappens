using UnityEngine;

namespace HuntHappens.Player
{
    [RequireComponent(typeof(CharacterController), typeof(PrototypeInput), typeof(FirstPersonLook))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;
        private CharacterController controller;
        private PrototypeInput input;
        private FirstPersonLook look;
        private float verticalSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PrototypeInput>();
            look = GetComponent<FirstPersonLook>();
        }

        private void Update()
        {
            input.ReadFrame();
            look.ApplyLook(input.Look);
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (controller.isGrounded && input.JumpPressed)
                verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);

            verticalSpeed += gravity * Time.deltaTime;
            Vector2 movement = Vector2.ClampMagnitude(input.Move, 1f);
            Vector3 velocity = (transform.right * movement.x + transform.forward * movement.y) * moveSpeed;
            velocity.y = verticalSpeed;
            CollisionFlags collision = controller.Move(velocity * Time.deltaTime);
            if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
        }
    }
}
