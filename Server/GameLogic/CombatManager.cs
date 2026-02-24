using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Server.Configuration;
using Server.Network;
using Shared.Models.Structs;
using Shared.Network.Messages;

namespace Server.GameLogic;

/// <summary>
/// Orchestrates server-side combat for one or more concurrent pairs per round.
///
/// Call <see cref="StartCombatRound"/> at the start of each combat phase.
/// Call <see cref="Update"/> every server tick (60 Hz) to stream snapshots.
/// Subscribe to <see cref="OnAllCombatsComplete"/> to receive round results.
/// </summary>
public sealed class CombatManager
{
    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly PlayerManager              _playerManager;
    private readonly ServerNetworkManager       _network;
    private readonly IReadOnlyList<EchoDefinition> _catalog;
    private readonly CombatSettings             _settings;

    // ── Active combat state ───────────────────────────────────────────────────

    private readonly List<CombatContext> _activeCombats = new();
    private bool _roundInProgress;

    // ── Events ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired once all pairs in the current round have finished streaming.
    /// Payload is the list of every <see cref="CombatResult"/> from this round.
    /// </summary>
    public event Action<List<CombatResult>>? OnAllCombatsComplete;

    // ── Constructor ───────────────────────────────────────────────────────────

    public CombatManager(
        PlayerManager playerManager,
        ServerNetworkManager network,
        IReadOnlyList<EchoDefinition> catalog,
        CombatSettings settings)
    {
        _playerManager = playerManager;
        _network       = network;
        _catalog       = catalog;
        _settings      = settings;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts all combats for the round. Each pair in <paramref name="pairs"/>
    /// runs as an independent <see cref="CombatContext"/>.
    /// Ghost pairs (P1Id == <see cref="MatchmakingManager.GhostPlayerId"/>) are
    /// handled transparently — only the real player receives network messages.
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

            // Determine p1 state: real player or ghost snapshot
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

            // Per-pair seed variation so pairs are deterministically independent
            int pairSeed = seed ^ (pairIndex * unchecked((int)0x9E3779B9));
            pairIndex++;

            bool p0HasUnits = p0State.BoardEchoInstanceIds.Any(id => id != -1);
            bool p1HasUnits = p1State.BoardEchoInstanceIds.Any(id => id != -1);

            CombatSimulationResult? simResult = null;

            if (p0HasUnits && p1HasUnits)
            {
                Console.WriteLine($"[Combat] Round {round} — Player {pair.P0Id} vs " +
                                  $"{(isGhost ? "Ghost" : pair.P1Id.ToString())} (seed={pairSeed})");

                var simulator = new CombatSimulator(p0State, p1State, _catalog, _settings, pairSeed, round);
                simResult = simulator.Run();

                Console.WriteLine($"[Combat] Simulation done. Winner={simResult.Result.WinnerPlayerId}, " +
                                  $"Damage={simResult.Result.DamageDealt}, " +
                                  $"Snapshots={simResult.Snapshots.Count}");
            }
            else
            {
                Console.WriteLine("[Combat] One side has no units — forfeit applied.");
            }

            var ctx = new CombatContext
            {
                P0Id         = pair.P0Id,
                P1Id         = pair.P1Id,
                IsGhostMatch = isGhost,
                Snapshots    = simResult != null
                                ? new Queue<CombatSnapshotPayload>(simResult.Snapshots)
                                : new Queue<CombatSnapshotPayload>(),
                Result       = simResult?.Result ?? BuildForfeitResult(p0HasUnits, pair, round),
                SnapshotTimer  = 0f,
                ResultApplied  = false,
            };

            _activeCombats.Add(ctx);

            // Notify real players that combat is starting
            if (simResult != null)
                SendCombatStarted(ctx, p0State, p1State);
        }

        // Edge-case: no valid pairs this round
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
    /// Called every server tick. Drains snapshot queues for all active contexts
    /// and fires <see cref="OnAllCombatsComplete"/> once every pair is resolved.
    /// </summary>
    public void Update(float delta)
    {
        if (!_roundInProgress || _activeCombats.Count == 0) return;

        foreach (var ctx in _activeCombats)
        {
            if (ctx.ResultApplied) continue;

            ctx.SnapshotTimer -= delta;
            if (ctx.SnapshotTimer > 0f) continue;

            if (ctx.Snapshots.Count > 0)
            {
                var snapshot = ctx.Snapshots.Dequeue();
                string json  = JsonSerializer.Serialize(snapshot);

                var msg = NetworkMessage.Create(
                    MessageType.CombatUpdate,
                    new CombatUpdateMessage { EventJson = json });

                SendToRealPlayers(ctx, msg);
                ctx.SnapshotTimer = _settings.SnapshotSendIntervalSeconds;
            }
            else
            {
                if (ctx.Result.HasValue)
                    ApplyCombatResult(ctx);

                ctx.ResultApplied = true;
            }
        }

        // All contexts resolved → fire completion event
        if (_activeCombats.All(c => c.ResultApplied))
        {
            var results = _activeCombats
                .Where(c => c.Result.HasValue)
                .Select(c => c.Result!.Value)
                .ToList();

            _roundInProgress = false;

            Console.WriteLine($"[Combat] Round complete — {results.Count} result(s) collected.");
            OnAllCombatsComplete?.Invoke(results);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void SendCombatStarted(CombatContext ctx, PlayerState p0State, PlayerState p1State)
    {
        int p0OpponentId = ctx.IsGhostMatch ? MatchmakingManager.GhostPlayerId : ctx.P1Id;

        var msgForP0 = NetworkMessage.Create(
            MessageType.CombatStarted,
            new CombatStartedMessage { OpponentId = p0OpponentId, OpponentState = p1State });
        _network.SendMessage(ctx.P0Id, msgForP0);

        if (!ctx.IsGhostMatch)
        {
            var msgForP1 = NetworkMessage.Create(
                MessageType.CombatStarted,
                new CombatStartedMessage { OpponentId = ctx.P0Id, OpponentState = p0State });
            _network.SendMessage(ctx.P1Id, msgForP1);
        }
    }

    private void SendToRealPlayers(CombatContext ctx, NetworkMessage msg)
    {
        _network.SendMessage(ctx.P0Id, msg);
        if (!ctx.IsGhostMatch)
            _network.SendMessage(ctx.P1Id, msg);
    }

    private void ApplyCombatResult(CombatContext ctx)
    {
        var result = ctx.Result!.Value;

        // Apply nexus damage (skip if loser is the ghost — ghost has no HP)
        if (result.LoserPlayerId != MatchmakingManager.GhostPlayerId)
            _playerManager.ModifyHP(result.LoserPlayerId, -result.DamageDealt);

        // Update streaks for real players only
        if (result.WinnerPlayerId != MatchmakingManager.GhostPlayerId)
            _playerManager.UpdateStreak(result.WinnerPlayerId, won: true);

        if (result.LoserPlayerId != MatchmakingManager.GhostPlayerId)
            _playerManager.UpdateStreak(result.LoserPlayerId, won: false);

        Console.WriteLine($"[Combat] Applied — Winner={result.WinnerPlayerId}, " +
                          $"Loser={result.LoserPlayerId}, Damage={result.DamageDealt}");

        var endMsg = NetworkMessage.Create(
            MessageType.CombatEnded,
            new CombatEndedMessage
            {
                WinnerId    = result.WinnerPlayerId,
                DamageDealt = result.DamageDealt,
            });

        SendToRealPlayers(ctx, endMsg);
    }

    private static CombatResult BuildForfeitResult(bool p0HasUnits, CombatPair pair, int round)
    {
        int damage   = 2 + round;
        int winnerId = p0HasUnits ? pair.P0Id : pair.P1Id;
        int loserId  = p0HasUnits ? pair.P1Id : pair.P0Id;

        return new CombatResult(
            WinnerPlayerId:      winnerId,
            LoserPlayerId:       loserId,
            DamageDealt:         damage,
            SurvivorInstanceIds: Array.Empty<int>(),
            ReplayData:          Array.Empty<byte>());
    }

    // ── Private inner class ────────────────────────────────────────────────────

    private sealed class CombatContext
    {
        public int  P0Id         { get; init; }
        public int  P1Id         { get; init; }
        public bool IsGhostMatch { get; init; }
        public Queue<CombatSnapshotPayload> Snapshots { get; init; } = new();
        public CombatResult? Result       { get; init; }
        public float         SnapshotTimer { get; set; }
        public bool          ResultApplied { get; set; }
    }
}
