using UnityEngine;
using System.Collections;

/// <summary>
/// Handles procedural animation for enemies (leg bobbing, punching, breathing, shivering).
/// </summary>
public class EnemyAnimationController : MonoBehaviour
{
    public enum State { Idle, Roam, Chase, Attack, Investigate, Scavenge, Flee }

    [Header("References")]
    [SerializeField] private EnemyLimbController limbController;
    [SerializeField] private Rigidbody2D rb;

    [Header("Walking (Legs)")]
    [SerializeField] private float bobSpeed = 10f;
    [SerializeField] private float bobAmount = 0.1f;

    [Header("Breathing (Idle)")]
    [SerializeField] private float breathSpeed = 2f;
    [SerializeField] private float breathAmount = 0.05f;

    [Header("Investigating (Sway)")]
    [SerializeField] private float swaySpeed = 1.5f;
    [SerializeField] private float swayAngle = 5f;

    [Header("Scared (Shake)")]
    [SerializeField] private float shakeIntensity = 0.05f;
    [SerializeField] private float shakeSpeed = 20f;

    // --- State ---
    private State currentState = State.Idle;
    
    // Limb References
    private Transform leftLegSlot, rightLegSlot;
    private Transform leftArmSlot, rightArmSlot;
    private Transform headSlot;
    private Transform visualsHolder;

    // Original Positions
    private Vector3 leftLegOrigPos, rightLegOrigPos;
    private Vector3 leftArmOrigPos, rightArmOrigPos;
    private Vector3 headOrigPos;
    private Vector3 visualsHolderOrigPos;

    private float walkTimer = 0f;
    private float breathTimer = 0f;
    private float swayTimer = 0f;
    private float currentBobOffset = 0f;
    private bool isPunching = false;

    void Start()
    {
        if (limbController == null) limbController = GetComponent<EnemyLimbController>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        visualsHolder = limbController.GetVisualsHolder();
        if (visualsHolder) visualsHolderOrigPos = visualsHolder.localPosition;

        // Legs
        leftLegSlot = limbController.GetLeftLegSlot();
        rightLegSlot = limbController.GetRightLegSlot();
        if (leftLegSlot) leftLegOrigPos = leftLegSlot.localPosition;
        if (rightLegSlot) rightLegOrigPos = rightLegSlot.localPosition;

        // Arms
        leftArmSlot = limbController.GetLeftArmSlot();
        rightArmSlot = limbController.GetRightArmSlot();
        if (leftArmSlot) leftArmOrigPos = leftArmSlot.localPosition;
        if (rightArmSlot) rightArmOrigPos = rightArmSlot.localPosition;

        // Head
        headSlot = limbController.headSlot;
        if (headSlot) headOrigPos = headSlot.localPosition;
    }

    public void SetState(State newState)
    {
        currentState = newState;
    }

    void Update()
    {
        HandleLegBobbing();
        HandleBodyStateAnimation();
    }

    private void HandleLegBobbing()
    {
        if (rb.linearVelocity.magnitude > 0.1f && limbController.hasLegs)
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

    private void HandleBodyStateAnimation()
    {
        // 1. Reset Visuals Holder (for Rotation/Position overrides)
        if (visualsHolder)
        {
            // Reset position slightly towards original every frame to prevent drift, then apply offsets
            visualsHolder.localPosition = Vector3.Lerp(visualsHolder.localPosition, visualsHolderOrigPos, Time.deltaTime * 5f);
            visualsHolder.localRotation = Quaternion.Lerp(visualsHolder.localRotation, Quaternion.identity, Time.deltaTime * 5f);
        }

        // 2. State Logic
        switch (currentState)
        {
            case State.Idle:
            case State.Roam:
            case State.Scavenge:
                ApplyBreathing();
                break;

            case State.Investigate:
                ApplyBreathing();
                ApplySway();
                break;

            case State.Flee:
                ApplyShake();
                break;
                
            case State.Chase:
            case State.Attack:
                // Tense up? For now, standard breathing or still is fine.
                // Maybe faster breathing.
                breathTimer += Time.deltaTime * breathSpeed * 2f; // Panting
                ApplyBreathingEffect();
                break;
        }
    }

    private void ApplyBreathing()
    {
        breathTimer += Time.deltaTime * breathSpeed;
        ApplyBreathingEffect();
    }

    private void ApplyBreathingEffect()
    {
        float breathOffset = Mathf.Sin(breathTimer) * breathAmount;

        // Move Head
        if (headSlot)
        {
            headSlot.localPosition = headOrigPos + new Vector3(0f, breathOffset, 0f);
        }

        // Move Arms (Shoulders) - Only if not punching
        if (!isPunching)
        {
            if (leftArmSlot) leftArmSlot.localPosition = leftArmOrigPos + new Vector3(0f, breathOffset * 0.5f, 0f);
            if (rightArmSlot) rightArmSlot.localPosition = rightArmOrigPos + new Vector3(0f, breathOffset * 0.5f, 0f);
        }
    }

    private void ApplySway()
    {
        swayTimer += Time.deltaTime * swaySpeed;
        float angle = Mathf.Sin(swayTimer) * swayAngle;
        
        if (visualsHolder)
        {
            visualsHolder.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void ApplyShake()
    {
        if (visualsHolder)
        {
            float x = (Mathf.PerlinNoise(Time.time * shakeSpeed, 0f) - 0.5f) * 2f * shakeIntensity;
            float y = (Mathf.PerlinNoise(0f, Time.time * shakeSpeed) - 0.5f) * 2f * shakeIntensity;
            visualsHolder.localPosition = visualsHolderOrigPos + new Vector3(x, y, 0f);
        }
    }

    public void TriggerPunch(Vector2 targetPos, float duration)
    {
        if (isPunching) return;

        Transform armToUse = null;
        Vector3 origPos = Vector3.zero;

        if (limbController.HasLeftArm()) { armToUse = limbController.GetLeftArmSlot(); origPos = leftArmOrigPos; }
        else if (limbController.HasRightArm()) { armToUse = limbController.GetRightArmSlot(); origPos = rightArmOrigPos; }

        if (armToUse != null)
        {
            Vector3 localTarget = limbController.GetVisualsHolder().InverseTransformPoint(targetPos);
            StartCoroutine(PunchCoroutine(armToUse, origPos, localTarget, duration));
        }
    }

    private IEnumerator PunchCoroutine(Transform arm, Vector3 origPos, Vector3 targetLocalPos, float duration)
    {
        isPunching = true;
        float halfDuration = duration / 2f;
        float timer = 0f;

        while (timer < halfDuration)
        {
            arm.localPosition = Vector3.Lerp(origPos, targetLocalPos, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null;
        }

        timer = 0f;
        while (timer < halfDuration)
        {
            arm.localPosition = Vector3.Lerp(targetLocalPos, origPos, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null;
        }

        arm.localPosition = origPos;
        isPunching = false;
    }
}