using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class CommandTroopAllocationRules
{
    internal static TroopAllocationData CreateTroopAllocationFromTotal(CityData city, int requestedTroops)
    {
        var allocation = new TroopAllocationData();
        var remaining = requestedTroops <= 0 || city.Troops <= 0 ? 0 : Math.Min(requestedTroops, city.Troops);
        if (remaining <= 0)
        {
            return allocation;
        }

        foreach (var troopType in new[]
                 {
                     TroopType.Infantry,
                     TroopType.Spearman,
                     TroopType.Archer,
                     TroopType.Cavalry,
                     TroopType.Crossbow,
                     TroopType.Siege
                 })
        {
            if (remaining <= 0)
            {
                break;
            }

            var available = city.GetTroops(troopType);
            if (available <= 0)
            {
                continue;
            }

            var toTake = Math.Min(available, remaining);
            SetTroopAllocationValue(allocation, troopType, toTake);
            remaining -= toTake;
        }

        return allocation;
    }

    internal static TroopAllocationData ClampTroopAllocationToCity(CityData city, TroopAllocationData requested)
    {
        return new TroopAllocationData
        {
            Infantry = ClampRequestedTroopAmount(requested.Infantry, city.InfantryTroops),
            Spearman = ClampRequestedTroopAmount(requested.Spearman, city.SpearmanTroops),
            Cavalry = ClampRequestedTroopAmount(requested.Cavalry, city.CavalryTroops),
            Archer = ClampRequestedTroopAmount(requested.Archer, city.ArcherTroops),
            Crossbow = ClampRequestedTroopAmount(requested.Crossbow, city.CrossbowTroops),
            Siege = ClampRequestedTroopAmount(requested.Siege, city.EngineerTroops)
        };
    }

    private static int ClampRequestedTroopAmount(int requestedAmount, int availableAmount)
    {
        return System.Math.Clamp(requestedAmount, 0, System.Math.Max(0, availableAmount));
    }

    internal static TroopAllocationData CreateTroopAllocationFromAttackDeployments(IEnumerable<AttackOfficerDeploymentData> deployments)
    {
        var allocation = new TroopAllocationData();
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

    private static SiegeEngineAllocationData CreateSiegeEngineAllocationFromAttackDeployments(IEnumerable<AttackOfficerDeploymentData> deployments)
    {
        var allocation = new SiegeEngineAllocationData();
        foreach (var deployment in deployments)
        {
            if (deployment.TroopType != TroopType.Siege || deployment.TroopCount <= 0)
            {
                continue;
            }

            switch (deployment.SiegeEngineType)
            {
                case SiegeEngineType.Ram:
                    allocation.Ram += 1;
                    break;
                case SiegeEngineType.Catapult:
                    allocation.Catapult += 1;
                    break;
                case SiegeEngineType.Ladder:
                    allocation.Ladder += 1;
                    break;
            }
        }

        return allocation;
    }

    private static void SetTroopAllocationValue(TroopAllocationData allocation, TroopType troopType, int value)
    {
        switch (troopType)
        {
            case TroopType.Infantry:
                allocation.Infantry = value;
                break;
            case TroopType.Spearman:
                allocation.Spearman = value;
                break;
            case TroopType.Cavalry:
                allocation.Cavalry = value;
                break;
            case TroopType.Archer:
                allocation.Archer = value;
                break;
            case TroopType.Crossbow:
                allocation.Crossbow = value;
                break;
            case TroopType.Siege:
                allocation.Siege = value;
                break;
        }
    }

    private static int GetTroopAllocationValue(TroopAllocationData allocation, TroopType troopType)
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

    internal static TroopAllocationData ScaleTroopAllocationToTotal(TroopAllocationData source, int targetTotal)
    {
        var allocation = new TroopAllocationData();
        var sourceTotal = source.Total;
        if (sourceTotal <= 0 || targetTotal <= 0)
        {
            return allocation;
        }

        if (targetTotal >= sourceTotal)
        {
            allocation.Infantry = source.Infantry;
            allocation.Spearman = source.Spearman;
            allocation.Cavalry = source.Cavalry;
            allocation.Archer = source.Archer;
            allocation.Crossbow = source.Crossbow;
            allocation.Siege = source.Siege;
            return allocation;
        }

        var troopTypes = new[]
        {
            TroopType.Infantry,
            TroopType.Spearman,
            TroopType.Cavalry,
            TroopType.Archer,
            TroopType.Crossbow,
            TroopType.Siege
        };
        var remainders = new List<(TroopType TroopType, double Fraction)>();
        var allocated = 0;
        foreach (var troopType in troopTypes)
        {
            var current = GetTroopAllocationValue(source, troopType);
            if (current <= 0)
            {
                SetTroopAllocationValue(allocation, troopType, 0);
                continue;
            }

            var scaled = current * targetTotal / (double)sourceTotal;
            var whole = (int)Math.Floor(scaled);
            SetTroopAllocationValue(allocation, troopType, whole);
            allocated += whole;
            remainders.Add((troopType, scaled - whole));
        }

        var remaining = targetTotal - allocated;
        foreach (var (troopType, _) in remainders
                     .OrderByDescending(item => item.Fraction)
                     .ThenByDescending(item => GetTroopAllocationValue(source, item.TroopType)))
        {
            if (remaining <= 0)
            {
                break;
            }

            var current = GetTroopAllocationValue(allocation, troopType);
            var sourceValue = GetTroopAllocationValue(source, troopType);
            if (current >= sourceValue)
            {
                continue;
            }

            SetTroopAllocationValue(allocation, troopType, current + 1);
            remaining -= 1;
        }

        return allocation;
    }

    internal static TroopAllocationData CreateTroopAllocationFromCityProportion(CityData city, int targetTotal)
    {
        return ScaleTroopAllocationToTotal(new TroopAllocationData
        {
            Infantry = city.InfantryTroops,
            Spearman = city.SpearmanTroops,
            Cavalry = city.CavalryTroops,
            Archer = city.ArcherTroops,
            Crossbow = city.CrossbowTroops,
            Siege = city.SiegeTroops
        }, targetTotal);
    }

}
