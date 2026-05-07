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
        TroopAllocationData? attackingAllocation = null,
        List<int>? defendingOfficerIds = null,
        List<AttackOfficerDeploymentData>? defenderOfficerDeployments = null,
        TroopAllocationData? defendingAllocation = null)
    {
        var clampedAttackTroops = attackingTroops < 0 ? 0 : attackingTroops;
        var attackerStrength = GetAverageOfficerStat(world, attacker, officer => officer.Strength, item => item.StrengthBonus, OfficerProgressionStat.Strength, attackingOfficerIds);
        var attackerLeadership = GetAverageOfficerStat(world, attacker, officer => officer.Leadership, item => item.LeadershipBonus, OfficerProgressionStat.Leadership, attackingOfficerIds);
        var attackerCombat = GetAverageOfficerStat(world, attacker, officer => officer.Combat, item => item.CombatBonus, OfficerProgressionStat.Combat, attackingOfficerIds);
        var defenderLeadership = GetAverageOfficerStat(world, defender, officer => officer.Leadership, item => item.LeadershipBonus, OfficerProgressionStat.Leadership, defendingOfficerIds);
        var defenderCombat = GetAverageOfficerStat(world, defender, officer => officer.Combat, item => item.CombatBonus, OfficerProgressionStat.Combat, defendingOfficerIds);
        var effectiveAttackAllocation = GetDeploymentAllocation(attackingAllocation, attackOfficerDeployments);
        var effectiveDefenseAllocation = GetEffectiveDefenseAllocation(defender, defendingAllocation, defenderOfficerDeployments);

        var attackStat = attackerStrength * 0.3f + attackerLeadership * 0.4f + attackerCombat * 0.3f;
        var deploymentModifier = GetAttackDeploymentModifier(effectiveAttackAllocation);
        var siegePressure = GetSiegePressureModifier(effectiveAttackAllocation);
        var troopCounterAttackModifier = GetAttackCounterModifier(effectiveAttackAllocation, effectiveDefenseAllocation);
        var troopCounterDefenseModifier = GetDefenseCounterModifier(effectiveAttackAllocation, effectiveDefenseAllocation);
        var attackMultiplier = 1.0f + attackStat / 220.0f + deploymentModifier + troopCounterAttackModifier;
        var defenseMultiplier = 1.0f + (defender.Defense * 0.006f) + (defenderLeadership / 260.0f) + (defenderCombat / 550.0f) + GetDefenderTroopModifier(effectiveDefenseAllocation) + troopCounterDefenseModifier - siegePressure;
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

    private static TroopAllocationData GetDeploymentAllocation(
        TroopAllocationData? allocationOverride,
        List<AttackOfficerDeploymentData>? deployments)
    {
        if (allocationOverride != null && allocationOverride.Total > 0)
        {
            return allocationOverride;
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

    private static TroopAllocationData GetEffectiveDefenseAllocation(
        CityData defender,
        TroopAllocationData? defendingAllocation,
        List<AttackOfficerDeploymentData>? defenderDeployments)
    {
        var allocation = GetDeploymentAllocation(defendingAllocation, defenderDeployments);
        if (allocation.Total > 0)
        {
            return allocation;
        }

        return new TroopAllocationData
        {
            Infantry = defender.InfantryTroops,
            Spearman = defender.SpearmanTroops,
            Cavalry = defender.CavalryTroops,
            Archer = defender.ArcherTroops,
            Crossbow = defender.CrossbowTroops,
            Siege = defender.SiegeTroops
        };
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

    private static float GetAttackCounterModifier(TroopAllocationData attackerAllocation, TroopAllocationData defenderAllocation)
    {
        return
            GetCounterPressure(attackerAllocation, defenderAllocation, TroopType.Infantry, TroopType.Archer, LightCounterBonus) +
            GetCounterPressure(attackerAllocation, defenderAllocation, TroopType.Spearman, TroopType.Cavalry, StrongCounterBonus) +
            GetCounterPressure(attackerAllocation, defenderAllocation, TroopType.Cavalry, TroopType.Archer, StrongCounterBonus) +
            GetCounterPressure(attackerAllocation, defenderAllocation, TroopType.Archer, TroopType.Spearman, LightCounterBonus) +
            GetCounterPressure(attackerAllocation, defenderAllocation, TroopType.Crossbow, TroopType.Cavalry, StrongCounterBonus);
    }

    private static float GetDefenseCounterModifier(TroopAllocationData attackerAllocation, TroopAllocationData defenderAllocation)
    {
        return
            GetCounterPressure(defenderAllocation, attackerAllocation, TroopType.Infantry, TroopType.Archer, LightCounterBonus) +
            GetCounterPressure(defenderAllocation, attackerAllocation, TroopType.Spearman, TroopType.Cavalry, StrongCounterBonus) +
            GetCounterPressure(defenderAllocation, attackerAllocation, TroopType.Cavalry, TroopType.Archer, StrongCounterBonus) +
            GetCounterPressure(defenderAllocation, attackerAllocation, TroopType.Archer, TroopType.Spearman, LightCounterBonus) +
            GetCounterPressure(defenderAllocation, attackerAllocation, TroopType.Crossbow, TroopType.Cavalry, StrongCounterBonus);
    }

    private static float GetCounterPressure(
        TroopAllocationData attackerAllocation,
        TroopAllocationData defenderAllocation,
        TroopType activeType,
        TroopType counteredType,
        float bonus)
    {
        var attackerShare = GetTroopShare(attackerAllocation, activeType);
        var defenderShare = GetTroopShare(defenderAllocation, counteredType);
        return attackerShare * defenderShare * bonus;
    }

    private static float GetDefenderTroopModifier(TroopAllocationData defenderAllocation)
    {
        var totalTroops = defenderAllocation.Total;
        if (totalTroops <= 0)
        {
            return 0.0f;
        }

        return
            (defenderAllocation.Spearman * 0.03f +
             defenderAllocation.Archer * 0.02f +
             defenderAllocation.Crossbow * 0.05f +
             defenderAllocation.Siege * 0.04f) / totalTroops;
    }

    private static float GetTroopShare(TroopAllocationData allocation, TroopType troopType)
    {
        if (allocation.Total <= 0)
        {
            return 0.0f;
        }

        return GetTroopCount(allocation, troopType) / (float)allocation.Total;
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
