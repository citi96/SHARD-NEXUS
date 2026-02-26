using System.Collections.Generic;
using System.Linq;
using Server.Configuration;
using Shared.Models.Enums;

namespace Server.GameLogic.Interventions;

public class AccelerateHandler : IInterventionHandler
{
    public InterventionType Type => InterventionType.Accelerate;

    public void Handle(PendingIntervention intervention, List<CombatUnit> units, InterventionSettings settings, ICombatEventDispatcher dispatcher)
    {
        var allies = units.Where(u => u.Team == intervention.Team && u.IsAlive);
        foreach (var ally in allies)
        {
            ally.SpeedBoostTicksLeft = settings.AccelerateDurationTicks;
        }
    }
}
