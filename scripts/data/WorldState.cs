using System.Collections.Generic;
using System.Linq;

namespace ThreeKingdom.Data;

public class WorldState
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int RandomSeed { get; set; }
    public List<CityData> Cities { get; set; } = new();
    public List<OfficerData> Officers { get; set; } = new();
    public List<FactionData> Factions { get; set; } = new();
    public List<PendingCommandData> PendingCommands { get; set; } = new();

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
}
