namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Stops all unit actions.
/// </summary>
public class FreezeEffect : BaseDebuffEffect
{
    public override string Id => "Freeze";
    public override bool PreventsActions => true;

    public FreezeEffect(int durationTicks) : base(durationTicks) { }
}
