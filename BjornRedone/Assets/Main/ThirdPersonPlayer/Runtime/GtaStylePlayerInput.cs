using UnityEngine;
using UnityEngine.InputSystem;

namespace Bjorn.ThirdPerson
{
    /// <summary>
    /// Self-contained input reader for the 3D player. It deliberately does not modify or
    /// enable the project's existing 2D action asset.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class GtaStylePlayerInput : MonoBehaviour
    {
        [Header("Mouse & Gamepad Look")]
        [SerializeField, Min(0.001f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Min(1f)] private float gamepadLookSpeed = 165f;
        [SerializeField, Range(0f, 0.95f)] private float gamepadDeadZone = 0.16f;

        [Header("Cursor")]
        [SerializeField] private bool lockCursorOnStart = true;
        [SerializeField] private bool clickToRecaptureCursor = true;

        private bool simulationMode;
        private Vector2 simulatedMove;
        private Vector2 simulatedLookDelta;
        private bool simulatedSprint;
        private bool simulatedAim;
        private bool queuedSimulatedJump;
        private bool queuedSimulatedShoulderSwap;
        private bool queuedSimulatedInteract;
        private bool gameplayBlocked;

        public Vector2 Move { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool AimHeld { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool ShoulderSwapPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool IsUsingGamepad { get; private set; }
        public bool GameplayBlocked => gameplayBlocked;

        private void OnEnable()
        {
            if (lockCursorOnStart && Application.isFocused)
            {
                CaptureCursor();
            }
        }

        private void OnDisable()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            JumpPressed = false;
            ShoulderSwapPressed = false;
            InteractPressed = false;

            if (simulationMode)
            {
                Move = Vector2.ClampMagnitude(simulatedMove, 1f);
                LookDelta = simulatedLookDelta;
                SprintHeld = simulatedSprint;
                AimHeld = simulatedAim;
                JumpPressed = queuedSimulatedJump;
                ShoulderSwapPressed = queuedSimulatedShoulderSwap;
                InteractPressed = queuedSimulatedInteract;
                queuedSimulatedJump = false;
                queuedSimulatedShoulderSwap = false;
                queuedSimulatedInteract = false;
                return;
            }

            if (gameplayBlocked)
            {
                Move = Vector2.zero;
                LookDelta = Vector2.zero;
                SprintHeld = false;
                AimHeld = false;
                IsUsingGamepad = false;
                ReleaseCursor();
                return;
            }

            ReadLiveInput();
            HandleCursorState();
        }

        private void ReadLiveInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;

            Vector2 keyboardMove = Vector2.zero;
            if (keyboard != null)
            {
                keyboardMove.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                keyboardMove.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
                keyboardMove = Vector2.ClampMagnitude(keyboardMove, 1f);
            }

            Vector2 gamepadMove = gamepad != null ? ApplyDeadZone(gamepad.leftStick.ReadValue()) : Vector2.zero;
            IsUsingGamepad = gamepadMove.sqrMagnitude > keyboardMove.sqrMagnitude + 0.001f;
            Move = IsUsingGamepad ? gamepadMove : keyboardMove;

            Vector2 mouseLook = mouse != null && Cursor.lockState == CursorLockMode.Locked
                ? mouse.delta.ReadValue() * mouseSensitivity
                : Vector2.zero;
            Vector2 gamepadLook = gamepad != null
                ? ApplyDeadZone(gamepad.rightStick.ReadValue()) * (gamepadLookSpeed * Time.unscaledDeltaTime)
                : Vector2.zero;
            IsUsingGamepad = gamepadLook.sqrMagnitude > mouseLook.sqrMagnitude + 0.001f || IsUsingGamepad;
            LookDelta = gamepadLook.sqrMagnitude > mouseLook.sqrMagnitude ? gamepadLook : mouseLook;

            SprintHeld = (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
                || (gamepad != null && gamepad.leftStickButton.isPressed);
            AimHeld = (mouse != null && mouse.rightButton.isPressed)
                || (gamepad != null && gamepad.leftTrigger.ReadValue() > 0.35f);
            JumpPressed = (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            ShoulderSwapPressed = (keyboard != null && keyboard.qKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.leftShoulder.wasPressedThisFrame);
            InteractPressed = (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.buttonWest.wasPressedThisFrame);
        }

        private void HandleCursorState()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (clickToRecaptureCursor && mouse != null && mouse.leftButton.wasPressedThisFrame
                && Cursor.lockState != CursorLockMode.Locked)
            {
                CaptureCursor();
            }
        }

        private Vector2 ApplyDeadZone(Vector2 value)
        {
            float magnitude = value.magnitude;
            if (magnitude <= gamepadDeadZone)
            {
                return Vector2.zero;
            }

            float scaledMagnitude = Mathf.InverseLerp(gamepadDeadZone, 1f, magnitude);
            return value.normalized * scaledMagnitude;
        }

        private static void CaptureCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void ReleaseCursor()
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
            }
            Cursor.visible = true;
        }

        public void SetSimulationMode(bool enabled)
        {
            simulationMode = enabled;
            simulatedMove = Vector2.zero;
            simulatedLookDelta = Vector2.zero;
            simulatedSprint = false;
            simulatedAim = false;
            queuedSimulatedJump = false;
            queuedSimulatedShoulderSwap = false;
            queuedSimulatedInteract = false;
        }

        public void SetSimulatedState(Vector2 move, Vector2 lookDelta, bool sprint, bool aim)
        {
            simulatedMove = move;
            simulatedLookDelta = lookDelta;
            simulatedSprint = sprint;
            simulatedAim = aim;
        }

        public void QueueSimulatedJump()
        {
            queuedSimulatedJump = true;
        }

        public void QueueSimulatedShoulderSwap()
        {
            queuedSimulatedShoulderSwap = true;
        }

        public void QueueSimulatedInteract()
        {
            queuedSimulatedInteract = true;
        }

        public void SetGameplayBlocked(bool blocked)
        {
            gameplayBlocked = blocked;
            if (blocked)
            {
                Move = Vector2.zero;
                LookDelta = Vector2.zero;
                SprintHeld = false;
                AimHeld = false;
                ReleaseCursor();
            }
            else if (lockCursorOnStart && Application.isFocused)
            {
                CaptureCursor();
            }
        }
    }
}
