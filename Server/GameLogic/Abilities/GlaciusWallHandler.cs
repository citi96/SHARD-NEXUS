using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Network.Messages;
using Server.GameLogic.StatusEffects;

namespace Server.GameLogic.Abilities;

/// <summary>
/// Handler for "Muro di Gelo" (Glacius).
/// Grants shield and slows/freezes nearby enemies.
/// </summary>
public class GlaciusWallHandler : IAbilityHandler
{
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
    {
        caster.Shield += 300;
        foreach (var enemy in allUnits.Where(u => u.IsAlive && u.Team != caster.Team))
        {
            if (GridUtils.ChebyshevDistance(caster, enemy) <= 1)
            {
                enemy.AddEffect(new SlowEffect(CombatConstants.Ticks(3f), 40)); // 3s, 40% slow
            }
        }
    }
}
