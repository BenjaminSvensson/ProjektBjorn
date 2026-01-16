using UnityEngine;

public class FallingIcicle : MonoBehaviour
{
    public float damage = 15f;
    public float targetY; 
    public float fallSpeed = 10f; // Manually control fall speed if not using gravity
    
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // Failsafe: Destroy after 5 seconds no matter what
        Destroy(gameObject, 5f); 
    }

    void Update()
    {
        // 1. Manual Falling (More reliable than gravity for top-down)
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

        // 2. Hit Floor Logic
        if (transform.position.y <= targetY)
        {
            Shatter();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Try to damage player
            var limb = other.GetComponent<PlayerLimbController>();
            if (limb) limb.TakeDamage(damage);

            // Also check for standard movement script health?
            // var health = other.GetComponent<PlayerHealth>();
            // if (health) health.TakeDamage(damage);

            Shatter();
        }
    }

    void Shatter()
    {
        // Add particle effect logic here if you have one
        Destroy(gameObject);
    }
}