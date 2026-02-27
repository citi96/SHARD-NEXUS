using System.Collections.Generic;
using Server.GameLogic.StatusEffects;
using Shared.Network.Messages;

namespace Server.GameLogic.Abilities;

/// <summary>
/// Ardore: Buffs all allies with +30% Attack Speed for 5 seconds.
/// </summary>
public class FlameheartAbilityHandler : IAbilityHandler
{
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
    {
        int duration = CombatConstants.Ticks(5f); // 5s
        int speedBonusPct = 30;

        foreach (var unit in allUnits)
        {
            if (unit.Team == caster.Team && unit.IsAlive)
            {
                unit.AddEffect(new AttackSpeedBuffEffect(duration, speedBonusPct));
            }
        }

        dispatcher.Dispatch(new CombatEventRecord
        {
            Type = "ability_ardore",
            Attacker = caster.InstanceId,
            StatusEffectId = "AttackSpeedBuff"
        });
    }
}
