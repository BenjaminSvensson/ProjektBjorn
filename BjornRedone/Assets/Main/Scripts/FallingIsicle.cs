using UnityEngine;

public class FallingIcicle : MonoBehaviour
{
    [Header("Settings")]
    public float damage = 15f;
    public float fallSpeed = 10f; 
    
    [Header("Visual Effects")]
    [Tooltip("Drag particle effects or broken ice prefabs here. One will be chosen randomly on impact.")]
    public GameObject[] onHitEffects;

    // Internal variables
    [HideInInspector] public float targetY; // Set by the Penguin script
    private bool hasShattered = false;

    void Start()
    {
        // Failsafe: Destroy after 5 seconds if it somehow misses everything
        Destroy(gameObject, 5f); 
    }

    void Update()
    {
        if (hasShattered) return;

        // Manual Falling logic
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

        // Hit Floor Logic
        // Check if we passed the target Y height
        if (transform.position.y <= targetY)
        {
            // Snap to the ground position so the effect spawns exactly on the floor
            transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
            Shatter();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasShattered) return;

        if (other.CompareTag("Player"))
        {
            // Damage Player via Limb Controller
            var limb = other.GetComponent<PlayerLimbController>();
            if (limb) limb.TakeDamage(damage);

            // Optional: Damage Player via standard health script if you use one
            // var health = other.GetComponent<PlayerHealth>();
            // if (health) health.TakeDamage(damage);

            Shatter();
        }
    }

    void Shatter()
    {
        hasShattered = true;

        // --- SPAWN COOL EFFECTS ---
        if (onHitEffects != null && onHitEffects.Length > 0)
        {
            // Pick a random effect from the list
            int randomIndex = Random.Range(0, onHitEffects.Length);
            GameObject selectedEffect = onHitEffects[randomIndex];

            if (selectedEffect != null)
            {
                // Ensure Z is -1 so it appears in front of the ground
                Vector3 spawnPos = new Vector3(transform.position.x, transform.position.y, -1f);
                
                // Spawn it!
                GameObject effectInstance = Instantiate(selectedEffect, spawnPos, Quaternion.identity);

                // Optional: If it's just sprite chunks and not a particle system that auto-destroys, 
                // clean it up after a few seconds.
                Destroy(effectInstance, 3f);
            }
        }
        // ---------------------------

        // Destroy the main icicle object
        Destroy(gameObject);
    }
}