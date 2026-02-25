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
            new(1, "Pyroth", Rarity.Common,    EchoClass.Vanguard,  Resonance.Fire,      500, 100, 50, 20, new int[]{}),
            new(2, "Aquos",  Rarity.Uncommon,  EchoClass.Caster,    Resonance.Frost,     300, 200, 70, 15, new int[]{}),
            new(3, "Terron", Rarity.Rare,      EchoClass.Vanguard,  Resonance.Earth,     800,  50, 30, 60, new int[]{}),
            new(4, "Zephyr", Rarity.Epic,      EchoClass.Assassin,  Resonance.Lightning, 400, 150, 90, 10, new int[]{}),
            new(5, "Lumin",  Rarity.Legendary, EchoClass.Support,   Resonance.Light,     600, 300, 40, 40, new int[]{}),
        });
}
