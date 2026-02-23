namespace Shared.Network.Messages;

/// <summary>
/// Client → Server: Player signals ready/not ready in lobby.
/// </summary>
public class ReadyUpMessage
{
    public bool IsReady { get; set; }
}
