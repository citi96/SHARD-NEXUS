using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Server.Configuration;
using Shared.Models.Enums;
using Shared.Models.Structs;

namespace Server.GameLogic;

/// <summary>
/// Manages the shared pool of Echo instances. 
/// Tracks remaining copies based on their rarity limits defined in EchoPoolSettings.
/// </summary>
public class EchoPoolManager
{
    private readonly EchoPoolSettings _settings;
    
    // Key: Echo Definition Id (e.g., Pyroth's ID), Value: Current Remaining Copies
    private readonly ConcurrentDictionary<int, int> _pool;
    
    private readonly Dictionary<int, EchoDefinition> _catalog;

    public EchoPoolManager(EchoPoolSettings settings, IEnumerable<EchoDefinition> catalog)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _pool = new ConcurrentDictionary<int, int>();
        _catalog = new Dictionary<int, EchoDefinition>();

        InitializePool(catalog);
    }

    private void InitializePool(IEnumerable<EchoDefinition> catalog)
    {
        foreach (var def in catalog)
        {
            _catalog[def.Id] = def;
            _pool[def.Id] = GetInitialCountForRarity(def.Rarity);
        }
    }

    private int GetInitialCountForRarity(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Common => _settings.Common,
            Rarity.Uncommon => _settings.Uncommon,
            Rarity.Rare => _settings.Rare,
            Rarity.Epic => _settings.Epic,
            Rarity.Legendary => _settings.Legendary,
            _ => 0
        };
    }

    /// <summary>
    /// Attempts to take one copy of the specified Echo from the pool.
    /// Reserving it for a shop or player purchase.
    /// </summary>
    /// <returns>True if a copy was successfully taken, false if the pool is empty.</returns>
    public bool TakeFromPool(int echoId)
    {
        if (!_catalog.ContainsKey(echoId))
            return false;

        while (true)
        {
            if (!_pool.TryGetValue(echoId, out int currentCount))
                return false;

            if (currentCount <= 0)
                return false; // Esaurito

            // Tentativo atomico di decrementare
            if (_pool.TryUpdate(echoId, currentCount - 1, currentCount))
            {
                Console.WriteLine($"[EchoPool] Taken 1x ID {echoId}. Remaining: {currentCount - 1}");
                return true;
            }
        }
    }

    /// <summary>
    /// Returns one copy of the specified Echo back to the pool 
    /// (e.g., when a player sells it or refreshes their shop without buying).
    /// </summary>
    public void ReturnToPool(int echoId)
    {
        if (!_catalog.TryGetValue(echoId, out var def))
            return;

        int maxCopies = GetInitialCountForRarity(def.Rarity);

        while (true)
        {
            if (!_pool.TryGetValue(echoId, out int currentCount))
                return;

            if (currentCount >= maxCopies)
            {
                // Safety net: non possiamo avere più copie del limite massimo
                Console.WriteLine($"[EchoPool] Warning: attempted to return ID {echoId} but pool is already full ({maxCopies}).");
                return;
            }

            if (_pool.TryUpdate(echoId, currentCount + 1, currentCount))
            {
                Console.WriteLine($"[EchoPool] Returned 1x ID {echoId}. Total: {currentCount + 1}/{maxCopies}");
                return;
            }
        }
    }

    /// <summary>
    /// Checks how many copies of a specific Echo are currently available.
    /// </summary>
    public int GetAvailable(int echoId)
    {
        return _pool.TryGetValue(echoId, out int count) ? count : 0;
    }
}
