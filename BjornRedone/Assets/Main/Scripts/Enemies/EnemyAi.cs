using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyLimbController))]
[RequireComponent(typeof(EnemyAnimationController))]
public class EnemyAI : MonoBehaviour
{
    private enum State { Roam, Chase, Attack }

    [Header("AI Settings")]
    public float detectionRadius = 5f;
    public float attackRange = 1.0f;
    public float roamRadius = 3f;
    public float baseMoveSpeed = 2f;
    public float baseDamage = 5f;

    [Header("Speed Multipliers")]
    [Tooltip("Multiplier applied to base speed when Roaming (usually < 1).")]
    [SerializeField] private float roamSpeedMult = 0.5f;
    [Tooltip("Multiplier applied to base speed when Chasing (usually > 1).")]
    [SerializeField] private float chaseSpeedMult = 1.3f;
    [Tooltip("How fast the enemy turns (Higher = snappier, Lower = smoother/heavier).")]
    [SerializeField] private float turningSpeed = 5f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float avoidDistance = 1.5f;
    [Tooltip("How wide the enemy is for collision checks.")]
    [SerializeField] private float bodyWidth = 0.5f;

    [Header("Timers")]
    public float attackCooldown = 1.5f;
    public float minRoamWaitTime = 1f;
    public float maxRoamWaitTime = 4f;

    // References
    private Transform player;
    private Rigidbody2D rb;
    private EnemyLimbController body;
    private EnemyAnimationController anim;
    private Vector2 startPos;
    
    // State
    private State currentState;
    private Vector2 roamTarget;
    private float roamTimer;
    private float attackTimer;
    private Vector3 originalScale;

    // --- NEW: Avoidance State ---
    private float avoidanceCommitTimer = 0f;
    private Vector2 committedAvoidDir;
    // --- END NEW ---

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<EnemyLimbController>();
        anim = GetComponent<EnemyAnimationController>();
        
        startPos = transform.position;
        originalScale = transform.localScale;
        
        rb.freezeRotation = true; 
        
        // Use the new fast finder
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;

        PickNewRoamTarget();
    }

    void FixedUpdate()
    {
        if (player == null) return;

        float distToPlayer = Vector2.Distance(transform.position, player.position);

        // --- State Transitions ---
        switch (currentState)
        {
            case State.Roam:
                if (distToPlayer < detectionRadius) currentState = State.Chase;
                break;
            case State.Chase:
                if (distToPlayer < attackRange) currentState = State.Attack;
                else if (distToPlayer > detectionRadius * 1.5f) currentState = State.Roam; 
                break;
            case State.Attack:
                if (distToPlayer > attackRange) currentState = State.Chase;
                break;
        }

        // --- State Logic ---
        switch (currentState)
        {
            case State.Roam:
                HandleRoam();
                break;
            case State.Chase:
                HandleChase();
                break;
            case State.Attack:
                HandleAttack();
                break;
        }
        
        if (attackTimer > 0) attackTimer -= Time.deltaTime;
        if (avoidanceCommitTimer > 0) avoidanceCommitTimer -= Time.deltaTime;
    }

    void HandleRoam()
    {
        // Use the SLOW roam multiplier
        float speed = (baseMoveSpeed + body.moveSpeedBonus) * roamSpeedMult; 
        if (!body.hasLegs) speed *= 0.2f; 

        MoveTowards(roamTarget, speed);

        // Check if stuck (velocity very low while trying to move)
        if (rb.linearVelocity.magnitude < 0.1f && roamTimer <= 0)
        {
            // Force a new target if we are stuck against a wall
            PickNewRoamTarget();
        }

        if (Vector2.Distance(transform.position, roamTarget) < 0.2f)
        {
            roamTimer -= Time.deltaTime;
            if (roamTimer <= 0) PickNewRoamTarget();
        }
    }

    void HandleChase()
    {
        // Use the FAST chase multiplier
        float speed = (baseMoveSpeed + body.moveSpeedBonus) * chaseSpeedMult;
        if (!body.hasLegs) speed *= 0.3f; 

        MoveTowards(player.position, speed);
    }

    void HandleAttack()
    {
        // Stop smoothly
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.deltaTime * 10f);

        if (attackTimer <= 0)
        {
            PerformAttack();
            attackTimer = attackCooldown;
        }
    }

    void PerformAttack()
    {
        if (!body.hasArms) return; 

        LimbData weapon = body.GetActiveWeaponLimb();
        float damage = baseDamage + body.attackDamageBonus;
        
        float punchDuration = weapon != null ? weapon.punchDuration : 0.2f;
        anim.TriggerPunch((Vector2)player.position, punchDuration);
        
        Vector2 dirToPlayer = (player.position - transform.position).normalized;
        Vector2 hitPos = (Vector2)transform.position + (dirToPlayer * 0.5f);
        
        Collider2D[] hits = Physics2D.OverlapCircleAll(hitPos, 0.5f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerLimbController pc = hit.GetComponent<PlayerLimbController>();
                if (pc)
                {
                    pc.TakeDamage(damage);
                }
            }
        }
    }

    void MoveTowards(Vector2 target, float speed)
    {
        Vector2 desiredDir = (target - (Vector2)transform.position).normalized;

        // --- SMART AVOIDANCE LOGIC ---
        
        // 1. If we are currently committed to an avoidance direction, Stick to it!
        if (avoidanceCommitTimer > 0)
        {
            desiredDir = committedAvoidDir;
        }
        else
        {
            // 2. Check for obstacles using CircleCast (thicker than a ray, detects corners better)
            RaycastHit2D hit = Physics2D.CircleCast(transform.position, bodyWidth / 2f, desiredDir, avoidDistance, obstacleLayer);

            if (hit.collider != null)
            {
                // Obstacle Detected!
                
                // Calculate "Whiskers"
                Vector2 leftDir = Quaternion.Euler(0, 0, 45) * desiredDir;
                Vector2 rightDir = Quaternion.Euler(0, 0, -45) * desiredDir;

                bool hitLeft = Physics2D.Raycast(transform.position, leftDir, avoidDistance, obstacleLayer);
                bool hitRight = Physics2D.Raycast(transform.position, rightDir, avoidDistance, obstacleLayer);

                if (!hitLeft && !hitRight)
                {
                    // Both open? Pick the one that gets us closer to target or maintain momentum
                    committedAvoidDir = leftDir; 
                }
                else if (!hitLeft)
                {
                    committedAvoidDir = leftDir;
                }
                else if (!hitRight)
                {
                    committedAvoidDir = rightDir;
                }
                else
                {
                    // Trapped! Turn around hard.
                    committedAvoidDir = -desiredDir; 
                }

                // 3. COMMIT to this decision for 0.5 seconds
                // This prevents the "jitter" where they switch decisions every frame
                avoidanceCommitTimer = 0.5f; 
                desiredDir = committedAvoidDir;
            }
        }

        // --- MOMENTUM STEERING ---
        // Instead of setting velocity directly, we Lerp towards it. 
        // This creates smooth turns instead of instant snaps.
        Vector2 targetVelocity = desiredDir * speed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVelocity, turningSpeed * Time.fixedDeltaTime);

        // Visual Flipping
        if (Mathf.Abs(rb.linearVelocity.x) > 0.1f)
        {
            if (rb.linearVelocity.x > 0) 
                transform.localScale = new Vector3(originalScale.x, originalScale.y, originalScale.z);
            else 
                transform.localScale = new Vector3(-originalScale.x, originalScale.y, originalScale.z);
        }
    }

    void PickNewRoamTarget()
    {
        // Simple random point
        Vector2 randomPoint = Random.insideUnitCircle * roamRadius;
        Vector2 potentialTarget = startPos + randomPoint;

        // Safety check: is the target inside a wall?
        if (Physics2D.OverlapCircle(potentialTarget, 0.2f, obstacleLayer))
        {
            roamTarget = startPos; // Fallback to start
        }
        else
        {
            roamTarget = potentialTarget;
        }
        
        roamTimer = Random.Range(minRoamWaitTime, maxRoamWaitTime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + transform.right * avoidDistance, bodyWidth/2f);
    }
}