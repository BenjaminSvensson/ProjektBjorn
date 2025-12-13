using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class PlayerLimbController : MonoBehaviour
{
    [Header("Visuals (Assign in Inspector)")]
    [Tooltip("The parent GameObject that holds all limb slots. This is what will shake.")]
    [SerializeField] private Transform visualsHolder;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.1f;

    [Header("Visuals - Torso")]
    [SerializeField] private GameObject torsoDefaultVisual;
    [SerializeField] private GameObject torsoDamagedVisual;
    [Tooltip("Health percentage (0-1) to show damaged visuals for Head/Torso.")]
    [SerializeField] private float damageVisualThreshold = 0.25f;

    [Header("Damage Feedback")]
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private Color flashColor = Color.red;

    [Header("Audio")] // --- NEW ---
    [SerializeField] private AudioClip limbAttachSound;

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
    private float maxTorsoHealth; // Used for threshold check

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
    
    // --- Logic ---
    private int initialLimbCount = 4; // Assuming 4 limbs for player usually
    private LimbSlot nextWeakLimb = (LimbSlot)(-1);

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
        
        maxTorsoHealth = torsoHealth;

        if (visualsHolder != null)
        {
            visualsHolderOriginalPos = visualsHolder.localPosition;
        }

        if(startingHead) AttachToSlot(startingHead, LimbSlot.Head, false, false, false);
        if(startingArm) AttachToSlot(startingArm, LimbSlot.LeftArm, true, false, false);
        if(startingArm) AttachToSlot(startingArm, LimbSlot.RightArm, false, false, false);
        if(startingLeg) AttachToSlot(startingLeg, LimbSlot.LeftLeg, true, false, false);
        if(startingLeg) AttachToSlot(startingLeg, LimbSlot.RightLeg, false, false, false);

        PickNextWeakLimb(); // Initial pick
        UpdateDamageVisuals();
        UpdatePlayerStats();
    }

    public void TakeDamage(float damageAmount, Vector2 hitDirection = default)
    {
        if (audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }

        if (BloodManager.Instance != null)
        {
            Vector2 dir = hitDirection == Vector2.zero ? Random.insideUnitCircle.normalized : hitDirection;
            BloodManager.Instance.SpawnBlood(transform.position, dir);
        }

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashDamageCoroutine());

        if (visualsHolder != null && !isShaking) StartCoroutine(ShakeVisuals());
        
        // --- Damage Logic ---
        torsoHealth -= damageAmount;

        float healthPercent = Mathf.Clamp01(torsoHealth / maxTorsoHealth);
        int targetLimbCount = Mathf.CeilToInt(healthPercent * initialLimbCount);
        int currentLimbs = GetCurrentArmLegCount();

        if (currentLimbs > targetLimbCount)
        {
            // We need to lose a limb. Lose the targeted one.
            if ((int)nextWeakLimb != -1 && IsLimbAttached(nextWeakLimb))
            {
                DetachLimb(nextWeakLimb);
            }
            else
            {
                PickNextWeakLimb();
                if((int)nextWeakLimb != -1) DetachLimb(nextWeakLimb);
            }
            PickNextWeakLimb(); // Prepare next
        }
        else if (currentHead && torsoHealth <= 0)
        {
            Debug.Log("Losing head!");
            DetachLimb(LimbSlot.Head);
        }

        if (torsoHealth <= 0)
        {
            Die();
        }
        
        UpdateDamageVisuals();
    }

    private void PickNextWeakLimb()
    {
        List<LimbSlot> available = new List<LimbSlot>();
        if (currentLeftArm) available.Add(LimbSlot.LeftArm);
        if (currentRightArm) available.Add(LimbSlot.RightArm);
        if (currentLeftLeg) available.Add(LimbSlot.LeftLeg);
        if (currentRightLeg) available.Add(LimbSlot.RightLeg);

        nextWeakLimb = (LimbSlot)(-1);
        if (available.Count > 0)
        {
            nextWeakLimb = available[Random.Range(0, available.Count)];
        }
    }

    private void UpdateDamageVisuals()
    {
        int currentLimbs = GetCurrentArmLegCount();
        
        float healthPerLimb = maxTorsoHealth / Mathf.Max(1, initialLimbCount);
        float dropThreshold = (currentLimbs - 1) * healthPerLimb; 
        float warningThreshold = dropThreshold + (healthPerLimb * 0.7f);

        if ((int)nextWeakLimb != -1)
        {
            bool shouldShowDamage = torsoHealth <= warningThreshold;
            WorldLimb limb = GetLimb(nextWeakLimb);
            if (limb != null)
            {
                limb.SetVisualState(shouldShowDamage);
            }
        }

        bool criticalHealth = (torsoHealth / maxTorsoHealth) <= damageVisualThreshold;
        if (torsoDefaultVisual) torsoDefaultVisual.SetActive(!criticalHealth);
        if (torsoDamagedVisual) torsoDamagedVisual.SetActive(criticalHealth);
        if (currentHead != null) currentHead.SetVisualState(criticalHealth);
    }

    private int GetCurrentArmLegCount()
    {
        int count = 0;
        if (currentLeftArm) count++;
        if (currentRightArm) count++;
        if (currentLeftLeg) count++;
        if (currentRightLeg) count++;
        return count;
    }

    private bool IsLimbAttached(LimbSlot slot)
    {
        return GetLimb(slot) != null;
    }

    private WorldLimb GetLimb(LimbSlot slot)
    {
        switch (slot)
        {
            case LimbSlot.Head: return currentHead;
            case LimbSlot.LeftArm: return currentLeftArm;
            case LimbSlot.RightArm: return currentRightArm;
            case LimbSlot.LeftLeg: return currentLeftLeg;
            case LimbSlot.RightLeg: return currentRightLeg;
            default: return null;
        }
    }

    public bool TryAttachLimb(LimbData limbToAttach, bool isDamaged)
    {
        if (limbToAttach == null) return false;

        bool attached = false;

        if (limbToAttach.limbType == LimbType.Arm)
        {
            if (currentRightArm == null) { AttachToSlot(limbToAttach, LimbSlot.RightArm, false, isDamaged); attached = true; }
            else if (currentLeftArm == null) { AttachToSlot(limbToAttach, LimbSlot.LeftArm, true, isDamaged); attached = true; }
        }
        else if (limbToAttach.limbType == LimbType.Leg)
        {
            if (currentRightLeg == null) { AttachToSlot(limbToAttach, LimbSlot.RightLeg, false, isDamaged); attached = true; }
            else if (currentLeftLeg == null) { AttachToSlot(limbToAttach, LimbSlot.LeftLeg, true, isDamaged); attached = true; }
        }

        if (attached)
        {
            torsoHealth = Mathf.Min(torsoHealth + 10f, maxTorsoHealth);
            PickNextWeakLimb();
            UpdateDamageVisuals();
            
            // --- NEW: Play Attach Sound ---
            if (audioSource != null && limbAttachSound != null)
            {
                audioSource.PlayOneShot(limbAttachSound);
            }
            // ------------------------------

            return true;
        }
        return false;
    }

    void DetachLimb(LimbSlot slot)
    {
        WorldLimb limbToDetach = GetLimb(slot);
        switch (slot)
        {
            case LimbSlot.Head: currentHead = null; break;
            case LimbSlot.LeftArm: currentLeftArm = null; break;
            case LimbSlot.RightArm: currentRightArm = null; break;
            case LimbSlot.LeftLeg: currentLeftLeg = null; break;
            case LimbSlot.RightLeg: currentRightLeg = null; break;
        }

        if (limbToDetach != null)
        {
            bool isMaintained = Random.value <= maintainLimbChance;
            GameObject thrownLimbObj = Instantiate(limbToDetach.GetLimbData().visualPrefab, transform.position, Quaternion.identity);
            Vector2 throwDirection = new Vector2(Random.Range(-1f, 1f), Random.Range(0.5f, 1f)).normalized; 
            WorldLimb worldLimb = thrownLimbObj.GetComponent<WorldLimb>();
            if (worldLimb)
            {
                worldLimb.InitializeThrow(limbToDetach.GetLimbData(), isMaintained, throwDirection, limbToDetach.IsShowingDamaged());
            }
            Destroy(limbToDetach.gameObject);
        }

        if (currentHead == null) Die();
        UpdatePlayerStats();
    }

    void AttachToSlot(LimbData limbData, LimbSlot slot, bool flipSprite, bool isDamaged, bool updateStats = true)
    {
        Transform parentSlot = GetSlotTransform(slot);
        if (parentSlot == null) return;

        GameObject limbObj = Instantiate(limbData.visualPrefab, parentSlot.position, parentSlot.rotation, parentSlot);
        limbObj.name = limbData.name + " (Attached)";

        SpriteRenderer sr = limbObj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && flipSprite) sr.flipX = true;

        WorldLimb limbComponent = limbObj.GetComponent<WorldLimb>();
        if (limbComponent == null) { Destroy(limbObj); return; }

        limbComponent.InitializeAttached(limbData, isDamaged);

        int sortingOrder = 0;
        switch (slot)
        {
            case LimbSlot.Head: currentHead = limbComponent; sortingOrder = headOrder; break;
            case LimbSlot.LeftArm: currentLeftArm = limbComponent; sortingOrder = leftArmOrder; break;
            case LimbSlot.RightArm: currentRightArm = limbComponent; sortingOrder = rightArmOrder; break;
            case LimbSlot.LeftLeg: currentLeftLeg = limbComponent; sortingOrder = legOrder; break;
            case LimbSlot.RightLeg: currentRightLeg = limbComponent; sortingOrder = legOrder; break;
        }

        SpriteRenderer[] srs = limbObj.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr_renderer in srs) sr_renderer.sortingOrder = sortingOrder;

        currentRenderers.AddRange(srs); 

        if (updateStats) UpdatePlayerStats();
    }

    void UpdatePlayerStats()
    {
        float totalMoveSpeed = 0f;
        int legCount = 0;
        int armCount = 0; 
        
        if (currentLeftLeg) { totalMoveSpeed += currentLeftLeg.GetLimbData().moveSpeedBonus; legCount++; }
        if (currentRightLeg) { totalMoveSpeed += currentRightLeg.GetLimbData().moveSpeedBonus; legCount++; }
        if (currentLeftArm) armCount++;
        if (currentRightArm) armCount++;

        if (legCount > 0)
        {
            totalMoveSpeed += baseMoveSpeed; 
            if (legCount == 1) totalMoveSpeed *= 0.6f; 
        }
        
        if (playerMovement) playerMovement.SetMoveSpeed(totalMoveSpeed);

        canCrawl = (legCount == 0 && armCount > 0);

        if (legCount == 0 && armCount == 0)
        {
            if (currentHead != null)
            {
                Debug.Log("Player is helpless. Detaching head.");
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
        this.enabled = false;
        if (playerMovement) playerMovement.enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        StopAllCoroutines();
        if(visualsHolder) visualsHolder.localPosition = visualsHolderOriginalPos;
    }

    public bool CanAttack() { return currentLeftArm != null || currentRightArm != null; }
    public bool CanCrawl() { return canCrawl; }

    public LimbData GetArmData(bool isLeftArm)
    {
        if (isLeftArm) return (currentLeftArm != null) ? currentLeftArm.GetLimbData() : null;
        else return (currentRightArm != null) ? currentRightArm.GetLimbData() : null;
    }

    public Transform GetVisualsHolder() { return visualsHolder; }
    public Transform GetLeftArmSlot() { return leftArmSlot; }
    public Transform GetRightArmSlot() { return rightArmSlot; }
    public Transform GetLeftLegSlot() { return leftLegSlot; }
    public Transform GetRightLegSlot() { return rightLegSlot; }

    private IEnumerator ShakeVisuals()
    {
        isShaking = true;
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;
            visualsHolder.localPosition = new Vector3(visualsHolderOriginalPos.x + x, visualsHolderOriginalPos.y + y, visualsHolderOriginalPos.z);
            elapsed += Time.deltaTime;
            yield return null; 
        }
        visualsHolder.localPosition = visualsHolderOriginalPos;
        isShaking = false;
    }

    private IEnumerator FlashDamageCoroutine()
    {
        currentRenderers.Clear();
        if (visualsHolder != null) visualsHolder.GetComponentsInChildren<SpriteRenderer>(currentRenderers);

        foreach (var renderer in currentRenderers) if (renderer != null) renderer.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        foreach (var renderer in currentRenderers) if (renderer != null) renderer.color = Color.white;
        
        flashCoroutine = null; 
    }
}