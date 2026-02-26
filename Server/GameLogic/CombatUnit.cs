using System;

namespace Server.GameLogic;

/// <summary>Mutable unit state during simulation. Not shared outside this assembly.</summary>
internal sealed class CombatUnit
{
    public int InstanceId { get; init; }
    public int DefinitionId { get; init; }
    public int Team { get; init; }
    public int Col { get; set; }
    public int Row { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; init; }
    public int Mana { get; set; }
    public int MaxMana { get; init; }
    public int Attack { get; init; }
    public int Defense { get; init; }
    public int Mr { get; init; }
    public int AttackRange { get; init; }
    public float CritChance { get; set; }
    public int CritMultiplier { get; set; } = 150;
    public int AttackCooldown { get; init; }
    public int AttackCooldownRemaining { get; set; }
    public bool IsAlive { get; set; }

    // Ability
    public int[] AbilityIds { get; init; } = Array.Empty<int>();

    // Intervention effects
    public int Shield { get; set; }
    public int FocusTargetId { get; set; } = -1;
    public int FocusTicksLeft { get; set; }
    public int SpeedBoostTicksLeft { get; set; }
    public bool IsRetreating { get; set; }
    public int RetreatTicksLeft { get; set; }
    public int ReturnCol { get; set; }
    public int ReturnRow { get; set; }

    // Ability effects
    public int DamageReflectPct { get; set; }
    public int DamageReflectTicksLeft { get; set; }
    public int SlowPct { get; set; }
    public int SlowTicksLeft { get; set; }
    public int MoveAccumulator { get; set; } = 100;

    // Striker specific effects
    public int EmberbladeEmpoweredAttacks { get; set; }
    public int BurnDps { get; set; }
    public int BurnTicksLeft { get; set; }
}
