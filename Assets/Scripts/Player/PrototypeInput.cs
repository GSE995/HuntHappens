using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HuntHappens.Player
{
    // Actions belong to this component; no generated input code or global asset is needed.
    public sealed class PrototypeInput : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool ThrowPressed { get; private set; }
        public bool SelectTranquilizerPressed { get; private set; }
        public bool SelectInteractionPressed { get; private set; }
        public bool SelectTrapPressed { get; private set; }
        public bool GameplayBlocked { get; private set; }
        public bool MenuClosePressed { get; private set; }
        private int suppressThroughFrame = -1;
        public void SetGameplayBlocked(bool blocked)
        {
            GameplayBlocked = blocked;
            ClearGameplayFrame();
            MenuClosePressed = false;
            suppressThroughFrame = Time.frameCount + 1;
            SetCursor(!blocked && isActiveAndEnabled);
        }
        private void ClearGameplayFrame()
        {
            Move = Look = Vector2.zero;
            JumpPressed = InteractPressed = ThrowPressed = false;
            SelectTranquilizerPressed = SelectInteractionPressed = SelectTrapPressed = false;
        }
#if ENABLE_INPUT_SYSTEM
        private InputAction move;
        private InputAction look;
        private InputAction jump;
        private InputAction releaseCursor;
        private InputAction captureCursor;
        private InputAction interact;
        private InputAction selectTranquilizer;
        private InputAction selectInteraction;
        private InputAction selectTrap;

        private void Awake()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            releaseCursor = new InputAction("Release cursor", InputActionType.Button, "<Keyboard>/escape");
            captureCursor = new InputAction("Capture cursor", InputActionType.Button, "<Mouse>/leftButton");
            interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            selectTranquilizer = new InputAction("Select tranquilizer", InputActionType.Button, "<Keyboard>/1");
            selectInteraction = new InputAction("Select interaction", InputActionType.Button, "<Keyboard>/2");
            selectTrap = new InputAction("Select trap", InputActionType.Button, "<Keyboard>/3");
        }

        private void OnEnable()
        {
            move.Enable(); look.Enable(); jump.Enable();
            releaseCursor.Enable(); captureCursor.Enable();
            interact.Enable();
            selectTranquilizer.Enable(); selectInteraction.Enable();
            selectTrap.Enable();
            SetCursor(true);
        }

        public void ReadFrame()
        {
            MenuClosePressed = false;
            if (GameplayBlocked)
            {
                ClearGameplayFrame();
                MenuClosePressed = Time.frameCount > suppressThroughFrame && (interact.WasPressedThisFrame() || releaseCursor.WasPressedThisFrame());
                SetCursor(false);
                return;
            }
            if (Time.frameCount <= suppressThroughFrame) { ClearGameplayFrame(); return; }
            bool wasCaptured = Cursor.lockState == CursorLockMode.Locked;
            if (releaseCursor.WasPressedThisFrame()) SetCursor(false);
            else if (captureCursor.WasPressedThisFrame()) SetCursor(true);

            bool captured = Cursor.lockState == CursorLockMode.Locked;
            Move = captured ? move.ReadValue<Vector2>() : Vector2.zero;
            Look = captured ? look.ReadValue<Vector2>() : Vector2.zero;
            JumpPressed = captured && jump.WasPressedThisFrame();
            InteractPressed = captured && wasCaptured && interact.WasPressedThisFrame();
            // Clicking back into Game captures the cursor without throwing the held item.
            ThrowPressed = captured && wasCaptured && captureCursor.WasPressedThisFrame();
            SelectTranquilizerPressed = captured && selectTranquilizer.WasPressedThisFrame();
            SelectInteractionPressed = captured && selectInteraction.WasPressedThisFrame();
            SelectTrapPressed = captured && selectTrap.WasPressedThisFrame();
        }

        private void OnDisable()
        {
            move.Disable(); look.Disable(); jump.Disable();
            releaseCursor.Disable(); captureCursor.Disable();
            interact.Disable();
            selectTranquilizer.Disable(); selectInteraction.Disable();
            selectTrap.Disable();
            Move = Look = Vector2.zero;
            JumpPressed = false;
            InteractPressed = ThrowPressed = false;
            SelectTranquilizerPressed = SelectInteractionPressed = false;
            SelectTrapPressed = false;
            SetCursor(false);
        }

        private void OnDestroy()
        {
            move?.Dispose(); look?.Dispose(); jump?.Dispose();
            releaseCursor?.Dispose(); captureCursor?.Dispose();
            interact?.Dispose();
            selectTranquilizer?.Dispose(); selectInteraction?.Dispose();
            selectTrap?.Dispose();
        }
#else
        private void OnEnable()
        {
            Debug.LogError("Hunt Happens requires Input System. Install it in Package Manager and enable it in Player Settings.", this);
        }
        public void ReadFrame() { }
#endif
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SetCursor(false);
        }

        private static void SetCursor(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
