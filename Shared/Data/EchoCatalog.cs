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
            // ID, Name, Rarity, Class, Resonance, HP, Mana, ATK, DEF, MR, AS, Range, Crit, CritMult, Abilities
            new(1, "Pyroth", Rarity.Common,    EchoClass.Vanguard,  Resonance.Fire,      650, 100, 45, 45, 30, 0.60f, 1, 0.00f, 150, new int[]{ 1 }),
            new(2, "Emberblade", Rarity.Common, EchoClass.Striker,   Resonance.Fire,      550, 100, 70, 25, 20, 0.90f, 1, 0.15f, 150, new int[]{ 3 }),
            new(3, "Voltedge", Rarity.Uncommon, EchoClass.Striker,   Resonance.Lightning, 500, 100, 80, 20, 25, 1.00f, 1, 0.20f, 150, new int[]{ 4 }),
            new(4, "Iceshot", Rarity.Common,    EchoClass.Ranger,    Resonance.Frost,     500, 100, 60, 20, 20, 1.10f, 4, 0.00f, 150, new int[]{ 5 }),
            new(5, "Sparkbow", Rarity.Uncommon, EchoClass.Ranger,    Resonance.Lightning, 480, 100, 55, 18, 22, 1.30f, 4, 0.00f, 150, new int[]{ 6 }),
            new(6, "Glacius", Rarity.Common,   EchoClass.Vanguard,  Resonance.Frost,     700, 100, 40, 40, 35, 0.55f, 1, 0.00f, 150, new int[]{ 2 }),
            // ID, Name, Rarity, Class, Resonance, HP, Mana, ATK, DEF, MR, AS, Range, Crit, CritMult, Abilities
            new(9, "Flameheart", Rarity.Common, EchoClass.Support, Resonance.Fire, 500, 100, 40, 20, 25, 0.80f, 2, 0.00f, 150, new int[]{ 7 }),
            new(10, "Beacon", Rarity.Common, EchoClass.Support, Resonance.Light, 480, 100, 35, 18, 30, 0.70f, 2, 0.00f, 150, new int[]{ 8 }),
            new(11, "Frostbite", Rarity.Common, EchoClass.Assassin, Resonance.Frost, 420, 100, 75, 15, 15, 1.20f, 1, 0.30f, 150, new int[]{ 9 }),
            new(12, "Shade", Rarity.Uncommon, EchoClass.Assassin, Resonance.Shadow, 380, 100, 85, 12, 12, 1.10f, 1, 0.35f, 180, new int[]{ 10 }),
        });
}
