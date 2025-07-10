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
    public int iterations = 100;
    public int walkLength = 30;
    public int corridorWidth = 3;
    public int minRoomSize = 6;
    public int maxRoomSize = 12;
    public int bossRoomSize = 15;
    [Range(0.1f, 1f)] public float decorationDensity = 0.2f;

    [Header("Lighting")]
    public Light2D lightPrefab;
    public Color[] lightColors;
    [Range(0.1f, 2f)] public float minIntensity = 0.5f;
    [Range(0.1f, 2f)] public float maxIntensity = 1.5f;

    [Header("References")]
    public Tilemap floorMap;
    public Tilemap wallMap;
    public Tilemap decorationMap;
    public Transform playerSpawn;
    public Transform bossRoomCenter;

    private HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> roomCenters = new HashSet<Vector2Int>();
    private List<BoundsInt> rooms = new List<BoundsInt>();
    private Vector2Int startPosition;
    private Vector2Int bossRoomPosition;

    private static readonly Vector2Int[] directions = {
        Vector2Int.up, Vector2Int.right,
        Vector2Int.down, Vector2Int.left
    };

    public void GenerateDungeon()
    {
        ClearMaps();
        RunProceduralGeneration();
        AddWalls();
        AddDecorations();
        PlaceLights();
        SpawnPlayer();
    }

    private void RunProceduralGeneration()
    {
        // Основной алгоритм случайного блуждания
        HashSet<Vector2Int> path = CreateRandomWalkPath();
        floorPositions.UnionWith(path);

        // Генерация комнат
        GenerateRooms();

        // Поиск самой дальней точки для босса
        FindBossRoomLocation();
    }

    private HashSet<Vector2Int> CreateRandomWalkPath()
    {
        var path = new HashSet<Vector2Int>();
        var currentPosition = Vector2Int.zero;
        startPosition = currentPosition;

        for (int i = 0; i < iterations; i++)
        {
            var walk = GenerateRandomWalk(currentPosition);
            path.UnionWith(walk);
            currentPosition = walk.Last();
            roomCenters.Add(currentPosition);
        }

        return path;
    }

    private IEnumerable<Vector2Int> GenerateRandomWalk(Vector2Int start)
    {
        var path = new List<Vector2Int>();
        var current = start;

        for (int i = 0; i < walkLength; i++)
        {
            Vector2Int direction = directions[Random.Range(0, directions.Length)];

            // Создание широких коридоров
            for (int w = 0; w < corridorWidth; w++)
            {
                Vector2Int offset = w > 0 ?
                    new Vector2Int(-direction.y, direction.x) : Vector2Int.zero;

                path.Add(current + offset);
            }

            current += direction * corridorWidth;
        }

        return path;
    }

    private void GenerateRooms()
    {
        foreach (var center in roomCenters)
        {
            int width = center == bossRoomPosition ?
                bossRoomSize : Random.Range(minRoomSize, maxRoomSize);
            int height = center == bossRoomPosition ?
                bossRoomSize : Random.Range(minRoomSize, maxRoomSize);

            var roomBounds = new BoundsInt(
                center.x - width / 2,
                center.y - height / 2,
                0,
                width,
                height,
                1
            );

            rooms.Add(roomBounds);
            AddRoomToFloor(roomBounds);
        }
    }

    private void AddRoomToFloor(BoundsInt room)
    {
        for (int x = room.xMin; x < room.xMax; x++)
        {
            for (int y = room.yMin; y < room.yMax; y++)
            {
                floorPositions.Add(new Vector2Int(x, y));
            }
        }
    }

    private void FindBossRoomLocation()
    {
        bossRoomPosition = floorPositions
            .OrderByDescending(pos => Vector2Int.Distance(startPosition, pos))
            .First();

        bossRoomCenter.position = new Vector3(bossRoomPosition.x, bossRoomPosition.y, 0);
    }

    private void AddWalls()
    {
        foreach (var position in floorPositions)
        {
            foreach (var direction in directions)
            {
                var neighborPos = position + direction;
                if (!floorPositions.Contains(neighborPos))
                {
                    // Особые стены для северной стороны
                    TileBase wallTile = (direction == Vector2Int.up) ?
                        northWallTile : GetRandomTile(wallTiles);

                    wallMap.SetTile((Vector3Int)neighborPos, wallTile);
                }
            }
        }
    }

    private void AddDecorations()
    {
        foreach (var position in floorPositions)
        {
            if (Random.value < decorationDensity &&
                !IsNearWall(position) &&
                position != (Vector2Int)startPosition)
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
        return directions.Any(dir =>
            wallMap.HasTile((Vector3Int)(position + dir)));
    }

    private void PlaceLights()
    {
        foreach (var room in rooms)
        {
            int lightsCount = room.size.x > bossRoomSize - 3 ? 5 :
                Random.Range(1, 4);

            for (int i = 0; i < lightsCount; i++)
            {
                Vector2 lightPos = new Vector2(
                    Random.Range(room.xMin + 1, room.xMax - 1),
                    Random.Range(room.yMin + 1, room.yMax - 1)
                );

                Light2D light = Instantiate(lightPrefab, lightPos, Quaternion.identity, transform);

                light.color = lightColors[Random.Range(0, lightColors.Length)];
                light.intensity = Random.Range(minIntensity, maxIntensity);
                light.pointLightOuterRadius = Random.Range(3f, 8f);
            }
        }
    }

    private void SpawnPlayer()
    {
        playerSpawn.position = new Vector3(startPosition.x, startPosition.y, 0);
    }

    private TileBase GetRandomTile(TileBase[] tiles)
    {
        return tiles.Length > 0 ?
            tiles[Random.Range(0, tiles.Length)] : null;
    }

    private void ClearMaps()
    {
        floorPositions.Clear();
        roomCenters.Clear();
        rooms.Clear();

        floorMap.ClearAllTiles();
        wallMap.ClearAllTiles();
        decorationMap.ClearAllTiles();
    }

    private void Start() => GenerateDungeon();
}