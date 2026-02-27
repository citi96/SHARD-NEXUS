using System.Collections.Generic;
using System.Linq;
using Server.Configuration;
using Shared.Models.Enums;

namespace Server.GameLogic.Interventions;

public class FocusHandler : IInterventionHandler
{
    public InterventionType Type => InterventionType.Focus;

    public void Handle(PendingIntervention intervention, List<CombatUnit> units, InterventionSettings settings, ICombatEventDispatcher dispatcher)
    {
        var target = InterventionProcessor.FindEnemyTarget(units, intervention);
        if (target == null) return;

        var allies = units.Where(u => u.Team == intervention.Team && u.IsAlive && !u.IsRetreating);
        foreach (var ally in allies)
        {
            ally.FocusTargetId = intervention.TargetId;
            ally.FocusTicksLeft = settings.FocusDurationTicks;
        }
    }
}
