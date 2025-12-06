using System.Collections.Generic;
using UnityEngine;

public class MapScanAgent : Agent
{
    public float scanDistance = 10f;

    private enum State
    {
        Idle,
        GoingToEdge,
        ScanningForward,
        ScanningBackward
    }

    private State state = State.Idle;

    private float yLevel;

    // Path from current position to the chosen map edge
    private List<Vector3> pathToEdge;
    private int edgeIndex;

    // Full boustrophedon traversal path starting at the edge
    private List<Vector3> traversalPath;
    private int scanIndex;

    protected override void StartAgent()
    {
        yLevel = transform.position.y;
        BeginMapScan();
    }

    private void BeginMapScan()
    {
        // Compute path to the fixed edge (0,0 in grid space)
        pathToEdge = SharedContext.Instance.PathToMapEdge(transform.position);
        edgeIndex = 0;

        if (pathToEdge != null && pathToEdge.Count > 0)
        {
            // Snap to the first point of the edge path
            Vector3 p = pathToEdge[edgeIndex];
            transform.position = new Vector3(p.x, yLevel, p.z);
            state = State.GoingToEdge;
        }
        else
        {
            // If no edge path (already at edge?) just start traversal directly
            SetupTraversalAtEdge();
        }
    }

    private void SetupTraversalAtEdge()
    {
        traversalPath = SharedContext.Instance.MapScanTraversalFromEdge();

        if (traversalPath == null || traversalPath.Count == 0)
        {
            state = State.Idle;
            return;
        }

        // Start at the first cell of the traversal
        scanIndex = 0;
        Vector3 start = traversalPath[scanIndex];
        transform.position = new Vector3(start.x, yLevel, start.z);

        state = State.ScanningForward;
    }

    protected override void MessageDispatch(Message msg)
    {
        // Optional: allow re-initialization via INIT_MAP_SCAN
        if (msg.Type == MessageType.INIT_MAP_SCAN &&
            msg.Performative == Performative.REQUEST)
        {
            BeginMapScan();
        }
    }

    protected override void FSMStep()
    {
        switch (state)
        {
            case State.Idle:
                // Do nothing
                break;

            case State.GoingToEdge:
                StepTowardsEdge();
                break;

            case State.ScanningForward:
                ScanStepForward();
                break;

            case State.ScanningBackward:
                ScanStepBackward();
                break;
        }
    }

    // ----------------- Edge movement -----------------

    private void StepTowardsEdge()
    {
        if (pathToEdge == null || pathToEdge.Count == 0)
        {
            SetupTraversalAtEdge();
            return;
        }

        // If we still have points to walk along the path to the edge
        if (edgeIndex < pathToEdge.Count - 1)
        {
            edgeIndex++;
            Vector3 next = pathToEdge[edgeIndex];
            next.y = yLevel;
            MoveTo(next);
        }
        else
        {
            // We are at the edge: snap exactly and start traversal
            Vector3 last = pathToEdge[edgeIndex];
            transform.position = new Vector3(last.x, yLevel, last.z);
            SetupTraversalAtEdge();
        }
    }

    // ----------------- Forward scanning -----------------

    private void ScanStepForward()
    {
        if (traversalPath == null || traversalPath.Count == 0)
            return;

        // 1) Scan current tile
        ScanCurrentTile();

        // 2) Move to next cell along the traversal path, or flip direction
        if (scanIndex < traversalPath.Count - 1)
        {
            scanIndex++;
            Vector3 next = traversalPath[scanIndex];
            next.y = yLevel;
            MoveTo(next);
        }
        else
        {
            // Reached the far end of the traversal (iteration complete)
            BroadcastIteration();
            state = State.ScanningBackward;
        }
    }

    // ----------------- Backward scanning -----------------

    private void ScanStepBackward()
    {
        if (traversalPath == null || traversalPath.Count == 0)
            return;

        // 1) Scan current tile
        ScanCurrentTile();

        // 2) Move to previous cell along the traversal path, or flip direction
        if (scanIndex > 0)
        {
            scanIndex--;
            Vector3 next = traversalPath[scanIndex];
            next.y = yLevel;
            MoveTo(next);
        }
        else
        {
            // Returned to the starting edge (iteration complete)
            BroadcastIteration();
            state = State.ScanningForward;
        }
    }

    // ----------------- Scanning & messages -----------------

    private void ScanCurrentTile()
    {
        Vector3 origin = transform.position;
        Vector3 direction = Vector3.down;

        TileContentType content = TileContentType.None;
        Plantation plantation = null;
        TomatoDeposit deposit = null;
        Vector3 hitPos = origin;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, scanDistance))
        {
            hitPos = hit.point;

            plantation = hit.collider.GetComponentInParent<Plantation>();
            if (plantation != null)
            {
                content = TileContentType.Plantation;
            }
            else
            {
                deposit = hit.collider.GetComponentInParent<TomatoDeposit>();
                if (deposit != null)
                {
                    content = TileContentType.Deposit;
                }
            }
        }
        else
        {
            // No hit: empty tile, we keep content = None and use current position
            hitPos = origin;
        }

        CommunicationBus.Instance.Broadcast(new Message
        {
            Performative = Performative.INFORM,
            Type = MessageType.SCAN_TILE,
            SenderId = Id,
            Pos = hitPos,
            TileContent = content,
            Plantation = plantation,
            Deposit = deposit
        });
    }

    private void BroadcastIteration()
    {
        CommunicationBus.Instance.Broadcast(new Message
        {
            Performative = Performative.INFORM,
            Type = MessageType.MAP_SCAN_ITERATION,
            SenderId = Id,
        });
    }
}
