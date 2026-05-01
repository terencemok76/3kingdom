using System.Collections.Generic;

namespace ThreeKingdom.Data;

public enum CommandType
{
    InternalAffairs,
    Develop,
    Recruit,
    Move,
    Search,
    CivilRelief,
    Merchant,
    Attack,
    Pass
}

public enum TroopType
{
    Infantry,
    Spearman,
    Cavalry,
    Archer,
    Crossbow,
    Siege
}

public enum MerchantTradeMode
{
    BuyFood,
    SellFood,
    BuyHorse
}

public class TroopAllocationData
{
    public int Infantry { get; set; }
    public int Spearman { get; set; }
    public int Cavalry { get; set; }
    public int Archer { get; set; }
    public int Crossbow { get; set; }
    public int Siege { get; set; }

    public int Total => Infantry + Spearman + Cavalry + Archer + Crossbow + Siege;
}

public class CommandRequest
{
    public CommandType Type { get; set; }
    public int ActorFactionId { get; set; }
    public int SourceCityId { get; set; }
    public int? TargetCityId { get; set; }
    public int TroopsToSend { get; set; }
    public int GoldToSend { get; set; }
    public int FoodToSend { get; set; }
    public int HorsesToSend { get; set; }
    public bool SellFood { get; set; }
    public TroopType RecruitTroopType { get; set; } = TroopType.Infantry;
    public MerchantTradeMode MerchantTradeMode { get; set; } = MerchantTradeMode.BuyFood;
    public TroopAllocationData TroopAllocation { get; set; } = new();
    public List<int> OfficerIds { get; set; } = new();
}

public class PendingCommandData
{
    public CommandType Type { get; set; }
    public int ActorFactionId { get; set; }
    public int SourceCityId { get; set; }
    public int TargetCityId { get; set; }
    public int TroopsToSend { get; set; }
    public int GoldToSend { get; set; }
    public int FoodToSend { get; set; }
    public int HorsesToSend { get; set; }
    public TroopType RecruitTroopType { get; set; } = TroopType.Infantry;
    public TroopAllocationData TroopAllocation { get; set; } = new();
    public List<int> OfficerIds { get; set; } = new();
}

public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string MessageZhHant { get; set; } = string.Empty;
    public string MessageEn { get; set; } = string.Empty;
}
