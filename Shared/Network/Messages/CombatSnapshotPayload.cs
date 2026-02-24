using System.Collections.Generic;

namespace Shared.Network.Messages;

/// <summary>
/// Payload serialized into <see cref="CombatUpdateMessage.EventJson"/>.
/// Represents the state of the board at a given simulation tick.
/// </summary>
public class CombatSnapshotPayload
{
    public int Tick { get; set; }
    public List<CombatUnitState> Units { get; set; } = new();
    public List<CombatEventRecord> Events { get; set; } = new();
}

public class CombatUnitState
{
    public int Id  { get; set; }
    public int Hp  { get; set; }
    public int Col { get; set; }
    public int Row { get; set; }
    public bool Alive { get; set; }
}

public class CombatEventRecord
{
    /// <summary>"attack" or "death"</summary>
    public string Type     { get; set; } = string.Empty;
    public int    Attacker { get; set; }
    public int    Target   { get; set; }
    public int    Damage   { get; set; }
}
