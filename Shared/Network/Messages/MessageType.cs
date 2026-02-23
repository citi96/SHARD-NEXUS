namespace Shared.Network.Messages;

/// <summary>
/// All network message types, byte-backed for compact serialization.
/// </summary>
public enum MessageType : byte
{
    // --- Infrastructure ---
    Ping = 0,
    Pong = 1,
    Ack = 2,

    // --- Client → Server ---
    JoinLobby = 10,
    ReadyUp = 11,
    BuyEcho = 12,
    SellEcho = 13,
    PositionEcho = 14,
    UseIntervention = 15,
    BuyXP = 16,
    RefreshShop = 17,

    // --- Server → Client ---
    JoinLobbyResponse = 50,
    LobbyState = 51,
    StartRound = 52,
    GameStarted = 53,
    PhaseChanged = 54,
    PlayerStateUpdate = 55,
    ShopRefreshed = 56,
    CombatStarted = 57,
    CombatUpdate = 58,
    CombatEnded = 59,
    PlayerEliminated = 60,
    GameEnded = 61,

    // --- Errors ---
    Error = 255
}
