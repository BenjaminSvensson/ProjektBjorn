using UnityEngine;
using System.Collections; // We need this for Coroutines
using System.Collections.Generic; // We need this for Lists

/// <summary>
/// Attach this script to any object that should damage the player on contact.
/// The object must have a Collider2D set to "Is Trigger = true".
/// The Player must have a Rigidbody2D and the tag "Player".
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))] 
public class DamageSource : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("The amount of damage to deal.")]
    [SerializeField] private float damageAmount = 10f;

    [Tooltip("The tag of the object to damage. Should be 'Player'.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Damage Type")]
    [Tooltip("Is this damage over time (like poison)? If false, damage is applied once.")]
    [SerializeField] private bool isTickDamage = false;

    [Tooltip("How often to apply tick damage (in seconds). Only used if 'Is Tick Damage' is true.")]
    [SerializeField] private float tickRate = 1f;

    [Header("Beartrap Settings")]
    [Tooltip("If checked, this trap will stop the player from moving for a duration.")]
    [SerializeField] private bool isBeartrap = false;
    [Tooltip("How long (in seconds) the player is stuck. Only used if 'Is Beartrap' is true.")]
    [SerializeField] private float trapDuration = 3f;
    // --- NEW: Beartrap Visuals ---
    [Tooltip("The GameObject holding the 'open' sprite. Will be hidden when triggered.")]
    [SerializeField] private GameObject openVisual;
    [Tooltip("The GameObject holding the 'closed' sprite. Will be shown when triggered.")]
    [SerializeField] private GameObject closedVisual;
    // --- END NEW ---

    [Header("Destruction")]
    [Tooltip("Should this object be destroyed after its first damage impact? (e.g., a bullet, a beartrap)")]
    [SerializeField] private bool destroyOnImpact = false;
    [Tooltip("How long to fade out the sprite before destroying.")]
    [SerializeField] private float fadeOutTime = 0.5f;

    [Header("Audio")]
    [Tooltip("The sound to play when damage is dealt.")]
    [SerializeField] private AudioClip damageSound;
    private AudioSource audioSource;

    // --- Private Variables ---
    private Coroutine tickDamageCoroutine;
    private PlayerLimbController currentlyTickingPlayer;
    private bool isTriggered = false; // Prevents one-shot traps from firing multiple times
    private List<SpriteRenderer> allRenderers = new List<SpriteRenderer>(); // For fading

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        
        // Find all renderers for fading
        GetComponentsInChildren<SpriteRenderer>(allRenderers);

        // --- NEW: Initialize visuals ---
        if (isBeartrap)
        {
            if (openVisual != null) openVisual.SetActive(true);
            if (closedVisual != null) closedVisual.SetActive(false);
        }
        // --- END NEW ---
    }

    /// <summary>
    /// Called when another collider enters this trigger.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // If this trap has already been triggered (e.g., a bullet), don't run again.
        if (isTriggered) return;

        if (other.CompareTag(playerTag))
        {
            PlayerLimbController player = other.GetComponent<PlayerLimbController>();
            
            if (player != null)
            {
                // --- BEARTRAP LOGIC ---
                if (isBeartrap)
                {
                    isTriggered = true; // This trap is used up
                    PlayerMovement playerMovement = other.GetComponent<PlayerMovement>();
                    
                    if (playerMovement != null)
                    {
                        Debug.Log("BEARTRAP! Player is trapped.");
                        playerMovement.SetTrapped(true);
                    }
                    
                    player.TakeDamage(damageAmount);
                    PlayDamageSound();
                    
                    // --- NEW: Swap visuals ---
                    if (openVisual != null) openVisual.SetActive(false);
                    if (closedVisual != null) closedVisual.SetActive(true);
                    // --- END NEW ---

                    // Start coroutine to release player and destroy trap
                    StartCoroutine(BeartrapReleaseCoroutine(playerMovement));
                    return; // Don't run any other logic
                }
                
                // --- TICK DAMAGE LOGIC ---
                if (isTickDamage)
                {
                    if (tickDamageCoroutine == null)
                    {
                        currentlyTickingPlayer = player;
                        tickDamageCoroutine = StartCoroutine(TickDamage(player));
                    }
                }
                // --- INSTANT DAMAGE LOGIC ---
                else
                {
                    player.TakeDamage(damageAmount);

                    if (destroyOnImpact)
                    {
                        isTriggered = true; // Mark as used
                        StartCoroutine(PlaySoundAndDestroy());
                    }
                    else
                    {
                        PlayDamageSound(); // For non-destroying spikes
                    }
                }
            }
        }
    }

    /// <summary>
    /// Called when another collider exits this trigger.
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        if (isTickDamage && other.CompareTag(playerTag))
        {
            PlayerLimbController player = other.GetComponent<PlayerLimbController>();
            
            if (player != null && player == currentlyTickingPlayer)
            {
                if (tickDamageCoroutine != null)
                {
                    Debug.Log("Stopping tick damage...");
                    StopCoroutine(tickDamageCoroutine);
                    tickDamageCoroutine = null;
                    currentlyTickingPlayer = null;
                }
            }
        }
    }

    /// <summary>
    /// A Coroutine that applies damage repeatedly.
    /// </summary>
    private IEnumerator TickDamage(PlayerLimbController player)
    {
        Debug.Log("Starting tick damage...");
        while (true)
        {
            if (player == null)
            {
                Debug.Log("Player is null, stopping tick damage.");
                tickDamageCoroutine = null;
                yield break; 
            }
            
            Debug.Log($"Dealing {damageAmount} tick damage to the player!");
            player.TakeDamage(damageAmount);
            PlayDamageSound();
            
            yield return new WaitForSeconds(tickRate);
        }
    }

    /// <summary>
    /// Plays the sound, disables the object, fades it out, then destroys it.
    /// </summary>
    private IEnumerator PlaySoundAndDestroy()
    {
        // Disable collider so it can't be hit again
        GetComponent<Collider2D>().enabled = false;
        
        PlayDamageSound();
        
        // Wait for the sound to play (or fade, whichever is longer)
        float soundLength = damageSound != null ? damageSound.length : 0f;
        float waitTime = Mathf.Max(soundLength, fadeOutTime);

        yield return StartCoroutine(FadeOut(fadeOutTime));
        
        // Wait any remaining time needed for sound
        if (waitTime > fadeOutTime)
        {
            yield return new WaitForSeconds(waitTime - fadeOutTime);
        }

        Destroy(gameObject);
    }
    
    /// <summary>
    /// Coroutine to release the player from the trap.
    /// </summary>
    private IEnumerator BeartrapReleaseCoroutine(PlayerMovement playerMovement)
    {
        // Disable collider
        GetComponent<Collider2D>().enabled = false;

        yield return new WaitForSeconds(trapDuration);
        
        if (playerMovement != null)
        {
            Debug.Log("Player released from trap.");
            playerMovement.SetTrapped(false);
        }
        
        // Fade out and destroy the trap
        yield return StartCoroutine(FadeOut(fadeOutTime));
        Destroy(gameObject);
    }

    /// <summary>
    /// Fades all sprites on this object and its children.
    /// </summary>
    private IEnumerator FadeOut(float duration)
    {
        float timer = 0f;
        
        // --- MODIFIED: We must re-find the renderers ---
        // We only found them in Awake(), but the active
        // visual (open/closed) has changed.
        allRenderers.Clear();
        GetComponentsInChildren<SpriteRenderer>(allRenderers);
        // --- END MODIFICATION ---

        // Get initial colors
        Dictionary<SpriteRenderer, Color> initialColors = new Dictionary<SpriteRenderer, Color>();
        foreach (var sr in allRenderers)
        {
            if(sr != null) initialColors[sr] = sr.color;
        }

        while (timer < duration)
        {
            float alpha = Mathf.Lerp(1f, 0f, timer / duration);
            foreach (var entry in initialColors)
            {
                if(entry.Key != null)
                    entry.Key.color = new Color(entry.Value.r, entry.Value.g, entry.Value.b, alpha);
            }
            
            timer += Time.deltaTime;
            yield return null;
        }
        
        // Ensure final alpha is 0
        foreach (var entry in initialColors)
        {
            if(entry.Key != null)
                entry.Key.color = new Color(entry.Value.r, entry.Value.g, entry.Value.b, 0f);
        }
    }

    /// <summary>
    /// Plays the assigned damage sound, if one exists.
    /// </summary>
    private void PlayDamageSound()
    {
        if (audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }
    }
}