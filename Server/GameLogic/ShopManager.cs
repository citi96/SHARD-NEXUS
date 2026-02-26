using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Server.Configuration;
using Shared.Models.Enums;
using Shared.Models.Structs;
using Shared.Network.Messages;

namespace Server.GameLogic;

public class PityCounters
{
    public int NoRarePlusCount { get; set; } = 0;
    public int NoEpicPlusCount { get; set; } = 0;
    public int NoLegendaryCount { get; set; } = 0;

    public void IncrementAll()
    {
        NoRarePlusCount++;
        NoEpicPlusCount++;
        NoLegendaryCount++;
    }

    public void ResetRare() { NoRarePlusCount = 0; }
    public void ResetEpic() { NoEpicPlusCount = 0; NoRarePlusCount = 0; }
    public void ResetLegendary() { NoLegendaryCount = 0; NoEpicPlusCount = 0; NoRarePlusCount = 0; }
}

public class ShopManager
{
    private readonly ShopSettings _settings;
    private readonly EchoPoolManager _poolManager;
    private readonly PlayerManager _playerManager;

    // The reference data needed to filter by rarity and costs
    private readonly Dictionary<int, EchoDefinition> _catalog;
    private readonly Random _rand = new Random();

    // Key: PlayerId, Value: Array of 5 EchoDefinition Ids (-1 means empty or bought)
    private readonly ConcurrentDictionary<int, int[]> _playerShops = new();
    private readonly ConcurrentDictionary<int, PityCounters> _playerPity = new();

    public const int ShopSize = 5;

    public ShopManager(
        ShopSettings settings, 
        EchoPoolManager poolManager, 
        PlayerManager playerManager, 
        IEnumerable<EchoDefinition> catalog)
    {
        _settings = settings;
        _poolManager = poolManager;
        _playerManager = playerManager;
        _catalog = catalog.ToDictionary(k => k.Id, v => v);
    }

    public bool HandleRefresh(int playerId)
    {
        if (!_playerManager.TryDeductGold(playerId, _settings.RefreshCost))
            return false;

        // Return old shop echoes to pool
        if (_playerShops.TryGetValue(playerId, out int[]? oldShop) && oldShop != null)
        {
            foreach (var echoId in oldShop)
            {
                if (echoId != -1)
                {
                    _poolManager.ReturnToPool(echoId);
                }
            }
        }

        // Increment Pity Counters
        var pity = _playerPity.GetOrAdd(playerId, _ => new PityCounters());
        pity.IncrementAll();

        GenerateShop(playerId);
        return true;
    }

    public void GenerateShop(int playerId)
    {
        var player = _playerManager.GetPlayerState(playerId);
        int level = player?.Level ?? 1;
        var pity = _playerPity.GetOrAdd(playerId, _ => new PityCounters());

        ShopProbabilities probs = GetProbabilitiesForLevel(level);

        int[] newShop = new int[ShopSize];
        for (int i = 0; i < ShopSize; i++)
        {
            newShop[i] = -1; // init empty
            
            // 1. Check Pity guarantees first
            Rarity targetRarity = Rarity.Common;
            bool forceRarity = false;

            if (pity.NoLegendaryCount >= _settings.PityThresholdLegendary)
            {
                targetRarity = Rarity.Legendary; forceRarity = true;
                pity.ResetLegendary();
            }
            else if (pity.NoEpicPlusCount >= _settings.PityThresholdEpic)
            {
                targetRarity = Rarity.Epic; forceRarity = true;
                pity.ResetEpic();
            }
            else if (pity.NoRarePlusCount >= _settings.PityThresholdRare)
            {
                targetRarity = Rarity.Rare; forceRarity = true;
                pity.ResetRare();
            }

            // 2. If no pity triggered, roll naturally based on level probabilities
            if (!forceRarity)
            {
                targetRarity = RollRarity(probs);
            }

            // 3. Try to pick from pool, fallback if pool exhausted for that rarity
            int echoId = TryDrawFromPool(targetRarity);

            // Fallbacks: if pool is exhausted for the rolled rarity, cascade down until we find *something*
            if (echoId == -1)
            {
                // Simple cascade logic down
                for (int r = (int)targetRarity - 1; r >= 1; r--)
                {
                    echoId = TryDrawFromPool((Rarity)r);
                    if (echoId != -1) break;
                }
            }

            // If we STILL don't have an echo, the pools for this tier and below are entirely empty (extreme edge case)
            newShop[i] = echoId; 

            // Track Natural buys vs Pity guarantees for resetting counters appropriately
            if (echoId != -1)
            {
                var drawnEcho = _catalog[echoId];
                if (!forceRarity) // Natural rolls might reset pity spontaneously
                {
                    if (drawnEcho.Rarity == Rarity.Legendary) pity.ResetLegendary();
                    else if (drawnEcho.Rarity == Rarity.Epic) pity.ResetEpic();
                    else if (drawnEcho.Rarity == Rarity.Rare) pity.ResetRare();
                }
            }
        }

        _playerShops[playerId] = newShop;

        // Note: Broadcast ShopRefreshedMessage back to the client via GameManager event hook
        var message = NetworkMessage.Create(MessageType.ShopRefreshed, new ShopRefreshedMessage
        {
            EchoDefinitionIds = new List<int>(newShop)
        });
        
        // This class doesn't have NetworkManager, it should expose an Event or GameServer uses it.
        OnShopUpdated?.Invoke(playerId, message);
    }

    public event Action<int, NetworkMessage>? OnShopUpdated;

    public bool HandleBuy(int playerId, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= ShopSize) return false;

        if (!_playerShops.TryGetValue(playerId, out int[]? currentShop) || currentShop == null)
            return false;

        int echoId = currentShop[slotIndex];
        if (echoId == -1) return false;

        if (!_catalog.TryGetValue(echoId, out var def))
            return false;

        int cost = GetCostForRarity(def.Rarity);

        if (!_playerManager.HasEnoughGold(playerId, cost))
            return false;

        int newInstanceId = echoId * 1000 + _rand.Next(0, 999);

        if (!_playerManager.TryAddToBench(playerId, newInstanceId))
            return false;

        if (!_playerManager.TryDeductGold(playerId, cost))
            return false;

        currentShop[slotIndex] = -1;

        Console.WriteLine($"[Shop] Player {playerId} bought Echo {def.Name} (Rarity: {def.Rarity}, Cost: {cost})");

        var message = NetworkMessage.Create(MessageType.ShopRefreshed, new ShopRefreshedMessage
        {
            EchoDefinitionIds = new List<int>(currentShop)
        });
        OnShopUpdated?.Invoke(playerId, message);
        return true;
    }

    private ShopProbabilities GetProbabilitiesForLevel(int level)
    {
        string levelKey = level switch
        {
            1 => "1",
            2 or 3 => "2-3",
            >= 4 and <= 6 => "4-6",
            >= 7 and <= 9 => "7-9",
            _ => "10"
        };

        if (_settings.ProbabilitiesByLevel.TryGetValue(levelKey, out var probs))
            return probs;

        // Default fallback
        return new ShopProbabilities { Common = 100 };
    }

    private Rarity RollRarity(ShopProbabilities probs)
    {
        int total = probs.Common + probs.Uncommon + probs.Rare + probs.Epic + probs.Legendary;
        if (total == 0) return Rarity.Common;

        int roll = _rand.Next(0, total);
        
        if (roll < probs.Common) return Rarity.Common;
        roll -= probs.Common;
        if (roll < probs.Uncommon) return Rarity.Uncommon;
        roll -= probs.Uncommon;
        if (roll < probs.Rare) return Rarity.Rare;
        roll -= probs.Rare;
        if (roll < probs.Epic) return Rarity.Epic;
        return Rarity.Legendary;
    }

    private int TryDrawFromPool(Rarity targetRarity)
    {
        // Find all echoes matching the target rarity
        var candidates = _catalog.Values.Where(e => e.Rarity == targetRarity).ToList();
        if (candidates.Count == 0) return -1;

        // Shuffle candidates so we don't always pick the first one
        candidates = candidates.OrderBy(x => _rand.Next()).ToList();

        foreach (var candidate in candidates)
        {
            if (_poolManager.TakeFromPool(candidate.Id))
            {
                return candidate.Id;
            }
        }
        return -1; // All echoes of this rarity are exhausted
    }

    public void HandleSell(int playerId, int echoInstanceId)
    {
        int definitionId = echoInstanceId / 1000;
        if (!_catalog.TryGetValue(definitionId, out var def)) return;

        if (!_playerManager.TryRemoveFromBenchOrBoard(playerId, echoInstanceId)) return;

        int refund = GetCostForRarity(def.Rarity);
        _playerManager.AddGold(playerId, refund);
        _poolManager.ReturnToPool(definitionId);

        Console.WriteLine($"[Shop] Player {playerId} sold Echo {def.Name} for {refund} gold.");
    }

    private int GetCostForRarity(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Common => 1,
            Rarity.Uncommon => 2,
            Rarity.Rare => 3,
            Rarity.Epic => 4,
            Rarity.Legendary => 5,
            _ => 1
        };
    }
}
