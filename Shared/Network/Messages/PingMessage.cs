namespace Shared.Network.Messages;

/// <summary>
/// Ping message for latency measurement. Contains a timestamp (ticks).
/// </summary>
public class PingMessage
{
    public long Timestamp { get; set; }
}
