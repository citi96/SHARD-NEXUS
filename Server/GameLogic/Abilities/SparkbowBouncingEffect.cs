using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Network.Messages;

namespace Server.GameLogic.Abilities;

public class SparkbowBouncingEffect : BaseStatusEffect
{
    public override string Id => "SparkbowBouncing";
    private int _charges;

    public SparkbowBouncingEffect(int duration, int charges) : base(duration)
    {
        _charges = charges;
    }

    public override void OnAttack(CombatUnit unit, CombatUnit target, List<CombatUnit> allUnits, List<CombatEventRecord> events)
    {
        if (_charges <= 0) return;
        _charges--;

        // Find 2 additional targets (total 3 including original)
        var targets = allUnits
            .Where(u => u.IsAlive && u.Team != unit.Team && u.InstanceId != target.InstanceId)
            .OrderBy(u => GridUtils.ChebyshevDistance(unit, u))
            .Take(2)
            .ToList();

        foreach (var t in targets)
        {
            int baseDamage = unit.GetEffectiveStats().Attack;
            int damage = baseDamage * 50 / 100; // 50% bounce damage
            t.Hp -= damage;

            events.Add(new CombatEventRecord
            {
                Type = "chain_attack",
                Attacker = unit.InstanceId,
                Target = t.InstanceId,
                Damage = damage
            });

            if (t.Hp <= 0 && t.IsAlive)
            {
                t.IsAlive = false;
                events.Add(new CombatEventRecord { Type = "death", Target = t.InstanceId });
            }
        }
    }

    public override void OnTick(CombatUnit unit, int currentTick, List<CombatEventRecord> events)
    {
        base.OnTick(unit, currentTick, events);
        if (_charges <= 0) RemainingTicks = 0;
    }
}
