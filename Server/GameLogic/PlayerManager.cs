using System;
using System.Collections.Concurrent;
using Shared.Models.Structs;

namespace Server.GameLogic;

/// <summary>
/// Manages the runtime PlayerState for all active players in the match.
/// Handles Gold, XP, Levels, and Bench capacity.
/// </summary>
public class PlayerManager
{
    private readonly ConcurrentDictionary<int, PlayerState> _players = new();
    
    // Configurable defaults or constants
    public const int StartingGold = 2;
    public const int MaxBenchSlots = 10;
    public const int StartingLevel = 1;

    public void InitializePlayer(int playerId)
    {
        var newState = new PlayerState(
            PlayerId: playerId,
            NexusHealth: 100,
            Gold: StartingGold,
            Level: StartingLevel,
            Xp: 0,
            BoardEchoInstanceIds: Array.Empty<int>(), // Board is usually 7x4 or similar, represented by sparse array or list
            BenchEchoInstanceIds: new int[MaxBenchSlots], // Fixed size bench
            MutationIds: Array.Empty<int>(),
            WinStreak: 0,
            LossStreak: 0
        );

        // Fill bench with -1 (empty)
        for (int i = 0; i < MaxBenchSlots; i++)
        {
            newState.BenchEchoInstanceIds[i] = -1;
        }

        _players.TryAdd(playerId, newState);
    }

    public void RemovePlayer(int playerId)
    {
        _players.TryRemove(playerId, out _);
    }

    public PlayerState? GetPlayerState(int playerId)
    {
        if (_players.TryGetValue(playerId, out var state))
            return state;
        return null;
    }

    public bool HasEnoughGold(int playerId, int amount)
    {
        if (_players.TryGetValue(playerId, out var state))
        {
            return state.Gold >= amount;
        }
        return false;
    }

    public bool TryDeductGold(int playerId, int amount)
    {
        if (amount < 0) return false;

        while (true)
        {
            if (!_players.TryGetValue(playerId, out var state))
                return false;

            if (state.Gold < amount)
                return false;

            var newState = state with { Gold = state.Gold - amount };
            if (_players.TryUpdate(playerId, newState, state))
                return true;
        }
    }

    public void AddGold(int playerId, int amount)
    {
        if (amount <= 0) return;

        while (true)
        {
            if (!_players.TryGetValue(playerId, out var state))
                return;

            var newState = state with { Gold = state.Gold + amount };
            if (_players.TryUpdate(playerId, newState, state))
                return;
        }
    }

    /// <summary>
    /// Attempts to add a specific EchoInstance ID to the player's bench.
    /// Returns true if added, false if the bench is full.
    /// </summary>
    public bool TryAddToBench(int playerId, int echoInstanceId)
    {
        while (true)
        {
            if (!_players.TryGetValue(playerId, out var state))
                return false;

            int emptySlot = Array.IndexOf(state.BenchEchoInstanceIds, -1);
            if (emptySlot == -1)
                return false; // Bench fulll

            // state.BenchEchoInstanceIds is an array reference. In a purely immutable struct,
            // we'd clone the array. Since it's a record struct array, modifying the array element directly 
            // works but we must ensure we update the dictionary securely if other threads read it.
            // A better functional approach is cloning the array:
            int[] newBench = (int[])state.BenchEchoInstanceIds.Clone();
            newBench[emptySlot] = echoInstanceId;

            var newState = state with { BenchEchoInstanceIds = newBench };
            if (_players.TryUpdate(playerId, newState, state))
                return true;
        }
    }
}
