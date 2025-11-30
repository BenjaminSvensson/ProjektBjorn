using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] private float baseMoveSpeed = 5f;
    [SerializeField] private float sprintSpeedMultiplier = 1.5f;

    [Header("Crawl Settings")]
    [SerializeField] private float crawlSpeed = 2f;
    [SerializeField] private float crawlReach = 3f; 
    private Camera cam;
    private bool isCrawlHeld = false;
    
    private Vector2 crawlPlantPoint; 
    private bool isCrawling = false; // Tracks if we successfully planted

    [Header("Audio")]
    [Tooltip("The AudioSource used for action sounds (crawling). Assign this in the Inspector.")]
    [SerializeField] private AudioSource actionAudioSource;
    [Tooltip("The sound to play when 'planting' an arm to crawl.")]
    [SerializeField] private AudioClip crawlPlantSound;

    // --- Private State ---
    private Rigidbody2D rb;
    private InputSystem_Actions playerControls;
    private PlayerLimbController playerLimbController;

    private Vector2 moveInput = Vector2.zero;
    private float currentMoveSpeed;
    private bool isSprinting = false;

    // --- THIS IS THE MISSING VARIABLE ---
    private bool isTrapped = false;
    // --- END ---

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerControls = new InputSystem_Actions();
        playerLimbController = GetComponent<PlayerLimbController>();
        
        currentMoveSpeed = baseMoveSpeed;

        // This prevents the player from spinning around when colliding with things
        rb.freezeRotation = true;

        cam = Camera.main; 
        
        // Add a check in case it's not assigned
        if (actionAudioSource == null)
        {
            Debug.LogWarning("PlayerMovement is missing a reference to the Action Audio Source!");
        }
    }

    void OnEnable()
    {
        // --- Subscribe to the actions ---
        
        // Player
        playerControls.Player.Move.performed += HandleMove;
        playerControls.Player.Move.canceled += HandleMove;

        playerControls.Player.Sprint.performed += HandleSprint;
        playerControls.Player.Sprint.canceled += HandleSprint;
        
        // Listen to the Attack button for crawling
        playerControls.Player.Attack.performed += HandleCrawl;
        playerControls.Player.Attack.canceled += HandleCrawl;

        // --- Enable the "Player" Action Map ---
        playerControls.Player.Enable();
    }

    void OnDisable()
    {
        // --- Unsubscribe from all actions ---

        // Player
        playerControls.Player.Move.performed -= HandleMove;
        playerControls.Player.Move.canceled -= HandleMove;

        playerControls.Player.Sprint.performed -= HandleSprint;
        playerControls.Player.Sprint.canceled -= HandleSprint;

        playerControls.Player.Attack.performed -= HandleCrawl;
        playerControls.Player.Attack.canceled -= HandleCrawl;
        
        // --- Disable the "Player" Action Map ---
        playerControls.Player.Disable();
    }

    
    private void HandleMove(InputAction.CallbackContext callbackContext)
    {
        moveInput = callbackContext.ReadValue<Vector2>();
    }

    private void HandleSprint(InputAction.CallbackContext callbackContext)
    {
        isSprinting = callbackContext.performed;
    }

    private void HandleCrawl(InputAction.CallbackContext context)
    {
        isCrawlHeld = context.performed;

        // If the button was just PRESSED and we can crawl...
        if (context.performed && playerLimbController != null && playerLimbController.CanCrawl())
        {
            // Get mouse position
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

            Vector2 directionToMouse = mouseWorldPos - (Vector2)transform.position;
            float distanceToMouse = directionToMouse.magnitude;

            if (distanceToMouse > crawlReach)
            {
                crawlPlantPoint = (Vector2)transform.position + (directionToMouse.normalized * crawlReach);
            }
            else
            {
                crawlPlantPoint = mouseWorldPos;
            }
            
            isCrawling = true; // We have a valid grab!
            
            // --- Play Plant Sound ---
            if (actionAudioSource != null && crawlPlantSound != null)
            {
                actionAudioSource.PlayOneShot(crawlPlantSound);
            }
        }

        // If the button was RELEASED
        if (context.canceled)
        {
            isCrawling = false; // Stop crawling when button is released
        }
    }

    void FixedUpdate()
    {
        // --- Beartrap Check ---
        // If trapped, do nothing. Stop all movement.
        if (isTrapped)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        // --- END ---

        // Check if we are holding the button, have a valid plant, AND can crawl
        if (isCrawlHeld && isCrawling && playerLimbController != null && playerLimbController.CanCrawl())
        {
            // 1. Check if we've arrived
            float distanceToPoint = Vector2.Distance(transform.position, crawlPlantPoint);
            if (distanceToPoint < 0.1f) // 0.1f is a small "dead zone"
            {
                rb.linearVelocity = Vector2.zero;
                isCrawling = false; // Stop this crawl
                return;
            }
            
            // 2. Get mouse and body vectors
            Vector2 currentMousePos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 plantToBody = (Vector2)transform.position - crawlPlantPoint;
            Vector2 plantToMouse = currentMousePos - crawlPlantPoint;

            // 3. Calculate Pull Factor
            float pullAmount = Vector2.Dot(plantToMouse, plantToBody.normalized);

            float pullFactor = 0f;
            if (pullAmount > 0) // Only allow positive pulls
            {
                pullFactor = Mathf.Clamp01(pullAmount / plantToBody.magnitude);
            }
            
            // 4. Apply Velocity
            Vector2 crawlDirection = (crawlPlantPoint - (Vector2)transform.position).normalized;
            rb.linearVelocity = crawlDirection * crawlSpeed * pullFactor;
            
            return; 
        }

        // --- NORMAL LEG MOVEMENT ---
        if (currentMoveSpeed <= 0)
        {
            if (!isCrawlHeld) 
            {
                 rb.linearVelocity = Vector2.zero;
            }
            return;
        }
        
        float speed = isSprinting ? currentMoveSpeed * sprintSpeedMultiplier : currentMoveSpeed;
        rb.linearVelocity = moveInput * speed;
    }

    public void SetMoveSpeed(float newSpeed)
    {
        currentMoveSpeed = newSpeed;
    }

    /// <summary>
    /// Allows other scripts to read the current movement input.
    /// </summary>
    public Vector2 GetMoveInput()
    {
        return moveInput;
    }

    /// <summary>
    /// Called by other scripts (like DamageSource) to trap or untrap the player.
    /// </summary>
    public void SetTrapped(bool trapped)
    {
        isTrapped = trapped;
    }
}