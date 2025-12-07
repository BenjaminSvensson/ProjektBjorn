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
    [Tooltip("The layer(s) that can be hit by a punch.")]
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

    void Awake()
    {
        playerControls = new InputSystem_Actions();
        
        // --- FIX: Robust Camera Finding ---
        cam = Camera.main;
        if (cam == null)
        {
            // Try to find ANY camera if MainCamera tag is missing
            // Updated to use the new API to fix the warning
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
            // This happens if no mouse is detected (e.g. only gamepad connected)
            return; 
        }
        // --- END FIX ---

        float damage = limbController.baseAttackDamage + armData.attackDamageBonus;
        float reach = armData.attackReach;
        float radius = armData.impactSize;
        float duration = armData.punchDuration;
        float cooldown = armData.attackCooldown;
        
        attackCooldownTimer = cooldown;

        // Get mouse position safely
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        // --- FIX: Calculate depth for ScreenToWorldPoint ---
        // This ensures aiming works even if the camera is Perspective or at a different Z depth
        // We assume the game happens on Z=0 (where the player is usually located in 2D)
        float depth = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 screenPosWithZ = new Vector3(mouseScreenPos.x, mouseScreenPos.y, depth);
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(screenPosWithZ);
        // --- END FIX ---

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

        // Deal Damage
        Collider2D[] hits = Physics2D.OverlapCircleAll(hitPosition, radius, hittableLayers);

        foreach (Collider2D hit in hits)
        {
            // Example damage logic:
            // if (hit.TryGetComponent<EnemyHealth>(out EnemyHealth enemy))
            // {
            //     enemy.TakeDamage(damage);
            // }
            Debug.Log($"Hit: {hit.name}");
        }
    }
}