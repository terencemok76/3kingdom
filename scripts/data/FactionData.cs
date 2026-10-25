using System.Collections.Generic;

namespace ThreeKingdom.Data;

public class FactionData
{
    public int Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameZhHant { get; set; } = string.Empty;
    public string MapColorHex { get; set; } = string.Empty;
    public int RulerOfficerId { get; set; }
    public int ChancellorOfficerId { get; set; }
    public int ChiefStrategistOfficerId { get; set; }
    // A faction that accepted a surrender proposal has already transferred its
    // cities and officers.  Keep its successor so the HUD can report a
    // peaceful absorption instead of the generic "cities became neutral"
    // defeat outcome.
    public int SurrenderedToFactionId { get; set; }
    // AI voluntary ruler changes are intentionally rate-limited to prevent
    // leadership churn as officer scores fluctuate between monthly decisions.
    public int LastRulerChangeYear { get; set; }
    public int LastRulerChangeMonth { get; set; }
    public List<int> OfficerIds { get; set; } = new();
    public bool IsPlayer { get; set; }
}
