namespace Shared.Models.Structs;

/// <summary>
/// Describes an active resonance bonus for a player's board composition.
/// Sent inside <see cref="PlayerState"/> to the client.
/// </summary>
public readonly record struct ResonanceBonus(string ResonanceType, int Count, int Tier);
