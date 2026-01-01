using UnityEngine;
using System.Collections.Generic;
using System.Linq; 
using UnityEngine.InputSystem; 
using UnityEngine.SceneManagement; 

public enum Direction { Top, Bottom, Left, Right }

public class LevelGenerator : MonoBehaviour
{
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
    [Tooltip("The minimum number of rooms between Start and Boss.")]
    [SerializeField] private int minBossDistance = 5; 
    [SerializeField] private Vector2 roomSize = new Vector2(20, 10);
    [SerializeField] private int maxGenerationAttempts = 100; 

    [Header("Room Rules")]
    [SerializeField] private Room startRoomPrefab;
    [SerializeField] private List<RoomSpawnRule> normalRoomRules; 
    [SerializeField] private List<Room> bossRoomPrefabs;
    
    [Header("Dead Ends")]
    [Tooltip("Prefabs used to cap off open paths (Must have exactly 1 door to work best).")]
    [SerializeField] private List<Room> deadEndRoomPrefabs;

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

    [Header("UI")] // --- NEW ---
    [SerializeField] private LoadingScreen loadingScreen;

    private class RoomNode
    {
        public Vector2Int gridPos;
        public Room roomPrefab;
        public bool isBossRoom;
        public int distanceFromStart; 
        
        public RoomNode(Vector2Int pos, Room prefab, int dist, bool boss = false) 
        { 
            gridPos = pos; 
            roomPrefab = prefab; 
            distanceFromStart = dist;
            isBossRoom = boss; 
        }
    }

    private class GenerationState 
    { 
        public Vector2Int gridPos; 
        public Direction fromDir; 
        public int distance; 
    }

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
        // Try find loading screen if not assigned
        if (loadingScreen == null) loadingScreen = FindObjectOfType<LoadingScreen>();

        foreach (var room in instantiatedRooms) if (room) Destroy(room.gameObject);
        instantiatedRooms.Clear(); finalLayout.Clear();

        bool success = false;
        int attempt = 0;
        while (!success && attempt < maxGenerationAttempts) { attempt++; success = AttemptVirtualGeneration(); }

        if (success) 
        {
            SpawnWorld();
            
            // --- NEW: Dismiss Loading Screen ---
            if (loadingScreen != null)
            {
                loadingScreen.Dismiss();
            }
        }
        else 
        {
            Debug.LogError($"Failed to generate level after {maxGenerationAttempts} attempts. Try reducing Min Boss Distance or increasing Total Rooms.");
        }
    }

    private bool AttemptVirtualGeneration()
    {
        Dictionary<Vector2Int, RoomNode> virtualGrid = new Dictionary<Vector2Int, RoomNode>();
        List<GenerationState> frontier = new List<GenerationState>();
        List<RoomNode> generatedNodes = new List<RoomNode>();
        
        Dictionary<Room, int> spawnCounts = new Dictionary<Room, int>();

        if (startRoomPrefab == null) return false;
        
        // Start Room is distance 0
        RoomNode startNode = new RoomNode(Vector2Int.zero, startRoomPrefab, 0);
        virtualGrid[Vector2Int.zero] = startNode; generatedNodes.Add(startNode);
        AddNeighborsToFrontier(virtualGrid, frontier, startNode, 0);

        int targetNormalRooms = totalRooms - numberOfBossRooms - 1;
        int roomsBuilt = 0, safetyLoop = 0;

        while (roomsBuilt < targetNormalRooms && frontier.Count > 0 && safetyLoop < 5000)
        {
            safetyLoop++;
            int randIndex = Random.Range(0, frontier.Count);
            GenerationState state = frontier[randIndex];
            frontier.RemoveAt(randIndex);

            if (virtualGrid.ContainsKey(state.gridPos)) continue;

            Room validPrefab = FindBestMatchingRoomWeighted(virtualGrid, state.gridPos, normalRoomRules, spawnCounts);
            
            if (validPrefab != null)
            {
                RoomNode newNode = new RoomNode(state.gridPos, validPrefab, state.distance);
                virtualGrid[state.gridPos] = newNode; 
                generatedNodes.Add(newNode);
                
                if (!spawnCounts.ContainsKey(validPrefab)) spawnCounts[validPrefab] = 0;
                spawnCounts[validPrefab]++;

                roomsBuilt++; 
                AddNeighborsToFrontier(virtualGrid, frontier, newNode, state.distance);
            }
        }

        if (roomsBuilt < targetNormalRooms) return false; 

        // --- Boss Placement ---
        // Filter frontier for spots far enough away
        var validBossSpots = frontier.Where(x => x.distance >= minBossDistance && !virtualGrid.ContainsKey(x.gridPos)).ToList();
        
        if (validBossSpots.Count < numberOfBossRooms) return false; // Retry if we didn't get far enough

        int bossesPlaced = 0;
        int bossSafety = 0;
        
        // Use the valid spots list instead of generic frontier
        while (bossesPlaced < numberOfBossRooms && validBossSpots.Count > 0 && bossSafety < 100)
        {
            bossSafety++;
            int randIndex = Random.Range(0, validBossSpots.Count);
            GenerationState state = validBossSpots[randIndex]; 
            validBossSpots.RemoveAt(randIndex);

            if (virtualGrid.ContainsKey(state.gridPos)) continue;
            
            Room validBoss = FindBestMatchingRoomSimple(virtualGrid, state.gridPos, bossRoomPrefabs);
            
            if (validBoss != null)
            {
                RoomNode bossNode = new RoomNode(state.gridPos, validBoss, state.distance, true);
                virtualGrid[state.gridPos] = bossNode; generatedNodes.Add(bossNode);
                bossesPlaced++;
            }
        }

        if (bossesPlaced < numberOfBossRooms) return false; 

        CapOpenConnections(virtualGrid, generatedNodes);

        finalLayout = generatedNodes; 
        return true;
    }

    private void CapOpenConnections(Dictionary<Vector2Int, RoomNode> grid, List<RoomNode> allNodes)
    {
        var existingPositions = grid.Keys.ToList();

        foreach (var pos in existingPositions)
        {
            RoomNode node = grid[pos];
            // Dead ends inherit distance from neighbor + 1
            int dist = node.distanceFromStart + 1;

            if (node.roomPrefab.hasTopDoor)    TryCap(grid, allNodes, pos + Vector2Int.up, Direction.Bottom, dist);
            if (node.roomPrefab.hasBottomDoor) TryCap(grid, allNodes, pos + Vector2Int.down, Direction.Top, dist);
            if (node.roomPrefab.hasLeftDoor)   TryCap(grid, allNodes, pos + Vector2Int.left, Direction.Right, dist);
            if (node.roomPrefab.hasRightDoor)  TryCap(grid, allNodes, pos + Vector2Int.right, Direction.Left, dist);
        }
    }

    private void TryCap(Dictionary<Vector2Int, RoomNode> grid, List<RoomNode> allNodes, Vector2Int pos, Direction requiredDoor, int dist)
    {
        if (grid.ContainsKey(pos)) return; 

        Room capPrefab = deadEndRoomPrefabs.FirstOrDefault(r => 
            (requiredDoor == Direction.Top && r.hasTopDoor && !r.hasBottomDoor && !r.hasLeftDoor && !r.hasRightDoor) ||
            (requiredDoor == Direction.Bottom && r.hasBottomDoor && !r.hasTopDoor && !r.hasLeftDoor && !r.hasRightDoor) ||
            (requiredDoor == Direction.Left && r.hasLeftDoor && !r.hasTopDoor && !r.hasBottomDoor && !r.hasRightDoor) ||
            (requiredDoor == Direction.Right && r.hasRightDoor && !r.hasTopDoor && !r.hasBottomDoor && !r.hasLeftDoor)
        );

        if (capPrefab == null) capPrefab = FindBestMatchingRoomSimple(grid, pos, deadEndRoomPrefabs);

        if (capPrefab != null)
        {
            RoomNode capNode = new RoomNode(pos, capPrefab, dist);
            grid[pos] = capNode;
            allNodes.Add(capNode);
        }
    }

    private void AddNeighborsToFrontier(Dictionary<Vector2Int, RoomNode> grid, List<GenerationState> frontier, RoomNode node, int curDist)
    {
        int nextDist = curDist + 1;
        if (node.roomPrefab.hasTopDoor)    TryAddFrontier(grid, frontier, node.gridPos + Vector2Int.up, Direction.Bottom, nextDist);
        if (node.roomPrefab.hasBottomDoor) TryAddFrontier(grid, frontier, node.gridPos + Vector2Int.down, Direction.Top, nextDist);
        if (node.roomPrefab.hasLeftDoor)   TryAddFrontier(grid, frontier, node.gridPos + Vector2Int.left, Direction.Right, nextDist);
        if (node.roomPrefab.hasRightDoor)  TryAddFrontier(grid, frontier, node.gridPos + Vector2Int.right, Direction.Left, nextDist);
    }

    private void TryAddFrontier(Dictionary<Vector2Int, RoomNode> grid, List<GenerationState> frontier, Vector2Int pos, Direction fromDir, int dist)
    {
        if (!grid.ContainsKey(pos) && !frontier.Any(x => x.gridPos == pos)) 
            frontier.Add(new GenerationState { gridPos = pos, fromDir = fromDir, distance = dist });
    }

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
            if (rule.maxSpawns > 0)
            {
                int usedCount = currentCounts.ContainsKey(rule.roomPrefab) ? currentCounts[rule.roomPrefab] : 0;
                if (usedCount >= rule.maxSpawns) continue; 
            }

            Room room = rule.roomPrefab;
            if (Matches(room.hasTopDoor, t) && Matches(room.hasBottomDoor, b) && 
                Matches(room.hasLeftDoor, l) && Matches(room.hasRightDoor, r))
            {
                validRules.Add(rule);
                totalWeight += rule.spawnWeight;
            }
        }

        if (validRules.Count == 0) return null;

        float randomValue = Random.Range(0, totalWeight);
        float weightSum = 0;

        foreach (var rule in validRules)
        {
            weightSum += rule.spawnWeight;
            if (randomValue <= weightSum) return rule.roomPrefab;
        }

        return validRules.Last().roomPrefab; 
    }

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

            // --- Pass Distance Info to Rooms ---
            int dist = originalNode != null ? originalNode.distanceFromStart : Mathf.Abs(pos.x) + Mathf.Abs(pos.y);

            bool allowProps = dist != 0 && (originalNode == null || !originalNode.isBossRoom || spawnPropsInBossRooms) && (preventPropSpawningInRooms == null || originalNode == null || !preventPropSpawningInRooms.Contains(originalNode.roomPrefab));
            if (allowProps && environmentProps != null) room.PopulateRoom(environmentProps, roomSize, propSpawnAttemptsPerUnit);
            if (dist > 0 && enemySpawnList != null && enemySpawnList.Count > 0) room.SpawnEnemies(enemySpawnList, baseEnemyBudget + (dist * enemyBudgetPerDistance), roomSize);
        }
    }
}