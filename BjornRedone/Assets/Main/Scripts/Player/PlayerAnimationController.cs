using UnityEngine;
using UnityEngine.InputSystem; 
using System.Collections; 

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLimbController limbController;
    [SerializeField] private WeaponSystem weaponSystem;

    [Header("Leg Bobbing")]
    [SerializeField] private float bobSpeed = 10f;
    [SerializeField] private float bobAmount = 0.1f;

    [Header("Arm Aiming")]
    [SerializeField] private float armReachDistance = 0.3f;

    private Camera cam;
    private Transform visualsHolder;
    private Transform leftArmSlot, rightArmSlot, leftLegSlot, rightLegSlot;

    private Vector3 leftLegOrigPos, rightLegOrigPos;
    private Vector3 leftArmOrigPos, rightArmOrigPos;

    private float walkTimer = 0f;
    private float currentBobOffset = 0f;
    private bool isFacingRight = true;
    private bool isAttacking = false; 

    void Start()
    {
        cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();

        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (limbController == null) limbController = GetComponent<PlayerLimbController>();
        if (weaponSystem == null) weaponSystem = GetComponent<WeaponSystem>();
            
        if (limbController != null)
        {
            visualsHolder = limbController.GetVisualsHolder();
            leftArmSlot = limbController.GetLeftArmSlot();
            rightArmSlot = limbController.GetRightArmSlot();
            leftLegSlot = limbController.GetLeftLegSlot();
            rightLegSlot = limbController.GetRightLegSlot();

            if (leftLegSlot) leftLegOrigPos = leftLegSlot.localPosition;
            if (rightLegSlot) rightLegOrigPos = rightLegSlot.localPosition; 

            if (leftArmSlot) leftArmOrigPos = leftArmSlot.localPosition;
            if (rightArmSlot) rightArmOrigPos = rightArmSlot.localPosition;
        }
    }

    void Update()
    {
        if (limbController == null || playerMovement == null || cam == null || visualsHolder == null) return;

        if (!isAttacking) HandleArmAimingAndFlipping();
        HandleLegBobbing();
    }

    private void HandleArmAimingAndFlipping()
    {
        if (Mouse.current == null) return;
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        
        bool mouseIsRight = (mouseWorldPos.x > transform.position.x);
        if (mouseIsRight != isFacingRight)
        {
            isFacingRight = mouseIsRight;
            visualsHolder.localScale = new Vector3(isFacingRight ? 1 : -1, 1, 1);
        }

        AimSlot(leftArmSlot, leftArmOrigPos, mouseWorldPos);
        AimSlot(rightArmSlot, rightArmOrigPos, mouseWorldPos);
    }

    private void AimSlot(Transform slot, Vector3 originalLocalPos, Vector2 targetWorldPos)
    {
        if (slot == null) return;
        Vector2 localTargetPos = visualsHolder.InverseTransformPoint(targetWorldPos);
        Vector2 offset = Vector2.ClampMagnitude(localTargetPos - (Vector2)originalLocalPos, armReachDistance);
        slot.localPosition = originalLocalPos + (Vector3)offset;
        slot.up = -(targetWorldPos - (Vector2)slot.position).normalized;
    }

    private void HandleLegBobbing()
    {
        // Don't bob legs if attacking (kicking)
        if (isAttacking) return; 

        Vector2 moveInput = playerMovement.GetMoveInput();
        if (moveInput.magnitude > 0.1f)
        {
            walkTimer += Time.deltaTime * bobSpeed;
            currentBobOffset = Mathf.Sin(walkTimer) * bobAmount;
        }
        else
        {
            walkTimer = 0f;
            currentBobOffset = Mathf.Lerp(currentBobOffset, 0f, Time.deltaTime * 10f);
        }

        if (leftLegSlot) leftLegSlot.localPosition = new Vector3(leftLegOrigPos.x, leftLegOrigPos.y + currentBobOffset, leftLegOrigPos.z);
        if (rightLegSlot) rightLegSlot.localPosition = new Vector3(rightLegOrigPos.x, rightLegOrigPos.y - currentBobOffset, rightLegOrigPos.z);
    }

    public void TriggerPunch(Transform armToPunch, float punchDuration, Vector2 targetWorldPos)
    {
        Vector3 origPos = (armToPunch == leftArmSlot) ? leftArmOrigPos : rightArmOrigPos;
        Vector3 targetLocalPos = visualsHolder.InverseTransformPoint(targetWorldPos);
        StartCoroutine(LimbMoveCoroutine(armToPunch, origPos, targetLocalPos, punchDuration));
    }

    public void TriggerSwing(Transform armToSwing, float swingDuration, Vector2 targetWorldPos, float arcAngle)
    {
        Vector2 dirToTarget = (targetWorldPos - (Vector2)armToSwing.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, -dirToTarget);
        StartCoroutine(SwingCoroutine(armToSwing, targetRotation, swingDuration, arcAngle));
    }

    // +++ NEW: Trigger Kick +++
    public void TriggerKick(bool useLeftLeg, float duration, Vector2 targetWorldPos, float reach)
    {
        Transform leg = useLeftLeg ? leftLegSlot : rightLegSlot;
        Vector3 origPos = useLeftLeg ? leftLegOrigPos : rightLegOrigPos;

        if (leg == null) return;

        // Calculate a target position "Reach" distance away towards the mouse
        Vector2 dir = (targetWorldPos - (Vector2)leg.position).normalized;
        Vector3 targetLocalPos = origPos + (Vector3)(dir * reach);

        // We assume the kick visuals are controlled by the same type of coroutine as the punch
        StartCoroutine(LimbMoveCoroutine(leg, origPos, targetLocalPos, duration));
    }

    // +++ REFACTORED: Renamed from PunchCoroutine to LimbMoveCoroutine to reuse for Legs +++
    private IEnumerator LimbMoveCoroutine(Transform limb, Vector3 origPos, Vector3 targetLocalPos, float duration)
    {
        isAttacking = true;
        
        float halfDuration = duration / 2f;
        if (halfDuration < Time.deltaTime) halfDuration = Time.deltaTime; 

        float timer = 0f;
        while (timer < halfDuration)
        {
            float t = timer / halfDuration;
            limb.localPosition = Vector3.Lerp(origPos, targetLocalPos, t);
            timer += Time.deltaTime;
            yield return null;
        }

        timer = 0f;
        while (timer < halfDuration)
        {
            float t = timer / halfDuration; 
            limb.localPosition = Vector3.Lerp(targetLocalPos, origPos, t);
            timer += Time.deltaTime;
            yield return null;
        }

        limb.localPosition = origPos;
        isAttacking = false;
    }

    private IEnumerator SwingCoroutine(Transform arm, Quaternion centerRotation, float duration, float arcAngle)
    {
        isAttacking = true;
        Quaternion startRot = centerRotation * Quaternion.Euler(0, 0, -arcAngle / 2f);
        Quaternion endRot = centerRotation * Quaternion.Euler(0, 0, arcAngle / 2f);

        float timer = 0f;
        while (timer < duration)
        {
            float t = Mathf.SmoothStep(0f, 1f, timer / duration);
            arm.rotation = Quaternion.Slerp(startRot, endRot, t);
            timer += Time.deltaTime;
            yield return null;
        }
        isAttacking = false;
    }
}