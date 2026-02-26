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
        var target = units.FirstOrDefault(u => u.InstanceId == intervention.TargetId && u.IsAlive);
        if (target != null && target.Team == intervention.Team)
        {
            // Logic to find adjacent free cell
            var occupied = units.Where(u => u.IsAlive && !u.IsRetreating).Select(u => (u.Col, u.Row)).ToHashSet();
            foreach (var (dc, dr) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                int nc = target.Col + dc, nr = target.Row + dr;
                if (nc >= 0 && nc < 14 && nr >= 0 && nr < 4 && !occupied.Contains((nc, nr)))
                {
                    target.Col = nc;
                    target.Row = nr;
                    break;
                }
            }
        }
    }
}
