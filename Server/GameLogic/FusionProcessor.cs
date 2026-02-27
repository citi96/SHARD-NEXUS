using System;
using System.Collections.Generic;

namespace Server.GameLogic;

/// <summary>
/// Pure logic for star-upgrade fusion.
/// 3 identical echoes (same definitionId + same star level) → 1 echo with star+1.
/// Cascadable: 9×1★ → 3×2★ → 1×3★. Max star level is 3.
/// </summary>
public static class FusionProcessor
{
    public const byte MaxStarLevel = 3;

    public record FusionResult(
        int ResultInstanceId,
        int DefinitionId,
        byte NewStarLevel,
        bool IsOnBoard,
        int SlotIndex);

    /// <summary>
    /// Scans board + bench for groups of 3 identical echoes and fuses them.
    /// Mutates the arrays in-place and returns every fusion performed (including cascades).
    /// </summary>
    public static List<FusionResult> TryFuse(
        int[] boardIds, byte[] boardStars,
        int[] benchIds, byte[] benchStars)
    {
        var results = new List<FusionResult>();
        bool fused;

        do
        {
            fused = false;

            // Group all occupied slots by (definitionId, starLevel)
            var groups = new Dictionary<(int defId, byte stars), List<(bool isBoard, int index)>>();

            CollectSlots(boardIds, boardStars, isBoard: true, groups);
            CollectSlots(benchIds, benchStars, isBoard: false, groups);

            foreach (var (key, slots) in groups)
            {
                if (slots.Count < 3 || key.stars >= MaxStarLevel) continue;

                // Board slots first (survivor stays on board if possible), then by index
                slots.Sort((a, b) =>
                {
                    int cmp = b.isBoard.CompareTo(a.isBoard); // board (true) before bench (false)
                    return cmp != 0 ? cmp : a.index.CompareTo(b.index);
                });

                // First slot is the survivor, next 2 are consumed
                var winner = slots[0];
                var consumed1 = slots[1];
                var consumed2 = slots[2];

                byte newStars = (byte)(key.stars + 1);

                // Upgrade the winner
                SetStar(winner, newStars, boardStars, benchStars);

                // Clear the consumed slots
                ClearSlot(consumed1, boardIds, boardStars, benchIds, benchStars);
                ClearSlot(consumed2, boardIds, boardStars, benchIds, benchStars);

                int winnerId = winner.isBoard ? boardIds[winner.index] : benchIds[winner.index];
                results.Add(new FusionResult(winnerId, key.defId, newStars, winner.isBoard, winner.index));

                fused = true;
                break; // restart scan for cascading fusions
            }
        } while (fused);

        return results;
    }

    private static void CollectSlots(
        int[] ids, byte[] stars, bool isBoard,
        Dictionary<(int defId, byte stars), List<(bool isBoard, int index)>> groups)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == -1) continue;

            int defId = ids[i] / 1000;
            byte star = stars[i];
            var key = (defId, star);

            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<(bool, int)>();
                groups[key] = list;
            }

            list.Add((isBoard, i));
        }
    }

    private static void SetStar((bool isBoard, int index) slot, byte newStars, byte[] boardStars, byte[] benchStars)
    {
        if (slot.isBoard)
            boardStars[slot.index] = newStars;
        else
            benchStars[slot.index] = newStars;
    }

    private static void ClearSlot(
        (bool isBoard, int index) slot,
        int[] boardIds, byte[] boardStars,
        int[] benchIds, byte[] benchStars)
    {
        if (slot.isBoard)
        {
            boardIds[slot.index] = -1;
            boardStars[slot.index] = 0;
        }
        else
        {
            benchIds[slot.index] = -1;
            benchStars[slot.index] = 0;
        }
    }
}
