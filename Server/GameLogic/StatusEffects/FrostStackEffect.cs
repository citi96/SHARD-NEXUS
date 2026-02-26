using System;
using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Stacks up to 4 on the target.
/// At 4 stacks: Triggers 2s Freeze and applies FrostVulnerabilityEffect.
/// </summary>
public class FrostStackEffect : BaseDebuffEffect
{
    public override string Id => "FrostStack";
    private int _stacks = 1;

    public FrostStackEffect(int durationTicks) : base(durationTicks) { }

    public void AddStack(CombatUnit unit, ICombatEventDispatcher dispatcher)
    {
        _stacks++;
        RemainingTicks = DurationTicks; // Refresh duration

        if (_stacks >= 4)
        {
            // Trigger Freeze
            unit.AddEffect(new FreezeEffect(120)); // 2s = 120 ticks
            // Apply Vulnerability
            unit.AddEffect(new FrostVulnerabilityEffect(300)); // 5s = 300 ticks
            // Mark self for removal
            RemainingTicks = 0;

            dispatcher.Dispatch(new CombatEventRecord
            {
                Type = "frost_max_stacks",
                Target = unit.InstanceId,
                StatusEffectId = "FrostStack"
            });
        }
    }
}
