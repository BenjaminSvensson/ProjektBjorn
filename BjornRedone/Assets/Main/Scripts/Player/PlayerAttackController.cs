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
    
    // --- New: Independent Cooldowns ---
    private float leftArmCooldownTimer = 0f;
    private float rightArmCooldownTimer = 0f;
    
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
        // Decrement both timers independently
        if (leftArmCooldownTimer > 0) leftArmCooldownTimer -= Time.deltaTime;
        if (rightArmCooldownTimer > 0) rightArmCooldownTimer -= Time.deltaTime;

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
        // 1. Gather status of both arms
        LimbData leftData = limbController.GetArmData(true);
        LimbData rightData = limbController.GetArmData(false);
        
        // Check if arms exist AND are off cooldown
        bool leftReady = leftData != null && leftArmCooldownTimer <= 0;
        bool rightReady = rightData != null && rightArmCooldownTimer <= 0;

        // 2. If neither are ready, we can't do anything
        if (!leftReady && !rightReady) return;

        bool fireLeft = false;

        // 3. Logic to decide which to fire
        if (leftReady && rightReady)
        {
            // If BOTH are ready, stick to the alternating rhythm (L -> R -> L)
            fireLeft = isNextPunchLeft;
        }
        else if (leftReady)
        {
            // Only Left is ready (Right is missing or on cooldown)
            // Fire Left immediately, don't wait for Right
            fireLeft = true;
        }
        else if (rightReady)
        {
            // Only Right is ready (Left is missing or on cooldown)
            // Fire Right immediately, don't wait for Left
            fireLeft = false;
        }

        // 4. Execution
        if (fireLeft)
        {
            PerformPunch(limbController.GetLeftArmSlot(), leftData, true);
            // We just fired Left, so ideally the next one should be Right to keep rhythm
            isNextPunchLeft = false; 
        }
        else
        {
            PerformPunch(limbController.GetRightArmSlot(), rightData, false);
            // We just fired Right, so ideally the next one should be Left to keep rhythm
            isNextPunchLeft = true;
        }
    }

    private void PerformPunch(Transform armTransform, LimbData armData, bool isLeftArm)
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
        float knockback = armData.knockbackForce;

        // --- Set specific cooldown ---
        if (isLeftArm)
            leftArmCooldownTimer = cooldown;
        else
            rightArmCooldownTimer = cooldown;

        // Get mouse position safely
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        // --- Calculate depth for ScreenToWorldPoint ---
        float depth = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 screenPosWithZ = new Vector3(mouseScreenPos.x, mouseScreenPos.y, depth);
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(screenPosWithZ);

        // Calculate direction
        Vector2 punchDirection = (mouseWorldPos - (Vector2)armTransform.position).normalized;
        Vector2 hitPosition = (Vector2)armTransform.position + (punchDirection * reach);

        // --- Play Random Sound from Array ---
        if (actionAudioSource != null && armData.punchSounds != null && armData.punchSounds.Length > 0)
        {
            // Pick a random clip from the array
            AudioClip clipToPlay = armData.punchSounds[Random.Range(0, armData.punchSounds.Length)];
            
            if (clipToPlay != null)
            {
                actionAudioSource.pitch = armData.punchPitch;
                actionAudioSource.PlayOneShot(clipToPlay, armData.punchVolume);
            }
        }

        // Trigger Animation
        if (animController != null)
        {
            animController.TriggerPunch(armTransform, duration, hitPosition);
        }

        // --- DEAL DAMAGE ---
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
                    enemyRb.linearVelocity = Vector2.zero; 
                    enemyRb.AddForce(punchDirection * knockback, ForceMode2D.Impulse);
                }

                Debug.Log($"Hit Enemy {hit.name} for {damage} damage!");
            }
        }
    }
}