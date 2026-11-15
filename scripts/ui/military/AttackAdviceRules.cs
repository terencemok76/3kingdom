using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal enum AttackAdviceKind { Prepare, BreakAlliance, BreakTruce, LowFood, NoIntel, Unfavorable, Cautious, Favorable }

internal static class AttackAdviceRules
{
    public static OfficerData? SelectAdvisor(WorldState world, CityData sourceCity)
    {
        var faction = world.GetFaction(sourceCity.OwnerFactionId);
        var chief = faction != null ? world.GetOfficer(faction.ChiefStrategistOfficerId) : null;
        if (IsAvailable(chief, world)) return chief;
        return sourceCity.OfficerIds.Select(world.GetOfficer)
            .Where(officer => IsAvailable(officer, world)).Cast<OfficerData>()
            .OrderByDescending(officer => officer.Intelligence)
            .ThenByDescending(officer => officer.Leadership).ThenBy(officer => officer.Id).FirstOrDefault();
    }

    private static bool IsAvailable(OfficerData? officer, WorldState world) =>
        officer != null && officer.CaptiveFactionId <= 0 && (officer.DeathYear <= 0 || world.Year <= officer.DeathYear);

    public static AttackAdviceKind Evaluate(WorldState world, CityData source, CityData target, int troops, int food)
    {
        if (troops <= 0) return AttackAdviceKind.Prepare;
        var relation = source.OwnerFactionId == target.OwnerFactionId ? null : world.GetDiplomacyRelation(source.OwnerFactionId, target.OwnerFactionId);
        if (relation != null && relation.RemainingMonths > 0)
        {
            if (relation.Status == DiplomacyStatusType.Alliance) return AttackAdviceKind.BreakAlliance;
            if (relation.Status == DiplomacyStatusType.Truce) return AttackAdviceKind.BreakTruce;
        }
        if (food <= 0) return AttackAdviceKind.LowFood;
        if (!world.CanFactionViewCity(source.OwnerFactionId, target.Id)) return AttackAdviceKind.NoIntel;
        if (troops < target.Troops) return AttackAdviceKind.Unfavorable;
        if (troops < target.Troops + target.Defense * 5) return AttackAdviceKind.Cautious;
        return AttackAdviceKind.Favorable;
    }
}
