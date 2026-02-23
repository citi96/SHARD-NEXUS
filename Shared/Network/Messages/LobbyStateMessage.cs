namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: Current state of the lobby.
/// </summary>
public class LobbyStateMessage
{
    public List<LobbyPlayerInfo> Players { get; set; } = new();
    public bool AllReady { get; set; }
    
    // The exact seconds remaining in the countdown, if started.
    // Use -1f to indicate countdown is not active.
    public float CountdownRemaining { get; set; } = -1f;
}
