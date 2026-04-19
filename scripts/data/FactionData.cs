using System.Collections.Generic;

namespace ThreeKingdom.Data;

public class FactionData
{
    public int Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameZhHant { get; set; } = string.Empty;
    public int RulerOfficerId { get; set; }
    public List<int> OfficerIds { get; set; } = new();
    public bool IsPlayer { get; set; }
}
