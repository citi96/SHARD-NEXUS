namespace Server.GameLogic;

/// <summary>
/// Processor that executes IStatusEffect damage hooks (BeforeDeal/BeforeReceive).
/// Adheres to SOLID by delegating damage modification to the effects themselves.
/// </summary>
public class EffectDamageProcessor : IDamageProcessor
{
    public void Process(DamageContext context)
    {
        // 1. Attacker's effects (modify outgoing damage)
        foreach (var effect in context.Attacker.ActiveEffects)
        {
            effect.OnBeforeDealDamage(context);
        }

        // 2. Target's effects (modify incoming damage, e.g. Vulnerability, Reflect)
        foreach (var effect in context.Target.ActiveEffects)
        {
            effect.OnBeforeReceiveDamage(context);
        }
    }
}
