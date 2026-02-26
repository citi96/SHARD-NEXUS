namespace Server.GameLogic.StatusEffects;

/// <summary>
/// Prevents the unit from receiving any new status effects where IsDebuff is true.
/// </summary>
public class ImmunityEffect : BaseBuffEffect
{
    public override string Id => "Immunity";

    public ImmunityEffect(int durationTicks) : base(durationTicks) { }

    // Logic is handled in CombatUnit.AddEffect
}
