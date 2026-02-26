namespace Shared.Network.Messages;

/// <summary>
/// Server → All: an intervention was validated and queued for the next combat batch.
/// Broadcast to all connected clients (including observers).
/// </summary>
public class InterventionActivatedMessage
{
    /// <summary>Player who activated the intervention.</summary>
    public int PlayerId { get; set; }

    /// <summary>Intervention type name, e.g. "Reposition", "Focus", "Barrier", "Accelerate", "TacticalRetreat".</summary>
    public string InterventionType { get; set; } = string.Empty;

    /// <summary>Target unit instance ID, or -1 for whole-team interventions (Accelerate).</summary>
    public int TargetUnitId { get; set; }
}
