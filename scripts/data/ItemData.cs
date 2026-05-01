namespace ThreeKingdom.Data;

public enum ItemType
{
    Weapon,
    Horse,
    Book,
    Treasure
}

public class ItemData
{
    public int Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameZhHant { get; set; } = string.Empty;
    public ItemType ItemType { get; set; }
    public int StrengthBonus { get; set; }
    public int IntelligenceBonus { get; set; }
    public int CharmBonus { get; set; }
    public int LeadershipBonus { get; set; }
    public int PoliticsBonus { get; set; }
    public int CombatBonus { get; set; }
    public int LoyaltyBonus { get; set; }
    public int OwnerFactionId { get; set; }
    public int OwnerCityId { get; set; }
    public int EquippedOfficerId { get; set; }
    public string Rarity { get; set; } = "Common";
}
