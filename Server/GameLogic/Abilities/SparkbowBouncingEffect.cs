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

    public override void OnAttack(CombatUnit unit, CombatUnit target, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
    {
        if (_charges <= 0) return;
        _charges--;

        // Find 2 additional targets (total 3 including original)
        var targets = allUnits
            .Where(u => u.IsAlive && u.Team != unit.Team && u.InstanceId != target.InstanceId)
            .OrderBy(u => GridUtils.ChebyshevDistance(unit, u))
            .Take(2)
            .ToList();

        var stats = unit.GetEffectiveStats();
        int bounceDamage = stats.Attack * 50 / 100;

        foreach (var t in targets)
        {
            // Simple damage for bounce (no pipeline for secondary hits for now)
            t.Hp -= bounceDamage;
            dispatcher.Dispatch(new CombatEventRecord
            {
                Type = "bounce",
                Attacker = unit.InstanceId,
                Target = t.InstanceId,
                Damage = bounceDamage
            });

            if (t.Hp <= 0 && t.IsAlive)
            {
                t.IsAlive = false;
                dispatcher.Dispatch(new CombatEventRecord { Type = "death", Target = t.InstanceId });
            }
        }
    }

    public override void OnTick(CombatUnit unit, int currentTick, ICombatEventDispatcher dispatcher)
    {
        base.OnTick(unit, currentTick, dispatcher);
        if (_charges <= 0) RemainingTicks = 0;
    }
}
