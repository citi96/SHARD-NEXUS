using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Reflects a portion of damage taken back to the attacker.
/// </summary>
public class EmberbladeEmpowerEffect : BaseStatusEffect
{
    public override string Id => "EmberbladeEmpower";
    private int _charges = 3;

    public EmberbladeEmpowerEffect(int duration) : base(duration) { }

    public override void OnAttack(CombatUnit unit, CombatUnit target, List<CombatUnit> allUnits, List<CombatEventRecord> events)
    {
        if (_charges > 0)
        {
            _charges--;
            target.AddEffect(new BurnEffect(180, 30)); // 3s burn
            if (_charges <= 0) RemainingTicks = 0;
        }
    }

    public override void ModifyStats(ref CombatUnitStats stats)
    {
        if (_charges > 0)
        {
            stats = stats with { Attack = stats.Attack * 150 / 100 };
        }
    }
}
