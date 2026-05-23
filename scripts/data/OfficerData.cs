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
    [JsonPropertyName("birth_year")]
    public int BirthYear { get; set; }
    [JsonPropertyName("death_year")]
    public int DeathYear { get; set; }
    public int Strength { get; set; }
    public int Intelligence { get; set; }
    public int Charm { get; set; }
    public int Leadership { get; set; }
    public int Politics { get; set; }
    public int Loyalty { get; set; }
    public int Ambition { get; set; }
    public int Combat { get; set; }
    public int FarmExperience { get; set; }
    public int FarmRank { get; set; }
    public string FarmTitle { get; set; } = string.Empty;
    public int CommercialExperience { get; set; }
    public int CommercialRank { get; set; }
    public string CommercialTitle { get; set; } = string.Empty;
    public int DefendExperience { get; set; }
    public int DefendRank { get; set; }
    public string DefendTitle { get; set; } = string.Empty;
    public int DisasterPreventionExperience { get; set; }
    public int DisasterPreventionRank { get; set; }
    public string DisasterPreventionTitle { get; set; } = string.Empty;
    public int ConstructionExperience { get; set; }
    public int ConstructionRank { get; set; }
    public string ConstructionTitle { get; set; } = string.Empty;
    public int BattleExperience { get; set; }
    public int MilitaryRank { get; set; }
    public string GeneralTitle { get; set; } = string.Empty;
    public int StrategistExperience { get; set; }
    public int StrategistRank { get; set; }
    public string StrategistTitle { get; set; } = string.Empty;
    public int SpyExperience { get; set; }
    public int SpyRank { get; set; }
    public string SpyTitle { get; set; } = string.Empty;
    public int DiplomacyExperience { get; set; }
    public int DiplomacyRank { get; set; }
    public string DiplomacyTitle { get; set; } = string.Empty;
    public int CivilExperience { get; set; }
    public int CivilRank { get; set; }
    public string CivilTitle { get; set; } = string.Empty;
    public List<string> Appointments { get; set; } = new();
    [JsonPropertyName("relationship_type")]
    public Dictionary<string, string> RelationshipType { get; set; } = new();
    public int CityId { get; set; }
    [JsonIgnore]
    public int FreeOfficerStayMonths { get; set; }
    public int LastAssignedYear { get; set; } = -1;
    public int LastAssignedMonth { get; set; } = -1;
    public CommandType LastAssignedCommand { get; set; } = CommandType.Pass;
}
