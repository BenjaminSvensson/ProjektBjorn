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
        cam = Camera.main;

        if (limbController == null)
            limbController = GetComponent<PlayerLimbController>();
        if (animController == null)
            animController = GetComponent<PlayerAnimationController>();
            
        // Add a check in case it's not assigned
        if (actionAudioSource == null)
        {
            Debug.LogWarning("PlayerAttackController is missing a reference to the Action Audio Source!");
        }
    }

    void OnEnable()
    {
        // --- Subscribe to the actions ---
        
        // Player
        playerControls.Player.Attack.performed += HandleAttack;
        playerControls.Player.Attack.canceled += HandleAttack;

        // --- Enable the "Player" Action Map ---
        playerControls.Player.Enable();
    }

    void OnDisable()
    {
        // --- Unsubscribe from all actions ---

        // Player
        playerControls.Player.Attack.performed -= HandleAttack;
        playerControls.Player.Attack.canceled -= HandleAttack;
        
        // --- Disable the "Player" Action Map ---
        playerControls.Player.Disable();
    }

    private void HandleAttack(InputAction.CallbackContext callbackContext)
    {
        // .performed is true when the button is pressed
        // .canceled is false when the button is released
        isAttackHeld = callbackContext.performed;
    }

    void Update()
    {
        // Decrement the attack cooldown timer
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        // Check if the player is holding the attack button and cooldown is ready
        if (isAttackHeld && attackCooldownTimer <= 0)
        {
            // Check if we can attack (have arms) AND we are not in the crawl state
            if (limbController.CanAttack() && !limbController.CanCrawl())
            {
                TryPunch();
            }
        }
    }

    private void TryPunch()
    {
        // --- Find an arm to punch with ---
        Transform punchingArm = null;
        LimbData armData = null;
        bool isLeft = isNextPunchLeft;

        if (isNextPunchLeft)
        {
            // Try left arm first
            punchingArm = limbController.GetLeftArmSlot();
            armData = limbController.GetArmData(true);
            if (armData == null)
            {
                // Left arm missing, try right arm
                punchingArm = limbController.GetRightArmSlot();
                armData = limbController.GetArmData(false);
                isLeft = false;
            }
        }
        else
        {
            // Try right arm first
            punchingArm = limbController.GetRightArmSlot();
            armData = limbController.GetArmData(false);
            isLeft = false;
            if (armData == null)
            {
                // Right arm missing, try left arm
                punchingArm = limbController.GetLeftArmSlot();
                armData = limbController.GetArmData(true);
                isLeft = true;
            }
        }
        
        // Failsafe: if we still have no arm, do nothing
        if (armData == null)
        {
            return;
        }

        // We have an arm! Let's punch.
        PerformPunch(punchingArm, armData);

        // Switch to the other arm for the next punch
        isNextPunchLeft = !isNextPunchLeft;
    }

    private void PerformPunch(Transform armTransform, LimbData armData)
    {
        // Get stats from the arm
        float damage = limbController.baseAttackDamage + armData.attackDamageBonus;
        float reach = armData.attackReach;
        float radius = armData.impactSize;

        // --- NEW: Use new variables ---
        float duration = armData.punchDuration;
        float cooldown = armData.attackCooldown;
        attackCooldownTimer = cooldown;
        // --- END NEW ---

        // Get mouse position from camera
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

        // Calculate direction from arm to mouse.
        Vector2 punchDirection = (mouseWorldPos - (Vector2)armTransform.position).normalized;
        
        // Calculate the hit position
        Vector2 hitPosition = (Vector2)armTransform.position + (punchDirection * reach);

        // --- NEW: Play Sound ---
        if (actionAudioSource != null && armData.punchSound != null)
        {
            // Set pitch and volume based on the limb's data
            actionAudioSource.pitch = armData.punchPitch;
            actionAudioSource.PlayOneShot(armData.punchSound, armData.punchVolume);
        }
        // --- END NEW ---

        // Trigger the animation
        if (animController != null)
        {
            // Pass the punch DURATION to the animation
            animController.TriggerPunch(armTransform, duration, hitPosition);
        }

        // Perform the physics check to deal damage
        Collider2D[] hits = Physics2D.OverlapCircleAll(hitPosition, radius, hittableLayers);

        Debug.Log($"PUNCH! Dealt {damage} damage at {hitPosition}. Hit {hits.Length} targets.");

        foreach (Collider2D hit in hits)
        {
            // This is where you'd get an EnemyHealth component and deal damage
            // if (hit.TryGetComponent<EnemyHealth>(out EnemyHealth enemy))
            // {
            //     enemy.TakeDamage(damage);
            // }
            Debug.Log($"Hit: {hit.name}");
        }
    }
}