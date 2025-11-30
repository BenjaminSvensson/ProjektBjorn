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

    // --- NEW: For Beartrap ---
    private bool isTrapped = false;
    // --- END NEW ---

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerControls = new InputSystem_Actions();
        playerLimbController = GetComponent<PlayerLimbController>();
        
        currentMoveSpeed = baseMoveSpeed;
        rb.freezeRotation = true;
        cam = Camera.main; 
    }

    void OnEnable()
    {
        playerControls.Player.Move.performed += HandleMove;
        playerControls.Player.Move.canceled += HandleMove;

        playerControls.Player.Sprint.performed += HandleSprint;
        playerControls.Player.Sprint.canceled += HandleSprint;
        
        playerControls.Player.Attack.performed += HandleCrawl;
        playerControls.Player.Attack.canceled += HandleCrawl;

        playerControls.Player.Enable();
    }

    void OnDisable()
    {
        playerControls.Player.Move.performed -= HandleMove;
        playerControls.Player.Move.canceled -= HandleMove;

        playerControls.Player.Sprint.performed -= HandleSprint;
        playerControls.Player.Sprint.canceled -= HandleSprint;

        playerControls.Player.Attack.performed -= HandleCrawl;
        playerControls.Player.Attack.canceled -= HandleCrawl;
        
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

        if (context.performed && playerLimbController != null && playerLimbController.CanCrawl())
        {
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
            
            isCrawling = true; 
        }

        if (context.canceled)
        {
            isCrawling = false; 
        }
    }

    void FixedUpdate()
    {
        // --- NEW: Beartrap Check ---
        // If trapped, do nothing. Stop all movement.
        if (isTrapped)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        // --- END NEW ---

        // --- CRAWL LOGIC ---
        if (isCrawlHeld && isCrawling && playerLimbController != null && playerLimbController.CanCrawl())
        {
            float distanceToPoint = Vector2.Distance(transform.position, crawlPlantPoint);
            if (distanceToPoint < 0.1f) 
            {
                rb.linearVelocity = Vector2.zero;
                isCrawling = false; 
                return;
            }
            
            Vector2 currentMousePos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 plantToBody = (Vector2)transform.position - crawlPlantPoint;
            Vector2 plantToMouse = currentMousePos - crawlPlantPoint;

            float pullAmount = Vector2.Dot(plantToMouse, plantToBody.normalized);

            float pullFactor = 0f;
            if (pullAmount > 0) 
            {
                pullFactor = Mathf.Clamp01(pullAmount / plantToBody.magnitude);
            }
            
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

    public Vector2 GetMoveInput()
    {
        return moveInput;
    }

    // --- NEW: Public method for beartrap ---
    /// <summary>
    /// Called by other scripts (like DamageSource) to trap or untrap the player.
    /// </summary>
    public void SetTrapped(bool trapped)
    {
        isTrapped = trapped;
    }
    // --- END NEW ---
}