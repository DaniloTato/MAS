using UnityEngine;
using System.Collections.Generic;

public struct CellInfo
{
    public bool hasAgent;
    public bool isWalkable;
    public Plantation plantation;
}

// Renders the simulation
// Maintain global shared context: grid and grid information
public class SharedContext : MonoBehaviour
{
    public static SharedContext Instance { get; private set; }

    private int width;
    private int height;
    private int tileSize;
    private Vector3 reference;

    private CellInfo[,] grid;

    private TomatoDeposit healthyDeposit;
    private TomatoDeposit rottenDeposit;
    private Vector2Int healthyDepositCoord;
    private Vector2Int rottenDepositCoord;
    private bool hasHealthyDeposit;
    private bool hasRottenDeposit;

    private Dictionary<int, Vector2Int> agentCoords = new();

    private Vector3 right;  // +x
    private Vector3 down;   // -z

    // NEW: shared 4-neighbour directions
    private static readonly Vector2Int[] CardinalDirs = new[]
    {
        new Vector2Int( 1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int( 0, 1),
        new Vector2Int( 0,-1)
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CommunicationBus.Instance.OnMessage += OnMessageReceived;
    }

    private void OnDestroy()
    {
        if (CommunicationBus.Instance != null)
            CommunicationBus.Instance.OnMessage -= OnMessageReceived;
    }

    private void OnMessageReceived(Message msg)
    {
        if (msg.ReceiverId != -1)
            return;

        switch (msg.Type)
        {
            case MessageType.HEARTBEAT:
                SetAgentPresence(msg.SenderId, msg.Pos);
                break;

            case MessageType.INIT_HARVEST:
                {
                    Vector2Int coord = WorldToGrid(msg.Pos);
                    if (InBounds(coord))
                    {
                        Plantation p = GetPlantationFromAgentCell(coord);
                        if (p != null)
                            p.collecting = true;
                    }
                    break;
                }

            case MessageType.STOP_HARVEST:
                {
                    Vector2Int coord = WorldToGrid(msg.Pos);
                    if (InBounds(coord))
                    {
                        Plantation p = GetPlantationFromAgentCell(coord);
                        if (p != null)
                            p.collecting = false;
                    }
                    break;
                }

            case MessageType.SCAN_PLANTATION:
                ScanPlantation(msg.Pos);
                break;

            // NEW: process map scan info sent by MapScanAgent
            case MessageType.SCAN_TILE:
                HandleMapScanIteration(msg);
                break;
        }
    }

    #region Initialization

    public void Initialize(GridParameters parameters)
    {
        width = parameters.width;
        height = parameters.height;
        tileSize = parameters.tileSize;

        Transform refTransform = parameters.gridReference.transform;
        reference = refTransform.position;
        right = refTransform.right.normalized;
        down = -refTransform.forward.normalized;

        grid = new CellInfo[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y].isWalkable = true;
            }
        }
    }

    private void RegisterDeposit(TomatoDeposit deposit)
    {
        if (deposit == null)
            return;

        Vector2Int depositCoord = WorldToGrid(deposit.transform.position);

        if (deposit.rotten)
        {
            rottenDeposit = deposit;
            rottenDepositCoord = depositCoord;
            hasRottenDeposit = true;
        }
        else
        {
            healthyDeposit = deposit;
            healthyDepositCoord = depositCoord;
            hasHealthyDeposit = true;
        }

        if (InBounds(depositCoord))
            grid[depositCoord.x, depositCoord.y].isWalkable = false;
    }

    private void RegisterPlantation(Plantation plantation)
    {
        if (plantation == null)
            return;

        var coord = WorldToGrid(plantation.transform.position);
        if (!InBounds(coord))
            return;

        grid[coord.x, coord.y].isWalkable = false;
        grid[coord.x, coord.y].plantation = plantation;
    }

    #endregion

    #region Context Queries (public world-space API)

    /// <summary>
    /// Returns a world-space path (Y = 0) from 'from' to a fixed
    /// map edge cell (currently (0,0)) using a simple Manhattan walk.
    /// Used by MapScanAgent to reach the starting edge.
    /// </summary>
    public List<Vector3> PathToMapEdge(Vector3 from)
    {
        var path = new List<Vector3>();

        Vector2Int start = WorldToGrid(from);
        start = ClampToBounds(start);

        // Chosen edge: top-left corner (0,0)
        Vector2Int edge = new Vector2Int(0, 0);

        Vector2Int current = start;
        path.Add(ToWorldCoord(current, 0f));

        // Move in X until we hit edge.x
        while (current.x != edge.x)
        {
            current.x += current.x < edge.x ? 1 : -1;
            path.Add(ToWorldCoord(current, 0f));
        }

        // Then move in Y until we hit edge.y
        while (current.y != edge.y)
        {
            current.y += current.y < edge.y ? 1 : -1;
            path.Add(ToWorldCoord(current, 0f));
        }

        return path;
    }

    /// <summary>
    /// Returns a traversal path in world space (Y = 0) that starts at the
    /// same map edge used by PathToMapEdge (cell (0,0)) and visits every
    /// grid cell exactly once in a boustrophedon pattern.
    /// Used by MapScanAgent once it is at the edge.
    /// </summary>
    public List<Vector3> MapScanTraversalFromEdge()
    {
        var worldPath = new List<Vector3>(width * height);

        for (int y = 0; y < height; y++)
        {
            if (y % 2 == 0)
            {
                // left to right
                for (int x = 0; x < width; x++)
                    worldPath.Add(ToWorldCoord(new Vector2Int(x, y), 0f));
            }
            else
            {
                // right to left
                for (int x = width - 1; x >= 0; x--)
                    worldPath.Add(ToWorldCoord(new Vector2Int(x, y), 0f));
            }
        }

        return worldPath;
    }

    public int DepositTomatoes(int quantity, bool rottenType)
    {
        if (quantity <= 0)
            return 0;

        bool hasDeposit = rottenType ? hasRottenDeposit : hasHealthyDeposit;
        if (!hasDeposit)
            return 0;

        TomatoDeposit deposit = rottenType ? rottenDeposit : healthyDeposit;
        if (deposit == null)
            return 0;

        // The actual position of the agent is checked by the cutter via IsAtDeposit.
        deposit.DepositTomate(quantity);
        return quantity;
    }

    // CHANGED: now supports interacting from an adjacent tile
    public int GetTomatoesAt(Vector3 worldPos, bool rottenType)
    {
        Vector2Int coord = WorldToGrid(worldPos);
        Plantation p = GetPlantationFromAgentCell(coord); // NEW helper
        if (p == null) return 0;

        return rottenType ? p.RottenTomatoes : p.HealthyTomatoes;
    }

    // CHANGED: now supports harvesting from an adjacent tile
    public int HarvestTomatoesAt(Vector3 worldPos, bool rottenType, int maxAmount)
    {
        if (maxAmount <= 0) return 0;

        Vector2Int coord = WorldToGrid(worldPos);
        Plantation p = GetPlantationFromAgentCell(coord); // NEW helper
        if (p == null) return 0;

        int available = rottenType ? p.RottenTomatoes : p.HealthyTomatoes;
        int toHarvest = Mathf.Min(available, maxAmount);
        if (toHarvest <= 0) return 0;

        p.Harvest(toHarvest, rottenType);
        return toHarvest;
    }

    /// <summary>
    /// Returns the world-space center of the cell that contains the given world position.
    /// Y will be preserved from the input.
    /// </summary>
    public Vector3 CenterInCell(Vector3 worldPos)
    {
        Vector2Int coord = WorldToGrid(worldPos);
        Vector3 center = ToWorldCoord(coord, worldPos.y);
        return center;
    }

    /// <summary>
    /// Convert a grid coordinate to a world-space position.
    /// Y is explicitly provided, XZ is computed from the grid.
    /// </summary>
    public Vector3 ToWorldCoord(Vector2Int coord, float y = 0f)
    {
        coord = ClampToBounds(coord);

        float offsetX = (coord.x + 0.5f) * tileSize;
        float offsetZ = (coord.y + 0.5f) * tileSize;

        Vector3 world = reference + right * offsetX + down * offsetZ;
        world.y = y;
        return world;
    }

    /// <summary>
    /// Returns a cyclic path in world space (Y = 0) starting at startPos,
    /// visiting all plantations and returning to the start (if possible).
    /// (Used by drone scanner, semantics unchanged.)
    /// </summary>
    public List<Vector3> CycleThroughPlantations(Vector3 startPos)
    {
        Vector2Int startCoord = WorldToGrid(startPos);
        if (!InBounds(startCoord))
            startCoord = ClampToBounds(startCoord);

        List<Vector2Int> gridPath = CycleThroughPlantationsGrid(startCoord);

        List<Vector3> worldPath = new();
        foreach (var c in gridPath)
            worldPath.Add(ToWorldCoord(c, 0f));

        return worldPath;
    }

    /// <summary>
    /// Returns a path in world space (Y = 0) from startPos to a walkable
    /// cell adjacent (4-neighbour) to the closest non-harvesting plantation.
    /// Used by CutterAgent (ground agent).
    /// </summary>
    public List<Vector3> PathToPlantation(Vector3 startPos, bool collectsRotten)
    {
        Vector2Int startCoord = WorldToGrid(startPos);
        if (!InBounds(startCoord))
            startCoord = ClampToBounds(startCoord);

        List<Vector2Int> gridPath = PathToPlantationGrid(startCoord, collectsRotten);
        List<Vector3> worldPath = new();

        foreach (var c in gridPath)
            worldPath.Add(ToWorldCoord(c, 0f));

        return worldPath;
    }

    /// <summary>
    /// Returns a path in world space (Y = 0) from 'from' to a walkable
    /// cell adjacent (4-neighbour) to the correct deposit.
    /// Empty list if unreachable or deposit not configured.
    /// Used by CutterAgent.
    /// </summary>
    public List<Vector3> PathToDeposit(Vector3 from, bool collectsRotten)
    {
        List<Vector3> worldPath = new();

        bool hasDeposit = collectsRotten ? hasRottenDeposit : hasHealthyDeposit;
        if (!hasDeposit)
            return worldPath;

        Vector2Int start = WorldToGrid(from);

        List<Vector2Int> gridPath = PathToDepositGrid(start, collectsRotten); // NEW
        if (gridPath == null || gridPath.Count == 0)
            return worldPath;

        foreach (var c in gridPath)
            worldPath.Add(ToWorldCoord(c, 0f));

        return worldPath;
    }

    /// <summary>
    /// Returns a world position (Y = 0) in a cell that moves the agent
    /// further away from the deposit, without going through the deposit tile.
    /// If no strictly "further" cell is walkable, returns a walkable neighbour
    /// of the current cell that is not the deposit cell.
    /// </summary>
    public Vector3 MoveAwayFromDeposit(Vector3 currentPos, bool collectsRotten)
    {
        bool hasDeposit = collectsRotten ? hasRottenDeposit : hasHealthyDeposit;
        if (!hasDeposit)
            return currentPos; // nothing to do

        Vector2Int here = WorldToGrid(currentPos);
        Vector2Int depositCoord = collectsRotten ? rottenDepositCoord : healthyDepositCoord;

        // If somehow we are on the deposit tile (shouldn’t happen for cutters),
        // just stay in place to avoid weird behaviour.
        if (here == depositCoord)
            return ToWorldCoord(here, 0f);

        int dx = here.x - depositCoord.x;
        int dy = here.y - depositCoord.y;

        // We expect cutters to be adjacent: |dx| + |dy| == 1.
        // Step one more cell in the same direction, if possible.
        if (Mathf.Abs(dx) + Mathf.Abs(dy) == 1)
        {
            Vector2Int further = new Vector2Int(here.x + dx, here.y + dy);
            if (IsWalkable(further))
                return ToWorldCoord(further, 0f);
        }

        // Fallback: pick any walkable neighbour of the current cell
        // that is NOT the deposit cell.
        foreach (var d in CardinalDirs)
        {
            Vector2Int n = here + d;
            if (!InBounds(n)) continue;
            if (n == depositCoord) continue;
            if (IsWalkable(n))
                return ToWorldCoord(n, 0f);
        }

        // Absolute worst case: no better place to go, stay where you are.
        return ToWorldCoord(here, 0f);
    }

    /// <summary>
    /// True if the given world position is on a cell adjacent (4-neighbour)
    /// to the correct deposit for this agent (healthy or rotten).
    /// </summary>
    public bool IsAtDeposit(Vector3 worldPos, bool collectsRotten)
    {
        bool hasDeposit = collectsRotten ? hasRottenDeposit : hasHealthyDeposit;
        if (!hasDeposit)
            return false;

        Vector2Int here = WorldToGrid(worldPos);
        Vector2Int dCoord = collectsRotten ? rottenDepositCoord : healthyDepositCoord;

        int dx = Mathf.Abs(here.x - dCoord.x);
        int dy = Mathf.Abs(here.y - dCoord.y);

        // Adjacent (non-diagonal) tile
        return (dx + dy == 1);
    }

    #endregion

    #region Internal Query Helpers (grid space)

    private void HandleMapScanIteration(Message msg)
    {
        Vector2Int coord = WorldToGrid(msg.Pos);
        if (!InBounds(coord))
            return;

        switch (msg.TileContent)
        {
            case TileContentType.Plantation:
                if (msg.Plantation != null)
                    RegisterPlantation(msg.Plantation);
                break;

            case TileContentType.Deposit:
                if (msg.Deposit != null)
                    RegisterDeposit(msg.Deposit);
                break;

            case TileContentType.None:
            default:
                // Empty tile: clear plantation and mark walkable
                grid[coord.x, coord.y].plantation = null;
                grid[coord.x, coord.y].isWalkable = true;

                // If this cell was a deposit, clear that info too
                if (hasHealthyDeposit && coord == healthyDepositCoord)
                {
                    healthyDeposit = null;
                    hasHealthyDeposit = false;
                }

                if (hasRottenDeposit && coord == rottenDepositCoord)
                {
                    rottenDeposit = null;
                    hasRottenDeposit = false;
                }

                break;
        }
    }

    private void ScanPlantation(Vector3 pos)
    {
        Vector2Int coord = WorldToGrid(pos);
        if (!InBounds(coord))
            return;

        // For scanning we still interpret coord as plantation cell.
        Plantation p = GetPlantation(coord);
        if (p != null)
            p.Scan();
    }

    private void SetAgentPresence(int id, Vector3 pos)
    {
        var coord = WorldToGrid(pos);
        if (!InBounds(coord))
            return;

        if (agentCoords.TryGetValue(id, out var prevCoord))
            grid[prevCoord.x, prevCoord.y].hasAgent = false;

        agentCoords[id] = coord;
        grid[coord.x, coord.y].hasAgent = true;
    }

    /// <summary>
    /// Grid version: cycle path starting at startCoord.
    /// </summary>
    private List<Vector2Int> CycleThroughPlantationsGrid(Vector2Int startCoord)
    {
        List<Vector2Int> plantations = GetAllPlantationCoords();
        List<Vector2Int> path = new();

        Vector2Int current = startCoord;
        HashSet<Vector2Int> remaining = new HashSet<Vector2Int>(plantations);
        if (remaining.Contains(current))
            remaining.Remove(current);

        current = ClampToBounds(current);
        path.Add(current);

        while (remaining.Count > 0)
        {
            Vector2Int bestTarget = current;
            List<Vector2Int> bestSegment = null;

            foreach (var candidate in remaining)
            {
                var safeCandidate = ClampToBounds(candidate);
                List<Vector2Int> segment = FindPath(current, safeCandidate);
                if (segment == null || segment.Count == 0)
                    continue;

                if (bestSegment == null || segment.Count < bestSegment.Count)
                {
                    bestSegment = segment;
                    bestTarget = safeCandidate;
                }
            }

            if (bestSegment == null)
                break;

            for (int i = 1; i < bestSegment.Count; i++)
                path.Add(ClampToBounds(bestSegment[i]));

            current = ClampToBounds(bestTarget);
            remaining.Remove(current);
        }

        List<Vector2Int> backSegment = FindPath(current, startCoord);
        if (backSegment != null && backSegment.Count > 1)
        {
            for (int i = 1; i < backSegment.Count; i++)
                path.Add(ClampToBounds(backSegment[i]));
        }

        return path;
    }

    /// <summary>
    /// Grid version: path to a walkable cell adjacent to the closest
    /// non-harvesting plantation with available tomatoes for this cutter.
    /// </summary>
    private List<Vector2Int> PathToPlantationGrid(Vector2Int startCoord, bool collectsRotten)
    {
        List<Vector2Int> plantations = GetAllPlantationCoords();
        List<Vector2Int> bestPath = null;

        foreach (var plantCoord in plantations)
        {
            Plantation p = GetPlantation(plantCoord);
            if (p == null || p.collecting)
                continue;

            int available = collectsRotten ? p.RottenTomatoes : p.HealthyTomatoes;
            if (available <= 0)
                continue;

            // NEW: we want to end on a walkable neighbour, not on plantCoord itself
            var neighbours = GetWalkableNeighbours(plantCoord);
            foreach (var n in neighbours)
            {
                var safeCoord = ClampToBounds(n);
                List<Vector2Int> path = FindPath(startCoord, safeCoord);
                if (path == null || path.Count == 0)
                    continue;

                if (bestPath == null || path.Count < bestPath.Count)
                    bestPath = path;
            }
        }

        return bestPath ?? new List<Vector2Int>();
    }

    /// <summary>
    /// Grid path to a walkable cell adjacent to the correct deposit.
    /// </summary>
    private List<Vector2Int> PathToDepositGrid(Vector2Int startCoord, bool collectsRotten)
    {
        bool hasDeposit = collectsRotten ? hasRottenDeposit : hasHealthyDeposit;
        if (!hasDeposit)
            return new List<Vector2Int>();

        Vector2Int depositCoord = collectsRotten ? rottenDepositCoord : healthyDepositCoord;
        List<Vector2Int> bestPath = null;

        var neighbours = GetWalkableNeighbours(depositCoord);
        foreach (var n in neighbours)
        {
            var safeCoord = ClampToBounds(n);
            List<Vector2Int> path = FindPath(startCoord, safeCoord);
            if (path == null || path.Count == 0)
                continue;

            if (bestPath == null || path.Count < bestPath.Count)
                bestPath = path;
        }

        // Fallback: if no walkable neighbour is reachable, try the deposit cell itself.
        if (bestPath == null)
        {
            var fallback = FindPath(startCoord, depositCoord);
            if (fallback != null)
                return fallback;
            return new List<Vector2Int>();
        }

        return bestPath;
    }

    #endregion

    #region Utils

    private bool InBounds(Vector2Int coord)
    {
        return coord.x >= 0 && coord.x < width &&
               coord.y >= 0 && coord.y < height;
    }

    private Vector2Int ClampToBounds(Vector2Int coord)
    {
        coord.x = Mathf.Clamp(coord.x, 0, width - 1);
        coord.y = Mathf.Clamp(coord.y, 0, height - 1);
        return coord;
    }

    private bool IsWalkable(Vector2Int coord)
    {
        if (!InBounds(coord)) return false;
        var cell = grid[coord.x, coord.y];
        return cell.isWalkable && !cell.hasAgent;
    }

    private Plantation GetPlantation(Vector2Int coord)
    {
        if (!InBounds(coord)) return null;
        return grid[coord.x, coord.y].plantation;
    }

    // NEW: plantation for a cutter standing in coord (cell or adjacent)
    private Plantation GetPlantationFromAgentCell(Vector2Int coord)
    {
        // 1. Same cell
        Plantation p = GetPlantation(coord);
        if (p != null) return p;

        // 2. Any 4-neighbour cell
        foreach (var d in CardinalDirs)
        {
            Vector2Int n = coord + d;
            if (!InBounds(n)) continue;
            p = GetPlantation(n);
            if (p != null) return p;
        }

        return null;
    }

    // NEW: walkable 4-neighbours of a given coord
    private List<Vector2Int> GetWalkableNeighbours(Vector2Int coord)
    {
        var result = new List<Vector2Int>();
        foreach (var d in CardinalDirs)
        {
            Vector2Int n = coord + d;
            if (IsWalkable(n))
                result.Add(n);
        }
        return result;
    }

    private Vector2Int WorldToGrid(Vector3 coord)
    {
        Vector3 delta = coord - reference;

        float dx = Vector3.Dot(delta, right);
        float dz = Vector3.Dot(delta, down);

        int x = Mathf.FloorToInt(dx / tileSize);
        int y = Mathf.FloorToInt(dz / tileSize);

        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);

        return new Vector2Int(x, y);
    }

    private List<Vector2Int> GetAllPlantationCoords()
    {
        var result = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].plantation != null)
                    result.Add(new Vector2Int(x, y));
            }
        }
        return result;
    }

    /// <summary>
    /// BFS pathfinding on a 4-neighbour grid (up/down/left/right).
    /// Allows entering the goal cell even if it is not walkable (e.g., plantation cell).
    /// Returns a list of coords from start to goal (inclusive), or null if unreachable.
    /// </summary>
    private List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        start = ClampToBounds(start);
        goal = ClampToBounds(goal);

        if (!InBounds(start) || !InBounds(goal))
            return null;

        var queue = new Queue<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == goal)
                break;

            foreach (var d in CardinalDirs)
            {
                var next = new Vector2Int(current.x + d.x, current.y + d.y);
                if (!InBounds(next) || visited.Contains(next))
                    continue;

                if (next != goal && !IsWalkable(next))
                    continue;

                visited.Add(next);
                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(goal) && start != goal)
            return null;

        var path = new List<Vector2Int>();
        var node = goal;
        path.Add(node);

        while (node != start)
        {
            if (!cameFrom.TryGetValue(node, out var prev))
                break;
            node = prev;
            path.Add(node);
        }

        path.Reverse();
        return path;
    }

    #endregion
}
