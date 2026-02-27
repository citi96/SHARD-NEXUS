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
}
