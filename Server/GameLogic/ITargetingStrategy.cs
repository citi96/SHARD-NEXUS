using System.Collections.Generic;

namespace Server.GameLogic;

/// <summary>
/// Defines a strategy for selecting a combat target.
/// </summary>
public interface ITargetingStrategy
{
    /// <summary>
    /// Selects the best target from the available units.
    /// </summary>
    CombatUnit? SelectTarget(CombatUnit unit, List<CombatUnit> allUnits);
}
