using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyLimbController))]
[RequireComponent(typeof(EnemyAnimationController))]
public class EnemyAI : MonoBehaviour
{
    // Added 'Idle' at the start to match EnemyAnimationController.State indices
    private enum State { Idle, Roam, Chase, Attack, Investigate, Scavenge, Flee }

    public int mouthVaritation = 0;

    [Header("AI Settings")]
    public float detectionRadius = 5f;
    [Tooltip("How far away the enemy can hear bushes rustling.")]
    public float hearingRadius = 8f; 
    public float attackRange = 1.0f;
    public float roamRadius = 3f;
    public float baseMoveSpeed = 2f;
    public float baseDamage = 5f;

    [Header("Movement Behavior")]
    [Tooltip("If true, the enemy will try to keep a specific distance from the player instead of rushing in.")]
    [SerializeField] private bool maintainDistance = false;
    [Tooltip("The distance to maintain from the player (only if maintainDistance is true).")]
    [SerializeField] private float preferredDistance = 4f;
    [Tooltip("Buffer zone to prevent jittering when maintaining distance.")]
    [SerializeField] private float distanceBuffer = 0.5f;

    [Header("Flee Settings")]
    [Tooltip("Percentage of health (0-1) below which the enemy will flee.")]
    [SerializeField] private float fleeHealthThreshold = 0.25f;
    [SerializeField] private float fleeSpeedMult = 1.2f;
    
    [Header("Optimization")]
    [Tooltip("If the player is further than this distance, the AI logic will stop running.")]
    [SerializeField] private float cullingDistance = 30f; 

    [Header("Scavenging")]
    [SerializeField] private float scavengeRadius = 6f;
    [SerializeField] private float scavengeScanInterval = 1.0f; 

    [Header("Speed Multipliers")]
    [SerializeField] private float roamSpeedMult = 0.5f;
    [SerializeField] private float chaseSpeedMult = 1.4f;
    [SerializeField] private float turningSpeed = 5f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleLayer;
    [Tooltip("Layer mask for dangerous objects (traps). AI will avoid these from further away.")]
    [SerializeField] private LayerMask trapLayer;
    [SerializeField] private float avoidDistance = 1.5f;
    [Tooltip("How far ahead to look for traps. Should be higher than Avoid Distance.")]
    [SerializeField] private float trapAvoidDistance = 3.0f;
    [SerializeField] private float bodyWidth = 0.5f;

    [Header("Stuck Detection")] 
    [SerializeField] private float stuckCheckInterval = 0.5f;
    [SerializeField] private float stuckThreshold = 0.1f;
    [SerializeField] private float unstuckDuration = 1.0f;

    [Header("Audio Behavior")] 
    [SerializeField] private float minIdleSoundInterval = 3f;
    [SerializeField] private float maxIdleSoundInterval = 8f;
    [Tooltip("Minimum time (seconds) between flee sounds to prevent spam.")]
    [SerializeField] private float minFleeSoundInterval = 0.6f;

    [Header("Timers")]
    public float attackCooldown = 1.5f;
    public float minRoamWaitTime = 1f;
    public float maxRoamWaitTime = 4f;
    public float investigateTime = 3f;

    // References
    private Transform player;
    private Rigidbody2D rb;
    private EnemyLimbController body;
    private EnemyAnimationController anim;
    private Vector2 startPos;
    
    // State
    private State currentState;
    private Vector2 moveTarget; 
    private Vector2 lastKnownPlayerPos; 
    private float stateTimer;
    private float attackTimer;
    private Vector3 originalScale;

    // Audio State
    private float idleSoundTimer;
    private float fleeSoundTimer;

    // Scavenge State
    private WorldLimb targetLimb;
    private float scavengeScanTimer = 0f;

    // Avoidance State
    private float avoidanceCommitTimer = 0f;
    private Vector2 committedAvoidDir;

    // Stuck State 
    private Vector2 positionAtLastCheck;
    private float stuckTimer;
    private bool isForcingUnstuck = false;
    private Vector2 unstuckDir;
    private float forcingUnstuckTimer;

    // Trapped State
    private bool isTrapped = false;

    // Optimization
    private float cullingDistanceSq;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<EnemyLimbController>();
        anim = GetComponent<EnemyAnimationController>();
        
        startPos = transform.position;
        originalScale = transform.localScale;
        
        lastKnownPlayerPos = transform.position;
        
        rb.freezeRotation = true; 
        rb.gravityScale = 0f; 
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;

        positionAtLastCheck = transform.position;
        stuckTimer = stuckCheckInterval;
        
        idleSoundTimer = Random.Range(minIdleSoundInterval, maxIdleSoundInterval);

        // Pre-calculate squared distance for cheaper checks
        cullingDistanceSq = cullingDistance * cullingDistance;

        PickNewRoamTarget();
    }

    void FixedUpdate()
    {
        if (player == null) return;

        // Optimization: Use sqrMagnitude to avoid expensive Sqrt operations
        if (((Vector2)transform.position - (Vector2)player.position).sqrMagnitude > cullingDistanceSq)
        {
            rb.linearVelocity = Vector2.zero; 
            
            // Disable animation controller to save processing
            if (anim != null && anim.enabled) anim.enabled = false;
            
            return; 
        }
        else
        {
            // Re-enable animation controller if we are back in range
            if (anim != null && !anim.enabled) anim.enabled = true;
        }

        // --- Sync Animation State ---
        if (anim != null)
        {
            anim.SetState((EnemyAnimationController.State)currentState);
        }

        if (isTrapped)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        HandleStuckDetection();
        HandleIdleSounds();

        if (isForcingUnstuck)
        {
            LogicUnstuck();
            return;
        }

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
            case State.Scavenge:
                LogicScavenge();
                break;
            case State.Flee:
                LogicFlee();
                break;
        }

        if (attackTimer > 0) attackTimer -= Time.deltaTime;
        if (avoidanceCommitTimer > 0) avoidanceCommitTimer -= Time.deltaTime;
        if (scavengeScanTimer > 0) scavengeScanTimer -= Time.deltaTime;
        if (fleeSoundTimer > 0) fleeSoundTimer -= Time.deltaTime;
    }

    bool ShouldFlee()
    {
        bool lowHealth = (body.currentHealth / body.maxHealth) < fleeHealthThreshold;
        bool noArms = !body.hasArms;
        return lowHealth || noArms;
    }

    void HandleIdleSounds()
    {
        if (currentState == State.Roam || currentState == State.Investigate)
        {
            idleSoundTimer -= Time.deltaTime;
            if (idleSoundTimer <= 0)
            {
                if (Vector2.Distance(transform.position, player.position) < 15f)
                {
                    body.PlayIdleSound();
                }
                idleSoundTimer = Random.Range(minIdleSoundInterval, maxIdleSoundInterval);
            }
        }
    }

    // --- Helper for Flee Transition ---
    void SwitchToFleeState()
    {
        if (currentState != State.Flee)
        {
            currentState = State.Flee;
            
            // Only play sound if cooldown is ready
            if (fleeSoundTimer <= 0)
            {
                mouthVaritation = 1;
                body.PlayFleeSound();
                fleeSoundTimer = minFleeSoundInterval;
            }
        }
    }

    void SwitchToChaseState()
    {
        // Don't switch if already fleeing (unless explicitly handled elsewhere)
        // If we are already chasing, this refresh is harmless.
        if (currentState != State.Flee)
        {
            if (!ShouldFlee())
            {
                if (currentState != State.Chase)
                {
                    currentState = State.Chase;
                    body.PlaySpotSound();
                }
            }
            else
            {
                SwitchToFleeState();
            }
        }
    }

    // --- NEW: Public method called when damaged ---
    public void OnDamageTaken()
    {
        if (player == null) return;

        // Instantly know where the player is
        lastKnownPlayerPos = player.position;
        mouthVaritation = 2;
        // Try to chase
        SwitchToChaseState();
    }
    // ----------------------------------------------

    void LogicFlee()
    {
        if (!ShouldFlee())
        {
            currentState = State.Chase;
            return;
        }

        if (!CanSeePlayer())
        {
            currentState = State.Roam;
            PickNewRoamTarget();
            return;
        }

        Vector2 dirAway = ((Vector2)transform.position - (Vector2)player.position).normalized;
        Vector2 fleeTarget = (Vector2)transform.position + dirAway * 3f;

        float speed = (baseMoveSpeed + body.moveSpeedBonus) * fleeSpeedMult;
        if (!body.hasLegs) speed = 0f; 
        
        MoveTowards(fleeTarget, speed);
    }

    void LogicScavenge()
    {
        if (targetLimb == null)
        {
            PickNewRoamTarget();
            return;
        }

        if (CanSeePlayer())
        {
            SwitchToChaseState();
            return;
        }

        float speed = (baseMoveSpeed + body.moveSpeedBonus) * chaseSpeedMult;
        if (!body.hasLegs) speed = 0f; 

        MoveTowards(targetLimb.transform.position, speed);

        float dist = Vector2.Distance(transform.position, targetLimb.transform.position);
        
        // Increased pickup radius
        if (dist < 2.0f) 
        {
            // Safety Check: Ensure data exists
            if (targetLimb.GetLimbData() != null)
            {
                bool attached = body.TryAttachLimb(targetLimb.GetLimbData(), targetLimb.IsShowingDamaged());
                if (attached)
                {
                    Destroy(targetLimb.gameObject);
                    Debug.Log("Enemy successfully scavenged a limb!");
                }
            }
            
            // Pick a new target regardless of success to prevent getting stuck in a loop
            PickNewRoamTarget();
        }
    }

    void ScanForLimbs()
    {
        if (scavengeScanTimer > 0) return;
        scavengeScanTimer = scavengeScanInterval;

        if (!body.hasLegs) return;

        bool needArm = body.IsMissingArm();
        bool needLeg = body.IsMissingLeg();

        if (!needArm && !needLeg) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, scavengeRadius);
        float closestDist = float.MaxValue;
        WorldLimb bestCandidate = null;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("LimbPickup")) 
            {
                WorldLimb limb = hit.GetComponent<WorldLimb>();
                // FIX: Added '&& limb.GetLimbData() != null' to prevent crash if data is missing
                if (limb != null && limb.CanPickup() && limb.GetLimbData() != null)
                {
                    LimbType type = limb.GetLimbData().limbType;
                    
                    // --- UNIVERSAL LOGIC ---
                    bool isUseful = false;
                    if (type == LimbType.Arm && needArm) isUseful = true;
                    else if (type == LimbType.Leg && needLeg) isUseful = true;
                    else if (type == LimbType.Universal && (needArm || needLeg)) isUseful = true;

                    if (isUseful)
                    {
                        if (CanSeeObject(hit.transform.position))
                        {
                            float d = Vector2.Distance(transform.position, hit.transform.position);
                            if (d < closestDist)
                            {
                                closestDist = d;
                                bestCandidate = limb;
                            }
                        }
                    }
                }
            }
        }

        if (bestCandidate != null)
        {
            targetLimb = bestCandidate;
            currentState = State.Scavenge;
        }
    }

    void HandleStuckDetection()
    {
        if (currentState == State.Attack || isForcingUnstuck) return;

        stuckTimer -= Time.deltaTime;
        if (stuckTimer <= 0)
        {
            float distMoved = Vector2.Distance(transform.position, positionAtLastCheck);
            if (distMoved < stuckThreshold && rb.linearVelocity.magnitude > 0.1f)
            {
                StartUnstuck();
            }
            stuckTimer = stuckCheckInterval;
            positionAtLastCheck = transform.position;
        }
    }

    void StartUnstuck()
    {
        isForcingUnstuck = true;
        forcingUnstuckTimer = unstuckDuration;
        unstuckDir = Random.insideUnitCircle.normalized;
    }

    void LogicUnstuck()
    {
        float speed = baseMoveSpeed + body.moveSpeedBonus;
        if (!body.hasLegs) speed = 0f;

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, unstuckDir * speed, turningSpeed * Time.fixedDeltaTime);
        
        forcingUnstuckTimer -= Time.deltaTime;
        if (forcingUnstuckTimer <= 0)
        {
            isForcingUnstuck = false;
            if (currentState == State.Roam) PickNewRoamTarget();
        }
    }

    void LogicRoam()
    {
        float distFromHome = Vector2.Distance(transform.position, startPos);
        if (distFromHome > roamRadius * 2.5f) 
        {
            PickNewRoamTarget();
            return;
        }

        if (CanSeePlayer())
        {
            SwitchToChaseState();
            return;
        }

        ScanForLimbs();
        if (currentState == State.Scavenge) return;

        float speed = (baseMoveSpeed + body.moveSpeedBonus) * roamSpeedMult;
        if (!body.hasLegs) speed = 0f; 

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
        if (ShouldFlee())
        {
            SwitchToFleeState();
            return;
        }

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
            // Player lost from sight, go to last known position
            moveTarget = lastKnownPlayerPos;
            stateTimer = investigateTime;
            currentState = State.Investigate;
            return;
        }

        float speed = (baseMoveSpeed + body.moveSpeedBonus) * chaseSpeedMult;
        if (!body.hasLegs) speed = 0f;

        // --- MOVEMENT LOGIC ---
        if (maintainDistance)
        {
            if (distToPlayer > preferredDistance + distanceBuffer)
            {
                MoveTowards(player.position, speed);
            }
            else if (distToPlayer < preferredDistance - distanceBuffer)
            {
                Vector2 dirAway = ((Vector2)transform.position - (Vector2)player.position).normalized;
                Vector2 fleePos = (Vector2)transform.position + dirAway;
                MoveTowards(fleePos, speed);
            }
            else
            {
                rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, turningSpeed * Time.fixedDeltaTime);
                float dirX = player.position.x - transform.position.x;
                if (Mathf.Abs(dirX) > 0.1f)
                {
                    Vector3 scale = transform.localScale;
                    scale.x = Mathf.Abs(scale.x) * (dirX > 0 ? 1 : -1);
                    transform.localScale = scale;
                }
            }
        }
        else
        {
            MoveTowards(player.position, speed);
        }
    }

    void LogicAttack()
    {
        if (ShouldFlee())
        {
            SwitchToFleeState();
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, player.position);

        if (maintainDistance && distToPlayer < preferredDistance - distanceBuffer)
        {
            float speed = (baseMoveSpeed + body.moveSpeedBonus) * chaseSpeedMult;
            if (!body.hasLegs) speed = 0f;
            
            Vector2 dirAway = ((Vector2)transform.position - (Vector2)player.position).normalized;
            MoveTowards((Vector2)transform.position + dirAway, speed);
        }
        else
        {
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.deltaTime * 10f);
        }
        
        if (distToPlayer > attackRange * 1.2f)
        {
            SwitchToChaseState(); // Go back to chase
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
            SwitchToChaseState();
            return;
        }

        ScanForLimbs();
        if (currentState == State.Scavenge) return;

        float speed = (baseMoveSpeed + body.moveSpeedBonus) * chaseSpeedMult; 
        if (!body.hasLegs) speed = 0f;

        MoveTowards(moveTarget, speed);

        if (Vector2.Distance(transform.position, moveTarget) < 0.5f)
        {
            rb.linearVelocity = Vector2.zero;
            stateTimer -= Time.deltaTime;

            if (stateTimer <= 0)
            {
                startPos = transform.position;
                PickNewRoamTarget();
            }
        }
    }

    public void OnHearNoise(Vector2 noisePos)
    {
        if (Vector2.Distance(transform.position, noisePos) > hearingRadius) return;
        if (currentState == State.Chase || currentState == State.Attack || currentState == State.Flee) return;

        moveTarget = noisePos;
        stateTimer = investigateTime;
        currentState = State.Investigate;
    }

    private bool CanSeeObject(Vector2 targetPos)
    {
        float dist = Vector2.Distance(transform.position, targetPos);
        if (dist > detectionRadius) return false;

        Vector2 dirToTarget = (targetPos - (Vector2)transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToTarget, dist, obstacleLayer);
        return hit.collider == null;
    }

    private bool CanSeePlayer()
    {
        return CanSeeObject(player.position);
    }

    void PerformAttack()
    {
        if (!body.hasArms) return; 

        body.PlayAttackSound();

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
        if (speed <= 0.01f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 desiredDir = (target - (Vector2)transform.position).normalized;

        if (avoidanceCommitTimer > 0)
        {
            desiredDir = committedAvoidDir;
        }
        else
        {
            bool trapDetected = false;
            
            RaycastHit2D trapHit = Physics2D.CircleCast(transform.position, bodyWidth * 0.8f, desiredDir, trapAvoidDistance, trapLayer);
            if (trapHit.collider != null) trapDetected = true;

            bool wallDetected = false;
            RaycastHit2D[] wallHits = Physics2D.CircleCastAll(transform.position, bodyWidth / 2f, desiredDir, avoidDistance, obstacleLayer);
            foreach(var hit in wallHits)
            {
                if(hit.collider != null && hit.collider.gameObject != gameObject) 
                { 
                    wallDetected = true; 
                    break; 
                }
            }

            if (trapDetected || wallDetected)
            {
                Vector2 leftDir = Quaternion.Euler(0, 0, 60) * desiredDir;
                Vector2 rightDir = Quaternion.Euler(0, 0, -60) * desiredDir;

                bool hitLeft = Physics2D.Raycast(transform.position, leftDir, avoidDistance, obstacleLayer) ||
                               Physics2D.Raycast(transform.position, leftDir, trapAvoidDistance, trapLayer);

                bool hitRight = Physics2D.Raycast(transform.position, rightDir, avoidDistance, obstacleLayer) ||
                                Physics2D.Raycast(transform.position, rightDir, trapAvoidDistance, trapLayer);

                if (!hitLeft && !hitRight) committedAvoidDir = leftDir; 
                else if (!hitLeft) committedAvoidDir = leftDir;
                else if (!hitRight) committedAvoidDir = rightDir;
                else committedAvoidDir = -desiredDir; 

                avoidanceCommitTimer = 0.4f; 
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

    public void SetTrapped(bool trapped)
    {
        isTrapped = trapped;
        if (isTrapped) rb.linearVelocity = Vector2.zero;
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
        else if (currentState == State.Scavenge && targetLimb != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, targetLimb.transform.position);
        }
        else if (currentState == State.Flee)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 1f); 
        }
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(startPos, 0.2f);
    }
}