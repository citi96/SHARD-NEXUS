using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Reflects a portion of damage taken back to the attacker.
/// </summary>
public class ReflectEffect : BaseStatusEffect
{
    public override string Id => "Reflect";
    private readonly int _reflectPct;

    public ReflectEffect(int durationTicks, int reflectPct) : base(durationTicks)
    {
        _reflectPct = reflectPct;
    }

    public override void OnBeforeTakeDamage(CombatUnit unit, ref int damage, List<CombatEventRecord> events)
    {
        // Reflection is complex because we need the original attacker context.
        // We can handle this by adding a "Reflection" event in the simulator 
        // that checks for this effect's presence.
    }

    public int ReflectPct => _reflectPct;
}
