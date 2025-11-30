using System.Collections.Generic;
using UnityEngine;

public class TomatoesScanAgent : Agent
{
    public string plantationTag = "Plantation";
    public float scanDistance = 5f;

    private List<Vector3> cyclicPath;
    private int pathIndex;
    private float yLevel;

    protected override void StartAgent()
    {
        yLevel = transform.position.y;

        cyclicPath = SharedContext.Instance.CycleThroughPlantations(transform.position);
        pathIndex = 0;

        if (cyclicPath != null && cyclicPath.Count > 0)
        {
            var p = cyclicPath[pathIndex];
            transform.position = new Vector3(p.x, yLevel, p.z);
        }
    }

    protected override void MessageDispatch(Message msg)
    {
    }

    protected override void FSMStep()
    {
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
        transform.position = new Vector3(next.x, yLevel, next.z);
    }

    private void ScanPlantation(Vector3 hitPoint)
    {
        CommunicationBus.Instance.Broadcast(new Message
        {
            Performative = Performative.REQUEST,
            Type = MessageType.SCAN_PLANTATION,
            SenderId = Id,
            ReceiverId = -1,
            Pos = hitPoint
        });
    }
}
