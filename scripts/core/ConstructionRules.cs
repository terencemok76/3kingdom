using System;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class ConstructionRules
{
    internal readonly record struct ConstructionProgressResult(int ValuesGained, int CurrentValue, int CurrentProgress, int RequiredForNextValue);

    private const int SiegeEnginePointsPerUnit = 100;

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
        return projectType is ConstructionProjectType.BowWorkshop or ConstructionProjectType.SiegeWorkshop or ConstructionProjectType.HorsePasture;
    }

    internal static bool IsSiegeEngineProject(ConstructionProjectType projectType)
    {
        return projectType is ConstructionProjectType.Ram or ConstructionProjectType.Catapult or ConstructionProjectType.Ladder;
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
            return new ConstructionProgressResult(0, currentValue, currentProgress, GetRequiredPointsForNextValue(projectType, currentValue));
        }

        var value = currentValue;
        var progress = currentProgress + progressPoints;
        var valuesGained = 0;

        while (progress >= GetRequiredPointsForNextValue(projectType, value))
        {
            progress -= GetRequiredPointsForNextValue(projectType, value);
            value += 1;
            valuesGained += 1;
        }

        SetProjectValue(city, projectType, value);
        SetProjectProgress(city, projectType, progress);
        return new ConstructionProgressResult(valuesGained, value, progress, GetRequiredPointsForNextValue(projectType, value));
    }

    internal static int GetLevel(CityData city, ConstructionProjectType projectType)
    {
        return projectType switch
        {
            ConstructionProjectType.BowWorkshop => city.BowWorkshopLevel,
            ConstructionProjectType.SiegeWorkshop => city.SiegeWorkshopLevel,
            ConstructionProjectType.HorsePasture => city.HorsePastureLevel,
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

        return city.GetSiegeEngineCount(GetSiegeEngineType(projectType));
    }

    private static int GetProjectProgress(CityData city, ConstructionProjectType projectType)
    {
        if (IsFacilityProject(projectType))
        {
            return GetProgress(city, projectType);
        }

        return city.GetSiegeEngineProgress(GetSiegeEngineType(projectType));
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
            case ConstructionProjectType.Ram:
                city.RamCount = value;
                break;
            case ConstructionProjectType.Catapult:
                city.CatapultCount = value;
                break;
            case ConstructionProjectType.Ladder:
                city.LadderCount = value;
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
            case ConstructionProjectType.Ram:
                city.RamProgress = value;
                break;
            case ConstructionProjectType.Catapult:
                city.CatapultProgress = value;
                break;
            case ConstructionProjectType.Ladder:
                city.LadderProgress = value;
                break;
        }
    }
}
