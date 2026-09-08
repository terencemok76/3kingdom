using System.Collections.Generic;

namespace ThreeKingdom.Data;

public enum CampaignStage
{
    FieldBattle,
    SiegePreparation,
    CityBattle,
    Resolved
}

public enum CampaignBattleSide
{
    Attacker,
    Defender
}

public enum DefenderBattlePlan
{
    FieldIntercept,
    CityDefense
}

public enum PostFieldBattleDecision
{
    None,
    ImmediateAssault,
    PrepareNextMonthSiege,
    Withdraw
}

public enum CampaignControllerType
{
    Player,
    Ai
}

public enum CampaignTeamLocation
{
    Field,
    InnerCity,
    Traveling,
    Reserve,
    NeighborCity,
    Eliminated,
    Captured
}

public enum ReinforcementStatus
{
    Traveling,
    Arrived,
    Reserve,
    Deployed,
    Returning,
    Returned,
    Cancelled
}

public enum BattleInvitationSupportType
{
    Troops,
    Resources,
    FullSupport
}

public enum BattleInvitationStatus
{
    Pending,
    Accepted,
    Declined,
    Cancelled
}

public enum CampaignSupplyOwnership
{
    MainFaction,
    Gift,
    Expedition
}

public class CampaignBattleTeamData
{
    public int Id { get; set; }
    public int FactionId { get; set; }
    public CampaignBattleSide Side { get; set; }
    public CampaignControllerType ControllerType { get; set; }
    public int OfficerId { get; set; }
    public TroopType TroopType { get; set; } = TroopType.Infantry;
    public SiegeEngineType SiegeEngineType { get; set; } = SiegeEngineType.None;
    public int ActiveTroops { get; set; }
    public int WoundedTroops { get; set; }
    public int MaximumTroops { get; set; }
    public int Morale { get; set; } = 100;
    public int ReinforcementOrderId { get; set; }
    public CampaignTeamLocation Location { get; set; } = CampaignTeamLocation.Field;
    public string CooperationObjective { get; set; } = string.Empty;
}

public class BattleParticipantData
{
    public int FactionId { get; set; }
    public CampaignBattleSide Side { get; set; }
    public bool IsMainFaction { get; set; }
    public CampaignControllerType ControllerType { get; set; }
    public int GoldContribution { get; set; }
    public int FoodContribution { get; set; }
    public int TroopsCommitted { get; set; }
    public int TroopsLost { get; set; }
    public int BattleDaysParticipated { get; set; }
}

public class ReinforcementOrderData
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public int FactionId { get; set; }
    public CampaignBattleSide Side { get; set; }
    public int SourceCityId { get; set; }
    public int TargetCityId { get; set; }
    public int DispatchYear { get; set; }
    public int DispatchMonth { get; set; }
    public int RouteLinks { get; set; }
    public int RemainingBattleDays { get; set; }
    public int Gold { get; set; }
    public int Food { get; set; }
    public CampaignSupplyOwnership SupplyOwnership { get; set; } = CampaignSupplyOwnership.MainFaction;
    public ReinforcementStatus Status { get; set; } = ReinforcementStatus.Traveling;
    public List<CampaignBattleTeamData> Teams { get; set; } = new();
}

public class BattleInvitationData
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public int InviterFactionId { get; set; }
    public int InvitedFactionId { get; set; }
    public CampaignBattleSide Side { get; set; }
    public BattleInvitationSupportType SupportType { get; set; }
    public BattleInvitationStatus Status { get; set; } = BattleInvitationStatus.Pending;
    public int EnvoyOfficerId { get; set; }
    public int RequestedGold { get; set; }
    public int RequestedFood { get; set; }
    public int RequestedTroops { get; set; }
    public int ResponseBattleDays { get; set; } = 1;
    public string DecisionReason { get; set; } = string.Empty;
}

public class ActiveBattleCampaignData
{
    public int Id { get; set; }
    public int AttackerFactionId { get; set; }
    public int DefenderFactionId { get; set; }
    public int SourceCityId { get; set; }
    public int TargetCityId { get; set; }
    public int StartedYear { get; set; }
    public int StartedMonth { get; set; }
    public int CurrentYear { get; set; }
    public int CurrentMonth { get; set; }
    public int BattleDaysThisMonth { get; set; }
    public int TotalBattleDays { get; set; }
    public CampaignStage Stage { get; set; } = CampaignStage.FieldBattle;
    public DefenderBattlePlan DefenderPlan { get; set; } = DefenderBattlePlan.CityDefense;
    public PostFieldBattleDecision PostFieldDecision { get; set; }
    public bool IsPlayerInvolved { get; set; }
    public bool IsAwaitingPlayerDecision { get; set; }
    public bool AttackerWonFieldBattle { get; set; }
    public int AttackerGold { get; set; }
    public int AttackerFood { get; set; }
    public int DefenderGold { get; set; }
    public int DefenderFood { get; set; }
    public int AttackerGoldUpkeepRemainder { get; set; }
    public int AttackerFoodUpkeepRemainder { get; set; }
    public int DefenderGoldUpkeepRemainder { get; set; }
    public int DefenderFoodUpkeepRemainder { get; set; }
    public string BattleSnapshotJson { get; set; } = string.Empty;
    public List<CampaignBattleTeamData> Teams { get; set; } = new();
    public List<BattleParticipantData> Participants { get; set; } = new();
    public List<ReinforcementOrderData> Reinforcements { get; set; } = new();
    public List<BattleInvitationData> Invitations { get; set; } = new();
}
