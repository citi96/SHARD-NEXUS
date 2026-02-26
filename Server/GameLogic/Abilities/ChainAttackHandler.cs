using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Network.Messages;

namespace Server.GameLogic.Abilities;

/// <summary>
/// REUSABLE Handler for Bouncing/Chain attacks (Voltedge, Sparkbow).
/// </summary>
public class ChainAttackHandler : IAbilityHandler
{
    private readonly int _targetsCount;
    private readonly int _damagePct;

    public ChainAttackHandler(int targetsCount, int damagePct)
    {
        _targetsCount = targetsCount;
        _damagePct = damagePct;
    }

    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
    {
        var targets = allUnits
            .Where(u => u.IsAlive && u.Team != caster.Team)
            .OrderBy(u => GridUtils.ChebyshevDistance(caster, u))
            .Take(_targetsCount)
            .ToList();

        foreach (var target in targets)
        {
            int baseDamage = caster.GetEffectiveStats().Attack;
            int damage = baseDamage * _damagePct / 100;
            
            target.Hp -= damage; 

            dispatcher.Dispatch(new CombatEventRecord
            {
                Type = "chain_attack",
                Attacker = caster.InstanceId,
                Target = target.InstanceId,
                Damage = damage
            });

            if (target.Hp <= 0 && target.IsAlive)
            {
                target.IsAlive = false;
                dispatcher.Dispatch(new CombatEventRecord { Type = "death", Target = target.InstanceId });
            }
        }
    }
}
