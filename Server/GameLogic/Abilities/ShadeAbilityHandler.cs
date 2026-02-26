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
        // 2s Invisibility (120 ticks)
        caster.AddEffect(new InvisibilityEffect(120));
        
        // Empowerment (lasts until attack or max 5s)
        caster.AddEffect(new ShadeEmpowerEffect(300));
    }
}
