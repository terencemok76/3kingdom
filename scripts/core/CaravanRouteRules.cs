using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

/// <summary>
/// Finds the shortest route that stays inside one faction's connected cities.
/// A route contains both endpoints, so its travel time is route.Count - 1 months.
/// </summary>
public static class CaravanRouteRules
{
    public static List<int> FindFriendlyRoute(WorldState world, int sourceCityId, int targetCityId, int factionId)
    {
        if (sourceCityId == targetCityId || world.GetCity(sourceCityId)?.OwnerFactionId != factionId || world.GetCity(targetCityId)?.OwnerFactionId != factionId)
        {
            return new List<int>();
        }

        var queue = new Queue<int>();
        var previous = new Dictionary<int, int> { [sourceCityId] = 0 };
        queue.Enqueue(sourceCityId);
        while (queue.Count > 0)
        {
            var cityId = queue.Dequeue();
            var city = world.GetCity(cityId);
            if (city == null)
            {
                continue;
            }

            foreach (var neighbourId in city.ConnectedCityIds.OrderBy(id => id))
            {
                if (previous.ContainsKey(neighbourId) || world.GetCity(neighbourId)?.OwnerFactionId != factionId)
                {
                    continue;
                }

                previous[neighbourId] = cityId;
                if (neighbourId == targetCityId)
                {
                    return BuildRoute(previous, targetCityId);
                }

                queue.Enqueue(neighbourId);
            }
        }

        return new List<int>();
    }

    public static List<int> FindReachableFriendlyCityIds(WorldState world, int sourceCityId, int factionId) =>
        world.Cities
            .Where(city => city.Id != sourceCityId && city.OwnerFactionId == factionId && FindFriendlyRoute(world, sourceCityId, city.Id, factionId).Count > 0)
            .OrderBy(city => city.Id)
            .Select(city => city.Id)
            .ToList();

    private static List<int> BuildRoute(IReadOnlyDictionary<int, int> previous, int targetCityId)
    {
        var route = new List<int>();
        for (var cityId = targetCityId; cityId != 0; cityId = previous[cityId])
        {
            route.Add(cityId);
        }

        route.Reverse();
        return route;
    }
}
