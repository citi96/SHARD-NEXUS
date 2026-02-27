using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Deals damage over time according to a DPS value.
/// </summary>
public class BurnEffect : BaseDebuffEffect
{
    public override string Id => "Burn";
    private readonly int _dps;

    public BurnEffect(int durationTicks, int dps) : base(durationTicks)
    {
        _dps = dps;
    }

    public override void OnTick(CombatUnit unit, int currentTick, ICombatEventDispatcher dispatcher)
    {
        base.OnTick(unit, currentTick, dispatcher);

        if (currentTick % 60 == 0) // Once per second
        {
            unit.Hp -= _dps;
            dispatcher.Dispatch(new CombatEventRecord
            {
                Type = "burn",
                Target = unit.InstanceId,
                Damage = _dps
            });

            unit.TryMarkDead(dispatcher);
        }
    }
}
