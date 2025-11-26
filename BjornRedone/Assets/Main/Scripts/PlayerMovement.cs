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

        // --- New ---
        // We also need to listen for Attack and Interact
        playerControls.Player.Attack.performed += HandleAttack;
        playerControls.Player.Interact.performed += HandleInteract;

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
        playerControls.Player.Attack.performed -= HandleAttack;
        playerControls.Player.Interact.performed -= HandleInteract;

        // --- Disable the "Player" Action Map ---
        playerControls.Player.Disable();
    }


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.linearDamping = 3f;
    }

    // We don't need Update() anymore for input
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

    private void HandleAttack(InputAction.CallbackContext callbackContext)
    {
        Debug.Log("Interact pressed: Player taking 10 test damage!");
            // This line calls the damage system:
            playerLimbController.TakeDamage(10f);
    }

    private void HandleInteract(InputAction.CallbackContext callbackContext)
    {
        // We only care about the moment it's pressed
        // Debug.Log("Interact action triggered!"); // We'll replace this

        // --- New ---
        // Call the TakeDamage method on the limb controller
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

    // --- No changes needed from here down ---

    void FixedUpdate()
    {
        // Apply physics movement in FixedUpdate
        if (moveInput != Vector2.zero)
        {
            // --- Updated movement logic ---
            // Determine final speed based on sprinting or not
            float targetSpeed = isSprinting ? currentMoveSpeed * sprintMultiplier : currentMoveSpeed;
            
            rb.MovePosition(rb.position + moveInput * targetSpeed * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// This is called by PlayerLimbController to update the player's speed.
    /// </summary>
    public void SetMoveSpeed(float newSpeed)
    {
        currentMoveSpeed = newSpeed;
    }
}