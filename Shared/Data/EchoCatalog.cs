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
            // ID, Name, Rarity, Class, Resonance, HP, Mana, ATK, DEF, MR, AS, Range, Abilities
            new(1, "Pyroth", Rarity.Common,    EchoClass.Vanguard,  Resonance.Fire,      650, 100, 45, 45, 30, 0.60f, 1, new int[]{}),
            new(6, "Glacius", Rarity.Common,   EchoClass.Vanguard,  Resonance.Frost,     700, 100, 40, 40, 35, 0.55f, 1, new int[]{}),
        });
}
