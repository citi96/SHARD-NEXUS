using System.Text.Json;

namespace Shared.Network.Messages;

/// <summary>
/// Network message envelope: typed header + JSON payload.
/// Supports optional ACK via SequenceId.
/// </summary>
public class NetworkMessage
{
    private static uint _sequenceCounter;

    public MessageType Type { get; set; }
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// Monotonically increasing sequence number. Used for ACK tracking on critical messages.
    /// </summary>
    public uint SequenceId { get; set; }

    /// <summary>
    /// When true, the receiver must reply with an Ack message referencing this SequenceId.
    /// </summary>
    public bool RequiresAck { get; set; }

    /// <summary>
    /// Create a message with auto-assigned sequence ID.
    /// </summary>
    public static NetworkMessage Create<T>(MessageType type, T payload, bool requiresAck = false)
    {
        return new NetworkMessage
        {
            Type = type,
            PayloadJson = JsonSerializer.Serialize(payload),
            SequenceId = Interlocked.Increment(ref _sequenceCounter),
            RequiresAck = requiresAck
        };
    }

    /// <summary>
    /// Create a message with no payload (action-only messages like BuyXP, RefreshShop).
    /// </summary>
    public static NetworkMessage Create(MessageType type, bool requiresAck = false)
    {
        return new NetworkMessage
        {
            Type = type,
            PayloadJson = string.Empty,
            SequenceId = Interlocked.Increment(ref _sequenceCounter),
            RequiresAck = requiresAck
        };
    }

    public T? DeserializePayload<T>()
    {
        if (string.IsNullOrEmpty(PayloadJson))
            return default;

        return JsonSerializer.Deserialize<T>(PayloadJson);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    public static NetworkMessage? FromJson(string json)
    {
        return JsonSerializer.Deserialize<NetworkMessage>(json);
    }
}
