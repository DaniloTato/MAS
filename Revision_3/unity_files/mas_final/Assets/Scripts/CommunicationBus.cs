using System;
using UnityEngine;

public enum Performative
{
    INFORM,
    REQUEST,
    QUERY_REF,
    PROPOSE,
    ACCEPT_PROPOSAL,
    REJECT_PROPOSAL,
    CANCEL,
    FAILURE,
    CONFIRM,
    DISCONFIRM,
}

public enum MessageType
{
    HEARTBEAT,
    INIT_HARVEST,
    STOP_HARVEST,
    INIT_DEPOSIT,
    STOP_DEPOSIT,
    SCAN_PLANTATION,
}

public class Message
{
    // ACL logic
    public Performative Performative;
    public int SenderId;
    public int ReceiverId;          // -1 for broadcast
    public int ConversationId = 0;
    public int ReplyWith = 0;
    public int InReplyTo = 0;

    // Payload
    public MessageType Type;
    public Vector3 Pos;
}

public class CommunicationBus : MonoBehaviour
{
    public static CommunicationBus Instance { get; private set; }

    public event Action<Message> OnMessage;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SendMessage(Message msg)
    {
        OnMessage?.Invoke(msg);
    }

    public void Broadcast(Message msg)
    {
        msg.ReceiverId = -1;
        OnMessage?.Invoke(msg);
    }
}
