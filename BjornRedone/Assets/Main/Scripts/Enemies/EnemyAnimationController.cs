using UnityEngine;
using System.Collections;

/// <summary>
/// Handles procedural animation for enemies (leg bobbing, punching).
/// </summary>
public class EnemyAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyLimbController limbController;
    [SerializeField] private Rigidbody2D rb;

    [Header("Leg Bobbing")]
    [SerializeField] private float bobSpeed = 10f;
    [SerializeField] private float bobAmount = 0.1f;

    // State
    private Transform leftLegSlot, rightLegSlot;
    private Vector3 leftLegOrigPos, rightLegOrigPos;
    private Vector3 leftArmOrigPos, rightArmOrigPos;
    
    private float walkTimer = 0f;
    private float currentBobOffset = 0f;
    private bool isPunching = false;

    void Start()
    {
        if (limbController == null) limbController = GetComponent<EnemyLimbController>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        // Cache Transforms
        leftLegSlot = limbController.GetLeftLegSlot();
        rightLegSlot = limbController.GetRightLegSlot();

        // Cache Original Positions
        if (leftLegSlot) leftLegOrigPos = leftLegSlot.localPosition;
        if (rightLegSlot) rightLegOrigPos = rightLegSlot.localPosition;

        Transform leftArm = limbController.GetLeftArmSlot();
        Transform rightArm = limbController.GetRightArmSlot();
        
        if (leftArm) leftArmOrigPos = leftArm.localPosition;
        if (rightArm) rightArmOrigPos = rightArm.localPosition;
    }

    void Update()
    {
        HandleLegBobbing();
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

    /// <summary>
    /// This is the method that was missing!
    /// </summary>
    public void TriggerPunch(Vector2 targetPos, float duration)
    {
        if (isPunching) return;

        // Decide which arm to use
        Transform armToUse = null;
        Vector3 origPos = Vector3.zero;

        if (limbController.HasLeftArm())
        {
            armToUse = limbController.GetLeftArmSlot();
            origPos = leftArmOrigPos;
        }
        else if (limbController.HasRightArm())
        {
            armToUse = limbController.GetRightArmSlot();
            origPos = rightArmOrigPos;
        }

        if (armToUse != null)
        {
            // Convert world target to local space relative to the visuals holder
            Vector3 localTarget = limbController.GetVisualsHolder().InverseTransformPoint(targetPos);
            StartCoroutine(PunchCoroutine(armToUse, origPos, localTarget, duration));
        }
    }

    private IEnumerator PunchCoroutine(Transform arm, Vector3 origPos, Vector3 targetLocalPos, float duration)
    {
        isPunching = true;
        float halfDuration = duration / 2f;
        float timer = 0f;

        // Jab out
        while (timer < halfDuration)
        {
            arm.localPosition = Vector3.Lerp(origPos, targetLocalPos, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null;
        }

        timer = 0f;
        // Retract
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