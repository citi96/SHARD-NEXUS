namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: Combat round has ended.
/// </summary>
public class CombatEndedMessage
{
    public int WinnerId { get; set; }
    public int DamageDealt { get; set; }
}
