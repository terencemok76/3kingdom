using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class InternalAffairsPlanningRules
{
    internal static bool IsConstructionProjectJob(InternalAffairsJobType jobType)
    {
        return jobType is InternalAffairsJobType.Construction or InternalAffairsJobType.Manufacturing;
    }


    internal static InternalAffairsJobType? ChooseAuthorizedPlanJob(WorldState world, CityData city, OfficerData? prefect = null)
    {
        var activeJobs = new HashSet<InternalAffairsJobType>(
            world.InternalAffairsSchedules
                .Where(schedule => schedule.State == InternalAffairsScheduleState.Active && schedule.CityId == city.Id)
                .Select(schedule => schedule.JobType));
        var activeConstructionProjects = new HashSet<ConstructionProjectType>(
            world.InternalAffairsSchedules
                .Where(schedule =>
                    schedule.State == InternalAffairsScheduleState.Active &&
                    schedule.CityId == city.Id &&
                    IsConstructionProjectJob(schedule.JobType))
                .Select(schedule => schedule.ConstructionProjectType));

        var politics = prefect?.Politics ?? 50;
        var intelligence = prefect?.Intelligence ?? 50;
        var leadership = prefect?.Leadership ?? 50;
        var ambition = prefect?.Ambition ?? 50;
        var loyalty = prefect?.Loyalty ?? 50;
        var balancedCivil = (politics + intelligence) / 2;
        var defensiveBias = (leadership + intelligence + Math.Max(0, 100 - ambition / 2)) / 3;
        var growthBias = (politics + Math.Max(0, ambition) + loyalty / 2) / 3;
        var constructionBias = (leadership + politics + intelligence) / 3;

        var candidates = new (InternalAffairsJobType JobType, int Score)[]
        {
            (
                InternalAffairsJobType.Farm,
                city.Farm - (balancedCivil / 6) - ((city.Population < 42000 ? 10 : 0)) - ((city.Food < 1800 ? 14 : 0))
            ),
            (
                InternalAffairsJobType.Commercial,
                city.Commercial - (growthBias / 6) - ((city.Gold < 700 ? 18 : 0))
            ),
            (
                InternalAffairsJobType.Defend,
                city.Defense - (defensiveBias / 6) - ((city.Defense < 55 ? 14 : 0)) - ((city.Troops < 1800 ? 6 : 0))
            ),
            (
                InternalAffairsJobType.WaterControl,
                city.DisasterPrevention - (intelligence / 6) - ((city.DisasterPrevention < 45 ? 12 : 0))
            ),
            (
                InternalAffairsJobType.Construction,
                ((city.Commercial + city.Defense) / 2) - (constructionBias / 7) - (GetMissingFacilityCount(city) > 0 ? 8 : 0)
            )
        };

        var selectedJob = candidates
            .Where(candidate => !activeJobs.Contains(candidate.JobType))
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => (int)candidate.JobType)
            .Select(candidate => (InternalAffairsJobType?)candidate.JobType)
            .FirstOrDefault();

        if (selectedJob == InternalAffairsJobType.Construction)
        {
            var projectType = AiConstructionRules.ChooseConstructionProjectType(world, city);
            if (activeConstructionProjects.Contains(projectType))
            {
                return candidates
                    .Where(candidate => candidate.JobType != InternalAffairsJobType.Construction && !activeJobs.Contains(candidate.JobType))
                    .OrderBy(candidate => candidate.Score)
                    .ThenBy(candidate => (int)candidate.JobType)
                    .Select(candidate => (InternalAffairsJobType?)candidate.JobType)
                    .FirstOrDefault();
            }
        }

        return selectedJob;
    }

    internal static int ChooseAuthorizedPlanDuration(CityData city, InternalAffairsJobType jobType, OfficerData? prefect = null)
    {
        var politics = prefect?.Politics ?? 50;
        var intelligence = prefect?.Intelligence ?? 50;
        var leadership = prefect?.Leadership ?? 50;
        var ambition = prefect?.Ambition ?? 50;

        return jobType switch
        {
            InternalAffairsJobType.Farm when city.Farm < 45 => 4,
            InternalAffairsJobType.Commercial when city.Commercial < 45 => 4,
            InternalAffairsJobType.Defend when city.Defense < 50 => 4,
            InternalAffairsJobType.WaterControl when city.DisasterPrevention < 35 => 4,
            InternalAffairsJobType.Construction when GetMissingFacilityCount(city) > 0 => 3,
            InternalAffairsJobType.Defend when leadership >= 75 => 3,
            InternalAffairsJobType.Commercial when politics >= 75 && ambition >= 60 => 3,
            InternalAffairsJobType.Farm when intelligence >= 70 => 3,
            _ => 2
        };
    }


    private static int GetMissingFacilityCount(CityData city)
    {
        var count = 0;
        if (city.BowWorkshopLevel <= 0)
        {
            count += 1;
        }

        if (city.SiegeWorkshopLevel <= 0)
        {
            count += 1;
        }

        if (city.HorsePastureLevel <= 0)
        {
            count += 1;
        }

        return count;
    }


    internal static int ScoreInternalAffairsOfficer(OfficerData officer, InternalAffairsJobType jobType)
    {
        return jobType switch
        {
            InternalAffairsJobType.Farm => officer.Politics * 3 + officer.Charm + officer.Intelligence + officer.FarmRank * 25,
            InternalAffairsJobType.Commercial => officer.Politics * 3 + officer.Intelligence * 2 + officer.CommercialRank * 25,
            InternalAffairsJobType.Defend => officer.Leadership * 2 + officer.Politics * 2 + officer.DefendRank * 25,
            InternalAffairsJobType.WaterControl => officer.Intelligence * 2 + officer.Politics * 2 + officer.DisasterPreventionRank * 25,
            InternalAffairsJobType.Construction => officer.Politics * 2 + officer.Leadership + officer.Intelligence + officer.ConstructionRank * 25,
            InternalAffairsJobType.Manufacturing => officer.Politics * 2 + officer.Leadership + officer.Intelligence + officer.ConstructionRank * 25,
            InternalAffairsJobType.Extraction => officer.Politics * 2 + officer.Intelligence + officer.ConstructionRank * 25,
            InternalAffairsJobType.Survey => officer.Intelligence * 3 + officer.Politics * 2 + officer.ConstructionRank * 25,
            _ => officer.Politics + officer.Intelligence + officer.Charm
        };
    }


    internal static int GetInternalAffairsMonthlyGoldCost(InternalAffairsScheduleData schedule)
    {
        return GetInternalAffairsMonthlyGoldCost(schedule.InvestedGold, schedule.TotalMonths, schedule.RemainingMonths);
    }

    internal static int GetInternalAffairsMonthlyGoldCost(int investedGold, int totalMonths, int remainingMonths)
    {
        if (investedGold <= 0 || totalMonths <= 0 || remainingMonths <= 0)
        {
            return 0;
        }

        return investedGold;
    }

    internal static int GetMinimumInternalAffairsGold(int months)
    {
        return 1;
    }

    internal static int GetRecommendedInternalAffairsGold(InternalAffairsJobType jobType, int months)
    {
        return jobType switch
        {
            InternalAffairsJobType.Farm => 60,
            InternalAffairsJobType.Commercial => 70,
            InternalAffairsJobType.Defend => 80,
            InternalAffairsJobType.WaterControl => 70,
            InternalAffairsJobType.Construction => 100,
            InternalAffairsJobType.Manufacturing => 100,
            InternalAffairsJobType.Extraction => 75,
            InternalAffairsJobType.Survey => 120,
            _ => 60
        };
    }

}
