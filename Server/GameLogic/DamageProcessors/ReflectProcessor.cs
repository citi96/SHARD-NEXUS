using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic;

/// <summary>
/// Handles damage reflection logic.
/// </summary>
public class ReflectProcessor : IDamageProcessor
{
    public void Process(DamageContext context)
    {
        int damage = context.CalculatedDamage;
        foreach (var effect in context.Target.ActiveEffects)
        {
            effect.OnBeforeTakeDamage(context.Target, context.Attacker, ref damage, context.Dispatcher);
        }
        context.CalculatedDamage = damage;
    }
}
