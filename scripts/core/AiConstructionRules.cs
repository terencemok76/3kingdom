using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal enum AiCityDevelopmentPlan
{
    None,
    Supply,
    Resources,
    HorseBreeding,
    Fortress
}

// A production city makes one siege engine, then sends the completed engine to
// the adjacent frontline city.  Strategic raw materials deliberately stay
// local: this avoids treating every shortage as a convoy micro-management task.
internal readonly record struct AiSiegeEquipmentPlan(int TargetCityId, ConstructionProjectType ProjectType)
{
    internal bool IsValid => TargetCityId > 0 && ProjectType != ConstructionProjectType.None;
}

internal static class AiConstructionRules
{
    private const int MinimumStrategistPlanningIntelligence = 75;

    internal static ConstructionProjectType ChooseConstructionProjectType(WorldState world, CityData city)
    {
        return ChooseConstructionProjectType(world, city, AiCityDevelopmentPlan.None);
    }

    internal static ConstructionProjectType ChooseConstructionProjectType(WorldState world, CityData city, AiCityDevelopmentPlan plan)
    {
        var plannedProject = ChoosePlannedConstructionProject(city, plan);
        if (plannedProject != ConstructionProjectType.None)
        {
            return plannedProject;
        }

        if (ShouldExpandGranary(city))
        {
            return ConstructionProjectType.Granary;
        }

        if (ShouldExpandHorseStable(city))
        {
            return ConstructionProjectType.HorseStable;
        }

        if (ShouldExpandResourceDepot(city))
        {
            return ConstructionProjectType.ResourceDepot;
        }

        if (city.BowWorkshopLevel <= 0)
        {
            return ConstructionProjectType.BowWorkshop;
        }

        if (city.SiegeWorkshopLevel <= 0)
        {
            return ConstructionProjectType.SiegeWorkshop;
        }

        if (city.HorsePastureLevel <= 0)
        {
            return ConstructionProjectType.HorsePasture;
        }

        if (ShouldUpgradeSiegeWorkshop(world, city))
        {
            return ConstructionProjectType.SiegeWorkshop;
        }

        if (!IsFrontlineCity(world, city) && !ConstructionRules.IsAtMaximumLevel(city, ConstructionProjectType.HorsePasture))
        {
            return ConstructionProjectType.HorsePasture;
        }

        if (!ConstructionRules.IsAtMaximumLevel(city, ConstructionProjectType.BowWorkshop))
        {
            return ConstructionProjectType.BowWorkshop;
        }

        var adjacentEnemies = GetAdjacentEnemyCities(world, city);
        var highestEnemyDefense = adjacentEnemies.Count == 0 ? 0 : adjacentEnemies.Max(enemy => enemy.Defense);
        if (highestEnemyDefense >= 70 && city.RamCount < 2)
        {
            return ConstructionProjectType.Ram;
        }

        if (city.CatapultCount < 2)
        {
            return ConstructionProjectType.Catapult;
        }

        if (city.SiegeTroops >= 200 && city.LadderCount < 1)
        {
            return ConstructionProjectType.Ladder;
        }

        if (city.RamCount < 3)
        {
            return ConstructionProjectType.Ram;
        }

        if (city.CatapultCount <= city.RamCount)
        {
            return ConstructionProjectType.Catapult;
        }

        return ConstructionProjectType.Ladder;
    }

    internal static AiCityDevelopmentPlan ChooseCityDevelopmentPlan(WorldState world, CityData city, FactionData? faction)
    {
        var strategist = GetPlanningStrategist(world, faction);
        if (strategist == null || strategist.Intelligence < MinimumStrategistPlanningIntelligence)
        {
            return AiCityDevelopmentPlan.None;
        }

        if (IsFrontlineCity(world, city) &&
            !ConstructionRules.IsAtMaximumLevel(city, ConstructionProjectType.SiegeWorkshop) &&
            (city.Defense < 65 || city.DisasterPrevention < 55 || city.SiegeWorkshopLevel < 2))
        {
            return AiCityDevelopmentPlan.Fortress;
        }

        if (!ConstructionRules.IsAtMaximumLevel(city, ConstructionProjectType.ResourceDepot) &&
            IsRawMaterialPressureAtLeast(city, 1, 2))
        {
            return AiCityDevelopmentPlan.Resources;
        }

        if (!ConstructionRules.IsAtMaximumLevel(city, ConstructionProjectType.Granary) &&
            MarketRules.GetAmount(city, MarketProductType.Food) * 2 >= MarketRules.GetCapacity(city, MarketProductType.Food))
        {
            return AiCityDevelopmentPlan.Supply;
        }

        if (!IsFrontlineCity(world, city) &&
            !ConstructionRules.IsAtMaximumLevel(city, ConstructionProjectType.HorsePasture) &&
            (city.Horses > 0 || city.HorsePastureLevel > 0))
        {
            return AiCityDevelopmentPlan.HorseBreeding;
        }

        return AiCityDevelopmentPlan.None;
    }

    internal static AiSiegeEquipmentPlan ChooseSiegeEquipmentPlan(WorldState world, CityData productionCity, FactionData? faction)
    {
        var strategist = GetPlanningStrategist(world, faction);
        if (strategist == null || strategist.Intelligence < MinimumStrategistPlanningIntelligence ||
            productionCity.SiegeWorkshopLevel <= 0 || IsFrontlineCity(world, productionCity))
        {
            return default;
        }

        var chancellor = GetChancellor(world, faction);
        var candidates = productionCity.ConnectedCityIds
            .Select(world.GetCity)
            .Where(target => target != null && target.OwnerFactionId == productionCity.OwnerFactionId && IsFrontlineCity(world, target))
            .Cast<CityData>()
            .Select(target => new
            {
                Target = target,
                Project = ChooseNeededSiegeEquipment(world, target),
                ProductionCity = ChooseBestSiegeEquipmentProductionCity(world, target, productionCity.OwnerFactionId, ChooseNeededSiegeEquipment(world, target))
            })
            .Where(candidate => candidate.Project != ConstructionProjectType.None && candidate.ProductionCity?.Id == productionCity.Id)
            .Select(candidate => new
            {
                candidate.Target,
                candidate.Project,
                Score = ScoreSiegeEquipmentPlan(world, candidate.Target, strategist, chancellor, GetCityGovernor(world, productionCity))
            })
            .Where(candidate => candidate.Score >= 150)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Target.Id)
            .FirstOrDefault();

        return candidates == null
            ? default
            : new AiSiegeEquipmentPlan(candidates.Target.Id, candidates.Project);
    }

    internal static bool HasReadySiegeEquipment(CityData city, ConstructionProjectType projectType) =>
        projectType switch
        {
            ConstructionProjectType.Ram => city.RamCount > 0,
            ConstructionProjectType.Catapult => city.CatapultCount > 0,
            ConstructionProjectType.Ladder => city.LadderCount > 0,
            ConstructionProjectType.SupplyCart => city.SupplyCartCount > 0,
            _ => false
        };

    private static ConstructionProjectType ChoosePlannedConstructionProject(CityData city, AiCityDevelopmentPlan plan)
    {
        return plan switch
        {
            AiCityDevelopmentPlan.Fortress when CanUpgrade(city, ConstructionProjectType.SiegeWorkshop) => ConstructionProjectType.SiegeWorkshop,
            AiCityDevelopmentPlan.Resources when CanUpgrade(city, ConstructionProjectType.ResourceDepot) => ConstructionProjectType.ResourceDepot,
            AiCityDevelopmentPlan.Supply when CanUpgrade(city, ConstructionProjectType.Granary) => ConstructionProjectType.Granary,
            AiCityDevelopmentPlan.HorseBreeding when ShouldExpandHorseStable(city) => ConstructionProjectType.HorseStable,
            AiCityDevelopmentPlan.HorseBreeding when CanUpgrade(city, ConstructionProjectType.HorsePasture) => ConstructionProjectType.HorsePasture,
            _ => ConstructionProjectType.None
        };
    }

    private static OfficerData? GetPlanningStrategist(WorldState world, FactionData? faction)
    {
        if (faction == null)
        {
            return null;
        }

        return faction.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null && officer.CityId > 0)
            .Cast<OfficerData>()
            .Where(officer => officer.Id == faction.ChiefStrategistOfficerId || OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Strategist))
            .OrderByDescending(officer => officer.Id == faction.ChiefStrategistOfficerId)
            .ThenByDescending(officer => officer.Intelligence)
            .ThenBy(officer => officer.Id)
            .FirstOrDefault();
    }

    private static OfficerData? GetChancellor(WorldState world, FactionData? faction)
    {
        var chancellor = faction == null ? null : world.GetOfficer(faction.ChancellorOfficerId);
        return chancellor is { CityId: > 0 } ? chancellor : null;
    }

    private static OfficerData? GetCityGovernor(WorldState world, CityData city)
    {
        return city.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null && OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Governor))
            .Cast<OfficerData>()
            .OrderByDescending(officer => officer.Politics)
            .ThenBy(officer => officer.Id)
            .FirstOrDefault();
    }

    private static ConstructionProjectType ChooseNeededSiegeEquipment(WorldState world, CityData target)
    {
        var adjacentEnemies = GetAdjacentEnemyCities(world, target);
        var highestEnemyDefense = adjacentEnemies.Count == 0 ? 0 : adjacentEnemies.Max(enemy => enemy.Defense);
        if (highestEnemyDefense >= 70 && target.RamCount < 2)
        {
            return ConstructionProjectType.Ram;
        }

        if (target.CatapultCount < 2)
        {
            return ConstructionProjectType.Catapult;
        }

        return target.SiegeTroops >= 200 && target.LadderCount < 1
            ? ConstructionProjectType.Ladder
            : ConstructionProjectType.None;
    }

    // One frontline request has one designated safe workshop.  This keeps the
    // strategic loop legible: only the selected city gathers materials or
    // manufactures, then the completed engine moves to the frontline.
    private static CityData? ChooseBestSiegeEquipmentProductionCity(
        WorldState world,
        CityData target,
        int factionId,
        ConstructionProjectType projectType)
    {
        if (projectType == ConstructionProjectType.None)
        {
            return null;
        }

        return world.Cities
            .Where(city => city.OwnerFactionId == factionId &&
                           city.SiegeWorkshopLevel > 0 &&
                           !IsFrontlineCity(world, city) &&
                           city.ConnectedCityIds.Contains(target.Id))
            .OrderByDescending(city => ScoreSiegeEquipmentProductionCity(world, city, projectType))
            .ThenBy(city => city.Id)
            .FirstOrDefault();
    }

    private static int ScoreSiegeEquipmentProductionCity(
        WorldState world,
        CityData city,
        ConstructionProjectType projectType)
    {
        var resourceCost = ConstructionRules.GetResourceCost(projectType);
        var coveredMaterials = System.Math.Min(city.Wood, resourceCost.Wood) +
                               System.Math.Min(city.Metal, resourceCost.Metal) +
                               System.Math.Min(city.Stone, resourceCost.Stone);
        var governorPolitics = GetCityGovernor(world, city)?.Politics ?? 40;
        return (HasReadySiegeEquipment(city, projectType) ? 1000 : 0) +
               (ConstructionRules.CanAffordResourceCost(city, resourceCost) ? 500 : 0) +
               coveredMaterials + city.SiegeWorkshopLevel * 20 + governorPolitics / 3;
    }

    private static int ScoreSiegeEquipmentPlan(
        WorldState world,
        CityData target,
        OfficerData strategist,
        OfficerData? chancellor,
        OfficerData? sourceGovernor)
    {
        var highestEnemyDefense = GetAdjacentEnemyCities(world, target).Select(enemy => enemy.Defense).DefaultIfEmpty().Max();
        // Intelligence determines the quality of the target assessment; politics
        // from the chancellor and local governor changes how willing the AI is to
        // commit a safe city's workshop to the plan.
        return 50 + strategist.Intelligence + highestEnemyDefense / 2 +
               (chancellor?.Politics ?? 40) / 3 + (sourceGovernor?.Politics ?? 40) / 4 -
               System.Math.Max(0, target.Defense - 70) / 2;
    }

    private static bool CanUpgrade(CityData city, ConstructionProjectType projectType) =>
        !ConstructionRules.IsAtMaximumLevel(city, projectType) &&
        ConstructionRules.CanAffordResourceCost(city, ConstructionRules.GetResourceCost(projectType));

    private static bool IsRawMaterialPressureAtLeast(CityData city, int numerator, int denominator) =>
        MarketRules.GetAmount(city, MarketProductType.Wood) * denominator >= MarketRules.GetCapacity(city, MarketProductType.Wood) * numerator ||
        MarketRules.GetAmount(city, MarketProductType.Metal) * denominator >= MarketRules.GetCapacity(city, MarketProductType.Metal) * numerator ||
        MarketRules.GetAmount(city, MarketProductType.Stone) * denominator >= MarketRules.GetCapacity(city, MarketProductType.Stone) * numerator;

    private static bool ShouldExpandResourceDepot(CityData city)
    {
        if (ConstructionRules.IsAtMaximumLevel(city, ConstructionProjectType.ResourceDepot) ||
            !ConstructionRules.CanAffordResourceCost(city, ConstructionRules.GetResourceCost(ConstructionProjectType.ResourceDepot)))
        {
            return false;
        }

        return IsRawMaterialPressureAtLeast(city, 3, 4);
    }

    private static bool ShouldExpandGranary(CityData city)
    {
        return !ConstructionRules.IsAtMaximumLevel(city, ConstructionProjectType.Granary) &&
               ConstructionRules.CanAffordResourceCost(city, ConstructionRules.GetResourceCost(ConstructionProjectType.Granary)) &&
               MarketRules.GetAmount(city, MarketProductType.Food) * 4 >= MarketRules.GetCapacity(city, MarketProductType.Food) * 3;
    }

    private static bool ShouldExpandHorseStable(CityData city)
    {
        return !ConstructionRules.IsAtMaximumLevel(city, ConstructionProjectType.HorseStable) &&
               ConstructionRules.CanAffordResourceCost(city, ConstructionRules.GetResourceCost(ConstructionProjectType.HorseStable)) &&
               MarketRules.GetAmount(city, MarketProductType.Horse) * 4 >= MarketRules.GetCapacity(city, MarketProductType.Horse) * 3;
    }

    private static bool ShouldUpgradeSiegeWorkshop(WorldState world, CityData city)
    {
        return IsFrontlineCity(world, city) &&
               !ConstructionRules.IsAtMaximumLevel(city, ConstructionProjectType.SiegeWorkshop) &&
               ConstructionRules.CanAffordResourceCost(city, ConstructionRules.GetResourceCost(ConstructionProjectType.SiegeWorkshop));
    }

    internal static bool IsFrontlineCity(WorldState world, CityData city)
    {
        return GetAdjacentEnemyCities(world, city).Count > 0;
    }

    private static System.Collections.Generic.List<CityData> GetAdjacentEnemyCities(WorldState world, CityData city)
    {
        return city.ConnectedCityIds
            .Select(world.GetCity)
            .Where(connectedCity => connectedCity != null && connectedCity.OwnerFactionId > 0 && connectedCity.OwnerFactionId != city.OwnerFactionId)
            .Cast<CityData>()
            .ToList();
    }
}
