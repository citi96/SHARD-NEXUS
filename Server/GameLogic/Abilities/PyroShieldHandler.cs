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
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
    {
        caster.Shield += 250;
        dispatcher.Dispatch(new CombatEventRecord
        {
            Type = "ability",
            Attacker = caster.InstanceId,
            AbilityId = 4 // PyroShield
        });
    }
}
