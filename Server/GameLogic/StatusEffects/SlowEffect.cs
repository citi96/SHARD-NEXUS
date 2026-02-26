namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Reduces movement speed by a percentage.
/// </summary>
public class SlowEffect : BaseStatusEffect
{
    public override string Id => "Slow";
    private readonly int _pct;

    public SlowEffect(int durationTicks, int pct) : base(durationTicks)
    {
        _pct = pct;
    }

    public override void ModifyStats(ref CombatUnitStats stats)
    {
        stats = stats with { MoveSpeed = stats.MoveSpeed * (100 - _pct) / 100 };
    }
}
