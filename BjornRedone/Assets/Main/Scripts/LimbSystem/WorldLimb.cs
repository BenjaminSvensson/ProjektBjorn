using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Added for List

/// <summary>
/// This single script handles a limb that exists in the world.
/// It should be placed ON THE ROOT of your limb prefab (e.g., "GoblinArmPrefab").
/// It manages its state, from being thrown, to landing as a
/// pickup, or landing as a broken part.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldLimb : MonoBehaviour
{
    [Header("Visual State GameObjects (Assign in Prefab)")]
    [Tooltip("The visual to show when attached to the player")]
    [SerializeField] private GameObject defaultVisual;
    [Tooltip("The visual to show when in the air or as a pickup")]
    [SerializeField] private GameObject damagedVisual;
    [Tooltip("The visual to show when the limb breaks on landing")]
    [SerializeField] private GameObject brokenVisual;
    [Tooltip("The child object for the shadow sprite")]
    [SerializeField] private GameObject shadowGameObject;

    [Header("Physics Settings")]
    [SerializeField] private float throwSpeed = 5f;
    [SerializeField] private float throwHeight = 4f; // Starting "up" velocity
    [SerializeField] private float gravity = -9.8f;
    [SerializeField] private float airDrag = 0.98f; // Multiplier to slow speed
    [SerializeField] private float fadeDuration = 3.0f; // Time for broken limbs to fade

    // --- State Variables ---
    private enum State { Attached, Thrown, Pickup, Broken }
    private State currentState;
    
    private LimbData limbData;
    private bool isMaintained;
    private Vector2 moveDirection;
    private float currentVerticalSpeed;
    private Collider2D col;
    
    // We'll cache all renderers for fading
    private List<SpriteRenderer> brokenVisualRenderers = new List<SpriteRenderer>();

    void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        col.enabled = false; // Disable collider until it's a pickup

        // Cache renderers for the broken visual (for fading)
        if (brokenVisual != null)
        {
            brokenVisual.GetComponentsInChildren<SpriteRenderer>(brokenVisualRenderers);
        }

        // Start with all visuals off
        if(defaultVisual) defaultVisual.SetActive(false);
        if(damagedVisual) damagedVisual.SetActive(false);
        if(brokenVisual) brokenVisual.SetActive(false);
        if(shadowGameObject) shadowGameObject.SetActive(false);
    }

    /// <summary>
    /// Called by PlayerLimbController when attaching the limb.
    /// </summary>
    public void InitializeAttached(LimbData data)
    {
        limbData = data;
        currentState = State.Attached;
        
        if(defaultVisual) defaultVisual.SetActive(true); // Show default visual
        
        col.enabled = false;
        if(shadowGameObject) shadowGameObject.SetActive(false);
    }

    /// <summary>
    /// Called by PlayerLimbController to throw this limb.
    /// </summary>
    public void InitializeThrow(LimbData data, bool maintained, Vector2 direction)
    {
        limbData = data;
        isMaintained = maintained;
        moveDirection = direction;
        
        currentVerticalSpeed = throwHeight;
        
        if(shadowGameObject)
        {
            shadowGameObject.SetActive(true);
            shadowGameObject.transform.localPosition = Vector3.zero;
        }

        if(damagedVisual)
        {
            damagedVisual.SetActive(true); // Show damaged visual
            damagedVisual.transform.localPosition = Vector3.zero;
        }
        
        currentState = State.Thrown;
    }

    void Update()
    {
        if (currentState == State.Thrown)
        {
            HandleThrownState();
        }
    }

    private void HandleThrownState()
    {
        // 1. Move horizontally (on the ground plane)
        transform.position += (Vector3)moveDirection * throwSpeed * Time.deltaTime;
        // Apply "air drag"
        throwSpeed *= airDrag;

        // 2. Move vertically (the arc)
        currentVerticalSpeed += gravity * Time.deltaTime;
        // We move the damaged visual, not the shadow
        if(damagedVisual)
        {
            damagedVisual.transform.localPosition = new Vector3(0, damagedVisual.transform.localPosition.y + currentVerticalSpeed * Time.deltaTime, 0);
        }

        // 3. Check for landing
        if(damagedVisual && damagedVisual.transform.localPosition.y <= 0)
        {
            Land();
        }
        else if (damagedVisual == null)
        {
            // Failsafe if no damaged visual is assigned
            Land();
        }
    }

    /// <summary>
    /// Called when the limb hits the ground.
    /// </summary>
    private void Land()
    {
        if(damagedVisual) damagedVisual.transform.localPosition = Vector3.zero; // Snap to ground
        if(shadowGameObject) shadowGameObject.SetActive(false); // No more shadow needed

        if (isMaintained)
        {
            // --- Limb is Maintained ---
            currentState = State.Pickup;
            col.enabled = true; // Enable collider for pickup
            if(damagedVisual) damagedVisual.SetActive(true); // Ensure it's on
        }
        else
        {
            // --- Limb is Broken ---
            currentState = State.Broken;
            if(damagedVisual) damagedVisual.SetActive(false); // Hide damaged visual
            if(brokenVisual) brokenVisual.SetActive(true); // Show broken visual
            StartCoroutine(FadeOutAndDestroy());
        }
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float timer = 0f;
        
        // Set up list of original colors
        List<Color> startColors = new List<Color>();
        foreach (var rend in brokenVisualRenderers)
        {
            startColors.Add(rend.color);
        }

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = 1.0f - (timer / fadeDuration);

            for (int i = 0; i < brokenVisualRenderers.Count; i++)
            {
                Color newColor = startColors[i];
                newColor.a = alpha;
                brokenVisualRenderers[i].color = newColor;
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    // --- Public Methods for PlayerCollision ---
    public bool CanPickup()
    {
        return currentState == State.Pickup;
    }

    public LimbData GetLimbData()
    {
        return limbData;
    }
}