using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class BulletPickup : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Amount of reserve bullets to add.")]
    [SerializeField] private int ammoAmount = 10;
    [SerializeField] private AudioClip pickupSound;

    [Header("Physics")]
    [SerializeField] private float groundFriction = 3f;

    [Header("Magnet Settings")]
    [SerializeField] private float magnetRadius = 5f; // Distance to trigger magnet
    [SerializeField] private float magnetSpeed = 10f; // Flight speed

    private Rigidbody2D rb;
    private Transform playerTransform;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Ensure top-down physics behavior
        rb.gravityScale = 0f;
        rb.linearDamping = groundFriction; 
        rb.freezeRotation = false; // Allow it to spin if hit
    }

    void Start()
    {
        // Automatically find the player by Tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    void Update()
    {
        // If player exists, check distance
        if (playerTransform != null)
        {
            float distance = Vector2.Distance(transform.position, playerTransform.position);

            // If inside range, fly towards player
            if (distance < magnetRadius)
            {
                // We use transform.position here to override physics friction when magnetizing
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    playerTransform.position,
                    magnetSpeed * Time.deltaTime
                );
            }
        }
    }

    // --- Uses Collision (as requested to keep functionality) ---
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if we hit the player
        if (collision.gameObject.CompareTag("Player"))
        {
            WeaponSystem ws = collision.gameObject.GetComponent<WeaponSystem>();
            if (ws != null)
            {
                // 1. Add ammo
                ws.AddReserveAmmo(ammoAmount);

                // 2. Play Sound
                if (pickupSound != null)
                {
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position);
                }

                // 3. Destroy object
                Destroy(gameObject);
            }
        }
    }

    // Visualize range in Editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; // Yellow to distinguish as Ammo
        Gizmos.DrawWireSphere(transform.position, magnetRadius);
    }
}