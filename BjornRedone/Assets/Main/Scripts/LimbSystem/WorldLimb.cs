using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering; // Required for SortingGroup

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SortingGroup))]
[RequireComponent(typeof(DynamicYSorter))]
[RequireComponent(typeof(Rigidbody2D))]
public class WorldLimb : MonoBehaviour, IInteractable
{
    // --- Global Management (New Feature) ---
    // Tracks loose limbs to enforce the limit
    private static List<WorldLimb> looseLimbs = new List<WorldLimb>();
    private const int MAX_LOOSE_LIMBS = 6; 

    [Header("Scene Pickup Settings (For Prefabs)")]
    [SerializeField] private LimbData startingLimbData;
    [SerializeField] private bool startAsMaintainedPickup = false;
    [SerializeField] private bool startAsDamaged = false;

    [Header("Visual State GameObjects")]
    [SerializeField] private GameObject defaultVisual;
    [SerializeField] private GameObject damagedVisual;
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private GameObject shadowGameObject;

    [Header("Physics Settings")]
    [SerializeField] private float throwForce = 5f;
    [SerializeField] private float pickupDelay = 1.0f;
    [Tooltip("How fast the limb stops sliding (Friction).")]
    [SerializeField] private float groundFriction = 5f; 
    
    [Header("Debris Settings")]
    [Tooltip("How long broken/unusable limbs stay in the world before fading out.")]
    [SerializeField] private float brokenLimbLifetime = 30f; 
    [Tooltip("If the player is farther than this distance, destroy this limb (Optimization).")]
    [SerializeField] private float maxDistanceToPlayer = 40f;

    private enum State { Idle, Attached, Thrown, Pickup }
    private State currentState = State.Idle;
    
    private LimbData limbData;
    private bool isMaintained = false;
    private Rigidbody2D rb;
    private Collider2D col;
    
    private SortingGroup sortingGroup;
    private DynamicYSorter ySorter;

    private bool isShowingDamaged = false;
    private List<SpriteRenderer> brokenVisualRenderers = new List<SpriteRenderer>();

    private Transform playerTransform; 
    private float distanceCheckTimer = 0f;
    private float maxDistanceSq; 

    [Header("Interaction")]
    [SerializeField] private string interactionText = "Pick Up Limb";
    public string InteractionPromptText => interactionText;

    // --- IInteractable Implementation ---
    public void Interact(PlayerLimbController player)
    {
        if (CanPickup())
        {
            bool attached = player.TryAttachLimb(limbData, isShowingDamaged);
            if (attached)
            {
                Destroy(gameObject);
            }
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        
        sortingGroup = GetComponent<SortingGroup>();
        ySorter = GetComponent<DynamicYSorter>();
        
        if (brokenVisual != null)
        {
            brokenVisual.GetComponentsInChildren<SpriteRenderer>(brokenVisualRenderers);
        }

        // Initialize visuals to hidden
        if(defaultVisual) defaultVisual.SetActive(false);
        if(damagedVisual) damagedVisual.SetActive(false);
        if(brokenVisual) brokenVisual.SetActive(false);
        if(shadowGameObject) shadowGameObject.SetActive(false);
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;

        maxDistanceSq = maxDistanceToPlayer * maxDistanceToPlayer;

        // Handle pre-placed scene items
        if (currentState == State.Idle && startingLimbData != null)
        {
            InitializeAsScenePickup(startingLimbData, startAsMaintainedPickup);
            if (startAsDamaged) SetVisualState(true);
        }
    }

    void Update()
    {
        // Periodic distance check for optimization
        distanceCheckTimer += Time.deltaTime;
        if (distanceCheckTimer > 1.0f)
        {
            distanceCheckTimer = 0f;
            CheckDistanceCleanup();
        }
    }

    void OnDestroy()
    {
        // Clean up global list
        if (looseLimbs.Contains(this))
        {
            looseLimbs.Remove(this);
        }
    }

    private void RegisterLooseLimb()
    {
        looseLimbs.Add(this);

        // --- GLOBAL LIMIT LOGIC ---
        // If we exceed the limit, start fading out the oldest limb
        if (looseLimbs.Count > MAX_LOOSE_LIMBS)
        {
            WorldLimb oldest = looseLimbs[0];
            looseLimbs.RemoveAt(0); // Remove from list immediately
            if (oldest != null)
            {
                // Trigger smooth fade out instead of immediate destroy
                oldest.StartLimitFadeOut();
            }
        }
    }

    private void CheckDistanceCleanup()
    {
        if (currentState == State.Attached || currentState == State.Thrown) return;
        if (playerTransform == null) return;

        float distSq = (transform.position - playerTransform.position).sqrMagnitude;
        if (distSq > maxDistanceSq)
        {
            Destroy(gameObject);
        }
    }

    // --- NEW: Limit Fade Out Logic ---
    private void StartLimitFadeOut()
    {
        // Stop any existing coroutines (like delayed pickup activation or natural lifetime fade)
        StopAllCoroutines(); 
        
        // Prevent interaction/pickup while fading
        currentState = State.Idle; 
        if (col) col.enabled = false;
        
        // Start the smooth fade
        StartCoroutine(FadeOutImmediate(1.5f));
    }

    private IEnumerator FadeOutImmediate(float duration)
    {
        float timer = 0f;
        
        // Grab ALL renderers to ensure we fade whatever is currently visible (Broken or Default/Damaged)
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();
        
        // Capture starting alphas to prevent popping if already partially faded
        float[] startAlphas = new float[allRenderers.Length];
        for(int i = 0; i < allRenderers.Length; i++)
        {
            if(allRenderers[i] != null) startAlphas[i] = allRenderers[i].color.a;
        }

        while (timer < duration)
        {
            if (this == null) yield break;

            float progress = timer / duration;
            for(int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null)
                {
                    Color c = allRenderers[i].color;
                    c.a = Mathf.Lerp(startAlphas[i], 0f, progress);
                    allRenderers[i].color = c;
                }
            }
            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    // --- LOGIC: When attached to a body ---
    public void InitializeAttached(LimbData data, bool isDamaged)
    {
        limbData = data;
        currentState = State.Attached;
        SetVisualState(isDamaged);
        
        col.enabled = false;
        if (rb) rb.bodyType = RigidbodyType2D.Kinematic;

        if (sortingGroup) sortingGroup.enabled = false;
        if (ySorter) ySorter.enabled = false;
        this.enabled = false; 

        // Not a loose limb anymore
        if (looseLimbs.Contains(this)) looseLimbs.Remove(this);
    }

    // --- VISUAL LOGIC ---
    public void SetVisualState(bool isDamaged)
    {
        isShowingDamaged = isDamaged;

        // Determine which set of GameObjects to show
        // If attached, picked up (maintained), or thrown (maintained) -> Show Default/Damaged
        // If broken/debris -> Show Broken
        bool showBroken = (currentState == State.Thrown || currentState == State.Pickup) && !isMaintained;

        if (brokenVisual) brokenVisual.SetActive(showBroken);

        if (!showBroken)
        {
            if (isShowingDamaged)
            {
                if(defaultVisual) defaultVisual.SetActive(false);
                if(damagedVisual) damagedVisual.SetActive(true);
            }
            else
            {
                if(defaultVisual) defaultVisual.SetActive(true);
                if(damagedVisual) damagedVisual.SetActive(false);
            }
        }
        else
        {
            if(defaultVisual) defaultVisual.SetActive(false);
            if(damagedVisual) damagedVisual.SetActive(false);
        }
    }

    // --- LOGIC: When thrown ---
    public void InitializeThrow(LimbData data, bool maintained, Vector2 direction, bool isDamaged = false)
    {
        this.enabled = true; 
        limbData = data;
        currentState = State.Thrown;
        isMaintained = maintained;
        isShowingDamaged = isDamaged; 

        transform.SetParent(null);

        SetVisualState(isDamaged);

        if(shadowGameObject) shadowGameObject.SetActive(true);

        if (sortingGroup) sortingGroup.enabled = true;
        if (ySorter) ySorter.enabled = true;

        col.enabled = true;
        col.isTrigger = false; 
        
        if (rb)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f; // Top down
            // Reset damping momentarily for the throw so it flies
            rb.linearDamping = 0f; 
            
            rb.AddForce(direction * throwForce, ForceMode2D.Impulse); 
            rb.AddTorque(Random.Range(-90f, 90f)); 
        }

        // Register for Global Limit
        RegisterLooseLimb();

        StartCoroutine(BecomePickupAfterDelay(pickupDelay)); 
    }

    // --- LOGIC: Scene Startup ---
    public void InitializeAsScenePickup(LimbData data, bool maintained = true)
    {
        limbData = data;
        currentState = State.Pickup;
        isMaintained = maintained;
        
        SetVisualState(startAsDamaged);
        
        if(shadowGameObject) shadowGameObject.SetActive(true);

        if (sortingGroup) sortingGroup.enabled = true;
        if (ySorter) ySorter.enabled = true;

        col.enabled = true;
        col.isTrigger = false; 
        
        if (rb) 
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.linearDamping = groundFriction; 
            rb.angularDamping = 5f;
        }

        if (isMaintained)
        {
            gameObject.tag = "LimbPickup";
        }
        else
        {
            gameObject.tag = "Untagged"; 
            // Register as debris if it started broken in scene
            RegisterLooseLimb();
        }
    }

    private IEnumerator BecomePickupAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        currentState = State.Pickup;
        
        if (rb)
        {
            // Apply friction so it stops sliding
            rb.linearDamping = groundFriction; 
            rb.angularDamping = 5f;
        }
        
        col.isTrigger = false;

        // Optional blood splatter on land
        if (BloodManager.Instance != null && (isShowingDamaged || !isMaintained))
        {
            Vector2 randomDown = Quaternion.Euler(0, 0, Random.Range(-45f, 45f)) * Vector2.down;
            BloodManager.Instance.SpawnBlood(transform.position, randomDown, 0.7f);
        }

        if (isMaintained)
        {
            gameObject.tag = "LimbPickup";
        }
        else
        {
            gameObject.tag = "Untagged";
            // Start fading out if it's just debris
            StartCoroutine(FadeOutBrokenLimb(brokenLimbLifetime));
        }
    }

    private IEnumerator FadeOutBrokenLimb(float duration)
    {
        float fadeTime = 2.0f; 
        float waitTime = Mathf.Max(0, duration - fadeTime);

        yield return new WaitForSeconds(waitTime); 

        float timer = 0f;
        while (timer < fadeTime)
        {
            if (this == null) yield break;

            float alpha = Mathf.Lerp(1f, 0f, timer / fadeTime);
            foreach (var sr in brokenVisualRenderers)
            {
                if (sr != null)
                    sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
            }
            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    public bool CanPickup()
    {
        return (currentState == State.Pickup && isMaintained);
    }

    public bool IsShowingDamaged()
    {
        return isShowingDamaged;
    }

    public LimbData GetLimbData()
    {
        return limbData;
    }
}