using UnityEngine;

public class BirdEgg : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("The sprite object that moves up and down")]
    public Transform spriteVisual;
    [Tooltip("The shadow object that stays on the ground")]
    public Transform shadowVisual;
    
    [Header("Settings")]
    public float fallSpeed = 15.0f;
    public float damage = 10.0f;
    public float damageRadius = 0.5f;
    [Tooltip("How long the egg sits on the ground before disappearing")]
    public float persistDuration = 0.5f; 
    
    // Shadow visual settings matching the bird
    private Vector3 shadowScaleGround = new Vector3(0.5f, 0.25f, 1f);
    private Vector3 shadowScaleAir = new Vector3(0.2f, 0.1f, 1f);

    private float currentHeight;
    private bool isInitialized = false;
    private bool hasLanded = false;

    // Called by the BirdAI when spawning the egg
    public void Initialize(float startHeight, Vector2 groundPos)
    {
        currentHeight = startHeight;
        
        // CRITICAL: We move the entire object (including shadow) to the destination immediately.
        transform.position = groundPos; 
        
        // We set the sprite high up to simulate it being in the air.
        if (spriteVisual) spriteVisual.localPosition = new Vector3(0, currentHeight, 0);

        isInitialized = true;
    }

    void Update()
    {
        // If not setup or already on the ground, do nothing
        if (!isInitialized || hasLanded) return;

        // 1. Fall Logic
        currentHeight -= fallSpeed * Time.deltaTime;

        // 2. Impact Check
        if (currentHeight <= 0)
        {
            Land();
            return;
        }

        // 3. Update Visuals while falling
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        // Move Sprite
        if (spriteVisual)
        {
            spriteVisual.localPosition = new Vector3(0, currentHeight, 0);
        }

        // Scale Shadow (Small in air -> Big on ground)
        if (shadowVisual)
        {
            // Assuming 15.0f is a reasonable max height reference for scaling
            float ratio = Mathf.Clamp01(currentHeight / 15.0f); 
            shadowVisual.localScale = Vector3.Lerp(shadowScaleGround, shadowScaleAir, ratio);
            
            SpriteRenderer sr = shadowVisual.GetComponent<SpriteRenderer>();
            if(sr)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(0.8f, 0.2f, ratio);
                sr.color = c;
            }
        }
    }

    void Land()
    {
        hasLanded = true;
        currentHeight = 0;
        
        // Force visuals to ground state
        if (spriteVisual) spriteVisual.localPosition = Vector3.zero;
        if (shadowVisual)
        {
            shadowVisual.localScale = shadowScaleGround;
            SpriteRenderer sr = shadowVisual.GetComponent<SpriteRenderer>();
            if(sr) { Color c = sr.color; c.a = 0.8f; sr.color = c; }
        }

        // Deal Damage
        Explode();

        // Stop the egg (Keep it on ground for a moment before destroying)
        Destroy(gameObject, persistDuration);
    }

    void Explode()
    {
        // Optional: Spawn a particle effect prefab here if you have one
        // Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        // Damage Check
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, damageRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                hit.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}