using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Added for filtering enemies

/// <summary>
/// This script goes on the root of EVERY room prefab.
/// It defines the room's doors and environment spawn points.
/// </summary>
public class Room : MonoBehaviour
{
    [Header("Door Configuration")]
    [Tooltip("Check this if this room prefab has a door at the TOP.")]
    public bool hasTopDoor;
    [Tooltip("Check this if this room prefab has a door at the BOTTOM.")]
    public bool hasBottomDoor;
    [Tooltip("Check this if this room prefab has a door at the LEFT.")]
    public bool hasLeftDoor;
    [Tooltip("Check this if this room prefab has a door at the RIGHT.")]
    public bool hasRightDoor;

    [Header("Door Visuals (Walls)")]
    [Tooltip("The 'Wall' GameObject that blocks the TOP door. Will be hidden if a room connects here.")]
    public GameObject wallTop;
    [Tooltip("The 'Wall' GameObject that blocks the BOTTOM door.")]
    public GameObject wallBottom;
    [Tooltip("The 'Wall' GameObject that blocks the LEFT door.")]
    public GameObject wallLeft;
    [Tooltip("The 'Wall' GameObject that blocks the RIGHT door.")]
    public GameObject wallRight;

    [Header("Environment Settings")]
    [Tooltip("Distance from the room edge where props CANNOT spawn. Increase this if props are spawning in walls.")]
    [SerializeField] private float wallPadding = 2.5f;

    [Header("Spawning Rules")] // --- NEW ---
    [Tooltip("If false, no enemies will procedurally spawn here (Use for Boss Rooms or Puzzle Rooms).")]
    public bool allowEnemySpawning = true;

    [HideInInspector]
    public Vector2Int gridPos; // Set by the LevelGenerator

    /// <summary>
    /// Called by the LevelGenerator to open a door (by hiding its wall).
    /// </summary>
    public void OpenDoor(Direction dir)
    {
        switch (dir)
        {
            case Direction.Top:
                if (wallTop) wallTop.SetActive(false);
                break;
            case Direction.Bottom:
                if (wallBottom) wallBottom.SetActive(false);
                break;
            case Direction.Left:
                if (wallLeft) wallLeft.SetActive(false);
                break;
            case Direction.Right:
                if (wallRight) wallRight.SetActive(false);
                break;
        }
    }

    /// <summary>
    /// Called by LevelGenerator to spawn enemies based on budget.
    /// </summary>
    public void SpawnEnemies(List<LevelGenerator.EnemySpawnData> allEnemies, int budget, Vector2 roomSize)
    {
        if (!allowEnemySpawning) return;

        // 1. Calculate boundaries (reuse logic from props)
        float halfWidth = (roomSize.x / 2f) - wallPadding;
        float halfHeight = (roomSize.y / 2f) - wallPadding;

        // 2. Filter enemies:
        //    - Must fit within current remaining budget (initially full budget)
        //    - Must meet minimum distance requirement
        // We actually filter just by MinDistance here, and check budget in the loop.
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

            // Pick a random enemy from the valid list
            var enemyData = validEnemies[Random.Range(0, validEnemies.Count)];

            // Can we afford it?
            if (currentSpent + enemyData.cost <= budget)
            {
                // Spawn it!
                float x = Random.Range(-halfWidth, halfWidth);
                float y = Random.Range(-halfHeight, halfHeight);
                Vector3 spawnPos = new Vector3(x, y, 0);

                if (enemyData.prefab != null)
                {
                    Instantiate(enemyData.prefab, transform.position + spawnPos, Quaternion.identity, transform);
                    currentSpent += enemyData.cost;
                }
            }
            else
            {
                // If we can't afford this one, try to find a cheaper one?
                // For simplicity, we just try again next loop. 
                // If all are too expensive, the loop limit will break us out eventually.
                
                // Optimization: If the cheapest enemy is too expensive, just break immediately.
                int minCost = validEnemies.Min(e => e.cost);
                if (currentSpent + minCost > budget) break;
            }
        }
    }

    /// <summary>
    /// Called by the LevelGenerator to spawn environment props randomly.
    /// </summary>
    public void PopulateRoom(List<LevelGenerator.EnvironmentProp> props, Vector2 roomSize, float density)
    {
        if (props == null || props.Count == 0) return;

        // 1. Calculate how many total spawn attempts to make based on room area
        float roomArea = roomSize.x * roomSize.y;
        int spawnAttempts = Mathf.RoundToInt(roomArea * density);

        // 2. Define the spawn boundaries (in local space)
        float halfWidth = (roomSize.x / 2f) - wallPadding;
        float halfHeight = (roomSize.y / 2f) - wallPadding;

        for (int i = 0; i < spawnAttempts; i++)
        {
            // 3. Pick a random (X, Y) spot inside the room
            float x = Random.Range(-halfWidth, halfWidth);
            float y = Random.Range(-halfHeight, halfHeight);
            Vector3 spawnPos = new Vector3(x, y, 0);

            // 4. Go through the prop list (this respects the list order)
            foreach (var prop in props)
            {
                // 5. Roll the dice for this prop
                if (Random.value <= prop.spawnChance)
                {
                    // 6. Success! Spawn it.
                    GameObject obj = Instantiate(prop.prefab, transform);
                    obj.transform.localPosition = spawnPos;
                    
                    // --- Scale Logic ---
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
                    // ---

                    // 7. IMPORTANT: We break from the *inner* loop.
                    break;
                }
            }
        }
    }
}