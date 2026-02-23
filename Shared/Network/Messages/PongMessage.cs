namespace Shared.Network.Messages;

/// <summary>
/// Pong response to Ping. Echoes back the original timestamp + server receive time.
/// </summary>
public class PongMessage
{
    public long OriginalTimestamp { get; set; }
    public long ServerReceivedAt { get; set; }
}
