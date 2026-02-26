using System.Collections.Generic;
using Shared.Network.Messages;
using Server.GameLogic.StatusEffects;

namespace Server.GameLogic.Abilities;

public class IceshotHandler : IAbilityHandler
{
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, List<CombatEventRecord> events)
    {
        var target = allUnits
            .Where(u => u.IsAlive && u.Team != caster.Team)
            .OrderBy(u => GridUtils.ChebyshevDistance(caster, u))
            .FirstOrDefault();

        if (target != null)
        {
            int baseDamage = caster.GetEffectiveStats().Attack;
            int damage = baseDamage * 150 / 100;
            target.Hp -= damage;

            events.Add(new CombatEventRecord
            {
                Type = "ability",
                Attacker = caster.InstanceId,
                Target = target.InstanceId,
                Damage = damage,
                AbilityId = 5
            });

            target.AddEffect(new FreezeEffect(90)); // 1.5s freeze
        }
    }
}
