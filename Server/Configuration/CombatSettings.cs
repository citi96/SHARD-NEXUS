using System.Collections.Generic;

namespace Server.Configuration;

public class CombatSettings
{
    /// <summary>Maximum ticks before combat is forced to end (60 ticks/s × 60s = 3600).</summary>
    public int MaxCombatTicks { get; set; } = 3600;

    /// <summary>How many simulation ticks between each snapshot captured (3 → 20 snapshots/sec at 60Hz).</summary>
    public int SnapshotIntervalTicks { get; set; } = 3;

    /// <summary>How fast snapshots are streamed to clients (0.05s = 20/sec).</summary>
    public float SnapshotSendIntervalSeconds { get; set; } = 0.05f;

    /// <summary>Star-2 HP multiplier × 100 (200 = ×2.0). Integer for deterministic arithmetic.</summary>
    public int Star2HpPct { get; set; } = 200;

    /// <summary>Star-2 Attack multiplier × 100 (180 = ×1.8).</summary>
    public int Star2AttackPct { get; set; } = 180;

    /// <summary>Star-3 HP multiplier × 100 (300 = ×3.0).</summary>
    public int Star3HpPct { get; set; } = 300;

    /// <summary>Star-3 Attack multiplier × 100 (250 = ×2.5).</summary>
    public int Star3AttackPct { get; set; } = 250;

    /// <summary>Attack cooldown in ticks by EchoClass name. Lower = faster.</summary>
    public Dictionary<string, int> AttackCooldownByClass { get; set; } = new()
    {
        ["Vanguard"] = 30,
        ["Striker"] = 25,
        ["Ranger"] = 45,
        ["Caster"] = 40,
        ["Support"] = 50,
        ["Assassin"] = 20,
    };

    /// <summary>Attack range in Chebyshev distance by EchoClass name.</summary>
    public Dictionary<string, int> AttackRangeByClass { get; set; } = new()
    {
        ["Vanguard"] = 1,
        ["Striker"] = 1,
        ["Ranger"] = 3,
        ["Caster"] = 2,
        ["Support"] = 2,
        ["Assassin"] = 1,
    };
}
