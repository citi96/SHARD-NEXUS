namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: Game has started. Tells each client their assigned player ID.
/// </summary>
public class GameStartedMessage
{
    public int YourPlayerId { get; set; }
    public int TotalPlayers { get; set; }
}
