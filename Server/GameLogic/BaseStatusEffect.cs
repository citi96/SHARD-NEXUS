using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic;

/// <summary>
/// Abstract base class for status effects to reduce boilerplate.
/// </summary>
public abstract class BaseStatusEffect : IStatusEffect
{
    public abstract string Id { get; }
    public int DurationTicks { get; protected set; }
    public int RemainingTicks { get; protected set; }
    public bool IsExpired => RemainingTicks <= 0;
    public virtual bool PreventsActions => false;

    protected BaseStatusEffect(int durationTicks)
    {
        DurationTicks = durationTicks;
        RemainingTicks = durationTicks;
    }

    public virtual void OnApply(CombatUnit unit) { }

    public virtual void OnTick(CombatUnit unit, int currentTick, List<CombatEventRecord> events)
    {
        RemainingTicks--;
    }

    public virtual void OnRemove(CombatUnit unit) { }

    public virtual void ModifyStats(ref CombatUnitStats stats) { }

    public virtual void OnAttack(CombatUnit unit, CombatUnit target, List<CombatUnit> allUnits, List<CombatEventRecord> events) { }

    public virtual void OnBeforeTakeDamage(CombatUnit unit, ref int damage, List<CombatEventRecord> events) { }
}
