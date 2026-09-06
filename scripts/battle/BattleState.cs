using Godot;
using System.Collections.Generic;
using static ThreeKingdom.Battle.BattleBalanceSettings;

namespace ThreeKingdom.Battle;

internal sealed class BattleState
{
    internal bool IsBattleFinished;
    internal bool AttackerOutpostVictorySecured;
    internal Vector2I? HoverGrid;
    internal Vector2I? SelectedGrid;
    internal BattleGridKey? HoverGridKey;
    internal BattleGridKey? SelectedGridKey;
    internal BattleGridKey? SelectedUnitGrid;
    internal BattleOccupantInfo? SelectedUnit;
    internal BattleUnitRepository Units { get; } = new();
    internal HashSet<BattleGridKey> MovableGrids { get; } = new();
    internal HashSet<BattleGridKey> AttackableGrids { get; } = new();
    internal HashSet<BattleGridKey> WorkableGrids { get; } = new();
    internal HashSet<BattleGridKey> StrategyTargetGrids { get; } = new();
    internal HashSet<BattleGridKey> DuelTargetGrids { get; } = new();
    internal HashSet<BattleGridKey> ChargeTargetGrids { get; } = new();
    internal HashSet<BattleGridKey> HireOfficerTargetGrids { get; } = new();
    internal HashSet<BattlePieceMarker> StrategyUsedByMarkerThisTurn { get; } = new();
    internal HashSet<BattlePieceMarker> SupplyUsedByMarkerThisTurn { get; } = new();
    internal HashSet<BattlePieceMarker> ChargeUsedByMarkerThisTurn { get; } = new();
    internal HashSet<BattlePieceMarker> ActedByMarkerThisRound { get; } = new();
    internal BattleCommandMode CommandMode = BattleCommandMode.None;
    internal BattleStrategyAction SelectedStrategyAction = BattleStrategyAction.None;
    internal WorkerWorkAction WorkerWorkAction = WorkerWorkAction.General;
    internal int TurnNumber = 1;
    internal BattleTurnSide CurrentTurnSide = BattleTurnSide.TeamA;
    internal int BattleDateYear = BattleBalanceSettings.BattleDateYear;
    internal int BattleDateMonth = BattleBalanceSettings.BattleDateMonth;
    internal int BattleDateDay = BattleBalanceSettings.BattleDateDay;
    internal BattleAiControlledSides AiControlledSides;
    internal bool IsFieldAiRoundStarted;
    internal BattleTimeOfDay? CurrentBattleTimeOfDay;
    internal BattleWeatherType? CurrentBattleWeather;
    internal BattleWindDirection? CurrentBattleWindDirection;
    internal BattleWindPower? CurrentBattleWindPower;
    internal int TeamATotalTroops;
    internal int TeamBTotalTroops;
    internal int TeamASiegeUnits;
    internal int TeamBSiegeUnits;
    internal int TeamAGenerals;
    internal int TeamBGenerals;
    internal int TeamAStrategyPlans = InitialTeamStrategyPlans;
    internal int TeamBStrategyPlans = InitialTeamStrategyPlans;
    internal int TeamAGold = InitialTeamAGold;
    internal int TeamAFood = InitialTeamAFood;
    internal int TeamAZeroFoodDays;
    internal int TeamBGold = InitialTeamBGold;
    internal int TeamBFood = InitialTeamBFood;
    internal int TeamBZeroFoodDays;
}

internal enum BattleCommandMode
{
    None,
    AwaitingCommand,
    MoveSelect,
    AttackSelect,
    WorkSelect,
    StrategySelect,
    DuelSelect,
    ChargeSelect,
    HireOfficerSelect
}

internal enum BattleStrategyAction
{
    None,
    Extinguish,
    Fire,
    Mental
}

internal enum WorkerWorkAction
{
    General,
    WoodFence
}

internal enum BattleTurnSide
{
    TeamA,
    TeamB
}
