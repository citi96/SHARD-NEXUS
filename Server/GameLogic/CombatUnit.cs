using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.GameLogic;

/// <summary>Mutable unit state during simulation. Not shared outside this assembly.</summary>
public sealed class CombatUnit
{
    public int InstanceId { get; init; }
    public int DefinitionId { get; init; }
    public int Team { get; init; }
    public int Col { get; set; }
    public int Row { get; set; }
    public int Hp { get; set; }
    public int Mana { get; set; }
    public bool IsAlive { get; set; } = true;

    // Base Stats (from Definition)
    public int BaseMaxHp { get; init; }
    public int BaseMaxMana { get; init; }
    public int BaseAttack { get; init; }
    public int BaseDefense { get; init; }
    public int BaseMr { get; init; }
    public int BaseAttackRange { get; init; }
    public int BaseAttackCooldown { get; init; }
    public float BaseCritChance { get; init; }
    public int BaseCritMultiplier { get; init; } = 150;

    // State
    public int AttackCooldownRemaining { get; set; }
    public int[] AbilityIds { get; init; } = Array.Empty<int>();
    public int Shield { get; set; }
    public int MoveAccumulator { get; set; } = 100;

    // Intervention effects
    public int FocusTargetId { get; set; } = -1;
    public int FocusTicksLeft { get; set; }
    public int SpeedBoostTicksLeft { get; set; }
    public bool IsRetreating { get; set; }
    public int RetreatTicksLeft { get; set; }
    public int ReturnCol { get; set; }
    public int ReturnRow { get; set; }

    public ITargetingStrategy TargetingStrategy { get; set; } = new NearestEnemyStrategy();

    // Stats
    private readonly List<IStatusEffect> _activeEffects = new();
    public IReadOnlyList<IStatusEffect> ActiveEffects => _activeEffects;

    public void AddEffect(IStatusEffect effect)
    {
        if (effect.IsDebuff && _activeEffects.Any(e => e.Id == "Immunity" && !e.IsExpired))
        {
            return;
        }

        effect.OnApply(this);
        _activeEffects.Add(effect);
    }

    public void ClearDebuffs()
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            if (_activeEffects[i].IsDebuff)
            {
                _activeEffects[i].OnRemove(this);
                _activeEffects.RemoveAt(i);
            }
        }
    }

    public void UpdateEffects(int currentTick, ICombatEventDispatcher dispatcher)
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = _activeEffects[i];
            effect.OnTick(this, currentTick, dispatcher);
            if (effect.IsExpired)
            {
                effect.OnRemove(this);
                _activeEffects.RemoveAt(i);
            }
        }
    }

    public void TriggerOnAttack(CombatUnit target, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
    {
        foreach (var effect in _activeEffects)
        {
            effect.OnAttack(this, target, allUnits, dispatcher);
        }
    }

    public CombatUnitStats GetEffectiveStats()
    {
        var stats = new CombatUnitStats(
            BaseMaxHp, BaseMaxMana, BaseAttack, BaseDefense, BaseMr,
            BaseAttackRange, BaseAttackCooldown, BaseCritChance, BaseCritMultiplier, 
            100 // Base MoveSpeed
        );

        foreach (var effect in _activeEffects)
        {
            effect.ModifyStats(ref stats);
        }

        return stats;
    }


    public bool IsStealthed => _activeEffects.Any(e => e.IsStealthed);

    public bool IsActionable()
    {
        return IsAlive && !IsRetreating && !_activeEffects.Any(e => e.PreventsActions);
    }
}
