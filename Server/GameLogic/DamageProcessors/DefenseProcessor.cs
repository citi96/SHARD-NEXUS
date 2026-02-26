using System;

namespace Server.GameLogic;

/// <summary>
/// Handles base physical damage reduction via defense.
/// </summary>
public class DefenseProcessor : IDamageProcessor
{
    public void Process(DamageContext context)
    {
        // Damage = Max(1, Attack - Defense)
        context.CalculatedDamage = Math.Max(1, context.CalculatedDamage - context.Target.BaseDefense);
    }
}
