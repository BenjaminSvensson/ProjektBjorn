using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    [SerializeField, Range(0f, 1f)]
    [Tooltip("How faint the shadow gets at its peak height (0.0 = invisible, 1.0 = no change)")]
    private float minShadowAlphaFactor = 0.2f;
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
    private float currentSpinSpeed; // --- NEW ---
    
    // --- NEW ---
    private SpriteRenderer shadowSpriteRenderer;
    private float originalShadowAlpha;
    private float calculatedMaxHeight = 1f; // The peak of the arc

    // We'll cache all renderers for fading
    private List<SpriteRenderer> brokenVisualRenderers = new List<SpriteRenderer>();

    void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        col.enabled = false; // Disable collider until it's a pickup

        // --- NEW ---
        // Get the shadow's renderer and store its original alpha
        if (shadowGameObject != null)
        {
            shadowSpriteRenderer = shadowGameObject.GetComponent<SpriteRenderer>();
        }
        if (shadowSpriteRenderer != null)
        {
            originalShadowAlpha = shadowSpriteRenderer.color.a;
        }
        else
        {
            Debug.LogWarning("WorldLimb couldn't find a SpriteRenderer on its shadowGameObject!");
        }
        // --- END NEW ---

        // Cache renderers for the broken visual (for fading)
        // --- THIS IS THE FIX ---
        if (brokenVisual != null)
        {
            brokenVisual.GetComponentsInChildren<SpriteRenderer>(brokenVisualRenderers);
        }
        // ------------------------

        // We can remove the "Start with all visuals off" logic from Awake,
        // as our Initialize functions will now handle this robustly.
        // if(defaultVisual) defaultVisual.SetActive(false);
        // if(damagedVisual) damagedVisual.SetActive(false);
        // if(brokenVisual) brokenVisual.SetActive(false);
        // if(shadowGameObject) shadowGameObject.SetActive(false);
    }

    /// <summary>
    /// Called by PlayerLimbController when attaching the limb.
    /// </summary>
    public void InitializeAttached(LimbData data)
    {
        limbData = data;
        currentState = State.Attached;
        
        // --- FIX ---
        // Explicitly set all visual states
        if(defaultVisual) defaultVisual.SetActive(true);
        if(damagedVisual) damagedVisual.SetActive(false);
        if(brokenVisual) brokenVisual.SetActive(false);
        if(shadowGameObject) shadowGameObject.SetActive(false);
        
        col.enabled = false;
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
        currentSpinSpeed = Random.Range(-480f, 480f); // --- NEW: Set a random spin speed ---
        
        // --- NEW ---
        // Calculate the peak height of the arc for shadow scaling
        // Formula: peak = (initial_velocity^2) / (2 * -gravity)
        calculatedMaxHeight = (throwHeight * throwHeight) / (2 * -gravity);
        if (calculatedMaxHeight <= 0) calculatedMaxHeight = 1f; // Failsafe
        // --- END NEW ---

        // Explicitly set all visual states
        if(defaultVisual) defaultVisual.SetActive(false);
        if(damagedVisual) damagedVisual.SetActive(true); // Show damaged visual
        if(brokenVisual) brokenVisual.SetActive(false);
        if(shadowGameObject)
        {
            shadowGameObject.SetActive(true);
            shadowGameObject.transform.localPosition = Vector3.zero;
        }

        if(damagedVisual)
        {
            damagedVisual.transform.localPosition = Vector3.zero;
            damagedVisual.transform.localRotation = Quaternion.identity; // --- NEW: Reset rotation ---
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
            
            // --- NEW: Apply spin ---
            damagedVisual.transform.Rotate(0, 0, currentSpinSpeed * Time.deltaTime);
        }

        // --- NEW SHADOW FADE LOGIC ---
        if (shadowSpriteRenderer != null && calculatedMaxHeight > 0)
        {
            float currentHeight = (damagedVisual != null) ? damagedVisual.transform.localPosition.y : 0;
            currentHeight = Mathf.Max(0, currentHeight); // Ensure it's not negative

            // Calculate how "high" we are as a 0-1 percentage
            float heightPercent = Mathf.Clamp01(currentHeight / calculatedMaxHeight);

            // Lerp the alpha factor. 1.0f (full alpha) at ground (0% height),
            // minShadowAlphaFactor at peak (100% height)
            float newAlphaFactor = Mathf.Lerp(1.0f, minShadowAlphaFactor, heightPercent);
            
            // Apply the new alpha, based on the shadow's original alpha
            Color shadowColor = shadowSpriteRenderer.color;
            shadowColor.a = originalShadowAlpha * newAlphaFactor;
            shadowSpriteRenderer.color = shadowColor;
        }
        // --- END NEW LOGIC ---

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
        Quaternion landingRotation = Quaternion.identity; // --- NEW: Store landing rotation ---
        if (damagedVisual)
        {
            landingRotation = damagedVisual.transform.rotation;
            damagedVisual.transform.localPosition = Vector3.zero; // Snap to ground
        }

        if(shadowGameObject) shadowGameObject.SetActive(false); // No more shadow needed

        // --- NEW ---
        // Reset shadow alpha just in case
        if (shadowSpriteRenderer != null)
        {
            Color shadowColor = shadowSpriteRenderer.color;
            shadowColor.a = originalShadowAlpha;
            shadowSpriteRenderer.color = shadowColor;
        }
        // --- END NEW ---

        if (isMaintained)
        {
            // --- Limb is Maintained ---
            currentState = State.Pickup;
            col.enabled = true; // Enable collider for pickup
            
            // --- FIX ---
            // Explicitly set all visual states
            if(defaultVisual) defaultVisual.SetActive(false);
            if(damagedVisual) damagedVisual.SetActive(true); // Ensure it's on
            if(brokenVisual) brokenVisual.SetActive(false);
        }
        else
        {
            // --- Limb is Broken ---
            currentState = State.Broken;

            // --- FIX ---
            // Explicitly set all visual states
            if(defaultVisual) defaultVisual.SetActive(false);
            if(damagedVisual) damagedVisual.SetActive(false); // Hide damaged visual
            if(brokenVisual)
            {
                brokenVisual.SetActive(true); // Show broken visual
                brokenVisual.transform.rotation = landingRotation; // --- NEW: Apply landing rotation ---
            }
            
            StartCoroutine(FadeOutAndDestroy());
        }
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float timer = 0f;
        
        // Set up list of original colors
        List<Color> startColors = new List<Color>();
        if (brokenVisualRenderers.Count == 0)
        {
            Debug.LogWarning("No SpriteRenderers found in brokenVisual to fade!", this);
        }
        
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