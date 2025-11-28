using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// This script is attached to the limb prefab. It controls the limb's
/// visual state (default, damaged, broken) and its physical state (attached,
/// thrown, or lying on the ground as a pickup).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldLimb : MonoBehaviour
{
    // --- NEW: For placing limbs directly in the scene ---
    [Header("Scene Pickup Settings (For Prefabs)")]
    [Tooltip("Assign the LimbData here if you are placing this prefab directly in the scene as a pickup.")]
    [SerializeField] private LimbData startingLimbData;
    [Tooltip("Check this if this prefab should start as a 'maintained' (usable) pickup when placed in the scene.")]
    [SerializeField] private bool startAsMaintainedPickup = false;
    // --- NEW ---
    [Tooltip("If it's a 'maintained' pickup, should it show the 'damaged' visual? If false, it shows 'default'.")]
    [SerializeField] private bool startAsDamaged = false;
    // --- END NEW ---

    [Header("Visual State GameObjects (Assign in Prefab)")]
    [SerializeField] private GameObject defaultVisual;
    [SerializeField] private GameObject damagedVisual;
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private GameObject shadowGameObject;

    [Header("Physics Settings")]
    [SerializeField] private float throwForce = 5f;
    [SerializeField] private float pickupDespawnTime = 10f;

    // --- State ---
    private enum State { Idle, Attached, Thrown, Pickup }
    private State currentState = State.Idle;
    
    private LimbData limbData;
    private bool isMaintained = false;
    private Rigidbody2D rb;
    private Collider2D col;

    // We'll cache all renderers for fading
    private List<SpriteRenderer> brokenVisualRenderers = new List<SpriteRenderer>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        
        // Cache renderers for the broken visual (for fading)
        if (brokenVisual != null)
        {
            brokenVisual.GetComponentsInChildren<SpriteRenderer>(brokenVisualRenderers);
        }

        // Disable all visuals by default, they will be set by an Initialize function
        if(defaultVisual) defaultVisual.SetActive(false);
        if(damagedVisual) damagedVisual.SetActive(false);
        if(brokenVisual) brokenVisual.SetActive(false);
        if(shadowGameObject) shadowGameObject.SetActive(false);
    }

    void Start()
    {
        // This logic runs ONLY if the limb was placed in the scene
        // and NOT initialized by the player (since its state is still Idle).
        if (currentState == State.Idle && startingLimbData != null)
        {
            if (startAsMaintainedPickup)
            {
                InitializeAsScenePickup(startingLimbData, true);
            }
            else
            {
                // It's a scene object, but not a pickup (e.g., a "broken" one)
                InitializeAsScenePickup(startingLimbData, false);
            }
        }
        // --- FIX for disappearing limbs ---
        else if (currentState == State.Idle && startingLimbData == null)
        {
            // This is the most likely cause of the "disappearing limb" bug.
            Debug.LogError($"WorldLimb '{gameObject.name}' was placed in the scene but has no 'Starting Limb Data' assigned in the Inspector! It will be invisible.", this);
        }
        // --- END FIX ---
    }

    /// <summary>
    /// Called by PlayerLimbController when attaching the limb.
    /// </summary>
    public void InitializeAttached(LimbData data, bool isPickup)
    {
        limbData = data;
        currentState = State.Attached;
        
        // Explicitly set all visual states
        if (isPickup)
        {
            // This is a used limb, show the damaged visual
            if(defaultVisual) defaultVisual.SetActive(false);
            if(damagedVisual) damagedVisual.SetActive(true);
        }
        else
        {
            // This is a fresh limb (at start), show the default visual
            if(defaultVisual) defaultVisual.SetActive(true);
            if(damagedVisual) damagedVisual.SetActive(false);
        }
        
        if(brokenVisual) brokenVisual.SetActive(false);
        if(shadowGameObject) shadowGameObject.SetActive(false);
        
        col.enabled = false;
        if (rb) rb.bodyType = RigidbodyType2D.Kinematic;
    }

    /// <summary>
    /// Called when a limb is detached from the player.
    /// </summary>
    public void InitializeThrow(LimbData data, bool maintained, Vector2 direction)
    {
        limbData = data;
        currentState = State.Thrown;
        isMaintained = maintained;

        // Detach from parent
        transform.SetParent(null);

        // Set visual state
        if(defaultVisual) defaultVisual.SetActive(false);
        if(damagedVisual) damagedVisual.SetActive(isMaintained); // Show damaged if it's usable
        if(brokenVisual) brokenVisual.SetActive(!isMaintained); // Show broken if it's not
        if(shadowGameObject) shadowGameObject.SetActive(true);

        // Enable physics
        col.enabled = true;
        col.isTrigger = false; // Make it a solid object
        if (rb)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.AddForce(direction * throwForce, ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-90f, 90f));
        }

        // Start timer to become a pickup
        StartCoroutine(BecomePickupAfterDelay(1.0f)); 
    }

    public void InitializeAsScenePickup(LimbData data, bool maintained = true)
    {
        limbData = data;
        currentState = State.Pickup;
        isMaintained = maintained;
        
        // --- NEW VISUAL LOGIC ---
        // Set visual state based on inspector settings
        if(defaultVisual) defaultVisual.SetActive(false);
        if(damagedVisual) damagedVisual.SetActive(false);
        if(brokenVisual) brokenVisual.SetActive(false);

        if (isMaintained)
        {
            if (startAsDamaged)
            {
                if(damagedVisual) damagedVisual.SetActive(true);
            }
            else
            {
                if(defaultVisual) defaultVisual.SetActive(true);
            }
        }
        else
        {
            if(brokenVisual) brokenVisual.SetActive(true);
        }
        // --- END NEW VISUAL LOGIC ---
        
        if(shadowGameObject) shadowGameObject.SetActive(true);

        // Enable collider as a trigger
        col.enabled = true;
        col.isTrigger = true;
        
        // Disable physics
        if (rb) rb.bodyType = RigidbodyType2D.Kinematic;

        // Set tag based on if it's usable
        if (isMaintained)
        {
            gameObject.tag = "LimbPickup";
            // Scene-placed pickups should not despawn
        }
        else
        {
            gameObject.tag = "Untagged"; // It's just a broken prop
        }
    }


    /// <summary>
    /// Waits for a moment, then settles the limb on the ground.
    /// </summary>
    private IEnumerator BecomePickupAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        currentState = State.Pickup;
        
        // Stop physics
        if (rb)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0;
        }
        
        // Make it a trigger so the player can pick it up
        col.isTrigger = true;

        if (isMaintained)
        {
            // It's usable
            gameObject.tag = "LimbPickup";
            // Start the despawn timer *only for thrown limbs*
            StartCoroutine(DespawnTimer(pickupDespawnTime));
        }
        else
        {
            // It's just a broken prop
            gameObject.tag = "Untagged";
            // Start fading out the broken visual
            StartCoroutine(FadeOutBrokenLimb(2.0f));
        }
    }

    private IEnumerator DespawnTimer(float duration)
    {
        yield return new WaitForSeconds(duration);
        // Add a small fade-out effect here later if you want
        Destroy(gameObject);
    }

    private IEnumerator FadeOutBrokenLimb(float duration)
    {
        yield return new WaitForSeconds(duration); // Wait a bit before fading

        float timer = 0f;
        while (timer < duration)
        {
            float alpha = Mathf.Lerp(1f, 0f, timer / duration);
            foreach (var sr in brokenVisualRenderers)
            {
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
            }
            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Checks if the player is allowed to pick up this limb.
    /// </summary>
    /// <returns>True if the limb is in the Pickup state and is usable.</returns>
    public bool CanPickup()
    {
        // The player can pick up this limb if it's in the Pickup state
        // AND it's a "maintained" (usable) limb.
        return (currentState == State.Pickup && isMaintained);
    }

    public LimbData GetLimbData()
    {
        return limbData;
    }
}