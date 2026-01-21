using UnityEngine;
using UnityEngine.InputSystem; 
using System.Collections; 

public class PlayerAttackController : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private PlayerLimbController limbController;
    [SerializeField] private PlayerAnimationController animController;
    [SerializeField] private WeaponSystem weaponSystem; 
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Attack Settings")]
    [SerializeField] private LayerMask hittableLayers;
    [SerializeField] private float minPunchDelay = 0.15f;
    [SerializeField] private float baseProjectileKnockback = 5f; 
    
    [Header("Kick Settings")]
    [SerializeField] private float kickDamage = 10f;
    [SerializeField] private float kickReach = 1.2f;
    [SerializeField] private float kickRadius = 0.5f;
    [SerializeField] private float kickKnockback = 8f;
    [SerializeField] private float kickDuration = 0.4f;
    [SerializeField] private float kickCooldown = 1.0f;
    [SerializeField] private AudioClip[] kickSounds;

    [Header("Audio")]
    [SerializeField] private AudioSource actionAudioSource;

    private InputSystem_Actions playerControls;
    private bool isAttackHeld = false;
    private bool hasFiredSincePress = false; 
    private bool isNextPunchLeft = true;
    
    private float leftArmCooldownTimer = 0f;
    private float rightArmCooldownTimer = 0f;
    private float globalCooldownTimer = 0f; 
    private float kickCooldownTimer = 0f; 
    
    private float clickSoundCooldownTimer = 0f;
    private Multipliers multiplier;
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
        if (multiplier == null)  multiplier = GetComponent<Multipliers>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
    }

    void OnEnable() 
    { 
        playerControls.Player.Attack.performed += HandleAttack; 
        playerControls.Player.Attack.canceled += HandleAttack; 
        playerControls.Player.Kick.performed += HandleKick;
        playerControls.Player.Enable(); 
    }

    void OnDisable() 
    { 
        playerControls.Player.Attack.performed -= HandleAttack; 
        playerControls.Player.Attack.canceled -= HandleAttack; 
        playerControls.Player.Kick.performed -= HandleKick;
        playerControls.Player.Disable(); 
    }

    private void HandleAttack(InputAction.CallbackContext callbackContext)
    {
        isAttackHeld = callbackContext.performed;
        if (callbackContext.performed) hasFiredSincePress = false;
    }

    private void HandleKick(InputAction.CallbackContext context)
    {
        if (kickCooldownTimer <= 0 && globalCooldownTimer <= 0)
        {
            StartCoroutine(PerformKick());
        }
    }

    void Update()
    {
        if (leftArmCooldownTimer > 0) leftArmCooldownTimer -= Time.deltaTime;
        if (rightArmCooldownTimer > 0) rightArmCooldownTimer -= Time.deltaTime;
        if (globalCooldownTimer > 0) globalCooldownTimer -= Time.deltaTime;
        if (clickSoundCooldownTimer > 0) clickSoundCooldownTimer -= Time.deltaTime;
        if (kickCooldownTimer > 0) kickCooldownTimer -= Time.deltaTime; 

        if (isAttackHeld)
        {
            if (limbController.CanAttack() && !limbController.CanCrawl())
            {
                HandleCombatInput();
            }
        }
    }

    private IEnumerator PerformKick()
    {
        // --- 1. Smart Leg Selection (Take Over Logic) ---
        Transform leftLegSlot = limbController.GetLeftLegSlot();
        Transform rightLegSlot = limbController.GetRightLegSlot();

        bool hasLeft = leftLegSlot != null;
        bool hasRight = rightLegSlot != null;

        if (!hasLeft && !hasRight) yield break;

        bool useLeftLeg;
        if (hasLeft && hasRight)
        {
            useLeftLeg = Random.value > 0.5f;
        }
        else
        {
            useLeftLeg = hasLeft; 
        }

        // --- 2. Cooldowns & Locking ---
        kickCooldownTimer = kickCooldown;
        globalCooldownTimer = 0.2f; 
        if (playerMovement != null) playerMovement.SetMovementLocked(true);

        // --- 3. Accurate Mouse Aiming ---
        if (cam == null) cam = Camera.main;
        Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
        mouseScreenPos.z = cam.nearClipPlane + 10f; 
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

        // --- 4. Animation ---
        animController.TriggerKick(useLeftLeg, kickDuration, mouseWorldPos, kickReach);

        if (actionAudioSource != null && kickSounds != null && kickSounds.Length > 0)
        {
            actionAudioSource.pitch = Random.Range(0.8f, 1.0f); 
            actionAudioSource.PlayOneShot(kickSounds[Random.Range(0, kickSounds.Length)]);
        }

        // --- 5. Calculate Hitbox ---
        Vector2 kickOrigin = transform.position; 
        if (useLeftLeg && leftLegSlot != null) kickOrigin = leftLegSlot.position;
        else if (!useLeftLeg && rightLegSlot != null) kickOrigin = rightLegSlot.position;

        Vector2 dir = (mouseWorldPos - (Vector2)transform.position).normalized;
        Vector2 hitPos = kickOrigin + (dir * kickReach);

        float totalDamage = kickDamage * ((multiplier != null) ? multiplier.strength : 1f);
        float totalKnockback = kickKnockback * ((multiplier != null) ? multiplier.strength : 1f);

        CheckHit(hitPos, kickRadius, totalDamage, totalKnockback, dir, null);

        // Debug Draw (Line only, Sphere removed to fix error)
        Debug.DrawLine(kickOrigin, hitPos, Color.red, 1.0f);

        yield return new WaitForSeconds(kickDuration);

        if (playerMovement != null) playerMovement.SetMovementLocked(false);
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
        if (!weapon.allowHoldToFire && hasFiredSincePress) return;
        if (weaponSystem.GetCurrentWeaponCooldown() > 0) return;
        if (weaponSystem.IsReloading()) { TryPlayEmptySound(weapon); return; }

        int currentAmmo = weaponSystem.GetCurrentClipAmmo();
        if (currentAmmo <= 0)
        {
            if (weaponSystem.GetTotalReserveAmmo() > 0) weaponSystem.StartReload();
            else TryPlayEmptySound(weapon);
            return;
        }
        FireRangedWeapon(weapon);
    }

    private void TryPlayEmptySound(WeaponData weapon)
    {
        if (!weapon.allowHoldToFire && hasFiredSincePress) return;
        if (clickSoundCooldownTimer <= 0 && weapon.emptyClickSound != null)
        {
            if (actionAudioSource != null)
            {
                actionAudioSource.pitch = Random.Range(0.95f, 1.05f);
                actionAudioSource.PlayOneShot(weapon.emptyClickSound, 0.6f);
            }
            clickSoundCooldownTimer = 0.2f; 
            if (!weapon.allowHoldToFire) hasFiredSincePress = true; 
        }
    }

    private void FireRangedWeapon(WeaponData weapon)
    {
        if (weapon.projectilePrefab == null) return;
        int availableAmmo = weaponSystem.GetCurrentClipAmmo();
        int projectilesToFire = Mathf.Min(weapon.projectilesPerShot, availableAmmo);
        if (projectilesToFire <= 0) return;

        weaponSystem.ConsumeAmmo(projectilesToFire);
        weaponSystem.SetCurrentWeaponCooldown(Mathf.Max(weapon.fireRate, 0.1f));
        hasFiredSincePress = true;

        if (RoomCamera.Instance != null && weapon.screenShakeAmount > 0) RoomCamera.Instance.Shake(0.1f, weapon.screenShakeAmount);

        Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
        mouseScreenPos.z = cam.nearClipPlane + 10f;
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

        Vector2 fireOrigin = weaponSystem.GetFirePoint();
        Vector2 aimDir = (mouseWorldPos - fireOrigin).normalized;

        float finalKnockback = baseProjectileKnockback * weapon.knockbackMultiplier;

        for (int i = 0; i < projectilesToFire; i++)
        {
            float currentSpread = Random.Range(-weapon.spread / 2f, weapon.spread / 2f);
            Vector2 finalDir = Quaternion.Euler(0, 0, currentSpread) * aimDir;
            GameObject projObj = Instantiate(weapon.projectilePrefab, fireOrigin, Quaternion.identity);
            Projectile projScript = projObj.GetComponent<Projectile>();
            if (projScript != null) projScript.Initialize(finalDir, weapon.projectileSpeed, weapon.projectileDamage, finalKnockback, false);
        }

        if (actionAudioSource != null && weapon.shootSounds != null && weapon.shootSounds.Length > 0)
        {
            actionAudioSource.pitch = Random.Range(0.9f, 1.1f);
            actionAudioSource.PlayOneShot(weapon.shootSounds[Random.Range(0, weapon.shootSounds.Length)]);
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

        float speedMult = meleeWeapon != null ? meleeWeapon.attackSpeedMultiplier : 1.0f;

        if (meleeWeapon != null && leftData != null && rightData != null)
        {
            if (leftReady && rightReady) 
            {
                bool isRightMain = weaponSystem.IsHoldingWithRightHand();
                Transform mainArm = isRightMain ? limbController.GetRightArmSlot() : limbController.GetLeftArmSlot();
                LimbData mainData = isRightMain ? rightData : leftData;
                float totalDamage = limbController.baseAttackDamage + mainData.attackDamageBonus + meleeWeapon.meleeDamageBonus;

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
            bool fireLeft = (leftReady && rightReady) ? isNextPunchLeft : leftReady;

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
        float strengthMult = (multiplier != null) ? multiplier.strength : 1f;
        float damage = damageOverride.HasValue ? damageOverride.Value : (limbController.baseAttackDamage + armData.attackDamageBonus);
        float knockback = armData.knockbackForce;
        AudioClip[] soundPool = armData.punchSounds;

        bool hasWeaponBonus = false;
        if (weapon != null && weaponSystem != null)
        {
            bool isHoldingWithRight = weaponSystem.IsHoldingWithRightHand();
            if (damageOverride.HasValue || (isLeftArm && !isHoldingWithRight) || (!isLeftArm && isHoldingWithRight))
            {
                if (!damageOverride.HasValue) damage += weapon.meleeDamageBonus;
                knockback *= weapon.knockbackMultiplier;
                hasWeaponBonus = true;
            }
            if (weapon.meleeImpactSounds != null && weapon.meleeImpactSounds.Length > 0) soundPool = weapon.meleeImpactSounds;
        }

        damage *= strengthMult;
        knockback *= strengthMult;
        float calculatedDuration = armData.punchDuration / Mathf.Max(0.1f, speedMult);
        float calculatedCooldown = armData.attackCooldown / Mathf.Max(0.1f, speedMult);

        if (!damageOverride.HasValue)
        {
            if (weapon != null) weaponSystem.SetCurrentWeaponCooldown(calculatedCooldown);
            if (isLeftArm) leftArmCooldownTimer = calculatedCooldown;
            else rightArmCooldownTimer = calculatedCooldown;
            globalCooldownTimer = minPunchDelay;
        }

        if (playAudio && actionAudioSource != null && soundPool != null && soundPool.Length > 0)
        {
            actionAudioSource.pitch = armData.punchPitch * Random.Range(0.9f, 1.1f);
            actionAudioSource.PlayOneShot(soundPool[Random.Range(0, soundPool.Length)], armData.punchVolume);
        }

        Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
        mouseScreenPos.z = cam.nearClipPlane + 10f;
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

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

    private IEnumerator DelayedSwingHit(Transform arm, Vector2 targetPos, float dmg, float kb, LimbData data, float arc, float delay)
    {
        yield return new WaitForSeconds(delay);
        Vector2 dirToTarget = (targetPos - (Vector2)arm.position).normalized;
        Vector2 hitCenter = (Vector2)arm.position + (dirToTarget * data.attackReach);
        CheckHit(hitCenter, data.impactSize * 1.5f, dmg, kb, dirToTarget, data);
    }

    private void CheckHit(Vector2 pos, float radius, float damage, float knockback, Vector2 dir, LimbData data)
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(pos, radius, hitBuffer, hittableLayers);
        bool brokeWeapon = false; 

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hitBuffer[i];
            bool validHit = false;
            
            if (hit.TryGetComponent<EnemyLimbController>(out EnemyLimbController enemy))
            {
                enemy.TakeDamage(damage, dir);
                if (hit.TryGetComponent<Rigidbody2D>(out Rigidbody2D enemyRb))
                {
                    enemyRb.linearVelocity = Vector2.zero; 
                    enemyRb.AddForce(dir * knockback, ForceMode2D.Impulse);
                }
                validHit = true;
            }
            else if (hit.TryGetComponent<BirdEnemyAI>(out BirdEnemyAI bird))
            {
                bird.TakeDamage(damage);
                if (hit.TryGetComponent<Rigidbody2D>(out Rigidbody2D birdRb))
                {
                    birdRb.linearVelocity = Vector2.zero;
                    birdRb.AddForce(dir * knockback, ForceMode2D.Impulse);
                }
                validHit = true;
            }
            else if (hit.TryGetComponent<LootContainer>(out LootContainer container))
            {
                container.TakeDamage(damage, dir);
                validHit = true;
            }
            else if (hit.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            {
                if (rb.bodyType == RigidbodyType2D.Dynamic && !hit.CompareTag("Player"))
                {
                    rb.AddForce(dir * knockback, ForceMode2D.Impulse);
                    validHit = true;
                }
            }

            if (validHit && !brokeWeapon && data != null)
            {
                WeaponData w = weaponSystem.GetActiveWeapon();
                if (w != null && w.breaksOnMeleeHit)
                {
                    weaponSystem.BreakActiveWeapon();
                    brokeWeapon = true;
                }
            }
        }
    }
}