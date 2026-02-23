namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: A reduced state update for an opponent player.
/// Hides bench contents, exact gold, and board positions to prevent sniffing.
/// </summary>
public class OtherPlayerInfoMessage
{
    public int PlayerId { get; set; }
    public int NexusHealth { get; set; }
    public int Level { get; set; }
    public int WinStreak { get; set; }
    public int LossStreak { get; set; }
}
