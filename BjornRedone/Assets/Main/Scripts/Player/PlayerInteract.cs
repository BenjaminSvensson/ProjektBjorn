using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; 

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("The maximum range the player can interact from.")]
    [SerializeField] private float interactionRange = 2f;
    [Tooltip("The layer(s) that contain interactable objects (Weapons, Levers, Shop Items).")]
    [SerializeField] private LayerMask interactableLayer;

    [Header("UI")]
    [Tooltip("Reference to the GameObject that shows 'Press F to Pickup'.")]
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private TextMeshProUGUI promptText;

    private InputSystem_Actions playerControls;
    private IInteractable currentInteractable;

    void Awake()
    {
        playerControls = new InputSystem_Actions();
        
        if (interactionPrompt != null)
        {
            if (promptText == null) promptText = interactionPrompt.GetComponentInChildren<TextMeshProUGUI>();
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

    private void FindNearestInteractable()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionRange, interactableLayer);
        
        IInteractable nearest = null;
        float minDistance = float.MaxValue;

        foreach (Collider2D col in colliders)
        {
            // Check for IInteractable on the object or its parent
            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable == null) interactable = col.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                float distance = Vector2.Distance(transform.position, interactable.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = interactable;
                }
            }
        }

        if (nearest != currentInteractable)
        {
            currentInteractable = nearest;
            UpdatePrompt();
        }
    }

    private void UpdatePrompt()
    {
        if (interactionPrompt == null) return;

        if (currentInteractable != null)
        {
            interactionPrompt.SetActive(true);
            if (promptText != null)
            {
                promptText.text = $"[F] {currentInteractable.GetInteractionPrompt()}";
            }
        }
        else
        {
            interactionPrompt.SetActive(false);
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (currentInteractable != null)
        {
            // Pass the Player GameObject as the interactor
            currentInteractable.Interact(gameObject);
            
            // Clear current interactable immediately to avoid double-press issues
            // It will be found again next frame if it wasn't destroyed
            currentInteractable = null;
            UpdatePrompt();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}