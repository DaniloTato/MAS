using TMPro;
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GridParameters
{
    public int width;
    public int height;
    public int tileSize;
    public GameObject gridReference;
}

[System.Serializable]
public class SimulationParameters
{
    public GridParameters gridParams;

    [Header("World Objects")]
    public List<Agent> agents;

    [Header("Tick Timing")]
    [Range(0.1f, 2f)]
    public float tickInterval;

    [Header("Metrics")]
    public TMP_Text ticksText;
    public TMP_Text movementsText;
}

// Single parameter entry point
// Initializes the simulation shared context and agents
// Manages simulation control flow and steps via ticks
public class SimulationManager : MonoBehaviour
{
    public static SimulationManager Instance { get; private set; }

    [SerializeField]
    private SimulationParameters parameters;

    private int _ticks;
    private int Ticks
    {
        get { return _ticks; }
        set
        {
            _ticks = value;
            if (parameters.ticksText != null)
                parameters.ticksText.text = $"Time (ticks): {_ticks}";
        }
    }

    private int _movements;
    private int Movements
    {
        get { return _movements; }
        set
        {
            _movements = value;
            if (parameters.movementsText != null)
                parameters.movementsText.text = $"Movements: {_movements}";
        }
    }

    public int TileUnit => parameters.gridParams.tileSize;
    public float TickInterval => parameters.tickInterval;
    private float tickTimer = 0f;
    private bool simulationRunning = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        SharedContext.Instance.Initialize(parameters.gridParams);

        InitalizeAgents(parameters.agents);

        Ticks = 0;
        Movements = 0;
        tickTimer = 0f;
        simulationRunning = true;
    }

    private void Update()
    {
        if (!simulationRunning) return;

        tickTimer += Time.deltaTime;
        if (tickTimer < parameters.tickInterval)
            return;
        tickTimer -= parameters.tickInterval;

        foreach (var agent in parameters.agents)
        {
            agent?.Step();
        }

        Ticks++;
    }

    private void InitalizeAgents(List<Agent> agents)
    {
        int id = 0;
        foreach (var agent in agents)
        {
            agent.Initialize(id++);
        }
    }

    public void RegisterMovement()
    {
        Movements++;
    }

    private void OnDrawGizmos()
    {
        GridParameters grid = parameters?.gridParams;
        if (grid == null) return;

        Transform reference = grid.gridReference?.transform;
        if (reference == null) return;

        Vector3 origin = reference.position;

        Vector3 right = reference.right;
        Vector3 forward = reference.forward;
        Vector3 down = -forward;

        Gizmos.color = Color.green;

        float totalWidth = grid.width * grid.tileSize;
        float totalDepth = grid.height * grid.tileSize;

        for (int x = 0; x <= grid.width; x++)
        {
            float xOffset = x * grid.tileSize;
            Vector3 from = origin + right * xOffset;
            Vector3 to = from + down * totalDepth;
            Gizmos.DrawLine(from, to);
        }

        for (int y = 0; y <= grid.height; y++)
        {
            float zOffset = y * grid.tileSize;
            Vector3 from = origin + down * zOffset;
            Vector3 to = from + right * totalWidth;
            Gizmos.DrawLine(from, to);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(origin, Vector3.one * 0.1f);
    }
}
