namespace Shared.Models.Enums;

/// <summary>
/// The core phases of the gameplay loop.
/// </summary>
public enum GamePhase : byte
{
    WaitingForPlayers,
    Preparation,
    Combat,
    Reward,
    MutationChoice,
    GameOver
}
