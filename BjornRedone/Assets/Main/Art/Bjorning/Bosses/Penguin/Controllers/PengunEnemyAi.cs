using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PenguinEnemyAI : MonoBehaviour
{
    private enum State { Idle, Chasing, CastingMagic, MeleeAttacking, Dead }

    [Header("Components")]
    public Animator animator; 
    
    [Header("Core Settings")]
    public float moveSpeed = 3f;
    public float stopDistance = 0.5f; 
    public float wakeUpDistance = 15f; 

    [Header("Melee Settings")]
    public float meleeRange = 1.5f;
    public float meleeDamage = 10f;
    public float meleeCooldown = 2.0f;
    public float meleeWindUpTime = 0.3f; 

    [Header("Magic General")]
    public float magicRange = 10f;
    public float magicCooldown = 5.0f;
    public float castTime = 1.0f; 

    [Header("Magic 1: Icicles (Sky Drop)")]
    public GameObject iciclePrefab;
    public int icicleCount = 3;
    public float icicleRadius = 3f; 

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
    public AudioClip meleeSound;
    public AudioClip magicCastSound;
    public AudioClip magicImpactSound;
    public AudioClip trapSetSound;

    // Internal
    private Transform player;
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private State currentState = State.Idle;
    
    // NEW: Variable to remember how big you set him in the inspector
    private Vector3 originalScale; 

    private float meleeTimer;
    private float magicTimer;
    private bool isBusy = false; 

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0; 
        audioSource = GetComponent<AudioSource>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null) Debug.LogError("PENGUIN ERROR: No Animator assigned!");
        }

        // FIX: Remember the exact size you set in the Inspector
        if (animator != null)
        {
            originalScale = animator.transform.localScale;
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
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
        
        // FIX: Apply direction to the ORIGINAL scale, not to "1"
        if (animator != null)
        {
            // Calculate positive scale based on your original settings
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

        if(animator) animator.SetTrigger("Attack"); 
        PlaySound(meleeSound);

        yield return new WaitForSeconds(meleeWindUpTime);

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist <= meleeRange * 1.2f) 
        {
             var playerLimb = player.GetComponent<PlayerLimbController>();
             if(playerLimb) playerLimb.TakeDamage(meleeDamage, (player.position - transform.position).normalized);
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
        PlaySound(magicCastSound);

        int magicType = Random.Range(1, 4); 

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
            if (iciclePrefab)
            {
                Vector2 randomOffset = Random.insideUnitCircle * icicleRadius;
                Vector2 spawnPos = (Vector2)player.position + randomOffset;
                Instantiate(iciclePrefab, spawnPos, Quaternion.identity);
            }
            yield return new WaitForSeconds(0.1f); 
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

        PlaySound(trapSetSound);

        yield return new WaitForSeconds(trapDelay);

        for (int i = 0; i < trapCount; i++)
        {
            if (trapPrefab)
            {
                Instantiate(trapPrefab, trapPositions[i], Quaternion.identity);
                PlaySound(magicImpactSound);
            }
        }
    }

    IEnumerator Magic_IceWall()
    {
        if (iceWallPrefab)
        {
            float direction = animator != null ? Mathf.Sign(animator.transform.localScale.x) : 1f;
            
            Vector2 spawnPos = (Vector2)transform.position + (Vector2.right * direction * wallOffsetDistance);

            GameObject wall = Instantiate(iceWallPrefab, spawnPos, Quaternion.identity);
            Destroy(wall, wallLifeTime);
            PlaySound(magicImpactSound);
        }
        yield return null;
    }

    void SetAnimation(string stateName)
    {
        if (animator == null) return;

        if (stateName == "Walk") animator.SetBool("IsWalking", true);
        else animator.SetBool("IsWalking", false);
    }

    void PlaySound(AudioClip clip)
    {
        if (clip && audioSource) audioSource.PlayOneShot(clip);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, magicRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, wakeUpDistance);
    }
}