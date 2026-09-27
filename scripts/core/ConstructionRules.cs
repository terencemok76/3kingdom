using System;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class ConstructionRules
{
    internal readonly record struct StrategicResourceCost(int Wood, int Metal, int Stone)
    {
        internal static readonly StrategicResourceCost None = new(0, 0, 0);
        internal bool IsEmpty => Wood <= 0 && Metal <= 0 && Stone <= 0;

        internal StrategicResourceCost Add(StrategicResourceCost other) =>
            new(Wood + other.Wood, Metal + other.Metal, Stone + other.Stone);
    }

    internal readonly record struct ConstructionProgressResult(
        int ValuesGained,
        int CurrentValue,
        int CurrentProgress,
        int RequiredForNextValue,
        StrategicResourceCost MaterialsSpent,
        bool WaitingForMaterials);

    private const int SiegeEnginePointsPerUnit = 100;
    private const int DefenseStoneCostPerPoint = 2;
    private const int DisasterPreventionWoodCostPerPoint = 1;
    private const int DisasterPreventionStoneCostPerPoint = 2;

    internal static int GetRequiredPointsForNextLevel(int currentLevel)
    {
        return 100 * Math.Max(1, currentLevel + 1);
    }

    internal static int GetRequiredPointsForNextValue(ConstructionProjectType projectType, int currentValue)
    {
        return IsFacilityProject(projectType)
            ? GetRequiredPointsForNextLevel(currentValue)
            : SiegeEnginePointsPerUnit;
    }

    internal static StrategicResourceCost GetResourceCost(ConstructionProjectType projectType)
    {
        return projectType switch
        {
            ConstructionProjectType.BowWorkshop => new StrategicResourceCost(35, 15, 0),
            ConstructionProjectType.SiegeWorkshop => new StrategicResourceCost(60, 45, 35),
            ConstructionProjectType.HorsePasture => new StrategicResourceCost(45, 0, 25),
            ConstructionProjectType.ResourceDepot => new StrategicResourceCost(50, 25, 35),
            ConstructionProjectType.Granary => new StrategicResourceCost(40, 0, 30),
            ConstructionProjectType.HorseStable => new StrategicResourceCost(30, 0, 20),
            ConstructionProjectType.SupplyCart => new StrategicResourceCost(15, 5, 0),
            ConstructionProjectType.Ladder => new StrategicResourceCost(25, 10, 0),
            ConstructionProjectType.Ram => new StrategicResourceCost(50, 30, 0),
            ConstructionProjectType.Catapult => new StrategicResourceCost(40, 35, 25),
            _ => StrategicResourceCost.None
        };
    }

    internal static bool CanAffordResourceCost(CityData city, StrategicResourceCost cost) =>
        city.Wood >= cost.Wood && city.Metal >= cost.Metal && city.Stone >= cost.Stone;

    internal static bool TrySpendResourceCost(CityData city, StrategicResourceCost cost)
    {
        if (!CanAffordResourceCost(city, cost))
        {
            return false;
        }

        city.Wood -= cost.Wood;
        city.Metal -= cost.Metal;
        city.Stone -= cost.Stone;
        return true;
    }

    internal static int ApplyDefenseRepair(CityData city, int requestedDefenseGain)
    {
        var affordableDefenseGain = Math.Min(Math.Max(0, requestedDefenseGain), city.Stone / DefenseStoneCostPerPoint);
        city.Stone -= affordableDefenseGain * DefenseStoneCostPerPoint;
        return affordableDefenseGain;
    }

    internal static int GetDefenseStoneCost(int defenseGain) => Math.Max(0, defenseGain) * DefenseStoneCostPerPoint;

    internal static int ApplyDisasterPrevention(CityData city, int requestedGain)
    {
        var affordableGain = Math.Min(
            Math.Max(0, requestedGain),
            Math.Min(city.Wood / DisasterPreventionWoodCostPerPoint, city.Stone / DisasterPreventionStoneCostPerPoint));
        city.Wood -= affordableGain * DisasterPreventionWoodCostPerPoint;
        city.Stone -= affordableGain * DisasterPreventionStoneCostPerPoint;
        return affordableGain;
    }

    internal static StrategicResourceCost GetDisasterPreventionResourceCost(int preventionGain) =>
        new(Math.Max(0, preventionGain) * DisasterPreventionWoodCostPerPoint, 0, Math.Max(0, preventionGain) * DisasterPreventionStoneCostPerPoint);

    internal static int GetConstructionPoints(
        int politics,
        int intelligence,
        int leadership,
        int monthlyGold,
        int progressionBonus)
    {
        var goldPoints = Math.Max(1, monthlyGold / 20);
        var officerPoints = Math.Max(0, (politics * 2 + intelligence + leadership) / 60);
        return Math.Max(5, goldPoints + officerPoints + progressionBonus + 2);
    }

    internal static bool IsFacilityProject(ConstructionProjectType projectType)
    {
        return projectType is ConstructionProjectType.BowWorkshop or ConstructionProjectType.SiegeWorkshop or ConstructionProjectType.HorsePasture or ConstructionProjectType.ResourceDepot or ConstructionProjectType.Granary or ConstructionProjectType.HorseStable;
    }

    internal static bool IsSiegeEngineProject(ConstructionProjectType projectType)
    {
        return projectType is ConstructionProjectType.Ram or ConstructionProjectType.Catapult or ConstructionProjectType.Ladder or ConstructionProjectType.SupplyCart;
    }

    internal static SiegeEngineType GetSiegeEngineType(ConstructionProjectType projectType)
    {
        return projectType switch
        {
            ConstructionProjectType.Ram => SiegeEngineType.Ram,
            ConstructionProjectType.Catapult => SiegeEngineType.Catapult,
            ConstructionProjectType.Ladder => SiegeEngineType.Ladder,
            _ => SiegeEngineType.None
        };
    }

    internal static ConstructionProgressResult ApplyProgress(CityData city, ConstructionProjectType projectType, int progressPoints)
    {
        var currentValue = GetProjectValue(city, projectType);
        var currentProgress = GetProjectProgress(city, projectType);
        if (progressPoints <= 0 || projectType == ConstructionProjectType.None)
        {
            return new ConstructionProgressResult(0, currentValue, currentProgress, GetRequiredPointsForNextValue(projectType, currentValue), StrategicResourceCost.None, false);
        }

        var value = currentValue;
        var progress = currentProgress + progressPoints;
        var valuesGained = 0;
        var materialsSpent = StrategicResourceCost.None;
        var waitingForMaterials = false;

        while (progress >= GetRequiredPointsForNextValue(projectType, value))
        {
            var resourceCost = GetResourceCost(projectType);
            if (!TrySpendResourceCost(city, resourceCost))
            {
                progress = GetRequiredPointsForNextValue(projectType, value);
                waitingForMaterials = true;
                break;
            }

            progress -= GetRequiredPointsForNextValue(projectType, value);
            value += 1;
            valuesGained += 1;
            materialsSpent = materialsSpent.Add(resourceCost);
        }

        SetProjectValue(city, projectType, value);
        SetProjectProgress(city, projectType, progress);
        return new ConstructionProgressResult(valuesGained, value, progress, GetRequiredPointsForNextValue(projectType, value), materialsSpent, waitingForMaterials);
    }

    internal static int GetLevel(CityData city, ConstructionProjectType projectType)
    {
        return projectType switch
        {
            ConstructionProjectType.BowWorkshop => city.BowWorkshopLevel,
            ConstructionProjectType.SiegeWorkshop => city.SiegeWorkshopLevel,
            ConstructionProjectType.HorsePasture => city.HorsePastureLevel,
            ConstructionProjectType.ResourceDepot => city.ResourceDepotLevel,
            ConstructionProjectType.Granary => city.GranaryLevel,
            ConstructionProjectType.HorseStable => city.HorseStableLevel,
            _ => 0
        };
    }

    internal static int GetProgress(CityData city, ConstructionProjectType projectType)
    {
        return projectType switch
        {
            ConstructionProjectType.BowWorkshop => city.BowWorkshopProgress,
            ConstructionProjectType.SiegeWorkshop => city.SiegeWorkshopProgress,
            ConstructionProjectType.HorsePasture => city.HorsePastureProgress,
            ConstructionProjectType.ResourceDepot => city.ResourceDepotProgress,
            ConstructionProjectType.Granary => city.GranaryProgress,
            ConstructionProjectType.HorseStable => city.HorseStableProgress,
            _ => 0
        };
    }

    internal static int GetSiegeEngineCount(CityData city, SiegeEngineType siegeEngineType) => city.GetSiegeEngineCount(siegeEngineType);

    internal static int GetSiegeEngineProgress(CityData city, SiegeEngineType siegeEngineType) => city.GetSiegeEngineProgress(siegeEngineType);

    private static int GetProjectValue(CityData city, ConstructionProjectType projectType)
    {
        if (IsFacilityProject(projectType))
        {
            return GetLevel(city, projectType);
        }

        return projectType == ConstructionProjectType.SupplyCart
            ? city.SupplyCartCount
            : city.GetSiegeEngineCount(GetSiegeEngineType(projectType));
    }

    private static int GetProjectProgress(CityData city, ConstructionProjectType projectType)
    {
        if (IsFacilityProject(projectType))
        {
            return GetProgress(city, projectType);
        }

        return projectType == ConstructionProjectType.SupplyCart
            ? city.SupplyCartProgress
            : city.GetSiegeEngineProgress(GetSiegeEngineType(projectType));
    }

    private static void SetProjectValue(CityData city, ConstructionProjectType projectType, int value)
    {
        switch (projectType)
        {
            case ConstructionProjectType.BowWorkshop:
                city.BowWorkshopLevel = value;
                break;
            case ConstructionProjectType.SiegeWorkshop:
                city.SiegeWorkshopLevel = value;
                break;
            case ConstructionProjectType.HorsePasture:
                city.HorsePastureLevel = value;
                break;
            case ConstructionProjectType.ResourceDepot:
                city.ResourceDepotLevel = value;
                break;
            case ConstructionProjectType.Granary:
                city.GranaryLevel = value;
                break;
            case ConstructionProjectType.HorseStable:
                city.HorseStableLevel = value;
                break;
            case ConstructionProjectType.Ram:
                city.RamCount = value;
                break;
            case ConstructionProjectType.Catapult:
                city.CatapultCount = value;
                break;
            case ConstructionProjectType.Ladder:
                city.LadderCount = value;
                break;
            case ConstructionProjectType.SupplyCart:
                city.SupplyCartCount = value;
                break;
        }
    }

    private static void SetProjectProgress(CityData city, ConstructionProjectType projectType, int value)
    {
        switch (projectType)
        {
            case ConstructionProjectType.BowWorkshop:
                city.BowWorkshopProgress = value;
                break;
            case ConstructionProjectType.SiegeWorkshop:
                city.SiegeWorkshopProgress = value;
                break;
            case ConstructionProjectType.HorsePasture:
                city.HorsePastureProgress = value;
                break;
            case ConstructionProjectType.ResourceDepot:
                city.ResourceDepotProgress = value;
                break;
            case ConstructionProjectType.Granary:
                city.GranaryProgress = value;
                break;
            case ConstructionProjectType.HorseStable:
                city.HorseStableProgress = value;
                break;
            case ConstructionProjectType.Ram:
                city.RamProgress = value;
                break;
            case ConstructionProjectType.Catapult:
                city.CatapultProgress = value;
                break;
            case ConstructionProjectType.Ladder:
                city.LadderProgress = value;
                break;
            case ConstructionProjectType.SupplyCart:
                city.SupplyCartProgress = value;
                break;
        }
    }
}
