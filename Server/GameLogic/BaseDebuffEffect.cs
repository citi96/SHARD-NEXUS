namespace Server.GameLogic;

/// <summary>
/// Base class for harmful status effects.
/// </summary>
public abstract class BaseDebuffEffect : BaseStatusEffect
{
    public override bool IsDebuff => true;

    protected BaseDebuffEffect(int durationTicks) : base(durationTicks) { }
}
