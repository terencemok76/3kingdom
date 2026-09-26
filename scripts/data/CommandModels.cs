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
    Diplomacy,
    Spy,
    Attack,
    HireOfficer,
    Pass
}

public enum DiplomacyActionType
{
    Alliance,
    Truce,
    Gift,
    Demand,
    BreakPact
}

public enum SpyActionType
{
    Reconnaissance,
    Sabotage,
    Incite,
    Assassination
}

public enum TroopType
{
    Infantry,
    Spearman,
    Cavalry,
    Archer,
    Crossbow,
    Engineer,
    // Transitional legacy value. New UI and commands never create this type.
    Siege
}

public enum BattleEquipmentType
{
    None = 0,
    SupplyCart = 1,
    Ram = 2,
    Ladder = 3,
    Catapult = 4
}

// Compatibility surface for existing saves and battle snapshots. It remains
// readable while campaign support assets are migrated to BattleEquipmentType.
public enum SiegeEngineType
{
    None = 0,
    Ram = 1,
    Catapult = 2,
    Ladder = 3
}

public class BattleSupportDeploymentData
{
    // Independent engineer unit selected by the player. Equipment crews are
    // calculated separately and draw from the same city pool.
    public int EngineerTeamCount { get; set; }
    public bool SupplyCart { get; set; }
    public bool Ram { get; set; }
    public bool Ladder { get; set; }
    public bool Catapult { get; set; }

    public int EquipmentCount => (SupplyCart ? 1 : 0) + (Ram ? 1 : 0) + (Ladder ? 1 : 0) + (Catapult ? 1 : 0);
    public int TotalEngineerCount => EngineerTeamCount + BattleSupportRules.GetRequiredEngineerCount(this);

    public BattleSupportDeploymentData Clone() => new()
    {
        EngineerTeamCount = EngineerTeamCount,
        SupplyCart = SupplyCart,
        Ram = Ram,
        Ladder = Ladder,
        Catapult = Catapult
    };
}

public static class BattleSupportRules
{
    public const int SupplyCartEngineerRequirement = 20;
    public const int LadderEngineerRequirement = 50;
    public const int RamEngineerRequirement = 30;
    public const int CatapultEngineerRequirement = 50;

    public static int GetRequiredEngineerCount(BattleEquipmentType equipmentType) => equipmentType switch
    {
        BattleEquipmentType.SupplyCart => SupplyCartEngineerRequirement,
        BattleEquipmentType.Ladder => LadderEngineerRequirement,
        BattleEquipmentType.Ram => RamEngineerRequirement,
        BattleEquipmentType.Catapult => CatapultEngineerRequirement,
        _ => 0
    };

    public static int GetRequiredEngineerCount(BattleSupportDeploymentData support) =>
        (support.SupplyCart ? SupplyCartEngineerRequirement : 0) +
        (support.Ladder ? LadderEngineerRequirement : 0) +
        (support.Ram ? RamEngineerRequirement : 0) +
        (support.Catapult ? CatapultEngineerRequirement : 0);

    public static void SetEquipmentSelected(BattleSupportDeploymentData support, BattleEquipmentType equipmentType, bool selected)
    {
        switch (equipmentType)
        {
            case BattleEquipmentType.SupplyCart: support.SupplyCart = selected; break;
            case BattleEquipmentType.Ram: support.Ram = selected; break;
            case BattleEquipmentType.Ladder: support.Ladder = selected; break;
            case BattleEquipmentType.Catapult: support.Catapult = selected; break;
        }
    }
}

public enum MerchantTradeMode
{
    BuyFood,
    SellFood,
    BuyHorse,
    SellHorse,
    BuyMetal,
    SellMetal
}

public enum CapturedOfficerDisposition
{
    Kill,
    Recruit,
    Free,
    Jail
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

public class SiegeEngineAllocationData
{
    public int SupplyCart { get; set; }
    public int Ram { get; set; }
    public int Catapult { get; set; }
    public int Ladder { get; set; }

    public int Total => SupplyCart + Ram + Catapult + Ladder;
}

public class AttackOfficerDeploymentData
{
    public int OfficerId { get; set; }
    public TroopType TroopType { get; set; } = TroopType.Infantry;
    public int TroopCount { get; set; }
    public SiegeEngineType SiegeEngineType { get; set; } = SiegeEngineType.None;
}

public class CommandRequest
{
    public CommandType Type { get; set; }
    public int ActorFactionId { get; set; }
    public int SourceCityId { get; set; }
    public int? TargetCityId { get; set; }
    public int? TargetOfficerId { get; set; }
    public int ItemId { get; set; }
    public int TroopsToSend { get; set; }
    public bool HasTroopAllocation { get; set; }
    public int GoldToSend { get; set; }
    public int FoodToSend { get; set; }
    public int HorsesToSend { get; set; }
    public bool SellFood { get; set; }
    public TroopType RecruitTroopType { get; set; } = TroopType.Infantry;
    public MerchantTradeMode MerchantTradeMode { get; set; } = MerchantTradeMode.BuyFood;
    public DiplomacyActionType DiplomacyActionType { get; set; } = DiplomacyActionType.Alliance;
    public SpyActionType SpyActionType { get; set; } = SpyActionType.Reconnaissance;
    public TroopAllocationData TroopAllocation { get; set; } = new();
    public SiegeEngineAllocationData SiegeEngineAllocation { get; set; } = new();
    public BattleSupportDeploymentData BattleSupport { get; set; } = new();
    public List<AttackOfficerDeploymentData> AttackOfficerDeployments { get; set; } = new();
    public DefenderBattlePlan? DefenderBattlePlanOverride { get; set; }
    public int? TargetFactionId { get; set; }
    public int DurationMonths { get; set; } = 3;
    public List<int> OfficerIds { get; set; } = new();
    public List<int> CaptiveOfficerIds { get; set; } = new();
}

public class PendingCommandData
{
    public CommandType Type { get; set; }
    public int ActorFactionId { get; set; }
    public int SourceCityId { get; set; }
    public int TargetCityId { get; set; }
    public int TargetOfficerId { get; set; }
    public int ItemId { get; set; }
    public int TroopsToSend { get; set; }
    public int GoldToSend { get; set; }
    public int FoodToSend { get; set; }
    public int HorsesToSend { get; set; }
    public TroopType RecruitTroopType { get; set; } = TroopType.Infantry;
    public DiplomacyActionType DiplomacyActionType { get; set; } = DiplomacyActionType.Alliance;
    public SpyActionType SpyActionType { get; set; } = SpyActionType.Reconnaissance;
    public TroopAllocationData TroopAllocation { get; set; } = new();
    public SiegeEngineAllocationData SiegeEngineAllocation { get; set; } = new();
    public BattleSupportDeploymentData BattleSupport { get; set; } = new();
    public BattleSupportDeploymentData DefenderBattleSupport { get; set; } = new();
    public List<AttackOfficerDeploymentData> AttackOfficerDeployments { get; set; } = new();
    public List<AttackOfficerDeploymentData> DefenderOfficerDeployments { get; set; } = new();
    // Chosen while the player configures a defense.  These are dispatched only
    // after the campaign exists, so their ETA starts on battle day one.
    public List<DefenseReinforcementRequestData> DefenseReinforcementRequests { get; set; } = new();
    public DefenderBattlePlan DefenderBattlePlan { get; set; } = DefenderBattlePlan.CityDefense;
    public DefenderBattlePlan? DefenderBattlePlanOverride { get; set; }
    public int TargetFactionId { get; set; }
    public int DurationMonths { get; set; } = 3;
    public List<int> OfficerIds { get; set; } = new();
    public List<int> CaptiveOfficerIds { get; set; } = new();
}

public class DefenseReinforcementRequestData
{
    public int SourceCityId { get; set; }
    public bool IsAllianceRequest { get; set; }
    public int EnvoyOfficerId { get; set; }
    public int RequestedTroops { get; set; }
    public List<AttackOfficerDeploymentData> Deployments { get; set; } = new();
}

public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string MessageZhHant { get; set; } = string.Empty;
    public string MessageEn { get; set; } = string.Empty;
    public bool? IsPlayerRelated { get; set; }
    public bool IsRulerChange { get; set; }
    public List<int> CapturedOfficerIds { get; set; } = new();
    // Transient result notices generated while resolving a primary command,
    // such as an AI captor's prisoner disposition.
    public List<CommandResult> FollowUpResults { get; set; } = new();
    public int ActorFactionId { get; set; }
    public int AttackerRulerOfficerId { get; set; }
    public int ActiveBattleCampaignId { get; set; }
}
