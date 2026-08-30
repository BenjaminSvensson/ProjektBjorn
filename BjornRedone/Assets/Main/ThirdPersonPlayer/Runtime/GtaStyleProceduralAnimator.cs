using UnityEngine;

namespace Bjorn.ThirdPerson
{
    /// <summary>
    /// Animates the primitive humanoid directly. No Animator, clips, avatar, or rig package is required.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class GtaStyleProceduralAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GtaStylePlayerMotor motor;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform pelvis;
        [SerializeField] private Transform torso;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftArmPivot;
        [SerializeField] private Transform rightArmPivot;
        [SerializeField] private Transform leftLegPivot;
        [SerializeField] private Transform rightLegPivot;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;

        [Header("Procedural Motion")]
        [SerializeField, Range(5f, 75f)] private float walkStrideAngle = 34f;
        [SerializeField, Range(5f, 90f)] private float sprintStrideAngle = 54f;
        [SerializeField, Range(0f, 0.2f)] private float walkBobHeight = 0.055f;
        [SerializeField, Range(0f, 30f)] private float sprintLean = 11f;
        [SerializeField, Range(1f, 30f)] private float poseResponse = 12f;

        private Vector3 visualRootBasePosition;
        private Vector3 visualRootBaseScale;
        private Quaternion pelvisBaseRotation;
        private Quaternion torsoBaseRotation;
        private Quaternion headBaseRotation;
        private Quaternion leftArmBaseRotation;
        private Quaternion rightArmBaseRotation;
        private Quaternion leftLegBaseRotation;
        private Quaternion rightLegBaseRotation;
        private Quaternion leftFootBaseRotation;
        private Quaternion rightFootBaseRotation;
        private float locomotionBlend;
        private float stridePhase;
        private float landingImpulse;
        private float previousYaw;
        private bool poseCaptured;

        public float LocomotionBlend => locomotionBlend;
        public GtaStylePlayerMotor Motor => motor;
        public Transform VisualRoot => visualRoot;

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<GtaStylePlayerMotor>();
            }

            CaptureBindPose();
            previousYaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            if (!poseCaptured)
            {
                CaptureBindPose();
            }
        }

        private void Update()
        {
            if (motor == null || visualRoot == null || !poseCaptured)
            {
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float speed01 = motor.NormalizedSpeed;
            float response = 1f - Mathf.Exp(-poseResponse * deltaTime);
            locomotionBlend = Mathf.Lerp(locomotionBlend, speed01, response);
            stridePhase += deltaTime * Mathf.Lerp(3.2f, 12.5f, Mathf.Clamp01(locomotionBlend * 1.25f));

            if (motor.JustLanded)
            {
                landingImpulse = 1f;
            }
            landingImpulse = Mathf.MoveTowards(landingImpulse, 0f, deltaTime * 5.5f);

            float turnRate = Mathf.DeltaAngle(previousYaw, transform.eulerAngles.y) / deltaTime;
            previousYaw = transform.eulerAngles.y;
            float turnLean = Mathf.Clamp(turnRate / 420f, -1f, 1f) * -8f;
            float sprintAmount = motor.IsSprinting ? locomotionBlend : 0f;
            float strideAngle = Mathf.Lerp(walkStrideAngle, sprintStrideAngle, sprintAmount);
            float stride = Mathf.Sin(stridePhase) * strideAngle * locomotionBlend;
            float bounce = Mathf.Abs(Mathf.Cos(stridePhase)) * walkBobHeight * locomotionBlend;
            float idleBreath = Mathf.Sin(Time.time * 1.75f) * 0.008f * (1f - locomotionBlend);
            float forwardLean = sprintAmount * sprintLean;

            ApplyCorePose(bounce, idleBreath, forwardLean, turnLean);

            if (!motor.IsGrounded)
            {
                ApplyAirPose(response);
            }
            else if (motor.PlayerInput != null && motor.PlayerInput.AimHeld)
            {
                ApplyAimPose(stride, response);
            }
            else
            {
                ApplyLocomotionPose(stride, sprintAmount, response);
            }
        }

        private void ApplyCorePose(float bounce, float idleBreath, float forwardLean, float turnLean)
        {
            visualRoot.localPosition = visualRootBasePosition + Vector3.up * (bounce + idleBreath - landingImpulse * 0.055f);
            visualRoot.localScale = Vector3.Scale(visualRootBaseScale,
                new Vector3(1f + landingImpulse * 0.045f, 1f - landingImpulse * 0.075f, 1f + landingImpulse * 0.045f));

            if (pelvis != null)
            {
                pelvis.localRotation = pelvisBaseRotation * Quaternion.Euler(0f, 0f, turnLean * 0.45f);
            }
            if (torso != null)
            {
                torso.localRotation = torsoBaseRotation * Quaternion.Euler(forwardLean, 0f, turnLean);
            }
            if (head != null)
            {
                float headIdle = Mathf.Sin(Time.time * 0.65f) * 2f * (1f - locomotionBlend);
                head.localRotation = headBaseRotation * Quaternion.Euler(-forwardLean * 0.4f, headIdle, -turnLean * 0.7f);
            }
        }

        private void ApplyLocomotionPose(float stride, float sprintAmount, float response)
        {
            SetLocalRotation(leftLegPivot, leftLegBaseRotation * Quaternion.Euler(stride, 0f, 0f), response);
            SetLocalRotation(rightLegPivot, rightLegBaseRotation * Quaternion.Euler(-stride, 0f, 0f), response);

            float armSwing = stride * Mathf.Lerp(0.75f, 0.95f, sprintAmount);
            SetLocalRotation(leftArmPivot, leftArmBaseRotation * Quaternion.Euler(-armSwing, 0f, -3f * locomotionBlend), response);
            SetLocalRotation(rightArmPivot, rightArmBaseRotation * Quaternion.Euler(armSwing, 0f, 3f * locomotionBlend), response);

            SetLocalRotation(leftFoot, leftFootBaseRotation * Quaternion.Euler(-stride * 0.22f, 0f, 0f), response);
            SetLocalRotation(rightFoot, rightFootBaseRotation * Quaternion.Euler(stride * 0.22f, 0f, 0f), response);
        }

        private void ApplyAimPose(float stride, float response)
        {
            SetLocalRotation(leftLegPivot, leftLegBaseRotation * Quaternion.Euler(stride * 0.45f, 0f, 0f), response);
            SetLocalRotation(rightLegPivot, rightLegBaseRotation * Quaternion.Euler(-stride * 0.45f, 0f, 0f), response);
            SetLocalRotation(leftArmPivot, leftArmBaseRotation * Quaternion.Euler(-67f, -8f, -8f), response);
            SetLocalRotation(rightArmPivot, rightArmBaseRotation * Quaternion.Euler(-72f, 8f, 8f), response);
            SetLocalRotation(leftFoot, leftFootBaseRotation, response);
            SetLocalRotation(rightFoot, rightFootBaseRotation, response);
        }

        private void ApplyAirPose(float response)
        {
            float rising = Mathf.Clamp(motor.VerticalVelocity * 0.08f, -1f, 1f);
            SetLocalRotation(leftLegPivot, leftLegBaseRotation * Quaternion.Euler(-12f - rising * 10f, 0f, -5f), response);
            SetLocalRotation(rightLegPivot, rightLegBaseRotation * Quaternion.Euler(24f + rising * 8f, 0f, 5f), response);
            SetLocalRotation(leftArmPivot, leftArmBaseRotation * Quaternion.Euler(25f, 0f, -16f), response);
            SetLocalRotation(rightArmPivot, rightArmBaseRotation * Quaternion.Euler(-20f, 0f, 16f), response);
            SetLocalRotation(leftFoot, leftFootBaseRotation * Quaternion.Euler(-15f, 0f, 0f), response);
            SetLocalRotation(rightFoot, rightFootBaseRotation * Quaternion.Euler(-15f, 0f, 0f), response);
        }

        private static void SetLocalRotation(Transform target, Quaternion desired, float response)
        {
            if (target != null)
            {
                target.localRotation = Quaternion.Slerp(target.localRotation, desired, response);
            }
        }

        private void CaptureBindPose()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRootBasePosition = visualRoot.localPosition;
            visualRootBaseScale = visualRoot.localScale;
            pelvisBaseRotation = GetLocalRotation(pelvis);
            torsoBaseRotation = GetLocalRotation(torso);
            headBaseRotation = GetLocalRotation(head);
            leftArmBaseRotation = GetLocalRotation(leftArmPivot);
            rightArmBaseRotation = GetLocalRotation(rightArmPivot);
            leftLegBaseRotation = GetLocalRotation(leftLegPivot);
            rightLegBaseRotation = GetLocalRotation(rightLegPivot);
            leftFootBaseRotation = GetLocalRotation(leftFoot);
            rightFootBaseRotation = GetLocalRotation(rightFoot);
            poseCaptured = true;
        }

        private static Quaternion GetLocalRotation(Transform target)
        {
            return target != null ? target.localRotation : Quaternion.identity;
        }

        private void OnDisable()
        {
            if (!poseCaptured || visualRoot == null)
            {
                return;
            }

            visualRoot.localPosition = visualRootBasePosition;
            visualRoot.localScale = visualRootBaseScale;
            ResetRotation(pelvis, pelvisBaseRotation);
            ResetRotation(torso, torsoBaseRotation);
            ResetRotation(head, headBaseRotation);
            ResetRotation(leftArmPivot, leftArmBaseRotation);
            ResetRotation(rightArmPivot, rightArmBaseRotation);
            ResetRotation(leftLegPivot, leftLegBaseRotation);
            ResetRotation(rightLegPivot, rightLegBaseRotation);
            ResetRotation(leftFoot, leftFootBaseRotation);
            ResetRotation(rightFoot, rightFootBaseRotation);
        }

        private static void ResetRotation(Transform target, Quaternion rotation)
        {
            if (target != null)
            {
                target.localRotation = rotation;
            }
        }

        public void Configure(
            GtaStylePlayerMotor playerMotor,
            Transform visuals,
            Transform pelvisTransform,
            Transform torsoTransform,
            Transform headTransform,
            Transform leftArm,
            Transform rightArm,
            Transform leftLeg,
            Transform rightLeg,
            Transform leftFootTransform,
            Transform rightFootTransform)
        {
            motor = playerMotor;
            visualRoot = visuals;
            pelvis = pelvisTransform;
            torso = torsoTransform;
            head = headTransform;
            leftArmPivot = leftArm;
            rightArmPivot = rightArm;
            leftLegPivot = leftLeg;
            rightLegPivot = rightLeg;
            leftFoot = leftFootTransform;
            rightFoot = rightFootTransform;
            poseCaptured = false;
            CaptureBindPose();
        }
    }
}
