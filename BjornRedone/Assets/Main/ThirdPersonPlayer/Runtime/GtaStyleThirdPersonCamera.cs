using UnityEngine;

namespace Bjorn.ThirdPerson
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class GtaStyleThirdPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private GtaStylePlayerInput playerInput;
        [SerializeField] private GtaStylePlayerMotor playerMotor;

        [Header("Orbit")]
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);
        [SerializeField, Min(0.5f)] private float followDistance = 4.6f;
        [SerializeField, Min(0.5f)] private float aimDistance = 3.25f;
        [SerializeField] private float shoulderOffset = 0.5f;
        [SerializeField] private float aimShoulderOffset = 0.85f;
        [SerializeField] private float lookAhead = 0.4f;
        [SerializeField, Range(-80f, 80f)] private float minimumPitch = -25f;
        [SerializeField, Range(-80f, 80f)] private float maximumPitch = 62f;
        [SerializeField] private float startingPitch = 12f;

        [Header("Camera Feel")]
        [SerializeField, Min(0f)] private float autoRecenterDelay = 1.1f;
        [SerializeField, Range(0.05f, 2f)] private float autoRecenterTime = 0.65f;
        [SerializeField, Range(30f, 100f)] private float normalFieldOfView = 65f;
        [SerializeField, Range(30f, 100f)] private float sprintFieldOfView = 72f;
        [SerializeField, Range(30f, 100f)] private float aimFieldOfView = 54f;
        [SerializeField, Min(0.01f)] private float fieldOfViewResponse = 8f;

        [Header("Obstruction")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField, Range(0.05f, 0.5f)] private float collisionRadius = 0.22f;
        [SerializeField, Range(0f, 0.5f)] private float collisionPadding = 0.12f;
        [SerializeField, Min(0.05f)] private float minimumCollisionDistance = 0.35f;
        [SerializeField, Min(0.01f)] private float collisionReleaseSmoothTime = 0.12f;

        private readonly RaycastHit[] collisionHits = new RaycastHit[16];
        private Camera controlledCamera;
        private float yaw;
        private float pitch;
        private float yawVelocity;
        private float currentDistance;
        private float distanceVelocity;
        private float lastManualLookTime;
        private float shoulderSide = 1f;

        public float CurrentYaw => yaw;
        public float CurrentPitch => pitch;
        public Transform Target => target;
        public GtaStylePlayerInput PlayerInput => playerInput;
        public Camera ControlledCamera => controlledCamera;

        private void Awake()
        {
            controlledCamera = GetComponent<Camera>();
            ResolveReferences();
            SnapBehindTarget();
            currentDistance = followDistance;
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (currentDistance <= 0f)
            {
                currentDistance = followDistance;
            }
        }

        private void LateUpdate()
        {
            if (target == null || playerInput == null)
            {
                ResolveReferences();
                if (target == null || playerInput == null)
                {
                    return;
                }
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            UpdateOrbit(deltaTime);
            UpdateShoulder();
            UpdatePosition(deltaTime);
            UpdateFieldOfView(deltaTime);
        }

        private void UpdateOrbit(float deltaTime)
        {
            Vector2 look = playerInput.LookDelta;
            if (look.sqrMagnitude > 0.0001f)
            {
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, minimumPitch, maximumPitch);
                lastManualLookTime = Time.unscaledTime;
            }
            else if (!playerInput.AimHeld && playerInput.Move.sqrMagnitude > 0.08f
                && Time.unscaledTime - lastManualLookTime >= autoRecenterDelay)
            {
                float targetYaw = target.eulerAngles.y;
                if (playerMotor != null && playerMotor.PlanarVelocity.sqrMagnitude > 0.1f)
                {
                    Vector3 velocity = playerMotor.PlanarVelocity;
                    targetYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
                }

                yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, autoRecenterTime, Mathf.Infinity, deltaTime);
            }

            yaw = Mathf.Repeat(yaw, 360f);
        }

        private void UpdateShoulder()
        {
            if (playerInput.ShoulderSwapPressed)
            {
                shoulderSide *= -1f;
            }
        }

        private void UpdatePosition(float deltaTime)
        {
            bool aiming = playerInput.AimHeld;
            float requestedDistance = aiming ? aimDistance : followDistance;
            float requestedShoulder = (aiming ? aimShoulderOffset : shoulderOffset) * shoulderSide;
            Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + pivotOffset;
            Vector3 desiredOffset = orbitRotation * new Vector3(requestedShoulder, 0f, -requestedDistance);
            float obstructionDistance = FindObstructionDistance(pivot, desiredOffset, requestedDistance);

            if (obstructionDistance < currentDistance)
            {
                currentDistance = obstructionDistance;
                distanceVelocity = 0f;
            }
            else
            {
                currentDistance = Mathf.SmoothDamp(currentDistance, obstructionDistance, ref distanceVelocity,
                    collisionReleaseSmoothTime, Mathf.Infinity, deltaTime);
            }

            float distanceScale = requestedDistance > 0.001f ? currentDistance / requestedDistance : 1f;
            Vector3 finalOffset = orbitRotation * new Vector3(requestedShoulder * distanceScale, 0f, -currentDistance);
            Vector3 cameraPosition = pivot + finalOffset;
            Vector3 forwardLook = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * lookAhead;
            Vector3 lookTarget = pivot + forwardLook;

            transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(lookTarget - cameraPosition, Vector3.up));
        }

        private float FindObstructionDistance(Vector3 pivot, Vector3 desiredOffset, float requestedDistance)
        {
            float castDistance = desiredOffset.magnitude;
            if (castDistance <= 0.001f)
            {
                return requestedDistance;
            }

            Vector3 castDirection = desiredOffset / castDistance;
            int hitCount = Physics.SphereCastNonAlloc(pivot, collisionRadius, castDirection, collisionHits,
                castDistance, collisionMask, QueryTriggerInteraction.Ignore);
            float nearestDistance = castDistance;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = collisionHits[i];
                if (hit.collider == null || (target != null && hit.collider.transform.IsChildOf(target)))
                {
                    continue;
                }

                nearestDistance = Mathf.Min(nearestDistance, hit.distance);
            }

            if (nearestDistance >= castDistance)
            {
                return requestedDistance;
            }

            float ratio = requestedDistance / castDistance;
            return Mathf.Clamp((nearestDistance - collisionPadding) * ratio, minimumCollisionDistance, requestedDistance);
        }

        private void UpdateFieldOfView(float deltaTime)
        {
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }

            float targetFieldOfView = playerInput.AimHeld
                ? aimFieldOfView
                : (playerMotor != null && playerMotor.IsSprinting ? sprintFieldOfView : normalFieldOfView);
            float blend = 1f - Mathf.Exp(-fieldOfViewResponse * deltaTime);
            controlledCamera.fieldOfView = Mathf.Lerp(controlledCamera.fieldOfView, targetFieldOfView, blend);
        }

        private void ResolveReferences()
        {
            if (target == null)
            {
                GtaStylePlayerMotor motor = GetComponentInParent<GtaStylePlayerMotor>();
                if (motor != null)
                {
                    target = motor.transform;
                    playerMotor = motor;
                }
            }

            if (playerMotor == null && target != null)
            {
                playerMotor = target.GetComponent<GtaStylePlayerMotor>();
            }

            if (playerInput == null && target != null)
            {
                playerInput = target.GetComponent<GtaStylePlayerInput>();
            }
        }

        private void SnapBehindTarget()
        {
            if (target != null)
            {
                yaw = target.eulerAngles.y;
            }

            pitch = Mathf.Clamp(startingPitch, minimumPitch, maximumPitch);
            lastManualLookTime = Time.unscaledTime;
        }

        public void Configure(Transform followTarget, GtaStylePlayerInput input, GtaStylePlayerMotor motor)
        {
            target = followTarget;
            playerInput = input;
            playerMotor = motor;
            controlledCamera = GetComponent<Camera>();
            SnapBehindTarget();
            currentDistance = followDistance;
        }

        private void OnValidate()
        {
            maximumPitch = Mathf.Max(maximumPitch, minimumPitch + 1f);
            followDistance = Mathf.Max(0.5f, followDistance);
            aimDistance = Mathf.Clamp(aimDistance, 0.5f, followDistance);
            minimumCollisionDistance = Mathf.Min(minimumCollisionDistance, aimDistance);
        }
    }
}
