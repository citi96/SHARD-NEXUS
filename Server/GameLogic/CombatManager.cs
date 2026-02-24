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
/// Orchestrates server-side combat: triggers the simulation, streams snapshots to
/// clients at 20 snapshots/second, then applies the final result to player state.
///
/// Call <see cref="StartCombat"/> when the combat phase begins.
/// Call <see cref="Update"/> every server tick (60 Hz) to drain the snapshot queue.
/// </summary>
public sealed class CombatManager
{
    private readonly PlayerManager       _playerManager;
    private readonly ServerNetworkManager _network;
    private readonly IReadOnlyList<EchoDefinition> _catalog;
    private readonly CombatSettings      _settings;

    // Active combat state (at most one combat at a time for now; VS-009 will extend to N pairs)
    private Queue<CombatSnapshotPayload>? _pendingSnapshots;
    private CombatResult?                 _pendingResult;
    private int                           _activePair0;
    private int                           _activePair1;
    private float                         _snapshotTimer;
    private int                           _currentRound;
    private bool                          _resultApplied;

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

    /// <summary>
    /// Runs the deterministic simulation for the given pair and queues snapshots
    /// to stream back to both clients.
    /// </summary>
    public void StartCombat(int p0Id, int p1Id, int round, int seed)
    {
        var state0 = _playerManager.GetPlayerState(p0Id);
        var state1 = _playerManager.GetPlayerState(p1Id);

        if (state0 == null || state1 == null)
        {
            Console.WriteLine("[Combat] Cannot start: missing player state.");
            return;
        }

        var p0State = state0.Value;
        var p1State = state1.Value;

        bool p0HasUnits = p0State.BoardEchoInstanceIds.Any(id => id != -1);
        bool p1HasUnits = p1State.BoardEchoInstanceIds.Any(id => id != -1);

        if (!p0HasUnits || !p1HasUnits)
        {
            Console.WriteLine("[Combat] One or both players have no units on board — combat skipped.");
            // Still need to damage the player with no units (they forfeit)
            if (!p0HasUnits && p1HasUnits)
                ApplyForfeit(forfeitId: p0Id, winnerId: p1Id, round);
            else if (!p1HasUnits && p0HasUnits)
                ApplyForfeit(forfeitId: p1Id, winnerId: p0Id, round);
            return;
        }

        Console.WriteLine($"[Combat] Round {round} — Player {p0Id} vs Player {p1Id} (seed={seed})");

        var simulator = new CombatSimulator(p0State, p1State, _catalog, _settings, seed, round);
        var simResult = simulator.Run();

        _activePair0   = p0Id;
        _activePair1   = p1Id;
        _currentRound  = round;
        _pendingResult = simResult.Result;
        _resultApplied = false;
        _snapshotTimer = 0f;

        _pendingSnapshots = new Queue<CombatSnapshotPayload>(simResult.Snapshots);

        Console.WriteLine($"[Combat] Simulation complete. Winner={simResult.Result.WinnerPlayerId}, " +
                          $"Damage={simResult.Result.DamageDealt}, " +
                          $"Snapshots={simResult.Snapshots.Count}");

        // Notify both clients that combat is starting
        SendCombatStarted(p0Id, p1State);
        SendCombatStarted(p1Id, p0State);
    }

    /// <summary>
    /// Called every server tick. Drains the snapshot queue at the configured rate
    /// and sends <see cref="CombatEndedMessage"/> once all snapshots are flushed.
    /// </summary>
    public void Update(float delta)
    {
        if (_pendingSnapshots == null || _pendingResult == null || _resultApplied)
            return;

        _snapshotTimer -= delta;

        if (_snapshotTimer > 0f)
            return;

        if (_pendingSnapshots.Count > 0)
        {
            var snapshot = _pendingSnapshots.Dequeue();
            string json  = JsonSerializer.Serialize(snapshot);

            var msg = NetworkMessage.Create(
                MessageType.CombatUpdate,
                new CombatUpdateMessage { EventJson = json });

            _network.SendMessage(_activePair0, msg);
            _network.SendMessage(_activePair1, msg);

            _snapshotTimer = _settings.SnapshotSendIntervalSeconds;
        }
        else
        {
            // All snapshots sent — apply result and notify clients
            ApplyCombatResult(_pendingResult.Value);
            _resultApplied = true;
        }
    }

    private void SendCombatStarted(int recipientId, PlayerState opponentState)
    {
        int opponentId = recipientId == _activePair0 ? _activePair1 : _activePair0;
        var msg = NetworkMessage.Create(
            MessageType.CombatStarted,
            new CombatStartedMessage
            {
                OpponentId    = opponentId,
                OpponentState = opponentState,
            });
        _network.SendMessage(recipientId, msg);
    }

    private void ApplyCombatResult(CombatResult result)
    {
        // Damage loser's Nexus
        _playerManager.ModifyHP(result.LoserPlayerId, -result.DamageDealt);

        // Update win/loss streaks
        _playerManager.UpdateStreak(result.WinnerPlayerId, won: true);
        _playerManager.UpdateStreak(result.LoserPlayerId,  won: false);

        Console.WriteLine($"[Combat] Result applied — Winner={result.WinnerPlayerId}, " +
                          $"Loser={result.LoserPlayerId}, Damage={result.DamageDealt}");

        // Notify clients
        var endMsg = NetworkMessage.Create(
            MessageType.CombatEnded,
            new CombatEndedMessage
            {
                WinnerId    = result.WinnerPlayerId,
                DamageDealt = result.DamageDealt,
            });

        _network.SendMessage(_activePair0, endMsg);
        _network.SendMessage(_activePair1, endMsg);
    }

    private void ApplyForfeit(int forfeitId, int winnerId, int round)
    {
        int damage = 2 + round; // 0 survivors
        _playerManager.ModifyHP(forfeitId, -damage);
        _playerManager.UpdateStreak(winnerId,  won: true);
        _playerManager.UpdateStreak(forfeitId, won: false);

        var endMsg = NetworkMessage.Create(
            MessageType.CombatEnded,
            new CombatEndedMessage
            {
                WinnerId    = winnerId,
                DamageDealt = damage,
            });

        _network.SendMessage(_activePair0, endMsg);
        _network.SendMessage(_activePair1, endMsg);
    }
}