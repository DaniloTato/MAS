using System.Collections.Generic;
using UnityEngine;

public class TomatoesScanAgent : Agent
{
    public string plantationTag = "Plantation";
    public float scanDistance = 10f;

    private List<Vector3> cyclicPath;
    private int pathIndex;
    private float yLevel;
    private bool cycling;

    protected override void StartAgent()
    {
        yLevel = transform.position.y;
        cycling = false;
    }

    private void StartCycle()
    {
        if (cycling) return;

        cyclicPath = SharedContext.Instance.CycleThroughPlantations(transform.position);
        pathIndex = 0;

        if (cyclicPath != null && cyclicPath.Count > 0)
        {
            var p = cyclicPath[pathIndex];
            transform.position = new Vector3(p.x, yLevel, p.z);
        }

        cycling = true;
    }

    protected override void MessageDispatch(Message msg)
    {
        switch (msg.Type)
        {
            case MessageType.MAP_SCAN_ITERATION:
                StartCycle();
                break;
        }
    }

    protected override void FSMStep()
    {
        if (!cycling) return;
        MoveToNextCell();
        ScanAtCurrentPosition();
    }

    private void ScanAtCurrentPosition()
    {
        Vector3 origin = transform.position;
        Vector3 direction = Vector3.down;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, scanDistance))
            return;

        if (!hit.collider.CompareTag(plantationTag))
            return;

        ScanPlantation(hit.point);
    }

    private void MoveToNextCell()
    {
        if (cyclicPath == null || cyclicPath.Count == 0)
            return;

        pathIndex = (pathIndex + 1) % cyclicPath.Count;

        Vector3 next = cyclicPath[pathIndex];
        next.y = yLevel;

        MoveTo(next);
    }

    private void ScanPlantation(Vector3 hitPoint)
    {
        CommunicationBus.Instance.Broadcast(new Message
        {
            Performative = Performative.REQUEST,
            Type = MessageType.SCAN_PLANTATION,
            SenderId = Id,
            Pos = hitPoint
        });
    }
}
