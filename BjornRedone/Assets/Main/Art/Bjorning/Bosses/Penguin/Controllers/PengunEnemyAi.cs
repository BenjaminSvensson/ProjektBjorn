using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyLimbController))]
[RequireComponent(typeof(AudioSource))]
public class PenguinEnemyAI : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("Leave empty to auto-find PlayerMovement script.")]
    public Transform player; 

    [Header("Audio Settings")]
    public AudioClip[] attackSounds;
    public AudioClip[] magicCastSounds; // Sound when casting starts
    public AudioClip[] magicImpactSounds; // Sound when spell hits/spawns
    public AudioClip[] generalSounds; 
    public AudioClip[] footstepSounds;
    
    [Header("Screen Shake Settings")]
    public float footstepShakeAmount = 0.05f;
    public float attackShakeAmount = 0.2f;

    [Header("Setup")]
    public string weaponTag = "Weapon";
    public Animator animator;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float stopDistance = 0.5f;
    public float wakeUpDistance = 15f;

    [Header("Combat Stats")]
    public float meleeRange = 1.5f;
    public float meleeCooldown = 2.0f;
    public float magicRange = 8f;
    public float magicCooldown = 5.0f;

    [Header("Magic 1: Icicles (50% Chance)")]
    public GameObject iciclePrefab;
    public GameObject icicleWarningPrefab; // Red circle on ground
    public int icicleCount = 3;
    public float icicleRadius = 3f; 
    public float icicleSpawnHeight = 6f; 

    [Header("Magic 2: Ice Traps (30% Chance)")]
    public GameObject trapPrefab;
    public GameObject trapWarningPrefab;
    public int trapCount = 2;
    public float trapDelay = 1.0f; 

    [Header("Magic 3: Ice Wall (20% Chance)")]
    public GameObject iceWallPrefab;
    public float wallOffsetDistance = 2.0f;
    public float wallLifeTime = 5.0f;

    // Internal
    private Rigidbody2D rb;
    private EnemyLimbController limbController;
    private AudioSource audioSource;
    private float meleeTimer;
    private float magicTimer;
    private bool isBusy = false;
    private float generalSoundTimer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        limbController = GetComponent<EnemyLimbController>();
        audioSource = GetComponent<AudioSource>();

        generalSoundTimer = Random.Range(2f, 5f);

        // --- AUTO-TARGETING ---
        if (player == null)
        {
            var playerScript = FindObjectOfType<PlayerMovement>(); 
            if (playerScript != null) player = playerScript.transform;
            else {
                GameObject pTag = GameObject.FindGameObjectWithTag("Player");
                if (pTag != null) player = pTag.transform;
            }
        }
        
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0; 
        rb.freezeRotation = true;
    }

    void Update()
    {
        if (player == null || limbController == null) return;

        // --- GENERAL SOUND LOGIC ---
        if (!isBusy && Vector2.Distance(transform.position, player.position) < 20f)
        {
            generalSoundTimer -= Time.deltaTime;
            if (generalSoundTimer <= 0)
            {
                PlayRandomSound(generalSounds, 0.4f);
                generalSoundTimer = Random.Range(3f, 7f);
            }
        }

        float dist = Vector2.Distance(transform.position, player.position);

        if (meleeTimer > 0) meleeTimer -= Time.deltaTime;
        if (magicTimer > 0) magicTimer -= Time.deltaTime;

        if (isBusy) {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // --- AI LOGIC TREE ---
        if (dist > wakeUpDistance) {
            SetWalking(false);
        }
        else if (dist <= meleeRange && meleeTimer <= 0) {
            StartCoroutine(DoMeleeAttack());
        }
        else if (dist <= magicRange && magicTimer <= 0) {
            StartCoroutine(DoMagicAttack());
        }
        else {
            MoveToPlayer();
        }
    }

    void MoveToPlayer()
    {
        float speed = moveSpeed + limbController.moveSpeedBonus;
        if (speed < 0) speed = 0.5f; 

        Vector2 dir = (player.position - transform.position).normalized;
        rb.linearVelocity = dir * speed;

        if (dir.x > 0) transform.localScale = new Vector3(1, 1, 1);
        else transform.localScale = new Vector3(-1, 1, 1);

        SetWalking(true);
    }

    void SetWalking(bool isWalking)
    {
        if (animator) animator.SetBool("IsWalking", isWalking);
    }

    // Called by Bridge Script
    public void PlayFootstepSound()
    {
        if (animator != null && animator.GetBool("IsWalking"))
        {
            PlayRandomSound(footstepSounds, 0.5f);
            StartCoroutine(ShakeCamera(footstepShakeAmount, 0.1f));
        }
    }

    // --- COMBAT COROUTINES ---

    IEnumerator DoMeleeAttack()
    {
        isBusy = true;
        rb.linearVelocity = Vector2.zero;
        SetWalking(false);
        if(animator) animator.SetTrigger("Attack");

        PlayRandomSound(attackSounds, 1.0f);
        
        yield return new WaitForSeconds(0.4f); 
        
        if (player != null && Vector2.Distance(transform.position, player.position) <= meleeRange * 1.5f)
        {
            StartCoroutine(ShakeCamera(attackShakeAmount, 0.2f));
            var pLimb = player.GetComponent<PlayerLimbController>();
            if (pLimb) pLimb.TakeDamage(10f + limbController.attackDamageBonus);
        }

        meleeTimer = meleeCooldown;
        isBusy = false;
    }

    IEnumerator DoMagicAttack()
    {
        isBusy = true;
        rb.linearVelocity = Vector2.zero;
        SetWalking(false);
        if(animator) animator.SetTrigger("Magic");
        
        PlayRandomSound(magicCastSounds, 1.0f);

        yield return new WaitForSeconds(0.5f); // Casting time
        
        // --- CHOOSE SPELL (Weighted Random) ---
        int roll = Random.Range(0, 100);
        
        if (roll < 50) // 0-49 (50% Chance)
        {
            yield return StartCoroutine(Magic_Icicles());
        }
        else if (roll < 80) // 50-79 (30% Chance)
        {
            yield return StartCoroutine(Magic_Traps());
        }
        else // 80-99 (20% Chance)
        {
            yield return StartCoroutine(Magic_IceWall());
        }

        magicTimer = magicCooldown;
        isBusy = false;
    }

    // --- SPELL LOGIC ---

    IEnumerator Magic_Icicles()
    {
        for (int i = 0; i < icicleCount; i++)
        {
            if (player == null) break;

            // Pick a spot near the player
            Vector2 randomOffset = Random.insideUnitCircle * icicleRadius;
            Vector2 groundPos = (Vector2)player.position + randomOffset;

            // Spawn Warning Circle
            if (icicleWarningPrefab) 
            {
                GameObject warn = Instantiate(icicleWarningPrefab, groundPos, Quaternion.identity);
                Destroy(warn, 1.0f); // Destroy warning after 1 sec
            }

            // Schedule the Icicle to drop after slight delay
            StartCoroutine(SpawnIcicleDelayed(groundPos, 0.8f));
            
            yield return new WaitForSeconds(0.2f); // Gap between icicles
        }
    }

    IEnumerator SpawnIcicleDelayed(Vector2 targetPos, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (iciclePrefab)
        {
            // Spawn high up in the air
            Vector2 spawnPos = targetPos + (Vector2.up * icicleSpawnHeight);
            GameObject icicle = Instantiate(iciclePrefab, spawnPos, Quaternion.identity);
            
            // Tell the icicle where the "ground" is so it destroys itself on impact
            FallingIcicle script = icicle.GetComponent<FallingIcicle>();
            if (script) script.targetY = targetPos.y;

            PlayRandomSound(magicImpactSounds, 0.8f);
        }
    }

    IEnumerator Magic_Traps()
    {
        Vector2[] trapPositions = new Vector2[trapCount];

        // 1. Show Warnings
        for (int i = 0; i < trapCount; i++)
        {
            if (player == null) break;
            Vector2 randomOffset = Random.insideUnitCircle * 4f; 
            trapPositions[i] = (Vector2)player.position + randomOffset;
            
            if (trapWarningPrefab) 
            {
                GameObject warn = Instantiate(trapWarningPrefab, trapPositions[i], Quaternion.identity);
                Destroy(warn, trapDelay);
            }
        }

        // 2. Wait for arming time
        yield return new WaitForSeconds(trapDelay);
        
        PlayRandomSound(magicImpactSounds, 0.8f);

        // 3. Spawn Traps
        for (int i = 0; i < trapCount; i++)
        {
            if (trapPrefab) Instantiate(trapPrefab, trapPositions[i], Quaternion.identity);
        }
    }

    IEnumerator Magic_IceWall()
    {
        if (iceWallPrefab)
        {
            float direction = transform.localScale.x; 
            Vector2 spawnPos = (Vector2)transform.position + (Vector2.right * direction * wallOffsetDistance);
            
            GameObject wall = Instantiate(iceWallPrefab, spawnPos, Quaternion.identity);
            
            IceWall wallScript = wall.GetComponent<IceWall>();
            if (wallScript != null) wallScript.Activate(wallLifeTime);
            else Destroy(wall, wallLifeTime);
            
            PlayRandomSound(magicImpactSounds, 1.0f);
        }
        yield return null;
    }

    // --- HELPERS ---

    void PlayRandomSound(AudioClip[] clips, float volume)
    {
        if (clips != null && clips.Length > 0 && audioSource)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f); 
            audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
        }
    }

    IEnumerator ShakeCamera(float intensity, float duration)
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 originalPos = mainCam.transform.position;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                mainCam.transform.position = originalPos + (Vector3)Random.insideUnitCircle * intensity;
                elapsed += Time.deltaTime;
                yield return null;
            }
            mainCam.transform.position = originalPos;
        }
    }
}