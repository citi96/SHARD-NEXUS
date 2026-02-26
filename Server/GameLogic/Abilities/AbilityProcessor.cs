using System.Collections.Generic;
using System.Linq;
using Shared.Network.Messages;

namespace Server.GameLogic.Abilities;

/// <summary>
/// Registry and executor for abilities.
/// </summary>
public static class AbilityProcessor
{
    private static readonly Dictionary<int, IAbilityHandler> _handlers = new()
    {
        { 1, new PyroShieldHandler() },
        { 2, new GlaciusWallHandler() },
        { 3, new EmberbladeAbilityHandler() },
        { 4, new ChainAttackHandler(targetsCount: 3, damagePct: 80) }, // Voltedge
        { 5, new IceshotHandler() }, // Iceshot
        { 6, new SparkbowAbilityHandler() }, // Sparkbow
    };

    public static void Cast(int abilityId, CombatUnit caster, List<CombatUnit> allUnits, List<CombatEventRecord> events)
    {
        if (_handlers.TryGetValue(abilityId, out var handler))
        {
            handler.Execute(caster, allUnits, events);
            
            events.Add(new CombatEventRecord
            {
                Type = "cast",
                Attacker = caster.InstanceId,
                AbilityId = abilityId
            });
        }
    }
}
