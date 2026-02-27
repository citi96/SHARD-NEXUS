using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic.Abilities;

/// <summary>
/// Handler for "Arco Voltaico" (Sparkbow).
/// Empower next 5 attacks to bounce.
/// </summary>
public class SparkbowAbilityHandler : IAbilityHandler
{
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
    {
        caster.AddEffect(new SparkbowBouncingEffect(CombatConstants.Ticks(5f), 5)); // 5s or until charges empty
    }
}
