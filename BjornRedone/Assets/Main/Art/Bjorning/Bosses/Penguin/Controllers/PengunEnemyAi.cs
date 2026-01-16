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
    public AudioClip[] magicSounds;
    public AudioClip[] generalSounds; // Ambient squeaks/grunts
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

    [Header("Magic Prefabs")]
    public GameObject iceWallPrefab;
    public float wallOffsetDistance = 2.0f;
    public float wallLifeTime = 5.0f;
    // Add other magic prefabs here (icicles, traps) if needed

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

        // Randomize ambient sound timer
        generalSoundTimer = Random.Range(2f, 5f);

        // --- AUTO-TARGETING ---
        // 1. If slot is empty, look for PlayerMovement script
        if (player == null)
        {
            var playerScript = FindObjectOfType<PlayerMovement>(); 
            if (playerScript != null) player = playerScript.transform;
            else {
                // Fallback to Tag
                GameObject pTag = GameObject.FindGameObjectWithTag("Player");
                if (pTag != null) player = pTag.transform;
            }
        }
        
        // Safety checks
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

        // Face Direction
        if (dir.x > 0) transform.localScale = new Vector3(1, 1, 1);
        else transform.localScale = new Vector3(-1, 1, 1);

        SetWalking(true);
    }

    void SetWalking(bool isWalking)
    {
        if (animator) animator.SetBool("IsWalking", isWalking);
    }

    // --- PUBLIC FUNCTIONS FOR ANIMATION EVENTS ---
    // The "Bridge" script will call these.
    public void PlayFootstepSound()
    {
        // Only play if actually moving
        if (animator != null && animator.GetBool("IsWalking"))
        {
            PlayRandomSound(footstepSounds, 0.5f);
            StartCoroutine(ShakeCamera(footstepShakeAmount, 0.1f));
        }
    }

    // --- ATTACKS ---

    IEnumerator DoMeleeAttack()
    {
        isBusy = true;
        rb.linearVelocity = Vector2.zero;
        SetWalking(false);
        if(animator) animator.SetTrigger("Attack");

        PlayRandomSound(attackSounds, 1.0f);
        
        yield return new WaitForSeconds(0.4f); // Wait for hit frame
        
        // HIT CHECK
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
        
        PlayRandomSound(magicSounds, 1.0f);

        yield return new WaitForSeconds(0.5f);
        
        // SPAWN ICE WALL (or other magic)
        if (iceWallPrefab)
        {
            float direction = transform.localScale.x; // 1 or -1
            Vector2 spawnPos = (Vector2)transform.position + (Vector2.right * direction * wallOffsetDistance);
            
            GameObject wall = Instantiate(iceWallPrefab, spawnPos, Quaternion.identity);
            
            // Handle Fading
            IceWall wallScript = wall.GetComponent<IceWall>();
            if (wallScript != null) wallScript.Activate(wallLifeTime);
            else Destroy(wall, wallLifeTime);
        }

        magicTimer = magicCooldown;
        isBusy = false;
    }

    // --- HELPERS ---

    void PlayRandomSound(AudioClip[] clips, float volume)
    {
        if (clips != null && clips.Length > 0 && audioSource)
        {
            int randomIndex = Random.Range(0, clips.Length);
            audioSource.pitch = Random.Range(0.9f, 1.1f); 
            audioSource.PlayOneShot(clips[randomIndex], volume);
        }
    }

    IEnumerator ShakeCamera(float intensity, float duration)
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 originalPos = mainCam.transform.position;
            float elapsed = 0.0f;
            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;
                mainCam.transform.position = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
                elapsed += Time.deltaTime;
                yield return null;
            }
            mainCam.transform.position = originalPos;
        }
    }
}