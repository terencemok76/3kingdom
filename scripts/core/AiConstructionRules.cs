using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class AiConstructionRules
{
    internal static ConstructionProjectType ChooseConstructionProjectType(WorldState world, CityData city)
    {
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

        if (!IsFrontlineCity(world, city))
        {
            return city.HorsePastureLevel < 2
                ? ConstructionProjectType.HorsePasture
                : ConstructionProjectType.BowWorkshop;
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

    private static bool ShouldExpandResourceDepot(CityData city)
    {
        if (city.ResourceDepotLevel >= 2 ||
            !ConstructionRules.CanAffordResourceCost(city, ConstructionRules.GetResourceCost(ConstructionProjectType.ResourceDepot)))
        {
            return false;
        }

        return MarketRules.GetAmount(city, MarketProductType.Wood) * 4 >= MarketRules.GetCapacity(city, MarketProductType.Wood) * 3 ||
               MarketRules.GetAmount(city, MarketProductType.Metal) * 4 >= MarketRules.GetCapacity(city, MarketProductType.Metal) * 3 ||
               MarketRules.GetAmount(city, MarketProductType.Stone) * 4 >= MarketRules.GetCapacity(city, MarketProductType.Stone) * 3;
    }

    private static bool ShouldExpandGranary(CityData city)
    {
        return city.GranaryLevel < 2 &&
               ConstructionRules.CanAffordResourceCost(city, ConstructionRules.GetResourceCost(ConstructionProjectType.Granary)) &&
               MarketRules.GetAmount(city, MarketProductType.Food) * 4 >= MarketRules.GetCapacity(city, MarketProductType.Food) * 3;
    }

    private static bool ShouldExpandHorseStable(CityData city)
    {
        return city.HorseStableLevel < 2 &&
               ConstructionRules.CanAffordResourceCost(city, ConstructionRules.GetResourceCost(ConstructionProjectType.HorseStable)) &&
               MarketRules.GetAmount(city, MarketProductType.Horse) * 4 >= MarketRules.GetCapacity(city, MarketProductType.Horse) * 3;
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
