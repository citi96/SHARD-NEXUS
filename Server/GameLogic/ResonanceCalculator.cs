using System;
using System.Collections.Generic;
using Shared.Data;
using Shared.Models.Enums;
using Shared.Models.Structs;

namespace Server.GameLogic;

/// <summary>
/// Pure static calculator: given a board composition and thresholds,
/// returns the active resonance bonuses.
/// Prism echoes count as wildcard for every resonance type.
/// </summary>
public static class ResonanceCalculator
{
    public static ResonanceBonus[] Calculate(int[] boardEchoInstanceIds, int[] thresholds)
    {
        // Count echoes per resonance type
        var counts = new Dictionary<Resonance, int>();
        int prismCount = 0;

        foreach (int instanceId in boardEchoInstanceIds)
        {
            if (instanceId == -1) continue;

            var def = EchoCatalog.GetByInstanceId(instanceId);
            if (def == null) continue;

            if (def.Value.Resonance == Resonance.Prism)
            {
                prismCount++;
            }
            else
            {
                counts.TryGetValue(def.Value.Resonance, out int current);
                counts[def.Value.Resonance] = current + 1;
            }
        }

        // Build result: for each non-Prism resonance with count > 0, add Prism and compute tier
        var result = new List<ResonanceBonus>();

        foreach (var (resonance, rawCount) in counts)
        {
            int effectiveCount = rawCount + prismCount;
            int tier = 0;

            for (int t = 0; t < thresholds.Length; t++)
            {
                if (effectiveCount >= thresholds[t])
                    tier = t + 1;
            }

            if (tier > 0)
                result.Add(new ResonanceBonus(resonance.ToString(), effectiveCount, tier));
        }

        return result.ToArray();
    }
}
