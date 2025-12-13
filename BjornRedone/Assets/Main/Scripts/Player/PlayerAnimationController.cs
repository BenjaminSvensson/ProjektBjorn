using UnityEngine;
using UnityEngine.InputSystem; 
using System.Collections; 

/// <summary>
/// This script handles all procedural animations for the player,
/// like leg bobbing, arm aiming, and sprite flipping.
/// </summary>
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

    // --- Private Variables ---
    private Camera cam;
    private Transform visualsHolder;
    private Transform leftArmSlot, rightArmSlot, leftLegSlot, rightLegSlot;

    // Store the original positions
    private Vector3 leftLegOrigPos, rightLegOrigPos;
    private Vector3 leftArmOrigPos, rightArmOrigPos;

    private float walkTimer = 0f;
    private float currentBobOffset = 0f;
    private bool isFacingRight = true;
    private bool isAttacking = false; // Renamed from isPunching to cover Swings too

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
        else
        {
            Debug.LogError("PlayerAnimationController could not find PlayerLimbController!");
        }
    }

    void Update()
    {
        if (limbController == null || playerMovement == null || cam == null || visualsHolder == null)
            return;

        // Don't aim while attacking (Punching OR Swinging)
        if (!isAttacking)
        {
            HandleArmAimingAndFlipping();
        }
        HandleLegBobbing();
    }

    private void HandleArmAimingAndFlipping()
    {
        if (Mouse.current == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        if (float.IsNaN(mouseScreenPos.x) || float.IsNaN(mouseScreenPos.y)) return;

        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

        // --- 1. Flipping Logic ---
        bool mouseIsRight = (mouseWorldPos.x > transform.position.x);
        if (mouseIsRight != isFacingRight)
        {
            isFacingRight = mouseIsRight;
            visualsHolder.localScale = new Vector3(isFacingRight ? 1 : -1, 1, 1);
        }

        // --- 2. Arm Aiming Logic ---
        // Always aim arms at the mouse, even if holding a weapon.
        AimSlot(leftArmSlot, leftArmOrigPos, mouseWorldPos);
        AimSlot(rightArmSlot, rightArmOrigPos, mouseWorldPos);
    }

    private void AimSlot(Transform slot, Vector3 originalLocalPos, Vector2 targetWorldPos)
    {
        if (slot == null) return;

        Vector2 localTargetPos = visualsHolder.InverseTransformPoint(targetWorldPos);
        Vector2 localDirectionToTarget = localTargetPos - (Vector2)originalLocalPos;
        Vector2 offset = Vector2.ClampMagnitude(localDirectionToTarget, armReachDistance);
        slot.localPosition = originalLocalPos + (Vector3)offset;

        Vector2 worldDirToMouse = (targetWorldPos - (Vector2)slot.position).normalized;
        Vector2 lookDirection = -worldDirToMouse;
        slot.up = lookDirection;
    }

    private void HandleLegBobbing()
    {
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

        if (leftLegSlot)
            leftLegSlot.localPosition = new Vector3(leftLegOrigPos.x, leftLegOrigPos.y + currentBobOffset, leftLegOrigPos.z);
        
        if (rightLegSlot)
            rightLegSlot.localPosition = new Vector3(rightLegOrigPos.x, rightLegOrigPos.y - currentBobOffset, rightLegOrigPos.z);
    }

    // --- ANIMATION TRIGGERS ---

    public void TriggerPunch(Transform armToPunch, float punchDuration, Vector2 targetWorldPos)
    {
        Vector3 origPos = (armToPunch == leftArmSlot) ? leftArmOrigPos : rightArmOrigPos;
        Vector3 targetLocalPos = visualsHolder.InverseTransformPoint(targetWorldPos);
        StartCoroutine(PunchCoroutine(armToPunch, origPos, targetLocalPos, punchDuration));
    }

    // --- NEW: Swing Trigger ---
    public void TriggerSwing(Transform armToSwing, float swingDuration, Vector2 targetWorldPos, float arcAngle)
    {
        // Calculate the base rotation needed to point at the target
        Vector2 dirToTarget = (targetWorldPos - (Vector2)armToSwing.position).normalized;
        // The arm's 'up' vector points opposite to the hand, so we use -dir
        Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, -dirToTarget);
        
        StartCoroutine(SwingCoroutine(armToSwing, targetRotation, swingDuration, arcAngle));
    }

    private IEnumerator PunchCoroutine(Transform arm, Vector3 origPos, Vector3 targetLocalPos, float duration)
    {
        isAttacking = true;
        
        float halfDuration = duration / 2f;
        if (halfDuration < Time.deltaTime) halfDuration = Time.deltaTime; 

        float timer = 0f;
        while (timer < halfDuration)
        {
            float t = timer / halfDuration;
            arm.localPosition = Vector3.Lerp(origPos, targetLocalPos, t);
            timer += Time.deltaTime;
            yield return null;
        }

        timer = 0f;
        while (timer < halfDuration)
        {
            float t = timer / halfDuration; 
            arm.localPosition = Vector3.Lerp(targetLocalPos, origPos, t);
            timer += Time.deltaTime;
            yield return null;
        }

        arm.localPosition = origPos;
        isAttacking = false;
    }

    // --- NEW: Swing Coroutine ---
    private IEnumerator SwingCoroutine(Transform arm, Quaternion centerRotation, float duration, float arcAngle)
    {
        isAttacking = true;

        // Start angle relative to center (e.g. -45)
        Quaternion startRot = centerRotation * Quaternion.Euler(0, 0, -arcAngle / 2f);
        // End angle relative to center (e.g. +45)
        Quaternion endRot = centerRotation * Quaternion.Euler(0, 0, arcAngle / 2f);

        float timer = 0f;
        while (timer < duration)
        {
            // Spherical Lerp (Slerp) for smooth rotation
            float t = timer / duration;
            // Easing function for a "slash" feel (starts slow, fast middle, slow end)
            t = Mathf.SmoothStep(0f, 1f, t);
            
            arm.rotation = Quaternion.Slerp(startRot, endRot, t);
            
            timer += Time.deltaTime;
            yield return null;
        }

        isAttacking = false;
        // Arms will snap back to aim at mouse in Update() immediately after
    }
}