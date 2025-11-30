using System.Collections.Generic;
using UnityEngine;

public class CutterAgent : Agent
{
    [Header("Role")]
    public bool colectsRotten;

    [Header("Capacity")]
    public int capacity = 10;

    private int load;

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

        List<Vector3> pathToPlantation = null;
        bool hasAvailablePlantation = false;

        if (load < capacity)
        {
            pathToPlantation = ctx.PathToPlantation(pos);
            hasAvailablePlantation = pathToPlantation != null && pathToPlantation.Count > 0;
        }

        if (load < capacity && hasAvailablePlantation)
        {
            if (state != State.Harvest || currentPath == null || currentPath.Count == 0)
            {
                state = State.Harvest;
                currentPath = pathToPlantation;
                pathIndex = 0;
            }

            StepAlongPath();

            if (AtEndOfPath() && load < capacity)
            {
                load = Mathf.Min(capacity, load + 1);
            }

            return;
        }

        if (atDeposit && state != State.Deposit)
        {
            state = State.MoveAwayFromDeposit;

            Vector3 target = ctx.MoveAwayFromDeposit(colectsRotten);
            currentPath = new List<Vector3> { pos, target };
            pathIndex = 0;

            StepAlongPath();
            return;
        }

        if (load >= capacity || (load > 0 && !hasAvailablePlantation))
        {
            if (state != State.Deposit || currentPath == null || currentPath.Count == 0)
            {
                state = State.Deposit;
                currentPath = ctx.PathToDeposit(pos, colectsRotten);
                pathIndex = 0;
            }

            StepAlongPath();

            if (AtEndOfPath() && load > 0)
            {
                load = 0;
            }

            return;
        }

        state = State.Idle;
    }

    private void StepAlongPath()
    {
        if (currentPath == null || currentPath.Count == 0)
            return;

        if (pathIndex < currentPath.Count - 1)
            pathIndex++;

        Vector3 target = currentPath[pathIndex];
        transform.position = new Vector3(target.x, yLevel, target.z);
    }

    private bool AtEndOfPath()
    {
        return currentPath != null &&
               currentPath.Count > 0 &&
               pathIndex == currentPath.Count - 1;
    }
}
