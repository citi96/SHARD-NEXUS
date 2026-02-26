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
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, List<CombatEventRecord> events)
    {
        caster.Shield += 300;
        foreach (var enemy in allUnits.Where(u => u.IsAlive && u.Team != caster.Team))
        {
            if (ChebyshevDistance(caster, enemy) <= 1)
            {
                enemy.AddEffect(new SlowEffect(180, 40)); // 3s, 40% slow
            }
        }
    }

    private int ChebyshevDistance(CombatUnit a, CombatUnit b)
        => Math.Max(Math.Abs(a.Col - b.Col), Math.Abs(a.Row - b.Row));
}
