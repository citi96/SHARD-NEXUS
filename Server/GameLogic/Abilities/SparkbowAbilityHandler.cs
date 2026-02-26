using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic.Abilities;

/// <summary>
/// Handler for "Arco Voltaico" (Sparkbow).
/// Empower next 5 attacks to bounce.
/// </summary>
public class SparkbowAbilityHandler : IAbilityHandler
{
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, List<CombatEventRecord> events)
    {
        caster.AddEffect(new SparkbowBouncingEffect(300, 5)); // Duration or until charges empty
    }
}
