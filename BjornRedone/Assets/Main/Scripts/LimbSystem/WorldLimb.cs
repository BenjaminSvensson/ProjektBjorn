using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SortingGroup))]
[RequireComponent(typeof(DynamicYSorter))]
[RequireComponent(typeof(Rigidbody2D))]
public class WorldLimb : MonoBehaviour, IInteractable
{
    [Header("Scene Pickup Settings (For Prefabs)")]
    [SerializeField] private LimbData startingLimbData;
    [SerializeField] private bool startAsMaintainedPickup = false;
    [SerializeField] private bool startAsDamaged = false;

    [Header("Visual State GameObjects (Assign in Prefab)")]
    [SerializeField] private GameObject defaultVisual;
    [SerializeField] private GameObject damagedVisual;
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private GameObject shadowGameObject;

    [Header("Physics Settings")]
    [SerializeField] private float throwForce = 5f;
    [SerializeField] private float pickupDelay = 1.0f;
    
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
    
    // --- OPTIMIZATION: Store squared distance to avoid Sqrt operations ---
    private float maxDistanceSq; 

    [Header("Interaction")]
    [Tooltip("The text that will appear on the interaction prompt.")]
    [SerializeField] private string interactionText = "Pick Up Limb";
    public string InteractionPromptText => interactionText;

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

        if(defaultVisual) defaultVisual.SetActive(false);
        if(damagedVisual) damagedVisual.SetActive(false);
        if(brokenVisual) brokenVisual.SetActive(false);
        if(shadowGameObject) shadowGameObject.SetActive(false);
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;

        // --- OPTIMIZATION: Calculate Square once ---
        maxDistanceSq = maxDistanceToPlayer * maxDistanceToPlayer;

        if (currentState == State.Idle && startingLimbData != null)
        {
            if (startAsMaintainedPickup)
            {
                InitializeAsScenePickup(startingLimbData, true);
            }
            else
            {
                InitializeAsScenePickup(startingLimbData, false);
            }
        }
    }

    void Update()
    {
        // Check once every second to save performance
        distanceCheckTimer += Time.deltaTime;
        if (distanceCheckTimer > 1.0f)
        {
            distanceCheckTimer = 0f;
            CheckDistanceCleanup();
        }
    }

    private void CheckDistanceCleanup()
    {
        if (currentState == State.Attached || currentState == State.Thrown) return;
        if (playerTransform == null) return;

        // --- OPTIMIZATION: Use SqrMagnitude instead of Distance ---
        // Vector2.Distance uses SquareRoot, which is expensive for hundreds of objects.
        // SqrMagnitude is just simple multiplication.
        float distSq = (transform.position - playerTransform.position).sqrMagnitude;
        
        if (distSq > maxDistanceSq)
        {
            Destroy(gameObject);
        }
    }

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
    }

    public void SetVisualState(bool isDamaged)
    {
        isShowingDamaged = isDamaged;

        if (currentState == State.Attached || (currentState == State.Pickup && isMaintained) || (currentState == State.Thrown && isMaintained))
        {
            if (brokenVisual) brokenVisual.SetActive(false);
            
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
    }

    public void InitializeThrow(LimbData data, bool maintained, Vector2 direction, bool isDamaged = false)
    {
        this.enabled = true; 
        limbData = data;
        currentState = State.Thrown;
        isMaintained = maintained;
        isShowingDamaged = isDamaged; 

        transform.SetParent(null);

        if(defaultVisual) defaultVisual.SetActive(false);
        if(damagedVisual) damagedVisual.SetActive(false);
        if(brokenVisual) brokenVisual.SetActive(false);

        if (isMaintained)
        {
            SetVisualState(isDamaged); 
        }
        else
        {
            if(brokenVisual) brokenVisual.SetActive(true);
        }

        if(shadowGameObject) shadowGameObject.SetActive(true);

        if (sortingGroup) sortingGroup.enabled = true;
        if (ySorter) ySorter.enabled = true;

        col.enabled = true;
        col.isTrigger = false; 
        if (rb)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.AddForce(direction * throwForce, ForceMode2D.Impulse); 
            rb.AddTorque(Random.Range(-90f, 90f)); 
        }

        StartCoroutine(BecomePickupAfterDelay(pickupDelay)); 
    }

    public void InitializeAsScenePickup(LimbData data, bool maintained = true)
    {
        limbData = data;
        currentState = State.Pickup;
        isMaintained = maintained;
        
        if(defaultVisual) defaultVisual.SetActive(false);
        if(damagedVisual) damagedVisual.SetActive(false);
        if(brokenVisual) brokenVisual.SetActive(false);

        if (isMaintained)
        {
            SetVisualState(startAsDamaged);
        }
        else
        {
            if(brokenVisual) brokenVisual.SetActive(true);
            isShowingDamaged = false; 
        }
        
        if(shadowGameObject) shadowGameObject.SetActive(true);

        if (sortingGroup) sortingGroup.enabled = true;
        if (ySorter) ySorter.enabled = true;

        col.enabled = true;
        col.isTrigger = false; 
        
        if (rb) 
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.linearDamping = 10f; 
            rb.angularDamping = 10f;
        }

        if (isMaintained)
        {
            gameObject.tag = "LimbPickup";
        }
        else
        {
            gameObject.tag = "Untagged"; 
        }
    }

    private IEnumerator BecomePickupAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        currentState = State.Pickup;
        
        if (rb)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.linearDamping = 10f; 
            rb.angularDamping = 10f;
        }
        
        col.isTrigger = false;

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