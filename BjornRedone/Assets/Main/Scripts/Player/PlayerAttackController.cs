using UnityEngine;
using UnityEngine.InputSystem; // To listen for the Attack action

/// <summary>
/// This script handles all player attack logic.
/// It should be placed on the root "Player" GameObject.
/// </summary>
public class PlayerAttackController : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private PlayerLimbController limbController;
    [SerializeField] private PlayerAnimationController animController;

    [Header("Attack Settings")]
    [Tooltip("The layer(s) that can be hit by a punch. MAKE SURE ENEMIES ARE ON THIS LAYER.")]
    [SerializeField] private LayerMask hittableLayers;
    
    [Header("Audio")]
    [Tooltip("The AudioSource used for action sounds (punching). Assign this in the Inspector.")]
    [SerializeField] private AudioSource actionAudioSource;

    // --- Private State ---
    private InputSystem_Actions playerControls;
    private bool isAttackHeld = false;
    private bool isNextPunchLeft = true;
    private float attackCooldownTimer = 0f;
    private Camera cam;

    // --- OPTIMIZATION: Physics Buffer ---
    // We allocate this once to reuse memory, preventing lag spikes during combat.
    private Collider2D[] hitBuffer = new Collider2D[10]; 

    void Awake()
    {
        playerControls = new InputSystem_Actions();
        
        // --- FIX: Robust Camera Finding ---
        cam = Camera.main;
        if (cam == null)
        {
            // Try to find ANY camera if MainCamera tag is missing
            cam = FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("PlayerAttackController: NO CAMERA FOUND! Please tag your camera as 'MainCamera'.");
            }
        }
        // --- END FIX ---

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
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        if (isAttackHeld && attackCooldownTimer <= 0)
        {
            if (limbController.CanAttack() && !limbController.CanCrawl())
            {
                TryPunch();
            }
        }
    }

    private void TryPunch()
    {
        Transform punchingArm = null;
        LimbData armData = null;
        bool isLeft = isNextPunchLeft;

        // 1. Try the "next" arm (alternating)
        if (isNextPunchLeft)
        {
            punchingArm = limbController.GetLeftArmSlot();
            armData = limbController.GetArmData(true);
            
            // If that arm is missing, fall back to the other one
            if (armData == null)
            {
                punchingArm = limbController.GetRightArmSlot();
                armData = limbController.GetArmData(false);
                isLeft = false;
            }
        }
        else
        {
            punchingArm = limbController.GetRightArmSlot();
            armData = limbController.GetArmData(false);
            isLeft = false;
            
            // Fallback
            if (armData == null)
            {
                punchingArm = limbController.GetLeftArmSlot();
                armData = limbController.GetArmData(true);
                isLeft = true;
            }
        }
        
        // If we still have no arm, do nothing
        if (armData == null || punchingArm == null)
        {
            return;
        }

        PerformPunch(punchingArm, armData);
        isNextPunchLeft = !isNextPunchLeft;
    }

    private void PerformPunch(Transform armTransform, LimbData armData)
    {
        // --- FIX: Safety Checks ---
        if (cam == null) 
        {
            Debug.LogError("Cannot punch: Camera is missing!");
            return; 
        }
        if (Mouse.current == null)
        {
            return; 
        }

        float damage = limbController.baseAttackDamage + armData.attackDamageBonus;
        float reach = armData.attackReach;
        float radius = armData.impactSize;
        float duration = armData.punchDuration;
        float cooldown = armData.attackCooldown;
        float knockback = armData.knockbackForce; // Get knockback from LimbData

        attackCooldownTimer = cooldown;

        // Get mouse position safely
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        // --- Calculate depth for ScreenToWorldPoint ---
        float depth = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 screenPosWithZ = new Vector3(mouseScreenPos.x, mouseScreenPos.y, depth);
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(screenPosWithZ);

        // Calculate direction
        Vector2 punchDirection = (mouseWorldPos - (Vector2)armTransform.position).normalized;
        Vector2 hitPosition = (Vector2)armTransform.position + (punchDirection * reach);

        // Play Sound
        if (actionAudioSource != null && armData.punchSound != null)
        {
            actionAudioSource.pitch = armData.punchPitch;
            actionAudioSource.PlayOneShot(armData.punchSound, armData.punchVolume);
        }

        // Trigger Animation
        if (animController != null)
        {
            animController.TriggerPunch(armTransform, duration, hitPosition);
        }

        // --- DEAL DAMAGE (FIXED) ---
        // We use OverlapCircleNonAlloc for performance (no garbage generation)
        int hitCount = Physics2D.OverlapCircleNonAlloc(hitPosition, radius, hitBuffer, hittableLayers);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hitBuffer[i];
            
            // 1. Check for EnemyLimbController
            if (hit.TryGetComponent<EnemyLimbController>(out EnemyLimbController enemy))
            {
                enemy.TakeDamage(damage);

                // --- APPLY KNOCKBACK ---
                if (hit.TryGetComponent<Rigidbody2D>(out Rigidbody2D enemyRb))
                {
                    // Reset velocity slightly to ensure crisp knockback even if they were moving towards us
                    enemyRb.linearVelocity = Vector2.zero; 
                    enemyRb.AddForce(punchDirection * knockback, ForceMode2D.Impulse);
                }

                Debug.Log($"Hit Enemy {hit.name} for {damage} damage!");
            }
            // 2. Optional: Add logic here for breakable crates, etc.
        }
    }
}