using UnityEngine;

public abstract class Agent : MonoBehaviour
{
    public int Id { get; private set; }

    public void Initialize(int id)
    {
        Id = id;

        CommunicationBus.Instance.OnMessage += OnMessageReceived;

        var pos = SharedContext.Instance.CenterInCell(transform.position);
        transform.position = new Vector3(pos.x, transform.position.y, pos.z);

        Heartbeat();

        StartAgent();
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
            ReceiverId = -1,
            Pos = transform.position,
        });
    }
}
