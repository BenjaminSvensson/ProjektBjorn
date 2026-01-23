using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Collider2D))] // Added requirement for Collider
public class BirdEgg : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] AudioSource crackSoundSource;

    [Header("Components")]
    public Transform spriteVisual;
    public Transform shadowVisual;
    public Animator eggAnimator;

    [Header("Animation States")]
    public string fallStateName = "Fall";
    public string crackStateName = "Crack";
    
    [Header("Settings")]
    public float fallSpeed = 15.0f; 
    public float damage = 10.0f;
    public float damageRadius = 0.5f;
    public float persistDuration = 0.6f; // How long the broken egg stays on ground
    
    [Header("Shadow Scaling")]
    private Vector3 shadowScaleGround = new Vector3(0.5f, 0.25f, 1f);
    private Vector3 shadowScaleAir = new Vector3(0.2f, 0.1f, 1f);

    // Internal State
    private bool isInitialized = false;
    private bool hasLanded = false;
    private Collider2D myCollider;

    // Movement Tracking
    private Vector3 startOffset;
    private Vector3 currentOffset;
    private float totalHeight;
    private float fallDuration;
    private float timer;

    void Awake()
    {
        // 1. Get Components
        myCollider = GetComponent<Collider2D>();
        if (crackSoundSource == null) crackSoundSource = GetComponent<AudioSource>();

        // 2. CRITICAL FIX: Disable collider immediately so player doesn't walk into the invisible egg
        if (myCollider) myCollider.enabled = false;
    }

    public void Initialize(Vector2 targetPos, Vector2 visualStartPos)
    {
        // 1. Place the GameObject Hitbox on the GROUND (Target)
        transform.position = targetPos;
        
        // 2. Calculate how high the sprite should be
        Vector3 worldDiff = (Vector3)visualStartPos - (Vector3)targetPos;
        startOffset = worldDiff;
        currentOffset = startOffset;
        
        // 3. Calculate timing based on height
        totalHeight = Mathf.Max(worldDiff.y, 1.0f);
        fallDuration = totalHeight / fallSpeed;

        // 4. Set Visuals high in the air
        if (spriteVisual) spriteVisual.localPosition = startOffset;
        if (eggAnimator) eggAnimator.Play(fallStateName);
        
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || hasLanded) return;

        timer += Time.deltaTime;
        float ratio = Mathf.Clamp01(timer / fallDuration);

        // Move Sprite DOWN towards 0 (local position)
        currentOffset = Vector3.Lerp(startOffset, Vector3.zero, ratio);
        if (spriteVisual) spriteVisual.localPosition = currentOffset;

        UpdateShadow(ratio);

        if (ratio >= 1.0f) Land();
    }

    void UpdateShadow(float ratio)
    {
        if (shadowVisual)
        {
            shadowVisual.localScale = Vector3.Lerp(shadowScaleAir, shadowScaleGround, ratio);
            
            SpriteRenderer sr = shadowVisual.GetComponent<SpriteRenderer>();
            if(sr)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(0.2f, 0.8f, ratio);
                sr.color = c;
            }
        }
    }

    void Land()
    {
        hasLanded = true;
        
        // 1. Play FX
        if (crackSoundSource != null) crackSoundSource.Play();
        if (spriteVisual) spriteVisual.localPosition = Vector3.zero;
        if (eggAnimator) eggAnimator.Play(crackStateName);

        // 2. Visual Cleanup
        if (shadowVisual)
        {
            shadowVisual.localScale = shadowScaleGround;
            SpriteRenderer sr = shadowVisual.GetComponent<SpriteRenderer>();
            if(sr) { Color c = sr.color; c.a = 0.8f; sr.color = c; }
            shadowVisual.gameObject.SetActive(false); // Hide shadow on impact
        }

        // 3. EXPLODE (Deal Damage)
        Explode();
        
        // 4. Enable Collider (Optional: if you want the broken egg to block movement for a moment)
        // If you don't want the broken egg to block the player, keep this line commented out.
        // if (myCollider) myCollider.enabled = true; 

        Destroy(gameObject, persistDuration);
    }

    void Explode()
    {
        // Use OverlapCircle to detect anyone standing in the landing zone
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, damageRadius);
        
        foreach (var hit in hits)
        {
            // 1. Check for Player
            PlayerLimbController playerController = hit.GetComponent<PlayerLimbController>();
            if (playerController != null)
            {
                Vector2 dir = (hit.transform.position - transform.position).normalized;
                playerController.TakeDamage(damage, dir);
                continue; // Found player, move to next hit
            }
            
            // 2. Ignore the Bird that dropped it (Safety check)
            if (hit.GetComponent<BirdEnemyAI>()) continue;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}