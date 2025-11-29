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
    // --- REMOVED: Rigidbody rb ---

    [Header("Attack Settings")]
    [Tooltip("The layer(s) that can be hit by a punch.")]
    [SerializeField] private LayerMask hittableLayers;

    // --- REMOVED: Crawl Settings ---

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
        // --- REMOVED: Rigidbody rb assignment ---
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
            // --- MODIFIED: Simplified logic ---
            // Check if we can attack (have arms) AND we are not in the crawl state
            if (limbController.CanAttack() && !limbController.CanCrawl())
            {
                TryPunch();
            }
            // --- END MODIFICATION ---
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
            // This case should be rare, but good to have
            return;
        }

        // We have an arm! Let's punch.
        PerformPunch(punchingArm, armData);

        // Switch to the other arm for the next punch
        isNextPunchLeft = !isNextPunchLeft;
    }

    // --- REMOVED: TryCrawl() method ---

    private void PerformPunch(Transform armTransform, LimbData armData)
    {
        // Get stats from the arm
        float damage = limbController.baseAttackDamage + armData.attackDamageBonus;
        float speed = armData.attackSpeed;
        float reach = armData.attackReach;
        float radius = armData.impactSize;

        // Get mouse position from camera
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

        // Calculate direction from arm to mouse. This is now reliable!
        Vector2 punchDirection = (mouseWorldPos - (Vector2)armTransform.position).normalized;
        
        // Calculate the hit position using the correct direction
        Vector2 hitPosition = (Vector2)armTransform.position + (punchDirection * reach);


        // Calculate cooldown
        float cooldown = 1.0f / speed;
        attackCooldownTimer = cooldown;

        // Trigger the animation
        if (animController != null)
        {
            animController.TriggerPunch(armTransform, cooldown * 0.8f, hitPosition);
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