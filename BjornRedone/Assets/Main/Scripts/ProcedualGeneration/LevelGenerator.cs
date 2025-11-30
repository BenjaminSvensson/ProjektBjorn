using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Used for ordering
using UnityEngine.InputSystem; // --- NEW: Added for the new Input System ---

// Enum to define door directions.
public enum Direction { Top, Bottom, Left, Right }

public class LevelGenerator : MonoBehaviour
{
    // --- MODIFIED: Added new fields for variation ---
    [System.Serializable]
    public class EnvironmentProp
    {
        public GameObject prefab;
        [Tooltip("The chance (0.0 to 1.0) this prop will spawn at a given point.")]
        [Range(0f, 1f)]
        public float spawnChance = 0.5f;

        [Header("Variation")]
        [Tooltip("If true, the prop has a 50% chance to be flipped horizontally.")]
        public bool allowRandomFlip = true;
        [Tooltip("The minimum random scale to apply (1.0 = no change).")]
        [Range(0.5f, 1.5f)]
        public float minScale = 0.9f;
        [Tooltip("The maximum random scale to apply (1.0 = no change).")]
        [Range(0.5f, 1.5f)]
        public float maxScale = 1.1f;
    }
    // --- END MODIFICATION ---

    [Header("Generation Settings")]
    [Tooltip("The total number of rooms to generate (including start and boss rooms).")]
    [SerializeField] private int totalRooms = 20;
    [Tooltip("The total number of boss rooms to place at dead ends.")]
    [SerializeField] private int numberOfBossRooms = 1;
    [Tooltip("The exact size of one room. ALL room prefabs must be this size!")]
    [SerializeField] private Vector2 roomSize = new Vector2(20, 10);

    [Header("Room Prefabs")]
    [Tooltip("The prefab for the very first room.")]
    [SerializeField] private Room startRoomPrefab;
    [Tooltip("A list of all possible 'normal' room prefabs (hallways, branches, etc.).")]
    [SerializeField] private List<Room> normalRoomPrefabs;
    [Tooltip("A list of all possible 'boss' room prefabs.")]
    [SerializeField] private List<Room> bossRoomPrefabs;

    [Header("Environment Population")]
    [Tooltip("The list of all possible environment props and their spawn chances.")]
    [SerializeField] private List<EnvironmentProp> environmentProps;
    [Tooltip("How many spawn *attempts* to make per square unit. Higher = denser rooms.")]
    [SerializeField] private float propSpawnAttemptsPerUnit = 0.1f;

    // --- Private Internal State ---
    
    // A "frontier" of open doorways. Stores the grid position for a *new* room
    // and the direction its "entry" door must be.
    private class GenerationState
    {
        public Vector2Int gridPos;
        public Direction requiredEntryDoor;
    }

    private List<GenerationState> frontier = new List<GenerationState>();
    private Dictionary<Vector2Int, Room> grid = new Dictionary<Vector2Int, Room>();
    private List<Room> allRooms = new List<Room>();

    void Start()
    {
        GenerateLevel();
    }

    // --- MODIFIED: Debugging Function ---
    /// <summary>
    /// Listens for the 'R' key to regenerate the level for fast debugging.
    /// </summary>
    void Update()
    {
        // Check if a keyboard is present and the 'R' key was pressed this frame
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Debug.Log("--- [DEBUG] Regenerating Level via 'R' key! ---");
            GenerateLevel();
        }
    }
    // --- END MODIFICATION ---

    public void GenerateLevel()
    {
        // 1. Clear any old level
        foreach (var room in allRooms)
        {
            if(room) Destroy(room.gameObject);
        }
        grid.Clear();
        allRooms.Clear();
        frontier.Clear();

        // 2. Spawn Start Room
        if (startRoomPrefab == null)
        {
            Debug.LogError("Start Room Prefab is not assigned!");
            return;
        }
        SpawnRoom(startRoomPrefab, Vector2Int.zero);

        // 3. Generate Normal Rooms
        int normalRoomsToBuild = totalRooms - numberOfBossRooms - 1; // Subtract 1 for start room
        int roomsBuilt = 0;
        while (roomsBuilt < normalRoomsToBuild && frontier.Count > 0)
        {
            if (TrySpawnRoom(normalRoomPrefabs))
            {
                roomsBuilt++;
            }
        }

        // 4. Place Boss Rooms
        PlaceBossRooms();
        
        // 5. Populate All Rooms
        PopulateRooms();
        
        Debug.Log($"Level Generation Complete: {allRooms.Count} total rooms spawned.");
    }

    /// <summary>
    /// Tries to pick a random frontier point and spawn a matching normal room.
    /// </summary>
    private bool TrySpawnRoom(List<Room> roomList)
    {
        // Pick a random available doorway from our frontier
        int randIndex = Random.Range(0, frontier.Count);
        GenerationState state = frontier[randIndex];
        frontier.RemoveAt(randIndex); // This doorway is now "used"

        // Check if a room already exists at this grid position (e.g., from another branch)
        if (grid.ContainsKey(state.gridPos))
        {
            return false; // Failed to spawn, but we'll try another frontier point
        }

        // Find a random room prefab that has the required door
        Room roomPrefab = FindMatchingRoom(roomList, state.requiredEntryDoor);
        if (roomPrefab == null)
        {
            // This doorway is a dead end (no matching room prefab)
            return false; // Failed to spawn
        }

        // We have a valid room! Spawn it.
        SpawnRoom(roomPrefab, state.gridPos, state.requiredEntryDoor);
        return true; // Success!
    }

    /// <summary>
    /// Instantiates a room, registers it, and connects its doors.
    /// </summary>
    private void SpawnRoom(Room roomPrefab, Vector2Int gridPos, Direction entryDoor = (Direction)(-1))
    {
        // Calculate world position
        Vector3 worldPos = new Vector3(gridPos.x * roomSize.x, gridPos.y * roomSize.y, 0);
        
        // Spawn and name the room
        Room newRoom = Instantiate(roomPrefab, worldPos, Quaternion.identity, transform);
        newRoom.name = $"Room_{gridPos.x}_{gridPos.y} ({roomPrefab.name})";
        newRoom.gridPos = gridPos;

        // Register the room
        grid[gridPos] = newRoom;
        allRooms.Add(newRoom);

        // --- Connect doors ---
        if (entryDoor != (Direction)(-1))
        {
            Vector2Int neighborPos = gridPos + GetOppositeDirectionVector(entryDoor);
            if (grid.TryGetValue(neighborPos, out Room neighborRoom))
            {
                newRoom.OpenDoor(entryDoor);
                neighborRoom.OpenDoor(GetOppositeDirection(entryDoor));
            }
        }

        // Add this new room's *other* doors to the frontier
        AddNeighborsToFrontier(newRoom);
    }

    /// <summary>
    /// Checks all 4 sides of a new room and adds its available doors to the frontier.
    /// </summary>
    private void AddNeighborsToFrontier(Room room)
    {
        if (room.hasTopDoor)    TryAddFrontier(room.gridPos + Vector2Int.up,    Direction.Bottom);
        if (room.hasBottomDoor) TryAddFrontier(room.gridPos + Vector2Int.down,  Direction.Top);
        if (room.hasLeftDoor)   TryAddFrontier(room.gridPos + Vector2Int.left,  Direction.Right);
        if (room.hasRightDoor)  TryAddFrontier(room.gridPos + Vector2Int.right, Direction.Left);
    }

    /// <summary>
    /// Adds a new potential room location to the frontier, if it's not already occupied.
    /// </summary>
    private void TryAddFrontier(Vector2Int pos, Direction requiredDoor)
    {
        if (!grid.ContainsKey(pos))
        {
            frontier.Add(new GenerationState { gridPos = pos, requiredEntryDoor = requiredDoor });
        }
    }
    
    /// <summary>
    /// Finds a random room from a list that has at least one matching door.
    /// </summary>
    private Room FindMatchingRoom(List<Room> roomList, Direction requiredDoor)
    {
        List<Room> candidates = new List<Room>();
        foreach (Room room in roomList)
        {
            if (requiredDoor == Direction.Top    && room.hasTopDoor)    candidates.Add(room);
            if (requiredDoor == Direction.Bottom && room.hasBottomDoor) candidates.Add(room);
            if (requiredDoor == Direction.Left   && room.hasLeftDoor)   candidates.Add(room);
            if (requiredDoor == Direction.Right  && room.hasRightDoor)  candidates.Add(room);
        }

        if (candidates.Count == 0) return null; // No matching rooms
        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// After normal generation, finds dead ends and places boss rooms there.
    /// </summary>
    private void PlaceBossRooms()
    {
        if (bossRoomPrefabs.Count == 0) return;

        frontier = frontier.OrderBy(x => Random.value).ToList();

        int bossesPlaced = 0;
        while (bossesPlaced < numberOfBossRooms && frontier.Count > 0)
        {
            if (TrySpawnRoom(bossRoomPrefabs))
            {
                bossesPlaced++;
            }
        }
    }

    /// <summary>
    /// Tells every spawned room to populate itself with environment objects.
    /// </summary>
    private void PopulateRooms()
    {
        if (environmentProps == null || environmentProps.Count == 0) return;

        foreach (Room room in allRooms)
        {
            // Pass the prop list, room size, and density to each room
            room.PopulateRoom(environmentProps, roomSize, propSpawnAttemptsPerUnit);
        }
    }

    // --- Helper Methods ---
    private Vector2Int GetOppositeDirectionVector(Direction dir)
    {
        if (dir == Direction.Top)    return Vector2Int.down;
        if (dir == Direction.Bottom) return Vector2Int.up;
        if (dir == Direction.Left)   return Vector2Int.right;
        if (dir == Direction.Right)  return Vector2Int.left;
        return Vector2Int.zero;
    }

    private Direction GetOppositeDirection(Direction dir)
    {
        if (dir == Direction.Top)    return Direction.Bottom;
        if (dir == Direction.Bottom) return Direction.Top;
        if (dir == Direction.Left)   return Direction.Right;
        if (dir == Direction.Right)  return Direction.Left;
        return (Direction)(-1);
    }
}