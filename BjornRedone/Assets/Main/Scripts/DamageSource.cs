using UnityEngine;
using System.Collections; // We need this for Coroutines

/// <summary>
/// Attach this script to any object that should damage the player on contact.
/// The object must have a Collider2D set to "Is Trigger = true".
/// The Player must have a Rigidbody2D and the tag "Player".
/// </summary>
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

    [Tooltip("Should this object be destroyed after its first damage impact? (e.g., a bullet)")]
    [SerializeField] private bool destroyOnImpact = false;

    // --- Private Variables ---
    private Coroutine tickDamageCoroutine;
    private PlayerLimbController currentlyTickingPlayer;

    /// <summary>
    /// Called when another collider enters this trigger.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object we hit has the correct tag
        if (other.CompareTag(playerTag))
        {
            // Try to get the PlayerLimbController from the object we hit
            PlayerLimbController player = other.GetComponent<PlayerLimbController>();
            
            if (player != null)
            {
                if (isTickDamage)
                {
                    // --- Start Tick Damage ---
                    // Only start a new coroutine if one isn't already running
                    if (tickDamageCoroutine == null)
                    {
                        currentlyTickingPlayer = player;
                        tickDamageCoroutine = StartCoroutine(TickDamage(player));
                    }
                }
                else
                {
                    // --- Apply Instant Damage ---
                    Debug.Log($"Dealing {damageAmount} instant damage to the player!");
                    player.TakeDamage(damageAmount);

                    // If this is a one-time-use object (like a bullet), destroy it
                    if (destroyOnImpact)
                    {
                        Destroy(gameObject);
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
        // If this is a tick-damage source, we need to stop the coroutine
        if (isTickDamage && other.CompareTag(playerTag))
        {
            PlayerLimbController player = other.GetComponent<PlayerLimbController>();
            
            // Make sure the player exiting is the one we are ticking
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
        // This loop will run forever until StopCoroutine is called
        while (true)
        {
            // Make sure the player is still valid (hasn't been destroyed)
            if (player == null)
            {
                Debug.Log("Player is null, stopping tick damage.");
                tickDamageCoroutine = null;
                yield break; // Exit the coroutine
            }
            
            Debug.Log($"Dealing {damageAmount} tick damage to the player!");
            player.TakeDamage(damageAmount);
            
            // Wait for the specified time before ticking again
            yield return new WaitForSeconds(tickRate);
        }
    }
}