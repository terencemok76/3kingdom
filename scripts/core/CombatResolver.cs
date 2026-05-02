using System;
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
    private const float StrongCounterBonus = 0.18f;
    private const float LightCounterBonus = 0.10f;

    public CombatResult Resolve(
        WorldState world,
        CityData attacker,
        CityData defender,
        int attackingTroops,
        List<int>? attackingOfficerIds = null,
        List<AttackOfficerDeploymentData>? attackOfficerDeployments = null,
        TroopAllocationData? attackingAllocation = null)
    {
        var clampedAttackTroops = attackingTroops < 0 ? 0 : attackingTroops;
        var attackerStrength = GetAverageOfficerStat(world, attacker, officer => officer.Strength, item => item.StrengthBonus, OfficerProgressionStat.Strength, attackingOfficerIds);
        var attackerLeadership = GetAverageOfficerStat(world, attacker, officer => officer.Leadership, item => item.LeadershipBonus, OfficerProgressionStat.Leadership, attackingOfficerIds);
        var attackerCombat = GetAverageOfficerStat(world, attacker, officer => officer.Combat, item => item.CombatBonus, OfficerProgressionStat.Combat, attackingOfficerIds);
        var defenderLeadership = GetAverageOfficerStat(world, defender, officer => officer.Leadership, item => item.LeadershipBonus, OfficerProgressionStat.Leadership);
        var defenderCombat = GetAverageOfficerStat(world, defender, officer => officer.Combat, item => item.CombatBonus, OfficerProgressionStat.Combat);
        var effectiveAttackAllocation = GetAttackAllocation(attackingAllocation, attackOfficerDeployments);

        var attackStat = attackerStrength * 0.3f + attackerLeadership * 0.4f + attackerCombat * 0.3f;
        var deploymentModifier = GetAttackDeploymentModifier(effectiveAttackAllocation);
        var siegePressure = GetSiegePressureModifier(effectiveAttackAllocation);
        var troopCounterAttackModifier = GetAttackCounterModifier(effectiveAttackAllocation, defender);
        var troopCounterDefenseModifier = GetDefenseCounterModifier(effectiveAttackAllocation, defender);
        var attackMultiplier = 1.0f + attackStat / 220.0f + deploymentModifier + troopCounterAttackModifier;
        var defenseMultiplier = 1.0f + (defender.Defense * 0.006f) + (defenderLeadership / 260.0f) + (defenderCombat / 550.0f) + GetDefenderTroopModifier(defender) + troopCounterDefenseModifier - siegePressure;
        if (defenseMultiplier < 0.75f)
        {
            defenseMultiplier = 0.75f;
        }

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

    private static TroopAllocationData GetAttackAllocation(
        TroopAllocationData? attackingAllocation,
        List<AttackOfficerDeploymentData>? deployments)
    {
        if (attackingAllocation != null && attackingAllocation.Total > 0)
        {
            return attackingAllocation;
        }

        var allocation = new TroopAllocationData();
        if (deployments == null || deployments.Count == 0)
        {
            return allocation;
        }

        foreach (var deployment in deployments)
        {
            switch (deployment.TroopType)
            {
                case TroopType.Infantry:
                    allocation.Infantry += deployment.TroopCount;
                    break;
                case TroopType.Spearman:
                    allocation.Spearman += deployment.TroopCount;
                    break;
                case TroopType.Cavalry:
                    allocation.Cavalry += deployment.TroopCount;
                    break;
                case TroopType.Archer:
                    allocation.Archer += deployment.TroopCount;
                    break;
                case TroopType.Crossbow:
                    allocation.Crossbow += deployment.TroopCount;
                    break;
                case TroopType.Siege:
                    allocation.Siege += deployment.TroopCount;
                    break;
            }
        }

        return allocation;
    }

    private static int GetAverageOfficerStat(
        WorldState world,
        CityData city,
        System.Func<OfficerData, int> selector,
        System.Func<ItemData, int> itemBonusSelector,
        OfficerProgressionStat progressionStat,
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

            total += selector(officer) + itemBonus + OfficerProgressionRules.GetStatBonus(officer, progressionStat);
            count += 1;
        }

        return count == 0 ? 50 : total / count;
    }

    private static float GetAttackDeploymentModifier(TroopAllocationData allocation)
    {
        var totalTroops = allocation.Total;
        if (totalTroops <= 0)
        {
            return 0.0f;
        }

        float weightedModifier = 0.0f;
        var troopTypeSet = new HashSet<TroopType>();
        foreach (var troopType in Enum.GetValues<TroopType>())
        {
            var troopCount = GetTroopCount(allocation, troopType);
            if (troopCount <= 0)
            {
                continue;
            }

            troopTypeSet.Add(troopType);
            var troopModifier = troopType switch
            {
                TroopType.Infantry => 0.00f,
                TroopType.Spearman => 0.03f,
                TroopType.Cavalry => 0.08f,
                TroopType.Archer => 0.02f,
                TroopType.Crossbow => 0.05f,
                TroopType.Siege => 0.10f,
                _ => 0.0f
            };
            weightedModifier += troopModifier * troopCount / totalTroops;
        }

        var combinedArmsBonus = troopTypeSet.Count >= 3 ? 0.03f : 0.0f;
        return weightedModifier + combinedArmsBonus;
    }

    private static float GetSiegePressureModifier(TroopAllocationData allocation)
    {
        var totalTroops = allocation.Total;
        if (totalTroops <= 0)
        {
            return 0.0f;
        }

        var siegeTroops = allocation.Siege;
        return Math.Min(0.12f, siegeTroops / (float)totalTroops * 0.20f);
    }

    private static float GetAttackCounterModifier(TroopAllocationData attackerAllocation, CityData defender)
    {
        return
            GetCounterPressure(attackerAllocation, defender, TroopType.Infantry, TroopType.Archer, LightCounterBonus) +
            GetCounterPressure(attackerAllocation, defender, TroopType.Spearman, TroopType.Cavalry, StrongCounterBonus) +
            GetCounterPressure(attackerAllocation, defender, TroopType.Cavalry, TroopType.Archer, StrongCounterBonus) +
            GetCounterPressure(attackerAllocation, defender, TroopType.Archer, TroopType.Spearman, LightCounterBonus) +
            GetCounterPressure(attackerAllocation, defender, TroopType.Crossbow, TroopType.Cavalry, StrongCounterBonus);
    }

    private static float GetDefenseCounterModifier(TroopAllocationData attackerAllocation, CityData defender)
    {
        return
            GetCounterPressure(defender, attackerAllocation, TroopType.Infantry, TroopType.Archer, LightCounterBonus) +
            GetCounterPressure(defender, attackerAllocation, TroopType.Spearman, TroopType.Cavalry, StrongCounterBonus) +
            GetCounterPressure(defender, attackerAllocation, TroopType.Cavalry, TroopType.Archer, StrongCounterBonus) +
            GetCounterPressure(defender, attackerAllocation, TroopType.Archer, TroopType.Spearman, LightCounterBonus) +
            GetCounterPressure(defender, attackerAllocation, TroopType.Crossbow, TroopType.Cavalry, StrongCounterBonus);
    }

    private static float GetCounterPressure(
        TroopAllocationData attackerAllocation,
        CityData defender,
        TroopType attackerType,
        TroopType counteredType,
        float bonus)
    {
        var attackerShare = GetTroopShare(attackerAllocation, attackerType);
        var defenderShare = GetTroopShare(defender, counteredType);
        return attackerShare * defenderShare * bonus;
    }

    private static float GetCounterPressure(
        CityData attacker,
        TroopAllocationData defenderAllocation,
        TroopType attackerType,
        TroopType counteredType,
        float bonus)
    {
        var attackerShare = GetTroopShare(attacker, attackerType);
        var defenderShare = GetTroopShare(defenderAllocation, counteredType);
        return attackerShare * defenderShare * bonus;
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

    private static float GetTroopShare(TroopAllocationData allocation, TroopType troopType)
    {
        if (allocation.Total <= 0)
        {
            return 0.0f;
        }

        return GetTroopCount(allocation, troopType) / (float)allocation.Total;
    }

    private static float GetTroopShare(CityData city, TroopType troopType)
    {
        if (city.Troops <= 0)
        {
            return 0.0f;
        }

        return city.GetTroops(troopType) / (float)city.Troops;
    }

    private static int GetTroopCount(TroopAllocationData allocation, TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Infantry => allocation.Infantry,
            TroopType.Spearman => allocation.Spearman,
            TroopType.Cavalry => allocation.Cavalry,
            TroopType.Archer => allocation.Archer,
            TroopType.Crossbow => allocation.Crossbow,
            TroopType.Siege => allocation.Siege,
            _ => 0
        };
    }
}
