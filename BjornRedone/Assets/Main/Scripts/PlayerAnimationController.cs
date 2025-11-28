using UnityEngine;
using UnityEngine.InputSystem; // We need this for mouse input
using System.Collections; // For coroutines

/// <summary>
/// This script handles all procedural animations for the player,
/// like leg bobbing, arm aiming, and sprite flipping.
/// It should be placed on the root "Player" GameObject.
/// </summary>
public class PlayerAnimationController : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLimbController limbController;

    [Header("Leg Bobbing")]
    [SerializeField] private float bobSpeed = 10f;
    [SerializeField] private float bobAmount = 0.1f;

    // --- NEW ---
    [Header("Arm Aiming")]
    [SerializeField] private float armReachDistance = 0.3f;

    // --- Private Variables ---
    private Camera cam;
    private Transform visualsHolder;
    private Transform leftArmSlot, rightArmSlot, leftLegSlot, rightLegSlot;

    // --- FIX ---
    // Store the original positions of the leg slots
    private Vector3 leftLegOrigPos, rightLegOrigPos;
    // --- NEW ---
    // Store the original positions of the arm slots
    private Vector3 leftArmOrigPos, rightArmOrigPos;

    private float walkTimer = 0f;
    private float currentBobOffset = 0f;
    private bool isFacingRight = true;
    private bool isPunching = false; // --- NEW ---

    void Start()
    {
        cam = Camera.main;

        // Get references from other scripts
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (limbController == null)
            limbController = GetComponent<PlayerLimbController>();
            
        if (limbController != null)
        {
            // Get all the transforms we need to animate
            visualsHolder = limbController.GetVisualsHolder();
            leftArmSlot = limbController.GetLeftArmSlot();
            rightArmSlot = limbController.GetRightArmSlot();
            leftLegSlot = limbController.GetLeftLegSlot();
            rightLegSlot = limbController.GetRightLegSlot();

            // --- FIX ---
            // Store the original local positions
            if (leftLegSlot) leftLegOrigPos = leftLegSlot.localPosition;
            if (rightLegSlot) rightLegOrigPos = rightLegSlot.localPosition;

            // --- NEW ---
            // Store the original arm local positions
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
        // Failsafe if references are missing
        if (limbController == null || playerMovement == null || cam == null || visualsHolder == null)
            return;

        // --- MODIFIED: Don't aim while punching ---
        if (!isPunching)
        {
            HandleArmAimingAndFlipping();
        }
        HandleLegBobbing();
    }

    /// <summary>
    /// Aims the arms at the mouse and flips the player visual.
    /// </summary>
    private void HandleArmAimingAndFlipping()
    {
        // Get mouse position in world space
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

        // --- 1. Flipping Logic ---
        // Check if the mouse is to the right or left of the player
        bool mouseIsRight = (mouseWorldPos.x > transform.position.x);
        if (mouseIsRight != isFacingRight)
        {
            // Flip the entire visual holder
            isFacingRight = mouseIsRight;
            visualsHolder.localScale = new Vector3(isFacingRight ? 1 : -1, 1, 1);
        }

        // --- 2. Arm Aiming Logic ---
        // This is where we'll add the "punching" check later
        // if (isPunching) return; // This check is now in Update()

        // --- UPDATED ---
        AimSlot(leftArmSlot, leftArmOrigPos, mouseWorldPos);
        AimSlot(rightArmSlot, rightArmOrigPos, mouseWorldPos);
    }

    /// <summary>
    /// Helper function to point a specific transform (like an arm) at a world target.
    /// </summary>
    private void AimSlot(Transform slot, Vector3 originalLocalPos, Vector2 targetWorldPos)
    {
        if (slot == null) return;

        // --- NEW OFFSET LOGIC ---
        // Get the mouse position in the local space of the visualsHolder
        Vector2 localTargetPos = visualsHolder.InverseTransformPoint(targetWorldPos);

        // Get the direction from the arm's origin to the local mouse pos
        Vector2 direction = localTargetPos - (Vector2)originalLocalPos;

        // Clamp the magnitude of this direction to create the "reach"
        Vector2 offset = Vector2.ClampMagnitude(direction, armReachDistance);

        // Apply the new offset position
        slot.localPosition = originalLocalPos + (Vector3)offset;
        // --- END NEW OFFSET LOGIC ---


        // --- ROTATION LOGIC (Modified to use the new offset position) ---
        // Get direction from the arm's *new* world position to the mouse
        Vector2 worldDirection = targetWorldPos - (Vector2)slot.position;
        // Calculate the angle
        float angle = Mathf.Atan2(worldDirection.y, worldDirection.x) * Mathf.Rad2Deg;

        // --- THIS IS THE FIX ---
        // We subtract 90 degrees because the arm sprite's "bottom" (where the hand is)
        // should point at the cursor, not its "right" side.
        angle -= 90f;
        // --- END FIX ---

        // Apply the rotation
        // We add 180 degrees if the player is flipped, so the arm aims correctly
        if (!isFacingRight) angle += 180f;

        slot.rotation = Quaternion.Euler(0, 0, angle);
    }

    /// <summary>
    /// Creates a bobbing motion on the legs when walking.
    /// </summary>
    private void HandleLegBobbing()
    {
        // Get movement input from the PlayerMovement script
        Vector2 moveInput = playerMovement.GetMoveInput();

        if (moveInput.magnitude > 0.1f)
        {
            // Player is moving
            walkTimer += Time.deltaTime * bobSpeed;
            // Use Sin wave for a smooth up/down bob
            currentBobOffset = Mathf.Sin(walkTimer) * bobAmount;
        }
        else
        {
            // Player is standing still
            walkTimer = 0f; // Reset timer
            // Smoothly lerp the bob back to 0
            currentBobOffset = Mathf.Lerp(currentBobOffset, 0f, Time.deltaTime * 10f);
        }

        // --- FIX ---
        // Apply the bob offset TO the original Y position, keeping X and Z
        if (leftLegSlot)
            leftLegSlot.localPosition = new Vector3(leftLegOrigPos.x, leftLegOrigPos.y + currentBobOffset, leftLegOrigPos.z);
        if (rightLegSlot)
            rightLegSlot.localPosition = new Vector3(rightLegOrigPos.x, rightLegOrigPos.y - currentBobOffset, rightLegOrigPos.z);
    }

    // --- NEW PUBLIC METHOD ---
    /// <summary>
    /// Triggers a simple punch animation on the specified arm.
    /// </summary>
    // --- MODIFIED: Receives targetWorldPos (which is the hitPosition) ---
    public void TriggerPunch(Transform armToPunch, float punchDuration, Vector2 targetWorldPos)
    {
        // Find the arm's original local position
        Vector3 origPos = (armToPunch == leftArmSlot) ? leftArmOrigPos : rightArmOrigPos;

        // --- FIX ---
        // Convert the WORLD target position to the LOCAL space of the visualsHolder
        Vector3 targetLocalPos = visualsHolder.InverseTransformPoint(targetWorldPos);
        // --- END FIX ---

        // Pass the LOCAL target position to the coroutine
        StartCoroutine(PunchCoroutine(armToPunch, origPos, targetLocalPos, punchDuration));
    }

    // --- MODIFIED: Receives targetLocalPos ---
    private IEnumerator PunchCoroutine(Transform arm, Vector3 origPos, Vector3 targetLocalPos, float duration)
    {
        isPunching = true;
        
        float halfDuration = duration / 2f;
        float timer = 0f;

        // --- Jab Out ---
        while (timer < halfDuration)
        {
            float t = timer / halfDuration; // Easing (0 to 1)
            // Lerp from original local pos to target local pos
            arm.localPosition = Vector3.Lerp(origPos, targetLocalPos, t);
            timer += Time.deltaTime;
            yield return null;
        }

        timer = 0f;
        
        // --- Retract ---
        while (timer < halfDuration)
        {
            float t = timer / halfDuration; // Easing (0 to 1)
            arm.localPosition = Vector3.Lerp(targetLocalPos, origPos, t);
            timer += Time.deltaTime;
            yield return null;
        }

        // Restore
        arm.localPosition = origPos;
        isPunching = false;
    }
}