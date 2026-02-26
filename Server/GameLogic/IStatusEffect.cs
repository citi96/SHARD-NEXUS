using System;

namespace Server.GameLogic;

/// <summary>
/// Container for calculated/modified unit stats.
/// Used by the StatusEffect system to apply modifiers without altering base values.
/// </summary>
public record struct CombatUnitStats(
    int MaxHp,
    int MaxMana,
    int Attack,
    int Defense,
    int Mr,
    int AttackRange,
    int AttackCooldown,
    float CritChance,
    int CritMultiplier,
    int MoveSpeed // 100 = base, 50 = half, etc.
);

public interface IStatusEffect
{
    string Id { get; }
    void OnApply(CombatUnit unit);
    void OnTick(CombatUnit unit, int currentTick, List<Shared.Network.Messages.CombatEventRecord> events);
    void OnRemove(CombatUnit unit);
    
    /// <summary>
    /// Modifies the effective stats of the unit.
    /// Priority can be handled by the execution order in the manager.
    /// </summary>
    void ModifyStats(ref CombatUnitStats stats);

    /// <summary>
    /// Hook for when the unit performs an attack.
    /// </summary>
    void OnAttack(CombatUnit unit, CombatUnit target, List<CombatUnit> allUnits, List<Shared.Network.Messages.CombatEventRecord> events);

    /// <summary>
    /// Hook for damage modification (e.g. Damage Reflection, Damage Reduction).
    /// </summary>
    void OnBeforeTakeDamage(CombatUnit unit, ref int damage, List<Shared.Network.Messages.CombatEventRecord> events);

    bool IsExpired { get; }
    bool PreventsActions { get; }
}
