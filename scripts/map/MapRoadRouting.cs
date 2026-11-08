using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

/// <summary>Visual-only paths over the road segments drawn on the strategic map.</summary>
public static class MapRoadRouting
{
    public static List<int> FindRoadRoute(WorldState world, int sourceCityId, int targetCityId)
    {
        if (world.GetCity(sourceCityId) == null || world.GetCity(targetCityId) == null) return new List<int>();
        if (sourceCityId == targetCityId) return new List<int> { sourceCityId };

        var neighbors = world.Cities.ToDictionary(city => city.Id, _ => new SortedSet<int>());
        // RouteRenderer.Bind draws an edge only from its lower-ID city.
        foreach (var city in world.Cities)
            foreach (var neighborId in city.ConnectedCityIds)
                if (city.Id < neighborId && neighbors.ContainsKey(neighborId))
                {
                    neighbors[city.Id].Add(neighborId);
                    neighbors[neighborId].Add(city.Id);
                }

        var previous = new Dictionary<int, int> { [sourceCityId] = 0 };
        var queue = new Queue<int>();
        queue.Enqueue(sourceCityId);
        while (queue.Count > 0)
        {
            var cityId = queue.Dequeue();
            foreach (var neighborId in neighbors[cityId])
            {
                if (previous.ContainsKey(neighborId)) continue;
                previous[neighborId] = cityId;
                if (neighborId == targetCityId)
                {
                    var route = new List<int>();
                    for (var current = targetCityId; current != 0; current = previous[current]) route.Add(current);
                    route.Reverse();
                    return route;
                }
                queue.Enqueue(neighborId);
            }
        }
        return new List<int>();
    }

    public static Vector2[] BuildRoadPoints(WorldState world, IReadOnlyList<int> route)
    {
        var points = new List<Vector2>();
        for (var index = 1; index < route.Count; index++)
        {
            var from = world.GetCity(route[index - 1]);
            var to = world.GetCity(route[index]);
            if (from == null || to == null) return System.Array.Empty<Vector2>();
            var segment = RouteRenderer.GetRoutePoints(from.Id, to.Id, new Vector2(from.MapX, from.MapY), new Vector2(to.MapX, to.MapY));
            if (segment.Length < 2) return System.Array.Empty<Vector2>();
            points.AddRange(index == 1 ? segment : segment.Skip(1));
        }
        return points.ToArray();
    }
}
