using System.Collections.Generic;
using System.Linq;

namespace Server.GameLogic;

/// <summary>
/// Target the farthest enemy based on Chebyshev distance.
/// Commonly used by Assassins to jump to the backline.
/// </summary>
public class FarthestEnemyStrategy : ITargetingStrategy
{
    public CombatUnit? SelectTarget(CombatUnit unit, List<CombatUnit> allUnits)
    {
        CombatUnit? farthest = null;
        int maxDist = -1;

        foreach (var candidate in allUnits)
        {
            if (!candidate.IsAlive || candidate.Team == unit.Team || candidate.IsRetreating || candidate.IsStealthed)
                continue;

            int dist = GridUtils.ChebyshevDistance(unit, candidate);
            if (dist > maxDist)
            {
                maxDist = dist;
                farthest = candidate;
            }
            else if (dist == maxDist && farthest != null)
            {
                // Tie-breaker: lowest instance ID for determinism
                if (candidate.InstanceId < farthest.InstanceId)
                {
                    farthest = candidate;
                }
            }
        }

        return farthest;
    }
}
