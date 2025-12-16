using UnityEngine;
using System.Collections.Generic;
using System.Linq; 

public class Room : MonoBehaviour
{
    [Header("Door Configuration")]
    public bool hasTopDoor;
    public bool hasBottomDoor;
    public bool hasLeftDoor;
    public bool hasRightDoor;

    [Header("Door Visuals (Walls)")]
    public GameObject wallTop;
    public GameObject wallBottom;
    public GameObject wallLeft;
    public GameObject wallRight;

    [Header("Environment Settings")]
    [Tooltip("Distance from the room edge where props CANNOT spawn.")]
    [SerializeField] private float wallPadding = 2.5f;
    [Tooltip("Minimum distance between any two spawned objects (props or enemies).")]
    [SerializeField] private float minSpawnSpacing = 1.5f; // --- NEW ---

    [Header("Spawning Rules")]
    [Tooltip("If false, no enemies will procedurally spawn here.")]
    public bool allowEnemySpawning = true;

    [HideInInspector]
    public Vector2Int gridPos; 

    // Track local positions of everything spawned in this room
    private List<Vector3> spawnedObjectPositions = new List<Vector3>();

    public void OpenDoor(Direction dir)
    {
        switch (dir)
        {
            case Direction.Top: if (wallTop) wallTop.SetActive(false); break;
            case Direction.Bottom: if (wallBottom) wallBottom.SetActive(false); break;
            case Direction.Left: if (wallLeft) wallLeft.SetActive(false); break;
            case Direction.Right: if (wallRight) wallRight.SetActive(false); break;
        }
    }

    public void SpawnEnemies(List<LevelGenerator.EnemySpawnData> allEnemies, int budget, Vector2 roomSize)
    {
        if (!allowEnemySpawning) return;

        float halfWidth = (roomSize.x / 2f) - wallPadding;
        float halfHeight = (roomSize.y / 2f) - wallPadding;

        int distFromStart = Mathf.Abs(gridPos.x) + Mathf.Abs(gridPos.y);
        
        List<LevelGenerator.EnemySpawnData> validEnemies = allEnemies
            .Where(e => e.minDistanceReq <= distFromStart)
            .ToList();

        if (validEnemies.Count == 0) return;

        int currentSpent = 0;
        int safetyLoop = 0;

        while (currentSpent < budget && safetyLoop < 50)
        {
            safetyLoop++;

            var enemyData = validEnemies[Random.Range(0, validEnemies.Count)];

            if (currentSpent + enemyData.cost <= budget)
            {
                // Try 10 times to find a valid spot
                Vector3 spawnPos = Vector3.zero;
                bool validSpot = false;

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    float x = Random.Range(-halfWidth, halfWidth);
                    float y = Random.Range(-halfHeight, halfHeight);
                    spawnPos = new Vector3(x, y, 0);

                    if (!IsTooClose(spawnPos))
                    {
                        validSpot = true;
                        break;
                    }
                }

                if (validSpot)
                {
                    if (enemyData.prefab != null)
                    {
                        Instantiate(enemyData.prefab, transform.position + spawnPos, Quaternion.identity, transform);
                        currentSpent += enemyData.cost;
                        
                        // Register this spot so future enemies don't overlap
                        spawnedObjectPositions.Add(spawnPos);
                    }
                }
            }
            else
            {
                int minCost = validEnemies.Min(e => e.cost);
                if (currentSpent + minCost > budget) break;
            }
        }
    }

    public void PopulateRoom(List<LevelGenerator.EnvironmentProp> props, Vector2 roomSize, float density)
    {
        if (props == null || props.Count == 0) return;

        float roomArea = roomSize.x * roomSize.y;
        int spawnAttempts = Mathf.RoundToInt(roomArea * density);

        float halfWidth = (roomSize.x / 2f) - wallPadding;
        float halfHeight = (roomSize.y / 2f) - wallPadding;

        // Clear list for new population (just in case)
        spawnedObjectPositions.Clear();

        for (int i = 0; i < spawnAttempts; i++)
        {
            float x = Random.Range(-halfWidth, halfWidth);
            float y = Random.Range(-halfHeight, halfHeight);
            Vector3 spawnPos = new Vector3(x, y, 0);

            // Check if this spot is too close to existing props
            if (IsTooClose(spawnPos)) continue;

            foreach (var prop in props)
            {
                if (Random.value <= prop.spawnChance)
                {
                    GameObject obj = Instantiate(prop.prefab, transform);
                    obj.transform.localPosition = spawnPos;
                    
                    Vector3 originalScale = obj.transform.localScale;
                    float scaleMultiplier = Random.Range(prop.minScale, prop.maxScale);
                    float flipMultiplier = 1f;
                    if (prop.allowRandomFlip && Random.value < 0.5f)
                    {
                        flipMultiplier = -1f;
                    }

                    obj.transform.localScale = new Vector3(
                        originalScale.x * scaleMultiplier * flipMultiplier,
                        originalScale.y * scaleMultiplier,
                        originalScale.z 
                    );

                    // Register this position so future props (and enemies) avoid it
                    spawnedObjectPositions.Add(spawnPos);
                    break;
                }
            }
        }
    }

    // Helper to check distance against all previously spawned objects
    private bool IsTooClose(Vector3 pos)
    {
        foreach (Vector3 occupied in spawnedObjectPositions)
        {
            if (Vector3.Distance(pos, occupied) < minSpawnSpacing)
            {
                return true;
            }
        }
        return false;
    }
}