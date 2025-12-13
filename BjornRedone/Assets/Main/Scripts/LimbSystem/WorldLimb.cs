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
    
    [Tooltip("Time in seconds after being thrown before the limb can be picked up.")]
    [SerializeField] private float pickupDelay = 1.0f;
    
    [Header("Debris Settings")]
    [Tooltip("How long broken/unusable limbs stay in the world before fading out.")]
    [SerializeField] private float brokenLimbLifetime = 30f; 

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

        // Hide all initially
        if(defaultVisual) defaultVisual.SetActive(false);
        if(damagedVisual) damagedVisual.SetActive(false);
        if(brokenVisual) brokenVisual.SetActive(false);
        if(shadowGameObject) shadowGameObject.SetActive(false);
    }

    void Start()
    {
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

    public void InitializeAttached(LimbData data, bool isDamaged)
    {
        limbData = data;
        currentState = State.Attached;
        SetVisualState(isDamaged);
        
        col.enabled = false;
        if (rb) rb.bodyType = RigidbodyType2D.Kinematic;

        if (sortingGroup) sortingGroup.enabled = false;
        if (ySorter) ySorter.enabled = false;
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
        // --- CHANGED: IsTrigger false so player can push it ---
        col.isTrigger = false; 
        
        if (rb) 
        {
            // --- CHANGED: Dynamic so it has physics ---
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.linearDamping = 10f; // CORRECTED: linearDrag -> drag
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
            // --- CHANGED: Stay Dynamic but add drag to stop sliding ---
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.linearDamping = 10f; // CORRECTED: linearDrag -> drag
            rb.angularDamping = 10f;
        }
        
        // --- CHANGED: Solid collider ---
        col.isTrigger = false;

        // Splat on land
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