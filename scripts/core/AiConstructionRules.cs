using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class AiConstructionRules
{
    internal static ConstructionProjectType ChooseConstructionProjectType(WorldState world, CityData city)
    {
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
