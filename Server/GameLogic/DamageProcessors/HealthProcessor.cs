using Shared.Network.Messages;

namespace Server.GameLogic;

/// <summary>
/// Final processor that reduces health and handles death.
/// </summary>
public class HealthProcessor : IDamageProcessor
{
    public void Process(DamageContext context)
    {
        if (context.CalculatedDamage <= 0) return;

        context.Target.Hp -= context.CalculatedDamage;

        if (context.Target.Hp <= 0 && context.Target.IsAlive)
        {
            context.Target.IsAlive = false;
            context.Events.Add(new CombatEventRecord 
            { 
                Type = "death", 
                Target = context.Target.InstanceId 
            });
        }
    }
}
