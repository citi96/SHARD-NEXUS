using System.Collections.Generic;
using Shared.Network.Messages;
using Server.GameLogic.StatusEffects;

namespace Server.GameLogic.Abilities;

public class IceshotHandler : IAbilityHandler
{
    public void Execute(CombatUnit caster, List<CombatUnit> allUnits, ICombatEventDispatcher dispatcher)
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

            dispatcher.Dispatch(new CombatEventRecord
            {
                Type = "ability",
                Attacker = caster.InstanceId,
                Target = target.InstanceId,
                Damage = damage
            });

            target.AddEffect(new FreezeEffect(90)); // 1.5s freeze

            if (target.Hp <= 0 && target.IsAlive)
            {
                target.IsAlive = false;
                dispatcher.Dispatch(new CombatEventRecord { Type = "death", Target = target.InstanceId });
            }
        }
    }
}
