namespace Server.GameLogic;

/// <summary>
/// Defines a processor in the damage calculation pipeline.
/// </summary>
public interface IDamageProcessor
{
    /// <summary>
    /// Processes and modifies the damage context.
    /// </summary>
    void Process(DamageContext context);
}
