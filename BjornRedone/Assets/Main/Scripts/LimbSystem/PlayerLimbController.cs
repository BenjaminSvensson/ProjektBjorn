using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class PlayerLimbController : MonoBehaviour
{
    // ... (all variables at the top are the same) ...
    [Header("Visuals (Assign in Inspector)")]
    [Tooltip("The parent GameObject that holds all limb slots. This is what will shake.")]
    [SerializeField] private Transform visualsHolder;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.1f;

    [Header("Damage Feedback")]
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private Color flashColor = Color.red;

    [Header("Sorting Orders (Relative to Body)")]
    [SerializeField] private int headOrder = 10;
    [SerializeField] private int leftArmOrder = 5;
    [SerializeField] private int rightArmOrder = -5;
    [SerializeField] private int legOrder = -10;

    [Header("Limb Slots (Assign in Inspector)")]
    public Transform headSlot;
    public Transform leftArmSlot;
    public Transform rightArmSlot;
    public Transform leftLegSlot;
    public Transform rightLegSlot;

    [Header("Base Stats")]
    public float baseMoveSpeed = 5f;
    public float baseAttackDamage = 1f;
    public float torsoHealth = 100f; 

    [Header("Starting Limbs (Prefabs)")]
    public LimbData startingHead;
    public LimbData startingArm;
    public LimbData startingLeg;

    [Header("Limb Physics")]
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
    private Vector3 visualsHolderOriginalPos; 
    private bool isShaking = false;
    private bool canCrawl = false;
    private AudioSource audioSource;
    private List<SpriteRenderer> currentRenderers = new List<SpriteRenderer>();
    private Coroutine flashCoroutine;

    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody2D>(); 
        audioSource = GetComponent<AudioSource>();

        if (playerMovement == null)
        {
            Debug.LogError("PlayerLimbController needs a PlayerMovement script on the same GameObject.");
        }
        if (rb == null) 
        {
            Debug.LogError("PlayerLimbController needs a Rigidbody2D on the same GameObject.");
        }
        
        if (visualsHolder != null)
        {
            visualsHolderOriginalPos = visualsHolder.localPosition;
        }
        else
        {
            Debug.LogError("VisualsHolder is not assigned in PlayerLimbController!");
        }

        // --- MODIFICATION ---
        // Spawn starting limbs, but tell AttachToSlot NOT to update stats yet.
        if(startingHead) AttachToSlot(startingHead, LimbSlot.Head, false, false, false);
        if(startingArm) AttachToSlot(startingArm, LimbSlot.LeftArm, true, false, false);
        if(startingArm) AttachToSlot(startingArm, LimbSlot.RightArm, false, false, false);
        if(startingLeg) AttachToSlot(startingLeg, LimbSlot.LeftLeg, true, false, false);
        if(startingLeg) AttachToSlot(startingLeg, LimbSlot.RightLeg, false, false, false);

        // Now, update stats ONCE, after all limbs are attached.
        UpdatePlayerStats();
        // --- END MODIFICATION ---
    }

    /// <summary>
    /// Attaches a limb prefab to a specific slot.
    /// </summary>
    // --- MODIFIED: Added 'updateStats' parameter ---
    void AttachToSlot(LimbData limbData, LimbSlot slot, bool flipSprite, bool isDamaged, bool updateStats = true)
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

        limbComponent.InitializeAttached(limbData, isDamaged);

        int sortingOrder = 0;
        switch (slot)
        {
            case LimbSlot.Head:     
                currentHead = limbComponent;
                sortingOrder = headOrder;
                break;
            case LimbSlot.LeftArm:  
                currentLeftArm = limbComponent;
                sortingOrder = leftArmOrder;
                break;
            case LimbSlot.RightArm: 
                currentRightArm = limbComponent;
                sortingOrder = rightArmOrder;
                break;
            case LimbSlot.LeftLeg:  
                currentLeftLeg = limbComponent;
                sortingOrder = legOrder;
                break;
            case LimbSlot.RightLeg: 
                currentRightLeg = limbComponent;
                sortingOrder = legOrder;
                break;
        }

        SpriteRenderer[] srs = limbObj.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr_renderer in srs)
        {
            sr_renderer.sortingOrder = sortingOrder;
        }

        // --- MODIFICATION ---
        // Only update stats if allowed (defaults to true for pickups)
        if (updateStats)
        {
            UpdatePlayerStats();
        }
        // --- END MODIFICATION ---
    }

    /// <summary>
    // Called when picking up a limb.
    /// </summary>
    public bool TryAttachLimb(LimbData limbToAttach, bool isDamaged)
    {
        if (limbToAttach == null) return false;

        // Note: This calls AttachToSlot with the default updateStats = true,
        // so it will correctly update stats when picking up a limb.
        if (limbToAttach.limbType == LimbType.Arm)
        {
            if (currentRightArm == null)
            {
                AttachToSlot(limbToAttach, LimbSlot.RightArm, false, isDamaged);
                return true; 
            }
            else if (currentLeftArm == null)
            {
                AttachToSlot(limbToAttach, LimbSlot.LeftArm, true, isDamaged);
                return true; 
            }
        }
        else if (limbToAttach.limbType == LimbType.Leg)
        {
            if (currentRightLeg == null)
            {
                AttachToSlot(limbToAttach, LimbSlot.RightLeg, false, isDamaged);
                return true; 
            }
            else if (currentLeftLeg == null)
            {
                AttachToSlot(limbToAttach, LimbSlot.LeftLeg, true, isDamaged);
                return true; 
            }
        }
        return false;
    }

    /// <summary>
    /// Call this when the player takes damage.
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        if (audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        flashCoroutine = StartCoroutine(FlashDamageCoroutine());

        if (visualsHolder != null && !isShaking)
        {
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
        float totalMoveSpeed = 0f;
        int legCount = 0;
        int armCount = 0; 
        
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
        
        if (currentLeftArm) armCount++;
        if (currentRightArm) armCount++;

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

        // Crawl State Logic
        canCrawl = (legCount == 0 && armCount > 0);

        Debug.Log($"Stats Updated: Speed={totalMoveSpeed}, Legs={legCount}, Arms={armCount}, CanCrawl={canCrawl}");

        // --- Helplessness Check ---
        // (This is now safe to run because it's only called after all limbs are attached)
        if (legCount == 0 && armCount == 0)
        {
            if (currentHead != null)
            {
                Debug.Log("Player is helpless (0 arms, 0 legs). Detaching head.");
                DetachLimb(LimbSlot.Head); 
            }
        }
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

// ... (rest of the script is unchanged: Getters, ShakeVisuals, FlashDamageCoroutine) ...

    // --- PUBLIC GETTERS for PlayerAttackController ---
    
    public bool CanAttack()
    {
        return currentLeftArm != null || currentRightArm != null;
    }

    public bool CanCrawl()
    {
        return canCrawl;
    }

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

    // --- PUBLIC GETTERS for PlayerAnimationController ---
    public Transform GetVisualsHolder() { return visualsHolder; }
    public Transform GetLeftArmSlot() { return leftArmSlot; }
    public Transform GetRightArmSlot() { return rightArmSlot; }
    public Transform GetLeftLegSlot() { return leftLegSlot; }
    public Transform GetRightLegSlot() { return rightLegSlot; }


    /// <summary>
    /// Shakes the visualsHolder transform for a short duration.
    /// </summary>
    private IEnumerator ShakeVisuals()
    {
        isShaking = true;

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

        isShaking = false;
    }

    /// <summary>
    /// Flashes all player sprites (body and limbs) to the flashColor.
    /// </summary>
    private IEnumerator FlashDamageCoroutine()
    {
        // 1. Find all renderers
        currentRenderers.Clear();
        if (visualsHolder != null)
        {
            visualsHolder.GetComponentsInChildren<SpriteRenderer>(currentRenderers);
        }

        // 2. Set to flash color
        foreach (var renderer in currentRenderers)
        {
            if (renderer != null)
            {
                renderer.color = flashColor;
            }
        }

        // 3. Wait
        yield return new WaitForSeconds(flashDuration);

        // 4. Reset to white
        foreach (var renderer in currentRenderers)
        {
            if (renderer != null)
            {
                renderer.color = Color.white;
            }
        }
        
        flashCoroutine = null; // Mark as finished
    }
}