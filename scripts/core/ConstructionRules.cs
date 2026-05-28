using System;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class ConstructionRules
{
    internal readonly record struct ConstructionProgressResult(int LevelsGained, int CurrentLevel, int CurrentProgress, int RequiredForNextLevel);

    internal static int GetRequiredPointsForNextLevel(int currentLevel)
    {
        return 100 * Math.Max(1, currentLevel + 1);
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

    internal static ConstructionProgressResult ApplyProgress(CityData city, ConstructionProjectType projectType, int progressPoints)
    {
        if (progressPoints <= 0 || projectType == ConstructionProjectType.None)
        {
            var currentLevel = GetLevel(city, projectType);
            return new ConstructionProgressResult(0, currentLevel, GetProgress(city, projectType), GetRequiredPointsForNextLevel(currentLevel));
        }

        var level = GetLevel(city, projectType);
        var progress = GetProgress(city, projectType) + progressPoints;
        var levelsGained = 0;

        while (progress >= GetRequiredPointsForNextLevel(level))
        {
            progress -= GetRequiredPointsForNextLevel(level);
            level += 1;
            levelsGained += 1;
        }

        SetLevel(city, projectType, level);
        SetProgress(city, projectType, progress);
        return new ConstructionProgressResult(levelsGained, level, progress, GetRequiredPointsForNextLevel(level));
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

    private static void SetLevel(CityData city, ConstructionProjectType projectType, int value)
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
        }
    }

    private static void SetProgress(CityData city, ConstructionProjectType projectType, int value)
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
        }
    }
}
