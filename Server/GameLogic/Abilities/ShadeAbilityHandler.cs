using System.Collections.Generic;
using Server.GameLogic.StatusEffects;

namespace Server.GameLogic.Abilities;

/// <summary>
/// Dissolvenza: 2s Invisibility. Next attack +100% damage and Stun.
/// </summary>
public class ShadeAbilityHandler : IAbilityHandler
{
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
    {
        caster.AddEffect(new InvisibilityEffect(CombatConstants.Ticks(2f)));    // 2s
        caster.AddEffect(new ShadeEmpowerEffect(CombatConstants.Ticks(5f)));    // 5s, consumed on next attack
    }
}
