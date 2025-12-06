using UnityEngine;

public abstract class Agent : MonoBehaviour
{
    public int Id { get; private set; }

    private bool isMoving;
    private Vector3 moveStartPos;
    private Vector3 moveEndPos;
    private Quaternion startRot;
    private Quaternion endRot;
    private float moveElapsed;

    protected bool IsMoving => isMoving;

    public void Initialize(int id)
    {
        Id = id;

        CommunicationBus.Instance.OnMessage += OnMessageReceived;

        var pos = SharedContext.Instance.CenterInCell(transform.position);
        transform.position = new Vector3(pos.x, transform.position.y, pos.z);

        Heartbeat();
        StartAgent();
    }

    private void OnDestroy()
    {
        if (CommunicationBus.Instance != null)
            CommunicationBus.Instance.OnMessage -= OnMessageReceived;
    }

    private void Update()
    {
        UpdateMovement();
    }

    public void Step()
    {
        Heartbeat();
        FSMStep();
    }

    private void OnMessageReceived(Message msg)
    {
        if (msg.ReceiverId != Id && msg.ReceiverId != -1)
            return;

        MessageDispatch(msg);
    }

    protected abstract void StartAgent();
    protected abstract void MessageDispatch(Message msg);
    protected abstract void FSMStep();

    private void Heartbeat()
    {
        CommunicationBus.Instance.Broadcast(new Message
        {
            Performative = Performative.INFORM,
            Type = MessageType.HEARTBEAT,
            SenderId = Id,
            Pos = transform.position,
        });
    }

    protected void MoveTo(Vector3 targetPosition)
    {
        targetPosition.y = transform.position.y;

        moveStartPos = transform.position;
        moveEndPos = targetPosition;
        moveElapsed = 0f;

        startRot = transform.rotation;

        Vector3 dir = moveEndPos - moveStartPos;
        dir.y = 0f;

        if (dir.sqrMagnitude > 1e-6f)
            endRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        else
            endRot = startRot;

        isMoving = true;

        SimulationManager.Instance.RegisterMovement();
    }

    private void UpdateMovement()
    {
        if (!isMoving)
            return;

        moveElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(moveElapsed / SimulationManager.Instance.TickInterval);

        // Interpolate position & rotation
        transform.position = Vector3.Lerp(moveStartPos, moveEndPos, t);
        transform.rotation = Quaternion.Slerp(startRot, endRot, t);

        if (t >= 1f)
        {
            // Snap to final state to avoid accumulation errors
            transform.position = moveEndPos;
            transform.rotation = endRot;
            isMoving = false;
        }
    }
}
