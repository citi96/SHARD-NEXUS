using System.Collections.Generic;
using Shared.Models.Structs;
using Shared.Network.Messages;

namespace Server.GameLogic;

/// <summary>Output from <see cref="CombatSimulator.RunBatch"/>.</summary>
public sealed class CombatSimulationResult
{
    public CombatResult Result { get; }
    public IReadOnlyList<CombatSnapshotPayload> Snapshots { get; }

    public CombatSimulationResult(CombatResult result, List<CombatSnapshotPayload> snapshots)
    {
        Result = result;
        Snapshots = snapshots;
    }
}
