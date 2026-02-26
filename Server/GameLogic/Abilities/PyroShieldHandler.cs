using System.Collections.Generic;
using Shared.Network.Messages;
using Server.GameLogic.StatusEffects;

namespace Server.GameLogic.Abilities;

/// <summary>
/// Handler for "Scudo di Fiamme" (Pyroth).
/// Applies a reflection status effect.
/// </summary>
public class PyroShieldHandler : IAbilityHandler
{
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, List<CombatEventRecord> events)
    {
        caster.AddEffect(new ReflectEffect(240, 20)); // 4s, 20% reflect
    }
}
