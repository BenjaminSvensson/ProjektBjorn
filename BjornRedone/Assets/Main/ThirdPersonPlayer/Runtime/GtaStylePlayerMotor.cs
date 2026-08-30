using UnityEngine;

namespace Bjorn.ThirdPerson
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(GtaStylePlayerInput))]
    public sealed class GtaStylePlayerMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GtaStylePlayerInput playerInput;
        [SerializeField] private Transform movementCamera;

        [Header("GTA-style Locomotion")]
        [SerializeField, Min(0f)] private float jogSpeed = 4.8f;
        [SerializeField, Min(0f)] private float sprintSpeed = 7.4f;
        [SerializeField, Min(0f)] private float aimSpeed = 3.1f;
        [SerializeField, Min(0.01f)] private float acceleration = 22f;
        [SerializeField, Min(0.01f)] private float deceleration = 30f;
        [SerializeField, Range(0.01f, 0.5f)] private float turnSmoothTime = 0.075f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.55f;

        [Header("Jump & Gravity")]
        [SerializeField] private float gravity = -25f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.15f;
        [SerializeField] private float groundedGravity = -3f;

        private CharacterController characterController;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private float turnVelocity;
        private bool wasGrounded;

        public Vector3 PlanarVelocity => planarVelocity;
        public float VerticalVelocity => verticalVelocity;
        public float NormalizedSpeed => sprintSpeed > 0.001f ? Mathf.Clamp01(planarVelocity.magnitude / sprintSpeed) : 0f;
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool JustLanded { get; private set; }
        public Vector2 MoveInput => playerInput != null ? playerInput.Move : Vector2.zero;
        public GtaStylePlayerInput PlayerInput => playerInput;
        public Transform MovementCamera => movementCamera;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (playerInput == null)
            {
                playerInput = GetComponent<GtaStylePlayerInput>();
            }
        }

        private void Start()
        {
            if (movementCamera == null && Camera.main != null)
            {
                movementCamera = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (characterController == null || playerInput == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            UpdateGroundingAndGravity(deltaTime);
            UpdatePlanarMovement(deltaTime);
            UpdateFacing(deltaTime);

            characterController.Move((planarVelocity + Vector3.up * verticalVelocity) * deltaTime);

            bool groundedAfterMove = characterController.isGrounded;
            if (groundedAfterMove && verticalVelocity < groundedGravity)
            {
                verticalVelocity = groundedGravity;
            }

            JustLanded = !wasGrounded && groundedAfterMove;
            IsGrounded = groundedAfterMove;
            wasGrounded = groundedAfterMove;
        }

        private void UpdateGroundingAndGravity(float deltaTime)
        {
            IsGrounded = characterController.isGrounded;
            JustLanded = false;

            if (IsGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundedGravity;
            }

            if (IsGrounded && playerInput.JumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                IsGrounded = false;
            }
            else
            {
                verticalVelocity += gravity * deltaTime;
            }
        }

        private void UpdatePlanarMovement(float deltaTime)
        {
            Vector2 moveInput = Vector2.ClampMagnitude(playerInput.Move, 1f);
            Vector3 cameraForward = movementCamera != null ? movementCamera.forward : transform.forward;
            Vector3 cameraRight = movementCamera != null ? movementCamera.right : transform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward = cameraForward.sqrMagnitude > 0.001f ? cameraForward.normalized : transform.forward;
            cameraRight = cameraRight.sqrMagnitude > 0.001f ? cameraRight.normalized : transform.right;

            Vector3 desiredDirection = cameraForward * moveInput.y + cameraRight * moveInput.x;
            if (desiredDirection.sqrMagnitude > 1f)
            {
                desiredDirection.Normalize();
            }

            IsSprinting = playerInput.SprintHeld && !playerInput.AimHeld && moveInput.y > -0.25f && moveInput.sqrMagnitude > 0.1f;
            float topSpeed = playerInput.AimHeld ? aimSpeed : (IsSprinting ? sprintSpeed : jogSpeed);
            Vector3 desiredVelocity = desiredDirection * (topSpeed * moveInput.magnitude);
            float changeRate = desiredVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration;
            if (!IsGrounded)
            {
                changeRate *= airControl;
            }

            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, changeRate * deltaTime);
        }

        private void UpdateFacing(float deltaTime)
        {
            Vector3 faceDirection;
            if (playerInput.AimHeld && movementCamera != null)
            {
                faceDirection = movementCamera.forward;
                faceDirection.y = 0f;
            }
            else
            {
                faceDirection = planarVelocity.sqrMagnitude > 0.04f ? planarVelocity : Vector3.zero;
            }

            if (faceDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            float targetYaw = Mathf.Atan2(faceDirection.x, faceDirection.z) * Mathf.Rad2Deg;
            float smoothTime = playerInput.AimHeld ? turnSmoothTime * 0.65f : turnSmoothTime;
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref turnVelocity, smoothTime, Mathf.Infinity, deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void Configure(GtaStylePlayerInput input, Transform cameraTransform)
        {
            playerInput = input;
            movementCamera = cameraTransform;
        }

        public void Teleport(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            bool wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(worldPosition, worldRotation);
            planarVelocity = Vector3.zero;
            verticalVelocity = groundedGravity;

            if (characterController != null)
            {
                characterController.enabled = wasEnabled;
            }
        }

        private void OnValidate()
        {
            sprintSpeed = Mathf.Max(sprintSpeed, jogSpeed);
            acceleration = Mathf.Max(0.01f, acceleration);
            deceleration = Mathf.Max(0.01f, deceleration);
            gravity = Mathf.Min(-0.01f, gravity);
            jumpHeight = Mathf.Max(0f, jumpHeight);
        }
    }
}
