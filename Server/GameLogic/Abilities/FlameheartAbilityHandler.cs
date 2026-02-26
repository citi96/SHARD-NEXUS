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
        // 5 seconds = 300 ticks (60 ticks per second)
        int duration = 300;
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
