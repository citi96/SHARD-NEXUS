using System.Collections.Generic;
using System.Linq;
using Server.Configuration;
using Shared.Models.Enums;

namespace Server.GameLogic.Interventions;

public class BarrierHandler : IInterventionHandler
{
    public InterventionType Type => InterventionType.Barrier;

    public void Handle(PendingIntervention intervention, List<CombatUnit> units, InterventionSettings settings, ICombatEventDispatcher dispatcher)
    {
        var target = units.FirstOrDefault(u => u.InstanceId == intervention.TargetId && u.IsAlive);
        if (target != null && target.Team == intervention.Team)
        {
            target.Shield += settings.BarrierShieldHp;
        }
    }
}
