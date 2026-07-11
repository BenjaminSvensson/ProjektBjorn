using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq; // We need this to order objects by distance

/// <summary>
/// This script, placed on the Player, scans for the nearest IInteractable object
/// and handles the "Interact" input.
/// 
/// REQUIRES:
/// 1. A child GameObject with a UI Text/TextMeshPro component for the prompt.
/// 2. The player must have the "Interact" action set up in their InputSystem_Actions.
/// 3. Interactable objects must be on a specific layer (e.g., "Interactable").
/// </summary>
public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("The maximum range the player can interact from.")]
    [SerializeField] private float interactionRange = 2f;
    [Tooltip("The layer(s) that contain interactable objects.")]
    [SerializeField] private LayerMask interactableLayer;

    [Header("Required References")]
    [Tooltip("A reference to the player's main limb controller.")]
    [SerializeField] private PlayerLimbController limbController;
    [Tooltip("A reference to the UI prompt GameObject (e.g., a text box).")]
    [SerializeField] private GameObject interactionPrompt;

    // --- Private State ---
    private InputSystem_Actions playerControls;
    private IInteractable currentInteractable;
    private UnityEngine.UI.Text promptText; // Use this if using standard UI Text
    // private TMPro.TextMeshProUGUI promptText; // Use this if using TextMeshPro

    void Awake()
    {
        playerControls = new InputSystem_Actions();
        if (limbController == null)
            limbController = GetComponent<PlayerLimbController>();
        
        if (interactionPrompt != null)
        {
            // Get the text component from the prompt object
            promptText = interactionPrompt.GetComponent<UnityEngine.UI.Text>();
            // If using TextMeshPro, use this line instead:
            // promptText = interactionPrompt.GetComponent<TMPro.TextMeshProUGUI>();
            
            // Start with the prompt hidden
            interactionPrompt.SetActive(false);
        }
    }

    void OnEnable()
    {
        playerControls.Player.Interact.performed += OnInteract;
        playerControls.Player.Enable();
    }

    void OnDisable()
    {
        playerControls.Player.Interact.performed -= OnInteract;
        playerControls.Player.Disable();
    }

    void Update()
    {
        FindNearestInteractable();
    }

    /// <summary>
    /// Scans for the nearest interactable object and updates the UI prompt.
    /// </summary>
    private void FindNearestInteractable()
    {
        // Find all colliders within range on the specified layer
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionRange, interactableLayer);
        
        IInteractable nearest = null;
        float minDistance = float.MaxValue;

        foreach (Collider2D col in colliders)
        {
            // Check if this object implements the IInteractable interface
            if (col.TryGetComponent<IInteractable>(out IInteractable interactable))
            {
                float distance = Vector2.Distance(transform.position, col.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = interactable;
                }
            }
        }

        // Check if the nearest interactable has changed
        if (nearest != currentInteractable)
        {
            currentInteractable = nearest;
            UpdatePrompt();
        }
    }

    /// <summary>
    /// Updates the UI prompt's text and visibility.
    /// </summary>
    private void UpdatePrompt()
    {
        if (interactionPrompt == null || promptText == null) return;

        if (currentInteractable != null)
        {
            // We have a target, show the prompt
            promptText.text = currentInteractable.InteractionPromptText;
            interactionPrompt.SetActive(true);
        }
        else
        {
            // No target, hide the prompt
            interactionPrompt.SetActive(false);
        }
    }

    /// <summary>
    /// Called when the "Interact" input action is pressed.
    /// </summary>
    private void OnInteract(InputAction.CallbackContext context)
    {
        // If we have a valid interactable object in range, call its Interact method
        if (currentInteractable != null)
        {
            currentInteractable.Interact(limbController);
            
            // After interacting, clear the target and hide the prompt
            currentInteractable = null;
            UpdatePrompt();
        }
    }

    // Optional: Draw a gizmo in the editor to see the interaction range
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}