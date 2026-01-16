using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PenguinEnemyAI : MonoBehaviour
{
    private enum State { Idle, Chasing, CastingMagic, MeleeAttacking, Dead }

    [Header("Components")]
    public Animator animator; 
    public Collider2D mainCollider; 
    
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public GameObject deathEffect; 

    [Header("Core Settings")]
    public float moveSpeed = 3f;
    public float stopDistance = 0.5f; 
    public float wakeUpDistance = 15f; 

    [Header("Melee Settings")]
    public float meleeRange = 1.5f;
    public float meleeDamage = 10f;
    public float meleeKnockbackForce = 15f; 
    public float meleeCooldown = 2.0f;
    public float meleeWindUpTime = 0.3f; 

    [Header("Magic General")]
    public float magicRange = 10f;
    public float magicCooldown = 5.0f;
    public float castTime = 1.0f; 

    [Header("Magic 1: Icicles (Sky Drop)")]
    public GameObject iciclePrefab;
    public GameObject icicleWarningPrefab;
    public int icicleCount = 3;
    public float icicleRadius = 3f; 
    public float icicleSpawnHeight = 10f; 
    public float icicleWarningDuration = 1.0f; 

    [Header("Magic 2: Ice Traps")]
    public GameObject warningVisualPrefab; 
    public GameObject trapPrefab;
    public int trapCount = 2;
    public float trapDelay = 1.0f; 

    [Header("Magic 3: Ice Wall")]
    public GameObject iceWallPrefab;
    public float wallOffsetDistance = 2.0f; 
    public float wallLifeTime = 5.0f;

    [Header("Audio")]
    public AudioClip[] meleeSounds;
    public AudioClip[] magicCastSounds;
    public AudioClip[] magicImpactSounds; 
    public AudioClip[] trapSetSounds;
    public AudioClip[] hurtSounds; 
    public AudioClip deathSound;   

    // Internal
    private Transform player;
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private SpriteRenderer spriteRenderer; // NEW: Needed for color flash
    private State currentState = State.Idle;
    
    private float currentHealth;
    private Vector3 originalScale; 
    private float meleeTimer;
    private float magicTimer;
    private int lastMagicType = 0; 
    private bool isBusy = false; 

    void Start()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0; 
        audioSource = GetComponent<AudioSource>();

        if (mainCollider == null) mainCollider = GetComponent<Collider2D>();

        // Try to find the SpriteRenderer on this object or children (in case art is separate)
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
            originalScale = animator.transform.localScale;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
    }

    // --- UPDATED DAMAGE SYSTEM ---
    public void TakeDamage(float amount)
    {
        if (currentState == State.Dead) return;

        currentHealth -= amount;
        
        PlayRandomSound(hurtSounds);
        
        // Trigger the Red Flash
        StartCoroutine(FlashDamageEffect());
        
        // Optional: Trigger Hurt Animation
        if(animator) animator.SetTrigger("Hurt"); 

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    IEnumerator FlashDamageEffect()
    {
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color; // Remember normal color
            spriteRenderer.color = Color.red;           // Turn Red
            
            yield return new WaitForSeconds(0.1f);      // Wait 0.1 seconds
            
            spriteRenderer.color = originalColor;       // Return to normal
        }
    }
    // -----------------------------

    void Die()
    {
        currentState = State.Dead;
        rb.linearVelocity = Vector2.zero;
        isBusy = true;

        if (deathSound && audioSource) audioSource.PlayOneShot(deathSound);
        if (animator) animator.SetTrigger("Death"); 
        
        if (mainCollider) mainCollider.enabled = false; 
        
        if (deathEffect) Instantiate(deathEffect, transform.position, Quaternion.identity);

        Destroy(gameObject, 5f);
    }

    void Update()
    {
        if (player == null || currentState == State.Dead) return;

        if (meleeTimer > 0) meleeTimer -= Time.deltaTime;
        if (magicTimer > 0) magicTimer -= Time.deltaTime;

        float distToPlayer = Vector2.Distance(transform.position, player.position);

        if (isBusy) 
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (distToPlayer > wakeUpDistance)
        {
            SetAnimation("Idle");
            return;
        }

        if (distToPlayer <= meleeRange && meleeTimer <= 0)
        {
            StartCoroutine(ExecuteMeleeAttack());
            return;
        }

        if (distToPlayer <= magicRange && magicTimer <= 0)
        {
            StartCoroutine(ExecuteMagicSequence());
            return;
        }

        MoveTowardsPlayer();
    }

    void MoveTowardsPlayer()
    {
        Vector2 dir = (player.position - transform.position).normalized;
        
        if (animator != null)
        {
            float absX = Mathf.Abs(originalScale.x);
            if (dir.x > 0) 
                animator.transform.localScale = new Vector3(absX, originalScale.y, originalScale.z); 
            else if (dir.x < 0) 
                animator.transform.localScale = new Vector3(-absX, originalScale.y, originalScale.z);
        }

        float dist = Vector2.Distance(transform.position, player.position);
        
        if (dist > stopDistance)
        {
            rb.linearVelocity = dir * moveSpeed;
            SetAnimation("Walk");
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            SetAnimation("Idle");
        }
    }

    IEnumerator ExecuteMeleeAttack()
    {
        isBusy = true; 
        rb.linearVelocity = Vector2.zero; 
        currentState = State.MeleeAttacking;

        if(animator) 
        {
            animator.ResetTrigger("Attack"); 
            animator.SetTrigger("Attack");
        }
        
        PlayRandomSound(meleeSounds);

        yield return new WaitForSeconds(meleeWindUpTime);

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist <= meleeRange * 1.5f) 
        {
             Vector2 directionToPlayer = (player.position - transform.position).normalized;
             var playerLimb = player.GetComponent<PlayerLimbController>();
             if(playerLimb) playerLimb.TakeDamage(meleeDamage, directionToPlayer);

             Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
             if (playerRb != null)
                 playerRb.AddForce(directionToPlayer * meleeKnockbackForce, ForceMode2D.Impulse);
        }

        yield return new WaitForSeconds(0.5f);
        meleeTimer = meleeCooldown;
        isBusy = false; 
        currentState = State.Chasing;
    }

    IEnumerator ExecuteMagicSequence()
    {
        isBusy = true; 
        rb.linearVelocity = Vector2.zero;
        currentState = State.CastingMagic;

        if(animator) animator.SetTrigger("Magic"); 
        
        PlayRandomSound(magicCastSounds);

        int magicType = Random.Range(1, 4); 

        if (magicType == 3 && lastMagicType == 3)
        {
            magicType = Random.Range(1, 3);
        }

        lastMagicType = magicType;

        yield return new WaitForSeconds(0.5f);

        switch (magicType)
        {
            case 1:
                yield return StartCoroutine(Magic_Icicles());
                break;
            case 2:
                yield return StartCoroutine(Magic_Traps());
                break;
            case 3:
                yield return StartCoroutine(Magic_IceWall());
                break;
        }

        yield return new WaitForSeconds(castTime);

        magicTimer = magicCooldown;
        isBusy = false;
        currentState = State.Chasing;
    }

    IEnumerator Magic_Icicles()
    {
        for (int i = 0; i < icicleCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * icicleRadius;
            Vector2 groundPos = (Vector2)player.position + randomOffset;

            if (icicleWarningPrefab)
            {
                Instantiate(icicleWarningPrefab, groundPos, Quaternion.identity);
            }

            StartCoroutine(SpawnIcicleWithDelay(groundPos, icicleWarningDuration));
            yield return new WaitForSeconds(0.2f); 
        }
    }

    IEnumerator SpawnIcicleWithDelay(Vector2 groundPos, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (iciclePrefab)
        {
            Vector2 spawnPos = groundPos + (Vector2.up * icicleSpawnHeight);
            GameObject icicle = Instantiate(iciclePrefab, spawnPos, Quaternion.identity);
            
            var script = icicle.GetComponent<FallingIcicle>();
            if (script) script.Setup(groundPos.y);
            
            PlayRandomSound(magicImpactSounds);
        }
    }

    IEnumerator Magic_Traps()
    {
        Vector2[] trapPositions = new Vector2[trapCount];

        for (int i = 0; i < trapCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * 4f; 
            trapPositions[i] = (Vector2)player.position + randomOffset;

            if (warningVisualPrefab)
            {
                GameObject warning = Instantiate(warningVisualPrefab, trapPositions[i], Quaternion.identity);
                Destroy(warning, trapDelay); 
            }
        }

        PlayRandomSound(trapSetSounds);

        yield return new WaitForSeconds(trapDelay);

        for (int i = 0; i < trapCount; i++)
        {
            if (trapPrefab)
                Instantiate(trapPrefab, trapPositions[i], Quaternion.identity);
        }
        
        PlayRandomSound(magicImpactSounds);
    }

    IEnumerator Magic_IceWall()
    {
        if (iceWallPrefab)
        {
            float direction = animator != null ? Mathf.Sign(animator.transform.localScale.x) : 1f;
            Vector2 spawnPos = (Vector2)transform.position + (Vector2.right * direction * wallOffsetDistance);

            GameObject wall = Instantiate(iceWallPrefab, spawnPos, Quaternion.identity);
            Destroy(wall, wallLifeTime);
            PlayRandomSound(magicImpactSounds);
        }
        yield return null;
    }

    void SetAnimation(string stateName)
    {
        if (animator == null) return;
        if (stateName == "Walk") animator.SetBool("IsWalking", true);
        else animator.SetBool("IsWalking", false);
    }

    void PlayRandomSound(AudioClip[] clips)
    {
        if (clips != null && clips.Length > 0 && audioSource)
        {
            int randomIndex = Random.Range(0, clips.Length);
            AudioClip clip = clips[randomIndex];
            if (clip != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(clip);
            }
        }
    }
}