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
    private int tileWidth;
    private int tileHeight;
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
                        Plantation p = GetPlantation(coord);
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
                        Plantation p = GetPlantation(coord);
                        if (p != null)
                            p.collecting = false;
                    }
                    break;
                }

            case MessageType.SCAN_PLANTATION:
                ScanPlantation(msg.Pos);
                break;
        }
    }

    #region Initialization

    public void Initialize(GridParameters parameters)
    {
        width = parameters.width;
        height = parameters.height;
        tileWidth = parameters.tileWidth;
        tileHeight = parameters.tileHeight;

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

    public void RegisterDeposits(TomatoDeposit healthyDeposit, TomatoDeposit rottenDeposit)
    {
        this.healthyDeposit = healthyDeposit;
        this.rottenDeposit = rottenDeposit;

        hasHealthyDeposit = healthyDeposit != null;
        hasRottenDeposit = rottenDeposit != null;

        if (hasHealthyDeposit)
            healthyDepositCoord = WorldToGrid(healthyDeposit.transform.position);

        if (hasRottenDeposit)
            rottenDepositCoord = WorldToGrid(rottenDeposit.transform.position);
    }

    public void RegisterPlantation(Plantation plantation)
    {
        var coord = WorldToGrid(plantation.transform.position);

        grid[coord.x, coord.y].isWalkable = false;
        grid[coord.x, coord.y].plantation = plantation;
    }

    #endregion

    #region Context Queries (public world-space API)

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

        float offsetX = (coord.x + 0.5f) * tileWidth;
        float offsetZ = (coord.y + 0.5f) * tileHeight;

        Vector3 world = reference + right * offsetX + down * offsetZ;
        world.y = y;
        return world;
    }

    /// <summary>
    /// Returns a cyclic path in world space (Y = 0) starting at startPos,
    /// visiting all plantations and returning to the start (if possible).
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
    /// Returns a path in world space (Y = 0) from startPos to the closest
    /// non-harvesting plantation. Empty list if none reachable.
    /// </summary>
    public List<Vector3> PathToPlantation(Vector3 startPos)
    {
        Vector2Int startCoord = WorldToGrid(startPos);
        if (!InBounds(startCoord))
            startCoord = ClampToBounds(startCoord);

        List<Vector2Int> gridPath = PathToPlantationGrid(startCoord);
        List<Vector3> worldPath = new();

        foreach (var c in gridPath)
            worldPath.Add(ToWorldCoord(c, 0f));

        return worldPath;
    }

    /// <summary>
    /// Returns a path in world space (Y = 0) from 'from' to the
    /// correct deposit (healthy or rotten). Empty list if unreachable
    /// or deposit not configured.
    /// </summary>
    public List<Vector3> PathToDeposit(Vector3 from, bool collectsRotten)
    {
        List<Vector3> worldPath = new();

        bool hasDeposit = collectsRotten ? hasRottenDeposit : hasHealthyDeposit;
        if (!hasDeposit)
            return worldPath;

        Vector2Int start = WorldToGrid(from);
        Vector2Int goal = collectsRotten ? rottenDepositCoord : healthyDepositCoord;

        List<Vector2Int> gridPath = FindPath(start, goal);
        if (gridPath == null || gridPath.Count == 0)
            return worldPath;

        foreach (var c in gridPath)
            worldPath.Add(ToWorldCoord(c, 0f));

        return worldPath;
    }

    /// <summary>
    /// Returns a world position (Y = 0) in a walkable cell adjacent to the
    /// appropriate deposit. If none are walkable, returns the deposit cell itself.
    /// </summary>
    public Vector3 MoveAwayFromDeposit(bool collectsRotten)
    {
        bool hasDeposit = collectsRotten ? hasRottenDeposit : hasHealthyDeposit;
        if (!hasDeposit)
            return Vector3.zero;

        Vector2Int baseCoord = collectsRotten ? rottenDepositCoord : healthyDepositCoord;

        var dirs = new[]
        {
        new Vector2Int( 1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int( 0, 1),
        new Vector2Int( 0,-1)
    };

        foreach (var d in dirs)
        {
            Vector2Int next = ClampToBounds(baseCoord + d);
            if (IsWalkable(next))
                return ToWorldCoord(next, 0f);
        }

        return ToWorldCoord(baseCoord, 0f);
    }

    /// <summary>
    /// True if the given world position is on the correct deposit cell
    /// for this agent (healthy or rotten).
    /// </summary>
    public bool IsAtDeposit(Vector3 worldPos, bool collectsRotten)
    {
        bool hasDeposit = collectsRotten ? hasRottenDeposit : hasHealthyDeposit;
        if (!hasDeposit)
            return false;

        Vector2Int here = WorldToGrid(worldPos);
        Vector2Int dCoord = collectsRotten ? rottenDepositCoord : healthyDepositCoord;

        return here == dCoord;
    }

    #endregion

    #region Internal Query Helpers (grid space)

    private void ScanPlantation(Vector3 pos)
    {
        Vector2Int coord = WorldToGrid(pos);
        if (!InBounds(coord))
            return;

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
    /// Grid version: path to closest non-harvesting plantation.
    /// </summary>
    private List<Vector2Int> PathToPlantationGrid(Vector2Int startCoord)
    {
        List<Vector2Int> plantations = GetAllPlantationCoords();
        List<Vector2Int> bestPath = null;

        foreach (var coord in plantations)
        {
            Plantation p = GetPlantation(coord);
            if (p == null || p.collecting)
                continue;

            var safeCoord = ClampToBounds(coord);
            List<Vector2Int> path = FindPath(startCoord, safeCoord);
            if (path == null || path.Count == 0)
                continue;

            if (bestPath == null || path.Count < bestPath.Count)
                bestPath = path;
        }

        return bestPath ?? new List<Vector2Int>();
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

    private Vector2Int WorldToGrid(Vector3 coord)
    {
        Vector3 delta = coord - reference;

        float dx = Vector3.Dot(delta, right);
        float dz = Vector3.Dot(delta, down);

        int x = Mathf.FloorToInt(dx / tileWidth);
        int y = Mathf.FloorToInt(dz / tileHeight);

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
    /// Allows entering the goal cell even if it is not walkable (plantation cell).
    /// Returns a list of coords from start to goal (inclusive), or null if unreachable.
    /// </summary>
    private List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        start = ClampToBounds(start);
        goal = ClampToBounds(goal);

        if (!InBounds(start) || !InBounds(goal))
            return null;

        var dirs = new[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

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

            foreach (var d in dirs)
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
