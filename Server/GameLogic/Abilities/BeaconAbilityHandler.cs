using System.Collections.Generic;
using System.Linq;
using Server.GameLogic.StatusEffects;
using Shared.Network.Messages;

namespace Server.GameLogic.Abilities;

/// <summary>
/// Illumina: Heals lowest HP ally for 25% Max HP, clears debuffs, and grants immunity for 3 seconds.
/// </summary>
public class BeaconAbilityHandler : IAbilityHandler
{
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
    {
        // Find ally with lowest HP percentage
        var targets = allUnits
            .Where(u => u.Team == caster.Team && u.IsAlive)
            .OrderBy(u => (double)u.Hp / u.GetEffectiveStats().MaxHp)
            .ToList();

        if (targets.Count == 0) return;

        var target = targets[0];
        var stats = target.GetEffectiveStats();
        
        // 25% Max HP heal
        int healAmount = stats.MaxHp * 25 / 100;
        target.Hp = Math.Min(target.Hp + healAmount, stats.MaxHp);

        // Clear debuffs
        target.ClearDebuffs();

        // 3s Immunity (180 ticks)
        target.AddEffect(new ImmunityEffect(180));

        dispatcher.Dispatch(new CombatEventRecord
        {
            Type = "ability_illumina",
            Attacker = caster.InstanceId,
            Target = target.InstanceId,
            Damage = -healAmount, // Negative damage usually represents healing in logs
            StatusEffectId = "Immunity"
        });
    }
}
