using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;

/// <summary>
/// This script is attached to the limb prefab. It controls the limb's
/// visual state and its physical state (attached, thrown, or pickup).
/// </summary>
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
    [SerializeField] private float pickupDespawnTime = 10f;
    // --- NEW VARIABLE ---
    [Tooltip("Time in seconds after being thrown before the limb can be picked up.")]
    [SerializeField] private float pickupDelay = 1.0f;
    // --- END NEW VARIABLE ---

    // --- State ---
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

    // --- IInteractable Implementation ---
    [Header("Interaction")]
    [Tooltip("The text that will appear on the interaction prompt.")]
    [SerializeField] private string interactionText = "Pick Up Limb";
    public string InteractionPromptText => interactionText;

    public void Interact(PlayerLimbController player)
    {
        // Only allow interaction if this is a usable pickup
        if (CanPickup())
        {
            bool attached = player.TryAttachLimb(limbData, isShowingDamaged);
            if (attached)
            {
                Destroy(gameObject);
            }
        }
    }
    // --- End IInteractable ---

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
        else if (currentState == State.Idle && startingLimbData == null)
        {
            Debug.LogError($"WorldLimb '{gameObject.name}' was placed in the scene but has no 'Starting Limb Data' assigned!", this);
        }
    }

    public void InitializeAttached(LimbData data, bool isDamaged)
    {
        limbData = data;
        currentState = State.Attached;
        isShowingDamaged = isDamaged; 
        
        if (isDamaged)
        {
            if(defaultVisual) defaultVisual.SetActive(false);
            if(damagedVisual) damagedVisual.SetActive(true);
        }
        else
        {
            if(defaultVisual) defaultVisual.SetActive(true);
            if(damagedVisual) damagedVisual.SetActive(false);
        }
        
        if(brokenVisual) brokenVisual.SetActive(false);
        if(shadowGameObject) shadowGameObject.SetActive(false);
        
        col.enabled = false;
        if (rb) rb.bodyType = RigidbodyType2D.Kinematic;

        if (sortingGroup) sortingGroup.enabled = false;
        if (ySorter) ySorter.enabled = false;
    }

    public void InitializeThrow(LimbData data, bool maintained, Vector2 direction)
    {
        limbData = data;
        currentState = State.Thrown;
        isMaintained = maintained;
        isShowingDamaged = isMaintained; 

        transform.SetParent(null);

        if(defaultVisual) defaultVisual.SetActive(false);
        if(damagedVisual) damagedVisual.SetActive(isMaintained); 
        if(brokenVisual) brokenVisual.SetActive(!isMaintained); 
        if(shadowGameObject) shadowGameObject.SetActive(true);

        if (sortingGroup) sortingGroup.enabled = true;
        if (ySorter) ySorter.enabled = true;

        col.enabled = true;
        col.isTrigger = false; // Start as solid (so it can bounce)
        if (rb)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.AddForce(direction * throwForce, ForceMode2D.Impulse); 
            rb.AddTorque(Random.Range(-90f, 90f)); 
        }

        // --- MODIFIED: Use the new variable ---
        StartCoroutine(BecomePickupAfterDelay(pickupDelay)); 
        // --- END MODIFICATION ---
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
            if (startAsDamaged)
            {
                if(damagedVisual) damagedVisual.SetActive(true);
                isShowingDamaged = true; 
            }
            else
            {
                if(defaultVisual) defaultVisual.SetActive(true);
                isShowingDamaged = false; 
            }
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
        col.isTrigger = true; 
        
        if (rb) rb.bodyType = RigidbodyType2D.Kinematic;

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
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0;
        }
        
        col.isTrigger = true;

        if (isMaintained)
        {
            gameObject.tag = "LimbPickup";
            StartCoroutine(DespawnTimer(pickupDespawnTime));
        }
        else
        {
            gameObject.tag = "Untagged";
            StartCoroutine(FadeOutBrokenLimb(2.0f));
        }
    }

    private IEnumerator DespawnTimer(float duration)
    {
        yield return new WaitForSeconds(duration);
        Destroy(gameObject);
    }

    private IEnumerator FadeOutBrokenLimb(float duration)
    {
        yield return new WaitForSeconds(duration); 

        float timer = 0f;
        while (timer < duration)
        {
            float alpha = Mathf.Lerp(1f, 0f, timer / duration);
            foreach (var sr in brokenVisualRenderers)
            {
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