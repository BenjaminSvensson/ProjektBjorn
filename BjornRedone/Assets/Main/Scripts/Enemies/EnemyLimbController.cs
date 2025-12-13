using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class EnemyLimbController : MonoBehaviour
{
    [Header("Base Stats")]
    public float maxHealth = 50f;
    public float currentHealth;
    
    [Header("Limb Configuration")]
    public LimbData startingHead;
    public LimbData startingLeftArm;
    public LimbData startingRightArm;
    public LimbData startingLeftLeg;
    public LimbData startingRightLeg;

    [Header("Limb Slots (Assign in Inspector)")]
    [Tooltip("The parent object for visual shaking.")]
    public Transform visualsHolder;
    public Transform headSlot;
    public Transform leftArmSlot;
    public Transform rightArmSlot;
    public Transform leftLegSlot;
    public Transform rightLegSlot;

    [Header("Damage Settings")]
    [Tooltip("Chance (0-1) that a limb will detach when the enemy takes damage.")]
    [Range(0f, 1f)] public float limbDropChance = 0.4f;
    [Tooltip("Chance (0-1) that a detached limb spawns as a usable pickup. If false, it spawns as broken debris.")]
    [Range(0f, 1f)] public float maintainLimbChance = 0.3f; 
    
    [Header("Alert Settings")] // --- NEW ---
    [Tooltip("Radius to alert other enemies when damaged.")]
    [SerializeField] private float damageAlertRadius = 10f;
    [Tooltip("Layer mask for enemies to alert.")]
    [SerializeField] private LayerMask enemyLayer; 

    [SerializeField] private AudioClip damageSound;
    [SerializeField] private Color damageFlashColor = Color.red;

    // --- State ---
    private WorldLimb currentHead;
    private WorldLimb currentLeftArm;
    private WorldLimb currentRightArm;
    private WorldLimb currentLeftLeg;
    private WorldLimb currentRightLeg;

    // Health Logic State
    private int initialLimbCount = 0; // Arms + Legs only (Head is treated as "Life")

    // Stats calculated from limbs
    [HideInInspector] public float moveSpeedBonus = 0f;
    [HideInInspector] public float attackDamageBonus = 0f;
    [HideInInspector] public bool hasLegs = false;
    [HideInInspector] public bool hasArms = false;

    // Components
    private AudioSource audioSource;
    private List<SpriteRenderer> renderers = new List<SpriteRenderer>();

    void Start()
    {
        currentHealth = maxHealth;
        audioSource = GetComponent<AudioSource>();

        // Equip starting limbs FIRST
        if (startingHead) AttachLimb(startingHead, LimbSlot.Head);
        if (startingLeftArm) AttachLimb(startingLeftArm, LimbSlot.LeftArm);
        if (startingRightArm) AttachLimb(startingRightArm, LimbSlot.RightArm);
        if (startingLeftLeg) AttachLimb(startingLeftLeg, LimbSlot.LeftLeg);
        if (startingRightLeg) AttachLimb(startingRightLeg, LimbSlot.RightLeg);
        
        UpdateStats();

        // Calculate how many non-head limbs we started with
        initialLimbCount = GetCurrentArmLegCount();

        // NOW find all renderers, so we include the newly spawned limbs in the flash effect
        GetComponentsInChildren<SpriteRenderer>(renderers);
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        
        // --- NEW: Alert others ---
        AlertNearbyEnemies();
        // -------------------------

        // Feedback
        if (damageSound && audioSource) audioSource.PlayOneShot(damageSound);
        StartCoroutine(FlashDamage());

        // --- NEW LOGIC: Limbs indicate Health ---
        
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Calculate how many limbs we *should* have based on health %
            float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);
            int targetLimbCount = Mathf.CeilToInt(healthPercent * initialLimbCount);
            
            int currentLimbs = GetCurrentArmLegCount();

            // While we have more limbs than our health allows, lose them!
            // This loop ensures that a massive hit (100% -> 10% health) drops multiple limbs at once.
            while (currentLimbs > targetLimbCount && currentLimbs > 0)
            {
                LoseRandomArmOrLeg();
                currentLimbs--;
            }
        }
    }

    // --- NEW: Helper for alerting enemies ---
    private void AlertNearbyEnemies()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, damageAlertRadius, enemyLayer);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue; // Don't alert self
            
            EnemyAI ai = hit.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.OnHearNoise(transform.position);
            }
        }
    }

    private void LoseRandomArmOrLeg()
    {
        List<LimbSlot> availableSlots = new List<LimbSlot>();
        if (currentLeftArm) availableSlots.Add(LimbSlot.LeftArm);
        if (currentRightArm) availableSlots.Add(LimbSlot.RightArm);
        if (currentLeftLeg) availableSlots.Add(LimbSlot.LeftLeg);
        if (currentRightLeg) availableSlots.Add(LimbSlot.RightLeg);

        if (availableSlots.Count > 0)
        {
            LimbSlot slot = availableSlots[Random.Range(0, availableSlots.Count)];
            DetachLimb(slot);
        }
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

    private void DetachLimb(LimbSlot slot)
    {
        WorldLimb limbToRemove = null;

        switch (slot)
        {
            case LimbSlot.Head: limbToRemove = currentHead; currentHead = null; break;
            case LimbSlot.LeftArm: limbToRemove = currentLeftArm; currentLeftArm = null; break;
            case LimbSlot.RightArm: limbToRemove = currentRightArm; currentRightArm = null; break;
            case LimbSlot.LeftLeg: limbToRemove = currentLeftLeg; currentLeftLeg = null; break;
            case LimbSlot.RightLeg: limbToRemove = currentRightLeg; currentRightLeg = null; break;
        }

        if (limbToRemove != null)
        {
            // Spawn the pickup
            GameObject pickup = Instantiate(limbToRemove.GetLimbData().visualPrefab, transform.position, Quaternion.identity);
            WorldLimb pickupScript = pickup.GetComponent<WorldLimb>();
            
            // Fling it away
            Vector2 flingDir = Random.insideUnitCircle.normalized;

            // --- CHANGED: Use probability to decide if it's usable (maintained) or broken ---
            bool isMaintained = Random.value < maintainLimbChance;
            pickupScript.InitializeThrow(limbToRemove.GetLimbData(), isMaintained, flingDir);

            // Important: Remove its renderer from our list so we don't try to flash a destroyed object
            SpriteRenderer[] limbRenderers = limbToRemove.GetComponentsInChildren<SpriteRenderer>();
            foreach(var sr in limbRenderers)
            {
                if(renderers.Contains(sr)) renderers.Remove(sr);
            }

            Destroy(limbToRemove.gameObject);
            UpdateStats();
        }
    }

    private void AttachLimb(LimbData data, LimbSlot slot)
    {
        Transform parent = GetSlotTransform(slot);
        GameObject obj = Instantiate(data.visualPrefab, parent);
        WorldLimb limb = obj.GetComponent<WorldLimb>();
        
        // Initialize as "Attached" so it doesn't have physics
        limb.InitializeAttached(data, false);

        // Sorting Order Logic
        int order = 0;
        switch (slot)
        {
            case LimbSlot.Head: currentHead = limb; order = 10; break;
            case LimbSlot.LeftArm: currentLeftArm = limb; order = 5; break;
            case LimbSlot.RightArm: currentRightArm = limb; order = -5; break;
            case LimbSlot.LeftLeg: currentLeftLeg = limb; order = -10; break;
            case LimbSlot.RightLeg: currentRightLeg = limb; order = -10; break;
        }

        // Apply sorting order
        foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>())
        {
            sr.sortingOrder = order;
        }
    }

    private void UpdateStats()
    {
        moveSpeedBonus = 0;
        attackDamageBonus = 0;
        int legCount = 0;
        int armCount = 0;

        if (currentLeftLeg) { moveSpeedBonus += currentLeftLeg.GetLimbData().moveSpeedBonus; legCount++; }
        if (currentRightLeg) { moveSpeedBonus += currentRightLeg.GetLimbData().moveSpeedBonus; legCount++; }
        
        if (currentLeftArm) { attackDamageBonus += currentLeftArm.GetLimbData().attackDamageBonus; armCount++; }
        if (currentRightArm) { attackDamageBonus += currentRightArm.GetLimbData().attackDamageBonus; armCount++; }

        hasLegs = legCount > 0;
        hasArms = armCount > 0;
    }

    private Transform GetSlotTransform(LimbSlot slot)
    {
        switch (slot)
        {
            case LimbSlot.Head: return headSlot;
            case LimbSlot.LeftArm: return leftArmSlot;
            case LimbSlot.RightArm: return rightArmSlot;
            case LimbSlot.LeftLeg: return leftLegSlot;
            case LimbSlot.RightLeg: return rightLegSlot;
            default: return transform;
        }
    }

    private void Die()
    {
        // Explosive death! Detach ALL remaining limbs.
        if (currentLeftArm) DetachLimb(LimbSlot.LeftArm);
        if (currentRightArm) DetachLimb(LimbSlot.RightArm);
        if (currentLeftLeg) DetachLimb(LimbSlot.LeftLeg);
        if (currentRightLeg) DetachLimb(LimbSlot.RightLeg);
        if (currentHead) DetachLimb(LimbSlot.Head);

        // Destroy the torso/container
        Destroy(gameObject);
    }

    private IEnumerator FlashDamage()
    {
        // Filter out nulls just in case a limb was destroyed mid-frame
        for (int i = renderers.Count - 1; i >= 0; i--)
        {
            if (renderers[i] == null) renderers.RemoveAt(i);
            else renderers[i].color = damageFlashColor;
        }
        
        yield return new WaitForSeconds(0.1f);
        
        for (int i = renderers.Count - 1; i >= 0; i--)
        {
            if (renderers[i] == null) renderers.RemoveAt(i);
            else renderers[i].color = Color.white;
        }
    }
    
    // --- Public Helpers for AI & Animation ---
    public LimbData GetActiveWeaponLimb()
    {
        if (currentLeftArm) return currentLeftArm.GetLimbData();
        if (currentRightArm) return currentRightArm.GetLimbData();
        return null; 
    }

    // New getters for Animation Controller
    public Transform GetVisualsHolder() { return visualsHolder; }
    public Transform GetLeftArmSlot() { return leftArmSlot; }
    public Transform GetRightArmSlot() { return rightArmSlot; }
    public Transform GetLeftLegSlot() { return leftLegSlot; }
    public Transform GetRightLegSlot() { return rightLegSlot; }
    public bool HasLeftArm() { return currentLeftArm != null; }
    public bool HasRightArm() { return currentRightArm != null; }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageAlertRadius);
    }
}