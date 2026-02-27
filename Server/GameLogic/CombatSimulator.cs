using System;
using System.Collections.Generic;
using System.Linq;
using Server.Configuration;
using Shared.Models.Enums;
using Shared.Models.Structs;
using Shared.Network.Messages;
using Server.GameLogic.StatusEffects;

namespace Server.GameLogic;

/// <summary>
/// Deterministic, server-side combat simulation.
/// Given two PlayerStates, an echo catalog, a seed and settings it runs tick-by-tick
/// via <see cref="RunBatch"/> and exposes <see cref="IsDone"/> and <see cref="FinalResult"/>.
///
/// DETERMINISM CONTRACT
/// - <see cref="System.Random"/> seeded once at construction.
/// - Units processed every tick in ascending InstanceId order.
/// - All stat arithmetic is integer-only (no floating point).
/// </summary>
public sealed class CombatSimulator
{
    // ──────────────────────────────────────────────
    // Board geometry  (must match Client GridRenderer)
    // Player board: 4 cols × 4 rows = 16 slots
    //   BoardIndex → col = idx % 4,  row = idx / 4
    // Team 0 occupies cols 0-3  (left side)
    // Team 1 occupies cols 4-7  (right, mirrored)
    // Combined board: 8 wide × 4 tall
    // ──────────────────────────────────────────────
    private const int BoardCols = CombatConstants.PlayerBoardCols;       // 4
    private const int CombatWidth = CombatConstants.CombatWidth;         // 8

    private const int ManaPerAttack = 10;
    private const int ManaPerHit = 5;

    private readonly CombatSettings _settings;
    private readonly InterventionSettings _intSettings;
    private readonly ResonanceSettings _resSettings;
    private readonly Dictionary<int, EchoDefinition> _catalog;
    private readonly List<CombatUnit> _units;
    private readonly int _p0Id;
    private readonly int _p1Id;
    private readonly int _round;
    private readonly Random _rng;
    private readonly CombatUnitFactory _unitFactory;
    private readonly List<IDamageProcessor> _damagePipeline;

    // ── Incremental state ─────────────────────────
    private int _currentTick = 0;

    private readonly CombatBoard _board;
    private readonly ICombatEventDispatcher _dispatcher;

    public bool IsDone { get; private set; }
    public CombatResult? FinalResult { get; private set; }

    public CombatSimulator(
        PlayerState p0,
        PlayerState p1,
        IEnumerable<EchoDefinition> catalog,
        CombatSettings settings,
        InterventionSettings intSettings,
        ResonanceSettings resSettings,
        int seed,
        int round)
    {
        _settings = settings;
        _intSettings = intSettings;
        _resSettings = resSettings;
        _catalog = catalog.ToDictionary(d => d.Id);
        _p0Id = p0.PlayerId;
        _p1Id = p1.PlayerId;
        _round = round;
        _rng = new Random(seed);
        _unitFactory = new CombatUnitFactory(catalog, resSettings, settings);
        _damagePipeline = new List<IDamageProcessor>
        {
            new DefenseProcessor(),
            new CriticalStrikeProcessor(),
            new EffectDamageProcessor(), // Handles Reflect, Vulnerability, and Buffs via hooks
            new ShieldProcessor(),
            new HealthProcessor()
        };

        _units = new List<CombatUnit>();
        LoadUnits(p0, team: 0);
        LoadUnits(p1, team: 1);

        _board = new CombatBoard(_units);
        _dispatcher = new CombatEventBuffer();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the tick-0 snapshot (initial positions, full HP).
    /// Call once after construction to send the opening frame.
    /// </summary>
    public CombatSnapshotPayload GetInitialSnapshot()
        => TakeSnapshot(0, Array.Empty<CombatEventRecord>());

    /// <summary>
    /// Advances the simulation by <see cref="CombatSettings.SnapshotIntervalTicks"/> ticks,
    /// applying any pending interventions first.
    /// Returns the snapshot for this batch, or <c>null</c> if already done.
    /// When the method sets <see cref="IsDone"/>, <see cref="FinalResult"/> is also populated.
    /// </summary>
    public CombatSnapshotPayload? RunBatch(List<PendingIntervention> interventions)
    {
        if (IsDone) return null;

        // Apply interventions before advancing ticks
        foreach (var inv in interventions)
            InterventionProcessor.Process(inv, _units, _intSettings, _dispatcher);

        _dispatcher.Clear();

        for (int i = 0; i < _settings.SnapshotIntervalTicks; i++)
        {
            _currentTick++;
            TickOnce();

            if (_currentTick >= _settings.MaxCombatTicks || _board.IsOneSideEliminated(out _))
            {
                IsDone = true;
                break;
            }
        }

        var snapshot = TakeSnapshot(_currentTick, _dispatcher.GetCapturedEvents());

        if (IsDone)
            FinalResult = BuildResult();

        return snapshot;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Unit loading
    // ──────────────────────────────────────────────────────────────────────────

    private void LoadUnits(PlayerState state, int team)
    {
        for (int idx = 0; idx < state.BoardEchoInstanceIds.Length; idx++)
        {
            int instanceId = state.BoardEchoInstanceIds[idx];
            if (instanceId == -1) continue;

            int boardCol = idx % BoardCols;
            int boardRow = idx / BoardCols;
            int combatCol = team == 0 ? boardCol : CombatWidth - 1 - boardCol;
            byte starLevel = state.BoardEchoStarLevels[idx];

            _units.Add(_unitFactory.Create(instanceId, team, combatCol, boardRow, state.ActiveResonances, starLevel));
        }
    }


    // ──────────────────────────────────────────────────────────────────────────
    // Tick logic
    // ──────────────────────────────────────────────────────────────────────────

    private void TickOnce()
    {
        var aliveUnits = _board.GetAllAlive()
            .OrderBy(u => u.InstanceId)
            .ToList();

        foreach (var unit in aliveUnits)
        {
            if (!unit.IsAlive) continue;

            if (unit.IsRetreating)
            {
                unit.RetreatTicksLeft--;
                if (unit.RetreatTicksLeft <= 0)
                {
                    unit.IsRetreating = false;
                    unit.Col = unit.ReturnCol;
                    unit.Row = unit.ReturnRow;
                }
                continue;
            }

            if (!unit.IsActionable())
            {
                unit.UpdateEffects(_currentTick, _dispatcher);
                continue;
            }

            unit.AttackCooldownRemaining = Math.Max(0, unit.AttackCooldownRemaining - 1);
            if (unit.SpeedBoostTicksLeft > 0)
                unit.AttackCooldownRemaining = Math.Max(0, unit.AttackCooldownRemaining - 1);

            var target = (unit.FocusTicksLeft > 0)
                ? _units.FirstOrDefault(u => u.InstanceId == unit.FocusTargetId && u.IsAlive && !u.IsRetreating)
                : unit.TargetingStrategy.SelectTarget(unit, _units);

            if (target == null)
            {
                unit.UpdateEffects(_currentTick, _dispatcher);
                continue;
            }

            var stats = unit.GetEffectiveStats();

            if (GridUtils.ChebyshevDistance(unit, target) <= stats.AttackRange)
            {
                if (unit.AttackCooldownRemaining == 0)
                {
                    float roll = (float)_rng.NextDouble();
                    bool isCrit = roll < stats.CritChance;

                    // DAMAGE PIPELINE
                    var damageContext = new DamageContext(unit, target, stats.Attack, isCrit, _dispatcher);
                    foreach (var processor in _damagePipeline)
                        processor.Process(damageContext);

                    _dispatcher.Dispatch(new CombatEventRecord
                    {
                        Type = isCrit ? "crit" : "attack",
                        Attacker = unit.InstanceId,
                        Target = target.InstanceId,
                        Damage = damageContext.CalculatedDamage,
                    });

                    unit.TriggerOnAttack(target, _units, _dispatcher);
                    unit.AttackCooldownRemaining = stats.AttackCooldown;

                    // Mana regen
                    unit.Mana = Math.Min(stats.MaxMana, unit.Mana + ManaPerAttack);
                    target.Mana = Math.Min(stats.MaxMana, target.Mana + ManaPerHit);

                    if (unit.IsAlive && unit.Mana >= stats.MaxMana && unit.AbilityIds.Length > 0)
                    {
                        Abilities.AbilityProcessor.Cast(unit.AbilityIds[0], unit, _units, _dispatcher);
                        unit.Mana = 0;
                    }
                }
            }
            else
            {
                unit.MoveAccumulator += stats.MoveSpeed;
                if (unit.MoveAccumulator >= 100)
                {
                    unit.MoveAccumulator -= 100;
                    var (dc, dr) = GridUtils.GetStepToward(unit, target);
                    unit.Col += dc;
                    unit.Row += dr;
                }
            }

            unit.UpdateEffects(_currentTick, _dispatcher);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Intervention application
    // ──────────────────────────────────────────────────────────────────────────

    // Monolithic helpers removed. Extracted to CombatBoard and InterventionProcessor.

    // ──────────────────────────────────────────────────────────────────────────
    // Snapshot & result
    // ──────────────────────────────────────────────────────────────────────────

    private CombatSnapshotPayload TakeSnapshot(int tick, IEnumerable<CombatEventRecord> events)
        => new()
        {
            Tick = tick,
            Units = _units.Select(u => new CombatUnitState
            {
                Id = u.InstanceId,
                Hp = u.Hp,
                MaxHp = u.BaseMaxHp,
                Mana = u.Mana,
                MaxMana = u.BaseMaxMana,
                Shield = u.Shield,
                Col = u.Col,
                Row = u.Row,
                Alive = u.IsAlive,
            }).ToList(),
            Events = events.ToList(),
        };

    private CombatResult BuildResult()
    {
        var s0 = _units.Where(u => u.Team == 0 && u.IsAlive).ToList();
        var s1 = _units.Where(u => u.Team == 1 && u.IsAlive).ToList();

        int winnerId, loserId;
        int[] survivorIds;

        if (s0.Count > 0 && s1.Count == 0)
        {
            winnerId = _p0Id; loserId = _p1Id;
            survivorIds = s0.Select(u => u.InstanceId).ToArray();
        }
        else if (s1.Count > 0 && s0.Count == 0)
        {
            winnerId = _p1Id; loserId = _p0Id;
            survivorIds = s1.Select(u => u.InstanceId).ToArray();
        }
        else
        {
            winnerId = s0.Count >= s1.Count ? _p0Id : _p1Id;
            loserId = winnerId == _p0Id ? _p1Id : _p0Id;
            survivorIds = (winnerId == _p0Id ? s0 : s1).Select(u => u.InstanceId).ToArray();
        }

        return new CombatResult(
            WinnerPlayerId: winnerId,
            LoserPlayerId: loserId,
            DamageDealt: 2 + _round + survivorIds.Length,
            SurvivorInstanceIds: survivorIds,
            ReplayData: Array.Empty<byte>());
    }
}
