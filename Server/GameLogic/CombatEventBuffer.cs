using Shared.Network.Messages;
using System.Collections.Generic;

namespace Server.GameLogic;

/// <summary>
/// Simple implementation that collects events in a list.
/// </summary>
public class CombatEventBuffer : ICombatEventDispatcher
{
    private readonly List<CombatEventRecord> _events = new();

    public void Dispatch(CombatEventRecord record)
    {
        _events.Add(record);
    }

    public List<CombatEventRecord> GetCapturedEvents() => new(_events);

    public void Clear() => _events.Clear();
}
