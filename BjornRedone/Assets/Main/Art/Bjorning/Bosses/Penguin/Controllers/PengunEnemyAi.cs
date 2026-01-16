using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyLimbController))] // Forces the Limb Controller to exist
public class PenguinEnemyAI : MonoBehaviour
{
    private enum State { Idle, Chasing, CastingMagic, MeleeAttacking, Dead }

    [Header("Components")]
    public Animator animator; 
    
    [Header("Damage Detection")]
    public string weaponTag = "Weapon"; // Must match your Project Settings > Tags
    public float damageFromWeapon = 10f; // How much damage to pass to the Limb Controller

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

    [Header("Magic 1: Icicles")]
    public GameObject iciclePrefab;
    public GameObject icicleWarningPrefab;
    public int icicleCount = 3;
    public float icicleRadius = 3f; 
    public float icicleSpawnHeight = 10f; 
    public float icicleWarningDuration = 1.0f; 

    [Header("Magic 2: Traps")]
    public GameObject warningVisualPrefab; 
    public GameObject trapPrefab;
    public int trapCount = 2;
    public float trapDelay = 1.0f; 

    [Header("Magic 3: Ice Wall")]
    public GameObject iceWallPrefab;
    public float wallOffsetDistance = 2.0f; 
    public float wallLifeTime = 5.0f;

    [Header("Audio (Attacks Only)")]
    public AudioClip[] meleeSounds;
    public AudioClip[] magicCastSounds;
    public AudioClip[] magicImpactSounds; 
    public AudioClip[] trapSetSounds;
    // Note: Hurt/Death sounds are now handled by EnemyLimbController!

    // Internal
    private Transform player;
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private EnemyLimbController limbController; // REF TO LIMB CONTROLLER
    private State currentState = State.Idle;
    
    private Vector3 originalScale; 
    private float meleeTimer;
    private float magicTimer;
    private int lastMagicType = 0; 
    private bool isBusy = false; 

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0; 
        audioSource = GetComponent<AudioSource>();
        limbController = GetComponent<EnemyLimbController>(); // Get the controller

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
            originalScale = animator.transform.localScale;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
    }

    // --- COLLISION LOGIC ---

    void OnTriggerEnter2D(Collider2D other)
    {
        // Safety check: if LimbController says we are dead, stop everything
        if (limbController == null) return;

        // 1. DAMAGE THE PLAYER (Contact Damage)
        if (other.CompareTag("Player"))
        {
            var playerLimb = other.GetComponent<PlayerLimbController>();
            if(playerLimb) 
            {
                Vector2 dir = (other.transform.position - transform.position).normalized;
                playerLimb.TakeDamage(10f, dir); 
            }
        }

        // 2. DETECT WEAPON HIT
        if (other.CompareTag(weaponTag)) 
        {
            // Calculate direction for blood splatter
            Vector2 hitDirection = (transform.position - other.transform.position).normalized;
            
            // Tell the LimbController to take damage
            // It will handle Health, Gore, Limbs falling off, and Death automatically
            limbController.TakeDamage(damageFromWeapon, hitDirection);
        }
    }

    // --- AI LOOP ---

    void Update()
    {
        // If limbController is missing (object destroyed) or player is gone, stop.
        if (limbController == null || player == null) return;

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
        // Check LimbController for movement speed bonuses (e.g., if legs are missing)
        float currentSpeed = moveSpeed + limbController.moveSpeedBonus;
        // Ensure speed doesn't go negative if legs are gone
        currentSpeed = Mathf.Max(0.5f, currentSpeed); 

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
            rb.linearVelocity = dir * currentSpeed;
            SetAnimation("Walk");
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            SetAnimation("Idle");
        }
    }

    // --- ACTIONS ---

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
             
             // Optional: Add LimbController damage bonus (if strong arms are attached)
             float totalDamage = meleeDamage + limbController.attackDamageBonus;

             var playerLimb = player.GetComponent<PlayerLimbController>();
             if(playerLimb) playerLimb.TakeDamage(totalDamage, directionToPlayer);

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
        if (magicType == 3 && lastMagicType == 3) magicType = Random.Range(1, 3);
        lastMagicType = magicType;

        yield return new WaitForSeconds(0.5f);

        switch (magicType)
        {
            case 1: yield return StartCoroutine(Magic_Icicles()); break;
            case 2: yield return StartCoroutine(Magic_Traps()); break;
            case 3: yield return StartCoroutine(Magic_IceWall()); break;
        }

        yield return new WaitForSeconds(castTime);

        magicTimer = magicCooldown;
        isBusy = false;
        currentState = State.Chasing;
    }

    // --- SPELL HELPERS ---

    IEnumerator Magic_Icicles()
    {
        for (int i = 0; i < icicleCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * icicleRadius;
            Vector2 groundPos = (Vector2)player.position + randomOffset;

            if (icicleWarningPrefab) Instantiate(icicleWarningPrefab, groundPos, Quaternion.identity);
            
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
            if (warningVisualPrefab) Destroy(Instantiate(warningVisualPrefab, trapPositions[i], Quaternion.identity), trapDelay);
        }

        PlayRandomSound(trapSetSounds);
        yield return new WaitForSeconds(trapDelay);

        for (int i = 0; i < trapCount; i++)
        {
            if (trapPrefab) Instantiate(trapPrefab, trapPositions[i], Quaternion.identity);
        }
        PlayRandomSound(magicImpactSounds);
    }

    IEnumerator Magic_IceWall()
    {
        if (iceWallPrefab)
        {
            float direction = animator != null ? Mathf.Sign(animator.transform.localScale.x) : 1f;
            Vector2 spawnPos = (Vector2)transform.position + (Vector2.right * direction * wallOffsetDistance);
            Destroy(Instantiate(iceWallPrefab, spawnPos, Quaternion.identity), wallLifeTime);
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