namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: Updated shop contents for this player.
/// </summary>
public class ShopRefreshedMessage
{
    public List<int> EchoDefinitionIds { get; set; } = new();
}
