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

    // --- Private State ---
    private Rigidbody2D rb;
    private InputSystem_Actions playerControls;
    private PlayerLimbController playerLimbController;

    private Vector2 moveInput = Vector2.zero;
    private float currentMoveSpeed;
    private bool isSprinting = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerControls = new InputSystem_Actions();
        playerLimbController = GetComponent<PlayerLimbController>();
        
        currentMoveSpeed = baseMoveSpeed;

        // This prevents the player from spinning around when colliding with things
        rb.freezeRotation = true;

        cam = Camera.main; 
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

    // --- MODIFIED: This now clamps the reach ---
    private void HandleCrawl(InputAction.CallbackContext context)
    {
        isCrawlHeld = context.performed;

        // If the button was just PRESSED and we can crawl...
        if (context.performed && playerLimbController != null && playerLimbController.CanCrawl())
        {
            // Get mouse position
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

            // --- NEW: Clamp reach ---
            Vector2 directionToMouse = mouseWorldPos - (Vector2)transform.position;
            float distanceToMouse = directionToMouse.magnitude;

            // If distance is over our reach, clamp it.
            if (distanceToMouse > crawlReach)
            {
                // Get the direction and multiply by reach to get the max point
                crawlPlantPoint = (Vector2)transform.position + (directionToMouse.normalized * crawlReach);
                Debug.Log($"Crawl point clamped to max reach at: {crawlPlantPoint}");
            }
            else
            {
                // We are IN REACH, use the exact mouse position
                crawlPlantPoint = mouseWorldPos;
                Debug.Log($"Crawl point planted at: {crawlPlantPoint}");
            }
            
            isCrawling = true; // We always have a valid grab now!
            // TODO: Trigger arm "plant" animation here
            // --- END NEW ---
        }

        // If the button was RELEASED
        if (context.canceled)
        {
            isCrawling = false; // Stop crawling when button is released
        }
    }
    // --- END MODIFICATION ---

    void FixedUpdate()
    {
        // --- MODIFIED CRAWL LOGIC: Plant-and-Pull ---
        // Check if we are holding the button, have a valid plant, AND can crawl
        if (isCrawlHeld && isCrawling && playerLimbController != null && playerLimbController.CanCrawl())
        {
            // --- 1. Check if we've arrived ---
            float distanceToPoint = Vector2.Distance(transform.position, crawlPlantPoint);
            if (distanceToPoint < 0.1f) // 0.1f is a small "dead zone"
            {
                // We've reached the point, stop moving
                rb.linearVelocity = Vector2.zero;
                isCrawling = false; // Stop this crawl
                return;
            }
            
            // --- 2. Get mouse and body vectors ---
            Vector2 currentMousePos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            // Vector from the plant point to the player's body
            Vector2 plantToBody = (Vector2)transform.position - crawlPlantPoint;
            // Vector from the plant point to the current mouse position
            Vector2 plantToMouse = currentMousePos - crawlPlantPoint;

            // --- 3. Calculate Pull Factor ---
            // Project the mouse-pull onto the line of action (plant-to-body)
            // This finds out how far "back" (towards the body) the mouse is.
            float pullAmount = Vector2.Dot(plantToMouse, plantToBody.normalized);

            float pullFactor = 0f;
            if (pullAmount > 0) // Only allow positive pulls
            {
                // Normalize the pull amount based on the total distance. 1.0 = mouse at player's body
                pullFactor = Mathf.Clamp01(pullAmount / plantToBody.magnitude);
            }
            
            // --- 4. Apply Velocity ---
            // The direction to move is *towards* the plant point
            Vector2 crawlDirection = (crawlPlantPoint - (Vector2)transform.position).normalized;
            
            // Apply velocity based on how hard we are pulling
            rb.linearVelocity = crawlDirection * crawlSpeed * pullFactor;
            
            // IMPORTANT: Exit here so we don't run the normal move logic
            return; 
        }
        // --- END CRAWL LOGIC ---

        // --- NORMAL LEG MOVEMENT ---
        if (currentMoveSpeed <= 0)
        {
            // Stop any leftover movement if we're not crawling and have no legs
            // (isCrawlHeld will be false if we released the button, or isCrawling is false)
            if (!isCrawlHeld) 
            {
                 rb.linearVelocity = Vector2.zero;
            }
            return;
        }
        
        // Standard movement logic
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
}