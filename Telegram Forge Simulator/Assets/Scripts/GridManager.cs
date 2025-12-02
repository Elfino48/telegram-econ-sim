using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [Header("Tilemaps")]
    public Tilemap grassLayer; // Order 0
    public Tilemap floorLayer; // Order 1
    public Tilemap wallLayer;  // Order 2

    [Header("Tiles")]
    public TileBase grassTile;
    public TileBase floorTile;
    public TileBase wallTop;
    public TileBase wallLeft;
    public TileBase wallRight;
    public TileBase wallBottom; // NEW: Bottom Wall Slice
    public TileBase wallTopLeft;
    public TileBase wallTopRight;

    [Header("Prefabs")]
    public GameObject signPrefab;
    private List<GameObject> activeSigns = new List<GameObject>();

    private const int CHUNK_SIZE = 6;

    void Awake()
    {
        Instance = this;
    }

    public void GenerateMap(Chunk[] ownedChunks)
    {
        // 1. Clear everything
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

        // 2. Loop Chunks to Paint Core Base & Grass
        foreach (Chunk chunk in ownedChunks)
        {
            Vector2Int currentChunkPos = new Vector2Int(chunk.x, chunk.y);

            // A. Paint The Floor
            PaintChunkFloor(chunk.x, chunk.y, floorPositions);

            // B. Check Neighbors for Grass & Signs
            CheckNeighbor(currentChunkPos, new Vector2Int(0, 1), false, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(0, -1), true, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(-1, 0), true, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(1, 0), true, ownedChunkPositions);

            // Diagonals
            CheckNeighbor(currentChunkPos, new Vector2Int(-1, 1), false, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(1, 1), false, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(-1, -1), false, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(1, -1), false, ownedChunkPositions);
        }

        // 3. Paint Walls
        PaintWalls(floorPositions);
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

            // NEW: South Check
            Vector2Int south = new Vector2Int(pos.x, pos.y - 1);

            bool hasFloorNorth = floorPositions.Contains(north);
            bool hasFloorWest = floorPositions.Contains(west);
            bool hasFloorEast = floorPositions.Contains(east);
            bool hasFloorSouth = floorPositions.Contains(south);

            // --- NORTH WALLS (Face) ---
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

            // --- WEST WALLS ---
            if (!hasFloorWest)
            {
                if (!wallLayer.HasTile((Vector3Int)west))
                    wallLayer.SetTile((Vector3Int)west, wallLeft);
            }

            // --- EAST WALLS ---
            if (!hasFloorEast)
            {
                if (!wallLayer.HasTile((Vector3Int)east))
                    wallLayer.SetTile((Vector3Int)east, wallRight);
            }

            // --- SOUTH WALLS (NEW) ---
            if (!hasFloorSouth)
            {
                // We paint directly ON the empty tile below us
                if (!wallLayer.HasTile((Vector3Int)south))
                {
                    wallLayer.SetTile((Vector3Int)south, wallBottom);
                }
            }
        }
    }
}