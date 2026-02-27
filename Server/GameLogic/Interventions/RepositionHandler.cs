using System.Collections.Generic;
using System.Linq;
using Server.Configuration;
using Shared.Models.Enums;

namespace Server.GameLogic.Interventions;

public class RepositionHandler : IInterventionHandler
{
    public InterventionType Type => InterventionType.Reposition;

    public void Handle(PendingIntervention intervention, List<CombatUnit> units, InterventionSettings settings, ICombatEventDispatcher dispatcher)
    {
        var target = InterventionProcessor.FindAllyTarget(units, intervention);
        if (target == null) return;

        var freeCells = new CombatBoard(units).GetAdjacentFreeCells(target);
        if (freeCells.Count > 0)
        {
            (target.Col, target.Row) = freeCells[0];
        }
    }
}
