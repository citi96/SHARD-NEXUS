using Shared.Network.Messages;

namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Shade's empowerment: Next attack +100% damage and Stun.
/// </summary>
public class ShadeEmpowerEffect : BaseBuffEffect
{
    public override string Id => "ShadeEmpower";

    public ShadeEmpowerEffect(int durationTicks) : base(durationTicks) { }

    public override void OnBeforeDealDamage(DamageContext context)
    {
        // +100% damage
        context.CalculatedDamage *= 2;
        
        // Remove self after one attack
        RemainingTicks = 0;
    }

    public override void OnAttack(CombatUnit unit, CombatUnit target, System.Collections.Generic.List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
    {
        // Apply Stun (Freeze for 1s)
        target.AddEffect(new FreezeEffect(60));
        
        dispatcher.Dispatch(new CombatEventRecord
        {
            Type = "shade_stun",
            Attacker = unit.InstanceId,
            Target = target.InstanceId,
            StatusEffectId = "Freeze"
        });
    }
}
