using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [Header("Tilemaps")]
    public Tilemap grassLayer;
    public Tilemap floorLayer;
    public Tilemap wallLayer;

    [Header("Tiles")]
    public TileBase grassTile;
    public TileBase floorTile;
    public TileBase wallTop;
    public TileBase wallLeft;
    public TileBase wallRight;
    public TileBase wallBottom;
    public TileBase wallTopLeft;
    public TileBase wallTopRight;

    [Header("Prefabs")]
    public GameObject signPrefab;
    private List<GameObject> activeSigns = new List<GameObject>();

    [Header("Furniture")]
    public GameObject[] furniturePrefabs;
    private List<GameObject> activeFurniture = new List<GameObject>();

    private const int CHUNK_SIZE = 6;

    void Awake()
    {
        Instance = this;
    }

    public void GenerateMap(Chunk[] ownedChunks)
    {
        grassLayer.ClearAllTiles();
        floorLayer.ClearAllTiles();
        wallLayer.ClearAllTiles();

        foreach (var sign in activeSigns)
        {
            if (sign != null) Destroy(sign);
        }
        activeSigns.Clear();

        if (ownedChunks == null || ownedChunks.Length == 0) return;

        HashSet<Vector2Int> ownedChunkPositions = new HashSet<Vector2Int>();
        foreach (var c in ownedChunks) ownedChunkPositions.Add(new Vector2Int(c.x, c.y));

        HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();

        foreach (Chunk chunk in ownedChunks)
        {
            Vector2Int currentChunkPos = new Vector2Int(chunk.x, chunk.y);

            PaintChunkFloor(chunk.x, chunk.y, floorPositions);

            CheckNeighbor(currentChunkPos, new Vector2Int(0, 1), false, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(0, -1), true, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(-1, 0), true, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(1, 0), true, ownedChunkPositions);

            CheckNeighbor(currentChunkPos, new Vector2Int(-1, 1), false, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(1, 1), false, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(-1, -1), false, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(1, -1), false, ownedChunkPositions);
        }

        PaintWalls(floorPositions);
    }

    public void SpawnObjects(TelegramUser user)
    {
        foreach (var obj in activeFurniture) Destroy(obj);
        activeFurniture.Clear();

        if (user.objects_list == null) return;

        foreach (var objData in user.objects_list)
        {
            GameObject prefabToSpawn = null;
            foreach (var p in furniturePrefabs)
            {
                if (p.name == objData.type_id || p.name + "(Clone)" == objData.type_id)
                {
                    prefabToSpawn = p;
                    break;
                }
            }

            if (prefabToSpawn != null)
            {
                Vector3 pos = new Vector3(objData.x, objData.y, 0);
                GameObject newObj = Instantiate(prefabToSpawn, pos, Quaternion.identity);
                activeFurniture.Add(newObj);
            }
        }
    }

    void CheckNeighbor(Vector2Int center, Vector2Int offset, bool spawnSign, HashSet<Vector2Int> ownedPositions)
    {
        Vector2Int neighborPos = center + offset;

        if (!ownedPositions.Contains(neighborPos))
        {
            PaintChunkGrass(neighborPos.x, neighborPos.y);
            if (spawnSign)
            {
                SpawnSignAtChunk(neighborPos.x, neighborPos.y);
            }
        }
    }

    void PaintChunkFloor(int cx, int cy, HashSet<Vector2Int> floorPositions)
    {
        int startX = cx * CHUNK_SIZE;
        int startY = cy * CHUNK_SIZE;
        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int y = 0; y < CHUNK_SIZE; y++)
            {
                Vector3Int pos = new Vector3Int(startX + x, startY + y, 0);
                floorLayer.SetTile(pos, floorTile);
                floorPositions.Add(new Vector2Int(pos.x, pos.y));
            }
        }
    }

    void PaintChunkGrass(int cx, int cy)
    {
        int startX = cx * CHUNK_SIZE;
        int startY = cy * CHUNK_SIZE;
        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int y = 0; y < CHUNK_SIZE; y++)
            {
                Vector3Int pos = new Vector3Int(startX + x, startY + y, 0);
                if (!grassLayer.HasTile(pos))
                    grassLayer.SetTile(pos, grassTile);
            }
        }
    }

    void SpawnSignAtChunk(int cx, int cy)
    {
        foreach (var s in activeSigns)
        {
            if (s == null) continue;
            ExpansionSign es = s.GetComponent<ExpansionSign>();
            if (es.chunkX == cx && es.chunkY == cy) return;
        }

        float centerX = (cx * CHUNK_SIZE) + (CHUNK_SIZE / 2f);
        float centerY = (cy * CHUNK_SIZE) + (CHUNK_SIZE / 2f);

        Vector3 pos = new Vector3(centerX - 0.5f, centerY - 0.5f, 0);

        GameObject sign = Instantiate(signPrefab, pos, Quaternion.identity);
        ExpansionSign script = sign.GetComponent<ExpansionSign>();
        script.chunkX = cx;
        script.chunkY = cy;

        activeSigns.Add(sign);
    }

    void PaintWalls(HashSet<Vector2Int> floorPositions)
    {
        foreach (Vector2Int pos in floorPositions)
        {
            Vector2Int north = new Vector2Int(pos.x, pos.y + 1);
            Vector2Int west = new Vector2Int(pos.x - 1, pos.y);
            Vector2Int east = new Vector2Int(pos.x + 1, pos.y);
            Vector2Int south = new Vector2Int(pos.x, pos.y - 1);

            bool hasFloorNorth = floorPositions.Contains(north);
            bool hasFloorWest = floorPositions.Contains(west);
            bool hasFloorEast = floorPositions.Contains(east);
            bool hasFloorSouth = floorPositions.Contains(south);

            if (!hasFloorNorth)
            {
                wallLayer.SetTile((Vector3Int)north, wallTop);

                Vector2Int northWest = new Vector2Int(north.x - 1, north.y);
                if (!floorPositions.Contains(northWest))
                {
                    if (!floorPositions.Contains(new Vector2Int(northWest.x, northWest.y - 1)))
                    {
                        wallLayer.SetTile((Vector3Int)northWest, wallLeft);
                        wallLayer.SetTile((Vector3Int)new Vector2Int(northWest.x, northWest.y + 1), wallLeft);
                    }
                }

                Vector2Int northEast = new Vector2Int(north.x + 1, north.y);
                if (!floorPositions.Contains(northEast))
                {
                    if (!floorPositions.Contains(new Vector2Int(northEast.x, northEast.y - 1)))
                    {
                        wallLayer.SetTile((Vector3Int)northEast, wallRight);
                        wallLayer.SetTile((Vector3Int)new Vector2Int(northEast.x, northEast.y + 1), wallRight);
                    }
                }
            }

            if (!hasFloorWest)
            {
                if (!wallLayer.HasTile((Vector3Int)west))
                    wallLayer.SetTile((Vector3Int)west, wallLeft);
            }

            if (!hasFloorEast)
            {
                if (!wallLayer.HasTile((Vector3Int)east))
                    wallLayer.SetTile((Vector3Int)east, wallRight);
            }

            if (!hasFloorSouth)
            {
                if (!wallLayer.HasTile((Vector3Int)south))
                {
                    wallLayer.SetTile((Vector3Int)south, wallBottom);
                }
            }
        }
    }
}