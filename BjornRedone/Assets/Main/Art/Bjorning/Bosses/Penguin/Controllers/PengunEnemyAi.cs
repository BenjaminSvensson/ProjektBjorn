using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyLimbController))]
public class PenguinEnemyAI : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("You can leave this empty! The script will auto-find the PlayerMovement script.")]
    public Transform player; 

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
    private float meleeTimer;
    private float magicTimer;
    private bool isBusy = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        limbController = GetComponent<EnemyLimbController>();
        
        // --- AUTO-TARGETING LOGIC ---
        
        // 1. If the slot is empty, find the unique PlayerMovement script
        if (player == null)
        {
            // This is the most reliable way to find the real player
            var playerScript = FindObjectOfType<PlayerMovement>(); 
            
            if (playerScript != null) 
            {
                player = playerScript.transform;
            }
            else 
            {
                // Fallback: If PlayerMovement isn't found, try the Tag
                GameObject pTag = GameObject.FindGameObjectWithTag("Player");
                if (pTag != null) player = pTag.transform;
            }
        }

        // 2. Final Error Check
        if (player == null) {
            Debug.LogError("CRITICAL: Penguin could not find the Player! (No PlayerMovement script and no 'Player' tag found).");
        }
        else if (player == transform) {
            Debug.LogError("Penguin is targeting ITSELF! Check your scripts.");
            player = null;
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0; 
        rb.freezeRotation = true;
    }

    void Update()
    {
        // If we still have no player, just wait.
        if (player == null || limbController == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (meleeTimer > 0) meleeTimer -= Time.deltaTime;
        if (magicTimer > 0) magicTimer -= Time.deltaTime;

        if (isBusy) {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // LOGIC
        if (dist > wakeUpDistance) {
            // Idle / Sleep
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

        if(animator) animator.SetBool("IsWalking", true);
    }

    IEnumerator DoMeleeAttack()
    {
        isBusy = true;
        rb.linearVelocity = Vector2.zero;
        if(animator) {
            animator.SetBool("IsWalking", false);
            animator.SetTrigger("Attack");
        }
        limbController.PlayAttackSound();

        yield return new WaitForSeconds(0.5f); 
        
        // HIT PLAYER
        if (player != null && Vector2.Distance(transform.position, player.position) <= meleeRange * 1.5f)
        {
            // Look for PlayerLimbController first
            var pLimb = player.GetComponent<PlayerLimbController>();
            if (pLimb) 
            {
                pLimb.TakeDamage(10f + limbController.attackDamageBonus);
            }
            else
            {
                // Fallback: Look for the same PlayerMovement script we found earlier?
                // Or just apply force if you have a different health system
            }
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
        yield return new WaitForSeconds(0.5f);
        
        // Insert magic spawning code here (Icicles, Walls, etc.)
        
        magicTimer = magicCooldown;
        isBusy = false;
    }
}