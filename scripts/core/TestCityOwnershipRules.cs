using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class TestCityOwnershipRules
{
    public static bool TrySwitchToNextActiveFaction(WorldState world, CityData city, out int previousFactionId, out int nextFactionId)
    {
        previousFactionId = city.OwnerFactionId;
        var factionIds = world.Factions
            .Where(faction => faction.Id > 0 && world.Cities.Any(mapCity => mapCity.OwnerFactionId == faction.Id))
            .OrderBy(faction => faction.Id)
            .Select(faction => faction.Id)
            .ToList();
        if (factionIds.Count < 2)
        {
            nextFactionId = previousFactionId;
            return false;
        }

        var currentIndex = factionIds.IndexOf(previousFactionId);
        nextFactionId = factionIds[(currentIndex >= 0 ? currentIndex + 1 : 0) % factionIds.Count];
        if (nextFactionId == previousFactionId)
        {
            return false;
        }

        world.InternalAffairsSchedules.RemoveAll(schedule => schedule.CityId == city.Id);
        city.OwnerFactionId = nextFactionId;
        return true;
    }
}
