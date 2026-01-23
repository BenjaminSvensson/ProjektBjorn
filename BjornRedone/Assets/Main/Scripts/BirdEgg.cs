using UnityEngine;

public class BirdEgg : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Transform spriteHolder; // Assign the Child Sprite Object here
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer shadowRenderer;

    [Header("Settings")]
    [SerializeField] private float gravity = 40f; // How fast it accelerates down
    [SerializeField] private float damageRadius = 1.5f;
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private float explosionDuration = 0.5f; // Time to destroy after crack

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip impactSound;

    private float verticalSpeed = 0f;
    private float currentHeight = 0f;
    private bool hasHitGround = false;
    private bool isInitialized = false;

    // Called by BirdEnemyAI
    public void Initialize(Vector2 targetPos, Vector2 spawnPos)
    {
        // 1. Place the logical object (hitbox/shadow) on the ground
        transform.position = targetPos;

        // 2. Calculate how high up the bird was
        // We use the Y difference between the bird (spawnPos) and the target (targetPos)
        currentHeight = Mathf.Abs(spawnPos.y - targetPos.y);

        // 3. Set the sprite to that height
        if (spriteHolder)
            spriteHolder.localPosition = new Vector3(0, currentHeight, 0);

        // 4. Reset shadow size (small when high up)
        if (shadowRenderer)
            shadowRenderer.transform.localScale = Vector3.one * 0.5f;

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || hasHitGround) return;

        // --- Simulate Gravity ---
        verticalSpeed -= gravity * Time.deltaTime;
        currentHeight += verticalSpeed * Time.deltaTime;

        // --- Update Shadow Size (Gets bigger as egg falls) ---
        if (shadowRenderer)
        {
            float shadowScale = Mathf.Lerp(1.2f, 0.5f, Mathf.Clamp01(currentHeight / 10f));
            shadowRenderer.transform.localScale = new Vector3(shadowScale, shadowScale * 0.5f, 1f);
        }

        // --- Check for Impact ---
        if (currentHeight <= 0)
        {
            // SNAP to ground
            currentHeight = 0;
            if (spriteHolder) spriteHolder.localPosition = Vector3.zero;
            
            Crack(); // Trigger immediately
        }
        else
        {
            // Apply visual height
            if (spriteHolder) spriteHolder.localPosition = new Vector3(0, currentHeight, 0);
        }
    }

    private void Crack()
    {
        hasHitGround = true;

        // 1. Play Animation
        if (animator) animator.Play("Crack"); // Make sure your Animation clip is named "Crack"

        // 2. Play Sound
        if (audioSource && impactSound) 
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(impactSound);
        }

        // 3. Hide Shadow
        if (shadowRenderer) shadowRenderer.enabled = false;

        // 4. Deal Damage Area
        ExplodeDamage();

        // 5. Destroy Object after animation finishes
        Destroy(gameObject, explosionDuration);
    }

    private void ExplodeDamage()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, damageRadius);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<PlayerLimbController>(out var player))
            {
                // Calculate push direction from egg center
                Vector2 dir = (hit.transform.position - transform.position).normalized;
                player.TakeDamage(damageAmount, dir);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}