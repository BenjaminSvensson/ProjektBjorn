using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] private float baseMoveSpeed = 5f;
    [SerializeField] private float sprintSpeedMultiplier = 1.5f;

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

        // --- FIX FOR ROTATION ---
        // This prevents the player from spinning around when colliding with things
        rb.freezeRotation = true;
        // --- END FIX ---
    }

    void OnEnable()
    {
        // --- Subscribe to the actions ---
        
        // Player
        playerControls.Player.Move.performed += HandleMove;
        playerControls.Player.Move.canceled += HandleMove;

        playerControls.Player.Sprint.performed += HandleSprint;
        playerControls.Player.Sprint.canceled += HandleSprint;

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
        
        // --- Disable the "Player" Action Map ---
        playerControls.Player.Disable();
    }

    
    private void HandleMove(InputAction.CallbackContext callbackContext)
    {
        // .performed is called when the button is pressed/held
        // .canceled is called when the button is released
        // This reads the Vector2 from the WASD keys or joystick
        moveInput = callbackContext.ReadValue<Vector2>();
    }

    private void HandleSprint(InputAction.CallbackContext callbackContext)
    {
        // .performed is true when the button is pressed
        // .canceled is false when the button is released
        isSprinting = callbackContext.performed;
    }

    void FixedUpdate()
    {
        // --- NEW MOVEMENT LOGIC ---
        // Calculate the speed (sprinting or walking)
        float speed = isSprinting ? currentMoveSpeed * sprintSpeedMultiplier : currentMoveSpeed;

        // Apply the velocity to the Rigidbody
        // This provides smooth, physics-based movement
        rb.linearVelocity = moveInput * speed;
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