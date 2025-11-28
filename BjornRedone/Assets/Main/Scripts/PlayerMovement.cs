using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private float currentMoveSpeed = 5f;

    private bool isSprinting = false;
    [SerializeField] private float sprintMultiplier = 1.5f;

    // --- New ---
    // This variable will hold our auto-generated controls
    private InputSystem_Actions playerControls;

    // --- New ---
    // Reference to the limb controller to call TakeDamage
    private PlayerLimbController playerLimbController;

    /// <summary>
    /// OnEnable is called when the script is turned on.
    /// This is the perfect place to start listening for inputs.
    /// </summary>
    void OnEnable()
    {
        // --- Subscribe to the actions ---

        // For the "Move" action
        playerControls.Player.Move.performed += HandleMove; // When a key is pressed
        playerControls.Player.Move.canceled += HandleMove; // When a key is released

        // For the "Sprint" action
        playerControls.Player.Sprint.performed += HandleSprint;
        playerControls.Player.Sprint.canceled += HandleSprint;

        // --- REMOVED ---
        // PlayerAttackController handles these now
        // playerControls.Player.Attack.performed += HandleAttack;
        // playerControls.Player.Interact.performed += HandleInteract;

        // --- Enable the "Player" Action Map ---
        playerControls.Player.Enable();
    } // <-- HERE IS THE FIX! This brace was missing.

    void Awake()
    {
        // Create a new instance of our auto-generated control class
        playerControls = new InputSystem_Actions();

        // --- New ---
        // Get the limb controller component on this same GameObject
        playerLimbController = GetComponent<PlayerLimbController>();
    }

    /// <summary>
    /// OnDisable is called when the script is turned off.
    /// We must "unsubscribe" to avoid errors.
    /// </summary>
    void OnDisable()
    {
        // --- Unsubscribe from all actions ---
        playerControls.Player.Move.performed -= HandleMove;
        playerControls.Player.Move.canceled -= HandleMove;
        playerControls.Player.Sprint.performed -= HandleSprint;
        playerControls.Player.Sprint.canceled -= HandleSprint;
        
        // --- REMOVED ---
        // playerControls.Player.Attack.performed -= HandleAttack;
        // playerControls.Player.Interact.performed -= HandleInteract;

        // --- Disable the "Player" Action Map ---
        playerControls.Player.Disable();
    }


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        
        // --- THE FIX ---
        // We are setting velocity directly, so we don't want
        // the physics engine to add any drag/damping.
        // This stops it from fighting our code on direction changes.
        rb.linearDamping = 0f; // <-- This is the modern property for Rigidbody2D
        // rb.linearDamping = 0f; // This is the old property
        
        // --- THIS IS THE KEY ---
        // Smooths the visual movement between physics frames
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; 
    }

    void Update() { }

    
    // --- These new methods handle the events from OnEnable/OnDisable ---

    private void HandleMove(InputAction.CallbackContext callbackContext)
    {
        moveInput = callbackContext.ReadValue<Vector2>();
        moveInput.Normalize(); // Normalize to prevent faster diagonal movement
    }

    private void HandleSprint(InputAction.CallbackContext callbackContext)
    {
        // .performed is true when the button is pressed
        // .canceled is true when it's released, so isSprinting becomes false
        isSprinting = callbackContext.performed;
    }

    // --- REMOVED: These methods are no longer needed ---
    /*
    private void HandleAttack(InputAction.CallbackContext callbackContext)
    {
        // This is for testing damage
        if (playerLimbController != null)
        {
            Debug.Log("Attack pressed: Player taking 10 test damage!");
            playerLimbController.TakeDamage(10f);
        }
    }

    private void HandleInteract(InputAction.CallbackContext callbackContext)
    {
        // This is for testing damage
        if (playerLimbController != null)
        {
            Debug.Log("Interact pressed: Player taking 10 test damage!");
            playerLimbController.TakeDamage(10f); // Sending 10 damage as a test
        }
        else
        {
            Debug.LogWarning("Interact pressed, but PlayerLimbController was not found!");
        }
    }
    */

    void FixedUpdate()
    {
        // --- NEW MOVEMENT LOGIC ---
        // Calculate the desired velocity
        // --- THIS IS THE FIX ---
        float targetSpeed = isSprinting ? currentMoveSpeed * sprintMultiplier : currentMoveSpeed;
        Vector2 targetVelocity = moveInput * targetSpeed;

        // Set the velocity directly
        // This gives much smoother results than MovePosition
        rb.linearVelocity = targetVelocity; // Using .velocity (safer than .linearVelocity)
        
        /*
        // Old logic:
        if (moveInput != Vector2.zero)
        {
            float targetSpeed = isSprinting ? currentMoveSpeed * sprintMultiplier : currentMoveSpeed;
            rb.MovePosition(rb.position + moveInput * targetSpeed * Time.fixedDeltaTime);
        }
        */
    }

    public void SetMoveSpeed(float newSpeed)
    {
        currentMoveSpeed = newSpeed;
    }

    // --- NEW PUBLIC GETTER ---
    /// <summary>
    /// Allows other scripts to read the current movement input.
    /// </summary>
    public Vector2 GetMoveInput()
    {
        return moveInput;
    }
}