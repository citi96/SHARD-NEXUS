using System.Collections.Generic;
using Shared.Network.Messages;
using Server.GameLogic.StatusEffects;

namespace Server.GameLogic.Abilities;

/// <summary>
/// Handler for "Lama Ardente" (Emberblade).
/// Applies a self-buff effect that empowers next attacks with Burn.
/// </summary>
public class EmberbladeAbilityHandler : IAbilityHandler
{
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, List<CombatEventRecord> events)
    {
        caster.AddEffect(new EmberbladeEmpowerEffect(caster.BaseAttackCooldown * 4)); 
    }
}
