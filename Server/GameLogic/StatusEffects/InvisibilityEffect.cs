namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Makes the unit invisible to enemies.
/// </summary>
public class InvisibilityEffect : BaseBuffEffect
{
    public override string Id => "Invisibility";
    public override bool IsStealthed => true;

    public InvisibilityEffect(int durationTicks) : base(durationTicks) { }
}
