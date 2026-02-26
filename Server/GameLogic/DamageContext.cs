using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic;

/// <summary>
/// Context for a damage calculation operation.
/// </summary>
/// <param name="Attacker">The unit dealing damage.</param>
/// <param name="Target">The unit receiving damage.</param>
/// <param name="RawDamage">The current damage value in the pipeline.</param>
/// <param name="IsCrit">Whether the attack is a critical strike.</param>
/// <param name="Events">Accumulated combat events.</param>
public record DamageContext(
    CombatUnit Attacker,
    CombatUnit Target,
    int RawDamage,
    bool IsCrit,
    ICombatEventDispatcher Dispatcher)
{
    public int CalculatedDamage { get; set; } = RawDamage;
}
