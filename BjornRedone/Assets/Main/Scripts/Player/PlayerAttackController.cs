using UnityEngine;
using UnityEngine.InputSystem; 

public class PlayerAttackController : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private PlayerLimbController limbController;
    [SerializeField] private PlayerAnimationController animController;
    [SerializeField] private WeaponSystem weaponSystem; 

    [Header("Attack Settings")]
    [SerializeField] private LayerMask hittableLayers;
    [SerializeField] private float minPunchDelay = 0.15f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource actionAudioSource;

    private InputSystem_Actions playerControls;
    private bool isAttackHeld = false;
    private bool isNextPunchLeft = true;
    
    // Cooldowns
    private float leftArmCooldownTimer = 0f;
    private float rightArmCooldownTimer = 0f;
    private float globalCooldownTimer = 0f; 
    // rangedCooldownTimer removed (now tracked in WeaponSystem)
    
    private Camera cam;
    private Collider2D[] hitBuffer = new Collider2D[10]; 

    void Awake()
    {
        playerControls = new InputSystem_Actions();
        cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();

        if (limbController == null) limbController = GetComponent<PlayerLimbController>();
        if (animController == null) animController = GetComponent<PlayerAnimationController>();
        if (weaponSystem == null) weaponSystem = GetComponent<WeaponSystem>();
    }

    void OnEnable() { playerControls.Player.Attack.performed += HandleAttack; playerControls.Player.Attack.canceled += HandleAttack; playerControls.Player.Enable(); }
    void OnDisable() { playerControls.Player.Attack.performed -= HandleAttack; playerControls.Player.Attack.canceled -= HandleAttack; playerControls.Player.Disable(); }

    private void HandleAttack(InputAction.CallbackContext callbackContext)
    {
        isAttackHeld = callbackContext.performed;
    }

    void Update()
    {
        if (leftArmCooldownTimer > 0) leftArmCooldownTimer -= Time.deltaTime;
        if (rightArmCooldownTimer > 0) rightArmCooldownTimer -= Time.deltaTime;
        if (globalCooldownTimer > 0) globalCooldownTimer -= Time.deltaTime;

        if (isAttackHeld)
        {
            if (limbController.CanAttack() && !limbController.CanCrawl())
            {
                HandleCombatInput();
            }
        }
    }

    private void HandleCombatInput()
    {
        WeaponData currentWeapon = weaponSystem != null ? weaponSystem.GetActiveWeapon() : null;

        // --- NEW: Check Weapon Slot Cooldown first ---
        if (currentWeapon != null && weaponSystem != null)
        {
            if (weaponSystem.GetCurrentWeaponCooldown() > 0) return;
        }

        if (currentWeapon != null && currentWeapon.type == WeaponType.Ranged)
        {
            FireRangedWeapon(currentWeapon);
        }
        else
        {
            TryMeleeAttack(currentWeapon);
        }
    }

    private void FireRangedWeapon(WeaponData weapon)
    {
        if (weapon.projectilePrefab == null) return;
        
        // --- NEW: Set Weapon Slot Cooldown ---
        weaponSystem.SetCurrentWeaponCooldown(weapon.fireRate);

        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 fireOrigin = transform.position; 
        Vector2 aimDir = (mouseWorldPos - fireOrigin).normalized;

        for (int i = 0; i < weapon.projectilesPerShot; i++)
        {
            float currentSpread = Random.Range(-weapon.spread / 2f, weapon.spread / 2f);
            Vector2 finalDir = Quaternion.Euler(0, 0, currentSpread) * aimDir;

            GameObject projObj = Instantiate(weapon.projectilePrefab, fireOrigin, Quaternion.identity);
            Projectile projScript = projObj.GetComponent<Projectile>();
            
            if (projScript != null)
                projScript.Initialize(finalDir, weapon.projectileSpeed, weapon.projectileDamage);
        }

        if (actionAudioSource != null && weapon.shootSounds != null && weapon.shootSounds.Length > 0)
        {
            AudioClip clip = weapon.shootSounds[Random.Range(0, weapon.shootSounds.Length)];
            if (clip != null)
            {
                actionAudioSource.pitch = Random.Range(0.9f, 1.1f);
                actionAudioSource.PlayOneShot(clip);
            }
        }
    }

    private void TryMeleeAttack(WeaponData meleeWeapon)
    {
        if (globalCooldownTimer > 0) return;

        LimbData leftData = limbController.GetArmData(true);
        LimbData rightData = limbController.GetArmData(false);
        
        bool leftReady = leftData != null && leftArmCooldownTimer <= 0;
        bool rightReady = rightData != null && rightArmCooldownTimer <= 0;

        // Note: For weapon attacks, we rely mainly on Weapon Cooldown (checked in HandleCombatInput).
        // However, we still check physical arm readiness to ensure we don't interrupt a swing animation.
        // But if switching weapons, the user might want to interrupt.
        // For robustness: we check arm readiness, but when firing a weapon, we only reserve the arm for the SWING DURATION.

        if (!leftReady && !rightReady) return;

        float speedMult = 1.0f;
        if (meleeWeapon != null) speedMult = meleeWeapon.attackSpeedMultiplier;

        if (meleeWeapon != null && leftData != null && rightData != null)
        {
            if (leftReady && rightReady) 
            {
                bool isRightMain = weaponSystem.IsHoldingWithRightHand();
                Transform mainArm = isRightMain ? limbController.GetRightArmSlot() : limbController.GetLeftArmSlot();
                LimbData mainData = isRightMain ? rightData : leftData;
                float totalDamage = limbController.baseAttackDamage + leftData.attackDamageBonus + rightData.attackDamageBonus + meleeWeapon.meleeDamageBonus;
                
                ExecuteMelee(mainArm, mainData, !isRightMain, meleeWeapon, speedMult, true, totalDamage);

                // --- KEY CHANGE: Arm Cooldown vs Weapon Cooldown ---
                // We set the WEAPON slot to the full cooldown (refire rate).
                // We set the ARM to only the swing duration (so it frees up for a switch-combo).
                float fullCooldown = mainData.attackCooldown / Mathf.Max(0.1f, speedMult);
                float swingDuration = mainData.punchDuration / Mathf.Max(0.1f, speedMult);
                
                weaponSystem.SetCurrentWeaponCooldown(fullCooldown);
                
                leftArmCooldownTimer = swingDuration;
                rightArmCooldownTimer = swingDuration;
                globalCooldownTimer = minPunchDelay;
            }
        }
        else
        {
            // Standard / Unarmed
            bool fireLeft = false;
            if (leftReady && rightReady) fireLeft = isNextPunchLeft;
            else if (leftReady) fireLeft = true;
            else if (rightReady) fireLeft = false;

            if (fireLeft)
            {
                ExecuteMelee(limbController.GetLeftArmSlot(), leftData, true, meleeWeapon, speedMult, true);
                isNextPunchLeft = false; 
            }
            else
            {
                ExecuteMelee(limbController.GetRightArmSlot(), rightData, false, meleeWeapon, speedMult, true);
                isNextPunchLeft = true;
            }
        }
    }

    private void ExecuteMelee(Transform armTransform, LimbData armData, bool isLeftArm, WeaponData weapon, float speedMult, bool playAudio, float? damageOverride = null)
    {
        if (cam == null || Mouse.current == null) return; 

        // 1. Calculate Stats
        float damage = damageOverride.HasValue ? damageOverride.Value : (limbController.baseAttackDamage + armData.attackDamageBonus);
        float knockback = armData.knockbackForce;
        AudioClip[] soundPool = armData.punchSounds;
        
        float calculatedDuration = armData.punchDuration / Mathf.Max(0.1f, speedMult);
        float calculatedCooldown = armData.attackCooldown / Mathf.Max(0.1f, speedMult);

        bool hasWeaponBonus = false;
        if (weapon != null && weaponSystem != null)
        {
            if (!damageOverride.HasValue)
            {
                bool isHoldingWithRight = weaponSystem.IsHoldingWithRightHand();
                if ((isLeftArm && !isHoldingWithRight) || (!isLeftArm && isHoldingWithRight))
                {
                    damage += weapon.meleeDamageBonus;
                    knockback *= weapon.knockbackMultiplier;
                    hasWeaponBonus = true;
                }
            }
            else
            {
                knockback *= weapon.knockbackMultiplier;
                hasWeaponBonus = true;
            }

            if (weapon.meleeImpactSounds != null && weapon.meleeImpactSounds.Length > 0)
                soundPool = weapon.meleeImpactSounds; 
        }

        // 2. Set Cooldowns (If not already handled by Two-Handed logic)
        if (!damageOverride.HasValue)
        {
            if (weapon != null)
            {
                // Weapon Equipped: Weapon takes full cooldown, Arm takes animation duration
                weaponSystem.SetCurrentWeaponCooldown(calculatedCooldown);
                if (isLeftArm) leftArmCooldownTimer = calculatedDuration;
                else rightArmCooldownTimer = calculatedDuration;
            }
            else
            {
                // Unarmed: Arm takes full cooldown
                if (isLeftArm) leftArmCooldownTimer = calculatedCooldown;
                else rightArmCooldownTimer = calculatedCooldown;
            }
            globalCooldownTimer = minPunchDelay;
        }

        // 3. Audio
        if (playAudio && actionAudioSource != null && soundPool != null && soundPool.Length > 0)
        {
            AudioClip clip = soundPool[Random.Range(0, soundPool.Length)];
            if (clip != null)
            {
                actionAudioSource.pitch = armData.punchPitch * Random.Range(0.9f, 1.1f);
                actionAudioSource.PlayOneShot(clip, armData.punchVolume);
            }
        }

        // 4. Visuals & Hits
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        
        if (weapon != null && hasWeaponBonus && weapon.attackStyle == MeleeAttackStyle.Swing)
        {
            float swingDirection = (mouseWorldPos.x < transform.position.x) ? 1f : -1f;
            float finalArc = Mathf.Abs(weapon.swingArc) * swingDirection;

            animController.TriggerSwing(armTransform, calculatedDuration, mouseWorldPos, finalArc);
            StartCoroutine(DelayedSwingHit(armTransform, mouseWorldPos, damage, knockback, armData, weapon.swingArc, calculatedDuration * 0.5f));
        }
        else
        {
            Vector2 punchDirection = (mouseWorldPos - (Vector2)armTransform.position).normalized;
            Vector2 hitPosition = (Vector2)armTransform.position + (punchDirection * armData.attackReach);

            animController.TriggerPunch(armTransform, calculatedDuration, hitPosition);
            CheckHit(hitPosition, armData.impactSize, damage, knockback, punchDirection, armData);
        }
    }

    private System.Collections.IEnumerator DelayedSwingHit(Transform arm, Vector2 targetPos, float dmg, float kb, LimbData data, float arc, float delay)
    {
        yield return new WaitForSeconds(delay);
        Vector2 dirToTarget = (targetPos - (Vector2)arm.position).normalized;
        Vector2 hitCenter = (Vector2)arm.position + (dirToTarget * data.attackReach);
        CheckHit(hitCenter, data.impactSize * 1.5f, dmg, kb, dirToTarget, data);
    }

    private void CheckHit(Vector2 pos, float radius, float damage, float knockback, Vector2 dir, LimbData data)
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(pos, radius, hitBuffer, hittableLayers);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hitBuffer[i];
            
            // --- UPDATED: Check for Enemies ---
            if (hit.TryGetComponent<EnemyLimbController>(out EnemyLimbController enemy))
            {
                enemy.TakeDamage(damage, dir);
                if (hit.TryGetComponent<Rigidbody2D>(out Rigidbody2D enemyRb))
                {
                    enemyRb.linearVelocity = Vector2.zero; 
                    enemyRb.AddForce(dir * knockback, ForceMode2D.Impulse);
                }
            }
            // --- NEW: Check for Loot Containers ---
            else if (hit.TryGetComponent<LootContainer>(out LootContainer container))
            {
                container.TakeDamage(damage, dir);
            }
        }
    }
}