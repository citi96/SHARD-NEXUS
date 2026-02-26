using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Frostbite's buff: Next 4 attacks apply Frost stacks.
/// </summary>
public class LamaGelidaEffect : BaseBuffEffect
{
    public override string Id => "LamaGelida";
    private int _charges = 4;

    public LamaGelidaEffect(int durationTicks) : base(durationTicks) { }

    public override void OnAttack(CombatUnit unit, CombatUnit target, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
    {
        if (_charges <= 0) return;

        // Find or add FrostStack on target
        var stackEffect = target.ActiveEffects.OfType<FrostStackEffect>().FirstOrDefault();
        if (stackEffect != null)
        {
            stackEffect.AddStack(target, dispatcher);
        }
        else
        {
            target.AddEffect(new FrostStackEffect(300)); // 5s duration
        }

        _charges--;
        if (_charges <= 0)
        {
            RemainingTicks = 0; // Expire
        }

        dispatcher.Dispatch(new CombatEventRecord
        {
            Type = "frost_apply",
            Attacker = unit.InstanceId,
            Target = target.InstanceId,
            StatusEffectId = "FrostStack"
        });
    }
}
