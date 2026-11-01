using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class MainCityRules
{
    public static CityData? FindMainCity(WorldState world, int factionId)
    {
        if (factionId <= 0) return null;
        var ruler = world.GetOfficer(world.GetFaction(factionId)?.RulerOfficerId ?? 0);
        var city = ruler == null ? null : world.GetCity(ruler.CityId);
        if (ruler != null && ruler.CaptiveFactionId <= 0 &&
            (ruler.DeathYear <= 0 || world.Year <= ruler.DeathYear) &&
            !OfficerAvailabilityRules.IsTravelingWithLogistics(world, ruler.Id) &&
            !BattleCampaignService.IsOfficerCommitted(world, ruler.Id) &&
            city?.OwnerFactionId == factionId && city.OfficerIds.Contains(ruler.Id))
            return city;
        return world.Cities.Where(candidate => candidate.OwnerFactionId == factionId).OrderBy(candidate => candidate.Id).FirstOrDefault();
    }
}
