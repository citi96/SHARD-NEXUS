namespace Server.GameLogic;

/// <summary>
/// Shared numeric constants for combat simulation.
/// Use <see cref="Ticks"/> to convert seconds to tick counts rather than scattering
/// magic numbers across ability and status-effect handlers.
/// </summary>
internal static class CombatConstants
{
    /// <summary>Simulation ticks per wall-clock second (matches <see cref="Configuration.CombatSettings.MaxCombatTicks"/> ÷ 60s).</summary>
    public const int TicksPerSecond = 60;

    /// <summary>Converts a duration in seconds to the equivalent number of simulation ticks.</summary>
    public static int Ticks(float seconds) => (int)(seconds * TicksPerSecond);

    /// <summary>Columns per player board (ally side). Matches Client GridRenderer.AllyCols.</summary>
    public const int PlayerBoardCols = 4;

    /// <summary>Rows per player board. Matches Client GridRenderer.Rows.</summary>
    public const int PlayerBoardRows = 4;

    /// <summary>Total columns on the combined combat board (both sides).</summary>
    public const int CombatWidth = PlayerBoardCols * 2; // 8

    /// <summary>Total rows on the combat board.</summary>
    public const int CombatHeight = PlayerBoardRows; // 4
}
