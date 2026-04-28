using System.Collections.Generic;

namespace ThreeKingdom.Data;

public enum CommandType
{
    InternalAffairs,
    Develop,
    Recruit,
    Move,
    Search,
    Merchant,
    Attack,
    Pass
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
    public bool SellFood { get; set; }
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
    public List<int> OfficerIds { get; set; } = new();
}

public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string MessageZhHant { get; set; } = string.Empty;
    public string MessageEn { get; set; } = string.Empty;
}
