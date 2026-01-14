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

    [Header("Magnet Settings")]
    [SerializeField] private float magnetRadius = 5f; // How close player needs to be
    [SerializeField] private float magnetSpeed = 10f; // How fast it flies to player

    [Header("Physics")]
    [SerializeField] private float scatterForce = 5f;

    [Header("Audio")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private float soundVolume = 0.6f;

    private bool isReady = false;
    private bool initialized = false;
    private Collider2D col;
    private Rigidbody2D rb;
    private Transform playerTransform; // Reference to the player

    void Awake()
    {
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        // 1. Find the player automatically by Tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        // Fallback: If spawned manually in scene for testing
        if (!initialized)
        {
            Initialize();
        }
    }

    // 2. Add Update loop to handle the Magnet movement
    void Update()
    {
        // Only magnetize if the scatter animation is finished and player exists
        if (!isReady || playerTransform == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer < magnetRadius)
        {
            // Move towards the player
            transform.position = Vector2.MoveTowards(
                transform.position, 
                playerTransform.position, 
                magnetSpeed * Time.deltaTime
            );
        }
    }

    public void Initialize()
    {
        initialized = true;

        // Scatter!
        if (rb != null)
        {
            rb.gravityScale = 0f; 
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
            rb.bodyType = RigidbodyType2D.Kinematic; // Kinematic allows us to move it via Transform in Update
        }
        
        isReady = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        if (!isReady) return;

        if (other.CompareTag("Player"))
        {
            PlayerWallet wallet = other.GetComponent<PlayerWallet>();
            
            if (wallet == null) wallet = other.GetComponentInParent<PlayerWallet>();

            if (wallet != null)
            {
                Collect(wallet);
            }
            else
            {
                Debug.LogWarning($"Coin hit Player ({other.name}), but no 'PlayerWallet' script found!");
            }
        }
    }

    private void Collect(PlayerWallet wallet)
    {
        wallet.AddCoins(coinValue);

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, soundVolume);
        }

        Destroy(gameObject);
    }

    // 3. Editor visualization for the magnet range
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, magnetRadius);
    }
}