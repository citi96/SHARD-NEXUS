using System;

namespace Server.GameLogic;

/// <summary>
/// Handles shield absorption before health reduction.
/// </summary>
public class ShieldProcessor : IDamageProcessor
{
    public void Process(DamageContext context)
    {
        int absorbed = Math.Min(context.Target.Shield, context.CalculatedDamage);
        context.Target.Shield -= absorbed;
        context.CalculatedDamage -= absorbed;
    }
}
