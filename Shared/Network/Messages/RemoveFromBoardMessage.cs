namespace Shared.Network.Messages;

/// <summary>
/// Client → Server: move an Echo from the board back to the bench (no gold refund).
/// </summary>
public class RemoveFromBoardMessage
{
    public int EchoInstanceId { get; set; }
}
