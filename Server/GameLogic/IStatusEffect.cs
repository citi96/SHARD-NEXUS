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
    void OnTick(CombatUnit unit, int currentTick, ICombatEventDispatcher dispatcher);
    void OnRemove(CombatUnit unit);
    
    /// <summary>
    /// Modifies the effective stats of the unit.
    /// Priority can be handled by the execution order in the manager.
    /// </summary>
    void ModifyStats(ref CombatUnitStats stats);

    /// <summary>
    /// Hook for when the unit performs an attack.
    /// </summary>
    void OnAttack(CombatUnit unit, CombatUnit target, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher);

    /// <summary>
    /// Called on the target of an attack before damage is deducted from Shield/Hp.
    /// Can modify the damage value.
    /// </summary>
    void OnBeforeReceiveDamage(DamageContext context);

    /// <summary>
    /// Called on the attacker before damage is processed.
    /// </summary>
    void OnBeforeDealDamage(DamageContext context);

    bool IsExpired { get; }
    bool PreventsActions { get; }
    bool IsDebuff { get; }
    bool IsStealthed { get; }
}
