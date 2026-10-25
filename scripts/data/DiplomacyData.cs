namespace ThreeKingdom.Data;

public enum DiplomacyStatusType
{
    Neutral,
    Truce,
    Alliance
}

public class DiplomacyRelationData
{
    public int FactionAId { get; set; }
    public int FactionBId { get; set; }
    public DiplomacyStatusType Status { get; set; } = DiplomacyStatusType.Neutral;
    public int RemainingMonths { get; set; }
    public int RelationScore { get; set; }
    // These scores are directional: A's confidence in B need not match B's confidence in A.
    public int TrustFromAToB { get; set; } = 50;
    public int TrustFromBToA { get; set; } = 50;
    public int ThreatFromAToB { get; set; }
    public int ThreatFromBToA { get; set; }
    public int LastUpdatedYear { get; set; }
    public int LastUpdatedMonth { get; set; }
}
