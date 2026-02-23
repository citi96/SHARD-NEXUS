namespace Shared.Network.Messages;

/// <summary>
/// Client → Server: Place or move an echo on the board.
/// </summary>
public class PositionEchoMessage
{
    public int EchoInstanceId { get; set; }
    public int BoardX { get; set; }
    public int BoardY { get; set; }
}
