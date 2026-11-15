using System.Collections.Generic;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal enum DeploymentValidationError
{
    None, NoOfficers, NoTroops, TroopsExceeded, EngineersExceeded, EquipmentExceeded, SupplyCartExceeded
}

internal static class DeploymentValidation
{
    public static DeploymentValidationError Validate(DeploymentSnapshot snapshot, CityData city) =>
        Validate(snapshot.Officers, snapshot.Troops, snapshot.Support, snapshot.Equipment, city);

    public static DeploymentValidationError Validate(IReadOnlyCollection<AttackOfficerDeploymentData> deployments, TroopAllocationData allocation, BattleSupportDeploymentData support, SiegeEngineAllocationData equipment, CityData city)
    {
        if (deployments.Count == 0) return DeploymentValidationError.NoOfficers;
        if (allocation.Total <= 0) return DeploymentValidationError.NoTroops;
        if (allocation.Infantry > city.InfantryTroops || allocation.Spearman > city.SpearmanTroops ||
            allocation.Cavalry > city.CavalryTroops || allocation.Archer > city.ArcherTroops ||
            allocation.Crossbow > city.CrossbowTroops) return DeploymentValidationError.TroopsExceeded;
        if (support.TotalEngineerCount > city.EngineerTroops) return DeploymentValidationError.EngineersExceeded;
        if (equipment.Ram > city.RamCount || equipment.Catapult > city.CatapultCount || equipment.Ladder > city.LadderCount)
            return DeploymentValidationError.EquipmentExceeded;
        if (support.SupplyCart && city.SupplyCartCount <= 0) return DeploymentValidationError.SupplyCartExceeded;
        return DeploymentValidationError.None;
    }
}
