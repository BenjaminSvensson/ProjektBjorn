using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))] // Now requires Rigidbody for physics
public class BulletPickup : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Amount of reserve bullets to add.")]
    [SerializeField] private int ammoAmount = 10;
    [SerializeField] private AudioClip pickupSound;
    
    [Header("Physics")]
    [SerializeField] private float groundFriction = 3f;

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Ensure top-down physics behavior
        rb.gravityScale = 0f; 
        rb.linearDamping = groundFriction;
        rb.freezeRotation = false; // Allow it to spin if hit
    }

    // --- UPDATED: Uses Collision instead of Trigger ---
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if we hit the player
        if (collision.gameObject.CompareTag("Player"))
        {
            WeaponSystem ws = collision.gameObject.GetComponent<WeaponSystem>();
            if (ws != null)
            {
                // Add ammo
                ws.AddReserveAmmo(ammoAmount);
                
                // Play Sound
                if (pickupSound != null)
                {
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position);
                }
                
                // Destroy object
                Destroy(gameObject);
            }
        }
    }
}