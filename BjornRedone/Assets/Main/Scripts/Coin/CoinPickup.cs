using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class CoinPickup : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int coinValue = 1;
    [SerializeField] private float autoPickupDelay = 0.5f; // Time before it can be picked up
    [SerializeField] private float despawnTime = 60f;

    [Header("Physics")]
    [SerializeField] private float scatterForce = 5f;

    [Header("Audio")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private float soundVolume = 0.6f;

    private bool isReady = false;
    private bool initialized = false;
    private Collider2D col;
    private Rigidbody2D rb;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        // Fallback: If spawned manually in scene for testing (not by enemy), initialize automatically
        if (!initialized)
        {
            Initialize();
        }
    }

    public void Initialize()
    {
        initialized = true;

        // Scatter!
        if (rb != null)
        {
            rb.gravityScale = 0f; // Ensure it doesn't fall off screen in Top-Down view
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            rb.AddForce(randomDir * scatterForce, ForceMode2D.Impulse);
        }
        
        // Solid initially so it bounces
        if (col != null)
        {
            col.isTrigger = false;
        }
        
        StartCoroutine(EnablePickupRoutine());
        Destroy(gameObject, despawnTime);
    }

    private IEnumerator EnablePickupRoutine()
    {
        yield return new WaitForSeconds(autoPickupDelay);
        
        // Turn into a trigger so the player can walk over it
        if (col != null) col.isTrigger = true;
        
        // Stop physics movement
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero; 
            rb.bodyType = RigidbodyType2D.Kinematic; 
        }
        
        isReady = true;
    }

    // Fires when entering the trigger
    void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    // Fires every frame you stand inside (Fixes the "standing still" bug)
    void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        if (!isReady) return;

        // IMPORTANT: Ensure your Player GameObject has the Tag "Player"
        if (other.CompareTag("Player"))
        {
            PlayerWallet wallet = other.GetComponent<PlayerWallet>();
            
            // If the collider is on a child object (like feet), look in parent
            if (wallet == null) wallet = other.GetComponentInParent<PlayerWallet>();

            if (wallet != null)
            {
                Collect(wallet);
            }
            else
            {
                // DEBUG: If you see this, you forgot to add the PlayerWallet script to your player!
                Debug.LogWarning($"Coin hit object tagged 'Player' ({other.name}), but no 'PlayerWallet' script was found on it or its parents!");
            }
        }
    }

    private void Collect(PlayerWallet wallet)
    {
        // Debug.Log("Coin Collected!"); // Uncomment to verify collection
        wallet.AddCoins(coinValue);

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, soundVolume);
        }

        Destroy(gameObject);
    }
}