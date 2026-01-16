using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyLimbController))]
[RequireComponent(typeof(AudioSource))] // Added AudioSource
public class PenguinEnemyAI : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("Leave empty to auto-find PlayerMovement script.")]
    public Transform player; 

    [Header("Audio Settings")]
    public AudioClip[] attackSounds;
    public AudioClip[] magicSounds;
    public AudioClip[] generalSounds; // Ambient/Waddle sounds
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

    // Internal
    private Rigidbody2D rb;
    private EnemyLimbController limbController;
    private AudioSource audioSource;
    private float meleeTimer;
    private float magicTimer;
    private bool isBusy = false;

    // General Sound Timer
    private float generalSoundTimer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        limbController = GetComponent<EnemyLimbController>();
        audioSource = GetComponent<AudioSource>();

        // Randomize start time for general sounds so multiple penguins don't speak at once
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
        // Only play if not attacking and close enough to be heard
        if (!isBusy && Vector2.Distance(transform.position, player.position) < 20f)
        {
            generalSoundTimer -= Time.deltaTime;
            if (generalSoundTimer <= 0)
            {
                PlayRandomSound(generalSounds, 0.4f); // Lower volume for ambient
                generalSoundTimer = Random.Range(3f, 7f); // Wait 3-7 seconds for next sound
            }
        }

        float dist = Vector2.Distance(transform.position, player.position);

        if (meleeTimer > 0) meleeTimer -= Time.deltaTime;
        if (magicTimer > 0) magicTimer -= Time.deltaTime;

        if (isBusy) {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // LOGIC
        if (dist > wakeUpDistance) {
            // Idle
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

        if(animator) animator.SetBool("IsWalking", true);
    }

    // --- ANIMATION EVENTS ---
    // This function is called by the Animation Event on Frame 8 and 26
    public void PlayFootstep()
    {
        // Only play if actually moving
        if (animator.GetBool("IsWalking"))
        {
            PlayRandomSound(footstepSounds, 0.6f);
            StartCoroutine(ShakeCamera(footstepShakeAmount, 0.1f));
        }
    }

    IEnumerator DoMeleeAttack()
    {
        isBusy = true;
        rb.linearVelocity = Vector2.zero;
        if(animator) {
            animator.SetBool("IsWalking", false);
            animator.SetTrigger("Attack");
        }

        // Play Attack Sound
        PlayRandomSound(attackSounds, 1.0f);
        
        yield return new WaitForSeconds(0.5f); 
        
        StartCoroutine(ShakeCamera(attackShakeAmount, 0.15f)); // Big shake on hit

        if (player != null && Vector2.Distance(transform.position, player.position) <= meleeRange * 1.5f)
        {
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
        if(animator) {
            animator.SetBool("IsWalking", false);
            animator.SetTrigger("Magic");
        }
        
        // Play Magic Sound
        PlayRandomSound(magicSounds, 1.0f);

        yield return new WaitForSeconds(0.5f);
        
        magicTimer = magicCooldown;
        isBusy = false;
    }

    // --- HELPERS ---

    void PlayRandomSound(AudioClip[] clips, float volume)
    {
        if (clips != null && clips.Length > 0 && audioSource)
        {
            int randomIndex = Random.Range(0, clips.Length);
            // Randomize pitch slightly for variety
            audioSource.pitch = Random.Range(0.9f, 1.1f); 
            audioSource.PlayOneShot(clips[randomIndex], volume);
        }
    }

    // Simple Camera Shake logic
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

                // Keeps the Z position the same
                mainCam.transform.position = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);

                elapsed += Time.deltaTime;
                yield return null;
            }

            mainCam.transform.position = originalPos;
        }
    }
}