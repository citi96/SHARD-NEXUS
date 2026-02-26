using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Reflects a portion of damage taken back to the attacker.
/// </summary>
public class ReflectEffect : BaseStatusEffect
{
    public override string Id => "Reflect";
    private readonly int _reflectPct;

    public ReflectEffect(int durationTicks, int reflectPct) : base(durationTicks)
    {
        _reflectPct = reflectPct;
    }

    public override void OnBeforeTakeDamage(CombatUnit unit, CombatUnit attacker, ref int damage, List<CombatEventRecord> events)
    {
        if (damage <= 0 || !attacker.IsAlive) return;

        int reflected = damage * _reflectPct / 100;
        if (reflected > 0)
        {
            attacker.Hp -= reflected;
            events.Add(new CombatEventRecord
            {
                Type = "reflect",
                Attacker = unit.InstanceId,
                Target = attacker.InstanceId,
                Damage = reflected,
            });

            if (attacker.Hp <= 0 && attacker.IsAlive)
            {
                attacker.IsAlive = false;
                events.Add(new CombatEventRecord { Type = "death", Target = attacker.InstanceId });
            }
        }
    }

    public int ReflectPct => _reflectPct;
}
