using UnityEngine;
using UnityEngine.UI;

public class BirdEgg : MonoBehaviour
{
    [Header("Components")]
    [Tooltip("The sprite object that moves")]
    public Transform spriteVisual;
    [Tooltip("The shadow object that stays on the ground")]
    public Transform shadowVisual;
    [Tooltip("Assign the Animator component here")]
    public Animator eggAnimator;

    [Header("Animation States")]
    public string fallStateName = "Fall";
    public string crackStateName = "Crack";
    
    [Header("Settings")]
    public float fallSpeed = 15.0f; 
    public float damage = 10.0f;
    public float damageRadius = 0.5f;
    [Tooltip("How long the egg stays visible after cracking before disappearing")]
    public float persistDuration = 0.6f; 
    
    private Vector3 shadowScaleGround = new Vector3(0.5f, 0.25f, 1f);
    private Vector3 shadowScaleAir = new Vector3(0.2f, 0.1f, 1f);

    private bool isInitialized = false;
    private bool hasLanded = false;

    // Movement Tracking
    private Vector3 startOffset;
    private Vector3 currentOffset;
    private float totalHeight;
    private float fallDuration;
    private float timer;

    public void Initialize(Vector2 targetPos, Vector2 visualStartPos)
    {
        // 1. Set ROOT position to the Target (where shadow will be)
        transform.position = targetPos;
        
        // 2. Calculate where the visual sprite needs to start relative to the root
        Vector3 worldDiff = (Vector3)visualStartPos - (Vector3)targetPos;
        startOffset = worldDiff;
        currentOffset = startOffset;
        
        // Use the Y difference as the "Height"
        totalHeight = Mathf.Max(worldDiff.y, 1.0f);
        
        // Calculate duration based on speed
        fallDuration = totalHeight / fallSpeed;

        if (spriteVisual) spriteVisual.localPosition = startOffset;
        
        // --- START ANIMATION ---
        if (eggAnimator) 
        {
            eggAnimator.Play(fallStateName);
        }
        
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || hasLanded) return;

        timer += Time.deltaTime;
        float ratio = Mathf.Clamp01(timer / fallDuration);

        // Linear interpolation from Start Offset to Zero (Target Center)
        currentOffset = Vector3.Lerp(startOffset, Vector3.zero, ratio);

        // Update Visuals
        if (spriteVisual)
        {
            spriteVisual.localPosition = currentOffset;
        }

        UpdateShadow(ratio);

        // Land Check
        if (ratio >= 1.0f)
        {
            Land();
        }
    }

    void UpdateShadow(float ratio)
    {
        if (shadowVisual)
        {
            // Ratio 0 = Air, Ratio 1 = Ground
            shadowVisual.localScale = Vector3.Lerp(shadowScaleAir, shadowScaleGround, ratio);
            
            SpriteRenderer sr = shadowVisual.GetComponent<SpriteRenderer>();
            if(sr)
            {
                Color c = sr.color;
                // Shadow gets darker as egg approaches
                c.a = Mathf.Lerp(0.2f, 0.8f, ratio);
                sr.color = c;
            }
        }
    }

    void Land()
    {
        hasLanded = true;
        
        // Snap to exact center
        if (spriteVisual) spriteVisual.localPosition = Vector3.zero;
        
        // Shadow final state
        if (shadowVisual)
        {
            shadowVisual.localScale = shadowScaleGround;
            SpriteRenderer sr = shadowVisual.GetComponent<SpriteRenderer>();
            if(sr) { Color c = sr.color; c.a = 0.8f; sr.color = c; }
        }

        // --- PLAY LAND ANIMATION ---
        if (eggAnimator)
        {
            eggAnimator.Play(crackStateName);
        }

        Explode();
        
        // Wait for animation to finish before destroying
        Destroy(gameObject, persistDuration);
    }

    void Explode()
    {
        // Optional: Particle effect here
        // Instantiate(dustParticles, transform.position, Quaternion.identity);
        shadowVisual.gameObject.SetActive(false);  
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, damageRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                hit.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}