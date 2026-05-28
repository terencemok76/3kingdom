using System.Collections.Generic;

namespace ThreeKingdom.Data;

public class ScenarioSetupData
{
    public List<CityStartData> CityStarts { get; set; } = new();
    public List<FactionStartData> FactionStarts { get; set; } = new();
}

public class CityStartData
{
    public int CityId { get; set; }
    public int OwnerFactionId { get; set; }
    public int Gold { get; set; }
    public int Food { get; set; }
    public int Horses { get; set; }
    public int Population { get; set; }
    public int Troops { get; set; }
    public int InfantryTroops { get; set; }
    public int SpearmanTroops { get; set; }
    public int CavalryTroops { get; set; }
    public int ArcherTroops { get; set; }
    public int CrossbowTroops { get; set; }
    public int SiegeTroops { get; set; }
    public int BowWorkshopLevel { get; set; }
    public int BowWorkshopProgress { get; set; }
    public int SiegeWorkshopLevel { get; set; }
    public int SiegeWorkshopProgress { get; set; }
    public int HorsePastureLevel { get; set; }
    public int HorsePastureProgress { get; set; }
    public bool HasBowWorkshop { get; set; }
    public bool HasSiegeWorkshop { get; set; }
    public List<int> OfficerIds { get; set; } = new();
}

public class FactionStartData
{
    public int FactionId { get; set; }
    public List<int> CityIds { get; set; } = new();
    public List<int> OfficerIds { get; set; } = new();
}
