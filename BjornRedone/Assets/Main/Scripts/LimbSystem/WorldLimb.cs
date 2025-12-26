using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;

// Fixed: Split RequireComponent into multiple attributes to avoid constructor error
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SortingGroup))]
[RequireComponent(typeof(DynamicYSorter))]
[RequireComponent(typeof(Rigidbody2D))]
public class WorldLimb : MonoBehaviour, IInteractable
{
    private static List<WorldLimb> looseLimbs = new List<WorldLimb>();
    private const int MAX_LOOSE_LIMBS = 6; 

    [Header("Scene Pickup Settings")]
    [SerializeField] private LimbData startingLimbData;
    [SerializeField] private bool startAsMaintainedPickup = false, startAsDamaged = false;

    [Header("Visual State GameObjects")]
    [SerializeField] private GameObject defaultVisual, damagedVisual, brokenVisual, shadowGameObject;

    [Header("Physics Settings")]
    [SerializeField] private float throwForce = 5f, pickupDelay = 1.0f, groundFriction = 5f; 
    
    [Header("Debris Settings")]
    [SerializeField] private float brokenLimbLifetime = 30f, maxDistanceToPlayer = 40f;

    private enum State { Idle, Attached, Thrown, Pickup }
    private State currentState = State.Idle;
    private LimbData limbData;
    private bool isMaintained = false, isShowingDamaged = false;
    private Rigidbody2D rb;
    private Collider2D col;
    private SortingGroup sortingGroup;
    private DynamicYSorter ySorter;
    private List<SpriteRenderer> brokenVisualRenderers = new List<SpriteRenderer>();
    private Transform playerTransform; 
    private float distanceCheckTimer = 0f, maxDistanceSq; 

    [Header("Interaction")]
    [SerializeField] private string interactionText = "Pick Up Limb";
    public string InteractionPromptText => interactionText;

    public void Interact(PlayerLimbController player)
    {
        if (CanPickup() && player.TryAttachLimb(limbData, isShowingDamaged)) Destroy(gameObject);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); col = GetComponent<Collider2D>();
        sortingGroup = GetComponent<SortingGroup>(); ySorter = GetComponent<DynamicYSorter>();
        if (brokenVisual) brokenVisual.GetComponentsInChildren<SpriteRenderer>(brokenVisualRenderers);
        defaultVisual?.SetActive(false); damagedVisual?.SetActive(false); brokenVisual?.SetActive(false); shadowGameObject?.SetActive(false);
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) playerTransform = p.transform;
        maxDistanceSq = maxDistanceToPlayer * maxDistanceToPlayer;
        if (currentState == State.Idle && startingLimbData)
        {
            InitializeAsScenePickup(startingLimbData, startAsMaintainedPickup);
            if (startAsDamaged) SetVisualState(true);
        }
    }

    void Update()
    {
        distanceCheckTimer += Time.deltaTime;
        if (distanceCheckTimer > 1.0f) { distanceCheckTimer = 0f; CheckDistanceCleanup(); }
    }

    void OnDestroy() { if (looseLimbs.Contains(this)) looseLimbs.Remove(this); }

    private void RegisterLooseLimb()
    {
        looseLimbs.Add(this);
        if (looseLimbs.Count > MAX_LOOSE_LIMBS) { WorldLimb oldest = looseLimbs[0]; looseLimbs.RemoveAt(0); oldest?.StartLimitFadeOut(); }
    }

    private void CheckDistanceCleanup()
    {
        if (currentState == State.Attached || currentState == State.Thrown || !playerTransform) return;
        if ((transform.position - playerTransform.position).sqrMagnitude > maxDistanceSq) Destroy(gameObject);
    }

    private void StartLimitFadeOut() { StopAllCoroutines(); currentState = State.Idle; if (col) col.enabled = false; StartCoroutine(FadeOutImmediate(1.5f)); }

    private IEnumerator FadeOutImmediate(float duration)
    {
        float timer = 0f;
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();
        float[] alphas = new float[srs.Length];
        for(int i = 0; i < srs.Length; i++) if(srs[i]) alphas[i] = srs[i].color.a;

        while (timer < duration)
        {
            if (!this) yield break;
            for(int i = 0; i < srs.Length; i++) if (srs[i]) { Color c = srs[i].color; c.a = Mathf.Lerp(alphas[i], 0f, timer / duration); srs[i].color = c; }
            timer += Time.deltaTime; yield return null;
        }
        Destroy(gameObject);
    }

    public void InitializeAttached(LimbData data, bool damaged)
    {
        limbData = data; currentState = State.Attached; SetVisualState(damaged);
        col.enabled = false; if (rb) rb.bodyType = RigidbodyType2D.Kinematic;
        if (sortingGroup) sortingGroup.enabled = false; if (ySorter) ySorter.enabled = false;
        this.enabled = false; if (looseLimbs.Contains(this)) looseLimbs.Remove(this);
    }

    public void SetVisualState(bool damaged)
    {
        isShowingDamaged = damaged;
        bool broken = (currentState == State.Thrown || currentState == State.Pickup) && !isMaintained;
        brokenVisual?.SetActive(broken);
        if (!broken) { defaultVisual?.SetActive(!isShowingDamaged); damagedVisual?.SetActive(isShowingDamaged); }
        else { defaultVisual?.SetActive(false); damagedVisual?.SetActive(false); }
    }

    public void InitializeThrow(LimbData data, bool maintained, Vector2 direction, bool damaged = false)
    {
        this.enabled = true; limbData = data; currentState = State.Thrown; isMaintained = maintained; isShowingDamaged = damaged; 
        transform.SetParent(null); SetVisualState(damaged); shadowGameObject?.SetActive(true);
        if (sortingGroup) sortingGroup.enabled = true; if (ySorter) ySorter.enabled = true;
        col.enabled = true; col.isTrigger = false; 
        if (rb) { rb.bodyType = RigidbodyType2D.Dynamic; rb.gravityScale = 0f; rb.linearDamping = 0f; rb.AddForce(direction * throwForce, ForceMode2D.Impulse); rb.AddTorque(Random.Range(-90f, 90f)); }
        RegisterLooseLimb(); StartCoroutine(BecomePickupAfterDelay(pickupDelay)); 
    }

    public void InitializeAsScenePickup(LimbData data, bool maintained = true)
    {
        limbData = data; currentState = State.Pickup; isMaintained = maintained;
        SetVisualState(startAsDamaged); shadowGameObject?.SetActive(true);
        if (sortingGroup) sortingGroup.enabled = true; if (ySorter) ySorter.enabled = true;
        col.enabled = true; col.isTrigger = false; 
        if (rb) { rb.bodyType = RigidbodyType2D.Dynamic; rb.gravityScale = 0f; rb.linearDamping = groundFriction; rb.angularDamping = 5f; }
        if (isMaintained) gameObject.tag = "LimbPickup"; else { gameObject.tag = "Untagged"; RegisterLooseLimb(); }
    }

    private IEnumerator BecomePickupAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        currentState = State.Pickup;
        if (rb) { rb.linearDamping = groundFriction; rb.angularDamping = 5f; }
        
        // --- NEW: If broken (not maintained), becomes a Trigger (walk-through) ---
        col.isTrigger = !isMaintained; 

        if (BloodManager.Instance && (isShowingDamaged || !isMaintained)) BloodManager.Instance.SpawnBlood(transform.position, Quaternion.Euler(0, 0, Random.Range(-45f, 45f)) * Vector2.down, 0.7f);
        if (isMaintained) gameObject.tag = "LimbPickup"; else { gameObject.tag = "Untagged"; StartCoroutine(FadeOutBrokenLimb(brokenLimbLifetime)); }
    }

    private IEnumerator FadeOutBrokenLimb(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0, duration - 2.0f)); 
        float t = 0f;
        while (t < 2.0f) {
            if (!this) yield break;
            foreach (var sr in brokenVisualRenderers) if (sr) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Lerp(1f, 0f, t / 2.0f));
            t += Time.deltaTime; yield return null;
        }
        Destroy(gameObject);
    }

    public bool CanPickup() => currentState == State.Pickup && isMaintained;
    public bool IsShowingDamaged() => isShowingDamaged;
    public LimbData GetLimbData() => limbData;
}