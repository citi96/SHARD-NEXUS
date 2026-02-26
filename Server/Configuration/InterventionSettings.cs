using System.Collections.Generic;

namespace Server.Configuration;

public class InterventionSettings
{
    /// <summary>Maximum energy a player can hold during combat.</summary>
    public int MaxEnergy { get; set; } = 15;

    /// <summary>Ticks between passive energy gain (+1). 120 ticks = 2 seconds at 60 Hz.</summary>
    public int PassiveEnergyIntervalTicks { get; set; } = 120;

    /// <summary>Energy gained when an ally kills an enemy unit.</summary>
    public int KillEnergyGain { get; set; } = 2;

    /// <summary>HP of damage the player's team must absorb to gain +1 energy.</summary>
    public int DamageEnergyPerHp { get; set; } = 500;

    /// <summary>Ticks for Focalizza focus duration (3 seconds at 60 Hz).</summary>
    public int FocusDurationTicks { get; set; } = 180;

    /// <summary>Shield HP granted by Barriera.</summary>
    public int BarrierShieldHp { get; set; } = 500;

    /// <summary>Ticks for Accelera speed boost (4 seconds at 60 Hz).</summary>
    public int AccelerateDurationTicks { get; set; } = 240;

    /// <summary>Ticks for Ritiro Tattico invulnerability (2 seconds at 60 Hz).</summary>
    public int RetreatDurationTicks { get; set; } = 120;

    /// <summary>Energy cost per intervention type (key = InterventionType.ToString()).</summary>
    public Dictionary<string, int> EnergyCosts { get; set; } = new()
    {
        ["Reposition"]    = 3,
        ["Focus"]         = 5,
        ["Barrier"]       = 4,
        ["Accelerate"]    = 6,
        ["TacticalRetreat"] = 8,
    };

    /// <summary>Cooldown in seconds per intervention type.</summary>
    public Dictionary<string, float> CooldownSeconds { get; set; } = new()
    {
        ["Reposition"]    = 8f,
        ["Focus"]         = 12f,
        ["Barrier"]       = 15f,
        ["Accelerate"]    = 20f,
        ["TacticalRetreat"] = 25f,
    };
}
