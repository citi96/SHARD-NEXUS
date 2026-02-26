using Shared.Models.Enums;

namespace Shared.Models.Structs;

/// <summary>
/// Static template data for an Echo. 
/// Sent once or loaded from a deterministic catalog on both sides.
/// </summary>
public readonly record struct EchoDefinition(
    int Id,
    string Name,
    Rarity Rarity,
    EchoClass Class,
    Resonance Resonance,
    int BaseHealth,
    int BaseMana,
    int BaseAttack,
    int BaseDefense,
    int BaseMR,
    float BaseAttackSpeed,
    int BaseAttackRange,
    float BaseCritChance,
    int[] AbilityIds
);
