using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class PathfindingManager : MonoBehaviour
{
    public static PathfindingManager Instance;

    [Header("References")]
    public Tilemap floorLayer;
    public Tilemap wallLayer;

    // The mathematical grid
    private Dictionary<Vector2Int, Node> grid = new Dictionary<Vector2Int, Node>();

    // A simple Node class
    public class Node
    {
        public Vector2Int position;
        public bool isWalkable;
        public Node parent; // For retracing the path

        public Node(Vector2Int pos, bool walkable)
        {
            position = pos;
            isWalkable = walkable;
        }
    }

    void Awake()
    {
        Instance = this;
    }

    // Call this whenever the map changes (Load, Place Furniture)
    public void ScanMap()
    {
        grid.Clear();

        // 1. Get bounds of the floor
        BoundsInt bounds = floorLayer.cellBounds;

        // 2. Loop through every possible tile
        foreach (var pos in bounds.allPositionsWithin)
        {
            Vector3Int tilePos = new Vector3Int(pos.x, pos.y, 0);

            // Basic Rules
            bool hasFloor = floorLayer.HasTile(tilePos);
            bool hasWall = wallLayer.HasTile(tilePos);

            // Determine walkability
            bool walkable = hasFloor && !hasWall;

            // Add to grid
            Vector2Int gridPos = new Vector2Int(pos.x, pos.y);
            grid.Add(gridPos, new Node(gridPos, walkable));
        }

        // 3. Scan for Furniture (Dynamic Obstacles)
        // We find all colliders on the "Furniture" layer or just assume furniture has colliders
        // Since furniture is objects, we can check their positions physically or logically.
        // LOGICAL approach is better: Ask GridManager what furniture exists.

        // For now, let's stick to the Physics check as it's robust:
        // We will do this via Physics2D.OverlapPoint in the actual pathfinding or update nodes here.
        UpdateFurnitureObstacles();
    }

    void UpdateFurnitureObstacles()
    {
        // Check every walkable node to see if furniture is on top of it
        List<Vector2Int> keys = new List<Vector2Int>(grid.Keys);
        foreach (var pos in keys)
        {
            if (grid[pos].isWalkable)
            {
                // Check center of tile for collider
                Vector3 worldPos = floorLayer.GetCellCenterWorld(new Vector3Int(pos.x, pos.y, 0));
                Collider2D col = Physics2D.OverlapPoint(worldPos);

                // If we hit a collider that is NOT the floor/wall (assuming they don't have triggers there)
                // Note: Ensure your furniture has BoxCollider2D (which we did earlier)
                if (col != null && !col.isTrigger)
                {
                    // Block this node
                    grid[pos].isWalkable = false;
                }
            }
        }
    }

    // --- THE PATHFINDING ALGORITHM (BFS) ---
    public List<Vector2Int> FindPath(Vector2Int startPos, Vector2Int targetPos)
    {
        if (!grid.ContainsKey(startPos) || !grid.ContainsKey(targetPos))
        {
            Debug.LogWarning("Start or End point is off-grid.");
            return null;
        }

        // Reset parents
        foreach (var n in grid.Values) n.parent = null;

        Queue<Node> queue = new Queue<Node>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        Node startNode = grid[startPos];
        Node targetNode = grid[targetPos];

        queue.Enqueue(startNode);
        visited.Add(startPos);

        while (queue.Count > 0)
        {
            Node current = queue.Dequeue();

            if (current == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            foreach (Node neighbor in GetNeighbors(current))
            {
                if (!visited.Contains(neighbor.position) && neighbor.isWalkable)
                {
                    visited.Add(neighbor.position);
                    neighbor.parent = current;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return null; // No path found
    }

    // Add this to PathfindingManager.cs
    public Vector2Int GetRandomWalkableNode()
    {
        List<Vector2Int> walkableNodes = new List<Vector2Int>();

        foreach (var kvp in grid)
        {
            if (kvp.Value.isWalkable)
            {
                walkableNodes.Add(kvp.Key);
            }
        }

        if (walkableNodes.Count > 0)
        {
            return walkableNodes[Random.Range(0, walkableNodes.Count)];
        }

        return Vector2Int.zero; // Fallback
    }

    // Helper to check if a specific world position is walkable
    public bool IsWalkable(Vector3 worldPos)
    {
        Vector3Int cellPos = floorLayer.WorldToCell(worldPos);
        Vector2Int gridPos = new Vector2Int(cellPos.x, cellPos.y);

        if (grid.ContainsKey(gridPos))
            return grid[gridPos].isWalkable;

        return false;
    }

    List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>();

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in dirs)
        {
            Vector2Int checkPos = node.position + dir;
            if (grid.ContainsKey(checkPos))
            {
                neighbors.Add(grid[checkPos]);
            }
        }
        return neighbors;
    }

    List<Vector2Int> RetracePath(Node startNode, Node endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode.position);
            currentNode = currentNode.parent;
        }
        path.Reverse();
        return path;
    }
}