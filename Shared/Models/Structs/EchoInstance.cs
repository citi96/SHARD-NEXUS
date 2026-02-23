using Shared.Models.Enums;

namespace Shared.Models.Structs;

/// <summary>
/// Runtime instance of an Echo in the game.
/// Uses DefinitionId to refer back to the static template.
/// </summary>
public readonly record struct EchoInstance(
    int InstanceId,
    int DefinitionId,
    StarLevel Stars,
    int CurrentHealth,
    int CurrentMana,
    byte BoardIndex // Position on the board/bench, 255 if not placed
);
