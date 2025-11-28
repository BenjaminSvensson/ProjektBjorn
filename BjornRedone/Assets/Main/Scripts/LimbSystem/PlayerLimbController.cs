using UnityEngine;
using System.Collections; // We need this for Coroutines
using System.Collections.Generic;

public class PlayerLimbController : MonoBehaviour
{
    [Header("Visuals (Assign in Inspector)")]
    [Tooltip("The parent GameObject that holds all limb slots. This is what will shake.")]
    [SerializeField] private Transform visualsHolder;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.1f;

    [Header("Limb Slots (Assign in Inspector)")]
    public Transform headSlot;
    public Transform leftArmSlot;
    public Transform rightArmSlot;
    public Transform leftLegSlot;
    public Transform rightLegSlot;

    [Header("Base Stats")]
    public float baseMoveSpeed = 5f;
    public float baseAttackDamage = 1f;
    // Torso acts as the root and could have its own health
    public float torsoHealth = 100f; 

    [Header("Starting Limbs (Prefabs)")]
    [Tooltip("Player's starting head. Losing this = death.")]
    public LimbData startingHead;
    public LimbData startingArm;
    public LimbData startingLeg;

    [Header("Limb Physics")]
    [Tooltip("The chance a limb will be 'maintained' (re-usable) when lost")]
    [Range(0f, 1f)]
    public float maintainLimbChance = 0.3f;

    // --- Current Limb References ---
    private WorldLimb currentHead;
    private WorldLimb currentLeftArm;
    private WorldLimb currentRightArm;
    private WorldLimb currentLeftLeg;
    private WorldLimb currentRightLeg;

    // --- Component References ---
    private PlayerMovement playerMovement;
    private Rigidbody2D rb; 
    // private PlayerAttack playerAttack; // This is now in PlayerAttackController
    private Vector3 visualsHolderOriginalPos; 

    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody2D>(); 

        if (playerMovement == null)
        {
            Debug.LogError("PlayerLimbController needs a PlayerMovement script on the same GameObject.");
        }
        if (rb == null) 
        {
            Debug.LogError("PlayerLimbController needs a Rigidbody2D on the same GameObject.");
        }
        
        // Find or add attack script
        // playerAttack = GetComponent<PlayerAttack>(); 

        if (visualsHolder != null)
        {
            visualsHolderOriginalPos = visualsHolder.localPosition;
        }
        else
        {
            Debug.LogError("VisualsHolder is not assigned in PlayerLimbController!");
        }

        // Spawn starting limbs
        if(startingHead) AttachToSlot(startingHead, LimbSlot.Head, false, false);
        if(startingArm) AttachToSlot(startingArm, LimbSlot.LeftArm, true, false);
        if(startingArm) AttachToSlot(startingArm, LimbSlot.RightArm, false, false);
        if(startingLeg) AttachToSlot(startingLeg, LimbSlot.LeftLeg, true, false);
        if(startingLeg) AttachToSlot(startingLeg, LimbSlot.RightLeg, false, false);

        UpdatePlayerStats();
    }

    /// <summary>
    /// Attaches a limb prefab to a specific slot.
    /// </summary>
    void AttachToSlot(LimbData limbData, LimbSlot slot, bool flipSprite, bool isPickup)
    {
        Transform parentSlot = GetSlotTransform(slot);
        if (parentSlot == null) return;

        GameObject limbObj = Instantiate(limbData.visualPrefab, parentSlot.position, parentSlot.rotation, parentSlot);
        limbObj.name = limbData.name + " (Attached)";

        SpriteRenderer sr = limbObj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && flipSprite)
        {
            sr.flipX = true;
        }

        WorldLimb limbComponent = limbObj.GetComponent<WorldLimb>();
        if (limbComponent == null)
        {
            Debug.LogError($"Limb prefab '{limbData.name}' is missing the WorldLimb.cs script!", limbData);
            Destroy(limbObj);
            return;
        }

        limbComponent.InitializeAttached(limbData, isPickup);

        // Store the reference
        switch (slot)
        {
            case LimbSlot.Head:     currentHead = limbComponent; break;
            case LimbSlot.LeftArm:  currentLeftArm = limbComponent; break;
            case LimbSlot.RightArm: currentRightArm = limbComponent; break;
            case LimbSlot.LeftLeg:  currentLeftLeg = limbComponent; break;
            case LimbSlot.RightLeg: currentRightLeg = limbComponent; break;
        }

        UpdatePlayerStats();
    }

    /// <summary>
    // Called by PlayerCollision when it touches a LimbPickup.
    /// </summary>
    public void TryAttachLimb(LimbData limbToAttach)
    {
        if (limbToAttach == null) return;

        if (limbToAttach.limbType == LimbType.Arm)
        {
            if (currentRightArm == null)
            {
                AttachToSlot(limbToAttach, LimbSlot.RightArm, false, true);
            }
            else if (currentLeftArm == null)
            {
                AttachToSlot(limbToAttach, LimbSlot.LeftArm, true, true);
            }
        }
        else if (limbToAttach.limbType == LimbType.Leg)
        {
            if (currentRightLeg == null)
            {
                AttachToSlot(limbToAttach, LimbSlot.RightLeg, false, true);
            }
            else if (currentLeftLeg == null)
            {
                AttachToSlot(limbToAttach, LimbSlot.LeftLeg, true, true);
            }
        }
    }

    /// <summary>
    /// Call this when the player takes damage.
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        if (visualsHolder != null)
        {
            StopAllCoroutines(); 
            StartCoroutine(ShakeVisuals());
        }
        
        List<LimbSlot> detachableLimbs = new List<LimbSlot>();
        if (currentLeftArm) detachableLimbs.Add(LimbSlot.LeftArm);
        if (currentRightArm) detachableLimbs.Add(LimbSlot.RightArm);
        if (currentLeftLeg) detachableLimbs.Add(LimbSlot.LeftLeg);
        if (currentRightLeg) detachableLimbs.Add(LimbSlot.RightLeg);

        if (detachableLimbs.Count > 0)
        {
            LimbSlot slotToLose = detachableLimbs[Random.Range(0, detachableLimbs.Count)];
            DetachLimb(slotToLose);
        }
        else if (currentHead)
        {
            Debug.Log("Losing head!");
            DetachLimb(LimbSlot.Head);
        }
        else
        {
            torsoHealth -= damageAmount;
            if (torsoHealth <= 0)
            {
                Die();
            }
        }
    }

    /// <summary>
    /// Detaches a limb from a slot and spawns a pickup.
    /// </summary>
    void DetachLimb(LimbSlot slot)
    {
        WorldLimb limbToDetach = null;
        switch (slot)
        {
            case LimbSlot.Head:
                limbToDetach = currentHead;
                currentHead = null;
                break;
            case LimbSlot.LeftArm:
                limbToDetach = currentLeftArm;
                currentLeftArm = null;
                break;
            case LimbSlot.RightArm:
                limbToDetach = currentRightArm;
                currentRightArm = null;
                break;
            case LimbSlot.LeftLeg:
                limbToDetach = currentLeftLeg;
                currentLeftLeg = null;
                break;
            case LimbSlot.RightLeg:
                limbToDetach = currentRightLeg;
                currentRightLeg = null;
                break;
        }

        if (limbToDetach != null)
        {
            bool isMaintained = Random.value <= maintainLimbChance;
            GameObject thrownLimbObj = Instantiate(limbToDetach.GetLimbData().visualPrefab, transform.position, Quaternion.identity);
            Vector2 throwDirection = new Vector2(Random.Range(-1f, 1f), Random.Range(0.5f, 1f)).normalized; 
            WorldLimb worldLimb = thrownLimbObj.GetComponent<WorldLimb>();
            if (worldLimb)
            {
                worldLimb.InitializeThrow(limbToDetach.GetLimbData(), isMaintained, throwDirection);
            }
            
            Destroy(limbToDetach.gameObject);
        }

        if (currentHead == null)
        {
            Die();
        }
        
        UpdatePlayerStats();
    }

    /// <summary>
    /// Recalculates all player stats based on current limbs and applies them.
    /// </summary>
    void UpdatePlayerStats()
    {
        // --- NEW LOGIC: Enforce limb-specific stats ---
        
        float totalMoveSpeed = 0f;
        int legCount = 0;
        
        if (currentLeftLeg)
        {
            totalMoveSpeed += currentLeftLeg.GetLimbData().moveSpeedBonus;
            legCount++;
        }
        if (currentRightLeg)
        {
            totalMoveSpeed += currentRightLeg.GetLimbData().moveSpeedBonus;
            legCount++;
        }

        if (legCount > 0)
        {
            totalMoveSpeed += baseMoveSpeed; 
            if (legCount == 1)
            {
                totalMoveSpeed *= 0.6f; 
            }
        }
        
        if (playerMovement)
        {
            playerMovement.SetMoveSpeed(totalMoveSpeed);
        }

        Debug.Log($"Stats Updated: Speed={totalMoveSpeed}, Legs={legCount}");
    }

    Transform GetSlotTransform(LimbSlot slot)
    {
        switch (slot)
        {
            case LimbSlot.Head:     return headSlot;
            case LimbSlot.LeftArm:  return leftArmSlot;
            case LimbSlot.RightArm: return rightArmSlot;
            case LimbSlot.LeftLeg:  return leftLegSlot;
            case LimbSlot.RightLeg: return rightLegSlot;
            default:                return null;
        }
    }

    void Die()
    {
        Debug.Log("Player has died! (Lost their head or torso)");
        this.enabled = false;
        if (playerMovement) playerMovement.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        StopAllCoroutines();
        if(visualsHolder) visualsHolder.localPosition = visualsHolderOriginalPos;
    }

    // --- NEW PUBLIC GETTERS for PlayerAttackController ---
    
    /// <summary>
    /// Checks if the player has at least one arm to attack with.
    /// </summary>
    public bool CanAttack()
    {
        return currentLeftArm != null || currentRightArm != null;
    }

    /// <summary>
    /// Gets the LimbData for the specified arm.
    /// </summary>
    public LimbData GetArmData(bool isLeftArm)
    {
        if (isLeftArm)
        {
            return (currentLeftArm != null) ? currentLeftArm.GetLimbData() : null;
        }
        else
        {
            return (currentRightArm != null) ? currentRightArm.GetLimbData() : null;
        }
    }

    // --- END NEW GETTERS ---

    // --- NEW PUBLIC GETTERS ---
    public Transform GetVisualsHolder() { return visualsHolder; }
    public Transform GetLeftArmSlot() { return leftArmSlot; }
    public Transform GetRightArmSlot() { return rightArmSlot; }
    public Transform GetLeftLegSlot() { return leftLegSlot; }
    public Transform GetRightLegSlot() { return rightLegSlot; }
    // --- END NEW GETTERS ---


    /// <summary>
    /// Shakes the visualsHolder transform for a short duration.
    /// </summary>
    private IEnumerator ShakeVisuals()
    {
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            visualsHolder.localPosition = new Vector3(
                visualsHolderOriginalPos.x + x, 
                visualsHolderOriginalPos.y + y, 
                visualsHolderOriginalPos.z);

            elapsed += Time.deltaTime;
            
            yield return null; 
        }

        visualsHolder.localPosition = visualsHolderOriginalPos;
    }
}