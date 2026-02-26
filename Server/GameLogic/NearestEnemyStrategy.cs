using System.Collections.Generic;
using System.Linq;

namespace Server.GameLogic;

/// <summary>
/// Target the nearest enemy based on Chebyshev distance.
/// </summary>
public class NearestEnemyStrategy : ITargetingStrategy
{
    public CombatUnit? SelectTarget(CombatUnit unit, List<CombatUnit> allUnits)
    {
        CombatUnit? nearest = null;
        int minDist = int.MaxValue;

        foreach (var candidate in allUnits)
        {
            if (!candidate.IsAlive || candidate.Team == unit.Team || candidate.IsRetreating || candidate.IsStealthed) 
                continue;

            int dist = GridUtils.ChebyshevDistance(unit, candidate);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = candidate;
            }
        }

        return nearest;
    }
}
