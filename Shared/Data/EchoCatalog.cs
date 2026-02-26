#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shared.Models.Enums;
using Shared.Models.Structs;

namespace Shared.Data;

/// <summary>
/// Single source of truth for all EchoDefinitions.
/// Used by the server (replaces BuildMockCatalog) and by the client (name/rarity lookups).
///
/// InstanceId decoding contract (established in ShopManager):
///   definitionId = instanceId / 1000
/// </summary>
public static class EchoCatalog
{
    public static readonly IReadOnlyList<EchoDefinition> All = BuildCatalog();

    private static readonly Dictionary<int, EchoDefinition> _byId = new();

    static EchoCatalog()
    {
        foreach (var def in All)
            _byId[def.Id] = def;
    }

    /// <summary>Returns the EchoDefinition with the given definitionId, or null.</summary>
    public static EchoDefinition? GetById(int definitionId)
        => _byId.TryGetValue(definitionId, out var def) ? def : null;

    /// <summary>
    /// Decodes an instanceId using the (instanceId / 1000) contract
    /// and returns its EchoDefinition, or null if not found.
    /// </summary>
    public static EchoDefinition? GetByInstanceId(int instanceId)
        => GetById(instanceId / 1000);

    private static IReadOnlyList<EchoDefinition> BuildCatalog() =>
        new ReadOnlyCollection<EchoDefinition>(new List<EchoDefinition>
        {
            // ID, Name, Rarity, Class, Resonance, HP, Mana, ATK, DEF, MR, AS, Range, Crit, Abilities
            new(1, "Pyroth", Rarity.Common,    EchoClass.Vanguard,  Resonance.Fire,      650, 100, 45, 45, 30, 0.60f, 1, 0.00f, new int[]{ 1 }),
            new(2, "Emberblade", Rarity.Common, EchoClass.Striker,   Resonance.Fire,      550, 100, 70, 25, 20, 0.90f, 1, 0.15f, new int[]{ 3 }),
            new(3, "Voltedge", Rarity.Uncommon, EchoClass.Striker,   Resonance.Lightning, 500, 100, 80, 20, 25, 1.00f, 1, 0.20f, new int[]{ 4 }),
            new(6, "Glacius", Rarity.Common,   EchoClass.Vanguard,  Resonance.Frost,     700, 100, 40, 40, 35, 0.55f, 1, 0.00f, new int[]{ 2 }),
        });
}
