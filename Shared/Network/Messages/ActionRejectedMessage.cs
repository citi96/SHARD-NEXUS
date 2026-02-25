namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: the requested action was rejected.
/// </summary>
public class ActionRejectedMessage
{
    /// <summary>The action that was rejected, e.g. "BuyEcho", "PositionEcho".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Human-readable reason for the rejection.</summary>
    public string Reason { get; set; } = string.Empty;
}
