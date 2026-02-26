using Shared.Models.Enums;

namespace Server.GameLogic;

/// <summary>An intervention queued by a player to be applied before the next simulation batch.</summary>
public record PendingIntervention(int PlayerId, int Team, InterventionType Type, int TargetId);
