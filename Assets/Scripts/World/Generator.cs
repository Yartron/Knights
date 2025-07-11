using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

public class DungeonGenerator : MonoBehaviour
{
    [Header("Tile Settings")]
    public TileBase[] floorTiles;
    public TileBase[] wallTiles;
    public TileBase northWallTile;
    public TileBase[] decorationTiles;

    [Header("Generation Settings")]
    [Range(3, 15)] public int minRooms = 8;
    [Range(5, 20)] public int maxRooms = 12;
    [Range(1, 5)] public int corridorWidth = 3;
    [Range(4, 10)] public int minRoomSize = 6;
    [Range(6, 15)] public int maxRoomSize = 10;
    [Range(10, 20)] public int bossRoomSize = 14;
    [Range(0.05f, 0.3f)] public float branchChance = 0.15f;
    [Range(0.1f, 1f)] public float decorationDensity = 0.2f;

    [Header("Lighting")]
    public GameObject lightPrefab;
    public Color[] lightColors;
    [Range(0.1f, 2f)] public float minIntensity = 0.5f;
    [Range(0.1f, 2f)] public float maxIntensity = 1.5f;
    [Range(0.01f, 0.1f)] public float lightDensity = 0.03f;

    [Header("Enemies & Chests")]
    public GameObject[] enemyPrefabs;
    public GameObject chestPrefab;
    public GameObject bossPrefab;
    [Range(0.1f, 0.5f)] public float enemySpawnChance = 0.3f;
    [Range(0.05f, 0.2f)] public float chestSpawnChance = 0.1f;
    [Range(1, 5)] public int maxEnemiesPerRoom = 3;

    [Header("Decoration Prefabs")]
    public GameObject[] decorationPrefabs; // Префабы декораций
    [Range(0.01f, 0.1f)] public float decorationPrefabDensity = 0.05f; // Плотность префабов
    public float minDecorationDistance = 1.5f; // Минимальное расстояние между декорациями

    [Header("References")]
    public Tilemap floorMap;
    public Tilemap wallMap;
    public Tilemap decorationMap;
    public Transform playerSpawn;

    private HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();
    private List<Room> rooms = new List<Room>();
    private List<RoomConnection> connections = new List<RoomConnection>();
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private Room bossRoom;
    private List<Vector2> decorationPositions = new List<Vector2>(); // Позиции всех декораций

    private const int MAX_BRANCH_DEPTH = 2;
    private static readonly Vector2Int[] cardinalDirections = {
        Vector2Int.up, Vector2Int.right,
        Vector2Int.down, Vector2Int.left
    };

    public void GenerateDungeon()
    {
        ClearMaps();
        GenerateMainPath();
        GenerateBranches();
        GenerateCorridors();
        AddRoomsToFloor();
        AddWalls();
        AddTileDecorations();
        PlaceLights();
        PlaceEnemiesAndChests();
        PlaceDecorationPrefabs(); // Генерация префабных декораций
        SpawnPlayer();
        SpawnBoss();
    }

    private void GenerateMainPath()
    {
        Room startRoom = CreateRoom(Vector2Int.zero, false);
        rooms.Add(startRoom);
        Room currentRoom = startRoom;

        int roomCount = Random.Range(minRooms, maxRooms + 1);

        for (int i = 1; i < roomCount; i++)
        {
            bool isBossRoom = (i == roomCount - 1);
            Room newRoom = null;
            bool roomCreated = false;
            int attempts = 10;

            while (!roomCreated && attempts > 0)
            {
                Vector2Int direction = GetWeightedDirection(currentRoom.position, i, roomCount, false);
                int distance = isBossRoom ?
                    bossRoomSize + corridorWidth + 5 :
                    Random.Range(minRoomSize + corridorWidth, maxRoomSize + corridorWidth);

                Vector2Int newPos = currentRoom.position + direction * distance;
                newRoom = CreateRoom(newPos, isBossRoom);

                if (!RoomOverlaps(newRoom))
                {
                    rooms.Add(newRoom);
                    connections.Add(new RoomConnection(currentRoom, newRoom));
                    currentRoom = newRoom;
                    roomCreated = true;

                    if (isBossRoom) bossRoom = newRoom;
                }
                attempts--;
            }

            if (!roomCreated && isBossRoom)
            {
                Vector2Int newPos = currentRoom.position + Vector2Int.up * (bossRoomSize + corridorWidth + 10);
                newRoom = CreateRoom(newPos, true);
                rooms.Add(newRoom);
                connections.Add(new RoomConnection(currentRoom, newRoom));
                currentRoom = newRoom;
                bossRoom = newRoom;
            }
        }
    }

    private void GenerateBranches()
    {
        List<Room> roomsToProcess = new List<Room>(rooms);
        int branchDepth = 0;

        while (branchDepth < MAX_BRANCH_DEPTH)
        {
            List<Room> newBranches = new List<Room>();

            foreach (Room room in roomsToProcess)
            {
                if (room.isStart || room.isBoss) continue;

                if (Random.value < branchChance)
                {
                    Vector2Int direction = GetWeightedDirection(
                        room.position,
                        branchDepth,
                        MAX_BRANCH_DEPTH,
                        true
                    );

                    int distance = Random.Range(minRoomSize + corridorWidth, maxRoomSize + corridorWidth);
                    Vector2Int newPos = room.position + direction * distance;
                    Room newRoom = CreateRoom(newPos, false);

                    if (!RoomOverlaps(newRoom))
                    {
                        rooms.Add(newRoom);
                        connections.Add(new RoomConnection(room, newRoom));
                        newBranches.Add(newRoom);
                    }
                }
            }

            if (newBranches.Count == 0) break;

            roomsToProcess = newBranches;
            branchDepth++;
        }
    }

    private Vector2Int GetWeightedDirection(Vector2Int currentPos, int progress, int maxProgress, bool isBranch)
    {
        Dictionary<Vector2Int, float> directionWeights = new Dictionary<Vector2Int, float>();

        directionWeights[Vector2Int.right] = isBranch ? 0.7f : 0.8f - progress * 0.05f;
        directionWeights[Vector2Int.up] = 0.6f;
        directionWeights[Vector2Int.down] = 0.6f;
        directionWeights[Vector2Int.left] = isBranch ? 0.5f : 0.1f;

        directionWeights[new Vector2Int(1, 1)] = 0.4f;
        directionWeights[new Vector2Int(1, -1)] = 0.4f;
        directionWeights[new Vector2Int(-1, 1)] = isBranch ? 0.3f : 0.1f;
        directionWeights[new Vector2Int(-1, -1)] = isBranch ? 0.3f : 0.1f;

        float totalWeight = directionWeights.Values.Sum();
        float randomPoint = Random.value * totalWeight;

        foreach (var kvp in directionWeights)
        {
            if (randomPoint < kvp.Value) return kvp.Key;
            randomPoint -= kvp.Value;
        }

        return Vector2Int.right;
    }

    private Room CreateRoom(Vector2Int position, bool isBossRoom)
    {
        int size = isBossRoom ? bossRoomSize : Random.Range(minRoomSize, maxRoomSize);
        return new Room()
        {
            position = position,
            width = size,
            height = size,
            isStart = (position == Vector2Int.zero),
            isBoss = isBossRoom
        };
    }

    private bool RoomOverlaps(Room newRoom)
    {
        foreach (Room existingRoom in rooms)
        {
            float minDistance = (newRoom.width + existingRoom.width) / 2 + corridorWidth + 2;
            if (Vector2Int.Distance(newRoom.position, existingRoom.position) < minDistance)
            {
                return true;
            }
        }
        return false;
    }

    private void GenerateCorridors()
    {
        foreach (var connection in connections)
        {
            ConnectRooms(connection.roomA, connection.roomB);
        }
    }

    private void ConnectRooms(Room a, Room b)
    {
        Vector2Int roomAPoint = new Vector2Int(
            Random.Range(a.position.x - a.width / 4, a.position.x + a.width / 4),
            Random.Range(a.position.y - a.height / 4, a.position.y + a.height / 4)
        );

        Vector2Int roomBPoint = new Vector2Int(
            Random.Range(b.position.x - b.width / 4, b.position.x + b.width / 4),
            Random.Range(b.position.y - b.height / 4, b.position.y + b.height / 4)
        );

        Vector2Int current = roomAPoint;

        while (current.x != roomBPoint.x)
        {
            int move = (current.x < roomBPoint.x) ? 1 : -1;
            current.x += move;
            AddCorridorSection(current);
        }

        while (current.y != roomBPoint.y)
        {
            int move = (current.y < roomBPoint.y) ? 1 : -1;
            current.y += move;
            AddCorridorSection(current);
        }
    }

    private void AddCorridorSection(Vector2Int center)
    {
        int halfWidth = corridorWidth / 2;
        int extra = corridorWidth % 2;

        for (int x = -halfWidth; x <= halfWidth + extra; x++)
        {
            for (int y = -halfWidth; y <= halfWidth + extra; y++)
            {
                Vector2Int position = new Vector2Int(center.x + x, center.y + y);

                if (!IsPositionInAnyRoom(position))
                {
                    floorPositions.Add(position);
                    floorMap.SetTile((Vector3Int)position, GetRandomTile(floorTiles));
                }
            }
        }
    }

    private void AddRoomsToFloor()
    {
        foreach (Room room in rooms)
        {
            for (int x = -room.width / 2; x <= room.width / 2; x++)
            {
                for (int y = -room.height / 2; y <= room.height / 2; y++)
                {
                    Vector2Int position = new Vector2Int(
                        room.position.x + x,
                        room.position.y + y
                    );

                    floorPositions.Add(position);
                    floorMap.SetTile((Vector3Int)position, GetRandomTile(floorTiles));
                }
            }
        }
    }

    private bool IsPositionInAnyRoom(Vector2Int position)
    {
        foreach (Room room in rooms)
        {
            if (Mathf.Abs(position.x - room.position.x) <= room.width / 2 &&
                Mathf.Abs(position.y - room.position.y) <= room.height / 2)
            {
                return true;
            }
        }
        return false;
    }

    private void AddWalls()
    {
        HashSet<Vector2Int> wallCandidates = new HashSet<Vector2Int>();

        foreach (var floorPos in floorPositions)
        {
            foreach (var dir in cardinalDirections)
            {
                Vector2Int wallPos = floorPos + dir;
                if (!floorPositions.Contains(wallPos))
                {
                    wallCandidates.Add(wallPos);
                }
            }
        }

        foreach (var wallPos in wallCandidates)
        {
            Vector2Int belowPos = wallPos + Vector2Int.down;
            bool isNorthWallCandidate = floorPositions.Contains(belowPos);

            if (isNorthWallCandidate)
            {
                Vector2Int abovePos = wallPos + Vector2Int.up;
                bool hasFloorAbove = floorPositions.Contains(abovePos);

                if (!hasFloorAbove)
                {
                    wallMap.SetTile((Vector3Int)wallPos, northWallTile);
                    continue;
                }
            }

            wallMap.SetTile((Vector3Int)wallPos, GetRandomTile(wallTiles));
        }
    }

    private void AddTileDecorations()
    {
        foreach (var position in floorPositions)
        {
            if (Random.value < decorationDensity &&
                !IsNearWall(position) &&
                !rooms[0].ContainsPosition(position))
            {
                decorationMap.SetTile(
                    (Vector3Int)position,
                    GetRandomTile(decorationTiles)
                );
            }
        }
    }

    private bool IsNearWall(Vector2Int position)
    {
        foreach (var dir in cardinalDirections)
        {
            Vector2Int checkPos = position + dir;
            if (wallMap.HasTile((Vector3Int)checkPos))
                return true;
        }
        return false;
    }

    private void PlaceLights()
    {
        foreach (var pos in floorPositions)
        {
            if (Random.value < lightDensity && !IsNearWall(pos))
            {
                Vector2 worldPos = new Vector2(pos.x + 0.5f, pos.y + 0.5f);
                GameObject lightObj = Instantiate(
                    lightPrefab,
                    worldPos,
                    Quaternion.identity,
                    transform
                );

                spawnedObjects.Add(lightObj);

                Light2D lightComp = lightObj.GetComponent<Light2D>();
                if (lightComp != null)
                {
                    lightComp.color = lightColors[Random.Range(0, lightColors.Length)];
                    lightComp.intensity = Random.Range(minIntensity, maxIntensity);
                    lightComp.pointLightOuterRadius = Random.Range(15f, 30f);
                }
            }
        }

        if (bossRoom != null)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 lightPos = new Vector2(
                    bossRoom.position.x + Random.Range(-bossRoom.width / 4f, bossRoom.width / 4f),
                    bossRoom.position.y + Random.Range(-bossRoom.height / 4f, bossRoom.height / 4f)
                );

                GameObject lightObj = Instantiate(
                    lightPrefab,
                    lightPos,
                    Quaternion.identity,
                    transform
                );

                Light2D lightComp = lightObj.GetComponent<Light2D>();
                if (lightComp != null)
                {
                    lightComp.color = Color.red;
                    lightComp.intensity = Random.Range(1.5f, 2f);
                    lightComp.pointLightOuterRadius = Random.Range(15f, 27f);
                }
            }
        }
    }

    private void PlaceEnemiesAndChests()
    {
        foreach (Room room in rooms)
        {
            if (room.isStart) continue;

            if (!room.isBoss && Random.value < chestSpawnChance)
            {
                Vector2 chestPos = GetRandomPositionInRoom(room);
                GameObject chest = Instantiate(chestPrefab, chestPos, Quaternion.identity);
                spawnedObjects.Add(chest);
                decorationPositions.Add(chestPos); // Запоминаем позицию
            }

            if (!room.isBoss && Random.value < enemySpawnChance)
            {
                int enemyCount = Random.Range(1, maxEnemiesPerRoom + 1);
                for (int i = 0; i < enemyCount; i++)
                {
                    Vector2 enemyPos = GetRandomPositionInRoom(room);
                    GameObject enemy = Instantiate(
                        enemyPrefabs[Random.Range(0, enemyPrefabs.Length)],
                        enemyPos,
                        Quaternion.identity
                    );
                    spawnedObjects.Add(enemy);
                    decorationPositions.Add(enemyPos); // Запоминаем позицию
                }
            }
        }
    }

    private void PlaceDecorationPrefabs()
    {
        if (decorationPrefabs == null || decorationPrefabs.Length == 0) return;

        foreach (Room room in rooms)
        {
            // Пропускаем стартовую комнату
            if (room.isStart) continue;

            // Рассчитываем количество декораций для комнаты
            int decorationCount = Mathf.RoundToInt(room.width * room.height * decorationPrefabDensity);

            for (int i = 0; i < decorationCount; i++)
            {
                Vector2 position = GetRandomPositionInRoom(room);

                // Проверяем, что позиция подходит
                if (IsPositionValidForDecoration(position))
                {
                    GameObject prefab = decorationPrefabs[Random.Range(0, decorationPrefabs.Length)];
                    GameObject decoration = Instantiate(prefab, position, Quaternion.identity);


                    spawnedObjects.Add(decoration);
                    decorationPositions.Add(position); // Запоминаем позицию
                }
            }
        }
    }

    private bool IsPositionValidForDecoration(Vector2 position)
    {
        // Проверка расстояния до других объектов
        foreach (Vector2 existingPos in decorationPositions)
        {
            if (Vector2.Distance(position, existingPos) < minDecorationDistance)
            {
                return false;
            }
        }

        // Проверка, что не у стены
        Vector2Int gridPos = new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        if (IsNearWall(gridPos))
        {
            return false;
        }

        // Проверка, что не в коридоре (если нужно)
        if (IsPositionInCorridor(gridPos))
        {
            return false;
        }

        return true;
    }

    private bool IsPositionInCorridor(Vector2Int position)
    {
        foreach (Room room in rooms)
        {
            if (room.ContainsPosition(position))
            {
                return false;
            }
        }
        return true;
    }

    private Vector2 GetRandomPositionInRoom(Room room)
    {
        return new Vector2(
            room.position.x + Random.Range(-room.width / 3f, room.width / 3f),
            room.position.y + Random.Range(-room.height / 3f, room.height / 3f)
        );
    }

    private void SpawnPlayer()
    {
        if (rooms.Count > 0)
        {
            Vector2 spawnPos = new Vector2(rooms[0].position.x, rooms[0].position.y);
            playerSpawn.position = new Vector3(spawnPos.x, spawnPos.y, 0);
            decorationPositions.Add(spawnPos); // Запоминаем позицию игрока
        }
    }

    private void SpawnBoss()
    {
        if (bossRoom != null && bossPrefab != null)
        {
            Vector2 bossPos = new Vector2(bossRoom.position.x, bossRoom.position.y);
            GameObject boss = Instantiate(bossPrefab, bossPos, Quaternion.identity);
            spawnedObjects.Add(boss);
            decorationPositions.Add(bossPos); // Запоминаем позицию босса
        }
        else
        {
            Debug.LogError("Boss room or boss prefab is missing!");
        }
    }

    private TileBase GetRandomTile(TileBase[] tiles)
    {
        if (tiles == null || tiles.Length == 0) return null;
        return tiles[Random.Range(0, tiles.Length)];
    }

    private void ClearMaps()
    {
        floorPositions.Clear();
        rooms.Clear();
        connections.Clear();
        bossRoom = null;
        decorationPositions.Clear();

        floorMap.ClearAllTiles();
        wallMap.ClearAllTiles();
        decorationMap.ClearAllTiles();

        foreach (var obj in spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedObjects.Clear();

        foreach (Transform child in transform)
        {
            if (child != transform) Destroy(child.gameObject);
        }
    }

    private void Start() => GenerateDungeon();

    private class Room
    {
        public Vector2Int position;
        public int width;
        public int height;
        public bool isStart;
        public bool isBoss;

        public bool ContainsPosition(Vector2Int pos)
        {
            return Mathf.Abs(pos.x - position.x) <= width / 2 &&
                   Mathf.Abs(pos.y - position.y) <= height / 2;
        }
    }

    private class RoomConnection
    {
        public Room roomA;
        public Room roomB;

        public RoomConnection(Room a, Room b)
        {
            roomA = a;
            roomB = b;
        }
    }
}