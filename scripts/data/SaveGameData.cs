namespace ThreeKingdom.Data;

public class SaveGameData
{
    public int SlotIndex { get; set; }
    public string Description { get; set; } = string.Empty;
    public string SavedAtUtc { get; set; } = string.Empty;
    public WorldState World { get; set; } = new();
}

public class SaveSlotSummary
{
    public int SlotIndex { get; set; }
    public bool Exists { get; set; }
    public string Description { get; set; } = string.Empty;
    public string SavedAtUtc { get; set; } = string.Empty;
    public string StoryNameEn { get; set; } = string.Empty;
    public string StoryNameZhHant { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IsCampaignBattleSave { get; set; }
    public string BattleAttackerNameEn { get; set; } = string.Empty;
    public string BattleAttackerNameZhHant { get; set; } = string.Empty;
    public string BattleDefenderNameEn { get; set; } = string.Empty;
    public string BattleDefenderNameZhHant { get; set; } = string.Empty;
    public string BattleLocationNameEn { get; set; } = string.Empty;
    public string BattleLocationNameZhHant { get; set; } = string.Empty;
    public string PlayerRulerNameEn { get; set; } = string.Empty;
    public string PlayerRulerNameZhHant { get; set; } = string.Empty;
}
