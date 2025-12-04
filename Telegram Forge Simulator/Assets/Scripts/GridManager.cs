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
        PathfindingManager.Instance.ScanMap();
    }

    // This spawns Furniture (with data) AND Masters
    public void SpawnObjects(TelegramUser user)
    {
        // 1. CLEAR OLD FURNITURE
        foreach (var obj in activeFurniture)
        {
            if (obj != null) Destroy(obj);
        }
        activeFurniture.Clear();

        // 2. SPAWN FURNITURE
        if (user.objects_list != null)
        {
            foreach (var objData in user.objects_list)
            {
                // Find correct prefab by name
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

                    // Sorting Fix
                    if (newObj.GetComponent<SpriteRenderer>())
                        newObj.GetComponent<SpriteRenderer>().sortingOrder = 2;

                    // --- DATA INJECTION FIX ---
                    SmartObject smartObj = newObj.GetComponent<SmartObject>();
                    if (smartObj != null)
                    {
                        Dictionary<string, string> dict = new Dictionary<string, string>();

                        // If data exists on server, use it. Otherwise default to "10".
                        if (objData.data != null && !string.IsNullOrEmpty(objData.data.resources))
                        {
                            dict["resources"] = objData.data.resources;
                        }
                        else
                        {
                            // RULE: All chests default to 10 resources if undefined
                            dict["resources"] = "10";
                        }

                        smartObj.LoadData(dict); // Updates the text mesh immediately
                    }

                    activeFurniture.Add(newObj);
                }
            }
        }

        // 3. CLEAR OLD MASTERS
        foreach (var m in activeMasters)
        {
            if (m != null) Destroy(m);
        }
        activeMasters.Clear();

        // 4. SPAWN MASTERS
        if (user.masters_list != null)
        {
            foreach (var masterData in user.masters_list)
            {
                Vector3 pos = new Vector3(masterData.x, masterData.y, 0);
                GameObject newMaster = Instantiate(masterPrefab, pos, Quaternion.identity);
                activeMasters.Add(newMaster);
            }
        }

        // 5. UPDATE PATHFINDING
        // Important: Re-scan the map now that new furniture is blocking tiles
        if (PathfindingManager.Instance != null)
        {
            PathfindingManager.Instance.ScanMap();
        }
    }

    public void RegisterMaster(GameObject masterObj)
    {
        if (!activeMasters.Contains(masterObj))
        {
            activeMasters.Add(masterObj);
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
        // Don't spawn duplicates
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

    [Header("NPCs")]
    public GameObject masterPrefab;
    private List<GameObject> activeMasters = new List<GameObject>();

    public void SpawnMasters(TelegramUser user)
    {
        // Clear old
        foreach (var m in activeMasters) if (m != null) Destroy(m);
        activeMasters.Clear();

        if (user.masters_list == null) return;

        foreach (var masterData in user.masters_list)
        {
            Vector3 pos = new Vector3(masterData.x, masterData.y, 0);
            GameObject newMaster = Instantiate(masterPrefab, pos, Quaternion.identity);
            activeMasters.Add(newMaster);
        }
    }
}