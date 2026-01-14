using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class EnemyLimbController : MonoBehaviour
{
    // --- NEW: Custom definition for gore drops ---
    [System.Serializable]
    public struct GoreDropItem
    {
        public string name; // Just for organization in Inspector
        public GameObject prefab;
        [Range(0f, 1f)] public float dropChance;
        public int maxAmount;
    }
    // ---------------------------------------------

    [Header("Base Stats")]
    public float maxHealth = 50f;
    public float currentHealth;
    
    [Header("Loot - Currency")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int minCoins = 1;
    [SerializeField] private int maxCoins = 3;

    [Header("Loot - Gore")]
    [Tooltip("Define specific gore items, their drop chance, and how many can spawn.")]
    [SerializeField] private GoreDropItem[] goreDrops;

    [Header("Limb Configuration")]
    public LimbData startingHead;
    public LimbData startingLeftArm;
    public LimbData startingRightArm;
    public LimbData startingLeftLeg;
    public LimbData startingRightLeg;

    [Header("Limb Slots (Assign in Inspector)")]
    public Transform visualsHolder;
    public Transform headSlot;
    public Transform leftArmSlot;
    public Transform rightArmSlot;
    public Transform leftLegSlot;
    public Transform rightLegSlot;

    [Header("Visuals - Torso")]
    [SerializeField] private GameObject torsoDefaultVisual;
    [SerializeField] private GameObject torsoDamagedVisual;
    [Tooltip("Health percentage (0-1) to show damaged visuals for Head/Torso (when no limbs left).")]
    [SerializeField] private float damageVisualThreshold = 0.25f;

    [Header("Damage Settings")]
    [Range(0f, 1f)] public float limbDropChance = 0.4f;
    [Range(0f, 1f)] public float maintainLimbChance = 0.3f; 
    
    [Header("Alert Settings")] 
    [SerializeField] private float damageAlertRadius = 10f;
    [SerializeField] private LayerMask enemyLayer; 

    [Header("Audio - Vocals")]
    [SerializeField] private AudioClip[] damageSounds;
    [SerializeField] private AudioClip[] idleSounds;
    [SerializeField] private AudioClip[] spotSounds;
    [SerializeField] private AudioClip[] attackSounds;
    [SerializeField] private AudioClip[] fleeSounds;
    [SerializeField] private AudioClip[] deathSounds;

    [Header("Audio - FX")]
    [SerializeField] private AudioClip[] bloodSounds; 
    [SerializeField] private Color damageFlashColor = Color.red;

    // --- Audio Throttling ---
    private static float lastGlobalDamageSoundTime = 0f;
    private const float MIN_DAMAGE_SOUND_INTERVAL = 0.1f; 

    // --- State ---
    private WorldLimb currentHead;
    private WorldLimb currentLeftArm;
    private WorldLimb currentRightArm;
    private WorldLimb currentLeftLeg;
    private WorldLimb currentRightLeg;

    private int initialLimbCount = 0; 
    private LimbSlot nextWeakLimb = (LimbSlot)(-1); 

    [HideInInspector] public float moveSpeedBonus = 0f;
    [HideInInspector] public float attackDamageBonus = 0f;
    [HideInInspector] public bool hasLegs = false;
    [HideInInspector] public bool hasArms = false;
    
    private bool isDead = false; 

    private AudioSource audioSource;
    private List<SpriteRenderer> renderers = new List<SpriteRenderer>();

    void Start()
    {
        currentHealth = maxHealth;
        audioSource = GetComponent<AudioSource>();

        if (startingHead) AttachLimb(startingHead, LimbSlot.Head);
        if (startingLeftArm) AttachLimb(startingLeftArm, LimbSlot.LeftArm);
        if (startingRightArm) AttachLimb(startingRightArm, LimbSlot.RightArm);
        if (startingLeftLeg) AttachLimb(startingLeftLeg, LimbSlot.LeftLeg);
        if (startingRightLeg) AttachLimb(startingRightLeg, LimbSlot.RightLeg);
        
        UpdateTorsoVisuals(false);
        UpdateStats();

        initialLimbCount = GetCurrentArmLegCount();
        PickNextWeakLimb();

        GetComponentsInChildren<SpriteRenderer>(renderers);
    }

    public void TakeDamage(float amount, Vector2 hitDirection = default)
    {
        if (isDead) return;

        currentHealth -= amount;
        
        // --- NEW: Alert SELF (Instantly Chase) ---
        if (TryGetComponent<EnemyAI>(out EnemyAI myAI))
        {
            myAI.OnDamageTaken();
        }
        // ----------------------------------------

        AlertNearbyEnemies();

        if (BloodManager.Instance != null)
        {
            Vector2 dir = hitDirection == Vector2.zero ? Random.insideUnitCircle.normalized : hitDirection;
            BloodManager.Instance.SpawnBlood(transform.position, dir);
        }

        if (Time.time - lastGlobalDamageSoundTime > MIN_DAMAGE_SOUND_INTERVAL)
        {
            PlayRandomClip(damageSounds);
            lastGlobalDamageSoundTime = Time.time;
        }
        PlayRandomClip(bloodSounds);

        StartCoroutine(FlashDamage());

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);
            int targetLimbCount = Mathf.CeilToInt(healthPercent * initialLimbCount);
            int currentLimbs = GetCurrentArmLegCount();

            while (currentLimbs > targetLimbCount && currentLimbs > 0)
            {
                if ((int)nextWeakLimb != -1 && IsLimbAttached(nextWeakLimb))
                {
                    DetachLimb(nextWeakLimb);
                }
                else
                {
                    PickNextWeakLimb(); 
                    if ((int)nextWeakLimb != -1) DetachLimb(nextWeakLimb);
                }
                
                PickNextWeakLimb();
                currentLimbs--;
            }

            UpdateDamageVisuals();
        }
    }

    private void PickNextWeakLimb()
    {
        List<LimbSlot> armSlots = new List<LimbSlot>();
        List<LimbSlot> legSlots = new List<LimbSlot>();

        if (currentLeftArm) armSlots.Add(LimbSlot.LeftArm);
        if (currentRightArm) armSlots.Add(LimbSlot.RightArm);
        if (currentLeftLeg) legSlots.Add(LimbSlot.LeftLeg);
        if (currentRightLeg) legSlots.Add(LimbSlot.RightLeg);

        nextWeakLimb = (LimbSlot)(-1);

        float armBias = 0.75f; 

        if (armSlots.Count > 0 && legSlots.Count > 0)
        {
            if (Random.value < armBias) nextWeakLimb = armSlots[Random.Range(0, armSlots.Count)];
            else nextWeakLimb = legSlots[Random.Range(0, legSlots.Count)];
        }
        else if (armSlots.Count > 0)
        {
            nextWeakLimb = armSlots[Random.Range(0, armSlots.Count)];
        }
        else if (legSlots.Count > 0)
        {
            nextWeakLimb = legSlots[Random.Range(0, legSlots.Count)];
        }
    }

    private void UpdateDamageVisuals()
    {
        int currentLimbs = GetCurrentArmLegCount();
        
        float healthPerLimb = maxHealth / Mathf.Max(1, initialLimbCount);
        float dropThreshold = (currentLimbs - 1) * healthPerLimb; 
        float warningThreshold = dropThreshold + (healthPerLimb * 0.7f);

        if ((int)nextWeakLimb != -1)
        {
            bool shouldShowDamage = currentHealth <= warningThreshold;
            WorldLimb limb = GetLimb(nextWeakLimb);
            if (limb != null)
            {
                limb.SetVisualState(shouldShowDamage);
            }
        }

        bool criticalHealth = (currentHealth / maxHealth) <= damageVisualThreshold;
        UpdateTorsoVisuals(criticalHealth);
        if (currentHead != null) currentHead.SetVisualState(criticalHealth);
    }

    private void UpdateTorsoVisuals(bool isDamaged)
    {
        if (torsoDefaultVisual) torsoDefaultVisual.SetActive(!isDamaged);
        if (torsoDamagedVisual) torsoDamagedVisual.SetActive(isDamaged);
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

    public void PlayIdleSound() { PlayRandomClip(idleSounds, 0.8f); }
    public void PlaySpotSound() { PlayRandomClip(spotSounds, 1.2f); }
    public void PlayAttackSound() { PlayRandomClip(attackSounds); }
    public void PlayFleeSound() { PlayRandomClip(fleeSounds, 1.2f); }

    private void PlayRandomClip(AudioClip[] clips, float volumeScale = 1f)
    {
        if (clips != null && clips.Length > 0 && audioSource != null)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(clip, volumeScale);
            }
        }
    }

    public bool TryAttachLimb(LimbData limbToAttach, bool isDamaged)
    {
        if (isDead) return false; // Don't attach limbs to a dying enemy

        if (limbToAttach == null) return false;

        bool attached = false;

        // 1. Try filling Legs first (applies to Leg AND Universal)
        if (limbToAttach.limbType == LimbType.Leg || limbToAttach.limbType == LimbType.Universal)
        {
            if (currentRightLeg == null) { AttachToSlot(limbToAttach, LimbSlot.RightLeg, false, isDamaged); attached = true; }
            else if (currentLeftLeg == null) { AttachToSlot(limbToAttach, LimbSlot.LeftLeg, true, isDamaged); attached = true; }
        }

        // 2. Try filling Arms if not already attached (applies to Arm AND Universal)
        if (!attached && (limbToAttach.limbType == LimbType.Arm || limbToAttach.limbType == LimbType.Universal))
        {
            if (currentRightArm == null) { AttachToSlot(limbToAttach, LimbSlot.RightArm, false, isDamaged); attached = true; }
            else if (currentLeftArm == null) { AttachToSlot(limbToAttach, LimbSlot.LeftArm, true, isDamaged); attached = true; }
        }

        if (attached)
        {
            float healthPerLimb = maxHealth / Mathf.Max(1, initialLimbCount);
            currentHealth = Mathf.Min(currentHealth + healthPerLimb, maxHealth);
            
            PickNextWeakLimb();
            UpdateDamageVisuals();

            return true;
        }
        return false;
    }

    public bool IsMissingArm() { return currentLeftArm == null || currentRightArm == null; }
    public bool IsMissingLeg() { return currentLeftLeg == null || currentRightLeg == null; }

    private void AlertNearbyEnemies()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, damageAlertRadius, enemyLayer);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue; 
            
            EnemyAI ai = hit.GetComponent<EnemyAI>();
            if (ai != null) ai.OnHearNoise(transform.position);
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
        WorldLimb limbToRemove = GetLimb(slot);
        
        switch (slot)
        {
            case LimbSlot.Head: currentHead = null; break;
            case LimbSlot.LeftArm: currentLeftArm = null; break;
            case LimbSlot.RightArm: currentRightArm = null; break;
            case LimbSlot.LeftLeg: currentLeftLeg = null; break;
            case LimbSlot.RightLeg: currentRightLeg = null; break;
        }

        if (limbToRemove != null)
        {
            GameObject pickup = Instantiate(limbToRemove.GetLimbData().visualPrefab, transform.position, Quaternion.identity);
            WorldLimb pickupScript = pickup.GetComponent<WorldLimb>();
            Vector2 flingDir = Random.insideUnitCircle.normalized;
            bool isMaintained = Random.value < maintainLimbChance;
            
            pickupScript.InitializeThrow(limbToRemove.GetLimbData(), isMaintained, flingDir, true);

            SpriteRenderer[] limbRenderers = limbToRemove.GetComponentsInChildren<SpriteRenderer>();
            foreach(var sr in limbRenderers)
            {
                if(renderers.Contains(sr)) renderers.Remove(sr);
            }

            Destroy(limbToRemove.gameObject);
            UpdateStats();
        }
    }

    private void AttachToSlot(LimbData limbData, LimbSlot slot, bool flipSprite, bool isDamaged)
    {
        Transform parentSlot = GetSlotTransform(slot);
        if (parentSlot == null) return;

        GameObject limbObj = Instantiate(limbData.visualPrefab, parentSlot.position, parentSlot.rotation, parentSlot);
        WorldLimb limbComponent = limbObj.GetComponent<WorldLimb>();
        if (limbComponent == null) { Destroy(limbObj); return; }

        SpriteRenderer sr = limbObj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && flipSprite) sr.flipX = true;

        limbComponent.InitializeAttached(limbData, isDamaged);

        int order = 0;
        switch (slot)
        {
            case LimbSlot.Head: currentHead = limbComponent; order = 10; break;
            case LimbSlot.LeftArm: currentLeftArm = limbComponent; order = 5; break;
            case LimbSlot.RightArm: currentRightArm = limbComponent; order = 5; break; 
            case LimbSlot.LeftLeg: currentLeftLeg = limbComponent; order = -10; break;
            case LimbSlot.RightLeg: currentRightLeg = limbComponent; order = -10; break;
        }

        SpriteRenderer[] srs = limbObj.GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in srs) r.sortingOrder = order;
        renderers.AddRange(srs);

        UpdateStats();
    }

    private void AttachLimb(LimbData data, LimbSlot slot)
    {
        AttachToSlot(data, slot, false, false);
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
        if (isDead) return;
        isDead = true;

        if (BloodManager.Instance != null)
            BloodManager.Instance.SpawnBlood(transform.position, Random.insideUnitCircle.normalized, 2.5f);

        if (deathSounds != null && deathSounds.Length > 0)
        {
            AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position);
        }

        // --- SPAWN COINS ---
        if (coinPrefab != null)
        {
            int amount = Random.Range(minCoins, maxCoins + 1);
            for (int i = 0; i < amount; i++)
            {
                GameObject coin = Instantiate(coinPrefab, transform.position, Quaternion.identity);
                if (coin.TryGetComponent<CoinPickup>(out CoinPickup pickup))
                {
                    pickup.Initialize();
                }
            }
        }
        
        // --- NEW: SPAWN GORE DROPS (With Chance & Counts) ---
        if (goreDrops != null && goreDrops.Length > 0)
        {
            foreach (var drop in goreDrops)
            {
                if (drop.prefab != null)
                {
                    // 1. Roll the chance
                    if (Random.value <= drop.dropChance)
                    {
                        // 2. Determine quantity (1 to Max)
                        int quantity = Random.Range(1, drop.maxAmount + 1);

                        // 3. Spawn them
                        for (int i = 0; i < quantity; i++)
                        {
                            Vector2 randomOffset = Random.insideUnitCircle * 0.5f;
                            Instantiate(drop.prefab, (Vector2)transform.position + randomOffset, Quaternion.identity);
                        }
                    }
                }
            }
        }
        // ---------------------------------------------------

        if (currentLeftArm) DetachLimb(LimbSlot.LeftArm);
        if (currentRightArm) DetachLimb(LimbSlot.RightArm);
        if (currentLeftLeg) DetachLimb(LimbSlot.LeftLeg);
        if (currentRightLeg) DetachLimb(LimbSlot.RightLeg);
        if (currentHead) DetachLimb(LimbSlot.Head);
        
        Destroy(gameObject);
    }

    private IEnumerator FlashDamage()
    {
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
    
    public LimbData GetActiveWeaponLimb()
    {
        if (currentLeftArm) return currentLeftArm.GetLimbData();
        if (currentRightArm) return currentRightArm.GetLimbData();
        return null; 
    }

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