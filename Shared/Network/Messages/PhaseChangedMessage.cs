using Shared.Models.Enums;

namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: Phase transition notification.
/// </summary>
public class PhaseChangedMessage
{
    public GamePhase NewPhase { get; set; }
    public float PhaseDurationSecs { get; set; }
}
