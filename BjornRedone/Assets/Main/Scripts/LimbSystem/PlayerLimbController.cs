using UnityEngine;
using System.Collections.Generic;

public class PlayerLimbController : MonoBehaviour
{
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
    // We no longer need the limbDropPrefab!
    [Tooltip("The chance a limb will be 'maintained' (re-usable) when lost")]
    [Range(0f, 1f)]
    public float maintainLimbChance = 0.3f;

    // --- Current Limb References ---
    // THIS IS THE FIX: Changed from "Limb" to "WorldLimb"
    private WorldLimb currentHead;
    private WorldLimb currentLeftArm;
    private WorldLimb currentRightArm;
    private WorldLimb currentLeftLeg;
    private WorldLimb currentRightLeg;

    // --- Component References ---
    private PlayerMovement playerMovement;
    // private PlayerAttack playerAttack; // Add this when you create an attack script

    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement == null)
        {
            Debug.LogError("PlayerLimbController needs a PlayerMovement script on the same GameObject.");
        }
        
        // Find or add attack script
        // playerAttack = GetComponent<PlayerAttack>(); 

        // Spawn starting limbs
        if(startingHead) AttachToSlot(startingHead, LimbSlot.Head, false);
        if(startingArm) AttachToSlot(startingArm, LimbSlot.LeftArm, true);
        if(startingArm) AttachToSlot(startingArm, LimbSlot.RightArm, false);
        if(startingLeg) AttachToSlot(startingLeg, LimbSlot.LeftLeg, true);
        if(startingLeg) AttachToSlot(startingLeg, LimbSlot.RightLeg, false);

        UpdatePlayerStats();
    }

    /// <summary>
    /// Attaches a limb prefab to a specific slot.
    /// </summary>
    void AttachToSlot(LimbData limbData, LimbSlot slot, bool flipSprite)
    {
        Transform parentSlot = GetSlotTransform(slot);
        if (parentSlot == null) return;

        // Instantiate the limb's VISUAL PREFAB
        GameObject limbObj = Instantiate(limbData.visualPrefab, parentSlot.position, parentSlot.rotation, parentSlot);
        limbObj.name = limbData.name + " (Attached)";

        // Flip sprite if it's a left limb
        SpriteRenderer sr = limbObj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && flipSprite)
        {
            sr.flipX = true;
        }

        // Get the WorldLimb component from the prefab
        WorldLimb limbComponent = limbObj.GetComponent<WorldLimb>(); // This was already correct
        if (limbComponent == null)
        {
            Debug.LogError($"Limb prefab '{limbData.name}' is missing the WorldLimb.cs script!", limbData);
            Destroy(limbObj);
            return;
        }

        // Initialize it in its "Attached" state
        limbComponent.InitializeAttached(limbData);

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

        // Check Arms
        if (limbToAttach.limbType == LimbType.Arm)
        {
            // Prioritize Right Arm slot if both are missing
            if (currentRightArm == null)
            {
                AttachToSlot(limbToAttach, LimbSlot.RightArm, false);
            }
            else if (currentLeftArm == null)
            {
                AttachToSlot(limbToAttach, LimbSlot.LeftArm, true);
            }
        }
        // Check Legs
        else if (limbToAttach.limbType == LimbType.Leg)
        {
            // Prioritize Right Leg slot if both are missing
            if (currentRightLeg == null)
            {
                AttachToSlot(limbToAttach, LimbSlot.RightLeg, false);
            }
            else if (currentLeftLeg == null)
            {
                AttachToSlot(limbToAttach, LimbSlot.LeftLeg, true);
            }
        }
        // Note: Head cannot be picked up and attached, only lost.
    }

    /// <summary>
    /// Call this when the player takes damage.
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        // In a real game, you'd apply damage to torso/head health first.
        // For this demo, we'll just have a chance to lose a limb.
        
        // Create a list of limbs that can be lost
        List<LimbSlot> detachableLimbs = new List<LimbSlot>();
        if (currentLeftArm) detachableLimbs.Add(LimbSlot.LeftArm);
        if (currentRightArm) detachableLimbs.Add(LimbSlot.RightArm);
        if (currentLeftLeg) detachableLimbs.Add(LimbSlot.LeftLeg);
        if (currentRightLeg) detachableLimbs.Add(LimbSlot.RightLeg);

        if (detachableLimbs.Count > 0)
        {
            // Lose a random limb
            LimbSlot slotToLose = detachableLimbs[Random.Range(0, detachableLimbs.Count)];
            DetachLimb(slotToLose);
        }
        else if (currentHead)
        {
            // No other limbs left, lose the head!
            Debug.Log("Losing head!");
            DetachLimb(LimbSlot.Head);
        }
        else
        {
            // Only torso is left, take torso damage
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
        // We now detach the WorldLimb component
        WorldLimb limbToDetach = null; // This was already correct
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
            // --- NEW LOGIC ---
            // 1. Calculate if the limb is maintained
            bool isMaintained = Random.value <= maintainLimbChance;

            // 2. Spawn a NEW instance of the same limb prefab
            // We get the prefab from the limb's own data
            GameObject thrownLimbObj = Instantiate(limbToDetach.GetLimbData().visualPrefab, transform.position, Quaternion.identity);
                
            // 3. Give it a random throw direction
            Vector2 throwDirection = new Vector2(Random.Range(-1f, 1f), Random.Range(0.5f, 1f)).normalized; // Throw slightly upwards
                
            // 4. Get its WorldLimb component and initialize its "Thrown" state
            WorldLimb worldLimb = thrownLimbObj.GetComponent<WorldLimb>(); // This was already correct
            if (worldLimb)
            {
                // Tell the new limb to fly!
                worldLimb.InitializeThrow(limbToDetach.GetLimbData(), isMaintained, throwDirection);
            }
            else
            {
                Debug.LogError($"The limb prefab {limbToDetach.GetLimbData().name} is missing the WorldLimb.cs script!", thrownLimbObj);
            }
            
            // 5. Destroy the OLD limb object that was attached to the player
            Destroy(limbToDetach.gameObject);
        }

        // Check for death condition
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
        float totalMoveSpeed = baseMoveSpeed;
        float totalAttackDamage = baseAttackDamage;
        int legCount = 0;

        // Accumulate stats from all limbs
        // We also need to get the data via GetLimbData() here
        if (currentHead) totalMoveSpeed += currentHead.GetLimbData().moveSpeedBonus;
        if (currentLeftArm)
        {
            totalMoveSpeed += currentLeftArm.GetLimbData().moveSpeedBonus;
            totalAttackDamage += currentLeftArm.GetLimbData().attackDamageBonus;
        }
        if (currentRightArm)
        {
            totalMoveSpeed += currentRightArm.GetLimbData().moveSpeedBonus;
            totalAttackDamage += currentRightArm.GetLimbData().attackDamageBonus;
        }
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

        // --- Apply Gameplay Logic Based on Missing Limbs ---

        // Leg logic
        if (legCount == 1)
        {
            // Slower with one leg
            totalMoveSpeed *= 0.6f;
        }
        else if (legCount == 0)
        {
            // Crawling speed
            totalMoveSpeed *= 0.3f; 
        }

        // Arm logic
        // (e.g., if(currentLeftArm == null && currentRightArm == null) playerAttack.DisableAttacks(); )
        // (e.g., if(currentRightArm == null) playerAttack.DisablePrimaryAttack(); )

        // --- Send stats to other components ---
        if (playerMovement)
        {
            playerMovement.SetMoveSpeed(totalMoveSpeed);
        }
        
        // if (playerAttack)
        // {
        //    playerAttack.SetAttackDamage(totalAttackDamage);
        // }

        Debug.Log($"Stats Updated: Speed={totalMoveSpeed}, Damage={totalAttackDamage}, Legs={legCount}");
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
        // Disable controls, play death animation, restart level, etc.
        this.enabled = false;
        if (playerMovement) playerMovement.enabled = false;
    }
}