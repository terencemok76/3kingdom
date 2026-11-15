using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class CampaignDeploymentRules
{
    internal static void ValidateCityCanSupply(
        CityData city,
        IReadOnlyCollection<AttackOfficerDeploymentData> deployments,
        TroopAllocationData allocation,
        int gold,
        int food)
    {
        if ((deployments.Count == 0 && gold <= 0 && food <= 0) || deployments.Any(item => IsInvalidDeployment(item, city)))
        {
            throw new ReinforcementDispatchException(ReinforcementDispatchFailure.InvalidDeployment, "The reinforcement deployment is invalid.");
        }

        if (allocation.Infantry > city.InfantryTroops ||
            allocation.Spearman > city.SpearmanTroops ||
            allocation.Cavalry > city.CavalryTroops ||
            allocation.Archer > city.ArcherTroops ||
            allocation.Crossbow > city.CrossbowTroops ||
            allocation.Siege > city.SiegeTroops ||
            gold < 0 || gold > city.Gold || food < 0 || food > city.Food)
        {
            throw new ReinforcementDispatchException(ReinforcementDispatchFailure.InsufficientResources, "The source city lacks the selected troops or resources.");
        }

        var siege = BuildSiegeEngineAllocation(deployments);
        if (siege.Ram > city.RamCount || siege.Catapult > city.CatapultCount || siege.Ladder > city.LadderCount)
        {
            throw new ReinforcementDispatchException(ReinforcementDispatchFailure.InsufficientSiegeEngines, "The source city lacks the selected siege engines.");
        }
    }

    private static bool IsInvalidDeployment(AttackOfficerDeploymentData deployment, CityData city) =>
        deployment.OfficerId <= 0 || !city.OfficerIds.Contains(deployment.OfficerId) || deployment.TroopCount <= 0;


    internal static TroopAllocationData BuildTroopAllocation(IEnumerable<AttackOfficerDeploymentData> deployments)
    {
        var allocation = new TroopAllocationData();
        foreach (var item in deployments)
        {
            switch (item.TroopType)
            {
                case TroopType.Infantry: allocation.Infantry += item.TroopCount; break;
                case TroopType.Spearman: allocation.Spearman += item.TroopCount; break;
                case TroopType.Cavalry: allocation.Cavalry += item.TroopCount; break;
                case TroopType.Archer: allocation.Archer += item.TroopCount; break;
                case TroopType.Crossbow: allocation.Crossbow += item.TroopCount; break;
                case TroopType.Siege: allocation.Siege += item.TroopCount; break;
            }
        }

        return allocation;
    }

    internal static SiegeEngineAllocationData BuildSiegeEngineAllocation(IEnumerable<AttackOfficerDeploymentData> deployments)
    {
        var result = new SiegeEngineAllocationData();
        foreach (var item in deployments.Where(item => item.TroopType == TroopType.Siege))
        {
            switch (item.SiegeEngineType)
            {
                case SiegeEngineType.Ram: result.Ram++; break;
                case SiegeEngineType.Catapult: result.Catapult++; break;
                case SiegeEngineType.Ladder: result.Ladder++; break;
            }
        }

        return result;
    }

}
