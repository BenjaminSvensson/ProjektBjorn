using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Used for ordering
using UnityEngine.InputSystem; 
using UnityEngine.SceneManagement; // --- NEW: Required for Scene reloading ---

public enum Direction { Top, Bottom, Left, Right }

public class LevelGenerator : MonoBehaviour
{
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

    // --- Enemy Spawn Configuration ---
    [System.Serializable]
    public class EnemySpawnData
    {
        public string name = "Enemy";
        public GameObject prefab;
        [Tooltip("Cost to spawn this enemy. Higher cost = stronger enemy.")]
        public int cost = 1;
        [Tooltip("Minimum distance from start (0,0) required for this enemy to appear.")]
        public int minDistanceReq = 0;
    }
    // --------------------------------------

    [Header("Generation Settings")]
    [Tooltip("The total number of rooms to generate (including start and boss rooms).")]
    [SerializeField] private int totalRooms = 20;
    [Tooltip("The total number of boss rooms to place at dead ends.")]
    [SerializeField] private int numberOfBossRooms = 1;
    [Tooltip("The exact size of one room. ALL room prefabs must be this size!")]
    [SerializeField] private Vector2 roomSize = new Vector2(20, 10);
    [Tooltip("How many times to retry generating if the room count target isn't met.")]
    [SerializeField] private int maxGenerationAttempts = 10;

    [Header("Room Prefabs")]
    [SerializeField] private Room startRoomPrefab;
    [SerializeField] private List<Room> normalRoomPrefabs;
    [SerializeField] private List<Room> bossRoomPrefabs;

    [Header("Enemy Spawning")]
    [Tooltip("List of enemies and their costs.")]
    [SerializeField] private List<EnemySpawnData> enemySpawnList;
    [Tooltip("Base budget for the first room (usually low).")]
    [SerializeField] private int baseEnemyBudget = 2;
    [Tooltip("How much the budget increases per grid unit distance from start.")]
    [SerializeField] private int enemyBudgetPerDistance = 2;

    [Header("Environment Population")]
    [SerializeField] private List<EnvironmentProp> environmentProps;
    [SerializeField] private float propSpawnAttemptsPerUnit = 0.1f;

    // --- Private Internal State ---
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

    void Update()
    {
        if (Keyboard.current == null) return;

        // 'R' -> Regenerate Layout (Keeps Player state)
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            Debug.Log("--- [DEBUG] Regenerating Level via 'R' key! ---");
            GenerateLevel();
        }

        // 'T' -> Reload Scene (Full Reset)
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            Debug.Log("--- [DEBUG] Reloading Scene via 'T' key! ---");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void GenerateLevel()
    {
        int attempts = 0;

        while (attempts < maxGenerationAttempts)
        {
            // 1. Run a single generation attempt
            RunGenerationAttempt();

            // 2. Check if we hit our target
            if (allRooms.Count >= totalRooms)
            {
                Debug.Log($"Success! Generated {allRooms.Count} rooms on attempt {attempts + 1}.");
                break; // Exit the loop, we are done!
            }
            else
            {
                Debug.LogWarning($"Attempt {attempts + 1} failed (only {allRooms.Count}/{totalRooms} rooms). Retrying...");
                attempts++;
            }
        }

        if (allRooms.Count < totalRooms)
        {
            Debug.LogError("Failed to generate a complete level after " + maxGenerationAttempts + " attempts.");
        }
    }

    private void RunGenerationAttempt()
    {
        // 1. Clear any old level
        foreach (var room in allRooms)
        {
            if (room) Destroy(room.gameObject);
        }
        grid.Clear();
        allRooms.Clear();
        frontier.Clear();

        // 2. Spawn Start Room
        if (startRoomPrefab == null) return;
        SpawnRoom(startRoomPrefab, Vector2Int.zero);

        // 3. Generate Normal Rooms
        int normalRoomsToBuild = totalRooms - numberOfBossRooms - 1; 
        int roomsBuilt = 0;
        
        int safetyCounter = 0;
        while (roomsBuilt < normalRoomsToBuild && frontier.Count > 0 && safetyCounter < 1000)
        {
            if (TrySpawnRoom(normalRoomPrefabs))
            {
                roomsBuilt++;
            }
            safetyCounter++;
        }

        // 4. Place Boss Rooms
        PlaceBossRooms();

        // 5. Populate All Rooms (only done once per attempt)
        PopulateRooms();
    }

    private bool TrySpawnRoom(List<Room> roomList)
    {
        int randIndex = Random.Range(0, frontier.Count);
        GenerationState state = frontier[randIndex];
        frontier.RemoveAt(randIndex); 

        if (grid.ContainsKey(state.gridPos)) return false; 

        Room roomPrefab = FindMatchingRoom(roomList, state.requiredEntryDoor);
        if (roomPrefab == null) return false; 

        SpawnRoom(roomPrefab, state.gridPos, state.requiredEntryDoor);
        return true; 
    }

    private void SpawnRoom(Room roomPrefab, Vector2Int gridPos, Direction entryDoor = (Direction)(-1))
    {
        Vector3 worldPos = new Vector3(gridPos.x * roomSize.x, gridPos.y * roomSize.y, 0);
        
        Room newRoom = Instantiate(roomPrefab, worldPos, Quaternion.identity, transform);
        newRoom.name = $"Room_{gridPos.x}_{gridPos.y} ({roomPrefab.name})";
        newRoom.gridPos = gridPos;

        grid[gridPos] = newRoom;
        allRooms.Add(newRoom);

        if (entryDoor != (Direction)(-1))
        {
            Vector2Int neighborPos = gridPos + GetOppositeDirectionVector(entryDoor);
            if (grid.TryGetValue(neighborPos, out Room neighborRoom))
            {
                newRoom.OpenDoor(entryDoor);
                neighborRoom.OpenDoor(GetOppositeDirection(entryDoor));
            }
        }

        AddNeighborsToFrontier(newRoom);
    }

    private void AddNeighborsToFrontier(Room room)
    {
        if (room.hasTopDoor)    TryAddFrontier(room.gridPos + Vector2Int.up,    Direction.Bottom);
        if (room.hasBottomDoor) TryAddFrontier(room.gridPos + Vector2Int.down,  Direction.Top);
        if (room.hasLeftDoor)   TryAddFrontier(room.gridPos + Vector2Int.left,  Direction.Right);
        if (room.hasRightDoor)  TryAddFrontier(room.gridPos + Vector2Int.right, Direction.Left);
    }

    private void TryAddFrontier(Vector2Int pos, Direction requiredDoor)
    {
        if (!grid.ContainsKey(pos))
        {
            frontier.Add(new GenerationState { gridPos = pos, requiredEntryDoor = requiredDoor });
        }
    }
    
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

        if (candidates.Count == 0) return null; 
        return candidates[Random.Range(0, candidates.Count)];
    }

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

    private void PopulateRooms()
    {
        foreach (Room room in allRooms)
        {
            // --- Enemy Spawning ---
            // Calculate Manhattan distance from (0,0) as difficulty
            int distance = Mathf.Abs(room.gridPos.x) + Mathf.Abs(room.gridPos.y);
            
            // Only spawn enemies if distance > 0 (Skip Start Room)
            if (distance > 0)
            {
                // Calculate budget: Base + (Dist * Multiplier)
                int roomBudget = baseEnemyBudget + (distance * enemyBudgetPerDistance);
                
                // Call spawn method on room
                if (enemySpawnList != null && enemySpawnList.Count > 0)
                {
                    room.SpawnEnemies(enemySpawnList, roomBudget, roomSize);
                }
            }
            // ---------------------------

            // Spawn Environment Props
            if (environmentProps != null && environmentProps.Count > 0)
            {
                room.PopulateRoom(environmentProps, roomSize, propSpawnAttemptsPerUnit);
            }
        }
    }

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