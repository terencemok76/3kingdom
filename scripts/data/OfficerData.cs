using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ThreeKingdom.Data;

public class OfficerData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("name_cn")]
    public string NameZhHant { get; set; } = string.Empty;
    public string Role { get; set; } = "General";
    public string Belongs { get; set; } = string.Empty;
    public string Sex { get; set; } = string.Empty;
    public int Age { get; set; }
    public int Strength { get; set; }
    public int Intelligence { get; set; }
    public int Charm { get; set; }
    public int Leadership { get; set; }
    public int Politics { get; set; }
    public int Loyalty { get; set; }
    public int Ambition { get; set; }
    public int Combat { get; set; }
    [JsonPropertyName("relationship_type")]
    public Dictionary<string, string> RelationshipType { get; set; } = new();
    public int CityId { get; set; }
}
