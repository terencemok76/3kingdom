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
    public int LastUpdatedYear { get; set; }
    public int LastUpdatedMonth { get; set; }
}
