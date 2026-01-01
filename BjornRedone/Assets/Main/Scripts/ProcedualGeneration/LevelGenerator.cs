using UnityEngine;
using System.Collections.Generic;
using System.Linq; 
using UnityEngine.InputSystem; 
using UnityEngine.SceneManagement; 

public enum Direction { Top, Bottom, Left, Right }

public class LevelGenerator : MonoBehaviour
{
    // --- NEW: Rule Structure for Rooms ---
    [System.Serializable]
    public class RoomSpawnRule
    {
        [Tooltip("The Room Prefab.")]
        public Room roomPrefab;
        
        [Tooltip("Relative chance to spawn. Higher values = more frequent.")]
        [Range(0.1f, 100f)]
        public float spawnWeight = 10f;

        [Tooltip("Maximum times this specific room can spawn in the level. 0 = Unlimited.")]
        [Min(0)]
        public int maxSpawns = 0;
    }

    [System.Serializable]
    public class EnvironmentProp
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float spawnChance = 0.5f;
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
    [SerializeField] private int maxGenerationAttempts = 100; 

    [Header("Room Rules")]
    [SerializeField] private Room startRoomPrefab;
    // --- UPDATED: Replaced simple list with Rule List ---
    [SerializeField] private List<RoomSpawnRule> normalRoomRules; 
    [SerializeField] private List<Room> bossRoomPrefabs; // Keep bosses simple for now

    [Header("Enemy Spawning")]
    [SerializeField] private List<EnemySpawnData> enemySpawnList;
    [SerializeField] private int baseEnemyBudget = 2;
    [SerializeField] private int enemyBudgetPerDistance = 2;

    [Header("Environment Population")]
    [SerializeField] private List<EnvironmentProp> environmentProps;
    [SerializeField] private float propSpawnAttemptsPerUnit = 0.1f;
    
    [Header("Exceptions")]
    [SerializeField] private bool spawnPropsInBossRooms = false;
    [SerializeField] private List<Room> preventPropSpawningInRooms;

    private class RoomNode
    {
        public Vector2Int gridPos;
        public Room roomPrefab;
        public bool isBossRoom;
        public RoomNode(Vector2Int pos, Room prefab, bool boss = false) { gridPos = pos; roomPrefab = prefab; isBossRoom = boss; }
    }

    private class GenerationState { public Vector2Int gridPos; public Direction fromDir; }

    private List<RoomNode> finalLayout = new List<RoomNode>();
    private List<Room> instantiatedRooms = new List<Room>();

    void Start() => GenerateLevel();

    void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.yKey.wasPressedThisFrame) GenerateLevel();
        if (Keyboard.current.tKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GenerateLevel()
    {
        foreach (var room in instantiatedRooms) if (room) Destroy(room.gameObject);
        instantiatedRooms.Clear(); finalLayout.Clear();

        bool success = false;
        int attempt = 0;
        while (!success && attempt < maxGenerationAttempts) { attempt++; success = AttemptVirtualGeneration(); }

        if (success) SpawnWorld();
        else Debug.LogError($"Failed to generate level after {maxGenerationAttempts} attempts.");
    }

    private bool AttemptVirtualGeneration()
    {
        Dictionary<Vector2Int, RoomNode> virtualGrid = new Dictionary<Vector2Int, RoomNode>();
        List<GenerationState> frontier = new List<GenerationState>();
        List<RoomNode> generatedNodes = new List<RoomNode>();
        
        // --- NEW: Track Spawn Counts per Prefab ---
        Dictionary<Room, int> spawnCounts = new Dictionary<Room, int>();

        if (startRoomPrefab == null) return false;
        RoomNode startNode = new RoomNode(Vector2Int.zero, startRoomPrefab);
        virtualGrid[Vector2Int.zero] = startNode; generatedNodes.Add(startNode);
        AddNeighborsToFrontier(virtualGrid, frontier, startNode);

        int targetNormalRooms = totalRooms - numberOfBossRooms - 1;
        int roomsBuilt = 0, safetyLoop = 0;

        while (roomsBuilt < targetNormalRooms && frontier.Count > 0 && safetyLoop < 5000)
        {
            safetyLoop++;
            int randIndex = Random.Range(0, frontier.Count);
            GenerationState state = frontier[randIndex];
            frontier.RemoveAt(randIndex);

            if (virtualGrid.ContainsKey(state.gridPos)) continue;

            // --- UPDATED: Pass Rules and Spawn Counts ---
            Room validPrefab = FindBestMatchingRoomWeighted(virtualGrid, state.gridPos, normalRoomRules, spawnCounts);
            
            if (validPrefab != null)
            {
                RoomNode newNode = new RoomNode(state.gridPos, validPrefab);
                virtualGrid[state.gridPos] = newNode; 
                generatedNodes.Add(newNode);
                
                // Track usage
                if (!spawnCounts.ContainsKey(validPrefab)) spawnCounts[validPrefab] = 0;
                spawnCounts[validPrefab]++;

                roomsBuilt++; 
                AddNeighborsToFrontier(virtualGrid, frontier, newNode);
            }
        }

        if (roomsBuilt < targetNormalRooms) return false; 

        int bossesPlaced = 0, bossSafety = 0;
        while (bossesPlaced < numberOfBossRooms && frontier.Count > 0 && bossSafety < 100)
        {
            bossSafety++;
            int randIndex = Random.Range(0, frontier.Count);
            GenerationState state = frontier[randIndex]; frontier.RemoveAt(randIndex);
            if (virtualGrid.ContainsKey(state.gridPos)) continue;
            
            // Bosses use simple list logic still (can be updated similarly if needed)
            Room validBoss = FindBestMatchingRoomSimple(virtualGrid, state.gridPos, bossRoomPrefabs);
            
            if (validBoss != null)
            {
                RoomNode bossNode = new RoomNode(state.gridPos, validBoss, true);
                virtualGrid[state.gridPos] = bossNode; generatedNodes.Add(bossNode);
                bossesPlaced++;
            }
        }

        if (bossesPlaced < numberOfBossRooms) return false; 
        finalLayout = generatedNodes; return true;
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
        if (!grid.ContainsKey(pos) && !frontier.Any(x => x.gridPos == pos)) frontier.Add(new GenerationState { gridPos = pos, fromDir = fromDir });
    }

    // --- NEW: Weighted Selection with Limits ---
    private Room FindBestMatchingRoomWeighted(Dictionary<Vector2Int, RoomNode> grid, Vector2Int pos, List<RoomSpawnRule> rules, Dictionary<Room, int> currentCounts)
    {
        bool? t = GetRequirement(grid, pos + Vector2Int.up, Direction.Bottom);
        bool? b = GetRequirement(grid, pos + Vector2Int.down, Direction.Top);
        bool? l = GetRequirement(grid, pos + Vector2Int.left, Direction.Right);
        bool? r = GetRequirement(grid, pos + Vector2Int.right, Direction.Left);

        List<RoomSpawnRule> validRules = new List<RoomSpawnRule>();
        float totalWeight = 0f;

        foreach (var rule in rules)
        {
            if (rule.roomPrefab == null) continue;

            // 1. Check Limits
            if (rule.maxSpawns > 0)
            {
                int usedCount = currentCounts.ContainsKey(rule.roomPrefab) ? currentCounts[rule.roomPrefab] : 0;
                if (usedCount >= rule.maxSpawns) continue; // Skip if limit reached
            }

            // 2. Check Door Connections
            Room room = rule.roomPrefab;
            if (Matches(room.hasTopDoor, t) && Matches(room.hasBottomDoor, b) && 
                Matches(room.hasLeftDoor, l) && Matches(room.hasRightDoor, r))
            {
                validRules.Add(rule);
                totalWeight += rule.spawnWeight;
            }
        }

        if (validRules.Count == 0) return null;

        // 3. Weighted Random Pick
        float randomValue = Random.Range(0, totalWeight);
        float weightSum = 0;

        foreach (var rule in validRules)
        {
            weightSum += rule.spawnWeight;
            if (randomValue <= weightSum)
            {
                return rule.roomPrefab;
            }
        }

        return validRules.Last().roomPrefab; // Fallback
    }

    // Kept for Bosses/Simple lists
    private Room FindBestMatchingRoomSimple(Dictionary<Vector2Int, RoomNode> grid, Vector2Int pos, List<Room> candidates)
    {
        bool? t = GetRequirement(grid, pos + Vector2Int.up, Direction.Bottom);
        bool? b = GetRequirement(grid, pos + Vector2Int.down, Direction.Top);
        bool? l = GetRequirement(grid, pos + Vector2Int.left, Direction.Right);
        bool? r = GetRequirement(grid, pos + Vector2Int.right, Direction.Left);
        List<Room> valid = candidates.Where(room => Matches(room.hasTopDoor, t) && Matches(room.hasBottomDoor, b) && Matches(room.hasLeftDoor, l) && Matches(room.hasRightDoor, r)).ToList();
        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
    }

    private bool? GetRequirement(Dictionary<Vector2Int, RoomNode> grid, Vector2Int neighborPos, Direction neighborDoorDir)
    {
        if (grid.TryGetValue(neighborPos, out RoomNode neighbor))
        {
            switch (neighborDoorDir) {
                case Direction.Top: return neighbor.roomPrefab.hasTopDoor;
                case Direction.Bottom: return neighbor.roomPrefab.hasBottomDoor;
                case Direction.Left: return neighbor.roomPrefab.hasLeftDoor;
                case Direction.Right: return neighbor.roomPrefab.hasRightDoor;
            }
        }
        return null; 
    }

    private bool Matches(bool has, bool? req) => req == null || has == req.Value;

    private void SpawnWorld()
    {
        Dictionary<Vector2Int, Room> worldGrid = new Dictionary<Vector2Int, Room>();
        foreach (RoomNode node in finalLayout)
        {
            Room newRoom = Instantiate(node.roomPrefab, new Vector3(node.gridPos.x * roomSize.x, node.gridPos.y * roomSize.y, 0), Quaternion.identity, transform);
            newRoom.name = $"Room_{node.gridPos.x}_{node.gridPos.y}"; newRoom.gridPos = node.gridPos;
            worldGrid[node.gridPos] = newRoom; instantiatedRooms.Add(newRoom);
        }

        foreach (var kvp in worldGrid)
        {
            Vector2Int pos = kvp.Key; Room room = kvp.Value;
            RoomNode originalNode = finalLayout.Find(n => n.gridPos == pos);
            if (worldGrid.ContainsKey(pos + Vector2Int.up)) room.OpenDoor(Direction.Top);
            if (worldGrid.ContainsKey(pos + Vector2Int.down)) room.OpenDoor(Direction.Bottom);
            if (worldGrid.ContainsKey(pos + Vector2Int.left)) room.OpenDoor(Direction.Left);
            if (worldGrid.ContainsKey(pos + Vector2Int.right)) room.OpenDoor(Direction.Right);

            int dist = Mathf.Abs(pos.x) + Mathf.Abs(pos.y);
            bool allowProps = dist != 0 && (originalNode == null || !originalNode.isBossRoom || spawnPropsInBossRooms) && (preventPropSpawningInRooms == null || originalNode == null || !preventPropSpawningInRooms.Contains(originalNode.roomPrefab));
            if (allowProps && environmentProps != null) room.PopulateRoom(environmentProps, roomSize, propSpawnAttemptsPerUnit);
            if (dist > 0 && enemySpawnList != null && enemySpawnList.Count > 0) room.SpawnEnemies(enemySpawnList, baseEnemyBudget + (dist * enemyBudgetPerDistance), roomSize);
        }
    }
}