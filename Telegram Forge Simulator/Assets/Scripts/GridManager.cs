using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [Header("Tilemaps")]
    public Tilemap floorLayer;
    public Tilemap wallLayer;

    [Header("Tiles")]
    public TileBase floorTile;
    public TileBase wallTop;
    public TileBase wallLeft;
    public TileBase wallRight;

    private const int CHUNK_SIZE = 6;

    void Awake()
    {
        Instance = this;
    }

    public void GenerateMap(Chunk[] ownedChunks)
    {
        floorLayer.ClearAllTiles();
        wallLayer.ClearAllTiles();

        if (ownedChunks == null || ownedChunks.Length == 0) return;

        HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();

        // 1. Paint Floors & Register Positions
        foreach (Chunk chunk in ownedChunks)
        {
            int startX = chunk.x * CHUNK_SIZE;
            int startY = chunk.y * CHUNK_SIZE;

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

        // 2. Paint Walls (Scan for empty neighbors)
        foreach (Vector2Int pos in floorPositions)
        {
            // Check North (Top Wall)
            Vector2Int north = new Vector2Int(pos.x, pos.y + 1);
            if (!floorPositions.Contains(north))
            {
                wallLayer.SetTile(new Vector3Int(north.x, north.y, 0), wallTop);
            }

            // Check West (Left Wall)
            Vector2Int west = new Vector2Int(pos.x - 1, pos.y);
            if (!floorPositions.Contains(west))
            {
                // We paint ON the empty tile to the left
                wallLayer.SetTile(new Vector3Int(west.x, west.y, 0), wallLeft);
            }

            // Check East (Right Wall)
            Vector2Int east = new Vector2Int(pos.x + 1, pos.y);
            if (!floorPositions.Contains(east))
            {
                // We paint ON the empty tile to the right
                wallLayer.SetTile(new Vector3Int(east.x, east.y, 0), wallRight);
            }
        }
    }
}