using Shared.Models.Structs;

namespace Shared.Network.Messages;

/// <summary>
/// Server → Client: Full state update for a specific player.
/// </summary>
public class PlayerStateUpdateMessage
{
    public PlayerState State { get; set; }
}
