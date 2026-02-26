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
}
