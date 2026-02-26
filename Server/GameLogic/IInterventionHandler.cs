using System.Collections.Generic;
using Server.Configuration;
using Shared.Models.Enums;

namespace Server.GameLogic;

/// <summary>
/// Defines a handler for a specific type of tactical intervention.
/// </summary>
public interface IInterventionHandler
{
    InterventionType Type { get; }
    void Handle(PendingIntervention intervention, List<CombatUnit> units, InterventionSettings settings, ICombatEventDispatcher dispatcher);
}
