using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Server.Configuration;
using Server.Network;
using Shared.Models.Enums;
using Shared.Models.Structs;
using Shared.Network.Messages;

namespace Server.GameLogic;

/// <summary>
/// Orchestrates server-side combat for one or more concurrent pairs per round.
///
/// Call <see cref="StartCombatRound"/> at the start of each combat phase.
/// Call <see cref="Update"/> every server tick (60 Hz) to advance simulation and stream snapshots.
/// Subscribe to <see cref="OnAllCombatsComplete"/> to receive round results.
/// Subscribe to <see cref="OnActionRejected"/> to relay rejection messages.
/// </summary>
public sealed class CombatManager
{
    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly PlayerManager _playerManager;
    private readonly ServerNetworkManager _network;
    private readonly IReadOnlyList<EchoDefinition> _catalog;
    private readonly CombatSettings _settings;
    private readonly InterventionSettings _intSettings;
    private readonly ResonanceSettings _resSettings;

    // ── Active combat state ───────────────────────────────────────────────────

    private readonly List<CombatContext> _activeCombats = new();
    private bool _roundInProgress;

    // ── Events ─────────────────────────────────────────────────────────────────

    /// <summary>Fired once all pairs in the current round have finished.</summary>
    public event Action<List<CombatResult>>? OnAllCombatsComplete;

    /// <summary>Fired when an intervention is rejected (clientId, action, reason).</summary>
    public event Action<int, string, string>? OnActionRejected;

    // ── Constructor ───────────────────────────────────────────────────────────

    public CombatManager(
        PlayerManager playerManager,
        ServerNetworkManager network,
        IReadOnlyList<EchoDefinition> catalog,
        CombatSettings settings,
        InterventionSettings intSettings,
        ResonanceSettings resSettings)
    {
        _playerManager = playerManager;
        _network = network;
        _catalog = catalog;
        _settings = settings;
        _intSettings = intSettings;
        _resSettings = resSettings;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts all combats for the round.
    /// Creates <see cref="CombatSimulator"/> instances but does NOT run them —
    /// simulation advances tick-by-tick in <see cref="Update"/>.
    /// </summary>
    public void StartCombatRound(IEnumerable<CombatPair> pairs, int round, int seed)
    {
        _activeCombats.Clear();
        _roundInProgress = true;

        int pairIndex = 0;
        foreach (var pair in pairs)
        {
            var maybeP0 = _playerManager.GetPlayerState(pair.P0Id);
            if (maybeP0 == null)
            {
                Console.WriteLine($"[Combat] Pair skipped: player {pair.P0Id} not found.");
                continue;
            }

            PlayerState p1State;
            bool isGhost = pair.P1Id == MatchmakingManager.GhostPlayerId;

            if (isGhost)
            {
                if (pair.GhostState == null)
                {
                    Console.WriteLine($"[Combat] Ghost pair for {pair.P0Id} has no GhostState — skipping.");
                    continue;
                }
                p1State = pair.GhostState.Value;
            }
            else
            {
                var maybeP1 = _playerManager.GetPlayerState(pair.P1Id);
                if (maybeP1 == null)
                {
                    Console.WriteLine($"[Combat] Pair skipped: player {pair.P1Id} not found.");
                    continue;
                }
                p1State = maybeP1.Value;
            }

            var p0State = maybeP0.Value;
            int pairSeed = seed ^ (pairIndex * unchecked((int)0x9E3779B9));
            pairIndex++;

            bool p0HasUnits = p0State.BoardEchoInstanceIds.Any(id => id != -1);
            bool p1HasUnits = p1State.BoardEchoInstanceIds.Any(id => id != -1);

            CombatSimulator? simulator = null;

            if (p0HasUnits && p1HasUnits)
            {
                Console.WriteLine($"[Combat] Round {round} — Player {pair.P0Id} vs " +
                                  $"{(isGhost ? "Ghost" : pair.P1Id.ToString())} (seed={pairSeed})");

                simulator = new CombatSimulator(
                    p0State, p1State, _catalog, _settings, _intSettings, _resSettings, pairSeed, round);
            }
            else
            {
                Console.WriteLine("[Combat] One side has no units — forfeit applied.");
            }

            // Build unit→team lookup for energy attribution
            var unitTeam = new Dictionary<int, int>();
            foreach (int id in p0State.BoardEchoInstanceIds.Where(x => x != -1)) unitTeam[id] = 0;
            foreach (int id in p1State.BoardEchoInstanceIds.Where(x => x != -1)) unitTeam[id] = 1;

            var ctx = new CombatContext
            {
                P0Id = pair.P0Id,
                P1Id = pair.P1Id,
                IsGhostMatch = isGhost,
                Simulator = simulator,
                ForfeitResult = simulator == null
                    ? BuildForfeitResult(p0HasUnits, pair, round)
                    : null,
                SnapshotTimer = 0f,
                ResultApplied = false,
                UnitTeam = unitTeam,
            };

            _activeCombats.Add(ctx);

            if (simulator != null)
            {
                SendCombatStarted(ctx, p0State, p1State);
                // Send tick-0 snapshot immediately
                var initial = simulator.GetInitialSnapshot();
                SendSnapshot(ctx, initial);
            }
        }

        if (_activeCombats.Count == 0)
        {
            _roundInProgress = false;
            OnAllCombatsComplete?.Invoke(new List<CombatResult>());
        }
    }

    /// <summary>
    /// Backwards-compatible single-pair entry point (delegates to <see cref="StartCombatRound"/>).
    /// </summary>
    public void StartCombat(int p0Id, int p1Id, int round, int seed)
        => StartCombatRound(
            new[] { new CombatPair { P0Id = p0Id, P1Id = p1Id } },
            round, seed);

    /// <summary>
    /// Called every server tick. Advances the simulation, processes energy, sends snapshots.
    /// Fires <see cref="OnAllCombatsComplete"/> once every pair is resolved.
    /// </summary>
    public void Update(float delta)
    {
        if (!_roundInProgress || _activeCombats.Count == 0) return;

        foreach (var ctx in _activeCombats)
        {
            if (ctx.ResultApplied) continue;

            ctx.SnapshotTimer -= delta;
            if (ctx.SnapshotTimer > 0f) continue;
            ctx.SnapshotTimer = _settings.SnapshotSendIntervalSeconds;

            UpdateCooldowns(ctx, delta);

            if (ctx.Simulator != null && !ctx.Simulator.IsDone)
            {
                var snapshot = ctx.Simulator.RunBatch(ctx.PendingInterventions);
                ctx.PendingInterventions.Clear();

                if (snapshot != null)
                {
                    ProcessEnergyFromSnapshot(ctx, snapshot);
                    SendSnapshot(ctx, snapshot);
                    SendEnergyUpdates(ctx);
                }
            }

            bool done = ctx.Simulator == null || ctx.Simulator.IsDone;
            if (done && !ctx.ResultApplied)
            {
                ApplyCombatResult(ctx);
                ctx.ResultApplied = true;
            }
        }

        if (_activeCombats.All(c => c.ResultApplied))
        {
            var results = _activeCombats
                .Where(c => c.LastResult.HasValue)
                .Select(c => c.LastResult!.Value)
                .ToList();

            _roundInProgress = false;
            Console.WriteLine($"[Combat] Round complete — {results.Count} result(s) collected.");
            OnAllCombatsComplete?.Invoke(results);
        }
    }

    /// <summary>
    /// Validates and enqueues an intervention for the next simulation batch.
    /// Sends <see cref="ActionRejectedMessage"/> if validation fails.
    /// </summary>
    public void TryQueueIntervention(int playerId, InterventionType type, int targetId)
    {
        var ctx = _activeCombats.FirstOrDefault(c => c.P0Id == playerId || c.P1Id == playerId);
        if (ctx == null || ctx.ResultApplied || ctx.Simulator == null || ctx.Simulator.IsDone)
        {
            SendRejection(playerId, "UseIntervention", "Nessun combattimento attivo");
            return;
        }

        int team = ctx.P0Id == playerId ? 0 : 1;
        string typeName = type.ToString();

        int cost = _intSettings.EnergyCosts.TryGetValue(typeName, out int c) ? c : 99;
        if (ctx.Energy[team] < cost)
        {
            SendRejection(playerId, "UseIntervention", $"Energia insufficiente ({ctx.Energy[team]}/{cost})");
            return;
        }

        if (ctx.Cooldowns[team].TryGetValue(typeName, out float cd) && cd > 0f)
        {
            SendRejection(playerId, "UseIntervention", $"Cooldown: {cd:F0}s");
            return;
        }

        // Deduct energy and start cooldown
        ctx.Energy[team] -= cost;
        ctx.Cooldowns[team][typeName] = _intSettings.CooldownSeconds.TryGetValue(typeName, out float cooldown)
            ? cooldown : 10f;

        ctx.PendingInterventions.Add(new PendingIntervention(playerId, team, type, targetId));

        // Broadcast to all clients (observer support)
        var activMsg = NetworkMessage.Create(MessageType.InterventionActivated,
            new InterventionActivatedMessage
            {
                PlayerId = playerId,
                InterventionType = typeName,
                TargetUnitId = targetId,
            });
        _network.BroadcastMessage(activMsg);

        SendEnergyUpdates(ctx);

        Console.WriteLine($"[Combat] Intervention {typeName} queued for P{playerId} (team {team}, cost {cost})");
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void SendCombatStarted(CombatContext ctx, PlayerState p0State, PlayerState p1State)
    {
        int p0OpponentId = ctx.IsGhostMatch ? MatchmakingManager.GhostPlayerId : ctx.P1Id;

        _network.SendMessage(ctx.P0Id, NetworkMessage.Create(
            MessageType.CombatStarted,
            new CombatStartedMessage { OpponentId = p0OpponentId, OpponentState = p1State }));

        if (!ctx.IsGhostMatch)
            _network.SendMessage(ctx.P1Id, NetworkMessage.Create(
                MessageType.CombatStarted,
                new CombatStartedMessage { OpponentId = ctx.P0Id, OpponentState = p0State }));
    }

    private void SendSnapshot(CombatContext ctx, CombatSnapshotPayload snapshot)
    {
        string json = JsonSerializer.Serialize(snapshot);
        var msg = NetworkMessage.Create(MessageType.CombatUpdate,
            new CombatUpdateMessage { EventJson = json });
        SendToRealPlayers(ctx, msg);
    }

    private void ProcessEnergyFromSnapshot(CombatContext ctx, CombatSnapshotPayload snapshot)
    {
        // Passive energy: increment per-team tick counter
        for (int t = 0; t < 2; t++)
        {
            ctx.PassiveTick[t] += _settings.SnapshotIntervalTicks;
            while (ctx.PassiveTick[t] >= _intSettings.PassiveEnergyIntervalTicks)
            {
                ctx.Energy[t] = Math.Min(ctx.Energy[t] + 1, _intSettings.MaxEnergy);
                ctx.PassiveTick[t] -= _intSettings.PassiveEnergyIntervalTicks;
            }
        }

        foreach (var evt in snapshot.Events)
        {
            if (evt.Type == "death")
            {
                // Dying unit's team loses a unit → opposing team gets kill energy
                if (ctx.UnitTeam.TryGetValue(evt.Target, out int dyingTeam))
                {
                    int killerTeam = 1 - dyingTeam;
                    ctx.Energy[killerTeam] = Math.Min(
                        ctx.Energy[killerTeam] + _intSettings.KillEnergyGain,
                        _intSettings.MaxEnergy);
                }
            }
            else if (evt.Type == "attack" && evt.Damage > 0)
            {
                // Team whose unit was hit accumulates damage for energy
                if (ctx.UnitTeam.TryGetValue(evt.Target, out int hitTeam))
                {
                    ctx.DamageAccum[hitTeam] += evt.Damage;
                    while (ctx.DamageAccum[hitTeam] >= _intSettings.DamageEnergyPerHp)
                    {
                        ctx.Energy[hitTeam] = Math.Min(
                            ctx.Energy[hitTeam] + 1,
                            _intSettings.MaxEnergy);
                        ctx.DamageAccum[hitTeam] -= _intSettings.DamageEnergyPerHp;
                    }
                }
            }
        }
    }

    private void UpdateCooldowns(CombatContext ctx, float delta)
    {
        for (int t = 0; t < 2; t++)
        {
            foreach (var key in ctx.Cooldowns[t].Keys.ToList())
            {
                ctx.Cooldowns[t][key] = Math.Max(0f, ctx.Cooldowns[t][key] - delta);
            }
        }
    }

    private void SendEnergyUpdates(CombatContext ctx)
    {
        var msg0 = NetworkMessage.Create(MessageType.EnergyUpdate,
            new EnergyUpdateMessage { Energy = ctx.Energy[0], MaxEnergy = _intSettings.MaxEnergy });
        _network.SendMessage(ctx.P0Id, msg0);

        if (!ctx.IsGhostMatch)
        {
            var msg1 = NetworkMessage.Create(MessageType.EnergyUpdate,
                new EnergyUpdateMessage { Energy = ctx.Energy[1], MaxEnergy = _intSettings.MaxEnergy });
            _network.SendMessage(ctx.P1Id, msg1);
        }
    }

    private void ApplyCombatResult(CombatContext ctx)
    {
        CombatResult result;

        if (ctx.Simulator != null && ctx.Simulator.FinalResult.HasValue)
            result = ctx.Simulator.FinalResult.Value;
        else if (ctx.ForfeitResult.HasValue)
            result = ctx.ForfeitResult.Value;
        else
            return;

        ctx.LastResult = result;

        if (result.LoserPlayerId != MatchmakingManager.GhostPlayerId)
            _playerManager.ModifyHP(result.LoserPlayerId, -result.DamageDealt);

        if (result.WinnerPlayerId != MatchmakingManager.GhostPlayerId)
            _playerManager.UpdateStreak(result.WinnerPlayerId, won: true);
        if (result.LoserPlayerId != MatchmakingManager.GhostPlayerId)
            _playerManager.UpdateStreak(result.LoserPlayerId, won: false);

        Console.WriteLine($"[Combat] Applied — Winner={result.WinnerPlayerId}, " +
                          $"Loser={result.LoserPlayerId}, Damage={result.DamageDealt}");

        SendToRealPlayers(ctx, NetworkMessage.Create(MessageType.CombatEnded,
            new CombatEndedMessage
            {
                WinnerId = result.WinnerPlayerId,
                DamageDealt = result.DamageDealt,
            }));
    }

    private void SendToRealPlayers(CombatContext ctx, NetworkMessage msg)
    {
        _network.SendMessage(ctx.P0Id, msg);
        if (!ctx.IsGhostMatch)
            _network.SendMessage(ctx.P1Id, msg);
    }

    private void SendRejection(int clientId, string action, string reason)
    {
        OnActionRejected?.Invoke(clientId, action, reason);
    }

    private static CombatResult BuildForfeitResult(bool p0HasUnits, CombatPair pair, int round)
    {
        int damage = 2 + round;
        int winnerId = p0HasUnits ? pair.P0Id : pair.P1Id;
        int loserId = p0HasUnits ? pair.P1Id : pair.P0Id;

        return new CombatResult(
            WinnerPlayerId: winnerId,
            LoserPlayerId: loserId,
            DamageDealt: damage,
            SurvivorInstanceIds: Array.Empty<int>(),
            ReplayData: Array.Empty<byte>());
    }

    // ── Private inner class ────────────────────────────────────────────────────

    private sealed class CombatContext
    {
        public int P0Id { get; init; }
        public int P1Id { get; init; }
        public bool IsGhostMatch { get; init; }
        public CombatSimulator? Simulator { get; init; }
        public CombatResult? ForfeitResult { get; init; }
        public CombatResult? LastResult { get; set; }
        public float SnapshotTimer { get; set; }
        public bool ResultApplied { get; set; }

        // ── Unit→team lookup ──────────────────────────────────────────────────
        public Dictionary<int, int> UnitTeam { get; init; } = new();

        // ── Energy system ─────────────────────────────────────────────────────
        public int[] Energy { get; } = new int[2];
        public int[] PassiveTick { get; } = new int[2];
        public int[] DamageAccum { get; } = new int[2];

        public Dictionary<string, float>[] Cooldowns { get; } =
            new[] { new Dictionary<string, float>(), new Dictionary<string, float>() };

        // ── Pending interventions (cleared after each RunBatch) ───────────────
        public List<PendingIntervention> PendingInterventions { get; } = new();
    }
}
