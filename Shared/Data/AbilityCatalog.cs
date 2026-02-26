#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shared.Data;

/// <summary>
/// Single source of truth for ability name lookups.
/// Effect logic lives in CombatSimulator.CastAbility (server-only).
/// </summary>
public static class AbilityCatalog
{
    public static readonly IReadOnlyList<AbilityRecord> All = new ReadOnlyCollection<AbilityRecord>(new List<AbilityRecord>
    {
        // ID, Name
        new(1, "Scudo di Fiamme"),   // Pyroth (Vanguard / Fire)
        new(2, "Muro di Gelo"),      // Glacius (Vanguard / Frost)
    });

    private static readonly Dictionary<int, AbilityRecord> _byId = new();

    static AbilityCatalog()
    {
        foreach (var r in All)
            _byId[r.Id] = r;
    }

    public static AbilityRecord? GetById(int id)
        => _byId.TryGetValue(id, out var r) ? r : null;
}

public readonly record struct AbilityRecord(int Id, string Name);
