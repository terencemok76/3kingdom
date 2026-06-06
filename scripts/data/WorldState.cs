using System.Collections.Generic;
using System.Linq;

namespace ThreeKingdom.Data;

public class WorldState
{
    public class PendingSuccessionData
    {
        public int FactionId { get; set; }
        public List<int> CandidateOfficerIds { get; set; } = new();
    }

    public class CityIntelData
    {
        public int ViewerFactionId { get; set; }
        public int TargetCityId { get; set; }
        public int RemainingMonths { get; set; }
    }

    public class PendingCapturedOfficerData
    {
        public int WinnerFactionId { get; set; }
        public int WinnerCityId { get; set; }
        public int OfficerId { get; set; }
        public bool IsTestOnly { get; set; }
    }

    public string StoryId { get; set; } = string.Empty;
    public string StoryNameEn { get; set; } = string.Empty;
    public string StoryNameZhHant { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public int RandomSeed { get; set; }
    public List<CityData> Cities { get; set; } = new();
    public List<OfficerData> Officers { get; set; } = new();
    public List<ItemData> Items { get; set; } = new();
    public List<FactionData> Factions { get; set; } = new();
    public List<DiplomacyRelationData> DiplomacyRelations { get; set; } = new();
    public List<CityStartData> CityStarts { get; set; } = new();
    public List<FactionStartData> FactionStarts { get; set; } = new();
    public List<PendingCommandData> PendingCommands { get; set; } = new();
    public List<InternalAffairsScheduleData> InternalAffairsSchedules { get; set; } = new();
    public List<CityIntelData> CityIntelRecords { get; set; } = new();
    public List<PendingSuccessionData> PendingSuccessionRecords { get; set; } = new();
    public List<PendingCapturedOfficerData> PendingCapturedOfficerRecords { get; set; } = new();
    public bool ViewAllInformationEnabled { get; set; }

    public CityData? GetCity(int cityId)
    {
        return Cities.FirstOrDefault(c => c.Id == cityId);
    }

    public FactionData? GetFaction(int factionId)
    {
        return Factions.FirstOrDefault(f => f.Id == factionId);
    }

    public OfficerData? GetOfficer(int officerId)
    {
        return Officers.FirstOrDefault(o => o.Id == officerId);
    }

    public ItemData? GetItem(int itemId)
    {
        return Items.FirstOrDefault(item => item.Id == itemId);
    }

    public DiplomacyRelationData? GetDiplomacyRelation(int factionAId, int factionBId)
    {
        var low = factionAId < factionBId ? factionAId : factionBId;
        var high = factionAId < factionBId ? factionBId : factionAId;
        return DiplomacyRelations.FirstOrDefault(relation =>
            relation.FactionAId == low &&
            relation.FactionBId == high);
    }

    public CityIntelData? GetCityIntel(int viewerFactionId, int cityId)
    {
        return CityIntelRecords.FirstOrDefault(record =>
            record.ViewerFactionId == viewerFactionId &&
            record.TargetCityId == cityId &&
            record.RemainingMonths > 0);
    }

    public PendingSuccessionData? GetPendingSuccession(int factionId)
    {
        return PendingSuccessionRecords.FirstOrDefault(record => record.FactionId == factionId);
    }

    public List<PendingCapturedOfficerData> GetPendingCapturedOfficers(int factionId)
    {
        return PendingCapturedOfficerRecords
            .Where(record => record.WinnerFactionId == factionId)
            .ToList();
    }

    public PendingCapturedOfficerData? GetNextPendingCapturedOfficer(int factionId)
    {
        return PendingCapturedOfficerRecords.FirstOrDefault(record => record.WinnerFactionId == factionId);
    }

    public bool HasActiveCityIntel(int viewerFactionId, int cityId)
    {
        return GetCityIntel(viewerFactionId, cityId) != null;
    }

    public bool CanFactionViewCity(int viewerFactionId, int cityId)
    {
        if (viewerFactionId <= 0 || cityId <= 0)
        {
            return false;
        }

        var city = GetCity(cityId);
        if (city == null)
        {
            return false;
        }

        return city.OwnerFactionId == viewerFactionId ||
               HasActiveCityIntel(viewerFactionId, cityId);
    }

    public void UpsertCityIntel(int viewerFactionId, int cityId, int durationMonths)
    {
        if (viewerFactionId <= 0 || cityId <= 0 || durationMonths <= 0)
        {
            return;
        }

        var existing = GetCityIntel(viewerFactionId, cityId);
        if (existing != null)
        {
            existing.RemainingMonths = existing.RemainingMonths < durationMonths
                ? durationMonths
                : existing.RemainingMonths;
            return;
        }

        CityIntelRecords.Add(new CityIntelData
        {
            ViewerFactionId = viewerFactionId,
            TargetCityId = cityId,
            RemainingMonths = durationMonths
        });
    }
}
