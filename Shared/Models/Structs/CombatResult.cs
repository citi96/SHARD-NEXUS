namespace Shared.Models.Structs;

/// <summary>
/// The result of a combat phase between two players or a player and PvE.
/// </summary>
public readonly record struct CombatResult(
    int WinnerPlayerId,
    int LoserPlayerId,
    int DamageDealt,
    int[] SurvivorInstanceIds,
    byte[] ReplayData // Opaque byte array containing deterministic inputs or state steps for replay
);
