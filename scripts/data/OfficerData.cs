using System.Collections.Generic;

namespace ThreeKingdom.Data;

public class OfficerData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "General";
    public int War { get; set; }
    public int Intelligence { get; set; }
    public int Charm { get; set; }
    public int Loyalty { get; set; }
    public int CityId { get; set; }
}
