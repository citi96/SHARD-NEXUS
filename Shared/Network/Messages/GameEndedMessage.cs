namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: The game has ended. Contains winner and final placements.
/// </summary>
public class GameEndedMessage
{
    public int WinnerId { get; set; }
    public int[] Placements { get; set; } = Array.Empty<int>();
}
