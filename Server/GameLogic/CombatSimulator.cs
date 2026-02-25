using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Server.Configuration;
using Shared.Models.Enums;
using Shared.Models.Structs;
using Shared.Network.Messages;

namespace Server.GameLogic;

/// <summary>
/// Deterministic, server-side combat simulation.
/// Given two PlayerStates, an echo catalog, a seed and settings it runs to completion
/// and produces a <see cref="CombatSimulationResult"/> containing the outcome and all
/// snapshots needed by clients for visualisation.
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
    private const int CombatWidth = 14; // total width

    private readonly CombatSettings _settings;
    private readonly Dictionary<int, EchoDefinition> _catalog;
    private readonly List<CombatUnit> _units;
    private readonly int _seed;
    private readonly int _p0Id;
    private readonly int _p1Id;
    private readonly int _round;

    public CombatSimulator(
        PlayerState p0,
        PlayerState p1,
        IEnumerable<EchoDefinition> catalog,
        CombatSettings settings,
        int seed,
        int round)
    {
        _settings = settings;
        _catalog  = catalog.ToDictionary(d => d.Id);
        _seed     = seed;
        _round    = round;
        _p0Id     = p0.PlayerId;
        _p1Id     = p1.PlayerId;

        _units = new List<CombatUnit>();
        LoadUnits(p0, team: 0);
        LoadUnits(p1, team: 1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    public CombatSimulationResult Run()
    {
        var rng = new Random(_seed);
        var allEvents = new List<object>(); // for ReplayData
        var snapshots = new List<CombatSnapshotPayload>();

        // Snapshot at tick 0 (initial state)
        snapshots.Add(TakeSnapshot(0, Array.Empty<CombatEventRecord>()));

        for (int tick = 1; tick <= _settings.MaxCombatTicks; tick++)
        {
            var tickEvents = new List<CombatEventRecord>();

            // Process units in deterministic order
            var aliveUnits = _units
                .Where(u => u.IsAlive)
                .OrderBy(u => u.InstanceId)
                .ToList();

            foreach (var unit in aliveUnits)
            {
                if (!unit.IsAlive) continue; // may have died this tick

                unit.AttackCooldownRemaining = Math.Max(0, unit.AttackCooldownRemaining - 1);

                var target = FindNearestEnemy(unit);
                if (target == null) continue;

                if (ChebyshevDistance(unit, target) <= unit.AttackRange)
                {
                    // In range — attack if cooldown ready
                    if (unit.AttackCooldownRemaining == 0)
                    {
                        int damage = Math.Max(1, unit.Attack - target.Defense);
                        target.Hp -= damage;

                        var attackEvt = new CombatEventRecord
                        {
                            Type     = "attack",
                            Attacker = unit.InstanceId,
                            Target   = target.InstanceId,
                            Damage   = damage,
                        };
                        tickEvents.Add(attackEvt);
                        allEvents.Add(attackEvt);

                        unit.AttackCooldownRemaining = unit.AttackCooldown;

                        if (target.Hp <= 0)
                        {
                            target.IsAlive = false;
                            var deathEvt = new CombatEventRecord
                            {
                                Type   = "death",
                                Target = target.InstanceId,
                            };
                            tickEvents.Add(deathEvt);
                            allEvents.Add(deathEvt);
                        }
                    }
                }
                else
                {
                    // Move one step toward target (column first, then row)
                    MoveToward(unit, target);
                }
            }

            if (tick % _settings.SnapshotIntervalTicks == 0)
                snapshots.Add(TakeSnapshot(tick, tickEvents));

            if (IsOneSideEliminated())
                break;
        }

        return BuildResult(snapshots, allEvents);
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

            // Decode DefinitionId from the current ShopManager encoding: instanceId = defId * 1000 + rand
            int definitionId = instanceId / 1000;
            if (!_catalog.TryGetValue(definitionId, out var def)) continue;

            // Star level: default to One since there is no star-combining system yet
            int multiplier = 100; // ×1.00

            int hp      = def.BaseHealth  * multiplier / 100;
            int mana    = 0; // start with no mana
            int attack  = def.BaseAttack  * multiplier / 100;
            int defense = def.BaseDefense * multiplier / 100;
            int maxMana = def.BaseMana    * multiplier / 100;

            string className = def.Class.ToString();
            int cooldown = _settings.AttackCooldownByClass.TryGetValue(className, out int cd) ? cd : 30;
            int range    = _settings.AttackRangeByClass.TryGetValue(className, out int r)  ? r  : 1;

            // Map BoardIndex to combat board position
            int boardCol = idx % BoardCols;
            int boardRow = idx / BoardCols;
            int combatCol = team == 0
                ? boardCol                      // left side cols 0-6
                : CombatWidth - 1 - boardCol;   // right side cols 7-13 (mirrored)

            _units.Add(new CombatUnit
            {
                InstanceId               = instanceId,
                DefinitionId             = definitionId,
                Team                     = team,
                Col                      = combatCol,
                Row                      = boardRow,
                Hp                       = hp,
                MaxHp                    = hp,
                Mana                     = mana,
                MaxMana                  = maxMana,
                Attack                   = attack,
                Defense                  = defense,
                AttackRange              = range,
                AttackCooldown           = cooldown,
                AttackCooldownRemaining  = 0, // ready to attack from tick 1
                IsAlive                  = true,
            });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Per-tick helpers
    // ──────────────────────────────────────────────────────────────────────────

    private CombatUnit? FindNearestEnemy(CombatUnit unit)
    {
        CombatUnit? nearest = null;
        int minDist = int.MaxValue;

        foreach (var candidate in _units)
        {
            if (!candidate.IsAlive || candidate.Team == unit.Team) continue;
            int dist = ChebyshevDistance(unit, candidate);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private static int ChebyshevDistance(CombatUnit a, CombatUnit b)
        => Math.Max(Math.Abs(a.Col - b.Col), Math.Abs(a.Row - b.Row));

    private static void MoveToward(CombatUnit unit, CombatUnit target)
    {
        // Move one step per tick — columns first (primary axis), then rows
        int dCol = Math.Sign(target.Col - unit.Col);
        int dRow = Math.Sign(target.Row - unit.Row);

        if (dCol != 0)
            unit.Col += dCol;
        else if (dRow != 0)
            unit.Row += dRow;
    }

    private bool IsOneSideEliminated()
    {
        bool team0Alive = _units.Any(u => u.Team == 0 && u.IsAlive);
        bool team1Alive = _units.Any(u => u.Team == 1 && u.IsAlive);
        return !team0Alive || !team1Alive;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Snapshot
    // ──────────────────────────────────────────────────────────────────────────

    private CombatSnapshotPayload TakeSnapshot(int tick, IEnumerable<CombatEventRecord> events)
    {
        return new CombatSnapshotPayload
        {
            Tick   = tick,
            Units  = _units.Select(u => new CombatUnitState
            {
                Id     = u.InstanceId,
                Hp     = u.Hp,
                MaxHp  = u.MaxHp,
                Mana   = u.Mana,
                MaxMana = u.MaxMana,
                Col    = u.Col,
                Row    = u.Row,
                Alive  = u.IsAlive,
            }).ToList(),
            Events = events.ToList(),
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Result construction
    // ──────────────────────────────────────────────────────────────────────────

    private CombatSimulationResult BuildResult(
        List<CombatSnapshotPayload> snapshots,
        List<object> allEvents)
    {
        var team0Survivors = _units.Where(u => u.Team == 0 && u.IsAlive).ToList();
        var team1Survivors = _units.Where(u => u.Team == 1 && u.IsAlive).ToList();

        int winnerId, loserId;
        int[] survivorIds;

        if (team0Survivors.Count > 0 && team1Survivors.Count == 0)
        {
            // Team 0 (p0) wins
            winnerId    = _p0Id;
            loserId     = _p1Id;
            survivorIds = team0Survivors.Select(u => u.InstanceId).ToArray();
        }
        else if (team1Survivors.Count > 0 && team0Survivors.Count == 0)
        {
            // Team 1 (p1) wins
            winnerId    = _p1Id;
            loserId     = _p0Id;
            survivorIds = team1Survivors.Select(u => u.InstanceId).ToArray();
        }
        else
        {
            // Draw or max-tick reached — fewer casualties wins; else p0 wins
            winnerId    = team0Survivors.Count >= team1Survivors.Count ? _p0Id : _p1Id;
            loserId     = winnerId == _p0Id ? _p1Id : _p0Id;
            survivorIds = (winnerId == _p0Id ? team0Survivors : team1Survivors)
                            .Select(u => u.InstanceId).ToArray();
        }

        // Nexus damage = 2 + round + survivors
        int damage = 2 + _round + survivorIds.Length;

        // ReplayData: serialize all events as JSON bytes for deterministic replay
        byte[] replayData = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(allEvents));

        var result = new CombatResult(
            WinnerPlayerId:     winnerId,
            LoserPlayerId:      loserId,
            DamageDealt:        damage,
            SurvivorInstanceIds: survivorIds,
            ReplayData:         replayData);

        return new CombatSimulationResult(result, snapshots);
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Supporting types (internal to server)
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
}

/// <summary>Output from <see cref="CombatSimulator.Run()"/>.</summary>
public sealed class CombatSimulationResult
{
    public CombatResult                  Result    { get; }
    public IReadOnlyList<CombatSnapshotPayload> Snapshots { get; }

    public CombatSimulationResult(CombatResult result, List<CombatSnapshotPayload> snapshots)
    {
        Result    = result;
        Snapshots = snapshots;
    }
}
