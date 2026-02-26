using System.Collections.Generic;

namespace Server.Configuration;

public class ResonanceSettings
{
    public int[] Thresholds { get; set; } = { 2, 4, 6 };

    /// <summary>
    /// Stat bonuses per resonance per tier.
    /// Key = "ResonanceType_Tier" (e.g. "Fire_2"), Value = stat bonus dict.
    /// Supported stats: AtkPct, DefPct, HpPct, AsPct (percentage), ShieldFlat (flat value).
    /// Tiers with no stat bonus (effect-only stubs) have empty dicts.
    /// </summary>
    public Dictionary<string, Dictionary<string, int>> Bonuses { get; set; } = new()
    {
        // Fire: tier1=Burn(stub), tier2=+25%ATK, tier3=Burn6%+Ignite(stub)
        ["Fire_1"] = new(),
        ["Fire_2"] = new() { ["AtkPct"] = 25 },
        ["Fire_3"] = new() { ["AtkPct"] = 25 },
        // Frost: tier1=Slow(stub), tier2=+300Shield, tier3=Freeze(stub)
        ["Frost_1"] = new(),
        ["Frost_2"] = new() { ["ShieldFlat"] = 300 },
        ["Frost_3"] = new() { ["ShieldFlat"] = 300 },
        // Lightning: tier1=Chain(stub), tier2=+30%AS, tier3=Chain2+Stun(stub)
        ["Lightning_1"] = new(),
        ["Lightning_2"] = new() { ["AsPct"] = 30 },
        ["Lightning_3"] = new() { ["AsPct"] = 30 },
        // Earth: tier1=+20%DEF, tier2=Thorns(stub), tier3=+40%DEF+deathStun(stub)
        ["Earth_1"] = new() { ["DefPct"] = 20 },
        ["Earth_2"] = new() { ["DefPct"] = 20 },
        ["Earth_3"] = new() { ["DefPct"] = 40 },
        // Void: tier1=ManaDrain(stub), tier2=+15%ATK, tier3=Silence(stub)
        ["Void_1"] = new(),
        ["Void_2"] = new() { ["AtkPct"] = 15 },
        ["Void_3"] = new() { ["AtkPct"] = 15 },
        // Light: tier1=+10%HP, tier2=Shield500, tier3=Revive(stub)
        ["Light_1"] = new() { ["HpPct"] = 10 },
        ["Light_2"] = new() { ["ShieldFlat"] = 500 },
        ["Light_3"] = new() { ["HpPct"] = 10, ["ShieldFlat"] = 500 },
        // Shadow: tier1=+15%AS, tier2=Invis(stub), tier3=Backstab(stub)
        ["Shadow_1"] = new() { ["AsPct"] = 15 },
        ["Shadow_2"] = new() { ["AsPct"] = 15 },
        ["Shadow_3"] = new() { ["AsPct"] = 15 },
    };
}
