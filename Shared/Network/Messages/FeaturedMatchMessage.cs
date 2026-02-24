namespace Shared.Network.Messages;

/// <summary>
/// Server → All clients (broadcast): identifies the Featured Match for the current round.
/// Used by the observer system to know which combat to display.
/// </summary>
public class FeaturedMatchMessage
{
    public int Player1Id { get; set; }
    public int Player2Id { get; set; }

    /// <summary>"AtRisk" (one player below 15 HP) or "HighHP" (highest combined HP pair).</summary>
    public string Reason { get; set; } = string.Empty;
}
