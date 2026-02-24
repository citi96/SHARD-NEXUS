using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Models.Structs;

namespace Server.GameLogic;

// ──────────────────────────────────────────────────────────────────────────────
// Supporting data types
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Represents a single combat pairing for a round.</summary>
public sealed class CombatPair
{
    public int P0Id { get; init; }

    /// <summary>May be <see cref="MatchmakingManager.GhostPlayerId"/> for ghost matches.</summary>
    public int P1Id { get; init; }

    /// <summary>Non-null when <see cref="P1Id"/> equals <see cref="MatchmakingManager.GhostPlayerId"/>.</summary>
    public PlayerState? GhostState { get; init; }
}

/// <summary>Output of <see cref="MatchmakingManager.ComputePairings"/>.</summary>
public sealed class MatchmakingResult
{
    public IReadOnlyList<CombatPair> Pairs    { get; init; } = Array.Empty<CombatPair>();
    public FeaturedMatchInfo?        Featured { get; init; }
}

/// <summary>The match chosen for broadcast to all clients / observers.</summary>
public sealed class FeaturedMatchInfo
{
    public int    Player1Id { get; init; }
    public int    Player2Id { get; init; }
    /// <summary>"AtRisk" or "HighHP"</summary>
    public string Reason    { get; init; } = string.Empty;
}

// ──────────────────────────────────────────────────────────────────────────────
// MatchmakingManager
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Computes per-round player pairings according to the VS-009 rules:
/// <list type="bullet">
///   <item>No consecutive rematches (soft constraint — relaxed when unavoidable).</item>
///   <item>Prefer opponents with HP within <see cref="HpProximityThreshold"/>.</item>
///   <item>Odd player count → leftover player fights a <em>Ghost</em>.</item>
/// </list>
/// Also identifies the <em>Featured Match</em> for the observer/broadcast system.
/// </summary>
public sealed class MatchmakingManager
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Sentinel PlayerId used for ghost opponents.</summary>
    public const int GhostPlayerId = -99;

    /// <summary>HP below this value marks a player as "at risk" for Featured Match.</summary>
    public const int AtRiskHpThreshold = 15;

    /// <summary>Preferred HP delta between paired players.</summary>
    public const int HpProximityThreshold = 10;

    // ── Internal state ─────────────────────────────────────────────────────────

    /// <summary>playerId → opponent faced last round (-1 if none).</summary>
    private readonly Dictionary<int, int> _lastOpponent = new();

    /// <summary>playerId → snapshot of the team that last beat them (used as ghost).</summary>
    private readonly Dictionary<int, PlayerState> _ghostBank = new();

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds pairings for the upcoming round and selects the Featured Match.
    /// The returned list may contain ghost pairs if player count is odd.
    /// </summary>
    public MatchmakingResult ComputePairings(
        IReadOnlyList<PlayerState> players,
        int round,
        Random rng)
    {
        if (players.Count == 0)
            return new MatchmakingResult();

        if (players.Count == 1)
        {
            // Only one real player — they must fight a ghost
            var solo = players[0];
            return new MatchmakingResult
            {
                Pairs = new[] { BuildGhostPair(solo) },
            };
        }

        // Sort by HP descending; tie-break by PlayerId ascending for determinism
        var sorted = players
            .OrderByDescending(p => p.NexusHealth)
            .ThenBy(p => p.PlayerId)
            .ToList();

        var paired   = new HashSet<int>();
        var pairs    = new List<CombatPair>();

        foreach (var player in sorted)
        {
            if (paired.Contains(player.PlayerId)) continue;

            // Candidates: unpaired, not self, preferably not last opponent
            var allUnpaired = sorted
                .Where(c => c.PlayerId != player.PlayerId && !paired.Contains(c.PlayerId))
                .ToList();

            if (allUnpaired.Count == 0)
            {
                // Odd player out after all possible pairs formed → ghost
                pairs.Add(BuildGhostPair(player));
                paired.Add(player.PlayerId);
                continue;
            }

            // Prefer candidates that weren't the last opponent
            int lastOpp = _lastOpponent.TryGetValue(player.PlayerId, out int lo) ? lo : -1;
            var preferred = allUnpaired.Where(c => c.PlayerId != lastOpp).ToList();
            var pool = preferred.Count > 0 ? preferred : allUnpaired; // relax if necessary

            // Within pool, pick closest HP; tie-break by PlayerId
            var best = pool
                .OrderBy(c => Math.Abs(c.NexusHealth - player.NexusHealth))
                .ThenBy(c => c.PlayerId)
                .First();

            pairs.Add(new CombatPair { P0Id = player.PlayerId, P1Id = best.PlayerId });
            paired.Add(player.PlayerId);
            paired.Add(best.PlayerId);
        }

        var featured = SelectFeaturedMatch(pairs, players);
        return new MatchmakingResult { Pairs = pairs, Featured = featured };
    }

    /// <summary>
    /// Records the outcome of a combat round so future pairings can avoid
    /// rematches and the ghost bank stays current.
    /// </summary>
    /// <param name="winnerId">Real player who won (not a ghost).</param>
    /// <param name="loserId">Real player who lost.</param>
    /// <param name="winnerBoardSnapshot">Full state of the winner — their board becomes the loser's future ghost.</param>
    public void RecordRoundResult(int winnerId, int loserId, PlayerState winnerBoardSnapshot)
    {
        _lastOpponent[winnerId] = loserId;
        _lastOpponent[loserId]  = winnerId;
        _ghostBank[loserId]     = winnerBoardSnapshot;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private CombatPair BuildGhostPair(PlayerState player)
    {
        PlayerState ghost;
        if (_ghostBank.TryGetValue(player.PlayerId, out var banked))
        {
            // Ghost = team that last beat this player
            ghost = banked with { PlayerId = GhostPlayerId };
        }
        else
        {
            // No previous loss → ghost mirrors the player's own board
            ghost = player with { PlayerId = GhostPlayerId };
        }

        return new CombatPair
        {
            P0Id       = player.PlayerId,
            P1Id       = GhostPlayerId,
            GhostState = ghost,
        };
    }

    private static FeaturedMatchInfo? SelectFeaturedMatch(
        IReadOnlyList<CombatPair> pairs,
        IReadOnlyList<PlayerState> players)
    {
        if (pairs.Count == 0) return null;

        // Build HP lookup
        var hpLookup = players.ToDictionary(p => p.PlayerId, p => p.NexusHealth);

        int GetHp(int id) => hpLookup.TryGetValue(id, out int hp) ? hp : 0;

        // Priority 1: pair containing an at-risk player (<15 HP)
        foreach (var pair in pairs)
        {
            bool p0AtRisk = GetHp(pair.P0Id) < AtRiskHpThreshold;
            bool p1AtRisk = pair.P1Id != GhostPlayerId && GetHp(pair.P1Id) < AtRiskHpThreshold;
            if (p0AtRisk || p1AtRisk)
                return new FeaturedMatchInfo
                {
                    Player1Id = pair.P0Id,
                    Player2Id = pair.P1Id == GhostPlayerId ? pair.P0Id : pair.P1Id,
                    Reason    = "AtRisk",
                };
        }

        // Priority 2: pair with highest combined HP (excluding ghost pairs)
        var realPairs = pairs.Where(p => p.P1Id != GhostPlayerId).ToList();
        if (realPairs.Count == 0) return null;

        var top = realPairs
            .OrderByDescending(p => GetHp(p.P0Id) + GetHp(p.P1Id))
            .First();

        return new FeaturedMatchInfo
        {
            Player1Id = top.P0Id,
            Player2Id = top.P1Id,
            Reason    = "HighHP",
        };
    }
}
