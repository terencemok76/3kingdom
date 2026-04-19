using System.Collections.Generic;

namespace ThreeKingdom.Data;

public class CityData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameZhHant { get; set; } = string.Empty;
    public int OwnerFactionId { get; set; }
    public int Gold { get; set; }
    public int Food { get; set; }
    public int Troops { get; set; }

    // Phase 1 city development attributes (kept with requested naming)
    public int Farm { get; set; }
    public int Commercial { get; set; }
    public int Defense { get; set; }
    public int Loyalty { get; set; }
    public int LastCoreActionYear { get; set; } = -1;
    public int LastCoreActionMonth { get; set; } = -1;
    public int LastSearchYear { get; set; } = -1;
    public int LastSearchMonth { get; set; } = -1;

    public List<int> OfficerIds { get; set; } = new();
    public List<int> ConnectedCityIds { get; set; } = new();
    public float MapX { get; set; }
    public float MapY { get; set; }
}

