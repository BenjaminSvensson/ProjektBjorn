using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyLimbController))]
[RequireComponent(typeof(EnemyAnimationController))]
public class EnemyAI : MonoBehaviour
{
    private enum State { Roam, Chase, Attack, Investigate }

    [Header("AI Settings")]
    public float detectionRadius = 5f;
    [Tooltip("How far away the enemy can hear bushes rustling.")]
    public float hearingRadius = 8f; 
    public float attackRange = 1.0f;
    public float roamRadius = 3f;
    public float baseMoveSpeed = 2f;
    public float baseDamage = 5f;

    [Header("Speed Multipliers")]
    [SerializeField] private float roamSpeedMult = 0.5f;
    [SerializeField] private float chaseSpeedMult = 1.4f;
    [SerializeField] private float turningSpeed = 5f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float avoidDistance = 1.5f;
    [SerializeField] private float bodyWidth = 0.5f;

    [Header("Stuck Detection")] 
    [Tooltip("How often (in seconds) to check if we haven't moved.")]
    [SerializeField] private float stuckCheckInterval = 0.5f;
    [Tooltip("If we moved less than this distance in the interval, we are stuck.")]
    [SerializeField] private float stuckThreshold = 0.1f;
    [Tooltip("How long to force a random direction when stuck.")]
    [SerializeField] private float unstuckDuration = 1.0f;

    [Header("Timers")]
    public float attackCooldown = 1.5f;
    public float minRoamWaitTime = 1f;
    public float maxRoamWaitTime = 4f;
    [Tooltip("How long the enemy stays suspicious after losing sight/reaching noise.")]
    public float investigateTime = 3f;

    // References
    private Transform player;
    private Rigidbody2D rb;
    private EnemyLimbController body;
    private EnemyAnimationController anim;
    private Vector2 startPos; // The anchor point for roaming
    
    // State
    private State currentState;
    private Vector2 moveTarget; 
    private Vector2 lastKnownPlayerPos; 
    private float stateTimer;
    private float attackTimer;
    private Vector3 originalScale;

    // Avoidance State
    private float avoidanceCommitTimer = 0f;
    private Vector2 committedAvoidDir;

    // Stuck State 
    private Vector2 positionAtLastCheck;
    private float stuckTimer;
    private bool isForcingUnstuck = false;
    private Vector2 unstuckDir;
    private float forcingUnstuckTimer;

    // --- NEW: Trapped State ---
    private bool isTrapped = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<EnemyLimbController>();
        anim = GetComponent<EnemyAnimationController>();
        
        startPos = transform.position;
        originalScale = transform.localScale;
        
        // Initialize lastKnownPlayerPos to prevent running to (0,0) on spawn
        lastKnownPlayerPos = transform.position;
        
        rb.freezeRotation = true; 
        rb.gravityScale = 0f; 
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;

        // Initialize stuck check
        positionAtLastCheck = transform.position;
        stuckTimer = stuckCheckInterval;

        PickNewRoamTarget();
    }

    void FixedUpdate()
    {
        if (player == null) return;

        // --- NEW: Trapped Check ---
        // If trapped, stop all movement immediately.
        if (isTrapped)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        // --------------------------

        // --- Stuck Detection Logic ---
        HandleStuckDetection();

        // If we are forcing an unstuck maneuver, override normal logic
        if (isForcingUnstuck)
        {
            LogicUnstuck();
            return;
        }

        // --- Normal Logic ---
        switch (currentState)
        {
            case State.Roam:
                LogicRoam();
                break;
            case State.Chase:
                LogicChase();
                break;
            case State.Attack:
                LogicAttack();
                break;
            case State.Investigate:
                LogicInvestigate();
                break;
        }

        // Cooldowns
        if (attackTimer > 0) attackTimer -= Time.deltaTime;
        if (avoidanceCommitTimer > 0) avoidanceCommitTimer -= Time.deltaTime;
    }

    // --- Stuck Detection Helper ---
    void HandleStuckDetection()
    {
        // Don't check if we are attacking (standing still is normal) 
        // or already unstuck-ing.
        if (currentState == State.Attack || isForcingUnstuck) return;

        stuckTimer -= Time.deltaTime;
        if (stuckTimer <= 0)
        {
            float distMoved = Vector2.Distance(transform.position, positionAtLastCheck);
            
            // If we barely moved but we were trying to move...
            if (distMoved < stuckThreshold && rb.linearVelocity.magnitude > 0.1f)
            {
                StartUnstuck();
            }

            // Reset
            stuckTimer = stuckCheckInterval;
            positionAtLastCheck = transform.position;
        }
    }

    void StartUnstuck()
    {
        isForcingUnstuck = true;
        forcingUnstuckTimer = unstuckDuration;
        
        // Pick a random direction (simple but effective for wiggling out of corners)
        unstuckDir = Random.insideUnitCircle.normalized;
    }

    void LogicUnstuck()
    {
        // Just move in the random direction, ignoring avoidance/targets
        float speed = baseMoveSpeed + body.moveSpeedBonus;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, unstuckDir * speed, turningSpeed * Time.fixedDeltaTime);
        
        forcingUnstuckTimer -= Time.deltaTime;
        if (forcingUnstuckTimer <= 0)
        {
            isForcingUnstuck = false;
            // Also pick a new roam target if we were roaming, just in case the old one was bad
            if (currentState == State.Roam) PickNewRoamTarget();
        }
    }

    void LogicRoam()
    {
        // --- Editor Drag / Teleport Detection ---
        float distFromHome = Vector2.Distance(transform.position, startPos);
        if (distFromHome > roamRadius * 2.5f) 
        {
            PickNewRoamTarget();
            return;
        }

        if (CanSeePlayer())
        {
            currentState = State.Chase;
            return;
        }

        float speed = (baseMoveSpeed + body.moveSpeedBonus) * roamSpeedMult;
        if (!body.hasLegs) speed *= 0.2f;

        MoveTowards(moveTarget, speed);

        if (Vector2.Distance(transform.position, moveTarget) < 0.2f)
        {
            rb.linearVelocity = Vector2.zero; 
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0) PickNewRoamTarget();
        }
    }

    void LogicChase()
    {
        float distToPlayer = Vector2.Distance(transform.position, player.position);

        if (CanSeePlayer())
        {
            lastKnownPlayerPos = player.position;
            
            if (distToPlayer < attackRange)
            {
                currentState = State.Attack;
                return;
            }
        }
        else
        {
            moveTarget = lastKnownPlayerPos;
            stateTimer = investigateTime;
            currentState = State.Investigate;
            return;
        }

        float speed = (baseMoveSpeed + body.moveSpeedBonus) * chaseSpeedMult;
        if (!body.hasLegs) speed *= 0.3f;

        MoveTowards(player.position, speed);
    }

    void LogicAttack()
    {
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.deltaTime * 10f);
        
        float distToPlayer = Vector2.Distance(transform.position, player.position);

        if (distToPlayer > attackRange * 1.2f)
        {
            currentState = State.Chase;
            return;
        }

        if (attackTimer <= 0)
        {
            PerformAttack();
            attackTimer = attackCooldown;
        }
    }

    void LogicInvestigate()
    {
        if (CanSeePlayer())
        {
            currentState = State.Chase;
            return;
        }

        float speed = (baseMoveSpeed + body.moveSpeedBonus) * chaseSpeedMult; 
        if (!body.hasLegs) speed *= 0.3f;

        MoveTowards(moveTarget, speed);

        if (Vector2.Distance(transform.position, moveTarget) < 0.5f)
        {
            rb.linearVelocity = Vector2.zero;
            stateTimer -= Time.deltaTime;

            if (stateTimer <= 0)
            {
                // Update StartPos if we've traveled far to prevent backtracking
                startPos = transform.position;
                PickNewRoamTarget();
            }
        }
    }

    public void OnHearNoise(Vector2 noisePos)
    {
        if (Vector2.Distance(transform.position, noisePos) > hearingRadius) return;
        if (currentState == State.Chase || currentState == State.Attack) return;

        moveTarget = noisePos;
        stateTimer = investigateTime;
        currentState = State.Investigate;
    }

    private bool CanSeePlayer()
    {
        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > detectionRadius) return false;

        Vector2 dirToPlayer = (player.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToPlayer, dist, obstacleLayer);
        return hit.collider == null;
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
                if (pc) pc.TakeDamage(damage);
            }
        }
    }

    void MoveTowards(Vector2 target, float speed)
    {
        Vector2 desiredDir = (target - (Vector2)transform.position).normalized;

        if (avoidanceCommitTimer > 0)
        {
            desiredDir = committedAvoidDir;
        }
        else
        {
            // Use CircleCastAll to ensure we don't accidentally hit our own collider 
            // and think we are blocked, which causes erratic movement.
            RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, bodyWidth / 2f, desiredDir, avoidDistance, obstacleLayer);
            bool obstacleDetected = false;

            foreach(var hit in hits)
            {
                // If we hit something that IS NOT ME, it's an obstacle
                if(hit.collider != null && hit.collider.gameObject != gameObject)
                {
                    obstacleDetected = true;
                    break;
                }
            }

            if (obstacleDetected)
            {
                Vector2 leftDir = Quaternion.Euler(0, 0, 50) * desiredDir;
                Vector2 rightDir = Quaternion.Euler(0, 0, -50) * desiredDir;

                bool hitLeft = Physics2D.Raycast(transform.position, leftDir, avoidDistance, obstacleLayer);
                bool hitRight = Physics2D.Raycast(transform.position, rightDir, avoidDistance, obstacleLayer);

                if (!hitLeft && !hitRight) committedAvoidDir = leftDir; 
                else if (!hitLeft) committedAvoidDir = leftDir;
                else if (!hitRight) committedAvoidDir = rightDir;
                else committedAvoidDir = -desiredDir; 

                avoidanceCommitTimer = 0.3f; 
                desiredDir = committedAvoidDir;
            }
        }

        Vector2 targetVelocity = desiredDir * speed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVelocity, turningSpeed * Time.fixedDeltaTime);

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
        // Update startPos to current position to prevent backtracking to spawn
        startPos = transform.position;

        Vector2 randomPoint = Random.insideUnitCircle * roamRadius;
        Vector2 potentialTarget = startPos + randomPoint;

        if (Physics2D.OverlapCircle(potentialTarget, 0.2f, obstacleLayer))
            moveTarget = startPos;
        else
            moveTarget = potentialTarget;

        currentState = State.Roam;
        stateTimer = Random.Range(minRoamWaitTime, maxRoamWaitTime);
    }

    // --- NEW: Set Trapped State (called by DamageSource) ---
    public void SetTrapped(bool trapped)
    {
        isTrapped = trapped;
        if (isTrapped)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, hearingRadius);
        
        if (currentState == State.Investigate || currentState == State.Roam)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, moveTarget);
            Gizmos.DrawWireSphere(moveTarget, 0.5f);
        }
        
        // Visualize the Home Base
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(startPos, 0.2f);
    }
}