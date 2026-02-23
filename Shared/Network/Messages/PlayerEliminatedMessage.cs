namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: A player has been eliminated from the match.
/// </summary>
public class PlayerEliminatedMessage
{
    public int PlayerId { get; set; }
    public int FinalPlacement { get; set; }
}
