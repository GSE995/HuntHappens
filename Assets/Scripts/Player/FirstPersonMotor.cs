using UnityEngine;

namespace HuntHappens.Player
{
    [RequireComponent(typeof(CharacterController), typeof(PrototypeInput), typeof(FirstPersonLook))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 1.6f;
        [SerializeField, Range(.1f, 1f)] private float deerCarrySpeedMultiplier = .55f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;
        private CharacterController controller;
        private PrototypeInput input;
        private FirstPersonLook look;
        private HuntHappens.Interaction.ItemHolder holder;
        public float CurrentMoveSpeed => cartDriver != null && cartDriver.IsDriving
            ? cartDriver.MoveSpeed * (input != null && input.SprintHeld ? sprintMultiplier : 1f)
            : holder != null && holder.IsHoldingDeer
            ? moveSpeed * deerCarrySpeedMultiplier
            : moveSpeed * (input != null && input.SprintHeld ? sprintMultiplier : 1f);
        private HuntHappens.Transport.CartDriver cartDriver;
        private float verticalSpeed;
        private Vector3 impulse;
        public Vector3 ExternalVelocity => impulse + Vector3.up * verticalSpeed;
        public void ApplyImpulse(Vector3 value) { if (value.sqrMagnitude > 0) cartDriver?.Release(); impulse = Vector3.ProjectOnPlane(value, Vector3.up); verticalSpeed = value.y; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            cartDriver = GetComponent<HuntHappens.Transport.CartDriver>();
            input = GetComponent<PrototypeInput>();
            look = GetComponent<FirstPersonLook>();
            holder = GetComponent<HuntHappens.Interaction.ItemHolder>();
        }

        private void Update()
        {
            input.ReadFrame();
            if (!input.GameplayBlocked) look.ApplyLook(input.Look);
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (controller.isGrounded && input.JumpPressed && (cartDriver == null || !cartDriver.IsDriving))
                verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);

            verticalSpeed += gravity * Time.deltaTime;
            Vector2 movement = Vector2.ClampMagnitude(input.Move, 1f);
            float speed = CurrentMoveSpeed;
            Vector3 velocity = (transform.right * movement.x + transform.forward * movement.y) * speed;
            velocity.y = verticalSpeed;
            Vector3 displacement = (velocity + impulse) * Time.deltaTime;
            CollisionFlags collision = cartDriver != null && cartDriver.IsDriving ? cartDriver.Move(displacement) : controller.Move(displacement);
            impulse = Vector3.MoveTowards(impulse, Vector3.zero, 14 * Time.deltaTime);
            if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
        }
    }
}
