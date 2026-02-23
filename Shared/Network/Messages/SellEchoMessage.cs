namespace Shared.Network.Messages;

/// <summary>
/// Client → Server: Sell an echo from board or bench.
/// </summary>
public class SellEchoMessage
{
    public int EchoInstanceId { get; set; }
}
