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

    // --- Private State ---
    private InputSystem_Actions playerControls;
    private bool isAttackHeld = false;
    private bool isNextPunchLeft = true;
    private float attackCooldownTimer = 0f;
    private Camera cam; // --- NEW ---

    void Awake()
    {
        playerControls = new InputSystem_Actions();
        cam = Camera.main; // --- NEW ---

        if (limbController == null)
            limbController = GetComponent<PlayerLimbController>();
        if (animController == null)
            animController = GetComponent<PlayerAnimationController>();
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

    private void HandleAttack(InputAction.CallbackContext context)
    {
        isAttackHeld = context.performed;
    }

    void Update()
    {
        // Tick down the cooldown timer
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        // Check if the player is holding the attack button and cooldown is ready
        if (isAttackHeld && attackCooldownTimer <= 0)
        {
            TryPunch();
        }
    }

    private void TryPunch()
    {
        // Check if we can attack at all
        if (!limbController.CanAttack())
            return;

        // Determine which arm to use
        Transform punchingArm;
        LimbData armData;

        if (isNextPunchLeft)
        {
            punchingArm = limbController.GetLeftArmSlot();
            armData = limbController.GetArmData(true);
        }
        else
        {
            punchingArm = limbController.GetRightArmSlot();
            armData = limbController.GetArmData(false);
        }

        // If the chosen arm is missing, try the other one
        if (punchingArm == null || armData == null)
        {
            isNextPunchLeft = !isNextPunchLeft; // Switch to the other arm
            punchingArm = isNextPunchLeft ? limbController.GetLeftArmSlot() : limbController.GetRightArmSlot();
            armData = isNextPunchLeft ? limbController.GetArmData(true) : limbController.GetArmData(false);

            // If that one is *also* missing, we can't punch
            if (punchingArm == null || armData == null)
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
        float speed = armData.attackSpeed;
        float reach = armData.attackReach;
        float radius = armData.impactSize;

        // --- FIX ---
        // Get mouse position from camera
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

        // Calculate direction from arm to mouse. This is now reliable!
        Vector2 punchDirection = (mouseWorldPos - (Vector2)armTransform.position).normalized;
        
        // Calculate the hit position using the correct direction
        // This is the TARGET of the punch in WORLD space
        Vector2 hitPosition = (Vector2)armTransform.position + (punchDirection * reach);
        // --- END FIX ---


        // Calculate cooldown
        float cooldown = 1.0f / speed;
        attackCooldownTimer = cooldown;

        // Trigger the animation
        if (animController != null)
        {
            // --- MODIFIED: Pass the WORLD space hitPosition ---
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