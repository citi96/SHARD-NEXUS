using System.Collections.Generic;
using System.Linq;
using Server.Configuration;
using Shared.Models.Enums;

namespace Server.GameLogic.Interventions;

public class TacticalRetreatHandler : IInterventionHandler
{
    public InterventionType Type => InterventionType.TacticalRetreat;

    public void Handle(PendingIntervention intervention, List<CombatUnit> units, InterventionSettings settings, ICombatEventDispatcher dispatcher)
    {
        var target = InterventionProcessor.FindAllyTarget(units, intervention);
        if (target == null || target.IsRetreating) return;

        target.IsRetreating = true;
        target.RetreatTicksLeft = settings.RetreatDurationTicks;
        target.ReturnCol = target.Col;
        target.ReturnRow = target.Row;
        target.Col = intervention.Team == 0 ? 0 : CombatConstants.CombatWidth - 1;
    }
}
