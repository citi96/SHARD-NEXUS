namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: Current state of the lobby.
/// </summary>
public class LobbyStateMessage
{
    public List<LobbyPlayerInfo> Players { get; set; } = new();
    public bool AllReady { get; set; }
}
