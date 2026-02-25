using System;
using System.Collections.Concurrent;
using System.Linq;
using Server.Configuration;
using Shared.Models.Structs;

namespace Server.GameLogic;

/// <summary>
/// Manages the runtime PlayerState for all active players in the match.
/// Handles Gold, XP, Levels, and Bench capacity.
/// </summary>
public class PlayerManager
{
    private readonly PlayerSettings _settings;
    private readonly ConcurrentDictionary<int, PlayerState> _players = new();

    public event Action<int, PlayerState>? OnPlayerStateChanged;
    public event Action<int>? OnPlayerEliminated;

    public PlayerManager(PlayerSettings settings)
    {
        _settings = settings;
    }

    public void InitializePlayer(int playerId)
    {
        var newState = new PlayerState(
            PlayerId: playerId,
            NexusHealth: _settings.StartingHP,
            Gold: _settings.StartingGold,
            Level: 1,
            Xp: 0,
            BoardEchoInstanceIds: new int[_settings.BoardSlots], // 4 cols × 4 rows = 16 slots (index = row * 4 + col)
            BenchEchoInstanceIds: new int[_settings.BenchSlots],
            MutationIds: Array.Empty<int>(),
            WinStreak: 0,
            LossStreak: 0
        );

        for (int i = 0; i < _settings.BoardSlots; i++) newState.BoardEchoInstanceIds[i] = -1;
        for (int i = 0; i < _settings.BenchSlots; i++) newState.BenchEchoInstanceIds[i] = -1;

        if (_players.TryAdd(playerId, newState))
        {
            OnPlayerStateChanged?.Invoke(playerId, newState);
        }
    }

    public void RemovePlayer(int playerId)
    {
        _players.TryRemove(playerId, out _);
    }

    /// <summary>Returns a snapshot of all currently registered player IDs.</summary>
    public IReadOnlyList<int> GetAllPlayerIds() => _players.Keys.ToList();

    public PlayerState? GetPlayerState(int playerId)
    {
        if (_players.TryGetValue(playerId, out var state))
            return state;
        return null;
    }

    public bool HasEnoughGold(int playerId, int amount)
    {
        if (_players.TryGetValue(playerId, out var state))
            return state.Gold >= amount;
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

            var newState = state with { Gold = Math.Max(0, state.Gold - amount) };
            if (_players.TryUpdate(playerId, newState, state))
            {
                OnPlayerStateChanged?.Invoke(playerId, newState);
                return true;
            }
        }
    }

    public void AddGold(int playerId, int amount)
    {
        if (amount <= 0) return;

        while (true)
        {
            if (!_players.TryGetValue(playerId, out var state))
                return;

            int newGold = Math.Min(state.Gold + amount, _settings.MaxGold);
            var newState = state with { Gold = newGold };
            if (_players.TryUpdate(playerId, newState, state))
            {
                OnPlayerStateChanged?.Invoke(playerId, newState);
                return;
            }
        }
    }

    public void ModifyHP(int playerId, int amount)
    {
        while (true)
        {
            if (!_players.TryGetValue(playerId, out var state))
                return;

            var newState = state with { NexusHealth = Math.Max(0, state.NexusHealth + amount) };
            if (_players.TryUpdate(playerId, newState, state))
            {
                OnPlayerStateChanged?.Invoke(playerId, newState);

                if (newState.NexusHealth <= 0)
                {
                    OnPlayerEliminated?.Invoke(playerId);
                }
                return;
            }
        }
    }

    public void AddXP(int playerId, int amount)
    {
        if (amount <= 0) return;

        while (true)
        {
            if (!_players.TryGetValue(playerId, out var state))
                return;

            int currentXp = state.Xp + amount;
            int currentLevel = state.Level;

            // Loop checking if we can level up
            while (true)
            {
                if (_settings.XpToLevel.TryGetValue(currentLevel.ToString(), out int requiredXp))
                {
                    if (currentXp >= requiredXp)
                    {
                        currentXp -= requiredXp;
                        currentLevel++;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    // Reached max level configured
                    currentXp = 0;
                    break;
                }
            }

            var newState = state with { Xp = currentXp, Level = currentLevel };
            if (_players.TryUpdate(playerId, newState, state))
            {
                OnPlayerStateChanged?.Invoke(playerId, newState);
                return;
            }
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
                return false;

            int[] newBench = (int[])state.BenchEchoInstanceIds.Clone();
            newBench[emptySlot] = echoInstanceId;

            var newState = state with { BenchEchoInstanceIds = newBench };
            if (_players.TryUpdate(playerId, newState, state))
            {
                OnPlayerStateChanged?.Invoke(playerId, newState);
                return true;
            }
        }
    }

    /// <summary>
    /// Updates win/loss streak after a combat result.
    /// A win resets the loss streak; a loss resets the win streak.
    /// </summary>
    public void UpdateStreak(int playerId, bool won)
    {
        while (true)
        {
            if (!_players.TryGetValue(playerId, out var state)) return;

            var newState = won
                ? state with { WinStreak = state.WinStreak + 1, LossStreak = 0 }
                : state with { LossStreak = state.LossStreak + 1, WinStreak = 0 };

            if (_players.TryUpdate(playerId, newState, state))
            {
                OnPlayerStateChanged?.Invoke(playerId, newState);
                return;
            }
        }
    }

    public bool TryRemoveFromBenchOrBoard(int playerId, int echoInstanceId)
    {
        while (true)
        {
            if (!_players.TryGetValue(playerId, out var state)) return false;

            int benchSlot = Array.IndexOf(state.BenchEchoInstanceIds, echoInstanceId);
            int boardSlot = Array.IndexOf(state.BoardEchoInstanceIds, echoInstanceId);

            if (benchSlot == -1 && boardSlot == -1) return false;

            int[] newBench = (int[])state.BenchEchoInstanceIds.Clone();
            int[] newBoard = (int[])state.BoardEchoInstanceIds.Clone();

            if (benchSlot != -1) newBench[benchSlot] = -1;
            if (boardSlot != -1) newBoard[boardSlot] = -1;

            var newState = state with { BenchEchoInstanceIds = newBench, BoardEchoInstanceIds = newBoard };
            if (_players.TryUpdate(playerId, newState, state))
            {
                OnPlayerStateChanged?.Invoke(playerId, newState);
                return true;
            }
        }
    }

    public void GrantEndOfRoundGold(int playerId)
    {
        while (true)
        {
            if (!_players.TryGetValue(playerId, out var state)) return;

            int interest = Math.Min(_settings.MaxInterest, state.Gold / 10);
            int streak = Math.Max(state.WinStreak, state.LossStreak);
            int streakBonus = streak >= 6 ? 3 : streak >= 4 ? 2 : streak >= 2 ? 1 : 0;
            int earned = _settings.BaseGoldPerRound + interest + streakBonus;

            int newGold = Math.Min(state.Gold + earned, _settings.MaxGold);
            var newState = state with { Gold = newGold };
            if (_players.TryUpdate(playerId, newState, state))
            {
                OnPlayerStateChanged?.Invoke(playerId, newState);
                return;
            }
        }
    }

    public void GrantAutoXp(int playerId)
    {
        if (!_players.TryGetValue(playerId, out var state)) return;
        if (_settings.AutoXpPerRound.TryGetValue(state.Level.ToString(), out int amount) && amount > 0)
            AddXP(playerId, amount);
    }

    public void HandleBuyXP(int playerId)
    {
        if (!TryDeductGold(playerId, _settings.XpBuyCost)) return;
        AddXP(playerId, _settings.XpBuyAmount);
    }

    public bool TryMoveToBoard(int playerId, int echoInstanceId, int boardIndex)
    {
        while (true)
        {
            if (!_players.TryGetValue(playerId, out var state)) return false;

            if (boardIndex < 0 || boardIndex >= _settings.BoardSlots) return false;

            // Count units currently on board
            int unitsOnBoard = state.BoardEchoInstanceIds.Count(id => id != -1);
            if (unitsOnBoard >= state.Level) return false; // Exceeded level limit

            // Find unit in bench
            int benchSlot = Array.IndexOf(state.BenchEchoInstanceIds, echoInstanceId);
            if (benchSlot == -1) return false; // Not found on bench

            // Check if board spot is empty
            if (state.BoardEchoInstanceIds[boardIndex] != -1) return false;

            int[] newBench = (int[])state.BenchEchoInstanceIds.Clone();
            int[] newBoard = (int[])state.BoardEchoInstanceIds.Clone();

            newBench[benchSlot] = -1;
            newBoard[boardIndex] = echoInstanceId;

            var newState = state with { BenchEchoInstanceIds = newBench, BoardEchoInstanceIds = newBoard };
            if (_players.TryUpdate(playerId, newState, state))
            {
                OnPlayerStateChanged?.Invoke(playerId, newState);
                return true;
            }
        }
    }
}
