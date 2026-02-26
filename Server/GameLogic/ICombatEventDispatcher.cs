using System.Collections.Generic;
using Shared.Network.Messages;

namespace Server.GameLogic;

/// <summary>
/// Handles dispatching and recording combat events.
/// </summary>
public interface ICombatEventDispatcher
{
    void Dispatch(CombatEventRecord record);
    List<CombatEventRecord> GetCapturedEvents();
    void Clear();
}

