using System.Collections.Generic;
using Server.GameLogic.StatusEffects;

namespace Server.GameLogic.Abilities;

/// <summary>
/// Lama Gelida: Next 4 attacks apply Frost stacks.
/// </summary>
public class FrostbiteAbilityHandler : IAbilityHandler
{
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
    {
        caster.AddEffect(new LamaGelidaEffect(600)); // 10s window to use 4 attacks
    }
}
