namespace Shared.Models.Enums;

/// <summary>
/// The specific intervention card type played by a player during combat.
/// </summary>
public enum InterventionType : byte
{
    Reposition,
    Focus,
    Barrier,
    Accelerate,
    TacticalRetreat
}
