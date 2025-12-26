using UnityEngine;
using UnityEngine.UI; // Required for Image
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class PlayerLimbController : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Transform visualsHolder;
    [SerializeField] private float shakeDuration = 0.15f, shakeMagnitude = 0.1f;

    [Header("Visuals - Torso")]
    [SerializeField] private GameObject torsoDefaultVisual, torsoDamagedVisual;
    [SerializeField] private float damageVisualThreshold = 0.25f;

    [Header("Damage Feedback")]
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private Color flashColor = Color.red;

    [Header("Audio")] 
    [SerializeField] private AudioClip limbAttachSound;

    [Header("Low Health Feedback")] // --- NEW ---
    [Tooltip("An AudioSource dedicated to the heartbeat sound (separate from the main Action AudioSource).")]
    [SerializeField] private AudioSource heartbeatSource;
    [Tooltip("A UI Image covering the screen (e.g., a black or red vignette sprite).")]
    [SerializeField] private RawImage vignetteImage;
    [Range(0f, 1f)]
    [SerializeField] private float lowHealthThreshold = 0.95f; 
    [SerializeField] private float maxVignetteAlpha = 1.0f;
    [SerializeField] private float minHeartbeatPitch = 0.8f;
    [SerializeField] private float maxHeartbeatPitch = 2.0f;

    [Header("Sorting Orders")]
    [SerializeField] private int headOrder = 10, leftArmOrder = 5, rightArmOrder = -5, legOrder = -10;

    [Header("Limb Slots")]
    public Transform headSlot, leftArmSlot, rightArmSlot, leftLegSlot, rightLegSlot;

    [Header("Base Stats")]
    public float baseMoveSpeed = 5f, baseAttackDamage = 1f, torsoHealth = 100f; 
    private float maxTorsoHealth;

    [Header("Starting Limbs")]
    public LimbData startingHead, startingArm, startingLeg;

    [Header("Limb Physics")]
    [Range(0f, 1f)] public float maintainLimbChance = 0.3f;

    private WorldLimb currentHead, currentLeftArm, currentRightArm, currentLeftLeg, currentRightLeg;
    private int initialLimbCount = 4;
    private LimbSlot nextWeakLimb = (LimbSlot)(-1);

    private PlayerMovement playerMovement;
    private Rigidbody2D rb; 
    private Vector3 visualsHolderOriginalPos; 
    private bool isShaking = false, canCrawl = false;
    private AudioSource audioSource; // Used for One-Shots (Damage, Attach)
    private List<SpriteRenderer> currentRenderers = new List<SpriteRenderer>();
    private Coroutine flashCoroutine;

    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>(); rb = GetComponent<Rigidbody2D>(); audioSource = GetComponent<AudioSource>();
        maxTorsoHealth = torsoHealth;
        if (visualsHolder) visualsHolderOriginalPos = visualsHolder.localPosition;
        if(startingHead) AttachToSlot(startingHead, LimbSlot.Head, false, false, false);
        if(startingArm) { AttachToSlot(startingArm, LimbSlot.LeftArm, true, false, false); AttachToSlot(startingArm, LimbSlot.RightArm, false, false, false); }
        if(startingLeg) { AttachToSlot(startingLeg, LimbSlot.LeftLeg, true, false, false); AttachToSlot(startingLeg, LimbSlot.RightLeg, false, false, false); }
        PickNextWeakLimb(); UpdateDamageVisuals(); UpdatePlayerStats();

        // Setup Heartbeat Loop
        if (heartbeatSource != null)
        {
            heartbeatSource.loop = true;
            heartbeatSource.volume = 0f;
            if (!heartbeatSource.isPlaying) heartbeatSource.Play();
        }
        
        // Initialize Vignette
        if (vignetteImage != null)
        {
            Color c = vignetteImage.color;
            c.a = 0f;
            vignetteImage.color = c;
            vignetteImage.gameObject.SetActive(true);
        }
    }

    // --- NEW: Handle Continuous Feedback ---
    void Update()
    {
        HandleLowHealthEffects();
    }

    private void HandleLowHealthEffects()
    {
        if (maxTorsoHealth <= 0) return;

        float healthPercent = Mathf.Clamp01(torsoHealth / maxTorsoHealth);

        // If we are below threshold and alive
        if (healthPercent <= lowHealthThreshold && torsoHealth > 0)
        {
            // Intensity goes from 0 (at threshold) to 1 (at near death)
            float intensity = 1f - (healthPercent / lowHealthThreshold);
            
            // Audio
            if (heartbeatSource)
            {
                // Smoothly adjust volume and pitch
                heartbeatSource.volume = Mathf.Lerp(heartbeatSource.volume, intensity, Time.deltaTime * 2f);
                heartbeatSource.pitch = Mathf.Lerp(minHeartbeatPitch, maxHeartbeatPitch, intensity);
            }

            // Visual
            if (vignetteImage)
            {
                Color c = vignetteImage.color;
                c.a = Mathf.Lerp(c.a, intensity * maxVignetteAlpha, Time.deltaTime * 5f);
                vignetteImage.color = c;
            }
        }
        else
        {
            // Safe zone (or dead/handled elsewhere): Fade out effects
            if (heartbeatSource)
            {
                heartbeatSource.volume = Mathf.Lerp(heartbeatSource.volume, 0f, Time.deltaTime * 2f);
            }

            if (vignetteImage)
            {
                Color c = vignetteImage.color;
                c.a = Mathf.Lerp(c.a, 0f, Time.deltaTime * 5f);
                vignetteImage.color = c;
            }
        }
    }

    public void TakeDamage(float damageAmount, Vector2 hitDirection = default)
    {
        audioSource?.PlayOneShot(damageSound);
        BloodManager.Instance?.SpawnBlood(transform.position, hitDirection == Vector2.zero ? Random.insideUnitCircle.normalized : hitDirection);
        if (flashCoroutine != null) StopCoroutine(flashCoroutine); flashCoroutine = StartCoroutine(FlashDamageCoroutine());
        if (visualsHolder && !isShaking) StartCoroutine(ShakeVisuals());
        
        torsoHealth -= damageAmount;
        int targetCount = Mathf.CeilToInt((torsoHealth / maxTorsoHealth) * initialLimbCount);
        if (GetCurrentArmLegCount() > targetCount)
        {
            if ((int)nextWeakLimb != -1 && IsLimbAttached(nextWeakLimb)) DetachLimb(nextWeakLimb);
            else { PickNextWeakLimb(); if((int)nextWeakLimb != -1) DetachLimb(nextWeakLimb); }
            PickNextWeakLimb();
        }
        else if (currentHead && torsoHealth <= 0) DetachLimb(LimbSlot.Head);
        if (torsoHealth <= 0) Die();
        UpdateDamageVisuals();
    }

    private void PickNextWeakLimb()
    {
        List<LimbSlot> avail = new List<LimbSlot>();
        if (currentLeftArm) avail.Add(LimbSlot.LeftArm); if (currentRightArm) avail.Add(LimbSlot.RightArm);
        if (currentLeftLeg) avail.Add(LimbSlot.LeftLeg); if (currentRightLeg) avail.Add(LimbSlot.RightLeg);
        nextWeakLimb = avail.Count > 0 ? avail[Random.Range(0, avail.Count)] : (LimbSlot)(-1);
    }

    private void UpdateDamageVisuals()
    {
        int count = GetCurrentArmLegCount();
        float hPerLimb = maxTorsoHealth / Mathf.Max(1, initialLimbCount);
        if ((int)nextWeakLimb != -1) GetLimb(nextWeakLimb)?.SetVisualState(torsoHealth <= (count - 1) * hPerLimb + (hPerLimb * 0.7f));
        bool crit = (torsoHealth / maxTorsoHealth) <= damageVisualThreshold;
        torsoDefaultVisual?.SetActive(!crit); torsoDamagedVisual?.SetActive(crit); currentHead?.SetVisualState(crit);
    }

    private int GetCurrentArmLegCount() => (currentLeftArm?1:0) + (currentRightArm?1:0) + (currentLeftLeg?1:0) + (currentRightLeg?1:0);
    private bool IsLimbAttached(LimbSlot slot) => GetLimb(slot) != null;
    private WorldLimb GetLimb(LimbSlot slot) { switch (slot) { case LimbSlot.Head: return currentHead; case LimbSlot.LeftArm: return currentLeftArm; case LimbSlot.RightArm: return currentRightArm; case LimbSlot.LeftLeg: return currentLeftLeg; case LimbSlot.RightLeg: return currentRightLeg; default: return null; } }

    public bool TryAttachLimb(LimbData limbToAttach, bool isDamaged)
    {
        if (!limbToAttach) return false;
        bool attached = false;
        if (limbToAttach.limbType == LimbType.Leg || limbToAttach.limbType == LimbType.Universal)
        {
            if (!currentRightLeg) { AttachToSlot(limbToAttach, LimbSlot.RightLeg, false, isDamaged); attached = true; }
            else if (!currentLeftLeg) { AttachToSlot(limbToAttach, LimbSlot.LeftLeg, true, isDamaged); attached = true; }
        }
        if (!attached && (limbToAttach.limbType == LimbType.Arm || limbToAttach.limbType == LimbType.Universal))
        {
            if (!currentRightArm) { AttachToSlot(limbToAttach, LimbSlot.RightArm, false, isDamaged); attached = true; }
            else if (!currentLeftArm) { AttachToSlot(limbToAttach, LimbSlot.LeftArm, true, isDamaged); attached = true; }
        }
        if (attached) { torsoHealth = Mathf.Min(torsoHealth + 10f, maxTorsoHealth); PickNextWeakLimb(); UpdateDamageVisuals(); audioSource?.PlayOneShot(limbAttachSound); return true; }
        return false;
    }

    void DetachLimb(LimbSlot slot)
    {
        WorldLimb limb = GetLimb(slot);
        switch (slot) { case LimbSlot.Head: currentHead = null; break; case LimbSlot.LeftArm: currentLeftArm = null; break; case LimbSlot.RightArm: currentRightArm = null; break; case LimbSlot.LeftLeg: currentLeftLeg = null; break; case LimbSlot.RightLeg: currentRightLeg = null; break; }
        if (limb)
        {
            Instantiate(limb.GetLimbData().visualPrefab, transform.position, Quaternion.identity).GetComponent<WorldLimb>()?.InitializeThrow(limb.GetLimbData(), Random.value <= maintainLimbChance, new Vector2(Random.Range(-1f, 1f), Random.Range(0.5f, 1f)).normalized, limb.IsShowingDamaged());
            Destroy(limb.gameObject);
        }
        if (!currentHead) Die(); UpdatePlayerStats();
    }

    void AttachToSlot(LimbData limbData, LimbSlot slot, bool flipSprite, bool isDamaged, bool updateStats = true)
    {
        Transform parent = GetSlotTransform(slot); if (!parent) return;
        GameObject obj = Instantiate(limbData.visualPrefab, parent.position, parent.rotation, parent);
        if (obj.GetComponentInChildren<SpriteRenderer>() && flipSprite) obj.GetComponentInChildren<SpriteRenderer>().flipX = true;
        WorldLimb comp = obj.GetComponent<WorldLimb>(); if (!comp) { Destroy(obj); return; }
        comp.InitializeAttached(limbData, isDamaged);
        int order = (slot == LimbSlot.Head) ? headOrder : (slot == LimbSlot.LeftArm) ? leftArmOrder : (slot == LimbSlot.RightArm) ? rightArmOrder : legOrder;
        foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>()) { sr.sortingOrder = order; currentRenderers.Add(sr); }
        switch (slot) { case LimbSlot.Head: currentHead = comp; break; case LimbSlot.LeftArm: currentLeftArm = comp; break; case LimbSlot.RightArm: currentRightArm = comp; break; case LimbSlot.LeftLeg: currentLeftLeg = comp; break; case LimbSlot.RightLeg: currentRightLeg = comp; break; }
        if (updateStats) UpdatePlayerStats();
    }

    void UpdatePlayerStats()
    {
        float speed = (currentLeftLeg?currentLeftLeg.GetLimbData().moveSpeedBonus:0) + (currentRightLeg?currentRightLeg.GetLimbData().moveSpeedBonus:0);
        int legs = (currentLeftLeg?1:0) + (currentRightLeg?1:0);
        int arms = (currentLeftArm?1:0) + (currentRightArm?1:0);
        if (legs > 0) { speed += baseMoveSpeed; if (legs == 1) speed *= 0.6f; }
        playerMovement?.SetMoveSpeed(speed);
        canCrawl = (legs == 0 && arms > 0);
        if (legs == 0 && arms == 0 && currentHead) DetachLimb(LimbSlot.Head);
    }

    Transform GetSlotTransform(LimbSlot slot)
    {
        switch (slot) { case LimbSlot.Head: return headSlot; case LimbSlot.LeftArm: return leftArmSlot; case LimbSlot.RightArm: return rightArmSlot; case LimbSlot.LeftLeg: return leftLegSlot; case LimbSlot.RightLeg: return rightLegSlot; default: return null; }
    }

    void Die() 
    { 
        this.enabled = false; 
        playerMovement.enabled = false; 
        if (rb) rb.linearVelocity = Vector2.zero; 
        StopAllCoroutines(); 
        
        // --- Kill Heartbeat ---
        if (heartbeatSource) heartbeatSource.Stop();
        
        if(visualsHolder) visualsHolder.localPosition = visualsHolderOriginalPos; 
    }

    public bool CanAttack() => currentLeftArm != null || currentRightArm != null;
    public bool CanCrawl() => canCrawl;
    public LimbData GetArmData(bool left) => left ? (currentLeftArm?.GetLimbData()) : (currentRightArm?.GetLimbData());
    public Transform GetVisualsHolder() => visualsHolder; public Transform GetLeftArmSlot() => leftArmSlot; public Transform GetRightArmSlot() => rightArmSlot; public Transform GetLeftLegSlot() => leftLegSlot; public Transform GetRightLegSlot() => rightLegSlot;

    private IEnumerator ShakeVisuals()
    {
        isShaking = true; float e = 0f;
        while (e < shakeDuration) { visualsHolder.localPosition = visualsHolderOriginalPos + (Vector3)(Random.insideUnitCircle * shakeMagnitude); e += Time.deltaTime; yield return null; }
        visualsHolder.localPosition = visualsHolderOriginalPos; isShaking = false;
    }

    private IEnumerator FlashDamageCoroutine()
    {
        currentRenderers.Clear(); visualsHolder?.GetComponentsInChildren<SpriteRenderer>(currentRenderers);
        foreach (var r in currentRenderers) if (r) r.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        foreach (var r in currentRenderers) if (r) r.color = Color.white;
        flashCoroutine = null; 
    }
}