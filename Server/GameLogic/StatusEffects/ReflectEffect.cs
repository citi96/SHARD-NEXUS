using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Reflects a portion of damage taken back to the attacker.
/// </summary>
public class ReflectEffect : BaseBuffEffect
{
    public override string Id => "Reflect";
    private readonly int _reflectPct;

    public ReflectEffect(int durationTicks, int reflectPct) : base(durationTicks)
    {
        _reflectPct = reflectPct;
    }

    public override void OnBeforeReceiveDamage(DamageContext context)
    {
        if (context.CalculatedDamage <= 0 || !context.Attacker.IsAlive) return;

        int reflected = context.CalculatedDamage * _reflectPct / 100;
        if (reflected > 0)
        {
            context.Attacker.Hp -= reflected;
            context.Dispatcher.Dispatch(new CombatEventRecord
            {
                Type = "reflect",
                Attacker = context.Target.InstanceId,
                Target = context.Attacker.InstanceId,
                Damage = reflected,
                StatusEffectId = "Reflect"
            });

            if (context.Attacker.Hp <= 0 && context.Attacker.IsAlive)
            {
                context.Attacker.IsAlive = false;
                context.Dispatcher.Dispatch(new CombatEventRecord { Type = "death", Target = context.Attacker.InstanceId });
            }
        }
    }

    public int ReflectPct => _reflectPct;
}
