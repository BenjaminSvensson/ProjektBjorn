using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("UI Blocking - DRAG DEALER UI HERE")]
    [SerializeField] private GameObject dealerUI; // DRAG YOUR UI OBJECT HERE

    [Header("Base Stats")]
    [SerializeField] private float baseMoveSpeed = 5f;
    [SerializeField] private float sprintSpeedMultiplier = 1.5f;

    [Header("Crawl Settings")]
    [SerializeField] private float crawlSpeed = 2f;
    [SerializeField] private float crawlReach = 3f; 
    private Camera cam;
    private bool isCrawlHeld = false;
    
    private Vector2 crawlPlantPoint; 
    private bool isCrawling = false; 

    [Header("Audio")]
    [SerializeField] private AudioSource actionAudioSource;
    [SerializeField] private AudioClip crawlPlantSound;

    // --- Private State ---
    private Rigidbody2D rb;
    private InputSystem_Actions playerControls;
    private PlayerLimbController playerLimbController;
    private Multipliers multiplier;

    private Vector2 moveInput = Vector2.zero;
    private float currentMoveSpeed;
    private bool isSprinting = false;

    private bool isTrapped = false;
    private bool isMovementLocked = false; 

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerControls = new InputSystem_Actions();
        playerLimbController = GetComponent<PlayerLimbController>();
        multiplier = GetComponent<Multipliers>();
        currentMoveSpeed = baseMoveSpeed;
        rb.freezeRotation = true;

        cam = Camera.main; 
        if (actionAudioSource == null) Debug.LogWarning("PlayerMovement missing Audio Source!");
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
    
    private void HandleMove(InputAction.CallbackContext callbackContext) { moveInput = callbackContext.ReadValue<Vector2>(); }
    private void HandleSprint(InputAction.CallbackContext callbackContext) { isSprinting = callbackContext.performed; }

    private void HandleCrawl(InputAction.CallbackContext context)
    {
        // Don't crawl if shop is open
        if (dealerUI != null && dealerUI.activeInHierarchy) return;

        isCrawlHeld = context.performed;
        if (context.performed && playerLimbController != null && playerLimbController.CanCrawl())
        {
            Vector2 mouseWorldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 directionToMouse = mouseWorldPos - (Vector2)transform.position;
            
            crawlPlantPoint = directionToMouse.magnitude > crawlReach 
                ? (Vector2)transform.position + (directionToMouse.normalized * crawlReach) 
                : mouseWorldPos;
            
            isCrawling = true; 
            if (actionAudioSource != null && crawlPlantSound != null) actionAudioSource.PlayOneShot(crawlPlantSound);
        }
        if (context.canceled) isCrawling = false; 
    }

    void FixedUpdate()
    {
        // --- DISABLE MOVEMENT IF UI IS OPEN ---
        if (dealerUI != null && dealerUI.activeInHierarchy)
        {
            rb.linearVelocity = Vector2.zero; // Stop all momentum
            return;
        }
        // ---------------------------------------

        if (isTrapped || isMovementLocked)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isCrawlHeld && isCrawling && playerLimbController != null && playerLimbController.CanCrawl())
        {
            float distanceToPoint = Vector2.Distance(transform.position, crawlPlantPoint);
            if (distanceToPoint < 0.1f) { rb.linearVelocity = Vector2.zero; isCrawling = false; return; }
            
            Vector2 currentMousePos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 plantToBody = (Vector2)transform.position - crawlPlantPoint;
            Vector2 plantToMouse = currentMousePos - crawlPlantPoint;
            float pullFactor = Mathf.Clamp01(Vector2.Dot(plantToMouse, plantToBody.normalized) / plantToBody.magnitude);
            
            if (Vector2.Dot(plantToMouse, plantToBody.normalized) <= 0) pullFactor = 0; 

            rb.linearVelocity = (crawlPlantPoint - (Vector2)transform.position).normalized * crawlSpeed * pullFactor;
            return; 
        }

        if (currentMoveSpeed <= 0) { if (!isCrawlHeld) rb.linearVelocity = Vector2.zero; return; }

        float speed = currentMoveSpeed;
        if (multiplier != null) speed *= multiplier.speed;
        speed = isSprinting ? speed * sprintSpeedMultiplier : speed;

        rb.linearVelocity = moveInput * speed;
    }

    public void SetMoveSpeed(float newSpeed) { currentMoveSpeed = newSpeed; }
    public Vector2 GetMoveInput() { return moveInput; }
    public void SetTrapped(bool trapped) { isTrapped = trapped; }

    public void SetMovementLocked(bool locked)
    {
        isMovementLocked = locked;
        if (locked) rb.linearVelocity = Vector2.zero;
    }
}