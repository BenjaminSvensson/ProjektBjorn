using UnityEngine;

public class BloodManager : MonoBehaviour
{
    public static BloodManager Instance { get; private set; }

    [Header("Visuals")]
    [Tooltip("Particle System prefab for the aerial spray. Should emit towards +X.")]
    [SerializeField] private GameObject bloodParticlePrefab;
    
    [Tooltip("List of Prefabs to spawn as droplets. They MUST have the BloodSplat component attached.")]
    [SerializeField] private GameObject[] bloodSplatPrefabs;

    [Header("Settings")]
    [Tooltip("Adjust if your particle prefab faces Up (90) or Left (180).")]
    [SerializeField] private float rotationOffset = 0f; 
    [SerializeField] private float particleLifetime = 2f;

    [Header("Juice Settings")]
    [Tooltip("How many droplets to spawn per hit.")]
    [SerializeField] private int dropletsPerHit = 5;
    [Tooltip("How fast the droplets fly initially.")]
    [SerializeField] private float dropletSpeed = 4f;
    [Tooltip("How wide the shotgun spread is (in degrees).")]
    [SerializeField] private float spreadAngle = 45f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Spawns directional blood spray and a cluster of ground splats.
    /// </summary>
    public void SpawnBlood(Vector2 position, Vector2 hitDirection, float intensity = 1f)
    {
        // 1. Directional Particle Spray (The "Mist")
        if (bloodParticlePrefab != null)
        {
            GameObject particles = Instantiate(bloodParticlePrefab, position, bloodParticlePrefab.transform.rotation);
            float angle = Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg;
            particles.transform.Rotate(Vector3.forward, angle + rotationOffset, Space.World);
            Destroy(particles, particleLifetime);
        }

        // 2. Ground Splat Cluster (The "Puddles")
        if (bloodSplatPrefabs != null && bloodSplatPrefabs.Length > 0)
        {
            int count = Mathf.CeilToInt(dropletsPerHit * intensity);

            for (int i = 0; i < count; i++)
            {
                SpawnSingleDroplet(position, hitDirection, intensity);
            }
        }
    }

    private void SpawnSingleDroplet(Vector2 origin, Vector2 mainDirection, float intensity)
    {
        // Pick a random prefab from the list
        GameObject prefabToSpawn = bloodSplatPrefabs[Random.Range(0, bloodSplatPrefabs.Length)];
        
        GameObject splatObj = Instantiate(prefabToSpawn, origin, Quaternion.identity);
        BloodSplat splatScript = splatObj.GetComponent<BloodSplat>();

        // Calculate random spread direction
        float randomAngle = Random.Range(-spreadAngle / 2f, spreadAngle / 2f);
        Vector2 spreadDir = Quaternion.Euler(0, 0, randomAngle) * mainDirection;

        // Randomize speed (some fly far, some drop close)
        float speed = dropletSpeed * Random.Range(0.5f, 1.5f);
        
        // Initialize the physics
        if (splatScript != null)
        {
            splatScript.Initialize(spreadDir * speed, intensity);
        }
        else
        {
            Debug.LogWarning($"Blood prefab '{prefabToSpawn.name}' is missing the BloodSplat script!");
        }
    }
}