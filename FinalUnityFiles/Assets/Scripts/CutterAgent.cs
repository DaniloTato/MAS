using System.Collections.Generic;
using UnityEngine;

public class CutterAgent : Agent
{
    public bool colectsRotten;
    public int capacity = 10;

    public GameObject visualLoad;

    private int _load;
    private int load
    {
        get => _load;
        set
        {
            _load = value;
            visualLoad.SetActive(_load > 0);
        }
    }

    private enum State
    {
        Idle,
        Harvest,
        Deposit,
        MoveAwayFromDeposit
    }

    private State state = State.Idle;

    private List<Vector3> currentPath;
    private int pathIndex;
    private float yLevel;

    protected override void StartAgent()
    {
        yLevel = transform.position.y;
        state = State.Idle;
        currentPath = null;
        pathIndex = 0;
        load = 0;
    }

    protected override void MessageDispatch(Message msg)
    {
    }

    protected override void FSMStep()
    {
        var ctx = SharedContext.Instance;
        Vector3 pos = transform.position;
        bool atDeposit = ctx.IsAtDeposit(pos, colectsRotten);

        switch (state)
        {
            case State.Idle:
            case State.Harvest:
                {
                    // If we still have a path and haven't reached the target yet, keep walking
                    if (state == State.Harvest &&
                        currentPath != null &&
                        currentPath.Count > 0 &&
                        !AtEndOfPath())
                    {
                        StepAlongPath();
                        return;
                    }

                    // If we are in Harvest and already at the end of the path,
                    // we are adjacent to a plantation → harvest via adjacency-aware API.
                    if (state == State.Harvest && AtEndOfPath())
                    {
                        int remainingCapacity = capacity - load;
                        if (remainingCapacity > 0)
                        {
                            int harvested = ctx.HarvestTomatoesAt(pos, colectsRotten, remainingCapacity);
                            if (harvested > 0)
                                load += harvested;
                        }

                        // Finished interacting with this plantation; reset the current path
                        currentPath = null;
                        pathIndex = 0;

                        // If we are full, go deposit
                        if (load >= capacity)
                        {
                            state = State.Deposit;
                            currentPath = ctx.PathToDeposit(pos, colectsRotten);
                            pathIndex = 0;
                            StepAlongPath();
                            return;
                        }
                    }

                    // Either we were Idle, or we just finished harvesting and still have capacity
                    if (load < capacity)
                    {
                        var pathToPlantation = ctx.PathToPlantation(pos, colectsRotten);
                        bool hasPlantation = pathToPlantation != null && pathToPlantation.Count > 0;

                        if (hasPlantation)
                        {
                            state = State.Harvest;
                            currentPath = pathToPlantation;
                            pathIndex = 0;
                            StepAlongPath();
                            return;
                        }
                    }

                    // No plantations to go to or no capacity; if we carry something, go deposit
                    if (load > 0)
                    {
                        state = State.Deposit;
                        currentPath = ctx.PathToDeposit(pos, colectsRotten);
                        pathIndex = 0;
                        StepAlongPath();
                    }
                    else
                    {
                        state = State.Idle;
                    }

                    break;
                }

            case State.Deposit:
                {
                    // Walk to a tile adjacent to the deposit if we are not there yet
                    if (!atDeposit)
                    {
                        if (currentPath == null || currentPath.Count == 0)
                        {
                            currentPath = ctx.PathToDeposit(pos, colectsRotten);
                            pathIndex = 0;
                        }

                        if (!AtEndOfPath())
                        {
                            StepAlongPath();
                            return;
                        }
                    }

                    if (load > 0)
                    {
                        int deposited = ctx.DepositTomatoes(load, colectsRotten);
                        load -= deposited;
                    }

                    // Move away from the deposit without crossing through its cell.
                    state = State.MoveAwayFromDeposit;
                    Vector3 away = ctx.MoveAwayFromDeposit(pos, colectsRotten);
                    currentPath = new List<Vector3> { pos, away };
                    pathIndex = 0;
                    StepAlongPath();
                    break;
                }

            case State.MoveAwayFromDeposit:
                {
                    if (!AtEndOfPath())
                    {
                        StepAlongPath();
                    }
                    else
                    {
                        // Once we are away from the deposit, resume normal behavior
                        state = State.Idle;
                        currentPath = null;
                        pathIndex = 0;
                    }
                    break;
                }
        }
    }

    private void StepAlongPath()
    {
        if (currentPath == null || currentPath.Count == 0)
            return;

        if (AtEndOfPath())
            return;

        pathIndex++;

        Vector3 target = currentPath[pathIndex];
        target.y = yLevel;

        MoveTo(target);
    }

    private bool AtEndOfPath()
    {
        return currentPath != null &&
               currentPath.Count > 0 &&
               pathIndex == currentPath.Count - 1;
    }
}
