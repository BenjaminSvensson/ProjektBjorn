using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(EnemyLimbController))]
[RequireComponent(typeof(AudioSource))]
public class BirdEnemyAI : MonoBehaviour
{
    private enum BirdState { Grounded, TakingOff, Chasing, Retreating, OffScreenAttack, Diving, Stuck, Dead }

    [Header("Components")]
    public Transform spriteHolder;
    public Transform shadowSprite;
    public Transform eggSpawnPoint; 
    [SerializeField] private Animator animator;

    [Header("Audio Profile")]
    [Tooltip("Sound played repeatedly while flying")]
    public AudioClip flapSound;
    [Tooltip("Time in seconds between flap sounds")]
    public float flapInterval = 0.4f;
    [Tooltip("Sound played when starting a dive attack")]
    public AudioClip diveSound;
    [Tooltip("Sound played when dropping an egg")]
    public AudioClip eggDropSound;
    [Tooltip("Sound played when hit")]
    public AudioClip hitSound;
    [Tooltip("Sound played when dying")]
    public AudioClip deathSound;
    
    [Header("Health & Feedback")]
    public float maxHealth = 30f;
    public float contactDamage = 5f;
    public Color damageFlashColor = Color.red;
    
    private float currentHealth;
    private AudioSource audioSource;
    private SpriteRenderer[] childRenderers; 
    private Coroutine flashCoroutine;
    private float flapTimer; // Internal timer for flapping sound

    [Header("Optimization")]
    public float maxActivityDistance = 30.0f;

    [Header("Flying Settings")]
    public float flyHeight = 4.0f;
    public float offScreenHeight = 15.0f; 
    public float flySpeed = 5.0f; 
    public float takeoffSpeed = 2.0f;
    public float retreatSpeed = 8.0f; 
    public float trackingSpeed = 2.0f;

    [Header("Visual Perspective")]
    public float groundScale = 1.0f;
    public float highAirScale = 0.6f;

    [Header("Timing")]
    public float timeOnScreen = 5.0f;
    public float offScreenDuration = 5.0f;

    [Header("Shadow Settings")]
    public Vector3 shadowScaleGround = new Vector3(1f, 0.5f, 1f);
    public Vector3 shadowScaleAir = new Vector3(0.5f, 0.25f, 1f);
    public float shadowAlphaGround = 0.8f;
    public float shadowAlphaAir = 0.3f;

    [Header("Combat - Egg Bombing")]
    public GameObject eggPrefab;
    public float timeBetweenEggs = 0.8f;

    [Header("Combat - Dive Attack")]
    public float diveTellDuration = 1.0f; 
    public float diveSpeed = 20.0f; 
    public float stuckDuration = 2.0f;
    public float damageRadius = 1.5f;
    public float impactDamage = 10f;

    // Internal State
    private BirdState currentState = BirdState.Grounded;
    private Rigidbody2D rb;
    private Transform player;
    private SpriteRenderer shadowRenderer;
    private Camera mainCam;

    private float currentHeight = 0f;
    private float stateTimer;
    private float eggTimer;
    private Vector2 diveTargetPos;
    
    // Shadow Logic
    private Vector2 shadowTargetPos;
    private bool lockShadowToTarget = false;
    private Vector2 retreatDirection;

    void Start()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        
        if(animator == null && spriteHolder) animator = spriteHolder.GetComponent<Animator>();
        if (shadowSprite) shadowRenderer = shadowSprite.GetComponent<SpriteRenderer>();
        if (eggSpawnPoint == null) eggSpawnPoint = transform;

        if (spriteHolder)
        {
            childRenderers = spriteHolder.GetComponentsInChildren<SpriteRenderer>();
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;

        mainCam = Camera.main;
        if(spriteHolder) spriteHolder.localScale = new Vector3(groundScale, groundScale, 1);

        SwitchState(BirdState.Grounded);
    }

    void Update()
    {
        if (player == null || currentState == BirdState.Dead) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > maxActivityDistance && currentState != BirdState.OffScreenAttack)
        {
            rb.linearVelocity = Vector2.zero; 
            return; 
        }

        UpdateVisualsAndShadow();
        HandleAmbientAudio(); // Check if we should play flap sounds

        switch (currentState)
        {
            case BirdState.Grounded:
                if (dist < 12f) SwitchState(BirdState.TakingOff);
                break;
            case BirdState.TakingOff:
                currentHeight = Mathf.MoveTowards(currentHeight, flyHeight, takeoffSpeed * Time.deltaTime);
                if (Mathf.Abs(currentHeight - flyHeight) < 0.1f) SwitchState(BirdState.Chasing);
                break;
            case BirdState.Chasing:
                HandleChasing();
                break;
            case BirdState.Retreating:
                HandleRetreating();
                break;
            case BirdState.OffScreenAttack:
                HandleOffScreenAttack();
                break;
            case BirdState.Diving:
                break;
            case BirdState.Stuck:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0) SwitchState(BirdState.TakingOff);
                break;
        }
    }

    // --- AUDIO LOGIC ---

    void HandleAmbientAudio()
    {
        // Only flap if we are in a flying state
        bool isFlying = (currentState == BirdState.Chasing || 
                         currentState == BirdState.Retreating || 
                         currentState == BirdState.OffScreenAttack || 
                         currentState == BirdState.TakingOff);

        if (isFlying)
        {
            flapTimer -= Time.deltaTime;
            if (flapTimer <= 0)
            {
                PlaySound(flapSound, 0.4f, 0.9f, 1.1f); // Lower volume for wings, variable pitch
                flapTimer = flapInterval;
            }
        }
    }

    void PlaySound(AudioClip clip, float volume = 1.0f, float pitchMin = 0.9f, float pitchMax = 1.1f)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(pitchMin, pitchMax);
            audioSource.PlayOneShot(clip, volume);
        }
    }

    // --- DAMAGE LOGIC ---

    public void TakeDamage(float damageAmount)
    {
        if (currentState == BirdState.Dead) return;

        currentHealth -= damageAmount;
        
        PlaySound(hitSound, 1f); // Play Hit Sound

        if (animator) animator.SetTrigger("Hit");

        if (gameObject.activeInHierarchy)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashDamageEffect());
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator FlashDamageEffect()
    {
        if (childRenderers != null)
        {
            foreach (var sr in childRenderers) if (sr) sr.color = damageFlashColor;
        }

        yield return new WaitForSeconds(0.15f);

        if (childRenderers != null)
        {
            foreach (var sr in childRenderers) if (sr) sr.color = Color.white;
        }
    }

    void Die()
    {
        PlaySound(deathSound, 1f); // Play Death Sound

        currentState = BirdState.Dead;
        rb.linearVelocity = Vector2.zero;
        StopAllCoroutines();

        if (childRenderers != null)
        {
            foreach (var sr in childRenderers) if(sr) sr.color = Color.white;
        }

        if (animator) animator.Play("Die");
        
        if (shadowSprite) shadowSprite.gameObject.SetActive(false);
        GetComponent<Collider2D>().enabled = false;

        Destroy(gameObject, 2.0f);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (currentState == BirdState.Dead) return;

        if (collision.CompareTag("Player"))
        {
            PlayerLimbController playerController = collision.GetComponent<PlayerLimbController>();
            if (playerController != null)
            {
                Vector2 dir = (collision.transform.position - transform.position).normalized;
                playerController.TakeDamage(contactDamage, dir);
            }
        }
    }

    // --- STATE LOGIC ---

    void SwitchState(BirdState newState)
    {
        if (currentState == BirdState.Dead) return;

        currentState = newState;
        stateTimer = 0;

        switch (currentState)
        {
            case BirdState.TakingOff:
                animator.Play("FlyUpp");
                break;
            case BirdState.Chasing:
                animator.Play("FlyLoop");
                stateTimer = timeOnScreen; 
                break;
            case BirdState.Retreating:
                animator.Play("FlyLoop");
                retreatDirection = Vector2.up; 
                break;
            case BirdState.OffScreenAttack:
                animator.Play("FlyLoop");
                stateTimer = offScreenDuration; 
                eggTimer = 0.5f; 
                break;
            case BirdState.Diving:
                animator.Play("Attack");
                StartCoroutine(ExecuteDiveSequence());
                break;
            case BirdState.Stuck:
                rb.linearVelocity = Vector2.zero; 
                stateTimer = stuckDuration;
                animator.Play("Stuck"); 
                break;
        }
    }

    void HandleChasing()
    {
        Vector2 targetPos = player.position;
        Vector2 dir = (targetPos - rb.position).normalized;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, dir * trackingSpeed, Time.deltaTime * 2f);
        FaceDirection(dir.x);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) SwitchState(BirdState.Retreating);
    }

    void HandleRetreating()
    {
        rb.linearVelocity = retreatDirection * retreatSpeed;
        currentHeight = Mathf.MoveTowards(currentHeight, offScreenHeight, Time.deltaTime * 5f);

        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);
        if (viewportPos.y > 1.2f) SwitchState(BirdState.OffScreenAttack);
    }

    void HandleOffScreenAttack()
    {
        Vector2 targetPos = new Vector2(player.position.x, rb.position.y);
        Vector2 newPos = Vector2.MoveTowards(rb.position, targetPos, flySpeed * 2f * Time.deltaTime);
        rb.MovePosition(newPos);

        FaceDirection(player.position.x - rb.position.x);

        eggTimer -= Time.deltaTime;
        if (eggTimer <= 0)
        {
            DropEgg();
            eggTimer = timeBetweenEggs;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) SwitchState(BirdState.Diving);
    }

    IEnumerator ExecuteDiveSequence()
    {
        rb.linearVelocity = Vector2.zero;
        diveTargetPos = player.position;
        Vector2 startPos = rb.position; 
        float startH = currentHeight;

        lockShadowToTarget = true;
        shadowTargetPos = diveTargetPos;

        // Play Attack Sound
        PlaySound(diveSound, 1f, 1f, 1.2f); 

        yield return new WaitForSeconds(diveTellDuration);

        float t = 0;
        float dist = Vector2.Distance(startPos, diveTargetPos);
        float duration = Mathf.Max(dist / diveSpeed, 0.2f); 

        while (t < 1.0f)
        {
            t += Time.deltaTime / duration;
            Vector2 nextPos = Vector2.Lerp(startPos, diveTargetPos, t);
            rb.MovePosition(nextPos);
            currentHeight = Mathf.Lerp(startH, 0f, t * t);
            FaceDirection(diveTargetPos.x - startPos.x);
            yield return null;
        }

        rb.position = diveTargetPos; 
        currentHeight = 0f;
        lockShadowToTarget = false;
        
        CheckImpactDamage();

        SwitchState(BirdState.Stuck);
    }

    void DropEgg()
    {
        if (eggPrefab)
        {
            PlaySound(eggDropSound, 0.7f); // Play Egg Sound

            Vector2 targetLandPos = player.position;
            Vector2 releasePos = eggSpawnPoint.position;
            GameObject egg = Instantiate(eggPrefab, targetLandPos, Quaternion.identity);
            BirdEgg eggScript = egg.GetComponent<BirdEgg>();
            if (eggScript != null) eggScript.Initialize(targetLandPos, releasePos);
        }
    }

    // --- VISUAL & HELPER METHODS ---

    void FaceDirection(float xDir)
    {
        float direction = xDir > 0.1f ? 1f : (xDir < -0.1f ? -1f : Mathf.Sign(spriteHolder.localScale.x));
        float scale = GetCurrentPerspectiveScale();
        spriteHolder.localScale = new Vector3(direction * scale, scale, 1f);
    }

    float GetCurrentPerspectiveScale()
    {
        float ratio = Mathf.Clamp01(currentHeight / offScreenHeight);
        return Mathf.Lerp(groundScale, highAirScale, ratio);
    }

    void UpdateVisualsAndShadow()
    {
        if (spriteHolder)
        {
            spriteHolder.localPosition = new Vector3(0, currentHeight, 0);
            float dir = Mathf.Sign(spriteHolder.localScale.x);
            float scale = GetCurrentPerspectiveScale();
            spriteHolder.localScale = new Vector3(dir * scale, scale, 1);
        }

        if (shadowSprite && shadowRenderer)
        {
            if (lockShadowToTarget)
            {
                shadowSprite.position = shadowTargetPos;
                shadowSprite.localScale = shadowScaleGround * 1.5f; 
                Color c = shadowRenderer.color;
                c.a = 0.5f; 
                shadowRenderer.color = c;
            }
            else
            {
                shadowSprite.position = transform.position; 
                float ratio = Mathf.Clamp01(currentHeight / flyHeight);
                float alpha = Mathf.Lerp(shadowAlphaGround, shadowAlphaAir, ratio);
                if (currentHeight > flyHeight * 2.0f) alpha = 0f;
                shadowSprite.localScale = Vector3.Lerp(shadowScaleGround, shadowScaleAir, ratio);
                Color c = shadowRenderer.color;
                c.a = alpha;
                shadowRenderer.color = c;
            }
        }
    }

    void CheckImpactDamage()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, damageRadius);
        foreach(var hit in hits)
        {
            PlayerLimbController playerController = hit.GetComponent<PlayerLimbController>();
            if(playerController != null)
            {
                Vector2 dir = (hit.transform.position - transform.position).normalized;
                playerController.TakeDamage(impactDamage, dir);
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if(lockShadowToTarget) Gizmos.DrawWireSphere(diveTargetPos, damageRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxActivityDistance);
        if(eggSpawnPoint) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(eggSpawnPoint.position, 0.2f); }
    }
}