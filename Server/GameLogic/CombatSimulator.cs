using System;
using System.Collections.Generic;
using System.Linq;
using Server.Configuration;
using Shared.Models.Enums;
using Shared.Models.Structs;
using Shared.Network.Messages;

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
    // Board geometry
    // Player board: 7 cols × 4 rows = 28 slots
    //   BoardIndex → col = idx % 7,  row = idx / 7
    // Team 0 occupies cols 0-6  (left side)
    // Team 1 occupies cols 7-13 (right, mirrored: col = 13 - (idx%7))
    // Combined board: 14 wide × 4 tall
    // ──────────────────────────────────────────────
    private const int BoardCols   = 7;
    private const int CombatWidth = 14;

    private readonly CombatSettings       _settings;
    private readonly InterventionSettings _intSettings;
    private readonly Dictionary<int, EchoDefinition> _catalog;
    private readonly List<CombatUnit>     _units;
    private readonly int  _p0Id;
    private readonly int  _p1Id;
    private readonly int  _round;

    // ── Incremental state ─────────────────────────
    private int  _currentTick = 0;

    public bool          IsDone      { get; private set; }
    public CombatResult? FinalResult { get; private set; }

    public CombatSimulator(
        PlayerState p0,
        PlayerState p1,
        IEnumerable<EchoDefinition> catalog,
        CombatSettings settings,
        InterventionSettings intSettings,
        int seed,
        int round)
    {
        _settings    = settings;
        _intSettings = intSettings;
        _catalog     = catalog.ToDictionary(d => d.Id);
        _p0Id        = p0.PlayerId;
        _p1Id        = p1.PlayerId;
        _round       = round;

        _units = new List<CombatUnit>();
        LoadUnits(p0, team: 0);
        LoadUnits(p1, team: 1);
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
            ApplyIntervention(inv);

        var tickEvents = new List<CombatEventRecord>();

        for (int i = 0; i < _settings.SnapshotIntervalTicks; i++)
        {
            _currentTick++;
            TickOnce(tickEvents);

            if (_currentTick >= _settings.MaxCombatTicks || IsOneSideEliminated())
            {
                IsDone = true;
                break;
            }
        }

        var snapshot = TakeSnapshot(_currentTick, tickEvents);

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

            int definitionId = instanceId / 1000;
            if (!_catalog.TryGetValue(definitionId, out var def)) continue;

            int multiplier = 100;

            int hp      = def.BaseHealth  * multiplier / 100;
            int attack  = def.BaseAttack  * multiplier / 100;
            int defense = def.BaseDefense * multiplier / 100;
            int maxMana = def.BaseMana    * multiplier / 100;

            string className = def.Class.ToString();
            int cooldown = _settings.AttackCooldownByClass.TryGetValue(className, out int cd) ? cd : 30;
            int range    = _settings.AttackRangeByClass.TryGetValue(className, out int r)    ? r  : 1;

            int boardCol  = idx % BoardCols;
            int boardRow  = idx / BoardCols;
            int combatCol = team == 0
                ? boardCol
                : CombatWidth - 1 - boardCol;

            _units.Add(new CombatUnit
            {
                InstanceId              = instanceId,
                DefinitionId            = definitionId,
                Team                    = team,
                Col                     = combatCol,
                Row                     = boardRow,
                Hp                      = hp,
                MaxHp                   = hp,
                Mana                    = 0,
                MaxMana                 = maxMana,
                Attack                  = attack,
                Defense                 = defense,
                AttackRange             = range,
                AttackCooldown          = cooldown,
                AttackCooldownRemaining = 0,
                IsAlive                 = true,
            });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Tick logic
    // ──────────────────────────────────────────────────────────────────────────

    private void TickOnce(List<CombatEventRecord> tickEvents)
    {
        var aliveUnits = _units
            .Where(u => u.IsAlive)
            .OrderBy(u => u.InstanceId)
            .ToList();

        foreach (var unit in aliveUnits)
        {
            if (!unit.IsAlive) continue;

            // Retreating units: count down and restore position when done
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

            // Tick effect timers
            if (unit.FocusTicksLeft > 0)      unit.FocusTicksLeft--;
            if (unit.SpeedBoostTicksLeft > 0)  unit.SpeedBoostTicksLeft--;

            // Normal cooldown decrement; speed boost adds a second decrement (2× speed)
            unit.AttackCooldownRemaining = Math.Max(0, unit.AttackCooldownRemaining - 1);
            if (unit.SpeedBoostTicksLeft > 0)
                unit.AttackCooldownRemaining = Math.Max(0, unit.AttackCooldownRemaining - 1);

            // Find target: Focus override, else nearest enemy
            var target = (unit.FocusTicksLeft > 0)
                ? _units.FirstOrDefault(u => u.InstanceId == unit.FocusTargetId && u.IsAlive && !u.IsRetreating)
                : FindNearestEnemy(unit);

            if (target == null) continue;

            if (ChebyshevDistance(unit, target) <= unit.AttackRange)
            {
                if (unit.AttackCooldownRemaining == 0)
                {
                    int rawDamage = Math.Max(1, unit.Attack - target.Defense);

                    // Shield absorbs damage first
                    int absorbed = Math.Min(target.Shield, rawDamage);
                    target.Shield -= absorbed;
                    int damage = rawDamage - absorbed;

                    if (damage > 0)
                        target.Hp -= damage;

                    tickEvents.Add(new CombatEventRecord
                    {
                        Type     = "attack",
                        Attacker = unit.InstanceId,
                        Target   = target.InstanceId,
                        Damage   = rawDamage,
                    });

                    unit.AttackCooldownRemaining = unit.AttackCooldown;

                    if (target.Hp <= 0)
                    {
                        target.IsAlive = false;
                        tickEvents.Add(new CombatEventRecord
                        {
                            Type   = "death",
                            Target = target.InstanceId,
                        });
                    }
                }
            }
            else
            {
                MoveToward(unit, target);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Intervention application
    // ──────────────────────────────────────────────────────────────────────────

    private void ApplyIntervention(PendingIntervention inv)
    {
        int team   = inv.Team;
        var allies = _units.Where(u => u.Team == team && u.IsAlive && !u.IsRetreating).ToList();
        var target = _units.FirstOrDefault(u => u.InstanceId == inv.TargetId && u.IsAlive);

        switch (inv.Type)
        {
            case InterventionType.Reposition:
                if (target?.Team == team)
                {
                    var free = GetAdjacentFreeCells(target);
                    if (free.Count > 0)
                    {
                        target.Col = free[0].col;
                        target.Row = free[0].row;
                    }
                }
                break;

            case InterventionType.Focus:
                if (target != null && target.Team != team)
                {
                    foreach (var ally in allies)
                    {
                        ally.FocusTargetId  = inv.TargetId;
                        ally.FocusTicksLeft = _intSettings.FocusDurationTicks;
                    }
                }
                break;

            case InterventionType.Barrier:
                if (target?.Team == team)
                    target.Shield += _intSettings.BarrierShieldHp;
                break;

            case InterventionType.Accelerate:
                foreach (var ally in allies)
                    ally.SpeedBoostTicksLeft = _intSettings.AccelerateDurationTicks;
                break;

            case InterventionType.TacticalRetreat:
                if (target?.Team == team && !target.IsRetreating)
                {
                    target.IsRetreating     = true;
                    target.RetreatTicksLeft = _intSettings.RetreatDurationTicks;
                    target.ReturnCol        = target.Col;
                    target.ReturnRow        = target.Row;
                    target.Col              = team == 0 ? 0 : CombatWidth - 1;
                }
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private CombatUnit? FindNearestEnemy(CombatUnit unit)
    {
        CombatUnit? nearest = null;
        int minDist = int.MaxValue;

        foreach (var candidate in _units)
        {
            if (!candidate.IsAlive || candidate.Team == unit.Team || candidate.IsRetreating) continue;
            int dist = ChebyshevDistance(unit, candidate);
            if (dist < minDist) { minDist = dist; nearest = candidate; }
        }

        return nearest;
    }

    private static int ChebyshevDistance(CombatUnit a, CombatUnit b)
        => Math.Max(Math.Abs(a.Col - b.Col), Math.Abs(a.Row - b.Row));

    private static void MoveToward(CombatUnit unit, CombatUnit target)
    {
        int dCol = Math.Sign(target.Col - unit.Col);
        int dRow = Math.Sign(target.Row - unit.Row);
        if (dCol != 0) unit.Col += dCol;
        else if (dRow != 0) unit.Row += dRow;
    }

    private bool IsOneSideEliminated()
    {
        bool t0 = _units.Any(u => u.Team == 0 && u.IsAlive);
        bool t1 = _units.Any(u => u.Team == 1 && u.IsAlive);
        return !t0 || !t1;
    }

    private List<(int col, int row)> GetAdjacentFreeCells(CombatUnit unit)
    {
        var occupied = _units
            .Where(u => u.IsAlive && !u.IsRetreating)
            .Select(u => (u.Col, u.Row))
            .ToHashSet();

        var result = new List<(int, int)>();
        foreach (var (dc, dr) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
        {
            int nc = unit.Col + dc, nr = unit.Row + dr;
            if (nc >= 0 && nc < CombatWidth && nr >= 0 && nr < 4 && !occupied.Contains((nc, nr)))
                result.Add((nc, nr));
        }
        return result;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Snapshot & result
    // ──────────────────────────────────────────────────────────────────────────

    private CombatSnapshotPayload TakeSnapshot(int tick, IEnumerable<CombatEventRecord> events)
        => new()
        {
            Tick   = tick,
            Units  = _units.Select(u => new CombatUnitState
            {
                Id      = u.InstanceId,
                Hp      = u.Hp,
                MaxHp   = u.MaxHp,
                Mana    = u.Mana,
                MaxMana = u.MaxMana,
                Col     = u.Col,
                Row     = u.Row,
                Alive   = u.IsAlive,
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
            winnerId    = s0.Count >= s1.Count ? _p0Id : _p1Id;
            loserId     = winnerId == _p0Id ? _p1Id : _p0Id;
            survivorIds = (winnerId == _p0Id ? s0 : s1).Select(u => u.InstanceId).ToArray();
        }

        return new CombatResult(
            WinnerPlayerId:      winnerId,
            LoserPlayerId:       loserId,
            DamageDealt:         2 + _round + survivorIds.Length,
            SurvivorInstanceIds: survivorIds,
            ReplayData:          Array.Empty<byte>());
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Supporting types
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Mutable unit state during simulation. Not shared outside this assembly.</summary>
internal sealed class CombatUnit
{
    public int  InstanceId               { get; init; }
    public int  DefinitionId             { get; init; }
    public int  Team                     { get; init; }
    public int  Col                      { get; set; }
    public int  Row                      { get; set; }
    public int  Hp                       { get; set; }
    public int  MaxHp                    { get; init; }
    public int  Mana                     { get; set; }
    public int  MaxMana                  { get; init; }
    public int  Attack                   { get; init; }
    public int  Defense                  { get; init; }
    public int  AttackRange              { get; init; }
    public int  AttackCooldown           { get; init; }
    public int  AttackCooldownRemaining  { get; set; }
    public bool IsAlive                  { get; set; }

    // ── Intervention effect state ─────────────────
    public int  Shield              { get; set; }
    public int  FocusTargetId       { get; set; } = -1;
    public int  FocusTicksLeft      { get; set; }
    public int  SpeedBoostTicksLeft { get; set; }
    public bool IsRetreating        { get; set; }
    public int  RetreatTicksLeft    { get; set; }
    public int  ReturnCol           { get; set; }
    public int  ReturnRow           { get; set; }
}

/// <summary>Output from <see cref="CombatSimulator.RunBatch"/>.</summary>
public sealed class CombatSimulationResult
{
    public CombatResult                        Result    { get; }
    public IReadOnlyList<CombatSnapshotPayload> Snapshots { get; }

    public CombatSimulationResult(CombatResult result, List<CombatSnapshotPayload> snapshots)
    {
        Result    = result;
        Snapshots = snapshots;
    }
}

/// <summary>An intervention queued by a player to be applied before the next simulation batch.</summary>
public record PendingIntervention(int PlayerId, int Team, InterventionType Type, int TargetId);
