namespace Shared.Network.Messages;

/// <summary>
/// Info about a player in the lobby, used inside LobbyStateMessage.
/// </summary>
public class LobbyPlayerInfo
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public bool IsReady { get; set; }
}
