namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Increases attack speed for a duration.
/// </summary>
public class AttackSpeedBuffEffect : BaseBuffEffect
{
    public override string Id => "AttackSpeedBuff";
    private readonly int _pct;

    public AttackSpeedBuffEffect(int durationTicks, int pct) : base(durationTicks)
    {
        _pct = pct;
    }

    public override void ModifyStats(ref CombatUnitStats stats)
    {
        // AttackCooldown is in ticks (e.g., 60 = 1s).
        // +30% speed means 1.3x frequency, so cooldown / 1.3.
        stats = stats with { AttackCooldown = stats.AttackCooldown * 100 / (100 + _pct) };
    }
}
