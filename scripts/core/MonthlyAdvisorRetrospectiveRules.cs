using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public enum MonthlyAdvisorRetrospectiveFocus
{
    Stable,
    ResourceOpportunity,
    FoodPressure,
    CityEvent,
    StorageLoss
}

public sealed class MonthlyAdvisorRetrospectiveSummary
{
    public int CityId { get; init; }
    public MonthlyAdvisorRetrospectiveFocus Focus { get; init; }
}

public enum MonthlyAdvisorSpeakerRole
{
    None,
    Officer,
    Strategist,
    ChiefStrategist
}

public sealed class MonthlyAdvisorSpeakerSelection
{
    public int OfficerId { get; init; }
    public MonthlyAdvisorSpeakerRole Role { get; init; }
}

public static class MonthlyAdvisorSpeakerRules
{
    public static MonthlyAdvisorSpeakerSelection Select(WorldState world, int factionId)
    {
        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return new MonthlyAdvisorSpeakerSelection { Role = MonthlyAdvisorSpeakerRole.None };
        }

        var chief = world.GetOfficer(faction.ChiefStrategistOfficerId);
        if (IsAvailable(chief, world))
        {
            return Create(chief!, MonthlyAdvisorSpeakerRole.ChiefStrategist);
        }

        var officers = faction.OfficerIds.Select(world.GetOfficer).Where(officer => IsAvailable(officer, world)).Cast<OfficerData>();
        var strategist = officers
            .Where(officer => OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Strategist))
            .OrderByDescending(officer => officer.Intelligence)
            .ThenByDescending(officer => officer.Leadership)
            .FirstOrDefault();
        if (strategist != null)
        {
            return Create(strategist, MonthlyAdvisorSpeakerRole.Strategist);
        }

        var substitute = officers
            .Where(officer => officer.Id != faction.RulerOfficerId)
            .OrderByDescending(officer => officer.Intelligence)
            .ThenByDescending(officer => officer.Politics)
            .ThenByDescending(officer => officer.Leadership)
            .FirstOrDefault();
        return substitute != null
            ? Create(substitute, MonthlyAdvisorSpeakerRole.Officer)
            : new MonthlyAdvisorSpeakerSelection { Role = MonthlyAdvisorSpeakerRole.None };
    }

    private static MonthlyAdvisorSpeakerSelection Create(OfficerData officer, MonthlyAdvisorSpeakerRole role) => new()
    {
        OfficerId = officer.Id,
        Role = role
    };

    private static bool IsAvailable(OfficerData? officer, WorldState world) =>
        officer != null && officer.CaptiveFactionId <= 0 && (officer.DeathYear <= 0 || world.Year <= officer.DeathYear);
}

/// <summary>Selects one player-facing causal priority from a completed monthly result.</summary>
public static class MonthlyAdvisorRetrospectiveRules
{
    public static MonthlyAdvisorRetrospectiveSummary? SelectPriority(MonthlyEconomyResult result)
    {
        var reports = result.PlayerCityEconomyReports;
        if (reports.Count == 0)
        {
            return null;
        }

        var storageLoss = reports.Where(report => report.StorageLoss.HasLoss)
            .OrderByDescending(report => TotalStorageLoss(report.StorageLoss)).ThenBy(report => report.CityId).FirstOrDefault();
        if (storageLoss != null) return Create(storageLoss, MonthlyAdvisorRetrospectiveFocus.StorageLoss);

        var eventCityIds = result.PlayerCityEvents.Where(HasNegativeImpact).Select(item => item.CityId).ToHashSet();
        var eventReport = reports.Where(report => eventCityIds.Contains(report.CityId)).OrderBy(report => report.CityId).FirstOrDefault();
        if (eventReport != null) return Create(eventReport, MonthlyAdvisorRetrospectiveFocus.CityEvent);

        var foodPressure = reports.Where(report => report.FoodCapacity > 0 &&
                (report.FoodAmount * 4 < report.FoodCapacity || report.FoodDelta < 0 && report.FoodUpkeep > 0))
            .OrderBy(report => (double)report.FoodAmount / report.FoodCapacity).ThenBy(report => report.CityId).FirstOrDefault();
        if (foodPressure != null) return Create(foodPressure, MonthlyAdvisorRetrospectiveFocus.FoodPressure);

        var resourceOpportunity = reports.Where(report => report.WoodDelta > 0 || report.MetalDelta > 0 || report.StoneDelta > 0)
            .OrderByDescending(LargestRawMaterialGain).ThenBy(report => report.CityId).FirstOrDefault();
        return resourceOpportunity != null
            ? Create(resourceOpportunity, MonthlyAdvisorRetrospectiveFocus.ResourceOpportunity)
            : Create(reports.OrderBy(report => report.CityId).First(), MonthlyAdvisorRetrospectiveFocus.Stable);
    }

    private static MonthlyAdvisorRetrospectiveSummary Create(MonthlyCityEconomyReport report, MonthlyAdvisorRetrospectiveFocus focus) => new()
    {
        CityId = report.CityId,
        Focus = focus
    };

    private static int TotalStorageLoss(MarketStorageLoss loss) => loss.Food + loss.Horse + loss.Wood + loss.Metal + loss.Stone;
    private static bool HasNegativeImpact(MonthlyCityEvent item) => item.GoldDelta < 0 || item.FoodDelta < 0 || item.LoyaltyDelta < 0 ||
        item.FarmDelta < 0 || item.DefenseDelta < 0 || item.PopulationDelta < 0 || item.TroopDelta < 0;
    private static int LargestRawMaterialGain(MonthlyCityEconomyReport report) => System.Math.Max(report.WoodDelta, System.Math.Max(report.MetalDelta, report.StoneDelta));
}
