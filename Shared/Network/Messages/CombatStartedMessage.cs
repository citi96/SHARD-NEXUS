using Shared.Models.Structs;

namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: Combat round is starting against a specific opponent.
/// </summary>
public class CombatStartedMessage
{
    public int OpponentId { get; set; }
    public PlayerState OpponentState { get; set; }
}
