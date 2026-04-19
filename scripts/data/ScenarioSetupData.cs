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
    public int Troops { get; set; }
    public List<int> OfficerIds { get; set; } = new();
}

public class FactionStartData
{
    public int FactionId { get; set; }
    public List<int> CityIds { get; set; } = new();
    public List<int> OfficerIds { get; set; } = new();
}
