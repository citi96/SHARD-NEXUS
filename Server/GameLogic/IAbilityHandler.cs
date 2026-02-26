using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic;

/// <summary>
/// Encapsulates a reusable combat ability.
/// </summary>
public interface IAbilityHandler
{
    void Execute(CombatUnit caster, List<CombatUnit> allUnits, List<CombatEventRecord> events);
}
