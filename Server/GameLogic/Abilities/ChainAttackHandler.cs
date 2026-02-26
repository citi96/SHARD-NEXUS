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

    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, List<CombatEventRecord> events)
    {
        var targets = allUnits
            .Where(u => u.IsAlive && u.Team != caster.Team)
            .OrderBy(u => ChebyshevDistance(caster, u))
            .Take(_targetsCount)
            .ToList();

        foreach (var target in targets)
        {
            int baseDamage = caster.GetEffectiveStats().Attack;
            int damage = baseDamage * _damagePct / 100;
            
            target.Hp -= damage; 

            events.Add(new CombatEventRecord
            {
                Type = "chain_attack",
                Attacker = caster.InstanceId,
                Target = target.InstanceId,
                Damage = damage
            });

            if (target.Hp <= 0 && target.IsAlive)
            {
                target.IsAlive = false;
                events.Add(new CombatEventRecord { Type = "death", Target = target.InstanceId });
            }
        }
    }

    private int ChebyshevDistance(CombatUnit a, CombatUnit b)
        => Math.Max(Math.Abs(a.Col - b.Col), Math.Abs(a.Row - b.Row));
}
