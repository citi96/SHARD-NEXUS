using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Deals damage over time according to a DPS value.
/// </summary>
public class BurnEffect : BaseStatusEffect
{
    public override string Id => "Burn";
    private readonly int _dps;
    private int _accumulator;

    public BurnEffect(int durationTicks, int dps) : base(durationTicks)
    {
        _dps = dps;
    }

    public override void OnTick(CombatUnit unit, int currentTick, List<CombatEventRecord> events)
    {
        base.OnTick(unit, currentTick, events);

        _accumulator += _dps;
        int damage = _accumulator / 60; // Assuming 60Hz
        if (damage > 0)
        {
            _accumulator %= 60;
            unit.Hp -= damage;
            if (unit.Hp <= 0 && unit.IsAlive)
            {
                unit.IsAlive = false;
                events.Add(new CombatEventRecord { Type = "death", Target = unit.InstanceId });
            }
        }
    }
}
