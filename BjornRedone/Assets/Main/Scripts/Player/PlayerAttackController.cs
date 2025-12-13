using UnityEngine;
using UnityEngine.InputSystem; 

/// <summary>
/// This script handles all player attack logic.
/// </summary>
public class PlayerAttackController : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private PlayerLimbController limbController;
    [SerializeField] private PlayerAnimationController animController;

    [Header("Attack Settings")]
    [Tooltip("The layer(s) that can be hit by a punch. MAKE SURE ENEMIES ARE ON THIS LAYER.")]
    [SerializeField] private LayerMask hittableLayers;
    [Tooltip("Minimum time (in seconds) between any two punches to prevent simultaneous attacks. Creates a rhythm.")]
    [SerializeField] private float minPunchDelay = 0.15f;
    
    [Header("Audio")]
    [Tooltip("The AudioSource used for action sounds (punching). Assign this in the Inspector.")]
    [SerializeField] private AudioSource actionAudioSource;

    // --- Private State ---
    private InputSystem_Actions playerControls;
    private bool isAttackHeld = false;
    private bool isNextPunchLeft = true;
    
    // --- Independent Cooldowns ---
    private float leftArmCooldownTimer = 0f;
    private float rightArmCooldownTimer = 0f;
    private float globalCooldownTimer = 0f; 
    
    private Camera cam;

    private Collider2D[] hitBuffer = new Collider2D[10]; 

    void Awake()
    {
        playerControls = new InputSystem_Actions();
        
        cam = Camera.main;
        if (cam == null)
        {
            cam = FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("PlayerAttackController: NO CAMERA FOUND! Please tag your camera as 'MainCamera'.");
            }
        }

        if (limbController == null)
            limbController = GetComponent<PlayerLimbController>();
        if (animController == null)
            animController = GetComponent<PlayerAnimationController>();
            
        if (actionAudioSource == null)
        {
            Debug.LogWarning("PlayerAttackController is missing a reference to the Action Audio Source!");
        }
    }

    void OnEnable()
    {
        playerControls.Player.Attack.performed += HandleAttack;
        playerControls.Player.Attack.canceled += HandleAttack;
        playerControls.Player.Enable();
    }

    void OnDisable()
    {
        playerControls.Player.Attack.performed -= HandleAttack;
        playerControls.Player.Attack.canceled -= HandleAttack;
        playerControls.Player.Disable();
    }

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
                TryPunch();
            }
        }
    }

    private void TryPunch()
    {
        if (globalCooldownTimer > 0) return;

        LimbData leftData = limbController.GetArmData(true);
        LimbData rightData = limbController.GetArmData(false);
        
        bool leftReady = leftData != null && leftArmCooldownTimer <= 0;
        bool rightReady = rightData != null && rightArmCooldownTimer <= 0;

        if (!leftReady && !rightReady) return;

        bool fireLeft = false;

        if (leftReady && rightReady)
        {
            fireLeft = isNextPunchLeft;
        }
        else if (leftReady)
        {
            fireLeft = true;
        }
        else if (rightReady)
        {
            fireLeft = false;
        }

        if (fireLeft)
        {
            PerformPunch(limbController.GetLeftArmSlot(), leftData, true);
            isNextPunchLeft = false; 
        }
        else
        {
            PerformPunch(limbController.GetRightArmSlot(), rightData, false);
            isNextPunchLeft = true;
        }
    }

    private void PerformPunch(Transform armTransform, LimbData armData, bool isLeftArm)
    {
        if (cam == null) return; 
        if (Mouse.current == null) return; 

        float damage = limbController.baseAttackDamage + armData.attackDamageBonus;
        float reach = armData.attackReach;
        float radius = armData.impactSize;
        float duration = armData.punchDuration;
        float cooldown = armData.attackCooldown;
        float knockback = armData.knockbackForce;

        if (isLeftArm)
            leftArmCooldownTimer = cooldown;
        else
            rightArmCooldownTimer = cooldown;
            
        globalCooldownTimer = minPunchDelay;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        float depth = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 screenPosWithZ = new Vector3(mouseScreenPos.x, mouseScreenPos.y, depth);
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(screenPosWithZ);

        Vector2 punchDirection = (mouseWorldPos - (Vector2)armTransform.position).normalized;
        Vector2 hitPosition = (Vector2)armTransform.position + (punchDirection * reach);

        if (actionAudioSource != null && armData.punchSounds != null && armData.punchSounds.Length > 0)
        {
            AudioClip clipToPlay = armData.punchSounds[Random.Range(0, armData.punchSounds.Length)];
            if (clipToPlay != null)
            {
                actionAudioSource.pitch = armData.punchPitch;
                actionAudioSource.PlayOneShot(clipToPlay, armData.punchVolume);
            }
        }

        if (animController != null)
        {
            animController.TriggerPunch(armTransform, duration, hitPosition);
        }

        int hitCount = Physics2D.OverlapCircleNonAlloc(hitPosition, radius, hitBuffer, hittableLayers);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hitBuffer[i];
            
            if (hit.TryGetComponent<EnemyLimbController>(out EnemyLimbController enemy))
            {
                // --- PASS DIRECTION HERE ---
                enemy.TakeDamage(damage, punchDirection);

                if (hit.TryGetComponent<Rigidbody2D>(out Rigidbody2D enemyRb))
                {
                    enemyRb.linearVelocity = Vector2.zero; 
                    enemyRb.AddForce(punchDirection * knockback, ForceMode2D.Impulse);
                }

                Debug.Log($"Hit Enemy {hit.name} for {damage} damage!");
            }
        }
    }
}