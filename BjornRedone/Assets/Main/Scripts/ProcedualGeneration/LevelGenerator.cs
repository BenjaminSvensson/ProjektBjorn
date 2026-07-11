using UnityEngine;
using System.Collections.Generic;
using System.Linq; 
using UnityEngine.InputSystem; 
using UnityEngine.SceneManagement; 
using TMPro;

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
        [Min(1)] public int cost = 1;
        public int minDistanceReq = 0;
        [Min(0.01f)] public float spawnWeight = 1f;
    }

    [Header("Generation Settings")]
    [SerializeField] private int totalRooms = 20;
    [SerializeField] private int numberOfBossRooms = 1;
    [Tooltip("The minimum number of rooms between Start and Boss.")]
    [SerializeField] private int minBossDistance = 5; 
    [SerializeField] private Vector2 roomSize = new Vector2(20, 10);
    [SerializeField] private int maxGenerationAttempts = 100; 
    [Tooltip("How strongly generation extends the deepest path before adding side branches.")]
    [Range(0f, 1f)]
    [SerializeField] private float mainPathBias = 0.82f;

    [Header("Run Identity")]
    [Tooltip("Creates a new shareable run code every time a level is generated.")]
    [SerializeField] private bool randomizeSeed = true;
    [SerializeField] private int seed = 1337;
    [SerializeField] private bool announceRunCode = true;

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

    [Header("UI")]
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

    public int CurrentSeed { get; private set; }
    public string RunCode => unchecked((uint)CurrentSeed).ToString("X8");
    public int GeneratedRoomCount => finalLayout.Count;
    public int FurthestRoomDistance { get; private set; }
    public int BossRoomDistance { get; private set; }
    public int LastGenerationAttempts { get; private set; }

    void Start() => GenerateLevel();

    void Update()
    {
        if (Keyboard.current == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current.yKey.wasPressedThisFrame) RegenerateNewRun();
        if (Keyboard.current.tKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
#endif
    }

    public void GenerateLevel()
    {
        GenerateLevelInternal(null);
    }

    public void GenerateFromSeed(int requestedSeed)
    {
        GenerateLevelInternal(requestedSeed);
    }

    public void RegenerateNewRun()
    {
        GenerateLevelInternal(CreateSeed());
    }

    private void GenerateLevelInternal(int? requestedSeed)
    {
        if (loadingScreen == null) loadingScreen = FindFirstObjectByType<LoadingScreen>();

        CurrentSeed = requestedSeed ?? (randomizeSeed ? CreateSeed() : seed);
        loadingScreen?.Prepare(RunCode);

        foreach (var room in instantiatedRooms) if (room) Destroy(room.gameObject);
        instantiatedRooms.Clear(); finalLayout.Clear();

        UnityEngine.Random.State previousRandomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(CurrentSeed);

        bool success = false;
        LastGenerationAttempts = 0;
        int attempts = Mathf.Max(1, maxGenerationAttempts);

        try
        {
            while (!success && LastGenerationAttempts < attempts)
            {
                LastGenerationAttempts++;
                success = AttemptVirtualGeneration();
            }

            if (success)
            {
                SpawnWorld();
            }
        }
        finally
        {
            UnityEngine.Random.state = previousRandomState;
        }

        if (success)
        {
            loadingScreen?.Dismiss();
            AnnounceRun();
            Debug.Log($"Generated run {RunCode}: {GeneratedRoomCount} rooms, boss depth {BossRoomDistance}, furthest depth {FurthestRoomDistance}, {LastGenerationAttempts} attempt(s).");
        }
        else
        {
            string message = $"Failed to generate run {RunCode} after {attempts} attempts. Check room door coverage and boss-distance settings.";
            Debug.LogError(message);
            loadingScreen?.ShowError("GENERATION FAILED\nRESTART RUN TO RETRY");
        }
    }

    private int CreateSeed()
    {
        long ticks = System.DateTime.UtcNow.Ticks;
        return unchecked((int)(ticks ^ (ticks >> 32) ^ GetInstanceID()));
    }

    private bool AttemptVirtualGeneration()
    {
        Dictionary<Vector2Int, RoomNode> virtualGrid = new Dictionary<Vector2Int, RoomNode>();
        List<GenerationState> frontier = new List<GenerationState>();
        List<RoomNode> generatedNodes = new List<RoomNode>();
        
        Dictionary<Room, int> spawnCounts = new Dictionary<Room, int>();

        if (startRoomPrefab == null || normalRoomRules == null || normalRoomRules.Count == 0) return false;
        if (numberOfBossRooms > 0 && (bossRoomPrefabs == null || bossRoomPrefabs.Count == 0)) return false;
        
        RoomNode startNode = new RoomNode(Vector2Int.zero, startRoomPrefab, 0);
        virtualGrid[Vector2Int.zero] = startNode; generatedNodes.Add(startNode);
        AddNeighborsToFrontier(virtualGrid, frontier, startNode, 0);

        int targetNormalRooms = Mathf.Max(0, totalRooms - Mathf.Max(0, numberOfBossRooms) - 1);
        int roomsBuilt = 0, safetyLoop = 0;

        while (roomsBuilt < targetNormalRooms && frontier.Count > 0 && safetyLoop < 5000)
        {
            safetyLoop++;
            int randIndex = SelectFrontierIndex(frontier);
            GenerationState state = frontier[randIndex];
            frontier.RemoveAt(randIndex);

            if (virtualGrid.ContainsKey(state.gridPos)) continue;

            Room validPrefab = FindBestMatchingRoomWeighted(virtualGrid, state.gridPos, normalRoomRules, spawnCounts);
            
            if (validPrefab != null)
            {
                int exactDistance = GetConnectionDistance(virtualGrid, state.gridPos);
                RoomNode newNode = new RoomNode(state.gridPos, validPrefab, exactDistance);
                virtualGrid[state.gridPos] = newNode; 
                generatedNodes.Add(newNode);
                
                if (!spawnCounts.ContainsKey(validPrefab)) spawnCounts[validPrefab] = 0;
                spawnCounts[validPrefab]++;

                roomsBuilt++; 
                AddNeighborsToFrontier(virtualGrid, frontier, newNode, newNode.distanceFromStart);
            }
        }

        if (roomsBuilt < targetNormalRooms) return false; 

        RecalculateDistances(virtualGrid);

        var validBossSpots = frontier
            .Where(x => !virtualGrid.ContainsKey(x.gridPos))
            .GroupBy(x => x.gridPos)
            .Select(group => new GenerationState
            {
                gridPos = group.Key,
                fromDir = group.First().fromDir,
                distance = GetConnectionDistance(virtualGrid, group.Key)
            })
            .Where(x => x.distance >= minBossDistance)
            .ToList();
        
        if (validBossSpots.Count < numberOfBossRooms) return false; 

        int bossesPlaced = 0;
        int bossSafety = 0;
        
        while (bossesPlaced < numberOfBossRooms && validBossSpots.Count > 0 && bossSafety < 100)
        {
            bossSafety++;
            int furthestDistance = validBossSpots.Max(x => x.distance);
            List<int> furthestIndices = Enumerable.Range(0, validBossSpots.Count)
                .Where(i => validBossSpots[i].distance == furthestDistance)
                .ToList();
            int randIndex = furthestIndices[Random.Range(0, furthestIndices.Count)];
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
        RecalculateDistances(virtualGrid);

        finalLayout = generatedNodes; 
        FurthestRoomDistance = generatedNodes.Count > 0 ? generatedNodes.Max(node => node.distanceFromStart) : 0;
        BossRoomDistance = generatedNodes.Where(node => node.isBossRoom).Select(node => node.distanceFromStart).DefaultIfEmpty(0).Max();
        return true;
    }

    private int SelectFrontierIndex(List<GenerationState> frontier)
    {
        if (frontier.Count <= 1 || Random.value > mainPathBias)
        {
            return Random.Range(0, frontier.Count);
        }

        int deepest = frontier.Max(state => state.distance);
        List<int> deepCandidates = Enumerable.Range(0, frontier.Count)
            .Where(index => frontier[index].distance >= deepest - 1)
            .ToList();
        return deepCandidates[Random.Range(0, deepCandidates.Count)];
    }

    private int GetConnectionDistance(Dictionary<Vector2Int, RoomNode> grid, Vector2Int pos)
    {
        int bestDistance = int.MaxValue;
        TryReadNeighborDistance(grid, pos + Vector2Int.up, Direction.Bottom, ref bestDistance);
        TryReadNeighborDistance(grid, pos + Vector2Int.down, Direction.Top, ref bestDistance);
        TryReadNeighborDistance(grid, pos + Vector2Int.left, Direction.Right, ref bestDistance);
        TryReadNeighborDistance(grid, pos + Vector2Int.right, Direction.Left, ref bestDistance);
        return bestDistance == int.MaxValue ? 0 : bestDistance + 1;
    }

    private void TryReadNeighborDistance(Dictionary<Vector2Int, RoomNode> grid, Vector2Int neighborPos, Direction doorTowardPosition, ref int bestDistance)
    {
        if (grid.TryGetValue(neighborPos, out RoomNode neighbor) && HasDoor(neighbor.roomPrefab, doorTowardPosition))
        {
            bestDistance = Mathf.Min(bestDistance, neighbor.distanceFromStart);
        }
    }

    private void RecalculateDistances(Dictionary<Vector2Int, RoomNode> grid)
    {
        foreach (RoomNode node in grid.Values) node.distanceFromStart = int.MaxValue;
        if (!grid.TryGetValue(Vector2Int.zero, out RoomNode startNode)) return;

        Queue<RoomNode> queue = new Queue<RoomNode>();
        startNode.distanceFromStart = 0;
        queue.Enqueue(startNode);

        while (queue.Count > 0)
        {
            RoomNode node = queue.Dequeue();
            VisitConnectedNeighbor(grid, queue, node, Direction.Top, Vector2Int.up);
            VisitConnectedNeighbor(grid, queue, node, Direction.Bottom, Vector2Int.down);
            VisitConnectedNeighbor(grid, queue, node, Direction.Left, Vector2Int.left);
            VisitConnectedNeighbor(grid, queue, node, Direction.Right, Vector2Int.right);
        }

        foreach (RoomNode node in grid.Values)
        {
            if (node.distanceFromStart == int.MaxValue)
            {
                node.distanceFromStart = Mathf.Abs(node.gridPos.x) + Mathf.Abs(node.gridPos.y);
            }
        }
    }

    private void VisitConnectedNeighbor(Dictionary<Vector2Int, RoomNode> grid, Queue<RoomNode> queue, RoomNode node, Direction direction, Vector2Int offset)
    {
        if (!HasDoor(node.roomPrefab, direction)) return;
        if (!grid.TryGetValue(node.gridPos + offset, out RoomNode neighbor)) return;
        if (!HasDoor(neighbor.roomPrefab, Opposite(direction))) return;

        int candidateDistance = node.distanceFromStart + 1;
        if (candidateDistance >= neighbor.distanceFromStart) return;

        neighbor.distanceFromStart = candidateDistance;
        queue.Enqueue(neighbor);
    }

    private static Direction Opposite(Direction direction)
    {
        switch (direction)
        {
            case Direction.Top: return Direction.Bottom;
            case Direction.Bottom: return Direction.Top;
            case Direction.Left: return Direction.Right;
            default: return Direction.Left;
        }
    }

    private static bool HasDoor(Room room, Direction direction)
    {
        if (room == null) return false;
        switch (direction)
        {
            case Direction.Top: return room.hasTopDoor;
            case Direction.Bottom: return room.hasBottomDoor;
            case Direction.Left: return room.hasLeftDoor;
            default: return room.hasRightDoor;
        }
    }

    private void CapOpenConnections(Dictionary<Vector2Int, RoomNode> grid, List<RoomNode> allNodes)
    {
        var existingPositions = grid.Keys.ToList();

        foreach (var pos in existingPositions)
        {
            RoomNode node = grid[pos];
            
            // --- NEW: Skip Boss Rooms ---
            // Prevents putting dead ends on the boss room's extra doors.
            if (node.isBossRoom) continue;

            int dist = node.distanceFromStart + 1;

            if (node.roomPrefab.hasTopDoor)    TryCap(grid, allNodes, pos + Vector2Int.up, Direction.Bottom, dist);
            if (node.roomPrefab.hasBottomDoor) TryCap(grid, allNodes, pos + Vector2Int.down, Direction.Top, dist);
            if (node.roomPrefab.hasLeftDoor)   TryCap(grid, allNodes, pos + Vector2Int.left, Direction.Right, dist);
            if (node.roomPrefab.hasRightDoor)  TryCap(grid, allNodes, pos + Vector2Int.right, Direction.Left, dist);
        }
    }

    private void TryCap(Dictionary<Vector2Int, RoomNode> grid, List<RoomNode> allNodes, Vector2Int pos, Direction requiredDoor, int dist)
    {
        if (grid.ContainsKey(pos) || deadEndRoomPrefabs == null || deadEndRoomPrefabs.Count == 0) return;

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
        if (candidates == null || candidates.Count == 0) return null;
        bool? t = GetRequirement(grid, pos + Vector2Int.up, Direction.Bottom);
        bool? b = GetRequirement(grid, pos + Vector2Int.down, Direction.Top);
        bool? l = GetRequirement(grid, pos + Vector2Int.left, Direction.Right);
        bool? r = GetRequirement(grid, pos + Vector2Int.right, Direction.Left);
        List<Room> valid = candidates.Where(room => room != null && Matches(room.hasTopDoor, t) && Matches(room.hasBottomDoor, b) && Matches(room.hasLeftDoor, l) && Matches(room.hasRightDoor, r)).ToList();
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
        Dictionary<Vector2Int, RoomNode> nodesByPosition = finalLayout.ToDictionary(node => node.gridPos);
        foreach (RoomNode node in finalLayout)
        {
            Room newRoom = Instantiate(node.roomPrefab, new Vector3(node.gridPos.x * roomSize.x, node.gridPos.y * roomSize.y, 0), Quaternion.identity, transform);
            newRoom.name = $"Room_{node.gridPos.x}_{node.gridPos.y}"; newRoom.gridPos = node.gridPos;
            worldGrid[node.gridPos] = newRoom; instantiatedRooms.Add(newRoom);
        }

        foreach (var kvp in worldGrid)
        {
            Vector2Int pos = kvp.Key; Room room = kvp.Value;
            nodesByPosition.TryGetValue(pos, out RoomNode originalNode);
            if (worldGrid.ContainsKey(pos + Vector2Int.up)) room.OpenDoor(Direction.Top);
            if (worldGrid.ContainsKey(pos + Vector2Int.down)) room.OpenDoor(Direction.Bottom);
            if (worldGrid.ContainsKey(pos + Vector2Int.left)) room.OpenDoor(Direction.Left);
            if (worldGrid.ContainsKey(pos + Vector2Int.right)) room.OpenDoor(Direction.Right);

            int dist = originalNode != null ? originalNode.distanceFromStart : Mathf.Abs(pos.x) + Mathf.Abs(pos.y);

            bool allowProps = dist != 0 && (originalNode == null || !originalNode.isBossRoom || spawnPropsInBossRooms) && (preventPropSpawningInRooms == null || originalNode == null || !preventPropSpawningInRooms.Contains(originalNode.roomPrefab));
            if (allowProps && environmentProps != null) room.PopulateRoom(environmentProps, roomSize, propSpawnAttemptsPerUnit);
            if (dist > 0 && enemySpawnList != null && enemySpawnList.Count > 0)
            {
                room.SpawnEnemies(enemySpawnList, baseEnemyBudget + (dist * enemyBudgetPerDistance), roomSize, dist);
            }
        }
    }

    private void AnnounceRun()
    {
        if (!announceRunCode) return;

        GameObject areaTextObject = GameObject.Find("AreaText");
        TMP_Text areaText = areaTextObject != null ? areaTextObject.GetComponent<TMP_Text>() : null;
        if (areaText != null)
        {
            areaText.text = $"CUDDLETOWN  //  RUN {RunCode}";
        }
    }
}
