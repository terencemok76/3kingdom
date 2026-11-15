using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class OfficerSuccessionAndPrefectRules
{
    public static List<int> GetSuccessionCandidates(WorldState world, FactionData faction, OfficerData? previousRuler)
    {
        return faction.OfficerIds.Select(world.GetOfficer)
            .Where(officer => officer != null && IsOfficerAlive(world, officer) && officer.CaptiveFactionId <= 0 &&
                              !BattleCampaignService.IsOfficerCommitted(world, officer.Id))
            .OrderByDescending(officer => OfficerRelationshipRules.GetSuccessionRelationshipPriority(previousRuler, officer))
            .ThenByDescending(officer => officer!.Leadership + officer.Intelligence + officer.Politics + officer.Charm)
            .ThenByDescending(officer => officer!.Loyalty)
            .Select(officer => officer!.Id).ToList();
    }

    public static bool IsOfficerAlive(WorldState world, OfficerData? officer) =>
        officer != null && (officer.DeathYear <= 0 || world.Year <= officer.DeathYear);

    public static bool IsEligibleAutoPrefectCandidate(WorldState world, CityData city, FactionData faction, OfficerData officer) =>
        officer.CityId == city.Id && city.OfficerIds.Contains(officer.Id) && faction.OfficerIds.Contains(officer.Id) &&
        !world.Factions.Any(item => item.RulerOfficerId == officer.Id) && IsOfficerAlive(world, officer);

    public static OfficerData? GetBestAutoPrefectCandidate(WorldState world, CityData city, FactionData faction) =>
        city.OfficerIds.Select(world.GetOfficer)
            .Where(officer => officer != null && IsEligibleAutoPrefectCandidate(world, city, faction, officer))
            .OrderByDescending(officer => ScoreAutoPrefectCandidate(officer!))
            .ThenByDescending(officer => officer!.Loyalty)
            .ThenBy(officer => officer!.Id).FirstOrDefault();

    private static int ScoreAutoPrefectCandidate(OfficerData officer) =>
        officer.Politics * 3 + officer.Intelligence * 2 + officer.Charm + officer.Leadership +
        officer.FarmRank * 15 + officer.CommercialRank * 15 + officer.DefendRank * 10 +
        officer.DisasterPreventionRank * 10 + officer.ConstructionRank * 10;
}
