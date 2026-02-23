using Shared.Models.Enums;

namespace Shared.Models.Structs;

/// <summary>
/// The complete state of a match.
/// Contains the data for all players and shared elements.
/// </summary>
public readonly record struct GameState(
    int MatchId,
    int Round,
    GamePhase Phase,
    PlayerState[] Players,
    int[] SharedPoolEchoDefinitionIds,
    int RandomSeed
);
