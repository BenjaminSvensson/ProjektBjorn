using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyLimbController))]
public class PenguinEnemyAI : MonoBehaviour
{
    [Header("Targeting (Drag Player Here!)")]
    public Transform player; // <--- This is now Public!

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
        
        // --- HYBRID TARGETING SYSTEM ---
        
        // 1. Check if you manually dragged the player in
        if (player != null)
        {
            // Do nothing, we already have the player!
        }
        // 2. If slot is empty, try to find it automatically (Backup plan)
        else 
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // 3. Final Check
        if (player == null) {
            Debug.LogError("Penguin has NO TARGET. Please drag Player into the inspector slot!");
        }
        else if (player == transform) {
            Debug.LogError("Penguin is targeting ITSELF! Unassign the player slot in the inspector.");
            player = null;
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0; 
        rb.freezeRotation = true;
    }

    void Update()
    {
        if (player == null || limbController == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (meleeTimer > 0) meleeTimer -= Time.deltaTime;
        if (magicTimer > 0) magicTimer -= Time.deltaTime;

        if (isBusy) {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // LOGIC TREE
        if (dist > wakeUpDistance) {
            // Sleep
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
        
        // SIMPLE DAMAGE CHECK
        if (player != null && Vector2.Distance(transform.position, player.position) <= meleeRange * 1.5f)
        {
            // Try to find the Player's Limb Controller script
            var pLimb = player.GetComponent<PlayerLimbController>();
            if (pLimb) 
            {
                pLimb.TakeDamage(10f + limbController.attackDamageBonus);
            }
            // Fallback: If player has no limb script, check for basic health script?
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
        magicTimer = magicCooldown;
        isBusy = false;
    }

    // --- VISUALIZES THE ATTACK RANGE IN SCENE VIEW ---
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, magicRange);
    }
}