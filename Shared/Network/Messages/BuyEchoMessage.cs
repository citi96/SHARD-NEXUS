namespace Shared.Network.Messages;

/// <summary>
/// Client → Server: Buy an echo from the shop.
/// </summary>
public class BuyEchoMessage
{
    public int EchoDefinitionId { get; set; }
    public int ShopSlot { get; set; }
}
