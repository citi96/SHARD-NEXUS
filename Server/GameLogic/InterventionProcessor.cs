using System.Collections.Generic;
using Server.Configuration;
using Shared.Models.Enums;
using Server.GameLogic.Interventions;

namespace Server.GameLogic;

/// <summary>
/// Registry for intervention handlers.
/// </summary>
public static class InterventionProcessor
{
    private static readonly Dictionary<InterventionType, IInterventionHandler> _handlers = new()
    {
        { InterventionType.Barrier, new BarrierHandler() },
        { InterventionType.Focus, new FocusHandler() },
        { InterventionType.Accelerate, new AccelerateHandler() },
        { InterventionType.TacticalRetreat, new TacticalRetreatHandler() },
        { InterventionType.Reposition, new RepositionHandler() }
    };

    public static void Process(PendingIntervention intervention, List<CombatUnit> units, InterventionSettings settings, ICombatEventDispatcher dispatcher)
    {
        if (_handlers.TryGetValue(intervention.Type, out var handler))
        {
            handler.Handle(intervention, units, settings, dispatcher);
        }
    }

    /// <summary>Finds a living ally matching <see cref="PendingIntervention.TargetId"/>.</summary>
    internal static CombatUnit? FindAllyTarget(List<CombatUnit> units, PendingIntervention inv)
        => units.FirstOrDefault(u => u.InstanceId == inv.TargetId && u.IsAlive && u.Team == inv.Team);

    /// <summary>Finds a living enemy matching <see cref="PendingIntervention.TargetId"/>.</summary>
    internal static CombatUnit? FindEnemyTarget(List<CombatUnit> units, PendingIntervention inv)
        => units.FirstOrDefault(u => u.InstanceId == inv.TargetId && u.IsAlive && u.Team != inv.Team);
}
