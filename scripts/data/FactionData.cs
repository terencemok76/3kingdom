namespace ThreeKingdom.Data;

public class FactionData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameZhHant { get; set; } = string.Empty;
    public bool IsPlayer { get; set; }
}
