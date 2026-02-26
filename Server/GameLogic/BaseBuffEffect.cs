namespace Server.GameLogic;

/// <summary>
/// Base class for beneficial status effects.
/// </summary>
public abstract class BaseBuffEffect : BaseStatusEffect
{
    public override bool IsDebuff => false;

    protected BaseBuffEffect(int durationTicks) : base(durationTicks) { }
}
