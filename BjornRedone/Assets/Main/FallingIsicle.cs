using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class FallingIcicle : MonoBehaviour
{
    [Header("Settings")]
    public float fallSpeed = 15f;
    public float damage = 15f;
    
    [Header("Visuals & Audio")]
    public GameObject impactEffect; 
    public AudioClip[] impactSounds; 

    private float groundY;
    private bool isInitialized = false;
    private bool hasImpacted = false; // Prevents double-hits

    private AudioSource audioSource;
    private SpriteRenderer spriteRenderer;
    private Collider2D col;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    public void Setup(float targetY)
    {
        groundY = targetY;
        isInitialized = true;
    }

    void Update()
    {
        // Stop logic if we already hit the ground (we are just waiting for sound to finish now)
        if (!isInitialized || hasImpacted) return;

        // Fall down
        transform.Translate(Vector3.down * fallSpeed * Time.deltaTime);

        if (transform.position.y <= groundY)
        {
            Impact();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasImpacted) return;

        if (other.CompareTag("Player"))
        {
            var playerLimb = other.GetComponent<PlayerLimbController>();
            if (playerLimb)
            {
                playerLimb.TakeDamage(damage, Vector2.zero); 
                Impact();
            }
        }
    }

    void Impact()
    {
        hasImpacted = true;

        // 1. Turn invisible and disable collisions (So it LOOKS destroyed)
        if(spriteRenderer) spriteRenderer.enabled = false;
        if(col) col.enabled = false;

        // 2. Spawn Particles
        if (impactEffect) Instantiate(impactEffect, transform.position, Quaternion.identity);

        // 3. Play Sound and Wait
        if (impactSounds != null && impactSounds.Length > 0)
        {
            AudioClip clip = impactSounds[Random.Range(0, impactSounds.Length)];
            
            // Randomize pitch slightly for variety
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(clip);

            // Destroy the object only after the sound is done
            Destroy(gameObject, clip.length);
        }
        else
        {
            // No sound assigned? Destroy immediately.
            Destroy(gameObject);
        }
    }
}