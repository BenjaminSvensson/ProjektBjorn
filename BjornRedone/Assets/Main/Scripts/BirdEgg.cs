using UnityEngine;

[RequireComponent(typeof(AudioSource))] // 1. Ensure an AudioSource exists
public class BirdEgg : MonoBehaviour
{
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

    void Start()
    {
        // 2. Auto-assign if empty to prevent crashes
        if (crackSoundSource == null)
        {
            crackSoundSource = GetComponent<AudioSource>();
        }
    }

    public void Initialize(Vector2 targetPos, Vector2 visualStartPos)
    {
        transform.position = targetPos;
        
        Vector3 worldDiff = (Vector3)visualStartPos - (Vector3)targetPos;
        startOffset = worldDiff;
        currentOffset = startOffset;
        
        totalHeight = Mathf.Max(worldDiff.y, 1.0f);
        fallDuration = totalHeight / fallSpeed;

        if (spriteVisual) spriteVisual.localPosition = startOffset;
        
        if (eggAnimator) eggAnimator.Play(fallStateName);
        
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || hasLanded) return;

        timer += Time.deltaTime;
        float ratio = Mathf.Clamp01(timer / fallDuration);

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
        
        if (crackSoundSource != null) 
        {
            crackSoundSource.Play();
        }
        
        if (spriteVisual) spriteVisual.localPosition = Vector3.zero;
        if (shadowVisual)
        {
            shadowVisual.localScale = shadowScaleGround;
            SpriteRenderer sr = shadowVisual.GetComponent<SpriteRenderer>();
            if(sr) { Color c = sr.color; c.a = 0.8f; sr.color = c; }
        }

        if (eggAnimator) eggAnimator.Play(crackStateName);

        Explode();
        Destroy(gameObject, persistDuration);
    }

    void Explode()
    {
        
        if(shadowVisual) shadowVisual.gameObject.SetActive(false);
        
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, damageRadius);
        foreach (var hit in hits)
        {
            PlayerLimbController playerController = hit.GetComponent<PlayerLimbController>();
            if (playerController != null)
            {
                Vector2 dir = (hit.transform.position - transform.position).normalized;
                playerController.TakeDamage(damage, dir);
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}