namespace Shared.Models.Structs;

/// <summary>
/// Complete state of a specific player in the match.
/// Arrays are used for Board, Bench, and Mutations to allow efficient serialization as IDs.
/// </summary>
public readonly record struct PlayerState(
    int PlayerId,
    int NexusHealth,
    int Gold,
    int Level,
    int Xp,
    int[] BoardEchoInstanceIds,
    byte[] BoardEchoStarLevels,
    int[] BenchEchoInstanceIds,
    byte[] BenchEchoStarLevels,
    int[] MutationIds,
    int WinStreak,
    int LossStreak,
    ResonanceBonus[] ActiveResonances
);
