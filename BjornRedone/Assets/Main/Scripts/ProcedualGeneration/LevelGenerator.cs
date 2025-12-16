using UnityEngine;
using System.Collections.Generic;
using System.Linq; 
using UnityEngine.InputSystem; 
using UnityEngine.SceneManagement; 

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
        public bool allowRandomFlip = true;
        [Range(0.5f, 1.5f)] public float minScale = 0.9f;
        [Range(0.5f, 1.5f)] public float maxScale = 1.1f;
    }

    [System.Serializable]
    public class EnemySpawnData
    {
        public string name = "Enemy";
        public GameObject prefab;
        public int cost = 1;
        public int minDistanceReq = 0;
    }

    [Header("Generation Settings")]
    [SerializeField] private int totalRooms = 20;
    [SerializeField] private int numberOfBossRooms = 1;
    [SerializeField] private Vector2 roomSize = new Vector2(20, 10);
    [Tooltip("Maximum attempts to generate a valid layout before giving up.")]
    [SerializeField] private int maxGenerationAttempts = 100; // Increased because virtual generation is fast

    [Header("Room Prefabs")]
    [SerializeField] private Room startRoomPrefab;
    [SerializeField] private List<Room> normalRoomPrefabs;
    [SerializeField] private List<Room> bossRoomPrefabs;

    [Header("Enemy Spawning")]
    [SerializeField] private List<EnemySpawnData> enemySpawnList;
    [SerializeField] private int baseEnemyBudget = 2;
    [SerializeField] private int enemyBudgetPerDistance = 2;

    [Header("Environment Population")]
    [SerializeField] private List<EnvironmentProp> environmentProps;
    [SerializeField] private float propSpawnAttemptsPerUnit = 0.1f;

    // --- Virtual Data Structures ---
    private class RoomNode
    {
        public Vector2Int gridPos;
        public Room roomPrefab;
        public bool isBossRoom;
        
        public RoomNode(Vector2Int pos, Room prefab, bool boss = false)
        {
            gridPos = pos;
            roomPrefab = prefab;
            isBossRoom = boss;
        }
    }

    private class GenerationState
    {
        public Vector2Int gridPos;
        public Direction fromDir; // The direction we came FROM to get here
    }

    // Stores the final successful layout to be instantiated
    private List<RoomNode> finalLayout = new List<RoomNode>();
    private List<Room> instantiatedRooms = new List<Room>();

    void Start()
    {
        GenerateLevel();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            Debug.Log("--- [DEBUG] Regenerating Level... ---");
            GenerateLevel();
        }

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void GenerateLevel()
    {
        // 1. Cleanup Old Level
        foreach (var room in instantiatedRooms)
        {
            if (room) Destroy(room.gameObject);
        }
        instantiatedRooms.Clear();
        finalLayout.Clear();

        // 2. Try to find a valid layout using Virtual Generation
        bool success = false;
        int attempt = 0;

        while (!success && attempt < maxGenerationAttempts)
        {
            attempt++;
            success = AttemptVirtualGeneration();
        }

        if (success)
        {
            Debug.Log($"Generated valid layout on attempt {attempt}. Spawning world...");
            SpawnWorld();
        }
        else
        {
            Debug.LogError($"Failed to generate level after {maxGenerationAttempts} attempts. Check Room settings/Doors.");
        }
    }

    // --- PHASE 1: Virtual Generation (Fast, Logic only) ---
    private bool AttemptVirtualGeneration()
    {
        Dictionary<Vector2Int, RoomNode> virtualGrid = new Dictionary<Vector2Int, RoomNode>();
        List<GenerationState> frontier = new List<GenerationState>();
        List<RoomNode> generatedNodes = new List<RoomNode>();

        // A. Place Start Room
        if (startRoomPrefab == null) return false;
        
        RoomNode startNode = new RoomNode(Vector2Int.zero, startRoomPrefab);
        virtualGrid[Vector2Int.zero] = startNode;
        generatedNodes.Add(startNode);
        
        AddNeighborsToFrontier(virtualGrid, frontier, startNode);

        // B. Place Normal Rooms
        int targetNormalRooms = totalRooms - numberOfBossRooms - 1;
        int roomsBuilt = 0;
        int safetyLoop = 0;

        while (roomsBuilt < targetNormalRooms && frontier.Count > 0 && safetyLoop < 5000)
        {
            safetyLoop++;

            // Pick random frontier position
            int randIndex = Random.Range(0, frontier.Count);
            GenerationState state = frontier[randIndex];
            frontier.RemoveAt(randIndex);

            if (virtualGrid.ContainsKey(state.gridPos)) continue;

            // CRITICAL FIX: Check ALL neighbors, not just the one we came from.
            // This ensures we don't place a room that blocks an existing door or opens into a wall.
            Room validPrefab = FindBestMatchingRoom(virtualGrid, state.gridPos, normalRoomPrefabs);

            if (validPrefab != null)
            {
                RoomNode newNode = new RoomNode(state.gridPos, validPrefab);
                virtualGrid[state.gridPos] = newNode;
                generatedNodes.Add(newNode);
                roomsBuilt++;

                AddNeighborsToFrontier(virtualGrid, frontier, newNode);
            }
        }

        // C. Check if we failed (Dead end or not enough rooms)
        if (roomsBuilt < targetNormalRooms) return false; // Revert! (Try again)

        // D. Place Boss Rooms (At dead ends)
        List<Vector2Int> potentialBossSpots = GetDeadEnds(virtualGrid);
        
        int bossesPlaced = 0;
        foreach (Vector2Int pos in potentialBossSpots)
        {
            if (bossesPlaced >= numberOfBossRooms) break;

            // We need a boss room that fits here. 
            // Note: Usually we REPLACE the dead end room with a Boss room, 
            // or append to it. Here let's try to append to available frontier spots first.
            // Simplified: Just reuse the frontier for boss rooms? 
            // Better: Replace the furthest rooms with Boss Rooms?
            
            // Current simple logic: Try to append using remaining frontier if possible, 
            // but normally Boss rooms are special. Let's stick to the generated nodes list
            // and maybe swap the last created room with a boss room if it fits?
        }
        
        // Simpler Boss Logic: Just append to remaining frontier
        int bossSafety = 0;
        while (bossesPlaced < numberOfBossRooms && frontier.Count > 0 && bossSafety < 100)
        {
            int randIndex = Random.Range(0, frontier.Count);
            GenerationState state = frontier[randIndex];
            frontier.RemoveAt(randIndex);

            if (virtualGrid.ContainsKey(state.gridPos)) continue;

            Room validBoss = FindBestMatchingRoom(virtualGrid, state.gridPos, bossRoomPrefabs);
            if (validBoss != null)
            {
                RoomNode bossNode = new RoomNode(state.gridPos, validBoss, true);
                virtualGrid[state.gridPos] = bossNode;
                generatedNodes.Add(bossNode);
                bossesPlaced++;
            }
            bossSafety++;
        }

        if (bossesPlaced < numberOfBossRooms) return false; // Failed to place boss

        // Success! Save this layout
        finalLayout = generatedNodes;
        return true;
    }

    private void AddNeighborsToFrontier(Dictionary<Vector2Int, RoomNode> grid, List<GenerationState> frontier, RoomNode node)
    {
        if (node.roomPrefab.hasTopDoor)    TryAddFrontier(grid, frontier, node.gridPos + Vector2Int.up, Direction.Bottom);
        if (node.roomPrefab.hasBottomDoor) TryAddFrontier(grid, frontier, node.gridPos + Vector2Int.down, Direction.Top);
        if (node.roomPrefab.hasLeftDoor)   TryAddFrontier(grid, frontier, node.gridPos + Vector2Int.left, Direction.Right);
        if (node.roomPrefab.hasRightDoor)  TryAddFrontier(grid, frontier, node.gridPos + Vector2Int.right, Direction.Left);
    }

    private void TryAddFrontier(Dictionary<Vector2Int, RoomNode> grid, List<GenerationState> frontier, Vector2Int pos, Direction fromDir)
    {
        if (!grid.ContainsKey(pos))
        {
            // Avoid duplicates in frontier to keep it clean
            if (!frontier.Any(x => x.gridPos == pos))
            {
                frontier.Add(new GenerationState { gridPos = pos, fromDir = fromDir });
            }
        }
    }

    private Room FindBestMatchingRoom(Dictionary<Vector2Int, RoomNode> grid, Vector2Int pos, List<Room> candidates)
    {
        // 1. Analyze neighbors to see what doors we MUST have and MUST NOT have
        bool? needTop = GetRequirement(grid, pos + Vector2Int.up, Direction.Bottom);
        bool? needBottom = GetRequirement(grid, pos + Vector2Int.down, Direction.Top);
        bool? needLeft = GetRequirement(grid, pos + Vector2Int.left, Direction.Right);
        bool? needRight = GetRequirement(grid, pos + Vector2Int.right, Direction.Left);

        List<Room> valid = new List<Room>();

        foreach (var room in candidates)
        {
            if (!Matches(room.hasTopDoor, needTop)) continue;
            if (!Matches(room.hasBottomDoor, needBottom)) continue;
            if (!Matches(room.hasLeftDoor, needLeft)) continue;
            if (!Matches(room.hasRightDoor, needRight)) continue;

            valid.Add(room);
        }

        if (valid.Count > 0) return valid[Random.Range(0, valid.Count)];
        return null;
    }

    // Returns:
    // TRUE: Neighbor exists and has a door facing us (We MUST have a matching door)
    // FALSE: Neighbor exists and has NO door facing us (We MUST NOT have a door)
    // NULL: No neighbor exists yet (We can choose either way)
    private bool? GetRequirement(Dictionary<Vector2Int, RoomNode> grid, Vector2Int neighborPos, Direction neighborDoorDir)
    {
        if (grid.TryGetValue(neighborPos, out RoomNode neighbor))
        {
            switch (neighborDoorDir)
            {
                case Direction.Top: return neighbor.roomPrefab.hasTopDoor;
                case Direction.Bottom: return neighbor.roomPrefab.hasBottomDoor;
                case Direction.Left: return neighbor.roomPrefab.hasLeftDoor;
                case Direction.Right: return neighbor.roomPrefab.hasRightDoor;
            }
        }
        return null; // Empty space
    }

    private bool Matches(bool roomHasDoor, bool? requirement)
    {
        if (requirement == null) return true; // No constraint
        return roomHasDoor == requirement.Value; // Must match exactly
    }

    private List<Vector2Int> GetDeadEnds(Dictionary<Vector2Int, RoomNode> grid)
    {
        List<Vector2Int> deadEnds = new List<Vector2Int>();
        foreach (var kvp in grid)
        {
            int connections = 0;
            Vector2Int p = kvp.Key;
            // Simply check how many neighbors exist
            if (grid.ContainsKey(p + Vector2Int.up)) connections++;
            if (grid.ContainsKey(p + Vector2Int.down)) connections++;
            if (grid.ContainsKey(p + Vector2Int.left)) connections++;
            if (grid.ContainsKey(p + Vector2Int.right)) connections++;

            if (connections == 1) deadEnds.Add(p);
        }
        return deadEnds;
    }

    // --- PHASE 2: Instantiation (Real World) ---
    private void SpawnWorld()
    {
        Dictionary<Vector2Int, Room> worldGrid = new Dictionary<Vector2Int, Room>();

        foreach (RoomNode node in finalLayout)
        {
            Vector3 worldPos = new Vector3(node.gridPos.x * roomSize.x, node.gridPos.y * roomSize.y, 0);
            Room newRoom = Instantiate(node.roomPrefab, worldPos, Quaternion.identity, transform);
            newRoom.name = $"Room_{node.gridPos.x}_{node.gridPos.y}";
            newRoom.gridPos = node.gridPos;
            
            worldGrid[node.gridPos] = newRoom;
            instantiatedRooms.Add(newRoom);
        }

        // Connect Doors & Populate
        foreach (var kvp in worldGrid)
        {
            Vector2Int pos = kvp.Key;
            Room room = kvp.Value;

            // Open doors if neighbor exists
            if (worldGrid.ContainsKey(pos + Vector2Int.up)) room.OpenDoor(Direction.Top);
            if (worldGrid.ContainsKey(pos + Vector2Int.down)) room.OpenDoor(Direction.Bottom);
            if (worldGrid.ContainsKey(pos + Vector2Int.left)) room.OpenDoor(Direction.Left);
            if (worldGrid.ContainsKey(pos + Vector2Int.right)) room.OpenDoor(Direction.Right);

            // Populate
            int dist = Mathf.Abs(pos.x) + Mathf.Abs(pos.y);
            if (dist > 0) // Skip start room
            {
                int budget = baseEnemyBudget + (dist * enemyBudgetPerDistance);
                if (enemySpawnList != null && enemySpawnList.Count > 0)
                    room.SpawnEnemies(enemySpawnList, budget, roomSize);
                
                if (environmentProps != null)
                    room.PopulateRoom(environmentProps, roomSize, propSpawnAttemptsPerUnit);
            }
        }
    }
}