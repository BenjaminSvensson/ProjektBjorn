using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))]
public class LootContainer : MonoBehaviour
{
    [System.Serializable]
    public class LootEntry
    {
        public string name = "Item";
        [Tooltip("Assign the PREFAB here (e.g., 'GoblinArm_Pickup'). For Limbs, ensure the Prefab has WorldLimb attached and LimbData assigned.")]
        public GameObject prefab;
        [Tooltip("Likelihood of this item being chosen relative to others.")]
        public float weight = 10f;
    }

    [Header("Settings")]
    [SerializeField] private float maxHealth = 20f;
    
    [Header("Visuals (GameObjects)")]
    [Tooltip("The GameObject to show when the container is intact.")]
    [SerializeField] private GameObject activeVisual;
    [Tooltip("The GameObject to show when the container is broken/empty.")]
    [SerializeField] private GameObject destroyedVisual;

    [Header("Loot Configuration")]
    [SerializeField] private int minItemsToDrop = 1;
    [SerializeField] private int maxItemsToDrop = 3;
    [SerializeField] private float dropSpreadForce = 5f;
    [Tooltip("Chance (0-1) to drop a single item each time damage is taken (if items are available).")]
    [Range(0f, 1f)]
    [SerializeField] private float dropChanceOnHit = 0.5f;
    [SerializeField] private List<LootEntry> lootTable;

    [Header("Feedback")]
    [SerializeField] private AudioClip[] hitSounds; 
    [SerializeField] private AudioClip breakSound;
    [SerializeField] private AudioClip itemDropSound; 
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.1f;
    [SerializeField] private float hitFlashDuration = 0.1f; 
    [SerializeField] private ParticleSystem hitParticles;

    private float currentHealth;
    private bool isLooted = false;
    private AudioSource audioSource;
    private Transform visualTransform;
    private Vector3 originalPos;
    private bool isShaking = false;
    private Coroutine flashCoroutine; 
    
    // Loot tracking
    private int totalItemsToDrop;
    private int itemsDroppedCount = 0;
    private float totalLootWeight;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        visualTransform = transform;
        currentHealth = maxHealth;
    }

    void Start()
    {
        // Capture position after LevelGenerator has moved it
        originalPos = visualTransform.localPosition;

        // Calculate total items for this instance immediately
        totalItemsToDrop = Random.Range(minItemsToDrop, maxItemsToDrop + 1);

        // Pre-calculate weight
        totalLootWeight = 0f;
        if (lootTable != null)
        {
            foreach (var item in lootTable) totalLootWeight += item.weight;
        }

        // Initialize Visuals
        if (activeVisual) activeVisual.SetActive(true);
        if (destroyedVisual) destroyedVisual.SetActive(false);
    }

    public void TakeDamage(float amount, Vector2 hitDirection)
    {
        if (isLooted) return;

        currentHealth -= amount;

        // --- Feedback ---
        if (audioSource && hitSounds != null && hitSounds.Length > 0)
        {
            audioSource.PlayOneShot(hitSounds[Random.Range(0, hitSounds.Length)]);
        }
        
        if (hitParticles) hitParticles.Play();
        if (!isShaking) StartCoroutine(ShakeRoutine());

        // --- Logic ---
        if (currentHealth <= 0)
        {
            BreakOpen(hitDirection);
        }
        else
        {
            // Flash the broken visual briefly to indicate damage
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashBrokenVisual());

            // Incremental Drop: Try to drop one item if we have any left
            if (itemsDroppedCount < totalItemsToDrop && Random.value <= dropChanceOnHit)
            {
                SpawnSingleItem(hitDirection);
            }
        }
    }

    private void BreakOpen(Vector2 hitDir)
    {
        isLooted = true;

        // Stop any flash coroutine so we don't accidentally revert the visual
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);

        if (audioSource && breakSound) AudioSource.PlayClipAtPoint(breakSound, transform.position);

        // Drop ALL remaining items
        while (itemsDroppedCount < totalItemsToDrop)
        {
            SpawnSingleItem(hitDir);
        }

        // Switch Visuals Permanently
        if (activeVisual) activeVisual.SetActive(false);
        if (destroyedVisual) destroyedVisual.SetActive(true);
        
        // --- CHANGED: Collider remains ENABLED so it acts as a wall/obstacle ---
        // if (GetComponent<Collider2D>()) GetComponent<Collider2D>().enabled = false;
        
        if (hitParticles) hitParticles.Stop();
    }

    private void SpawnSingleItem(Vector2 hitDir)
    {
        if (lootTable == null || lootTable.Count == 0) return;

        GameObject prefabToSpawn = PickWeightedItem();
        
        if (prefabToSpawn != null)
        {
            // Increment tracking
            itemsDroppedCount++;

            // --- SPAWN DIRECTION LOGIC ---
            // Force downward direction (-Y) with random cone spread (+/- 45 degrees)
            float randomAngle = Random.Range(-45f, 45f);
            Vector2 finalDir = Quaternion.Euler(0, 0, randomAngle) * Vector2.down;

            // Spawn slightly in front (down) to avoid clipping behind container
            Vector3 spawnPos = transform.position + new Vector3(0, -0.5f, -0.1f);

            GameObject droppedItem = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            
            // Play Drop Sound
            if (audioSource && itemDropSound) audioSource.PlayOneShot(itemDropSound);

            // Logic to handle physics based on item type
            bool physicsHandled = false;

            // 1. Is it a Limb?
            if (droppedItem.TryGetComponent<WorldLimb>(out WorldLimb limb))
            {
                if (limb.GetLimbData() != null)
                {
                    limb.InitializeThrow(limb.GetLimbData(), true, finalDir);
                    physicsHandled = true;
                }
            }
            // 2. Is it a Weapon?
            else if (droppedItem.TryGetComponent<WeaponPickup>(out WeaponPickup wep))
            {
                wep.InitializeDrop(finalDir);
                physicsHandled = true;
            }
            // 3. Is it a Coin?
            else if (droppedItem.TryGetComponent<CoinPickup>(out CoinPickup coin))
            {
                coin.Initialize();
                physicsHandled = true;
            }

            // 4. Fallback Generic
            if (!physicsHandled && droppedItem.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            {
                rb.AddForce(finalDir * dropSpreadForce, ForceMode2D.Impulse);
            }
        }
    }

    private GameObject PickWeightedItem()
    {
        if (totalLootWeight <= 0) return null;

        float randomValue = Random.Range(0, totalLootWeight);
        float currentWeightSum = 0;

        foreach (var item in lootTable)
        {
            currentWeightSum += item.weight;
            if (randomValue <= currentWeightSum)
            {
                return item.prefab;
            }
        }
        return null;
    }

    private IEnumerator ShakeRoutine()
    {
        isShaking = true;
        float elapsed = 0f;
        
        // Ensure we start from the correct current spot
        originalPos = visualTransform.localPosition; 

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;
            
            visualTransform.localPosition = originalPos + new Vector3(x, y, 0);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        visualTransform.localPosition = originalPos;
        isShaking = false;
    }

    private IEnumerator FlashBrokenVisual()
    {
        if (activeVisual && destroyedVisual)
        {
            activeVisual.SetActive(false);
            destroyedVisual.SetActive(true);

            yield return new WaitForSeconds(hitFlashDuration);

            // Only revert if we haven't been destroyed in the meantime
            if (!isLooted)
            {
                activeVisual.SetActive(true);
                destroyedVisual.SetActive(false);
            }
        }
    }
}