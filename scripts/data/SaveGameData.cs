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
}
