using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class CombatResult
{
    public bool AttackerWon { get; set; }
    public int AttackerLosses { get; set; }
    public int DefenderLosses { get; set; }
}

public class CombatResolver
{
    public CombatResult Resolve(
        WorldState world,
        CityData attacker,
        CityData defender,
        int attackingTroops,
        List<int>? attackingOfficerIds = null,
        List<AttackOfficerDeploymentData>? attackOfficerDeployments = null)
    {
        var clampedAttackTroops = attackingTroops < 0 ? 0 : attackingTroops;
        var attackerStrength = GetAverageOfficerStat(world, attacker, officer => officer.Strength, item => item.StrengthBonus, attackingOfficerIds);
        var attackerCombat = GetAverageOfficerStat(world, attacker, officer => officer.Combat, item => item.CombatBonus, attackingOfficerIds);
        var defenderCombat = GetAverageOfficerStat(world, defender, officer => officer.Combat, item => item.CombatBonus);

        var attackStat = attackerStrength * 0.6f + attackerCombat * 0.4f;
        var attackMultiplier = 1.0f + attackStat / 200.0f + GetAttackDeploymentModifier(attackOfficerDeployments);
        var defenseMultiplier = 1.0f + (defender.Defense * 0.006f) + (defenderCombat / 500.0f) + GetDefenderTroopModifier(defender);

        var effectiveAttack = clampedAttackTroops * attackMultiplier;
        var effectiveDefense = defender.Troops * defenseMultiplier;

        var attackerWon = effectiveAttack >= effectiveDefense;
        return new CombatResult
        {
            AttackerWon = attackerWon,
            AttackerLosses = attackerWon ? defender.Troops / 3 : clampedAttackTroops / 2,
            DefenderLosses = attackerWon ? defender.Troops : clampedAttackTroops / 2
        };
    }

    private static int GetAverageOfficerStat(
        WorldState world,
        CityData city,
        System.Func<OfficerData, int> selector,
        System.Func<ItemData, int> itemBonusSelector,
        List<int>? officerIdsOverride = null)
    {
        var total = 0;
        var count = 0;

        var officerIds = officerIdsOverride != null && officerIdsOverride.Count > 0
            ? officerIdsOverride
            : city.OfficerIds;

        foreach (var officerId in officerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            var itemBonus = 0;
            foreach (var item in world.Items)
            {
                if (item.EquippedOfficerId == officer.Id)
                {
                    itemBonus += itemBonusSelector(item);
                }
            }

            total += selector(officer) + itemBonus;
            count += 1;
        }

        return count == 0 ? 50 : total / count;
    }

    private static float GetAttackDeploymentModifier(List<AttackOfficerDeploymentData>? deployments)
    {
        if (deployments == null || deployments.Count == 0)
        {
            return 0.0f;
        }

        var totalTroops = deployments.Sum(item => item.TroopCount);
        if (totalTroops <= 0)
        {
            return 0.0f;
        }

        float weightedModifier = 0.0f;
        foreach (var deployment in deployments)
        {
            var troopModifier = deployment.TroopType switch
            {
                TroopType.Infantry => 0.00f,
                TroopType.Spearman => 0.03f,
                TroopType.Cavalry => 0.08f,
                TroopType.Archer => 0.02f,
                TroopType.Crossbow => 0.05f,
                TroopType.Siege => 0.10f,
                _ => 0.0f
            };
            weightedModifier += troopModifier * deployment.TroopCount / totalTroops;
        }

        return weightedModifier;
    }

    private static float GetDefenderTroopModifier(CityData defender)
    {
        var totalTroops = defender.Troops;
        if (totalTroops <= 0)
        {
            return 0.0f;
        }

        return
            (defender.SpearmanTroops * 0.03f +
             defender.ArcherTroops * 0.02f +
             defender.CrossbowTroops * 0.05f +
             defender.SiegeTroops * 0.04f) / totalTroops;
    }
}
