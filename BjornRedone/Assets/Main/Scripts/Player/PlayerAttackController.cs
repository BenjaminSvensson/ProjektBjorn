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
    [SerializeField] private float baseProjectileKnockback = 5f; 
    
    [Header("Audio")]
    [SerializeField] private AudioSource actionAudioSource;

    private InputSystem_Actions playerControls;
    private bool isAttackHeld = false;
    private bool hasFiredSincePress = false; // --- NEW: Tracks semi-auto firing ---
    private bool isNextPunchLeft = true;
    
    private float leftArmCooldownTimer = 0f;
    private float rightArmCooldownTimer = 0f;
    private float globalCooldownTimer = 0f; 
    
    // Timer to prevent click sound spam
    private float clickSoundCooldownTimer = 0f;
    
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
        
        // --- NEW: Reset the trigger flag whenever we press the button ---
        if (callbackContext.performed)
        {
            hasFiredSincePress = false;
        }
    }

    void Update()
    {
        if (leftArmCooldownTimer > 0) leftArmCooldownTimer -= Time.deltaTime;
        if (rightArmCooldownTimer > 0) rightArmCooldownTimer -= Time.deltaTime;
        if (globalCooldownTimer > 0) globalCooldownTimer -= Time.deltaTime;
        if (clickSoundCooldownTimer > 0) clickSoundCooldownTimer -= Time.deltaTime;

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

        if (currentWeapon != null && currentWeapon.type == WeaponType.Ranged)
        {
            HandleRangedInput(currentWeapon);
        }
        else
        {
            TryMeleeAttack(currentWeapon);
        }
    }

    private void HandleRangedInput(WeaponData weapon)
    {
        // --- NEW: Check for Semi-Auto Logic ---
        if (!weapon.allowHoldToFire && hasFiredSincePress) return;

        // 1. Check Fire Rate Cooldown first (always applies)
        if (weaponSystem.GetCurrentWeaponCooldown() > 0) return;

        // 2. Check Reloading State
        if (weaponSystem.IsReloading())
        {
            TryPlayEmptySound(weapon);
            return;
        }

        // 3. Check Ammo
        int currentAmmo = weaponSystem.GetCurrentClipAmmo();
        if (currentAmmo <= 0)
        {
            // Empty Clip
            if (weaponSystem.GetTotalReserveAmmo() > 0)
            {
                // Has reserve? Start Reloading automatically
                weaponSystem.StartReload();
            }
            else
            {
                // No reserve? Click sound.
                TryPlayEmptySound(weapon);
            }
            return;
        }

        // 4. Fire!
        FireRangedWeapon(weapon);
    }

    private void TryPlayEmptySound(WeaponData weapon)
    {
        // Don't spam empty click for semi-auto if holding
        if (!weapon.allowHoldToFire && hasFiredSincePress) return;

        if (clickSoundCooldownTimer <= 0 && weapon.emptyClickSound != null)
        {
            if (actionAudioSource != null)
            {
                actionAudioSource.pitch = Random.Range(0.95f, 1.05f);
                actionAudioSource.PlayOneShot(weapon.emptyClickSound, 0.6f);
            }
            clickSoundCooldownTimer = 0.2f; 
            
            // Mark as "action taken" for semi-auto so it doesn't click every frame
            if (!weapon.allowHoldToFire) hasFiredSincePress = true; 
        }
    }

    private void FireRangedWeapon(WeaponData weapon)
    {
        if (weapon.projectilePrefab == null) return;
        
        int availableAmmo = weaponSystem.GetCurrentClipAmmo();
        int projectilesToFire = weapon.projectilesPerShot;

        // Cap projectiles to ammo available
        if (projectilesToFire > availableAmmo)
        {
            projectilesToFire = availableAmmo;
        }

        if (projectilesToFire <= 0) return;

        // Consume exact amount of ammo (1 bullet = 1 ammo)
        weaponSystem.ConsumeAmmo(projectilesToFire);
        
        // --- REVERTED: Safety Minimum Cooldown ---
        // Changed back to 0.1f since we now use allowHoldToFire for safety against accidental double-taps
        float cooldown = Mathf.Max(weapon.fireRate, 0.1f);
        weaponSystem.SetCurrentWeaponCooldown(cooldown);

        // --- NEW: Mark that we fired this press ---
        hasFiredSincePress = true;

        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 fireOrigin = weaponSystem.GetFirePoint();
        Vector2 aimDir = (mouseWorldPos - fireOrigin).normalized;

        float finalKnockback = baseProjectileKnockback;
        if (weapon != null) finalKnockback *= weapon.knockbackMultiplier;

        // Loop only for the amount of projectiles we can actually afford
        for (int i = 0; i < projectilesToFire; i++)
        {
            float currentSpread = Random.Range(-weapon.spread / 2f, weapon.spread / 2f);
            Vector2 finalDir = Quaternion.Euler(0, 0, currentSpread) * aimDir;

            GameObject projObj = Instantiate(weapon.projectilePrefab, fireOrigin, Quaternion.identity);
            Projectile projScript = projObj.GetComponent<Projectile>();
            
            if (projScript != null)
            {
                projScript.Initialize(finalDir, weapon.projectileSpeed, weapon.projectileDamage, finalKnockback, false);
            }
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

        if (meleeWeapon != null && weaponSystem != null && weaponSystem.GetCurrentWeaponCooldown() > 0) return;

        LimbData leftData = limbController.GetArmData(true);
        LimbData rightData = limbController.GetArmData(false);
        
        bool leftReady = leftData != null && leftArmCooldownTimer <= 0;
        bool rightReady = rightData != null && rightArmCooldownTimer <= 0;

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

                float fullCooldown = mainData.attackCooldown / Mathf.Max(0.1f, speedMult);
                weaponSystem.SetCurrentWeaponCooldown(fullCooldown);
                
                leftArmCooldownTimer = fullCooldown;
                rightArmCooldownTimer = fullCooldown;
                globalCooldownTimer = minPunchDelay;
            }
        }
        else
        {
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

        if (!damageOverride.HasValue)
        {
            if (weapon != null)
            {
                weaponSystem.SetCurrentWeaponCooldown(calculatedCooldown);
                if (isLeftArm) leftArmCooldownTimer = calculatedCooldown; 
                else rightArmCooldownTimer = calculatedCooldown;
            }
            else
            {
                if (isLeftArm) leftArmCooldownTimer = calculatedCooldown;
                else rightArmCooldownTimer = calculatedCooldown;
            }
            globalCooldownTimer = minPunchDelay;
        }

        if (playAudio && actionAudioSource != null && soundPool != null && soundPool.Length > 0)
        {
            AudioClip clip = soundPool[Random.Range(0, soundPool.Length)];
            if (clip != null)
            {
                actionAudioSource.pitch = armData.punchPitch * Random.Range(0.9f, 1.1f);
                actionAudioSource.PlayOneShot(clip, armData.punchVolume);
            }
        }

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
            
            if (hit.TryGetComponent<EnemyLimbController>(out EnemyLimbController enemy))
            {
                enemy.TakeDamage(damage, dir);
                if (hit.TryGetComponent<Rigidbody2D>(out Rigidbody2D enemyRb))
                {
                    enemyRb.linearVelocity = Vector2.zero; 
                    enemyRb.AddForce(dir * knockback, ForceMode2D.Impulse);
                }
            }
            else if (hit.TryGetComponent<LootContainer>(out LootContainer container))
            {
                container.TakeDamage(damage, dir);
            }
            else if (hit.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            {
                if (rb.bodyType == RigidbodyType2D.Dynamic && !hit.CompareTag("Player"))
                {
                    rb.AddForce(dir * knockback, ForceMode2D.Impulse);
                }
            }
        }
    }
}