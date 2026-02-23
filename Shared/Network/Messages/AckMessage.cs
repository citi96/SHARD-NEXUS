namespace Shared.Network.Messages;

/// <summary>
/// Acknowledgement for a message that had RequiresAck = true.
/// </summary>
public class AckMessage
{
    public uint AcknowledgedSequenceId { get; set; }
}
