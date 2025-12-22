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
    // --- Global Management ---
    private static List<WorldLimb> looseLimbs = new List<WorldLimb>();
    private const int MAX_LOOSE_LIMBS = 6; 

    [Header("Scene Pickup Settings")]
    [SerializeField] private LimbData startingLimbData;
    [SerializeField] private bool startAsMaintainedPickup = false;
    [SerializeField] private bool startAsDamaged = false;

    [Header("Visual State")]
    [SerializeField] private GameObject defaultVisual;
    [SerializeField] private GameObject damagedVisual;
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private GameObject shadowGameObject;

    [Header("Physics")]
    [SerializeField] private float throwForce = 5f;
    [SerializeField] private float pickupDelay = 1.0f;
    [SerializeField] private float groundFriction = 5f; 
    
    [Header("Debris")]
    [SerializeField] private float brokenLimbLifetime = 30f; 
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

    // --- IInteractable Implementation (UPDATED) ---
    
    public string GetInteractionPrompt()
    {
        return interactionText;
    }

    public Transform GetTransform()
    {
        return transform;
    }

    public void Interact(GameObject interactor)
    {
        // Check if the thing trying to interact is the Player with a LimbController
        PlayerLimbController player = interactor.GetComponent<PlayerLimbController>();

        if (player != null && CanPickup())
        {
            bool attached = player.TryAttachLimb(limbData, isShowingDamaged);
            if (attached)
            {
                Destroy(gameObject);
            }
        }
    }

    // --- End Interface Implementation ---

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sortingGroup = GetComponent<SortingGroup>();
        ySorter = GetComponent<DynamicYSorter>();
        
        if (brokenVisual != null) brokenVisual.GetComponentsInChildren<SpriteRenderer>(brokenVisualRenderers);

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

        if (currentState == State.Idle && startingLimbData != null)
        {
            InitializeAsScenePickup(startingLimbData, startAsMaintainedPickup);
            if (startAsDamaged) SetVisualState(true);
        }
    }

    void Update()
    {
        distanceCheckTimer += Time.deltaTime;
        if (distanceCheckTimer > 1.0f)
        {
            distanceCheckTimer = 0f;
            CheckDistanceCleanup();
        }
    }

    void OnDestroy()
    {
        if (looseLimbs.Contains(this)) looseLimbs.Remove(this);
    }

    private void RegisterLooseLimb()
    {
        looseLimbs.Add(this);
        if (looseLimbs.Count > MAX_LOOSE_LIMBS)
        {
            WorldLimb oldest = looseLimbs[0];
            looseLimbs.RemoveAt(0);
            if (oldest != null) oldest.StartLimitFadeOut();
        }
    }

    private void CheckDistanceCleanup()
    {
        if (currentState == State.Attached || currentState == State.Thrown) return;
        if (playerTransform == null) return;
        if ((transform.position - playerTransform.position).sqrMagnitude > maxDistanceSq) Destroy(gameObject);
    }

    private void StartLimitFadeOut()
    {
        StopAllCoroutines(); 
        currentState = State.Idle; 
        if (col) col.enabled = false;
        StartCoroutine(FadeOutImmediate(1.5f));
    }

    private IEnumerator FadeOutImmediate(float duration)
    {
        float timer = 0f;
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();
        float[] startAlphas = new float[allRenderers.Length];
        for(int i = 0; i < allRenderers.Length; i++) if(allRenderers[i] != null) startAlphas[i] = allRenderers[i].color.a;

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
        if (looseLimbs.Contains(this)) looseLimbs.Remove(this);
    }

    public void SetVisualState(bool isDamaged)
    {
        isShowingDamaged = isDamaged;
        bool showBroken = (currentState == State.Thrown || currentState == State.Pickup) && !isMaintained;

        if (brokenVisual) brokenVisual.SetActive(showBroken);

        if (!showBroken)
        {
            if(defaultVisual) defaultVisual.SetActive(!isShowingDamaged);
            if(damagedVisual) damagedVisual.SetActive(isShowingDamaged);
        }
        else
        {
            if(defaultVisual) defaultVisual.SetActive(false);
            if(damagedVisual) damagedVisual.SetActive(false);
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
        SetVisualState(isDamaged);
        if(shadowGameObject) shadowGameObject.SetActive(true);
        if (sortingGroup) sortingGroup.enabled = true;
        if (ySorter) ySorter.enabled = true;

        col.enabled = true;
        col.isTrigger = false; 
        
        if (rb)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.linearDamping = 0f; 
            rb.AddForce(direction * throwForce, ForceMode2D.Impulse); 
            rb.AddTorque(Random.Range(-90f, 90f)); 
        }
        RegisterLooseLimb();
        StartCoroutine(BecomePickupAfterDelay(pickupDelay)); 
    }

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

        if (isMaintained) gameObject.tag = "LimbPickup";
        else 
        {
            gameObject.tag = "Untagged"; 
            RegisterLooseLimb();
        }
    }

    private IEnumerator BecomePickupAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        currentState = State.Pickup;
        if (rb) { rb.linearDamping = groundFriction; rb.angularDamping = 5f; }
        col.isTrigger = false;

        if (BloodManager.Instance != null && (isShowingDamaged || !isMaintained))
        {
            Vector2 randomDown = Quaternion.Euler(0, 0, Random.Range(-45f, 45f)) * Vector2.down;
            BloodManager.Instance.SpawnBlood(transform.position, randomDown, 0.7f);
        }

        if (isMaintained) gameObject.tag = "LimbPickup";
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
            foreach (var sr in brokenVisualRenderers) if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
            timer += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }

    public bool CanPickup() { return (currentState == State.Pickup && isMaintained); }
    public bool IsShowingDamaged() { return isShowingDamaged; }
    public LimbData GetLimbData() { return limbData; }
}