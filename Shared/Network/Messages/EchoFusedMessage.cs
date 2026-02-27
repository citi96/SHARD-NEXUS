namespace Shared.Network.Messages;

/// <summary>
/// Sent by the server when 3 identical echoes fuse into a higher star level.
/// </summary>
public class EchoFusedMessage
{
    public int ResultInstanceId { get; set; }
    public byte NewStarLevel { get; set; }
    public int DefinitionId { get; set; }
    public bool IsOnBoard { get; set; }
    public int SlotIndex { get; set; }
}
