namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: current energy values for the receiving player's team.
/// Sent after every combat batch and after each intervention validation.
/// </summary>
public class EnergyUpdateMessage
{
    public int Energy    { get; set; }
    public int MaxEnergy { get; set; }
}
