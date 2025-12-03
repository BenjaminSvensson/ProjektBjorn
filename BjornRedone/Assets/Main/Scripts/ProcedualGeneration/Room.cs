using UnityEngine;
using System.Collections.Generic;

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
    /// Called by the LevelGenerator to spawn environment props randomly.
    /// </summary>
    /// <param name="props">The list of possible props to spawn.</param>
    /// <param name="roomSize">The (X, Y) size of the room.</param>
    /// <param name="density">The number of spawn attempts per square unit.</param>
    public void PopulateRoom(List<LevelGenerator.EnvironmentProp> props, Vector2 roomSize, float density)
    {
        if (props == null || props.Count == 0) return;

        // 1. Calculate how many total spawn attempts to make based on room area
        float roomArea = roomSize.x * roomSize.y;
        int spawnAttempts = Mathf.RoundToInt(roomArea * density);

        // 2. Define the spawn boundaries (in local space)
        // --- FIXED: Use the customized wallPadding variable ---
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
                    
                    // --- MODIFIED: This now RESPECTS original prefab scale ---
                    
                    // 1. Get the prefab's original scale
                    Vector3 originalScale = obj.transform.localScale;

                    // 2. Get the random new scale *multiplier*
                    float scaleMultiplier = Random.Range(prop.minScale, prop.maxScale);
                    
                    // 3. Get the flip multiplier
                    float flipMultiplier = 1f;
                    if (prop.allowRandomFlip && Random.value < 0.5f)
                    {
                        flipMultiplier = -1f;
                    }

                    // 4. Apply multipliers to the *original* scale
                    obj.transform.localScale = new Vector3(
                        originalScale.x * scaleMultiplier * flipMultiplier,
                        originalScale.y * scaleMultiplier,
                        originalScale.z // Preserve original Z scale
                    );
                    // --- END MODIFICATION ---

                    // 7. IMPORTANT: We break from the *inner* loop.
                    // This means we only spawn ONE prop per random spot.
                    break;
                }
            }
        }
    }
}