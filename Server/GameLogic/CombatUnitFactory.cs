using System.Collections.Generic;
using System.Linq;
using Server.Configuration;
using Shared.Models.Structs;
using Shared.Models.Enums;

namespace Server.GameLogic;

/// <summary>
/// Factory for creating initialized CombatUnits.
/// </summary>
public class CombatUnitFactory
{
    private readonly Dictionary<int, EchoDefinition> _catalog;
    private readonly ResonanceSettings _resSettings;

    public CombatUnitFactory(IEnumerable<EchoDefinition> catalog, ResonanceSettings resSettings)
    {
        _catalog = catalog.ToDictionary(d => d.Id);
        _resSettings = resSettings;
    }

    public CombatUnit Create(int instanceId, int team, int combatCol, int boardRow, ResonanceBonus[] playerResonances)
    {
        int definitionId = instanceId / 1000;
        if (!_catalog.TryGetValue(definitionId, out var def))
            throw new KeyNotFoundException($"Echo definition {definitionId} not found.");

        var bonuses = ComputeStatBonuses(playerResonances);

        int multiplier = 100;
        int hp = (def.BaseHealth * multiplier / 100) + (def.BaseHealth * bonuses.HpPct / 100);
        int attack = (def.BaseAttack * multiplier / 100) + (def.BaseAttack * bonuses.AtkPct / 100);
        int defense = (def.BaseDefense * multiplier / 100) + (def.BaseDefense * bonuses.DefPct / 100);
        int cooldown = (int)(60f / def.BaseAttackSpeed);
        if (bonuses.AsPct > 0)
            cooldown = cooldown * 100 / (100 + bonuses.AsPct);

        return new CombatUnit
        {
            InstanceId = instanceId,
            DefinitionId = definitionId,
            Team = team,
            Col = combatCol,
            Row = boardRow,
            Hp = hp,
            BaseMaxHp = hp,
            Mana = 0,
            BaseMaxMana = def.BaseMana,
            BaseAttack = attack,
            BaseDefense = defense,
            BaseMr = def.BaseMR,
            BaseAttackRange = def.BaseAttackRange,
            BaseCritChance = def.BaseCritChance,
            BaseCritMultiplier = def.BaseCritMultiplier,
            BaseAttackCooldown = cooldown,
            AttackCooldownRemaining = 0,
            IsAlive = true,
            Shield = bonuses.ShieldFlat,
            AbilityIds = def.AbilityIds,
            TargetingStrategy = def.Class == EchoClass.Assassin 
                ? new FarthestEnemyStrategy() 
                : new NearestEnemyStrategy()
        };
    }

    private (int AtkPct, int DefPct, int HpPct, int AsPct, int ShieldFlat) ComputeStatBonuses(ResonanceBonus[] resonances)
    {
        int atk = 0, def = 0, hp = 0, aspd = 0, shield = 0;
        if (resonances == null) return (atk, def, hp, aspd, shield);

        foreach (var r in resonances)
        {
            for (int tier = 1; tier <= r.Tier; tier++)
            {
                string key = $"{r.ResonanceType}_{tier}";
                if (!_resSettings.Bonuses.TryGetValue(key, out var bonusDict)) continue;
                atk += bonusDict.GetValueOrDefault("AtkPct");
                def += bonusDict.GetValueOrDefault("DefPct");
                hp += bonusDict.GetValueOrDefault("HpPct");
                aspd += bonusDict.GetValueOrDefault("AsPct");
                shield += bonusDict.GetValueOrDefault("ShieldFlat");
            }
        }
        return (atk, def, hp, aspd, shield);
    }
}
