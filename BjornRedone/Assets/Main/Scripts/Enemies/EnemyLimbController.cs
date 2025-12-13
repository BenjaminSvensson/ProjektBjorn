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
    [Range(0f, 1f)] public float limbDropChance = 0.4f;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private Color damageFlashColor = Color.red;

    // --- State ---
    private WorldLimb currentHead;
    private WorldLimb currentLeftArm;
    private WorldLimb currentRightArm;
    private WorldLimb currentLeftLeg;
    private WorldLimb currentRightLeg;

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

        // NOW find all renderers, so we include the newly spawned limbs in the flash effect
        GetComponentsInChildren<SpriteRenderer>(renderers);
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        
        // Feedback
        if (damageSound && audioSource) audioSource.PlayOneShot(damageSound);
        StartCoroutine(FlashDamage());

        // Chance to lose a limb
        if (Random.value < limbDropChance)
        {
            LoseRandomLimb();
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void LoseRandomLimb()
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
        else if (currentHead)
        {
            // If only head remains, losing it kills the enemy
            DetachLimb(LimbSlot.Head);
            Die();
        }
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
            pickupScript.InitializeThrow(limbToRemove.GetLimbData(), true, flingDir);

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
        if (currentHead) DetachLimb(LimbSlot.Head);
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
}