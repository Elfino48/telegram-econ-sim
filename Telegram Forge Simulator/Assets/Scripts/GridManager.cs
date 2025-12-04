using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [Header("Tilemaps")]
    public Tilemap grassLayer; // Order 0
    public Tilemap floorLayer; // Order 1
    public Tilemap wallLayer;  // Order 2 (Mode: Individual)

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

    [Header("Furniture & NPCs")]
    public GameObject[] furniturePrefabs;
    public GameObject masterPrefab; // Drag Master Prefab here

    private List<GameObject> activeFurniture = new List<GameObject>();
    private List<GameObject> activeMasters = new List<GameObject>();

    private const int CHUNK_SIZE = 6;

    void Awake()
    {
        Instance = this;
    }

    // --- MAP GENERATION ---

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

        // Create lookup
        HashSet<Vector2Int> ownedChunkPositions = new HashSet<Vector2Int>();
        foreach (var c in ownedChunks) ownedChunkPositions.Add(new Vector2Int(c.x, c.y));

        HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();

        // 2. Loop Chunks
        foreach (Chunk chunk in ownedChunks)
        {
            Vector2Int currentChunkPos = new Vector2Int(chunk.x, chunk.y);

            // A. Paint Floor
            PaintChunkFloor(chunk.x, chunk.y, floorPositions);

            // B. Paint Surroundings (Grass & Signs)
            // Top: Grass Only
            CheckNeighbor(currentChunkPos, new Vector2Int(0, 1), false, ownedChunkPositions);
            // Bottom: Grass + Sign
            CheckNeighbor(currentChunkPos, new Vector2Int(0, -1), true, ownedChunkPositions);
            // Left: Grass + Sign
            CheckNeighbor(currentChunkPos, new Vector2Int(-1, 0), true, ownedChunkPositions);
            // Right: Grass + Sign
            CheckNeighbor(currentChunkPos, new Vector2Int(1, 0), true, ownedChunkPositions);

            // Diagonals (Grass only)
            CheckNeighbor(currentChunkPos, new Vector2Int(-1, 1), false, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(1, 1), false, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(-1, -1), false, ownedChunkPositions);
            CheckNeighbor(currentChunkPos, new Vector2Int(1, -1), false, ownedChunkPositions);
        }

        // 3. Paint Walls
        PaintWalls(floorPositions);

        // 4. Update Pathfinding (Base grid)
        if (PathfindingManager.Instance != null) PathfindingManager.Instance.ScanMap();
    }

    // --- OBJECT SPAWNING ---

    public void SpawnObjects(TelegramUser user)
    {
        // 1. Clear Old Furniture
        foreach (var obj in activeFurniture) if (obj != null) Destroy(obj);
        activeFurniture.Clear();

        // 2. Clear Old Masters
        foreach (var m in activeMasters) if (m != null) Destroy(m);
        activeMasters.Clear();

        // 3. Spawn Furniture
        if (user.objects_list != null)
        {
            foreach (var objData in user.objects_list)
            {
                // Find Prefab by Name
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

                    // Fix Sorting (Ensure it plays nice with Wall Layer which is Order 2)
                    if (newObj.GetComponent<SpriteRenderer>())
                        newObj.GetComponent<SpriteRenderer>().sortingOrder = 2;

                    // Load Smart Data (Resources etc)
                    SmartObject smartObj = newObj.GetComponent<SmartObject>();
                    if (smartObj != null)
                    {
                        Dictionary<string, string> dict = new Dictionary<string, string>();

                        if (objData.data != null && !string.IsNullOrEmpty(objData.data.resources))
                        {
                            dict["resources"] = objData.data.resources;
                        }
                        else
                        {
                            // Default to 10 if missing
                            dict["resources"] = "10";
                        }

                        smartObj.LoadData(dict);
                    }

                    activeFurniture.Add(newObj);
                }
            }
        }

        // 4. Spawn Masters
        if (user.masters_list != null)
        {
            foreach (var masterData in user.masters_list)
            {
                Vector3 pos = new Vector3(masterData.x, masterData.y, 0);
                GameObject newMaster = Instantiate(masterPrefab, pos, Quaternion.identity);

                // Set Name
                MasterNPC npcScript = newMaster.GetComponent<MasterNPC>();
                if (npcScript != null)
                {
                    npcScript.SetDisplayName(masterData.name);
                }

                activeMasters.Add(newMaster);
            }
        }

        // 5. Update Pathfinding (Obstacles changed)
        if (PathfindingManager.Instance != null) PathfindingManager.Instance.ScanMap();
    }

    // Helper for manual hiring to avoid duplicates
    public void RegisterMaster(GameObject masterObj)
    {
        if (!activeMasters.Contains(masterObj))
        {
            activeMasters.Add(masterObj);
        }
    }

    // --- HELPER METHODS ---

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

            // NORTH WALL (Face + Corners)
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

            // SIDE WALLS
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

            // SOUTH WALL (New Bottom Strip)
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