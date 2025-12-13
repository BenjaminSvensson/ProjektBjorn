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
    private float rangedCooldownTimer = 0f; 
    
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
        if (rangedCooldownTimer > 0) rangedCooldownTimer -= Time.deltaTime;

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
            if (rangedCooldownTimer <= 0)
            {
                FireRangedWeapon(currentWeapon);
            }
        }
        else
        {
            TryMeleeAttack(currentWeapon);
        }
    }

    private void FireRangedWeapon(WeaponData weapon)
    {
        if (weapon.projectilePrefab == null) return;
        rangedCooldownTimer = weapon.fireRate;

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

        if (!leftReady && !rightReady) return;

        float speedMult = 1.0f;
        if (meleeWeapon != null) speedMult = meleeWeapon.attackSpeedMultiplier;

        // --- TWO-HANDED ATTACK LOGIC ---
        // If holding a weapon and BOTH arms are attached, we do a unified swing.
        // The WeaponSystem will visually drag the off-hand along.
        if (meleeWeapon != null && leftData != null && rightData != null)
        {
            if (leftReady && rightReady) // Both cooldowns ready
            {
                // Determine Main Hand (WeaponSystem prioritizes Right)
                bool isRightMain = weaponSystem.IsHoldingWithRightHand();
                Transform mainArm = isRightMain ? limbController.GetRightArmSlot() : limbController.GetLeftArmSlot();
                LimbData mainData = isRightMain ? rightData : leftData;

                // Calculate Combined Damage (Base + MainBonus + OffBonus + WeaponBonus)
                float totalDamage = limbController.baseAttackDamage + leftData.attackDamageBonus + rightData.attackDamageBonus + meleeWeapon.meleeDamageBonus;
                
                // Execute ONE swing on the Main Arm
                ExecuteMelee(mainArm, mainData, !isRightMain, meleeWeapon, speedMult, true, totalDamage);

                // Set cooldowns for BOTH arms
                float cooldown = mainData.attackCooldown / Mathf.Max(0.1f, speedMult);
                leftArmCooldownTimer = cooldown;
                rightArmCooldownTimer = cooldown;
                globalCooldownTimer = minPunchDelay;
            }
        }
        else
        {
            // --- SINGLE HAND / ALTERNATING LOGIC ---
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
            // Note: If using damageOverride (Two-handed), bonuses are already included.
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
                // Even with override, we apply knockback mult and sound
                knockback *= weapon.knockbackMultiplier;
                hasWeaponBonus = true;
            }

            if (weapon.meleeImpactSounds != null && weapon.meleeImpactSounds.Length > 0)
                soundPool = weapon.meleeImpactSounds; 
        }

        // 2. Set Cooldown (Only if not already handled by caller)
        // If damageOverride is null, we assume single-hand logic which sets timers here
        if (!damageOverride.HasValue)
        {
            if (isLeftArm) leftArmCooldownTimer = calculatedCooldown;
            else rightArmCooldownTimer = calculatedCooldown;
            globalCooldownTimer = minPunchDelay;
        }

        // 3. Play Audio
        if (playAudio && actionAudioSource != null && soundPool != null && soundPool.Length > 0)
        {
            AudioClip clip = soundPool[Random.Range(0, soundPool.Length)];
            if (clip != null)
            {
                actionAudioSource.pitch = armData.punchPitch * Random.Range(0.9f, 1.1f);
                actionAudioSource.PlayOneShot(clip, armData.punchVolume);
            }
        }

        // 4. Perform Attack Visuals & Hit
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        
        if (weapon != null && hasWeaponBonus && weapon.attackStyle == MeleeAttackStyle.Swing)
        {
            animController.TriggerSwing(armTransform, calculatedDuration, mouseWorldPos, weapon.swingArc);
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
        }
    }
}