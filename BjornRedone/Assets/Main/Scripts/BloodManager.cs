using UnityEngine;

public class BloodManager : MonoBehaviour
{
    public static BloodManager Instance { get; private set; }

    [Header("Visuals")]
    [Tooltip("Particle System prefab. IMPORTANT: Ensure particles fly towards the RIGHT (+X) axis by default.")]
    [SerializeField] private GameObject bloodParticlePrefab;
    [Tooltip("List of Sprite prefabs for blood puddles on the ground.")]
    [SerializeField] private GameObject[] bloodSplatPrefabs;

    [Header("Settings")]
    [Tooltip("Add degrees here if your prefab shoots Up (90) or Left (180).")]
    [SerializeField] private float rotationOffset = 0f; 
    [SerializeField] private float splatLifetime = 15f;
    [SerializeField] private float particleLifetime = 2f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Spawns directional blood spray and a ground splat.
    /// </summary>
    public void SpawnBlood(Vector2 position, Vector2 hitDirection, float sizeMultiplier = 1f)
    {
        // 1. Directional Particle Spray (The "Juice")
        if (bloodParticlePrefab != null)
        {
            // Use the prefab's original rotation as a base (preserves -90 X rotation if it exists)
            GameObject particles = Instantiate(bloodParticlePrefab, position, bloodParticlePrefab.transform.rotation);
            
            // Calculate the angle for the hit direction
            float angle = Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg;
            
            // Apply the angle + offset. We rotate around World Z to keep it flat on the 2D plane.
            particles.transform.Rotate(Vector3.forward, angle + rotationOffset, Space.World);

            Destroy(particles, particleLifetime);
        }

        // 2. Ground Splat (The "Spread")
        if (bloodSplatPrefabs != null && bloodSplatPrefabs.Length > 0)
        {
            GameObject prefab = bloodSplatPrefabs[Random.Range(0, bloodSplatPrefabs.Length)];
            
            // Random rotation for variety
            Quaternion randomRot = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            
            GameObject splat = Instantiate(prefab, position, randomRot);
            
            float scale = Random.Range(0.8f, 1.2f) * sizeMultiplier;
            splat.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer sr = splat.GetComponent<SpriteRenderer>();
            if (sr) 
            {
                sr.sortingOrder = -50; 
                StartCoroutine(FadeAndDestroy(sr, splatLifetime));
            }
            else
            {
                Destroy(splat, splatLifetime);
            }
        }
    }

    private System.Collections.IEnumerator FadeAndDestroy(SpriteRenderer sr, float time)
    {
        yield return new WaitForSeconds(time * 0.7f); // Wait 70% of lifetime
        
        float fadeDuration = time * 0.3f;
        float timer = 0f;
        Color startColor = sr.color;

        while (timer < fadeDuration)
        {
            if (sr == null) yield break; // Safety check
            float alpha = Mathf.Lerp(startColor.a, 0f, timer / fadeDuration);
            sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            timer += Time.deltaTime;
            yield return null;
        }
        
        if (sr != null) Destroy(sr.gameObject);
    }
}