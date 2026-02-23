namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: Tick update during combat. Payload is flexible JSON.
/// </summary>
public class CombatUpdateMessage
{
    /// <summary>
    /// Flexible JSON string for combat tick data (attacks, ability triggers, etc).
    /// </summary>
    public string EventJson { get; set; } = string.Empty;
}
