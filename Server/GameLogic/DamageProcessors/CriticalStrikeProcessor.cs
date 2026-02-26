namespace Server.GameLogic;

/// <summary>
/// Handles critical strike multiplier application.
/// </summary>
public class CriticalStrikeProcessor : IDamageProcessor
{
    public void Process(DamageContext context)
    {
        if (context.IsCrit)
        {
            var stats = context.Attacker.GetEffectiveStats();
            context.CalculatedDamage = context.CalculatedDamage * stats.CritMultiplier / 100;
        }
    }
}
