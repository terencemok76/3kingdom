using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ThreeKingdom.Core;

namespace ThreeKingdom.Battle;

public readonly record struct BattleGridKey(int X, int Y, int Level)
{
    public Vector2I Grid => new(X, Y);

    public override string ToString() => $"({X}, {Y}, L{Level})";
}

internal enum BattleDepthRenderKind
{
    CastleVisual,
    FireEffect,
    MoveHighlight,
    AttackHighlight,
    SelectedHighlight,
    SiegeEngine,
    Unit
}

[Tool]
public partial class BattleSceneController : Node2D
{
    public sealed record LaunchOptions(BattleScenarioType ScenarioType, bool UseEditorAuthoredLayout);

    public static LaunchOptions? PendingLaunchOptions { get; set; }

    private const float MapPaddingLeft = 220.0f;
    private const float MapPaddingTop = 220.0f;
    private const float MapPaddingRight = 220.0f;
    private const float MapPaddingBottom = 320.0f;
    private const float BattleLogMinimumWidth = 240.0f;
    private const float BattleLogMinimumHeight = 150.0f;
    private const float DefaultUnitVisualLift = -16.0f;
    private const float WallWalkUnitVisualLift = -58.0f;
    private const float WallWalkHighlightVisualLift = -42.0f;
    private static readonly Vector2 NorthEastWallTopHighlightOffset = new(60.0f, -86.0f);
    private static readonly Vector2 NorthWestWallTopHighlightOffset = new(-60.0f, -86.0f);
    // A wall-top unit must render after the NE wall segments that overlap it from the right.
    private const int WallTopLevelDepthOffset = 3;
    private const string CategoryUnit = "Unit";
    private const string CategorySiegeEngine = "SiegeEngine";
    private const string TroopInfantry = "Infantry";
    private const string TroopSpearman = "Spearman";
    private const string TroopArcher = "Archer";
    private const string TroopCavalry = "Cavalry";
    private const string TroopCrossbow = "Crossbow";
    private const string TroopGuard = "Guard";
    private const string TroopWorker = "Worker";
    private const string TroopRam = "Ram";
    private const string TroopLadder = "Ladder";
    private const string TroopCatapult = "Catapult";
    private const string TroopSupplyCart = "SupplyCart";
    private const string CatapultStoneTexturePath = "res://assets/battle/object/catapult_stone.png";
    private const string NorthEastScenePath = "res://scenes/battle/BattleScene.tscn";
    private const string NorthWestScenePath = "res://scenes/battle/BattleSceneNorthWest.tscn";
    private const string NorthEastSiegeScenarioPath = "res://data/scenarios/battle/siege_ne.tres";
    private const string NorthEastMoatScenarioPath = "res://data/scenarios/battle/moat_siege.tres";
    private const string NorthWestSiegeScenarioPath = "res://data/scenarios/battle/siege_nw.tres";
    private const string NorthWestMoatScenarioPath = "res://data/scenarios/battle/moat_siege_nw.tres";
    private const string BattleQuickSavePath = "user://saves/battle_quicksave.json";
    private const string InfantryIdleSouthEastScenePath = "res://scenes/battle/unit/InfantryIdleSe.tscn";
    private const string InfantryIdleSouthWestScenePath = "res://scenes/battle/unit/InfantryIdleSw.tscn";
    private const string InfantryIdleNorthEastScenePath = "res://scenes/battle/unit/InfantryIdleNe.tscn";
    private const string InfantryIdleNorthWestScenePath = "res://scenes/battle/unit/InfantryIdleNw.tscn";
    private const string InfantryMoveSouthEastScenePath = "res://scenes/battle/unit/InfantryMoveSe.tscn";
    private const string InfantryMoveSouthWestScenePath = "res://scenes/battle/unit/InfantryMoveSw.tscn";
    private const string InfantryMoveNorthEastScenePath = "res://scenes/battle/unit/InfantryMoveNe.tscn";
    private const string InfantryMoveNorthWestScenePath = "res://scenes/battle/unit/InfantryMoveNw.tscn";
    private const string InfantryAttackSouthEastScenePath = "res://scenes/battle/unit/InfantryAttackSe.tscn";
    private const string InfantryAttackSouthWestScenePath = "res://scenes/battle/unit/InfantryAttackSw.tscn";
    private const string InfantryAttackNorthEastScenePath = "res://scenes/battle/unit/InfantryAttackNe.tscn";
    private const string InfantryAttackNorthWestScenePath = "res://scenes/battle/unit/InfantryAttackNw.tscn";
    private const string InfantryHurtSouthEastScenePath = "res://scenes/battle/unit/InfantryHurtSe.tscn";
    private const string InfantryHurtSouthWestScenePath = "res://scenes/battle/unit/InfantryHurtSw.tscn";
    private const string InfantryHurtNorthEastScenePath = "res://scenes/battle/unit/InfantryHurtNe.tscn";
    private const string InfantryHurtNorthWestScenePath = "res://scenes/battle/unit/InfantryHurtNw.tscn";
    private const string SpearmanIdleSouthEastScenePath = "res://scenes/battle/unit/SpearmanIdleSe.tscn";
    private const string SpearmanIdleSouthWestScenePath = "res://scenes/battle/unit/SpearmanIdleSw.tscn";
    private const string SpearmanIdleNorthEastScenePath = "res://scenes/battle/unit/SpearmanIdleNe.tscn";
    private const string SpearmanIdleNorthWestScenePath = "res://scenes/battle/unit/SpearmanIdleNw.tscn";
    private const string SpearmanMoveSouthEastScenePath = "res://scenes/battle/unit/SpearmanMoveSe.tscn";
    private const string SpearmanMoveSouthWestScenePath = "res://scenes/battle/unit/SpearmanMoveSw.tscn";
    private const string SpearmanMoveNorthEastScenePath = "res://scenes/battle/unit/SpearmanMoveNe.tscn";
    private const string SpearmanMoveNorthWestScenePath = "res://scenes/battle/unit/SpearmanMoveNw.tscn";
    private const string SpearmanAttackSouthEastScenePath = "res://scenes/battle/unit/SpearmanAttackSe.tscn";
    private const string SpearmanAttackSouthWestScenePath = "res://scenes/battle/unit/SpearmanAttackSw.tscn";
    private const string SpearmanAttackNorthEastScenePath = "res://scenes/battle/unit/SpearmanAttackNe.tscn";
    private const string SpearmanAttackNorthWestScenePath = "res://scenes/battle/unit/SpearmanAttackNw.tscn";
    private const string SpearmanHurtSouthEastScenePath = "res://scenes/battle/unit/SpearmanHurtSe.tscn";
    private const string SpearmanHurtSouthWestScenePath = "res://scenes/battle/unit/SpearmanHurtSw.tscn";
    private const string SpearmanHurtNorthEastScenePath = "res://scenes/battle/unit/SpearmanHurtNe.tscn";
    private const string SpearmanHurtNorthWestScenePath = "res://scenes/battle/unit/SpearmanHurtNw.tscn";
    private const string ArcherIdleSouthEastScenePath = "res://scenes/battle/unit/ArcherIdleSe.tscn";
    private const string ArcherIdleSouthWestScenePath = "res://scenes/battle/unit/ArcherIdleSw.tscn";
    private const string ArcherIdleNorthEastScenePath = "res://scenes/battle/unit/ArcherIdleNe.tscn";
    private const string ArcherIdleNorthWestScenePath = "res://scenes/battle/unit/ArcherIdleNw.tscn";
    private const string ArcherMoveSouthEastScenePath = "res://scenes/battle/unit/ArcherMoveSe.tscn";
    private const string ArcherMoveSouthWestScenePath = "res://scenes/battle/unit/ArcherMoveSw.tscn";
    private const string ArcherMoveNorthEastScenePath = "res://scenes/battle/unit/ArcherMoveNe.tscn";
    private const string ArcherMoveNorthWestScenePath = "res://scenes/battle/unit/ArcherMoveNw.tscn";
    private const string ArcherAttackSouthEastScenePath = "res://scenes/battle/unit/ArcherAttackSe.tscn";
    private const string ArcherAttackSouthWestScenePath = "res://scenes/battle/unit/ArcherAttackSw.tscn";
    private const string ArcherAttackNorthEastScenePath = "res://scenes/battle/unit/ArcherAttackNe.tscn";
    private const string ArcherAttackNorthWestScenePath = "res://scenes/battle/unit/ArcherAttackNw.tscn";
    private const string ArcherHurtSouthEastScenePath = "res://scenes/battle/unit/ArcherHurtSe.tscn";
    private const string ArcherHurtSouthWestScenePath = "res://scenes/battle/unit/ArcherHurtSw.tscn";
    private const string ArcherHurtNorthEastScenePath = "res://scenes/battle/unit/ArcherHurtNe.tscn";
    private const string ArcherHurtNorthWestScenePath = "res://scenes/battle/unit/ArcherHurtNw.tscn";
    private const string CavalryIdleSouthEastScenePath = "res://scenes/battle/unit/CavalryIdleSe.tscn";
    private const string CavalryIdleSouthWestScenePath = "res://scenes/battle/unit/CavalryIdleSw.tscn";
    private const string CavalryIdleNorthEastScenePath = "res://scenes/battle/unit/CavalryIdleNe.tscn";
    private const string CavalryIdleNorthWestScenePath = "res://scenes/battle/unit/CavalryIdleNw.tscn";
    private const string CavalryMoveSouthEastScenePath = "res://scenes/battle/unit/CavalryMoveSe.tscn";
    private const string CavalryMoveSouthWestScenePath = "res://scenes/battle/unit/CavalryMoveSw.tscn";
    private const string CavalryMoveNorthEastScenePath = "res://scenes/battle/unit/CavalryMoveNe.tscn";
    private const string CavalryMoveNorthWestScenePath = "res://scenes/battle/unit/CavalryMoveNw.tscn";
    private const string CavalryAttackSouthEastScenePath = "res://scenes/battle/unit/CavalryAttackSe.tscn";
    private const string CavalryAttackSouthWestScenePath = "res://scenes/battle/unit/CavalryAttackSw.tscn";
    private const string CavalryAttackNorthEastScenePath = "res://scenes/battle/unit/CavalryAttackNe.tscn";
    private const string CavalryAttackNorthWestScenePath = "res://scenes/battle/unit/CavalryAttackNw.tscn";
    private const string CavalryHurtSouthEastScenePath = "res://scenes/battle/unit/CavalryHurtSe.tscn";
    private const string CavalryHurtSouthWestScenePath = "res://scenes/battle/unit/CavalryHurtSw.tscn";
    private const string CavalryHurtNorthEastScenePath = "res://scenes/battle/unit/CavalryHurtNe.tscn";
    private const string CavalryHurtNorthWestScenePath = "res://scenes/battle/unit/CavalryHurtNw.tscn";
    private const string CarIdleSouthEastScenePath = "res://scenes/battle/unit/CarIdleSe.tscn";
    private const string CarIdleSouthWestScenePath = "res://scenes/battle/unit/CarIdleSw.tscn";
    private const string CarIdleNorthEastScenePath = "res://scenes/battle/unit/CarIdleNe.tscn";
    private const string CarIdleNorthWestScenePath = "res://scenes/battle/unit/CarIdleNw.tscn";
    private const string SupplyCarIdleSouthEastScenePath = "res://scenes/battle/unit/SupplyCarIdleSe.tscn";
    private const string SupplyCarIdleSouthWestScenePath = "res://scenes/battle/unit/SupplyCarIdleSw.tscn";
    private const string SupplyCarIdleNorthEastScenePath = "res://scenes/battle/unit/SupplyCarIdleNe.tscn";
    private const string SupplyCarIdleNorthWestScenePath = "res://scenes/battle/unit/SupplyCarIdleNw.tscn";
    private const string CarLadderIdleSouthEastScenePath = "res://scenes/battle/unit/CarLadderIdleSe.tscn";
    private const string CarLadderIdleSouthWestScenePath = "res://scenes/battle/unit/CarLadderIdleSw.tscn";
    private const string CarLadderIdleNorthEastScenePath = "res://scenes/battle/unit/CarLadderIdleNe.tscn";
    private const string CarLadderIdleNorthWestScenePath = "res://scenes/battle/unit/CarLadderIdleNw.tscn";
    private const string CatapultIdleSouthEastScenePath = "res://scenes/battle/unit/CatapultIdleSe.tscn";
    private const string CatapultIdleSouthWestScenePath = "res://scenes/battle/unit/CatapultIdleSw.tscn";
    private const string CatapultIdleNorthEastScenePath = "res://scenes/battle/unit/CatapultIdleNe.tscn";
    private const string CatapultIdleNorthWestScenePath = "res://scenes/battle/unit/CatapultIdleNw.tscn";
    private const string WorkerIdleNorthEastScenePath = "res://scenes/battle/unit/WorkerIdleNe.tscn";
    private const string WorkerIdleNorthWestScenePath = "res://scenes/battle/unit/WorkerIdleNw.tscn";
    private const string WorkerIdleSouthEastScenePath = "res://scenes/battle/unit/WorkerIdleSe.tscn";
    private const string WorkerIdleSouthWestScenePath = "res://scenes/battle/unit/WorkerIdleSw.tscn";
    private const string WorkerMoveNorthEastScenePath = "res://scenes/battle/unit/WorkerMoveNe.tscn";
    private const string WorkerMoveNorthWestScenePath = "res://scenes/battle/unit/WorkerMoveNw.tscn";
    private const string WorkerMoveSouthEastScenePath = "res://scenes/battle/unit/WorkerMoveSe.tscn";
    private const string WorkerMoveSouthWestScenePath = "res://scenes/battle/unit/WorkerMoveSw.tscn";
    private const string WorkerWorkNorthEastScenePath = "res://scenes/battle/unit/WorkerWorkNe.tscn";
    private const string WorkerWorkNorthWestScenePath = "res://scenes/battle/unit/WorkerWorkNw.tscn";
    private const string WorkerWorkSouthEastScenePath = "res://scenes/battle/unit/WorkerWorkSe.tscn";
    private const string WorkerWorkSouthWestScenePath = "res://scenes/battle/unit/WorkerWorkSw.tscn";
    private const string WorkerHurtNorthEastScenePath = "res://scenes/battle/unit/WorkerHurtNe.tscn";
    private const string WorkerHurtNorthWestScenePath = "res://scenes/battle/unit/WorkerHurtNw.tscn";
    private const string WorkerHurtSouthEastScenePath = "res://scenes/battle/unit/WorkerHurtSe.tscn";
    private const string WorkerHurtSouthWestScenePath = "res://scenes/battle/unit/WorkerHurtSw.tscn";
    private const string WorkerAttackNorthEastScenePath = "res://scenes/battle/unit/WorkerAttackNe.tscn";
    private const string WorkerAttackNorthWestScenePath = "res://scenes/battle/unit/WorkerAttackNw.tscn";
    private const string WorkerAttackSouthEastScenePath = "res://scenes/battle/unit/WorkerAttackSe.tscn";
    private const string WorkerAttackSouthWestScenePath = "res://scenes/battle/unit/WorkerAttackSw.tscn";
    private const string CatapultAttackSouthEastScenePath = "res://scenes/battle/unit/CatapultAttackSe.tscn";
    private const string CatapultAttackSouthWestScenePath = "res://scenes/battle/unit/CatapultAttackSw.tscn";
    private const string CatapultAttackNorthEastScenePath = "res://scenes/battle/unit/CatapultAttackNe.tscn";
    private const string CatapultAttackNorthWestScenePath = "res://scenes/battle/unit/CatapultAttackNw.tscn";
    private const double InfantryMoveAnimationDurationSeconds = 0.4;
    private const double SpearmanMoveAnimationDurationSeconds = 0.4;
    private const double ArcherMoveAnimationDurationSeconds = 0.4;
    private const double CavalryMoveAnimationDurationSeconds = 0.4;
    private const double InfantryAttackAnimationDurationSeconds = 0.62;
    private const double SpearmanAttackAnimationDurationSeconds = 0.72;
    private const double ArcherAttackAnimationDurationSeconds = 0.62;
    private const double CavalryAttackAnimationDurationSeconds = 0.5;
    private const double WorkerAttackAnimationDurationSeconds = 0.75;
    private const double WorkerWorkAnimationDurationSeconds = 0.8;
    private const double CatapultAttackAnimationDurationSeconds = 0.72;
    private const double InfantryHurtAnimationDurationSeconds = 0.5;
    private const double SpearmanHurtAnimationDurationSeconds = 0.65;
    private const double ArcherHurtAnimationDurationSeconds = 0.5;
    private const double CavalryHurtAnimationDurationSeconds = 0.5;
    private const double WorkerHurtAnimationDurationSeconds = 0.75;
    private const double CarMoveAnimationDurationSeconds = 0.4;
    private const double CatapultMoveAnimationDurationSeconds = 0.4;
    private const int InfantryAttackDamage = 850;
    private const int SpearmanAttackDamage = 800;
    private const int ArcherAttackDamage = 900;
    private const int CavalryAttackDamage = 1100;
    private const int CavalryChargeDamage = 1650;
    private const int CavalryChargeVsSpearmanDamage = 900;
    private const int CavalryChargeSpearmanCounterDamage = 600;
    private const int WorkerBridgeRepairAmount = 450;
    private const int WorkerGateRepairAmount = 600;
    private const int WorkerAttackDamage = 350;
    private const int DefaultUnitMorale = 100;
    private const int WorkerMorale = 80;
    private const int SupplyCartMoraleRestore = 8;
    private const int SupplyCartRepairAmount = 450;
    private const int SupplyCartWoundedRecoveryAmount = 600;
    private const int ArcherMaxWeaponAmmo = 6;
    private const int CrossbowMaxWeaponAmmo = 4;
    private const int CatapultMaxWeaponAmmo = 3;
    private const float AmmoDepletedWeakAttackDamageRatio = 0.35f;
    private const int LowMoraleMovePenaltyThreshold = 30;
    private const int MessMoraleThreshold = 15;
    private const int MessStrategyRange = 3;
    private const int MessStrategyDurationTurns = 2;
    private const int MessStrategyMoraleDamage = 12;
    private const int CalmStrategyMoraleRestore = 15;
    private const int MessDesertionPercent = 5;
    private const int InitialTeamStrategyPlans = 6;
    private const float NormalDamageKilledRatio = 0.4f;
    private const float FireDamageKilledRatio = 0.7f;
    private const int DropStoneAttackDamage = 1200;
    private const int PourOilAttackDamage = 1000;
    private const double DropStoneEffectDurationSeconds = 0.48;
    private const double PourOilEffectDurationSeconds = 0.58;
    private const double ArrowProjectileEffectDurationSeconds = 0.42;
    private const double CatapultProjectileEffectDurationSeconds = 0.7;
    private const double HireOfficerEffectDurationSeconds = 1.35;
    private const double HireOfficerPopupDelaySeconds = 0.18;
    private const double HireOfficerMoralePopupDelaySeconds = 0.58;
    private const int FireStrategyRangeBonus = 1;
    private const int FireStrategyBaseDurationSunny = 3;
    private const int FireStrategyBaseDurationCloudy = 2;
    private const int FireStrategyBaseDurationRain = 1;
    private const int FireDamagePerTurnSunny = 420;
    private const int FireDamagePerTurnCloudy = 320;
    private const int FireDamagePerTurnRain = 180;
    private const int FireDamageToGate = 180;
    private const int FireDamageToWoodenFence = 260;
    private const int FireDamageToBridge = 220;
    private const int FireMaxSpreadCandidates = 8;
    private const int RamAttackDamage = 500;
    private const int CatapultAttackDamage = 1300;
    private const int InfantryStructureDamage = 180;
    private const int SpearmanStructureDamage = 160;
    private const int ArcherStructureDamage = 120;
    private const int CavalryStructureDamage = 220;
    private const int WorkerStructureDamage = 60;
    private const int RamStructureDamage = 900;
    private const int CatapultStructureDamage = 700;
    private const int RamMaxHitPoints = 2800;
    private const int LadderMaxHitPoints = 2200;
    private const int CatapultMaxHitPoints = 1800;
    private const int SupplyCartMaxHitPoints = 1600;
    private const int InitialTeamAGold = 8200;
    private const int InitialTeamAFood = 26000;
    private const int InitialTeamBGold = 6400;
    private const int InitialTeamBFood = 19800;
    private const int DailyFoodPer100ActiveTroops = 5;
    private const int DailyGoldPer100ActiveTroops = 1;
    private const int LowFoodMoralePenalty = 6;
    private const int StarvingMoralePenalty = 15;
    private const int SupplyCartDestroyedResourceLossPercent = 25;
    private const int SupplyCartCaptureMoraleBonus = 10;
    private const int HireOfficerGoldCost = 100;
    private const int HireOfficerRange = 2;
    private const int HireOfficerTeamMoraleBonus = 8;
    private const int HireOfficerTeamMoralePenalty = 8;
    private const double DamagePopupDurationSeconds = 2.0;
    private const double MoralePopupDurationSeconds = 1.6;
    private static readonly BattleHudTeamInfo TeamAInfo = new("Team A / Attacker", 0, 0, 0, 0, 0, InitialTeamAGold, InitialTeamAFood);
    private static readonly BattleHudTeamInfo TeamBInfo = new("Team B / Defender", 0, 0, 0, 0, 0, InitialTeamBGold, InitialTeamBFood);
    private const string BattleDateText = "191 Apr 4";
    private static readonly JsonSerializerOptions BattleSaveJsonOptions = new()
    {
        WriteIndented = true
    };

    private BattleMapData? _mapData;
    private Node2D? _mapRoot;
    private Camera2D? _camera;
    private TileMapLayer? _groundLayer;
    private TileMapLayer? _moatLayer;
    private TileMapLayer? _objectLayer;
    private TileMapLayer? _castleLayer;
    private TileMapLayer? _overlayLayer;
    private BattleHighlightRenderer? _highlightLayer;
    private ColorRect? _timeOfDayOverlay;
    private Tween? _timeOfDayOverlayTween;
    private ColorRect? _weatherOverlay;
    private Tween? _weatherOverlayTween;
    private Texture2D? _catapultStoneTexture;
    private Node2D? _battleDepthLayer;
    private Node2D? _occludedUnitSilhouetteLayer;
    private Control? _commandMenu;
    private Control? _battleLogPanel;
    private Control? _battleLogHeaderRow;
    private Control? _battleLogContent;
    private Control? _battleLogScroll;
    private Control? _battleLogResizeGrip;
    private Control? _battleResultOverlay;
    private Label? _battleResultLabel;
    private Button? _allLogButton;
    private Button? _selfLogButton;
    private Button? _minimizeLogButton;
    private Label? _battleLogLabel;
    private Label? _battleLogTitleLabel;
    private Label? _windowTitleLabel;
    private Label? _unitMenuInfoLabel;
    private Button? _endTurnButton;
    private Button? _timeButton;
    private Button? _weatherButton;
    private Button? _windButton;
    private Button? _windPowerButton;
    private Button? _battleOptionButton;
    private Control? _battleOptionOverlay;
    private Label? _battleOptionTitleLabel;
    private Button? _battleOptionCloseButton;
    private Button? _battleOptionSaveButton;
    private Button? _battleOptionLoadButton;
    private Button? _battleOptionLanguageButton;
    private Button? _battleBgmToggleButton;
    private HSlider? _battleBgmVolumeSlider;
    private Label? _battleBgmVolumeValueLabel;
    private Button? _battleSfxToggleButton;
    private HSlider? _battleSfxVolumeSlider;
    private Label? _battleSfxVolumeValueLabel;
    private Button? _battleSaveSettingsButton;
    private ScrollContainer? _commandScroll;
    private Button? _moveButton;
    private Button? _attackButton;
    private Button? _unionAttackButton;
    private Button? _chargeButton;
    private Button? _duelButton;
    private Button? _retreatButton;
    private Button? _hideButton;
    private Button? _dropStoneButton;
    private Button? _pourOilButton;
    private Button? _workButton;
    private Button? _installWoodFenceButton;
    private Button? _uninstallWoodFenceButton;
    private Button? _supplyButton;
    private Button? _resupplyWeaponButton;
    private Button? _captureSupplyCartButton;
    private Button? _hireOfficerButton;
    private Button? _strategyButton;
    private Button? _openGateButton;
    private bool _isDraggingMap;
    private bool _isDraggingCommandMenu;
    private bool _isDraggingBattleLog;
    private bool _isResizingBattleLog;
    private bool _isBattleLogMinimized;
    private bool _isBattleFinished;
    private Vector2 _lastMousePosition;
    private Vector2 _commandMenuDragOffset;
    private Vector2 _battleLogDragOffset;
    private Vector2 _battleLogResizeStartMouse;
    private Vector2 _battleLogResizeStartSize;
    private Vector2I? _hoverGrid;
    private Vector2I? _selectedGrid;
    private BattleGridKey? _hoverGridKey;
    private BattleGridKey? _selectedGridKey;
    private BattleGridKey? _selectedUnitGrid;
    private BattleOccupantInfo? _selectedUnit;
    private readonly HashSet<BattleGridKey> _movableGrids = new();
    private readonly HashSet<BattleGridKey> _attackableGrids = new();
    private readonly HashSet<BattleGridKey> _workableGrids = new();
    private readonly HashSet<BattleGridKey> _strategyTargetGrids = new();
    private readonly HashSet<BattleGridKey> _duelTargetGrids = new();
    private readonly HashSet<BattleGridKey> _chargeTargetGrids = new();
    private readonly HashSet<BattleGridKey> _hireOfficerTargetGrids = new();
    private readonly Dictionary<BattleGridKey, List<BattleOccupantInfo>> _occupantsByGrid = new();
    private readonly Dictionary<Node2D, BattleDepthEntry> _battleDepthEntries = new();
    private readonly Dictionary<Vector2I, Sprite2D> _castleDepthSpritesByGrid = new();
    private readonly List<BattleHighlightRenderer> _highlightDepthVisuals = new();
    private readonly Dictionary<BattleGridKey, Node2D> _occludedUnitSilhouettesByGrid = new();
    private readonly Dictionary<BattlePieceMarker, WallTopAttackAmmo> _wallTopAttackAmmoByMarker = new();
    private readonly Dictionary<BattleGridKey, BattleFireState> _activeFireByGrid = new();
    private readonly Dictionary<BattleGridKey, Node2D> _fireVisualsByGrid = new();
    private readonly HashSet<BattlePieceMarker> _strategyUsedByMarkerThisTurn = new();
    private readonly HashSet<BattlePieceMarker> _supplyUsedByMarkerThisTurn = new();
    private readonly HashSet<BattlePieceMarker> _chargeUsedByMarkerThisTurn = new();
    private readonly List<BattleLogEntry> _battleLogs = new();
    private readonly List<ColorRect> _rainStreaks = new();
    private BattleCommandMode _commandMode = BattleCommandMode.None;
    private BattleStrategyAction _selectedStrategyAction = BattleStrategyAction.None;
    private WorkerWorkAction _workerWorkAction = WorkerWorkAction.General;
    private int _turnNumber = 1;
    private BattleTurnSide _currentTurnSide = BattleTurnSide.TeamA;
    private BattleTimeOfDay? _currentBattleTimeOfDay;
    private BattleWeatherType? _currentBattleWeather;
    private BattleWindDirection? _currentBattleWindDirection;
    private BattleWindPower? _currentBattleWindPower;
    private int _teamATotalTroops;
    private int _teamBTotalTroops;
    private int _teamASiegeUnits;
    private int _teamBSiegeUnits;
    private int _teamAGenerals;
    private int _teamBGenerals;
    private int _teamAStrategyPlans = InitialTeamStrategyPlans;
    private int _teamBStrategyPlans = InitialTeamStrategyPlans;
    private int _teamAGold = InitialTeamAGold;
    private int _teamAFood = InitialTeamAFood;
    private int _teamBGold = InitialTeamBGold;
    private int _teamBFood = InitialTeamBFood;
    private bool _showSelfTeamLogOnly;
    private bool _battleBgmEnabled = true;
    private bool _battleSfxEnabled = true;
    private float _battleBgmVolume = 1.0f;
    private float _battleSfxVolume = 1.0f;
    private double _weatherEffectTime;
    private Vector2 _battleLogExpandedSize = new(310.0f, 370.0f);
    private bool _editorBakeBattleLayout;
    private bool _editorClearTileLayout;
    private bool _editorRefreshBattleDepthPreview;
    private GameAudioController? _battleAudioController;
    private readonly LocalizationService _localization = new();

    private readonly record struct BattleDepthEntry(Node2D Node, BattleGridKey Grid, BattleDepthRenderKind Kind, int LocalOrder);
    private readonly record struct UnionAttackParticipant(BattleGridKey Grid, BattleOccupantInfo Occupant);
    private readonly record struct BattleLogEntry(int Turn, string TeamName, string Category, string Message);
    private readonly record struct BattleCasualtyResult(BattleOccupantInfo UpdatedTarget, int ActualDamage, int KilledTroops, int WoundedTroops);
    private sealed record UnionAttackCandidate(BattleGridKey TargetGrid, List<UnionAttackParticipant> Participants);

    [Export]
    public BattleScenarioType ScenarioType { get; set; } = BattleScenarioType.SiegeAssault;

    [Export]
    public BattleScenarioDefinition? ScenarioDefinition { get; set; }

    [Export]
    public int DropStoneUsesPerUnit { get; set; } = 3;

    [Export]
    public int PourOilUsesPerUnit { get; set; } = 2;

    [Export]
    public bool UseEditorAuthoredLayout { get; set; }

    [Export]
    public bool EditorBakeBattleLayout
    {
        get => _editorBakeBattleLayout;
        set
        {
            if (!value || !Engine.IsEditorHint())
            {
                _editorBakeBattleLayout = value;
                return;
            }

            CacheSceneNodes();
            BakeBattleLayoutInEditor();
            _editorBakeBattleLayout = false;
            NotifyPropertyListChanged();
        }
    }

    [Export]
    public bool EditorClearTileLayout
    {
        get => _editorClearTileLayout;
        set
        {
            if (!value || !Engine.IsEditorHint())
            {
                _editorClearTileLayout = value;
                return;
            }

            CacheSceneNodes();
            ClearTileLayoutInEditor();
            _editorClearTileLayout = false;
            NotifyPropertyListChanged();
        }
    }

    [Export]
    public bool EditorRefreshBattleDepthPreview
    {
        get => _editorRefreshBattleDepthPreview;
        set
        {
            if (!value || !Engine.IsEditorHint())
            {
                _editorRefreshBattleDepthPreview = value;
                return;
            }

            CacheSceneNodes();
            BuildEditorPreview();
            _editorRefreshBattleDepthPreview = false;
            NotifyPropertyListChanged();
        }
    }

    public override void _Ready()
    {
        CacheSceneNodes();
        if (Engine.IsEditorHint())
        {
            BuildEditorPreview();
            return;
        }

        InitializeBattleLocalization();
        ApplyPendingLaunchOptions();
        StartBattleBgm();

        if (_endTurnButton != null)
        {
            _endTurnButton.Pressed += OnEndTurnButtonPressed;
        }

        if (_timeButton != null)
        {
            _timeButton.Pressed += OnTimeButtonPressed;
        }

        if (_weatherButton != null)
        {
            _weatherButton.Pressed += OnWeatherButtonPressed;
        }

        if (_windButton != null)
        {
            _windButton.Pressed += OnWindButtonPressed;
        }

        if (_windPowerButton != null)
        {
            _windPowerButton.Pressed += OnWindPowerButtonPressed;
        }

        if (_battleOptionButton != null)
        {
            _battleOptionButton.Pressed += OnBattleOptionButtonPressed;
        }

        if (_battleOptionCloseButton != null)
        {
            _battleOptionCloseButton.Pressed += HideBattleOptionDialog;
        }

        if (_battleOptionSaveButton != null)
        {
            _battleOptionSaveButton.Pressed += OnBattleOptionSaveButtonPressed;
        }

        if (_battleOptionLoadButton != null)
        {
            _battleOptionLoadButton.Pressed += OnBattleOptionLoadButtonPressed;
        }

        if (_battleOptionLanguageButton != null)
        {
            _battleOptionLanguageButton.Pressed += OnBattleOptionLanguageButtonPressed;
        }

        if (_battleBgmToggleButton != null)
        {
            _battleBgmToggleButton.Pressed += OnBattleBgmToggleButtonPressed;
        }

        if (_battleSfxToggleButton != null)
        {
            _battleSfxToggleButton.Pressed += OnBattleSfxToggleButtonPressed;
        }

        if (_battleBgmVolumeSlider != null)
        {
            _battleBgmVolumeSlider.ValueChanged += OnBattleBgmVolumeChanged;
        }

        if (_battleSfxVolumeSlider != null)
        {
            _battleSfxVolumeSlider.ValueChanged += OnBattleSfxVolumeChanged;
        }

        if (_battleSaveSettingsButton != null)
        {
            _battleSaveSettingsButton.Pressed += OnBattleSaveSettingsButtonPressed;
        }

        if (_moveButton != null)
        {
            _moveButton.Pressed += OnMoveButtonPressed;
        }

        if (_attackButton != null)
        {
            _attackButton.Pressed += OnAttackButtonPressed;
        }

        if (_unionAttackButton != null)
        {
            _unionAttackButton.Pressed += OnUnionAttackButtonPressed;
        }

        if (_chargeButton != null)
        {
            _chargeButton.Pressed += OnChargeButtonPressed;
        }

        if (_duelButton != null)
        {
            _duelButton.Pressed += OnDuelButtonPressed;
        }

        if (_retreatButton != null)
        {
            _retreatButton.Pressed += OnRetreatButtonPressed;
        }

        if (_hideButton != null)
        {
            _hideButton.Pressed += OnHideButtonPressed;
        }

        if (_dropStoneButton != null)
        {
            _dropStoneButton.Pressed += OnDropStoneButtonPressed;
        }

        if (_pourOilButton != null)
        {
            _pourOilButton.Pressed += OnPourOilButtonPressed;
        }

        if (_workButton != null)
        {
            _workButton.Pressed += OnWorkButtonPressed;
        }

        if (_installWoodFenceButton != null)
        {
            _installWoodFenceButton.Pressed += OnInstallWoodFenceButtonPressed;
        }

        if (_uninstallWoodFenceButton != null)
        {
            _uninstallWoodFenceButton.Pressed += OnUninstallWoodFenceButtonPressed;
        }

        if (_supplyButton != null)
        {
            _supplyButton.Pressed += OnSupplyButtonPressed;
        }

        if (_resupplyWeaponButton != null)
        {
            _resupplyWeaponButton.Pressed += OnResupplyWeaponButtonPressed;
        }

        if (_captureSupplyCartButton != null)
        {
            _captureSupplyCartButton.Pressed += OnCaptureSupplyCartButtonPressed;
        }

        if (_hireOfficerButton != null)
        {
            _hireOfficerButton.Pressed += OnHireOfficerButtonPressed;
        }

        if (_strategyButton != null)
        {
            _strategyButton.Pressed += OnStrategyButtonPressed;
        }

        if (_openGateButton != null)
        {
            _openGateButton.Pressed += OnOpenGateButtonPressed;
        }

        if (_allLogButton != null)
        {
            _allLogButton.Pressed += OnAllLogButtonPressed;
        }

        if (_selfLogButton != null)
        {
            _selfLogButton.Pressed += OnSelfLogButtonPressed;
        }

        if (_minimizeLogButton != null)
        {
            _minimizeLogButton.Pressed += OnMinimizeLogButtonPressed;
        }

        if (_battleLogHeaderRow != null)
        {
            _battleLogHeaderRow.GuiInput += OnBattleLogHeaderGuiInput;
        }

        if (_battleLogTitleLabel != null)
        {
            _battleLogTitleLabel.GuiInput += OnBattleLogHeaderGuiInput;
        }

        if (_windowTitleLabel != null)
        {
            _windowTitleLabel.GuiInput += OnCommandMenuTitleGuiInput;
        }

        InitializeMapDataAndLayers();
        BuildCastleDepthVisuals();
        PopulateMarkers();
        RefreshBattleDepthLayerOrder();
        RefreshOccludedUnitSilhouettes();
        ConfigureHud();
        ApplyTimeOfDayVisual(animate: false);
        BuildWeatherVisuals();
        ApplyWeatherVisual(animate: false);
        ApplyBattleLogPanelStyle();
        ApplyBattleOptionDialogStyle();
        RefreshBattleLogPanel();

        if (_mapRoot != null)
        {
            _mapRoot.Position = GetClampedMapPosition(_mapRoot.Position);
        }
    }

    private void InitializeBattleLocalization()
    {
        _localization.Load();
        var settings = LoadBattleOptionSettings();
        _localization.SetLanguage(settings.Language);
        RefreshBattleOptionDialogText();
    }

    private OptionSettingsData LoadBattleOptionSettings()
    {
        var settings = OptionSettingsStore.LoadOrDefault();
        _battleBgmEnabled = settings.BgmEnabled;
        _battleSfxEnabled = settings.SfxEnabled;
        _battleBgmVolume = Mathf.Clamp(settings.BgmVolume, 0.0f, 1.0f);
        _battleSfxVolume = Mathf.Clamp(settings.SfxVolume, 0.0f, 1.0f);
        return settings;
    }

    private void SaveBattleOptionSettings()
    {
        var settings = OptionSettingsStore.LoadOrDefault();
        settings.Language = _localization.CurrentLanguage;
        settings.BgmEnabled = _battleBgmEnabled;
        settings.SfxEnabled = _battleSfxEnabled;
        settings.BgmVolume = _battleBgmVolume;
        settings.SfxVolume = _battleSfxVolume;
        OptionSettingsStore.Save(settings);
    }

    private void ApplyBattleAudioSettings()
    {
        GameAudioController.Instance?.SetBgmEnabled(_battleBgmEnabled);
        GameAudioController.Instance?.SetSfxEnabled(_battleSfxEnabled);
        GameAudioController.Instance?.SetBgmVolume(_battleBgmVolume);
        GameAudioController.Instance?.SetSfxVolume(_battleSfxVolume);
        SetAudioBusMute(_battleSfxEnabled, "Sfx", "SFX");
    }

    private static void SetAudioBusMute(bool enabled, params string[] busNames)
    {
        foreach (var busName in busNames)
        {
            var busIndex = AudioServer.GetBusIndex(busName);
            if (busIndex >= 0)
            {
                AudioServer.SetBusMute(busIndex, !enabled);
            }
        }
    }

    private string BattleText(string key, string fallback)
    {
        var text = _localization.T(key);
        return string.Equals(text, key, StringComparison.OrdinalIgnoreCase) ? fallback : text;
    }

    private string BattleFormat(string key, string fallback, params object[] args)
    {
        var template = BattleText(key, fallback);
        return string.Format(template, args);
    }

    private void ApplyPendingLaunchOptions()
    {
        if (PendingLaunchOptions == null)
        {
            return;
        }

        ScenarioType = PendingLaunchOptions.ScenarioType;
        UseEditorAuthoredLayout = PendingLaunchOptions.UseEditorAuthoredLayout;
        PendingLaunchOptions = null;
    }

    private void StartBattleBgm()
    {
        var audioController = GameAudioController.Instance;
        if (audioController == null)
        {
            _battleAudioController = new GameAudioController
            {
                Name = "BattleAudioController"
            };
            AddChild(_battleAudioController);
            audioController = GameAudioController.Instance;
        }

        if (audioController == null)
        {
            return;
        }

        ApplyBattleAudioSettings();
        audioController.PlayBattleBgm();
    }

    private void OnBattleSaveButtonPressed()
    {
        if (TrySaveBattleQuickSave(out var errorMessage))
        {
            AppendBattleLog(GetCurrentTurnSideName(), "Save", $"Battle quick save completed: {BattleQuickSavePath}");
            return;
        }

        AppendBattleLog(GetCurrentTurnSideName(), "Save", $"Battle quick save failed: {errorMessage}");
    }

    private void OnBattleLoadButtonPressed()
    {
        if (TryLoadBattleQuickSave(out var errorMessage))
        {
            AppendBattleLog(GetCurrentTurnSideName(), "Load", $"Battle quick load completed: {BattleQuickSavePath}");
            return;
        }

        AppendBattleLog(GetCurrentTurnSideName(), "Load", $"Battle quick load failed: {errorMessage}");
    }

    private void OnBattleOptionButtonPressed()
    {
        ShowBattleOptionDialog();
    }

    private void ShowBattleOptionDialog()
    {
        RefreshBattleOptionDialogText();
        if (_battleOptionOverlay != null)
        {
            _battleOptionOverlay.Visible = true;
        }
    }

    private void HideBattleOptionDialog()
    {
        if (_battleOptionOverlay != null)
        {
            _battleOptionOverlay.Visible = false;
        }
    }

    private void OnBattleOptionSaveButtonPressed()
    {
        OnBattleSaveButtonPressed();
        RefreshBattleOptionDialogText();
    }

    private void OnBattleOptionLoadButtonPressed()
    {
        OnBattleLoadButtonPressed();
        RefreshBattleOptionDialogText();
    }

    private void OnBattleOptionLanguageButtonPressed()
    {
        _localization.ToggleLanguage();
        ConfigureHud();
        RefreshBattleLogPanel();
        RefreshBattleOptionDialogText();
        if (_commandMenu?.Visible == true)
        {
            var commandMenuPosition = _commandMenu.Position;
            ShowCommandMenu(commandMenuPosition - new Vector2(12.0f, 12.0f));
            _commandMenu.Position = commandMenuPosition;
        }
    }

    private void OnBattleBgmToggleButtonPressed()
    {
        _battleBgmEnabled = !_battleBgmEnabled;
        ApplyBattleAudioSettings();
        RefreshBattleOptionDialogText();
    }

    private void OnBattleSfxToggleButtonPressed()
    {
        _battleSfxEnabled = !_battleSfxEnabled;
        ApplyBattleAudioSettings();
        RefreshBattleOptionDialogText();
    }

    private void OnBattleBgmVolumeChanged(double value)
    {
        _battleBgmVolume = Mathf.Clamp((float)value / 100.0f, 0.0f, 1.0f);
        ApplyBattleAudioSettings();
        RefreshBattleOptionDialogText(updateSliderValues: false);
    }

    private void OnBattleSfxVolumeChanged(double value)
    {
        _battleSfxVolume = Mathf.Clamp((float)value / 100.0f, 0.0f, 1.0f);
        ApplyBattleAudioSettings();
        RefreshBattleOptionDialogText(updateSliderValues: false);
    }

    private void OnBattleSaveSettingsButtonPressed()
    {
        SaveBattleOptionSettings();
        AppendBattleLog(GetCurrentTurnSideName(), "Option", BattleText("log.option_settings_saved", "Option settings saved."));
        RefreshBattleOptionDialogText();
    }

    private void RefreshBattleOptionDialogText(bool updateSliderValues = true)
    {
        if (_battleOptionButton != null)
        {
            _battleOptionButton.Text = BattleText("ui.option", "Option");
        }

        if (_battleOptionTitleLabel != null)
        {
            _battleOptionTitleLabel.Text = BattleText("ui.options", "Options");
        }

        if (_battleOptionSaveButton != null)
        {
            _battleOptionSaveButton.Text = BattleText("ui.battle.save", "Save");
        }

        if (_battleOptionLoadButton != null)
        {
            _battleOptionLoadButton.Text = BattleText("ui.battle.load", "Load");
        }

        if (_battleOptionLanguageButton != null)
        {
            _battleOptionLanguageButton.Text = BattleText("ui.option_language_toggle", "Language: English / 繁體中文");
        }

        if (_battleBgmToggleButton != null)
        {
            _battleBgmToggleButton.Text = FormatBattleAudioToggleText(isBgm: true, _battleBgmEnabled);
        }

        if (_battleSfxToggleButton != null)
        {
            _battleSfxToggleButton.Text = FormatBattleAudioToggleText(isBgm: false, _battleSfxEnabled);
        }

        if (_battleBgmVolumeValueLabel != null)
        {
            _battleBgmVolumeValueLabel.Text = FormatBattleVolumePercent(_battleBgmVolume);
        }

        if (_battleSfxVolumeValueLabel != null)
        {
            _battleSfxVolumeValueLabel.Text = FormatBattleVolumePercent(_battleSfxVolume);
        }

        if (updateSliderValues)
        {
            if (_battleBgmVolumeSlider != null)
            {
                _battleBgmVolumeSlider.SetValueNoSignal(Mathf.RoundToInt(_battleBgmVolume * 100.0f));
            }

            if (_battleSfxVolumeSlider != null)
            {
                _battleSfxVolumeSlider.SetValueNoSignal(Mathf.RoundToInt(_battleSfxVolume * 100.0f));
            }
        }

        if (_battleSaveSettingsButton != null)
        {
            _battleSaveSettingsButton.Text = BattleText("ui.save_settings", "Save Settings");
        }
    }

    private string FormatBattleAudioToggleText(bool isBgm, bool enabled)
    {
        var label = isBgm ? "BGM" : "SFX";
        var state = enabled ? BattleText("ui.on", "On") : BattleText("ui.off", "Off");
        return BattleFormat("fmt.audio_toggle_button", "{0}: {1}", label, state);
    }

    private static string FormatBattleVolumePercent(float volume)
    {
        return $"{Mathf.RoundToInt(Mathf.Clamp(volume, 0.0f, 1.0f) * 100.0f)}%";
    }

    private bool TrySaveBattleQuickSave(out string errorMessage)
    {
        errorMessage = string.Empty;
        if (_mapData == null)
        {
            errorMessage = "battle map is not ready";
            return false;
        }

        try
        {
            var saveData = CreateBattleSaveData();
            Directory.CreateDirectory(ProjectSettings.GlobalizePath("user://saves"));
            File.WriteAllText(
                ProjectSettings.GlobalizePath(BattleQuickSavePath),
                JsonSerializer.Serialize(saveData, BattleSaveJsonOptions),
                Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            GD.PushError($"Battle quick save failed: {ex}");
            return false;
        }
    }

    private bool TryLoadBattleQuickSave(out string errorMessage)
    {
        errorMessage = string.Empty;
        var resolvedPath = ProjectSettings.GlobalizePath(BattleQuickSavePath);
        if (!File.Exists(resolvedPath))
        {
            errorMessage = "battle quick save file not found";
            return false;
        }

        try
        {
            var json = File.ReadAllText(resolvedPath, Encoding.UTF8);
            var saveData = JsonSerializer.Deserialize<BattleSaveData>(json, BattleSaveJsonOptions);
            if (saveData == null || saveData.Version != 1)
            {
                errorMessage = "unsupported battle save format";
                return false;
            }

            ApplyBattleSaveData(saveData);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            GD.PushError($"Battle quick load failed: {ex}");
            return false;
        }
    }

    private BattleSaveData CreateBattleSaveData()
    {
        var saveData = new BattleSaveData
        {
            ScenarioType = ScenarioType,
            UseEditorAuthoredLayout = UseEditorAuthoredLayout,
            TurnNumber = _turnNumber,
            CurrentTurnSide = _currentTurnSide,
            CurrentBattleTimeOfDay = GetCurrentBattleTimeOfDay(),
            CurrentBattleWeather = GetCurrentBattleWeather(),
            CurrentBattleWindDirection = GetCurrentBattleWindDirection(),
            CurrentBattleWindPower = GetCurrentBattleWindPower(),
            TeamAStrategyPlans = _teamAStrategyPlans,
            TeamBStrategyPlans = _teamBStrategyPlans,
            TeamAGold = _teamAGold,
            TeamAFood = _teamAFood,
            TeamBGold = _teamBGold,
            TeamBFood = _teamBFood,
            ShowSelfTeamLogOnly = _showSelfTeamLogOnly,
            BattleLogExpandedWidth = _battleLogExpandedSize.X,
            BattleLogExpandedHeight = _battleLogExpandedSize.Y,
            IsBattleLogMinimized = _isBattleLogMinimized
        };

        if (_mapData != null)
        {
            for (var y = 0; y < BattleMapData.Height; y++)
            {
                for (var x = 0; x < BattleMapData.Width; x++)
                {
                    saveData.Cells.Add(BattleCellSaveData.FromCell(_mapData.GetCell(x, y)));
                }
            }
        }

        foreach (var (grid, occupants) in _occupantsByGrid.OrderBy(entry => entry.Key.Y).ThenBy(entry => entry.Key.X).ThenBy(entry => entry.Key.Level))
        {
            foreach (var occupant in occupants)
            {
                if (!IsBattlePiece(occupant))
                {
                    continue;
                }

                var occupantSave = BattleOccupantSaveData.FromOccupant(grid, occupant);
                saveData.Occupants.Add(occupantSave);
                if (occupant.Marker == null)
                {
                    continue;
                }

                if (_strategyUsedByMarkerThisTurn.Contains(occupant.Marker))
                {
                    saveData.StrategyUsedUnitIds.Add(occupantSave.UnitId);
                }

                if (_supplyUsedByMarkerThisTurn.Contains(occupant.Marker))
                {
                    saveData.SupplyUsedUnitIds.Add(occupantSave.UnitId);
                }

                if (_chargeUsedByMarkerThisTurn.Contains(occupant.Marker))
                {
                    saveData.ChargeUsedUnitIds.Add(occupantSave.UnitId);
                }

                if (_wallTopAttackAmmoByMarker.TryGetValue(occupant.Marker, out var ammo))
                {
                    saveData.WallTopAmmo.Add(new BattleWallTopAmmoSaveData
                    {
                        UnitId = occupantSave.UnitId,
                        DropStoneUses = ammo.DropStoneUses,
                        PourOilUses = ammo.PourOilUses
                    });
                }
            }
        }

        foreach (var (grid, fireState) in _activeFireByGrid.OrderBy(entry => entry.Key.Y).ThenBy(entry => entry.Key.X).ThenBy(entry => entry.Key.Level))
        {
            saveData.ActiveFires.Add(new BattleFireSaveData
            {
                X = grid.X,
                Y = grid.Y,
                Level = grid.Level,
                RemainingTurns = fireState.RemainingTurns,
                BurnTurns = fireState.BurnTurns
            });
        }

        foreach (var log in _battleLogs)
        {
            saveData.Logs.Add(new BattleLogSaveData
            {
                Turn = log.Turn,
                TeamName = log.TeamName,
                Category = log.Category,
                Message = log.Message
            });
        }

        return saveData;
    }

    private void ApplyBattleSaveData(BattleSaveData saveData)
    {
        var reusableMarkersByStableId = CollectReusableBattleMarkersByStableId();
        ResetBattleRuntimeStateForLoad();

        ScenarioType = saveData.ScenarioType;
        UseEditorAuthoredLayout = saveData.UseEditorAuthoredLayout;
        _turnNumber = Math.Max(1, saveData.TurnNumber);
        _currentTurnSide = saveData.CurrentTurnSide;
        _currentBattleTimeOfDay = saveData.CurrentBattleTimeOfDay;
        _currentBattleWeather = saveData.CurrentBattleWeather;
        _currentBattleWindDirection = saveData.CurrentBattleWindDirection;
        _currentBattleWindPower = saveData.CurrentBattleWindPower;
        _teamAStrategyPlans = Mathf.Clamp(saveData.TeamAStrategyPlans, 0, InitialTeamStrategyPlans);
        _teamBStrategyPlans = Mathf.Clamp(saveData.TeamBStrategyPlans, 0, InitialTeamStrategyPlans);
        _teamAGold = Math.Max(0, saveData.TeamAGold);
        _teamAFood = Math.Max(0, saveData.TeamAFood);
        _teamBGold = Math.Max(0, saveData.TeamBGold);
        _teamBFood = Math.Max(0, saveData.TeamBFood);
        _showSelfTeamLogOnly = saveData.ShowSelfTeamLogOnly;
        _isBattleLogMinimized = saveData.IsBattleLogMinimized;
        _battleLogExpandedSize = new Vector2(
            Mathf.Max(BattleLogMinimumWidth, saveData.BattleLogExpandedWidth),
            Mathf.Max(BattleLogMinimumHeight, saveData.BattleLogExpandedHeight));

        InitializeMapDataAndLayers();
        ApplySavedCells(saveData.Cells);
        RefreshBattleMapVisualsAfterLoad();

        var markersByUnitId = new Dictionary<string, BattlePieceMarker>();
        foreach (var occupantSave in saveData.Occupants ?? new List<BattleOccupantSaveData>())
        {
            var marker = CreateSavedBattleMarker(occupantSave, reusableMarkersByStableId);
            if (marker != null)
            {
                markersByUnitId[occupantSave.UnitId] = marker;
            }
        }

        RecalculateBattleHudTotals();
        RestoreBattleTurnUsage(saveData, markersByUnitId);
        RestoreBattleLogs(saveData.Logs);
        RestoreBattleFires(saveData.ActiveFires);

        ConfigureHud();
        ApplyTimeOfDayVisual(animate: false);
        BuildWeatherVisuals();
        ApplyWeatherVisual(animate: false);
        RefreshBattleLogPanel();
        RefreshInfoPanel();
        RefreshHighlights();
        RefreshOccludedUnitSilhouettes();
        RefreshBattleDepthLayerOrder();
    }

    private void ResetBattleRuntimeStateForLoad()
    {
        HideCommandMenu();
        _isBattleFinished = false;
        if (_battleResultOverlay != null)
        {
            _battleResultOverlay.Visible = false;
        }
        ClearHighlightDepthVisuals();
        ClearOccludedUnitSilhouettes();
        ClearFireVisuals();
        PrepareBattlePieceMarkersForLoad();
        ClearCastleDepthVisuals();

        _battleDepthEntries.Clear();
        _occupantsByGrid.Clear();
        _wallTopAttackAmmoByMarker.Clear();
        _activeFireByGrid.Clear();
        _strategyUsedByMarkerThisTurn.Clear();
        _supplyUsedByMarkerThisTurn.Clear();
        _chargeUsedByMarkerThisTurn.Clear();
        _battleLogs.Clear();
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _chargeTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _workerWorkAction = WorkerWorkAction.General;
        _selectedUnit = null;
        _selectedUnitGrid = null;
        _selectedGrid = null;
        _selectedGridKey = null;
    }

    private void ClearFireVisuals()
    {
        foreach (var fireRoot in _fireVisualsByGrid.Values)
        {
            _battleDepthEntries.Remove(fireRoot);
            fireRoot.QueueFree();
        }

        _fireVisualsByGrid.Clear();
    }

    private Dictionary<string, Queue<BattlePieceMarker>> CollectReusableBattleMarkersByStableId()
    {
        var markersByStableId = new Dictionary<string, Queue<BattlePieceMarker>>();
        foreach (var occupant in _occupantsByGrid.Values.SelectMany(static occupants => occupants))
        {
            if (occupant.Marker == null || !IsBattlePiece(occupant))
            {
                continue;
            }

            var stableId = BuildBattleOccupantStableId(occupant);
            if (!markersByStableId.TryGetValue(stableId, out var markerQueue))
            {
                markerQueue = new Queue<BattlePieceMarker>();
                markersByStableId[stableId] = markerQueue;
            }

            markerQueue.Enqueue(occupant.Marker);
        }

        return markersByStableId;
    }

    private void PrepareBattlePieceMarkersForLoad()
    {
        foreach (var marker in GetBattlePieceMarkersInUnitLayer().ToList())
        {
            _battleDepthEntries.Remove(marker);
            marker.Visible = false;
        }
    }

    private IEnumerable<BattlePieceMarker> GetBattlePieceMarkersInUnitLayer()
    {
        var unitLayer = GetNodeOrNull<Node2D>("MapRoot/UnitLayer");
        return unitLayer == null
            ? Enumerable.Empty<BattlePieceMarker>()
            : EnumerateBattlePieceMarkers(unitLayer);
    }

    private static IEnumerable<BattlePieceMarker> EnumerateBattlePieceMarkers(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is BattlePieceMarker marker)
            {
                yield return marker;
            }

            foreach (var nestedMarker in EnumerateBattlePieceMarkers(child))
            {
                yield return nestedMarker;
            }
        }
    }

    private void ApplySavedCells(List<BattleCellSaveData>? cells)
    {
        if (_mapData == null || cells == null)
        {
            return;
        }

        foreach (var savedCell in cells)
        {
            if (!IsWithinMap(new Vector2I(savedCell.X, savedCell.Y)))
            {
                continue;
            }

            savedCell.ApplyTo(_mapData.GetCell(savedCell.X, savedCell.Y));
        }
    }

    private void RefreshBattleMapVisualsAfterLoad()
    {
        if (_mapData == null)
        {
            return;
        }

        if (_groundLayer != null)
        {
            BattleTileMapBuilder.ConfigureLayer(_groundLayer, _mapData, BattleTileLayerKind.Ground);
        }

        ConfigureOptionalTileMapLayer(_moatLayer, BattleTileLayerKind.Moat);
        if (_objectLayer != null)
        {
            BattleTileMapBuilder.ConfigureLayer(_objectLayer, _mapData, BattleTileLayerKind.Object);
            _objectLayer.Visible = true;
        }

        if (_castleLayer != null)
        {
            BattleTileMapBuilder.ConfigureLayer(_castleLayer, _mapData, BattleTileLayerKind.Castle);
        }

        if (_overlayLayer != null)
        {
            BattleTileMapBuilder.ConfigureLayer(_overlayLayer, _mapData, BattleTileLayerKind.DeploymentOverlay);
        }

        BuildCastleDepthVisuals();
    }

    private BattlePieceMarker? CreateSavedBattleMarker(BattleOccupantSaveData saveData, Dictionary<string, Queue<BattlePieceMarker>> reusableMarkersByStableId)
    {
        var unitLayer = GetNodeOrNull<Node2D>("MapRoot/UnitLayer");
        if (unitLayer == null)
        {
            return null;
        }

        var grid = new BattleGridKey(saveData.X, saveData.Y, saveData.Level);
        var marker = TryTakeReusableBattleMarker(saveData, reusableMarkersByStableId);
        if (marker == null)
        {
            marker = new BattlePieceMarker
            {
                Name = $"Saved_{saveData.ShortLabel}_{saveData.X}_{saveData.Y}_L{saveData.Level}"
            };
            unitLayer.AddChild(marker);
        }

        marker.Position = GetMarkerPosition(grid);
        marker.Visible = true;

        var occupant = saveData.ToOccupant(marker);
        marker.Setup(
            saveData.ShortLabel,
            GetSavedMarkerFillColor(occupant),
            GetSavedMarkerBorderColor(occupant),
            saveData.Category == CategorySiegeEngine ? 21.0f : 19.0f);
        marker.SetupNamePlate(string.IsNullOrWhiteSpace(saveData.OfficerName) ? saveData.DisplayName : saveData.OfficerName);
        marker.SetupTeamArrow(GetTeamArrowColor(saveData.TeamName));
        marker.SetupSpriteAnimationScene(GetIdleSceneForOccupant(occupant));
        UpdateMarkerStrengthBar(occupant);
        UpdateMarkerStatusIndicator(occupant);

        if (!_occupantsByGrid.TryGetValue(grid, out var occupants))
        {
            occupants = new List<BattleOccupantInfo>();
            _occupantsByGrid[grid] = occupants;
        }

        occupants.Add(occupant);
        RegisterBattleDepthEntry(marker, grid, saveData.Category == CategorySiegeEngine ? BattleDepthRenderKind.SiegeEngine : BattleDepthRenderKind.Unit);
        return marker;
    }

    private static BattlePieceMarker? TryTakeReusableBattleMarker(BattleOccupantSaveData saveData, Dictionary<string, Queue<BattlePieceMarker>> reusableMarkersByStableId)
    {
        var stableId = BuildBattleOccupantStableId(saveData);
        if (!reusableMarkersByStableId.TryGetValue(stableId, out var markerQueue) || markerQueue.Count == 0)
        {
            return null;
        }

        return markerQueue.Dequeue();
    }

    private static Color GetSavedMarkerFillColor(BattleOccupantInfo occupant)
    {
        if (occupant.TeamName.Contains("Defender", StringComparison.OrdinalIgnoreCase))
        {
            return occupant.Category == CategorySiegeEngine ? new Color("31576f") : new Color("2d668d");
        }

        return occupant.Category == CategorySiegeEngine ? new Color("725137") : new Color("9d4d32");
    }

    private static Color GetSavedMarkerBorderColor(BattleOccupantInfo occupant)
    {
        return occupant.TeamName.Contains("Defender", StringComparison.OrdinalIgnoreCase)
            ? new Color("e0f0ff")
            : new Color("f0d6a8");
    }

    private static string GetIdleSceneForOccupant(BattleOccupantInfo occupant)
    {
        return occupant.TroopType switch
        {
            TroopInfantry => GetInfantryIdleScene(occupant.FacingDirection),
            TroopSpearman => GetSpearmanIdleScene(occupant.FacingDirection),
            TroopArcher or TroopCrossbow => GetArcherIdleScene(occupant.FacingDirection),
            TroopCavalry => GetCavalryIdleScene(occupant.FacingDirection),
            TroopWorker => GetWorkerIdleScene(occupant.FacingDirection),
            TroopRam => GetCarIdleScene(occupant.FacingDirection),
            TroopLadder => GetCarLadderIdleScene(occupant.FacingDirection),
            TroopCatapult => GetCatapultIdleScene(occupant.FacingDirection),
            TroopSupplyCart => GetSupplyCarIdleScene(occupant.FacingDirection),
            _ => GetInfantryIdleScene(occupant.FacingDirection)
        };
    }

    private void RecalculateBattleHudTotals()
    {
        _teamATotalTroops = 0;
        _teamBTotalTroops = 0;
        _teamASiegeUnits = 0;
        _teamBSiegeUnits = 0;
        _teamAGenerals = 0;
        _teamBGenerals = 0;

        foreach (var occupant in _occupantsByGrid.Values.SelectMany(static occupants => occupants))
        {
            ApplyTeamTroopDelta(occupant.Category, occupant.TeamName, occupant.TroopCount);
            ApplyTeamSiegeUnitDelta(occupant.Category, occupant.TeamName, 1);
            ApplyTeamGeneralDelta(occupant.Category, occupant.TeamName, occupant.OfficerName, 1);
        }
    }

    private void RestoreBattleTurnUsage(BattleSaveData saveData, Dictionary<string, BattlePieceMarker> markersByUnitId)
    {
        foreach (var unitId in saveData.StrategyUsedUnitIds ?? new List<string>())
        {
            if (markersByUnitId.TryGetValue(unitId, out var marker))
            {
                _strategyUsedByMarkerThisTurn.Add(marker);
            }
        }

        foreach (var unitId in saveData.SupplyUsedUnitIds ?? new List<string>())
        {
            if (markersByUnitId.TryGetValue(unitId, out var marker))
            {
                _supplyUsedByMarkerThisTurn.Add(marker);
            }
        }

        foreach (var unitId in saveData.ChargeUsedUnitIds ?? new List<string>())
        {
            if (markersByUnitId.TryGetValue(unitId, out var marker))
            {
                _chargeUsedByMarkerThisTurn.Add(marker);
            }
        }

        foreach (var ammo in saveData.WallTopAmmo ?? new List<BattleWallTopAmmoSaveData>())
        {
            if (markersByUnitId.TryGetValue(ammo.UnitId, out var marker))
            {
                _wallTopAttackAmmoByMarker[marker] = new WallTopAttackAmmo(ammo.DropStoneUses, ammo.PourOilUses);
            }
        }
    }

    private void RestoreBattleLogs(List<BattleLogSaveData>? logs)
    {
        _battleLogs.Clear();
        if (logs == null)
        {
            return;
        }

        foreach (var log in logs)
        {
            _battleLogs.Add(new BattleLogEntry(log.Turn, log.TeamName, log.Category, log.Message));
        }
    }

    private void RestoreBattleFires(List<BattleFireSaveData>? fires)
    {
        _activeFireByGrid.Clear();
        if (fires == null)
        {
            return;
        }

        foreach (var fire in fires)
        {
            var grid = new BattleGridKey(fire.X, fire.Y, fire.Level);
            if (!IsWithinMap(grid.Grid))
            {
                continue;
            }

            _activeFireByGrid[grid] = new BattleFireState(fire.RemainingTurns, fire.BurnTurns);
            RefreshFireVisual(grid);
        }
    }

    private void BuildEditorPreview()
    {
        if (_groundLayer == null || _objectLayer == null || _castleLayer == null || _overlayLayer == null)
        {
            return;
        }

        var scenarioDefinition = ResolveScenarioDefinition();
        _mapData = ShouldUseEditorAuthoredLayout() && HasEditorAuthoredLayout()
            ? BattleMapData.CreateFromTileMapLayers(_groundLayer, _moatLayer, _objectLayer, _castleLayer, _overlayLayer, scenarioDefinition)
            : BattleMapData.Create(scenarioDefinition);

        if (ShouldUseEditorAuthoredLayout() && HasEditorAuthoredLayout())
        {
            BattleTileMapBuilder.ConfigureLayer(_groundLayer, _mapData, BattleTileLayerKind.Ground);
            ConfigureOptionalTileMapLayer(_moatLayer, BattleTileLayerKind.Moat);
            BattleTileMapBuilder.ConfigureLayer(_objectLayer, _mapData, BattleTileLayerKind.Object);
            if (_objectLayer != null)
            {
                _objectLayer.Visible = true;
            }
            BattleTileMapBuilder.ConfigureLayer(_overlayLayer, _mapData, BattleTileLayerKind.DeploymentOverlay);
        }
        else
        {
            BattleTileMapBuilder.ConfigureLayer(_groundLayer, _mapData, BattleTileLayerKind.Ground);
            ConfigureOptionalTileMapLayer(_moatLayer, BattleTileLayerKind.Moat);
            BattleTileMapBuilder.ConfigureLayer(_objectLayer, _mapData, BattleTileLayerKind.Object);
            if (_objectLayer != null)
            {
                _objectLayer.Visible = true;
            }
            BattleTileMapBuilder.ConfigureLayer(_overlayLayer, _mapData, BattleTileLayerKind.DeploymentOverlay);
            BattleTileMapBuilder.ConfigureLayer(_castleLayer, _mapData, BattleTileLayerKind.Castle);
        }

        ClearCastleDepthVisuals();
        _castleLayer.Visible = true;
        RefreshBattleDepthLayerOrder();
    }

    private void CacheSceneNodes()
    {
        _mapRoot ??= GetNodeOrNull<Node2D>("MapRoot");
        _camera ??= GetNodeOrNull<Camera2D>("Camera2D");
        _groundLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/GroundLayer");
        _moatLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/MoatLayer");
        _objectLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/ObjectLayer");
        _castleLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/CastleLayer");
        _overlayLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/OverlayLayer");
        _highlightLayer ??= GetNodeOrNull<BattleHighlightRenderer>("MapRoot/HighlightLayer");
        _battleDepthLayer ??= GetNodeOrNull<Node2D>("MapRoot/BattleDepthLayer");
        _occludedUnitSilhouetteLayer ??= GetNodeOrNull<Node2D>("MapRoot/OccludedUnitSilhouetteLayer");
        if (_highlightLayer != null && !Engine.IsEditorHint())
        {
            _highlightLayer.Visible = false;
        }

        if (_battleDepthLayer == null && _mapRoot != null && !Engine.IsEditorHint())
        {
            _battleDepthLayer = new Node2D
            {
                Name = "BattleDepthLayer",
                ZIndex = 20
            };
            _mapRoot.AddChild(_battleDepthLayer);
        }

        if (_occludedUnitSilhouetteLayer == null && _mapRoot != null && !Engine.IsEditorHint())
        {
            _occludedUnitSilhouetteLayer = new Node2D
            {
                Name = "OccludedUnitSilhouetteLayer",
                ZIndex = 28
            };
            _mapRoot.AddChild(_occludedUnitSilhouetteLayer);
        }

        if (_occludedUnitSilhouetteLayer != null)
        {
            _occludedUnitSilhouetteLayer.ZIndex = 28;
        }

        if (_battleDepthLayer != null)
        {
            _battleDepthLayer.Visible = true;
            _battleDepthLayer.ZIndex = 20;
        }

        _commandMenu ??= GetNodeOrNull<Control>("UiLayer/CommandMenu");
        _battleLogPanel ??= GetNodeOrNull<Control>("UiLayer/BattleLogPanel");
        _battleLogHeaderRow ??= GetNodeOrNull<Control>("UiLayer/BattleLogPanel/Margin/LogContent/HeaderRow");
        _battleLogContent ??= GetNodeOrNull<Control>("UiLayer/BattleLogPanel/Margin/LogContent");
        _battleLogScroll ??= GetNodeOrNull<Control>("UiLayer/BattleLogPanel/Margin/LogContent/LogScroll");
        _battleLogResizeGrip ??= GetNodeOrNull<Control>("UiLayer/BattleLogPanel/ResizeGrip");
        _battleResultOverlay ??= GetNodeOrNull<Control>("UiLayer/BattleResultOverlay");
        _battleResultLabel ??= GetNodeOrNull<Label>("UiLayer/BattleResultOverlay/ResultPanel/Margin/ResultLabel");
        if (_battleResultOverlay != null)
        {
            _battleResultOverlay.ZIndex = 200;
        }
        _allLogButton ??= GetNodeOrNull<Button>("UiLayer/BattleLogPanel/Margin/LogContent/HeaderRow/AllLogButton");
        _selfLogButton ??= GetNodeOrNull<Button>("UiLayer/BattleLogPanel/Margin/LogContent/HeaderRow/SelfLogButton");
        _minimizeLogButton ??= GetNodeOrNull<Button>("UiLayer/BattleLogPanel/Margin/LogContent/HeaderRow/MinimizeLogButton");
        _battleLogLabel ??= GetNodeOrNull<Label>("UiLayer/BattleLogPanel/Margin/LogContent/LogScroll/LogLabel");
        _battleLogTitleLabel ??= GetNodeOrNull<Label>("UiLayer/BattleLogPanel/Margin/LogContent/HeaderRow/TitleLabel");
        _timeOfDayOverlay ??= GetNodeOrNull<ColorRect>("UiLayer/TimeOfDayOverlay");
        _weatherOverlay ??= GetNodeOrNull<ColorRect>("UiLayer/WeatherOverlay");
        _windowTitleLabel ??= GetNodeOrNull<Label>("UiLayer/CommandMenu/MenuMargin/MenuButtons/WindowTitleLabel");
        _unitMenuInfoLabel ??= GetNodeOrNull<Label>("UiLayer/CommandMenu/MenuMargin/MenuButtons/UnitMenuInfoLabel");
        _commandScroll ??= GetNodeOrNull<ScrollContainer>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll");
        _endTurnButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/EndTurnButton");
        _timeButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/TimeButton");
        _weatherButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/WeatherButton");
        _windButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/WindButton");
        _windPowerButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/WindPowerButton");
        _battleOptionButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/BattleOptionButton");
        _battleOptionOverlay ??= GetNodeOrNull<Control>("UiLayer/BattleOptionOverlay");
        if (_battleOptionOverlay != null)
        {
            _battleOptionOverlay.ZIndex = 190;
        }
        _battleOptionTitleLabel ??= GetNodeOrNull<Label>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/TitleBar/TitleLabel");
        _battleOptionCloseButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/TitleBar/CloseButton");
        _battleOptionSaveButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SaveLoadRow/SaveButton");
        _battleOptionLoadButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SaveLoadRow/LoadButton");
        _battleOptionLanguageButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/LanguageButton");
        _battleBgmToggleButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/BgmAudioRow/BgmToggleButton");
        _battleBgmVolumeSlider ??= GetNodeOrNull<HSlider>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/BgmAudioRow/BgmVolumeSlider");
        _battleBgmVolumeValueLabel ??= GetNodeOrNull<Label>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/BgmAudioRow/BgmVolumeValueLabel");
        _battleSfxToggleButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SfxAudioRow/SfxToggleButton");
        _battleSfxVolumeSlider ??= GetNodeOrNull<HSlider>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SfxAudioRow/SfxVolumeSlider");
        _battleSfxVolumeValueLabel ??= GetNodeOrNull<Label>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SfxAudioRow/SfxVolumeValueLabel");
        _battleSaveSettingsButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SaveSettingsButton");
        _moveButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/MoveButton");
        _attackButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/AttackButton");
        _unionAttackButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/UnionAttackButton");
        _chargeButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/ChargeButton");
        _duelButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/DuelButton");
        _retreatButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/RetreatButton");
        _hideButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/HideButton");
        _dropStoneButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/DropStoneButton");
        _pourOilButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/PourOilButton");
        _workButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/WorkButton");
        _installWoodFenceButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/InstallWoodFenceButton");
        _uninstallWoodFenceButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/UninstallWoodFenceButton");
        _supplyButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/SupplyButton");
        _resupplyWeaponButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/ResupplyWeaponButton");
        _captureSupplyCartButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/CaptureSupplyCartButton");
        _hireOfficerButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/HireOfficerButton");
        _strategyButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/StrategyButton");
        _openGateButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/OpenGateButton");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        ScreenshotShortcut.HandleInput(this, @event);
        if (GetViewport().IsInputHandled())
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton mouseButton:
                HandleMouseButton(mouseButton);
                break;
            case InputEventMouseMotion mouseMotion:
                HandleMouseMotion(mouseMotion);
                break;
        }
    }

    public override void _Input(InputEvent @event)
    {
        HandleBattleLogPanelInput(@event);
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
        {
            return;
        }

        UpdateHoverGrid();
        UpdateWeatherVisual(delta);
    }

    private void InitializeMapDataAndLayers()
    {
        if (_groundLayer == null || _objectLayer == null || _castleLayer == null || _overlayLayer == null)
        {
            return;
        }

        var scenarioDefinition = ResolveScenarioDefinition();

        if (ShouldUseEditorAuthoredLayout() && HasEditorAuthoredLayout())
        {
            _mapData = BattleMapData.CreateFromTileMapLayers(_groundLayer, _moatLayer, _objectLayer, _castleLayer, _overlayLayer, scenarioDefinition);
            BattleTileMapBuilder.ConfigureLayer(_groundLayer, _mapData, BattleTileLayerKind.Ground);
            ConfigureOptionalTileMapLayer(_moatLayer, BattleTileLayerKind.Moat);
            BattleTileMapBuilder.ConfigureLayer(_objectLayer, _mapData, BattleTileLayerKind.Object);
            if (_objectLayer != null)
            {
                _objectLayer.Visible = true;
            }
            BattleTileMapBuilder.ConfigureLayer(_overlayLayer, _mapData, BattleTileLayerKind.DeploymentOverlay);
            return;
        }

        _mapData = BattleMapData.Create(scenarioDefinition);
        ConfigureTileMapLayer("MapRoot/GroundLayer", BattleTileLayerKind.Ground);
        ConfigureTileMapLayer("MapRoot/MoatLayer", BattleTileLayerKind.Moat);
        ConfigureTileMapLayer("MapRoot/ObjectLayer", BattleTileLayerKind.Object);
        ConfigureTileMapLayer("MapRoot/CastleLayer", BattleTileLayerKind.Castle);
        ConfigureTileMapLayer("MapRoot/OverlayLayer", BattleTileLayerKind.DeploymentOverlay);
    }

    private bool HasEditorAuthoredLayout()
    {
        return (_groundLayer?.GetUsedCells().Count ?? 0) > 0 ||
               (_moatLayer?.GetUsedCells().Count ?? 0) > 0 ||
               (_objectLayer?.GetUsedCells().Count ?? 0) > 0 ||
               (_castleLayer?.GetUsedCells().Count ?? 0) > 0 ||
               (_overlayLayer?.GetUsedCells().Count ?? 0) > 0;
    }

    private bool ShouldUseEditorAuthoredLayout()
    {
        return UseEditorAuthoredLayout || IsNorthWestSceneVariant();
    }

    private void BakeBattleLayoutInEditor()
    {
        if (_groundLayer == null || _objectLayer == null || _castleLayer == null || _overlayLayer == null)
        {
            return;
        }

        _mapData = BattleMapData.Create(ResolveScenarioDefinition());
        BattleTileMapBuilder.ConfigureLayer(_groundLayer, _mapData, BattleTileLayerKind.Ground);
        ConfigureOptionalTileMapLayer(_moatLayer, BattleTileLayerKind.Moat);
        BattleTileMapBuilder.ConfigureLayer(_objectLayer, _mapData, BattleTileLayerKind.Object);
        if (_objectLayer != null)
        {
            _objectLayer.Visible = true;
        }
        BattleTileMapBuilder.ConfigureLayer(_castleLayer, _mapData, BattleTileLayerKind.Castle);
        BattleTileMapBuilder.ConfigureLayer(_overlayLayer, _mapData, BattleTileLayerKind.DeploymentOverlay);
    }

    private BattleScenarioDefinition ResolveScenarioDefinition()
    {
        if (TryResolveSceneScenarioDefinition(out var sceneScenarioDefinition))
        {
            return sceneScenarioDefinition;
        }

        if (ScenarioDefinition != null)
        {
            return ScenarioDefinition;
        }

        return BattleScenarioDefinition.CreateBuiltIn(ScenarioType);
    }

    private bool TryResolveSceneScenarioDefinition(out BattleScenarioDefinition scenarioDefinition)
    {
        scenarioDefinition = null!;
        var scenarioPath = ResolveSceneScenarioPath();
        if (string.IsNullOrWhiteSpace(scenarioPath) || !ResourceLoader.Exists(scenarioPath))
        {
            return false;
        }

        var loadedScenarioResource = GD.Load<Resource>(scenarioPath);
        if (loadedScenarioResource is BattleScenarioDefinition loadedScenarioDefinition)
        {
            scenarioDefinition = loadedScenarioDefinition;
            return true;
        }

        if (TryCreateKnownSceneScenarioDefinition(scenarioPath, out scenarioDefinition))
        {
            return true;
        }

        return false;
    }

    private static bool TryCreateKnownSceneScenarioDefinition(string scenarioPath, out BattleScenarioDefinition scenarioDefinition)
    {
        scenarioDefinition = null!;
        var isNorthWest = scenarioPath.Equals(NorthWestSiegeScenarioPath, StringComparison.OrdinalIgnoreCase) ||
                          scenarioPath.Equals(NorthWestMoatScenarioPath, StringComparison.OrdinalIgnoreCase);
        var isMoat = scenarioPath.Equals(NorthEastMoatScenarioPath, StringComparison.OrdinalIgnoreCase) ||
                     scenarioPath.Equals(NorthWestMoatScenarioPath, StringComparison.OrdinalIgnoreCase);

        if (!isNorthWest &&
            !scenarioPath.Equals(NorthEastSiegeScenarioPath, StringComparison.OrdinalIgnoreCase) &&
            !scenarioPath.Equals(NorthEastMoatScenarioPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        scenarioDefinition = BattleScenarioDefinition.CreateBuiltIn(isMoat
            ? BattleScenarioType.MoatSiegeBattle
            : BattleScenarioType.SiegeAssault);
        scenarioDefinition.DisplayName = isMoat
            ? $"Moat Siege Battle ({(isNorthWest ? "NW" : "NE")})"
            : $"Siege Battle ({(isNorthWest ? "NW" : "NE")})";
        scenarioDefinition.DefaultStructureFacing = isNorthWest
            ? BattleStructureFacing.NorthWest
            : BattleStructureFacing.NorthEast;
        scenarioDefinition.OpenGateForegroundSide = isNorthWest
            ? BattleGateForegroundSide.Left
            : BattleGateForegroundSide.Right;
        scenarioDefinition.Weather = isMoat ? BattleWeatherType.Cloudy : BattleWeatherType.Sunny;
        scenarioDefinition.WindDirection = isNorthWest
            ? BattleWindDirection.SouthWest
            : BattleWindDirection.SouthEast;
        scenarioDefinition.WindPower = BattleWindPower.Breeze;
        scenarioDefinition.TimeOfDay = BattleTimeOfDay.Morning;
        scenarioDefinition.UnitSpawnGrids = CreateKnownSceneUnitSpawnGrids(isNorthWest);
        return true;
    }

    private static Godot.Collections.Dictionary<string, Vector2I> CreateKnownSceneUnitSpawnGrids(bool isNorthWest)
    {
        if (isNorthWest)
        {
            return new Godot.Collections.Dictionary<string, Vector2I>
            {
                { "AttackerA", new Vector2I(20, 14) },
                { "AttackerB", new Vector2I(18, 12) },
                { "AttackerC", new Vector2I(20, 10) },
                { "AttackerWorker", new Vector2I(18, 14) },
                { "Catapult", new Vector2I(15, 10) },
                { "DefenderA", new Vector2I(7, 14) },
                { "DefenderB", new Vector2I(7, 12) },
                { "DefenderC", new Vector2I(7, 10) },
                { "Ladder", new Vector2I(15, 14) },
                { "Ram", new Vector2I(16, 12) },
                { "Spearman", new Vector2I(18, 16) },
                { "SupplyCart", new Vector2I(18, 15) },
                { "Worker", new Vector2I(5, 9) }
            };
        }

        return new Godot.Collections.Dictionary<string, Vector2I>
        {
            { "AttackerA", new Vector2I(10, 20) },
            { "AttackerB", new Vector2I(12, 18) },
            { "AttackerC", new Vector2I(14, 20) },
            { "AttackerWorker", new Vector2I(16, 20) },
            { "Catapult", new Vector2I(14, 15) },
            { "DefenderA", new Vector2I(10, 7) },
            { "DefenderB", new Vector2I(14, 7) },
            { "DefenderC", new Vector2I(12, 7) },
            { "Ladder", new Vector2I(10, 15) },
            { "Ram", new Vector2I(12, 16) },
            { "Spearman", new Vector2I(8, 18) },
            { "SupplyCart", new Vector2I(16, 19) },
            { "Worker", new Vector2I(16, 5) }
        };
    }

    private string? ResolveSceneScenarioPath()
    {
        if (IsNorthWestSceneVariant())
        {
            return ScenarioType switch
            {
                BattleScenarioType.SiegeAssault => NorthWestSiegeScenarioPath,
                BattleScenarioType.MoatSiegeBattle => NorthWestMoatScenarioPath,
                _ => null
            };
        }

        if (IsNorthEastSceneVariant())
        {
            return ScenarioType switch
            {
                BattleScenarioType.SiegeAssault => NorthEastSiegeScenarioPath,
                BattleScenarioType.MoatSiegeBattle => NorthEastMoatScenarioPath,
                _ => null
            };
        }

        return null;
    }

    private bool IsNorthWestSceneVariant()
    {
        var sceneFilePath = GetCurrentSceneFilePath();
        return !string.IsNullOrWhiteSpace(sceneFilePath) &&
               sceneFilePath.EndsWith(NorthWestScenePath, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsNorthEastSceneVariant()
    {
        var sceneFilePath = GetCurrentSceneFilePath();
        return !string.IsNullOrWhiteSpace(sceneFilePath) &&
               sceneFilePath.EndsWith(NorthEastScenePath, StringComparison.OrdinalIgnoreCase);
    }

    private string GetCurrentSceneFilePath()
    {
        // Tool-mode previews can run before the node is attached to a SceneTree.
        var sceneFilePath = GetTree()?.CurrentScene?.SceneFilePath;
        if (string.IsNullOrWhiteSpace(sceneFilePath))
        {
            sceneFilePath = SceneFilePath;
        }

        return sceneFilePath;
    }

    private void ClearTileLayoutInEditor()
    {
        ClearLayer(_groundLayer, BattleTileLayerKind.Ground);
        ClearLayer(_moatLayer, BattleTileLayerKind.Moat);
        ClearLayer(_objectLayer, BattleTileLayerKind.Object);
        ClearLayer(_castleLayer, BattleTileLayerKind.Castle);
        ClearLayer(_overlayLayer, BattleTileLayerKind.DeploymentOverlay);
    }

    private static void ClearLayer(TileMapLayer? layer, BattleTileLayerKind layerKind)
    {
        if (layer == null)
        {
            return;
        }

        BattleTileMapBuilder.AssignLayerTileSet(layer, layerKind);
        foreach (var coords in layer.GetUsedCells())
        {
            layer.EraseCell(coords);
        }

        layer.UpdateInternals();
    }

    private void ConfigureTileMapLayer(string path, BattleTileLayerKind layerKind)
    {
        var tileMapLayer = GetNodeOrNull<TileMapLayer>(path);
        if (tileMapLayer == null)
        {
            return;
        }

        BattleTileMapBuilder.ConfigureLayer(tileMapLayer, _mapData!, layerKind);
        if (layerKind == BattleTileLayerKind.Moat)
        {
            tileMapLayer.Visible = ScenarioType == BattleScenarioType.MoatSiegeBattle;
        }
    }

    private static void AssignOptionalTileMapLayer(TileMapLayer? layer, BattleTileLayerKind layerKind)
    {
        if (layer == null)
        {
            return;
        }

        BattleTileMapBuilder.AssignLayerTileSet(layer, layerKind);
    }

    private void ConfigureOptionalTileMapLayer(TileMapLayer? layer, BattleTileLayerKind layerKind)
    {
        if (layer == null || _mapData == null)
        {
            return;
        }

        BattleTileMapBuilder.ConfigureLayer(layer, _mapData, layerKind);
        if (layerKind == BattleTileLayerKind.Moat)
        {
            layer.Visible = ScenarioType == BattleScenarioType.MoatSiegeBattle;
        }
    }

    private void BuildCastleDepthVisuals()
    {
        ClearCastleDepthVisuals();
        if (_battleDepthLayer == null || _groundLayer == null || _mapData == null)
        {
            return;
        }

        if (_castleLayer != null)
        {
            _castleLayer.Visible = false;
        }

        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var cell = _mapData.GetCell(x, y);
                if (!BattleTileMapBuilder.TryGetCastleSpriteSpec(cell, out var spec))
                {
                    continue;
                }

                var sprite = CreateCastleDepthSprite(cell.Grid, spec);
                _battleDepthLayer.AddChild(sprite);
                _castleDepthSpritesByGrid[cell.Grid] = sprite;
                RegisterBattleDepthEntry(sprite, ToGroundGridKey(cell.Grid), BattleDepthRenderKind.CastleVisual);
            }
        }
    }

    private void ClearCastleDepthVisuals()
    {
        foreach (var sprite in _castleDepthSpritesByGrid.Values)
        {
            _battleDepthEntries.Remove(sprite);
            sprite.QueueFree();
        }

        _castleDepthSpritesByGrid.Clear();
    }

    private void ClearHighlightDepthVisuals()
    {
        foreach (var visual in _highlightDepthVisuals)
        {
            _battleDepthEntries.Remove(visual);
            visual.QueueFree();
        }

        _highlightDepthVisuals.Clear();
    }

    private Sprite2D CreateCastleDepthSprite(Vector2I grid, BattleTileMapBuilder.BattleTileSpriteSpec spec)
    {
        var sprite = new Sprite2D
        {
            Name = $"Castle_{grid.X}_{grid.Y}",
            Texture = spec.Texture,
            RegionEnabled = true,
            RegionRect = spec.Region,
            FlipH = spec.FlipHorizontally,
            Centered = false,
            Offset = -spec.Pivot,
            Position = GetCastleDepthSpritePosition(grid),
            ZIndex = 0
        };
        return sprite;
    }

    private Vector2 GetCastleDepthSpritePosition(Vector2I grid)
    {
        return _groundLayer?.MapToLocal(grid) ?? BattleMapRenderer.GridToWorld(grid);
    }

    private void RefreshCastleDepthVisual(Vector2I grid)
    {
        if (_battleDepthLayer == null || _mapData == null || !IsWithinMap(grid))
        {
            return;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (!BattleTileMapBuilder.TryGetCastleSpriteSpec(cell, out var spec))
        {
            if (_castleDepthSpritesByGrid.Remove(grid, out var removedSprite))
            {
                _battleDepthEntries.Remove(removedSprite);
                removedSprite.QueueFree();
            }

            RefreshBattleDepthLayerOrder();
            return;
        }

        if (!_castleDepthSpritesByGrid.TryGetValue(grid, out var sprite))
        {
            sprite = CreateCastleDepthSprite(grid, spec);
            _battleDepthLayer.AddChild(sprite);
            _castleDepthSpritesByGrid[grid] = sprite;
        }
        else
        {
            sprite.Texture = spec.Texture;
            sprite.RegionRect = spec.Region;
            sprite.FlipH = spec.FlipHorizontally;
            sprite.Offset = -spec.Pivot;
            sprite.Position = GetCastleDepthSpritePosition(grid);
        }

        RegisterBattleDepthEntry(sprite, ToGroundGridKey(grid), BattleDepthRenderKind.CastleVisual);
        RefreshBattleDepthLayerOrder();
    }

    private void RegisterBattleDepthEntry(Node2D node, BattleGridKey grid, BattleDepthRenderKind kind)
    {
        if (_battleDepthLayer != null && node.GetParent() != _battleDepthLayer)
        {
            node.Reparent(_battleDepthLayer);
        }

        node.ZIndex = 0;
        _battleDepthEntries[node] = new BattleDepthEntry(node, grid, kind, GetBattleDepthLocalOrder(kind));
    }

    private void RefreshBattleDepthLayerOrder()
    {
        if (_battleDepthLayer == null)
        {
            return;
        }

        var sortedEntries = _battleDepthEntries.Values
            .Where(entry => GodotObject.IsInstanceValid(entry.Node) && entry.Node.GetParent() == _battleDepthLayer)
            .OrderBy(entry => entry, Comparer<BattleDepthEntry>.Create(CompareBattleDepthEntries))
            .ToList();

        for (var index = 0; index < sortedEntries.Count; index++)
        {
            _battleDepthLayer.MoveChild(sortedEntries[index].Node, index);
        }
    }

    private static int CompareBattleDepthEntries(BattleDepthEntry left, BattleDepthEntry right)
    {
        var leftBand = GetBattleDepthRenderBand(left.Kind);
        var rightBand = GetBattleDepthRenderBand(right.Kind);
        if (leftBand != rightBand)
        {
            return leftBand.CompareTo(rightBand);
        }

        var leftDepth = GetBattleDepth(left.Grid);
        var rightDepth = GetBattleDepth(right.Grid);
        if (leftDepth != rightDepth)
        {
            return leftDepth.CompareTo(rightDepth);
        }

        if (left.LocalOrder != right.LocalOrder)
        {
            return left.LocalOrder.CompareTo(right.LocalOrder);
        }

        if (left.Grid.Y != right.Grid.Y)
        {
            return left.Grid.Y.CompareTo(right.Grid.Y);
        }

        return left.Grid.X.CompareTo(right.Grid.X);
    }

    private static int GetBattleDepthRenderBand(BattleDepthRenderKind kind)
    {
        return kind switch
        {
            BattleDepthRenderKind.CastleVisual => 0,
            BattleDepthRenderKind.MoveHighlight or
            BattleDepthRenderKind.AttackHighlight or
            BattleDepthRenderKind.SelectedHighlight => 2,
            BattleDepthRenderKind.SiegeEngine or
            BattleDepthRenderKind.Unit => 3,
            BattleDepthRenderKind.FireEffect => 4,
            _ => 0
        };
    }

    private static int GetBattleDepth(BattleGridKey grid)
    {
        return grid.X + grid.Y + GetBattleLevelDepthOffset(grid.Level);
    }

    private static int GetBattleLevelDepthOffset(int level)
    {
        return level * WallTopLevelDepthOffset;
    }

    private static int GetBattleDepthLocalOrder(BattleDepthRenderKind kind)
    {
        return kind switch
        {
            BattleDepthRenderKind.CastleVisual => 0,
            BattleDepthRenderKind.MoveHighlight => 4,
            BattleDepthRenderKind.AttackHighlight => 5,
            BattleDepthRenderKind.SelectedHighlight => 6,
            BattleDepthRenderKind.SiegeEngine => 10,
            BattleDepthRenderKind.Unit => 20,
            BattleDepthRenderKind.FireEffect => 30,
            _ => 0
        };
    }

    private void RefreshOccludedUnitSilhouettes()
    {
        ClearOccludedUnitSilhouettes();
        if (_occludedUnitSilhouetteLayer == null || _mapData == null)
        {
            return;
        }

        foreach (var (grid, occupants) in _occupantsByGrid)
        {
            if (!IsUnitOccludedByCastleVisual(grid))
            {
                continue;
            }

            var occupant = occupants.FirstOrDefault(candidate =>
                candidate.Marker != null &&
                IsBattlePiece(candidate) &&
                IsVisibleToCurrentTurnSide(candidate));
            if (occupant?.Marker == null)
            {
                continue;
            }

            occupant.Marker.Visible = false;
            var silhouette = occupant.Marker.CreateSilhouetteVisual(GetOccludedUnitSilhouetteColor(occupant));
            if (silhouette == null)
            {
                ApplyHiddenMarkerVisibility(occupant);
                continue;
            }

            silhouette.Name = $"Occluded_{occupant.ShortLabel}_{grid.X}_{grid.Y}_L{grid.Level}";
            silhouette.Position = GetMarkerPosition(grid);
            _occludedUnitSilhouetteLayer.AddChild(silhouette);
            _occludedUnitSilhouettesByGrid[grid] = silhouette;
        }
    }

    private void ClearOccludedUnitSilhouettes()
    {
        RestoreOccludedMarkerVisibility();
        foreach (var silhouette in _occludedUnitSilhouettesByGrid.Values)
        {
            silhouette.QueueFree();
        }

        _occludedUnitSilhouettesByGrid.Clear();
    }

    private void RestoreOccludedMarkerVisibility()
    {
        foreach (var occupants in _occupantsByGrid.Values)
        {
            foreach (var occupant in occupants)
            {
                if (occupant.Marker != null && IsBattlePiece(occupant))
                {
                    ApplyHiddenMarkerVisibility(occupant);
                }
            }
        }
    }

    private bool IsUnitOccludedByCastleVisual(BattleGridKey grid)
    {
        if (_mapData == null || !IsWithinMap(grid.Grid))
        {
            return false;
        }

        if (grid.Level != 0)
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (cell.Structure == BattleStructureType.Gate)
        {
            if (cell.IsBroken)
            {
                return false;
            }

            return cell.IsGateOpen
                ? cell.HideGroundOccupantWhenGateOpen
                : cell.HideGroundOccupantWithForeground;
        }

        return cell.HideGroundOccupantWithForeground;
    }

    private static Color GetOccludedUnitSilhouetteColor(BattleOccupantInfo occupant)
    {
        return IsAttackerPiece(occupant)
            ? new Color(1.0f, 0.18f, 0.10f, 0.42f)
            : new Color(0.25f, 0.62f, 1.0f, 0.42f);
    }

    private void PopulateMarkers()
    {
        CreateMarker("MapRoot/UnitLayer/AttackerA", ResolveUnitSpawnGrid("AttackerA", new Vector2I(10, 20)), "I", "Attacker Infantry A", CategoryUnit, "Team A / Attacker", "Xiahou Yuan", TroopInfantry, 6200, new Color("ad4832"), new Color("f0d6a8"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Spearman", ResolveUnitSpawnGrid("Spearman", new Vector2I(8, 18)), "S", "Attacker Spearman", CategoryUnit, "Team A / Attacker", "Cao Hong", TroopSpearman, 4200, new Color("9b5931"), new Color("f0d6a8"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/AttackerB", ResolveUnitSpawnGrid("AttackerB", new Vector2I(12, 18)), "A", "Attacker Archer B", CategoryUnit, "Team A / Attacker", "Zhang He", TroopArcher, 5400, new Color("b96d2c"), new Color("f0d6a8"), moveRange: 4, attackRange: 3);
        CreateMarker("MapRoot/UnitLayer/AttackerC", ResolveUnitSpawnGrid("AttackerC", new Vector2I(14, 20)), "C", "Attacker Cavalry C", CategoryUnit, "Team A / Attacker", "Cao Chun", TroopCavalry, 4800, new Color("8f3f31"), new Color("f0d6a8"), moveRange: 6, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/AttackerWorker", ResolveUnitSpawnGrid("AttackerWorker", new Vector2I(16, 20)), "W", "Attacker Worker", CategoryUnit, "Team A / Attacker", "Worker", TroopWorker, 1800, new Color("715137"), new Color("f0d6a8"), moveRange: 3, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Ram", ResolveUnitSpawnGrid("Ram", new Vector2I(12, 16)), "R", "Battering Ram", CategorySiegeEngine, "Team A / Attacker", string.Empty, TroopRam, RamMaxHitPoints, new Color("7a4a20"), new Color("ead7aa"), 21.0f, moveRange: 3, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Ladder", ResolveUnitSpawnGrid("Ladder", new Vector2I(10, 15)), "L", "Siege Ladder", CategorySiegeEngine, "Team A / Attacker", string.Empty, TroopLadder, LadderMaxHitPoints, new Color("8c7b44"), new Color("ead7aa"), 21.0f, moveRange: 3, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Catapult", ResolveUnitSpawnGrid("Catapult", new Vector2I(14, 15)), "T", "Catapult", CategorySiegeEngine, "Team A / Attacker", string.Empty, TroopCatapult, CatapultMaxHitPoints, new Color("6e5131"), new Color("ead7aa"), 21.0f, moveRange: 2, attackRange: 4);
        CreateMarker("MapRoot/UnitLayer/SupplyCart", ResolveUnitSpawnGrid("SupplyCart", new Vector2I(16, 19)), "糧", "Supply Cart", CategorySiegeEngine, "Team A / Attacker", string.Empty, TroopSupplyCart, SupplyCartMaxHitPoints, new Color("6d5a2d"), new Color("f1df9b"), 21.0f, moveRange: 3, attackRange: 0);

        CreateMarker("MapRoot/UnitLayer/DefenderA", ResolveUnitSpawnGrid("DefenderA", new Vector2I(10, 7)), "D", "Defender Infantry A", CategoryUnit, "Team B / Defender", "Dong Zhuo", TroopInfantry, 5100, new Color("326b8d"), new Color("e0f0ff"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/DefenderB", ResolveUnitSpawnGrid("DefenderB", new Vector2I(14, 7)), "X", "Defender Crossbow B", CategoryUnit, "Team B / Defender", "Li Jue", TroopArcher, 4300, new Color("245f76"), new Color("e0f0ff"), moveRange: 4, attackRange: 3);
        CreateMarker("MapRoot/UnitLayer/DefenderC", ResolveUnitSpawnGrid("DefenderC", new Vector2I(12, 7)), "G", "Defender Commander", CategoryUnit, "Team B / Defender", "Guo Si", TroopSpearman, 3100, new Color("274e8a"), new Color("e0f0ff"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Worker", ResolveUnitSpawnGrid("Worker", new Vector2I(16, 5)), "W", "Defender Worker", CategoryUnit, "Team B / Defender", "Worker", TroopWorker, 1800, new Color("5f583e"), new Color("e8ddbc"), moveRange: 3, attackRange: 1);
    }

    private Vector2I ResolveUnitSpawnGrid(string unitKey, Vector2I fallbackGrid)
    {
        var scenarioDefinition = ResolveScenarioDefinition();
        if (scenarioDefinition.UnitSpawnGrids.TryGetValue(unitKey, out var configuredGrid))
        {
            return configuredGrid;
        }

        return fallbackGrid;
    }

    private void CreateMarker(string path, Vector2I grid, string label, string displayName, string category, string teamName, string officerName, string troopType, int troopCount, Color fillColor, Color borderColor, float radius = 19.0f, int moveRange = 0, int attackRange = 1)
    {
        var marker = GetNodeOrNull<BattlePieceMarker>(path);
        if (marker == null)
        {
            return;
        }

        var gridKey = GetDefaultGridKey(grid);
        marker.Position = GetMarkerPosition(gridKey);
        marker.Setup(label, fillColor, borderColor, radius);
        marker.SetupNamePlate(string.IsNullOrWhiteSpace(officerName) ? displayName : officerName);
        marker.SetupTeamArrow(GetTeamArrowColor(teamName));
        if (category == CategoryUnit)
        {
            marker.SetupTroopSegmentBar(troopCount, 0, troopCount);
        }
        else
        {
            marker.SetupHealthBar(troopCount, troopCount);
        }
        if (category == CategoryUnit && troopType == TroopInfantry)
        {
            marker.SetupSpriteAnimationScene(GetInitialInfantryDirectionScene(teamName));
        }
        else if (category == CategoryUnit && troopType == TroopSpearman)
        {
            marker.SetupSpriteAnimationScene(SpearmanIdleSouthEastScenePath);
        }
        else if (category == CategoryUnit && troopType == TroopArcher)
        {
            marker.SetupSpriteAnimationScene(ArcherIdleSouthEastScenePath);
        }
        else if (category == CategoryUnit && troopType == TroopCavalry)
        {
            marker.SetupSpriteAnimationScene(CavalryIdleSouthEastScenePath);
        }
        else if (category == CategoryUnit && troopType == TroopWorker)
        {
            marker.SetupSpriteAnimationScene(WorkerIdleSouthEastScenePath);
        }
        else if (category == CategorySiegeEngine && troopType == TroopRam)
        {
            marker.SetupSpriteAnimationScene(CarIdleSouthEastScenePath);
        }
        else if (category == CategorySiegeEngine && troopType == TroopLadder)
        {
            marker.SetupSpriteAnimationScene(CarLadderIdleSouthEastScenePath);
        }
        else if (category == CategorySiegeEngine && troopType == TroopCatapult)
        {
            marker.SetupSpriteAnimationScene(CatapultIdleSouthEastScenePath);
        }
        else if (category == CategorySiegeEngine && troopType == TroopSupplyCart)
        {
            marker.SetupSpriteAnimationScene(SupplyCarIdleSouthEastScenePath);
        }

        RegisterOccupant(gridKey, displayName, category, label, teamName, officerName, troopType, troopCount, moveRange, attackRange, marker);
        ApplyTeamTroopDelta(category, teamName, troopCount);
        ApplyTeamSiegeUnitDelta(category, teamName, 1);
        ApplyTeamGeneralDelta(category, teamName, officerName, 1);
        RegisterBattleDepthEntry(marker, gridKey, category == CategorySiegeEngine ? BattleDepthRenderKind.SiegeEngine : BattleDepthRenderKind.Unit);
    }

    private static Color GetTeamArrowColor(string teamName)
    {
        return teamName.Contains("Attacker")
            ? new Color(1.0f, 0.18f, 0.12f, 0.96f)
            : new Color(0.18f, 0.58f, 1.0f, 0.96f);
    }

    private Vector2 GetMarkerPosition(Vector2I grid)
    {
        return GetMarkerPosition(GetDefaultGridKey(grid));
    }

    private Vector2 GetMarkerPosition(BattleGridKey gridKey)
    {
        if (gridKey.Level == 2)
        {
            return GetWallTopAnchorPosition(gridKey);
        }

        var grid = gridKey.Grid;
        var gridCenter = _groundLayer?.MapToLocal(grid) ?? BattleMapRenderer.GridToWorld(grid);
        return gridCenter + new Vector2(0.0f, GetUnitVisualLift(gridKey));
    }

    private float GetUnitVisualLift(Vector2I grid)
    {
        return GetUnitVisualLift(GetDefaultGridKey(grid));
    }

    private float GetUnitVisualLift(BattleGridKey gridKey)
    {
        if (gridKey.Level == 2)
        {
            return WallWalkUnitVisualLift;
        }

        return DefaultUnitVisualLift;
    }

    private Vector2 GetHighlightPosition(Vector2I grid)
    {
        return GetHighlightPosition(GetDefaultGridKey(grid));
    }

    private Vector2 GetHighlightPosition(BattleGridKey gridKey)
    {
        if (gridKey.Level == 2)
        {
            return GetWallTopAnchorPosition(gridKey);
        }

        var grid = gridKey.Grid;
        var gridCenter = _groundLayer?.MapToLocal(grid) ?? BattleMapRenderer.GridToWorld(grid);
        return gridCenter;
    }

    private Vector2 GetWallTopAnchorPosition(BattleGridKey gridKey)
    {
        var grid = gridKey.Grid;
        var gridCenter = _groundLayer?.MapToLocal(grid) ?? BattleMapRenderer.GridToWorld(grid);
        return gridCenter + GetWallTopHighlightOffset() + new Vector2(0.0f, WallWalkHighlightVisualLift);
    }

    private Vector2 GetWallTopHighlightOffset()
    {
        if (IsNorthWestSceneVariant())
        {
            return NorthWestWallTopHighlightOffset;
        }

        return NorthEastWallTopHighlightOffset;
    }

    private void ConfigureHud()
    {
        var titleLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/TitleLabel");
        var summaryLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/SummaryLabel");
        var teamBLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/TeamBLabel");
        var coordinateLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/CoordinateLabel");

        var scenarioName = ResolveScenarioDefinition().DisplayName;
        if (titleLabel != null)
        {
            titleLabel.Text = BattleFormat(
                "ui.battle.title",
                "Scenario: {0}   Date: {1}   Turn: {2}   Acting Side: {3}",
                scenarioName,
                BattleDateText,
                _turnNumber,
                FormatTeamName(GetCurrentTurnSideName()));
        }

        if (_timeButton != null)
        {
            _timeButton.Text = FormatBattleTimeOfDay(GetCurrentBattleTimeOfDay());
        }

        if (_weatherButton != null)
        {
            _weatherButton.Text = FormatBattleWeather(GetCurrentBattleWeather());
        }

        if (_windButton != null)
        {
            _windButton.Text = FormatBattleWindDirectionShort(GetCurrentBattleWindDirection());
        }

        if (_windPowerButton != null)
        {
            _windPowerButton.Text = FormatBattleWindPower(GetCurrentBattleWindPower());
        }

        if (_endTurnButton != null)
        {
            _endTurnButton.Text = BattleText("ui.battle.end_turn", "End Turn");
        }

        if (_battleOptionButton != null)
        {
            _battleOptionButton.Text = BattleText("ui.option", "Option");
        }

        if (_windowTitleLabel != null)
        {
            _windowTitleLabel.Text = BattleText("ui.battle.window_title", "Unit Command");
        }

        RefreshBattleOptionDialogText();

        if (summaryLabel != null)
        {
            summaryLabel.Text = BuildTeamHudText(TeamAInfo with { TotalTroops = _teamATotalTroops, WoundedTroops = GetTotalWoundedTroopsForTeam(TeamAInfo.Name), TotalSiegeUnits = _teamASiegeUnits, TotalGenerals = _teamAGenerals, StrategyPlans = _teamAStrategyPlans, TotalGold = _teamAGold, TotalFood = _teamAFood });
        }

        if (teamBLabel != null)
        {
            teamBLabel.Text = BuildTeamHudText(TeamBInfo with { TotalTroops = _teamBTotalTroops, WoundedTroops = GetTotalWoundedTroopsForTeam(TeamBInfo.Name), TotalSiegeUnits = _teamBSiegeUnits, TotalGenerals = _teamBGenerals, StrategyPlans = _teamBStrategyPlans, TotalGold = _teamBGold, TotalFood = _teamBFood });
        }

        if (coordinateLabel != null)
        {
            coordinateLabel.Text = BuildCoordinateText();
        }

        RefreshHiddenUnitVisibility();
        RefreshInfoPanel();
        RefreshBattleResultState();
    }

    private bool IsForestGrid(BattleGridKey grid)
    {
        return _mapData != null &&
               grid.Level == 0 &&
               IsWithinMap(grid.Grid) &&
               _mapData.GetCell(grid.X, grid.Y).Terrain == BattleTerrainType.Forest;
    }

    private bool CanHideAtGrid(BattleGridKey grid, BattleOccupantInfo unit)
    {
        return IsBattlePiece(unit) &&
               !unit.IsHidden &&
               IsForestGrid(grid);
    }

    private bool CanHideSelectedUnit()
    {
        return _selectedUnit != null &&
               _selectedUnitGrid.HasValue &&
               CanHideAtGrid(_selectedUnitGrid.Value, _selectedUnit);
    }

    private bool IsHiddenFromSide(BattleOccupantInfo occupant, string viewerTeamName)
    {
        return occupant.IsHidden && occupant.TeamName != viewerTeamName;
    }

    private bool IsVisibleToCurrentTurnSide(BattleOccupantInfo occupant)
    {
        return !IsHiddenFromSide(occupant, GetCurrentTurnSideName());
    }

    private void RefreshHiddenUnitVisibility()
    {
        foreach (var occupants in _occupantsByGrid.Values)
        {
            foreach (var occupant in occupants)
            {
                ApplyHiddenMarkerVisibility(occupant);
            }
        }
    }

    private void ApplyHiddenMarkerVisibility(BattleOccupantInfo occupant)
    {
        if (occupant.Marker == null || !IsBattlePiece(occupant))
        {
            return;
        }

        var visible = IsVisibleToCurrentTurnSide(occupant);
        occupant.Marker.Visible = visible;
        occupant.Marker.SetHiddenBodyVisual(visible && occupant.IsHidden);
    }

    private void RefreshBattleResultState()
    {
        if (!TryBuildBattleResultMessage(out var resultMessage))
        {
            _isBattleFinished = false;
            if (_battleResultOverlay != null)
            {
                _battleResultOverlay.Visible = false;
            }
            if (_endTurnButton != null)
            {
                _endTurnButton.Disabled = false;
            }
            return;
        }

        var wasFinished = _isBattleFinished;
        _isBattleFinished = true;
        HideCommandMenu();
        CancelCommandAction(clearSelection: false);
        if (_battleResultLabel != null)
        {
            _battleResultLabel.Text = resultMessage;
        }
        if (_battleResultOverlay != null)
        {
            _battleResultOverlay.Visible = true;
        }
        if (_endTurnButton != null)
        {
            _endTurnButton.Disabled = true;
        }
        if (!wasFinished)
        {
            AppendBattleLog("Battle", "Result", resultMessage.Replace('\n', ' '));
        }
    }

    private bool TryBuildBattleResultMessage(out string resultMessage)
    {
        resultMessage = string.Empty;
        if (_mapData == null || _occupantsByGrid.Count == 0)
        {
            return false;
        }

        var teamAHasGeneral = _teamAGenerals > 0;
        var teamBHasGeneral = _teamBGenerals > 0;
        if (teamAHasGeneral && teamBHasGeneral)
        {
            return false;
        }

        if (!teamAHasGeneral && !teamBHasGeneral)
        {
            resultMessage = "Battle Finished\nDraw\nBoth sides have no generals.";
            return true;
        }

        var winnerName = teamAHasGeneral ? TeamAInfo.Name : TeamBInfo.Name;
        var defeatedName = teamAHasGeneral ? TeamBInfo.Name : TeamAInfo.Name;
        resultMessage = $"Battle Finished\n{winnerName} Victory\n{defeatedName} has no generals.";
        return true;
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (_isBattleFinished)
        {
            HideCommandMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
        {
            UpdateHoverGrid();
            _selectedGrid = _hoverGrid;
            _selectedGridKey = _hoverGridKey;
            if (_commandMode == BattleCommandMode.MoveSelect)
            {
                if (!TryMoveSelectedUnit())
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            if (_commandMode == BattleCommandMode.AttackSelect)
            {
                if (!TryAttackSelectedTarget())
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            if (_commandMode == BattleCommandMode.StrategySelect)
            {
                if (!TryExecuteSelectedStrategy())
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            if (_commandMode == BattleCommandMode.DuelSelect)
            {
                if (!TryExecuteSelectedDuel())
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            if (_commandMode == BattleCommandMode.ChargeSelect)
            {
                if (!TryExecuteSelectedCharge())
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            if (_commandMode == BattleCommandMode.HireOfficerSelect)
            {
                if (!TryExecuteSelectedHireOfficer())
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            if (_commandMode == BattleCommandMode.WorkSelect)
            {
                if (!TryPerformWorkerWork())
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            UpdateUnitSelection();
            if (_selectedUnit != null)
            {
                ShowCommandMenu(mouseButton.Position);
            }
            else
            {
                HideCommandMenu();
            }

            RefreshCoordinateLabel();
            RefreshInfoPanel();
            RefreshHighlights();
            return;
        }

        if (mouseButton.ButtonIndex != MouseButton.Right)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left && !mouseButton.Pressed)
            {
                _isDraggingBattleLog = false;
                _isResizingBattleLog = false;
            }

            return;
        }

        if (mouseButton.Pressed)
        {
            _isDraggingMap = true;
            _lastMousePosition = mouseButton.Position;
            GetViewport().SetInputAsHandled();
            return;
        }

        _isDraggingMap = false;
    }

    private void HandleMouseMotion(InputEventMouseMotion mouseMotion)
    {
        if (_isDraggingCommandMenu && _commandMenu != null)
        {
            _commandMenu.Position = ClampCommandMenuPosition(mouseMotion.GlobalPosition - _commandMenuDragOffset);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_isDraggingMap || _mapRoot == null)
        {
            return;
        }

        var delta = mouseMotion.Position - _lastMousePosition;
        _lastMousePosition = mouseMotion.Position;
        _mapRoot.Position = GetClampedMapPosition(_mapRoot.Position + delta);
        GetViewport().SetInputAsHandled();
    }

    private Vector2 GetClampedMapPosition(Vector2 position)
    {
        var mapBounds = GetMapBounds();
        var visibleRect = GetVisibleWorldRect();

        return new Vector2(
            ClampAxis(position.X, mapBounds.Position.X, mapBounds.End.X, visibleRect.Position.X, visibleRect.End.X),
            ClampAxis(position.Y, mapBounds.Position.Y, mapBounds.End.Y, visibleRect.Position.Y, visibleRect.End.Y));
    }

    private void UpdateHoverGrid()
    {
        if (_groundLayer == null || _mapData == null)
        {
            return;
        }

        var localMouse = _groundLayer.ToLocal(GetGlobalMousePosition());
        var newHoverGridKey = ResolvePointerGridKey(localMouse);
        var newHoverGrid = newHoverGridKey?.Grid;
        if (_hoverGrid == newHoverGrid && _hoverGridKey == newHoverGridKey)
        {
            return;
        }

        _hoverGrid = newHoverGrid;
        _hoverGridKey = newHoverGridKey;
        RefreshCoordinateLabel();
    }

    private BattleGridKey? ResolvePointerGridKey(Vector2 localMouse)
    {
        if (_groundLayer == null || _mapData == null)
        {
            return null;
        }

        var highlightedGrid = ResolvePointerHighlightedGrid(localMouse);
        if (highlightedGrid.HasValue)
        {
            return highlightedGrid.Value;
        }

        var occludedUnitGrid = ResolvePointerOccludedUnitSilhouetteGridKey(localMouse);
        if (occludedUnitGrid.HasValue)
        {
            return occludedUnitGrid.Value;
        }

        var groundCandidate = _groundLayer.LocalToMap(localMouse);
        var layeredGrid = ResolvePointerLayeredGridKey(localMouse, groundCandidate);
        if (layeredGrid.HasValue)
        {
            return layeredGrid.Value;
        }

        var markerGridKey = ResolvePointerMarkerGridKey(localMouse);
        if (markerGridKey.HasValue)
        {
            return markerGridKey.Value;
        }

        return IsWithinMap(groundCandidate) ? GetDefaultGridKey(groundCandidate) : null;
    }

    private BattleGridKey? ResolvePointerLayeredGridKey(Vector2 localMouse, Vector2I groundCandidate)
    {
        if (_groundLayer == null || !IsWithinMap(groundCandidate) || !IsGateGrid(groundCandidate))
        {
            return null;
        }

        var groundKey = ToGroundGridKey(groundCandidate);
        var wallTopKey = ToWallWalkGridKey(groundCandidate);
        var inGroundDiamond = PointInDiamond(localMouse, GetHighlightPosition(groundKey), 46.0f, 24.0f);
        var inWallTopDiamond = PointInDiamond(localMouse, GetHighlightPosition(wallTopKey), 46.0f, 24.0f);
        if (inGroundDiamond && !inWallTopDiamond)
        {
            return groundKey;
        }

        if (inWallTopDiamond && !inGroundDiamond)
        {
            return wallTopKey;
        }

        if (inGroundDiamond && inWallTopDiamond)
        {
            var groundDistance = localMouse.DistanceSquaredTo(GetHighlightPosition(groundKey));
            var wallTopDistance = localMouse.DistanceSquaredTo(GetHighlightPosition(wallTopKey));
            return groundDistance <= wallTopDistance ? groundKey : wallTopKey;
        }

        return null;
    }

    private BattleGridKey? ResolvePointerHighlightedGrid(Vector2 localMouse)
    {
        var activeGrids = _commandMode switch
        {
            BattleCommandMode.MoveSelect => _movableGrids,
            BattleCommandMode.AttackSelect => _attackableGrids,
            BattleCommandMode.WorkSelect => _workableGrids,
            BattleCommandMode.StrategySelect => _strategyTargetGrids,
            BattleCommandMode.DuelSelect => _duelTargetGrids,
            BattleCommandMode.ChargeSelect => _chargeTargetGrids,
            BattleCommandMode.HireOfficerSelect => _hireOfficerTargetGrids,
            _ => null
        };

        if (activeGrids == null)
        {
            return null;
        }

        var highlightedCandidates = activeGrids
            .Where(grid => PointInDiamond(localMouse, GetHighlightPosition(grid), 46.0f, 24.0f))
            .ToList();
        if (highlightedCandidates.Count == 0)
        {
            return null;
        }

        if (highlightedCandidates.Count == 1)
        {
            return highlightedCandidates[0];
        }

        var sharedGrid = highlightedCandidates[0].Grid;
        if (highlightedCandidates.All(grid => grid.Grid == sharedGrid))
        {
            var layeredGrid = ResolvePointerLayeredGridKey(localMouse, sharedGrid);
            if (layeredGrid.HasValue && highlightedCandidates.Contains(layeredGrid.Value))
            {
                return layeredGrid.Value;
            }
        }

        return highlightedCandidates
            .OrderBy(grid => localMouse.DistanceSquaredTo(GetHighlightPosition(grid)))
            .ThenByDescending(grid => grid.Level)
            .First();
    }

    private static bool PointInDiamond(Vector2 point, Vector2 center, float halfWidth, float halfHeight)
    {
        var dx = Mathf.Abs(point.X - center.X) / halfWidth;
        var dy = Mathf.Abs(point.Y - center.Y) / halfHeight;
        return dx + dy <= 1.0f;
    }

    private BattleGridKey? ResolvePointerMarkerGridKey(Vector2 localMouse)
    {
        foreach (var (grid, occupants) in _occupantsByGrid)
        {
            foreach (var occupant in occupants)
            {
                if (occupant.Marker == null || !IsBattlePiece(occupant))
                {
                    continue;
                }

                var markerRadius = Mathf.Max(20.0f, occupant.Marker.Radius + 8.0f);
                if (localMouse.DistanceTo(occupant.Marker.Position) <= markerRadius)
                {
                    return grid;
                }
            }
        }

        return null;
    }

    private BattleGridKey? ResolvePointerOccludedUnitSilhouetteGridKey(Vector2 localMouse)
    {
        if (_commandMode is BattleCommandMode.MoveSelect or BattleCommandMode.AttackSelect or BattleCommandMode.StrategySelect or BattleCommandMode.DuelSelect or BattleCommandMode.ChargeSelect or BattleCommandMode.HireOfficerSelect)
        {
            return null;
        }

        foreach (var (grid, silhouette) in _occludedUnitSilhouettesByGrid)
        {
            if (!GodotObject.IsInstanceValid(silhouette))
            {
                continue;
            }

            if (localMouse.DistanceTo(silhouette.Position) <= 32.0f)
            {
                return grid;
            }
        }

        return null;
    }

    private bool IsWithinMap(Vector2I grid)
    {
        return grid.X >= 0 &&
               grid.X < BattleMapData.Width &&
               grid.Y >= 0 &&
               grid.Y < BattleMapData.Height;
    }

    private BattleGridKey GetDefaultGridKey(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return ToGroundGridKey(grid);
        }

        return IsWallTopGrid(grid)
            ? ToWallWalkGridKey(grid)
            : ToGroundGridKey(grid);
    }

    private static BattleGridKey ToGroundGridKey(Vector2I grid)
    {
        return new BattleGridKey(grid.X, grid.Y, 0);
    }

    private static BattleGridKey ToWallWalkGridKey(Vector2I grid)
    {
        return new BattleGridKey(grid.X, grid.Y, 2);
    }

    private bool IsWallTopGrid(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return cell.Structure is BattleStructureType.Wall or BattleStructureType.Gate or BattleStructureType.Tower;
    }

    private BattleGridKey? ResolveStepGridKey(BattleGridKey sourceGrid, Vector2I destinationGrid)
    {
        if (_mapData == null || !IsWithinMap(destinationGrid))
        {
            return null;
        }

        if (sourceGrid.Level == 2)
        {
            if (IsWallTopGrid(destinationGrid))
            {
                return ToWallWalkGridKey(destinationGrid);
            }

            return null;
        }

        if (IsWallTopGrid(destinationGrid))
        {
            if (sourceGrid.Level == 0 &&
                IsGateGrid(sourceGrid.Grid) &&
                IsGateGrid(destinationGrid))
            {
                return ToGroundGridKey(destinationGrid);
            }

            if (CanUseGateGroundPassage(destinationGrid) && sourceGrid.Level == 0)
            {
                return ToGroundGridKey(destinationGrid);
            }

            return IsInsideCityGroundGrid(sourceGrid.Grid)
                ? ToWallWalkGridKey(destinationGrid)
                : null;
        }

        return ToGroundGridKey(destinationGrid);
    }

    private bool IsGateGroundPassage(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return cell.Structure == BattleStructureType.Gate && (cell.IsGateOpen || cell.IsBroken);
    }

    private bool CanUseGateGroundPassage(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (cell.Structure != BattleStructureType.Gate)
        {
            return false;
        }

        return cell.IsGateOpen || cell.IsBroken;
    }

    private bool IsGateGrid(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        return _mapData.GetCell(grid.X, grid.Y).Structure == BattleStructureType.Gate;
    }

    private void RefreshCoordinateLabel()
    {
        var coordinateLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/CoordinateLabel");
        if (coordinateLabel != null)
        {
            coordinateLabel.Text = BuildCoordinateText();
        }
    }

    private void RefreshInfoPanel()
    {
        var infoLabel = GetNodeOrNull<Label>("UiLayer/SidePanel/Margin/PanelContent/InfoScroll/InfoPadding/InfoLabel") ??
                        GetNodeOrNull<Label>("UiLayer/SidePanel/Margin/PanelContent/InfoScroll/InfoLabel") ??
                        GetNodeOrNull<Label>("UiLayer/SidePanel/Margin/PanelContent/InfoLabel");
        if (infoLabel == null)
        {
            return;
        }

        infoLabel.Text = BuildInfoText();
    }

    private string BuildCoordinateText()
    {
        return BattleFormat(
            "ui.battle.coordinate_hover_click",
            "Hover: {0}    Click: {1}",
            FormatGrid(_hoverGridKey, _hoverGrid),
            FormatGrid(_selectedGridKey, _selectedGrid));
    }

    private string BuildTeamHudText(BattleHudTeamInfo info)
    {
        return BattleFormat(
            "ui.battle.team_hud",
            "{0}   Troops: {1:N0} / {2:N0} wounded   Generals: {3:N0}   Siege: {4:N0}   Gold: {5:N0}   Food: {6:N0}",
            FormatTeamName(info.Name),
            info.TotalTroops,
            info.WoundedTroops,
            info.TotalGenerals,
            info.TotalSiegeUnits,
            info.TotalGold,
            info.TotalFood);
    }

    private int GetTotalWoundedTroopsForTeam(string teamName)
    {
        var total = 0;
        foreach (var occupants in _occupantsByGrid.Values)
        {
            foreach (var occupant in occupants)
            {
                if (occupant.Category == CategoryUnit && occupant.TeamName == teamName)
                {
                    total += occupant.WoundedTroops;
                }
            }
        }

        return total;
    }

    private void AppendBattleLog(BattleOccupantInfo actor, string category, string message)
    {
        AppendBattleLog(actor.TeamName, category, message);
    }

    private void AppendBattleLog(string teamName, string category, string message)
    {
        _battleLogs.Add(new BattleLogEntry(_turnNumber, teamName, category, message));
        RefreshBattleLogPanel();
    }

    private void RefreshBattleLogPanel()
    {
        if (_battleLogPanel == null || _battleLogLabel == null)
        {
            return;
        }

        _battleLogPanel.Visible = true;
        ApplyBattleLogPanelLayout();
        if (_allLogButton != null)
        {
            _allLogButton.Disabled = !_showSelfTeamLogOnly;
            _allLogButton.Text = _showSelfTeamLogOnly
                ? BattleText("ui.battle.all_log", "All")
                : BattleFormat("ui.battle.active_log_filter", "{0} *", BattleText("ui.battle.all_log", "All"));
        }

        if (_selfLogButton != null)
        {
            _selfLogButton.Disabled = _showSelfTeamLogOnly;
            _selfLogButton.Text = _showSelfTeamLogOnly
                ? BattleFormat("ui.battle.active_log_filter", "{0} *", BattleText("ui.battle.self_log", "Self"))
                : BattleText("ui.battle.self_log", "Self");
        }

        var selfTeamName = GetCurrentTurnSideName();
        var visibleLogs = _battleLogs
            .Where(entry => !_showSelfTeamLogOnly || entry.TeamName == selfTeamName)
            .TakeLast(80)
            .ToList();
        if (visibleLogs.Count == 0)
        {
            _battleLogLabel.Text = _showSelfTeamLogOnly
                ? BattleFormat("ui.battle.no_self_log", "No {0} log yet.", FormatTeamName(selfTeamName))
                : BattleText("ui.battle.no_log", "No battle log yet.");
            return;
        }

        var builder = new StringBuilder();
        foreach (var entry in visibleLogs)
        {
            builder.AppendLine($"T{entry.Turn} [{FormatLogTeamName(entry.TeamName)}] {entry.Category}: {entry.Message}");
        }

        _battleLogLabel.Text = builder.ToString().TrimEnd();
    }

    private void ApplyBattleLogPanelStyle()
    {
        if (_battleLogPanel is PanelContainer panel)
        {
            var panelStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.1f, 0.9f),
                BorderColor = new Color(0.48f, 0.39f, 0.24f, 1.0f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 0,
                CornerRadiusBottomRight = 0
            };
            panel.AddThemeStyleboxOverride("panel", panelStyle);
        }

        if (_battleLogTitleLabel != null)
        {
            _battleLogTitleLabel.Text = BattleText("ui.battle.battle_log", "Battle Log");
            _battleLogTitleLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.78f, 0.62f, 1.0f));
        }

        ApplyBattleLogButtonStyle(_allLogButton);
        ApplyBattleLogButtonStyle(_selfLogButton);
        ApplyBattleLogButtonStyle(_minimizeLogButton);
        if (_battleLogLabel != null)
        {
            _battleLogLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.88f, 0.78f, 1.0f));
        }
    }

    private static void ApplyBattleLogButtonStyle(Button? button)
    {
        if (button == null)
        {
            return;
        }

        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.72f, 0.62f, 0.43f, 1.0f),
            BorderColor = new Color(0.48f, 0.39f, 0.24f, 1.0f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
        pressedStyle.BgColor = new Color(0.58f, 0.49f, 0.33f, 1.0f);
        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", normalStyle);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);
        button.AddThemeStyleboxOverride("disabled", pressedStyle);
        button.AddThemeColorOverride("font_color", new Color(0.12f, 0.09f, 0.06f, 1.0f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.12f, 0.09f, 0.06f, 1.0f));
        button.AddThemeColorOverride("font_pressed_color", new Color(0.08f, 0.06f, 0.04f, 1.0f));
        if (button.Name == "MinimizeLogButton")
        {
            button.CustomMinimumSize = new Vector2(36.0f, button.CustomMinimumSize.Y);
        }
        else
        {
            button.CustomMinimumSize = new Vector2(Mathf.Max(52.0f, button.CustomMinimumSize.X), button.CustomMinimumSize.Y);
        }
    }

    private void ApplyBattleOptionDialogStyle()
    {
        var optionPanel = GetNodeOrNull<PanelContainer>("UiLayer/BattleOptionOverlay/Center/Panel");
        if (optionPanel != null)
        {
            optionPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.05f, 0.06f, 0.88f),
                BorderColor = new Color(0.58f, 0.48f, 0.28f, 1.0f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.34f),
                ShadowSize = 8
            });
        }

        foreach (var label in new[]
        {
            _battleOptionTitleLabel,
            _battleBgmVolumeValueLabel,
            _battleSfxVolumeValueLabel
        })
        {
            label?.AddThemeColorOverride("font_color", new Color(0.92f, 0.86f, 0.72f, 1.0f));
        }

        foreach (var button in new[]
        {
            _battleOptionCloseButton,
            _battleOptionSaveButton,
            _battleOptionLoadButton,
            _battleOptionLanguageButton,
            _battleBgmToggleButton,
            _battleSfxToggleButton,
            _battleSaveSettingsButton
        })
        {
            ApplyBattleOptionButtonStyle(button);
        }

        ApplyBattleOptionSliderStyle(_battleBgmVolumeSlider);
        ApplyBattleOptionSliderStyle(_battleSfxVolumeSlider);
    }

    private static void ApplyBattleOptionButtonStyle(Button? button)
    {
        if (button == null)
        {
            return;
        }

        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.76f, 0.68f, 0.48f, 1.0f),
            BorderColor = new Color(0.42f, 0.33f, 0.18f, 1.0f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        };
        var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
        hoverStyle.BgColor = new Color(0.86f, 0.76f, 0.54f, 1.0f);
        var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
        pressedStyle.BgColor = new Color(0.58f, 0.49f, 0.32f, 1.0f);

        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", hoverStyle);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);
        button.AddThemeStyleboxOverride("disabled", pressedStyle);
        button.AddThemeColorOverride("font_color", new Color(0.12f, 0.09f, 0.05f, 1.0f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.10f, 0.07f, 0.04f, 1.0f));
        button.AddThemeColorOverride("font_pressed_color", new Color(0.08f, 0.06f, 0.03f, 1.0f));
    }

    private static void ApplyBattleOptionSliderStyle(HSlider? slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.AddThemeColorOverride("font_color", new Color(0.92f, 0.86f, 0.72f, 1.0f));
    }

    private void OnAllLogButtonPressed()
    {
        _showSelfTeamLogOnly = false;
        RefreshBattleLogPanel();
    }

    private void OnSelfLogButtonPressed()
    {
        _showSelfTeamLogOnly = true;
        RefreshBattleLogPanel();
    }

    private void HandleBattleLogPanelInput(InputEvent @event)
    {
        if (_battleLogPanel == null)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                if (mouseButton.Pressed)
                {
                    var mousePosition = mouseButton.GlobalPosition;
                    if (IsPointInBattleLogButtonArea(mousePosition))
                    {
                        return;
                    }

                    if (!_isBattleLogMinimized && IsPointInBattleLogResizeGrip(mousePosition))
                    {
                        _isResizingBattleLog = true;
                        _isDraggingBattleLog = false;
                        _battleLogResizeStartMouse = mousePosition;
                        _battleLogResizeStartSize = _battleLogPanel.Size;
                        GetViewport().SetInputAsHandled();
                        return;
                    }

                    if (IsPointInBattleLogDragArea(mousePosition))
                    {
                        _isDraggingBattleLog = true;
                        _isResizingBattleLog = false;
                        _battleLogDragOffset = mousePosition - _battleLogPanel.Position;
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
                else
                {
                    _isDraggingBattleLog = false;
                    _isResizingBattleLog = false;
                }

                break;
            case InputEventMouseMotion mouseMotion:
                if (_isDraggingBattleLog)
                {
                    _battleLogPanel.Position = ClampBattleLogPanelPosition(mouseMotion.GlobalPosition - _battleLogDragOffset, _battleLogPanel.Size);
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (_isResizingBattleLog && !_isBattleLogMinimized)
                {
                    var resizeDelta = mouseMotion.GlobalPosition - _battleLogResizeStartMouse;
                    ResizeBattleLogPanel(_battleLogResizeStartSize + resizeDelta);
                    GetViewport().SetInputAsHandled();
                }

                break;
        }
    }

    private bool IsPointInBattleLogDragArea(Vector2 globalPosition)
    {
        if (_battleLogPanel == null || !_battleLogPanel.GetGlobalRect().HasPoint(globalPosition))
        {
            return false;
        }

        return !IsPointInBattleLogButtonArea(globalPosition) &&
               (_isBattleLogMinimized || !IsPointInBattleLogResizeGrip(globalPosition));
    }

    private bool IsPointInBattleLogButtonArea(Vector2 globalPosition)
    {
        return (_allLogButton?.GetGlobalRect().HasPoint(globalPosition) ?? false) ||
               (_selfLogButton?.GetGlobalRect().HasPoint(globalPosition) ?? false) ||
               (_minimizeLogButton?.GetGlobalRect().HasPoint(globalPosition) ?? false);
    }

    private bool IsPointInBattleLogResizeGrip(Vector2 globalPosition)
    {
        if (_battleLogPanel == null)
        {
            return false;
        }

        var panelRect = _battleLogPanel.GetGlobalRect();
        var gripRect = new Rect2(panelRect.End - new Vector2(24.0f, 24.0f), new Vector2(24.0f, 24.0f));
        return gripRect.HasPoint(globalPosition);
    }

    private void OnMinimizeLogButtonPressed()
    {
        if (_battleLogPanel == null)
        {
            return;
        }

        _isBattleLogMinimized = !_isBattleLogMinimized;
        if (_isBattleLogMinimized)
        {
            _battleLogExpandedSize = _battleLogPanel.Size;
        }

        ApplyBattleLogPanelLayout();
    }

    private void ApplyBattleLogPanelLayout()
    {
        if (_battleLogPanel == null)
        {
            return;
        }

        if (_battleLogScroll != null)
        {
            _battleLogScroll.Visible = !_isBattleLogMinimized;
        }

        if (_battleLogResizeGrip != null)
        {
            _battleLogResizeGrip.Visible = !_isBattleLogMinimized;
        }

        if (_allLogButton != null)
        {
            _allLogButton.Visible = !_isBattleLogMinimized;
        }

        if (_selfLogButton != null)
        {
            _selfLogButton.Visible = !_isBattleLogMinimized;
        }

        if (_minimizeLogButton != null)
        {
            _minimizeLogButton.Text = _isBattleLogMinimized ? "+" : "_";
        }

        var targetSize = _isBattleLogMinimized
            ? new Vector2(Mathf.Max(220.0f, _battleLogPanel.Size.X), 52.0f)
            : GetClampedBattleLogPanelSize(_battleLogExpandedSize);
        _battleLogPanel.Size = targetSize;
        _battleLogPanel.Position = ClampBattleLogPanelPosition(_battleLogPanel.Position, targetSize);
    }

    private void ResizeBattleLogPanel(Vector2 desiredSize)
    {
        if (_battleLogPanel == null)
        {
            return;
        }

        var targetSize = GetClampedBattleLogPanelSize(desiredSize);
        _battleLogExpandedSize = targetSize;
        _battleLogPanel.Size = targetSize;
        _battleLogPanel.Position = ClampBattleLogPanelPosition(_battleLogPanel.Position, targetSize);
    }

    private Vector2 GetClampedBattleLogPanelSize(Vector2 desiredSize)
    {
        var viewportSize = GetViewportRect().Size;
        return new Vector2(
            Mathf.Clamp(desiredSize.X, BattleLogMinimumWidth, Mathf.Max(BattleLogMinimumWidth, viewportSize.X - 20.0f)),
            Mathf.Clamp(desiredSize.Y, BattleLogMinimumHeight, Mathf.Max(BattleLogMinimumHeight, viewportSize.Y - 20.0f)));
    }

    private Vector2 ClampBattleLogPanelPosition(Vector2 desiredPosition, Vector2 panelSize)
    {
        var viewportSize = GetViewportRect().Size;
        var maxX = Mathf.Max(0.0f, viewportSize.X - panelSize.X);
        var maxY = Mathf.Max(0.0f, viewportSize.Y - panelSize.Y);
        return new Vector2(
            Mathf.Clamp(desiredPosition.X, 0.0f, maxX),
            Mathf.Clamp(desiredPosition.Y, 0.0f, maxY));
    }

    private void OnBattleLogHeaderGuiInput(InputEvent @event)
    {
        if (_battleLogPanel == null)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                _isDraggingBattleLog = true;
                _battleLogDragOffset = mouseButton.GlobalPosition - _battleLogPanel.Position;
                GetViewport().SetInputAsHandled();
            }
            else
            {
                _isDraggingBattleLog = false;
            }
        }
    }

    private static string FormatLogTeamName(string teamName)
    {
        if (teamName.Contains("Attacker"))
        {
            return "A";
        }

        if (teamName.Contains("Defender"))
        {
            return "B";
        }

        return teamName;
    }

    private static string FormatLogUnit(BattleOccupantInfo unit)
    {
        if (string.IsNullOrWhiteSpace(unit.OfficerName))
        {
            return unit.DisplayName;
        }

        return $"{unit.OfficerName}/{unit.TroopType}";
    }

    private static string FormatMorale(BattleOccupantInfo unit)
    {
        return unit.Morale.HasValue ? unit.Morale.Value.ToString("N0") : "-";
    }

    private static string FormatWeaponAmmo(BattleOccupantInfo unit)
    {
        return unit.WeaponAmmo.HasValue && unit.MaxWeaponAmmo.HasValue
            ? $"{unit.WeaponAmmo.Value:N0}/{unit.MaxWeaponAmmo.Value:N0}"
            : "-";
    }

    private static string FormatWeaponAmmoLog(BattleOccupantInfo unit)
    {
        return unit.WeaponAmmo.HasValue && unit.MaxWeaponAmmo.HasValue
            ? $" (ammo {unit.WeaponAmmo.Value:N0}/{unit.MaxWeaponAmmo.Value:N0})"
            : string.Empty;
    }

    private static string FormatNormalAttackAmmoLog(BattleOccupantInfo unit, bool isWeakCloseAttack)
    {
        if (isWeakCloseAttack)
        {
            return unit.MaxWeaponAmmo.HasValue
                ? $" (weak close attack, ammo 0/{unit.MaxWeaponAmmo.Value:N0})"
                : " (weak close attack)";
        }

        return FormatWeaponAmmoLog(unit);
    }

    private static string FormatWoundedTroops(BattleOccupantInfo unit)
    {
        return unit.Category == CategoryUnit ? unit.WoundedTroops.ToString("N0") : "-";
    }

    private string BuildEmptyUnitMenuInfoText()
    {
        return string.Join(
            "\n",
            BattleFormat("ui.battle.menu_team", "Team: {0}", "-"),
            BattleFormat("ui.battle.menu_officer", "Officer: {0}", "-"),
            BattleFormat("ui.battle.menu_type", "Type: {0}", "-"),
            BattleFormat("ui.battle.menu_status", "Status: {0}", "-"),
            BattleFormat("ui.battle.menu_morale", "Morale: {0}", "-"),
            BattleFormat("ui.battle.menu_ammo", "Ammo: {0}", "-"),
            BattleFormat("ui.battle.menu_active", "Active: {0}", "-"),
            BattleFormat("ui.battle.menu_wounded", "Wounded: {0}", "-"));
    }

    private string FormatUnitCategory(string category)
    {
        return category switch
        {
            CategoryUnit => BattleText("ui.battle.category_unit", "Unit"),
            CategorySiegeEngine => BattleText("ui.battle.category_siege_engine", "Siege Engine"),
            _ => category
        };
    }

    private string FormatTroopType(string troopType)
    {
        return troopType switch
        {
            TroopInfantry => BattleText("ui.battle.troop_infantry", "Infantry"),
            TroopSpearman => BattleText("ui.battle.troop_spearman", "Spearman"),
            TroopArcher => BattleText("ui.battle.troop_archer", "Archer"),
            TroopCavalry => BattleText("ui.battle.troop_cavalry", "Cavalry"),
            TroopCrossbow => BattleText("ui.battle.troop_crossbow", "Crossbow"),
            TroopGuard => BattleText("ui.battle.troop_guard", "Guard"),
            TroopWorker => BattleText("ui.battle.troop_worker", "Worker"),
            TroopRam => BattleText("ui.battle.troop_ram", "Ram"),
            TroopLadder => BattleText("ui.battle.troop_ladder", "Ladder"),
            TroopCatapult => BattleText("ui.battle.troop_catapult", "Catapult"),
            TroopSupplyCart => BattleText("ui.battle.troop_supply_cart", "Supply Cart"),
            _ => troopType
        };
    }

    private string FormatStrategyAvailability(BattleOccupantInfo unit)
    {
        if (!HasStrategyPlans(unit.TeamName))
        {
            return BattleText("ui.battle.no_strategy_plan", "No strategy plan");
        }

        if (unit.Marker != null && _strategyUsedByMarkerThisTurn.Contains(unit.Marker))
        {
            return BattleText("ui.battle.already_used_this_turn", "Already used this turn");
        }

        return ResolveStrategyAction(unit) switch
        {
            BattleStrategyAction.Fire => BattleText("ui.battle.fire_ready", "Fire Ready"),
            BattleStrategyAction.Mental => BattleText("ui.battle.mess_calm_ready", "Mess / Calm Ready"),
            _ => BattleText("ui.battle.unavailable", "Unavailable")
        };
    }

    private static bool IsMessed(BattleOccupantInfo unit)
    {
        return unit.MessTurns > 0;
    }

    private string FormatBattleStatus(BattleOccupantInfo unit)
    {
        var statuses = new List<string>();
        if (unit.IsHidden)
        {
            statuses.Add(BattleText("ui.battle.status_hidden", "Hidden"));
        }

        if (IsMessed(unit))
        {
            statuses.Add(BattleFormat("ui.battle.status_mess_turns", "Mess ({0} turns)", unit.MessTurns));
        }

        return statuses.Count == 0 ? BattleText("ui.battle.status_normal", "Normal") : string.Join(", ", statuses);
    }

    private static int GetEffectiveMoveRange(BattleOccupantInfo unit)
    {
        if (IsMessed(unit))
        {
            return Math.Min(unit.MoveRange, 1);
        }

        if (unit.Morale.HasValue && unit.Morale.Value <= LowMoraleMovePenaltyThreshold)
        {
            return Math.Max(1, unit.MoveRange - 1);
        }

        return unit.MoveRange;
    }

    private bool HasStrategyPlans(string teamName)
    {
        return GetStrategyPlans(teamName) > 0;
    }

    private int GetStrategyPlans(string teamName)
    {
        return teamName.Contains("Attacker") ? _teamAStrategyPlans : _teamBStrategyPlans;
    }

    private bool TrySpendStrategyPlan(string teamName)
    {
        if (teamName.Contains("Attacker"))
        {
            if (_teamAStrategyPlans <= 0)
            {
                return false;
            }

            _teamAStrategyPlans--;
            return true;
        }

        if (_teamBStrategyPlans <= 0)
        {
            return false;
        }

        _teamBStrategyPlans--;
        return true;
    }

    private static string FormatWorkerWorkAction(WorkerWorkAction action, bool removedWoodFence = false)
    {
        return action switch
        {
            WorkerWorkAction.WoodFence => removedWoodFence ? "removes wood fence" : "installs wood fence",
            _ => "works on bridge"
        };
    }

    private static string FormatGrid(Vector2I? grid)
    {
        return grid.HasValue ? $"({grid.Value.X}, {grid.Value.Y})" : "-";
    }

    private static string FormatGrid(BattleGridKey? gridKey, Vector2I? fallbackGrid)
    {
        if (gridKey.HasValue)
        {
            return gridKey.Value.ToString();
        }

        return FormatGrid(fallbackGrid);
    }

    private string BuildInfoText()
    {
        if (!_selectedGrid.HasValue || _mapData == null)
        {
            return BattleText("ui.battle.tile_info", "Tile Info") + "\n" +
                   BattleFormat("ui.battle.coordinate", "Coordinate: {0}", "-") + "\n" +
                   BattleText("ui.battle.inspect_hint", "Click a tile to inspect terrain, structure, deployment zone, and units.");
        }

        var grid = _selectedGrid.Value;
        var cell = _mapData.GetCell(grid.X, grid.Y);
        var builder = new StringBuilder();
        builder.AppendLine(BattleText("ui.battle.tile_info", "Tile Info"));
        builder.AppendLine(BattleFormat("ui.battle.coordinate", "Coordinate: {0}", FormatGrid(_selectedGridKey, _selectedGrid)));
        builder.AppendLine(BattleFormat("ui.battle.terrain", "Terrain: {0}", FormatTerrain(cell.Terrain)));
        builder.AppendLine(BattleFormat("ui.battle.structure", "Structure: {0}", FormatStructure(cell.Structure)));
        if (cell.Structure != BattleStructureType.None)
        {
            builder.AppendLine(BattleFormat("ui.battle.facing", "Facing: {0}", FormatStructureFacing(cell.StructureFacing)));
        }
        if (cell.Structure == BattleStructureType.Gate)
        {
            builder.AppendLine(BattleFormat("ui.battle.gate_segment", "Gate Segment: {0}", FormatGateSegment(cell.GateSegment)));
        }
        if (cell.HasStructureHealth)
        {
            var durability = GetDisplayStructureDurability(grid, cell);
            builder.AppendLine(BattleFormat("ui.battle.durability", "Durability: {0}/{1}", durability.Current, durability.Max));
            builder.AppendLine(BattleFormat("ui.battle.status", "Status: {0}", cell.IsBroken ? BattleText("ui.battle.broken", "Broken") : BattleText("ui.battle.intact", "Intact")));
        }
        if (cell.HasBridgeHealth)
        {
            builder.AppendLine(BattleFormat("ui.battle.bridge_hp", "Bridge HP: {0}/{1}", cell.BridgeHealth, cell.BridgeMaxHealth));
            builder.AppendLine(BattleFormat("ui.battle.bridge_status", "Bridge Status: {0}", cell.IsBridgeDamaged ? BattleText("ui.battle.damaged", "Damaged") : BattleText("ui.battle.complete", "Complete")));
        }

        builder.AppendLine(BattleFormat("ui.battle.deployment", "Deployment: {0}", FormatDeploymentZone(cell.DeploymentZone)));
        builder.AppendLine(BattleFormat("ui.battle.height", "Height: {0}", cell.HeightLevel));
        builder.AppendLine(BattleFormat("ui.battle.blocks_move", "Blocks Move: {0}", IsCellBlockingMovement(cell) ? BattleText("ui.battle.yes", "Yes") : BattleText("ui.battle.no", "No")));
        if (_activeFireByGrid.TryGetValue(ToGroundGridKey(grid), out var fireState))
        {
            builder.AppendLine(BattleFormat("ui.battle.fire_burning", "Fire: Burning ({0} turn left)", fireState.RemainingTurns));
        }
        else
        {
            builder.AppendLine(BattleFormat("ui.battle.fire", "Fire: {0}", BattleText("ui.battle.none", "None")));
        }
        if (cell.Structure == BattleStructureType.Gate)
        {
            builder.AppendLine(BattleFormat("ui.battle.gate", "Gate: {0}", cell.IsGateOpen ? BattleText("ui.battle.open", "Open") : BattleText("ui.battle.closed", "Closed")));
        }

        builder.AppendLine(BattleText("ui.battle.occupants", "Occupants"));

        var occupantsAtGrid = GetOccupantsAtSelectedGrid(grid)
            .Where(entry => IsVisibleToCurrentTurnSide(entry.Occupant))
            .ToList();
        if (occupantsAtGrid.Count > 0)
        {
            foreach (var (gridKey, occupant) in occupantsAtGrid)
            {
                var hpText = occupant.Category == CategorySiegeEngine
                    ? BattleFormat("ui.battle.inline_hp", " HP {0}/{1}", occupant.HitPoints, occupant.MaxHitPoints)
                    : BattleFormat("ui.battle.inline_unit_stats", " Active {0:N0}/{1:N0} Wounded {2} Morale {3}", occupant.TroopCount, occupant.MaxHitPoints, FormatWoundedTroops(occupant), FormatMorale(occupant));
                var ammoText = occupant.MaxWeaponAmmo.HasValue
                    ? BattleFormat("ui.battle.inline_ammo", " Ammo {0}", FormatWeaponAmmo(occupant))
                    : string.Empty;
                var statusText = occupant.IsHidden || IsMessed(occupant)
                    ? BattleFormat("ui.battle.inline_status", " Status {0}", FormatBattleStatus(occupant))
                    : string.Empty;
                builder.AppendLine($"- {FormatUnitCategory(occupant.Category)}: {occupant.DisplayName} [{occupant.ShortLabel}] L{gridKey.Level}{hpText}{ammoText}{statusText}");
            }
        }
        else
        {
            builder.AppendLine($"- {BattleText("ui.battle.none", "None")}");
        }

        if (_selectedUnit != null && _selectedUnitGrid.HasValue)
        {
            builder.AppendLine(BattleText("ui.battle.selected_piece", "Selected Piece"));
            builder.AppendLine($"- {_selectedUnit.DisplayName} [{_selectedUnit.ShortLabel}]");
            builder.AppendLine(BattleFormat("ui.battle.list_category", "- Category: {0}", FormatUnitCategory(_selectedUnit.Category)));
            builder.AppendLine(BattleFormat("ui.battle.list_grid", "- Grid: {0}", $"({_selectedUnitGrid.Value.X}, {_selectedUnitGrid.Value.Y}, L{_selectedUnitGrid.Value.Level})"));
            builder.AppendLine(BattleFormat("ui.battle.list_status", "- Status: {0}", FormatBattleStatus(_selectedUnit)));
            builder.AppendLine(BattleFormat("ui.battle.list_move_range", "- Move Range: {0}/{1}", GetEffectiveMoveRange(_selectedUnit), _selectedUnit.MoveRange));
            if (_selectedUnit.MaxWeaponAmmo.HasValue)
            {
                builder.AppendLine(BattleFormat("ui.battle.list_weapon_ammo", "- Weapon Ammo: {0}", FormatWeaponAmmo(_selectedUnit)));
            }

            var effectiveAttackRange = GetEffectiveAttackRange(_selectedUnit);
            var attackRangeText = effectiveAttackRange == _selectedUnit.AttackRange
                ? _selectedUnit.AttackRange.ToString()
                : BattleFormat("ui.battle.effective_value", "{0} (effective {1})", _selectedUnit.AttackRange, effectiveAttackRange);
            builder.AppendLine(BattleFormat("ui.battle.list_attack_range", "- Attack Range: {0}", attackRangeText));
            if (_selectedUnit.Category == CategorySiegeEngine)
            {
                builder.AppendLine(BattleFormat("ui.battle.list_hp", "- HP: {0}/{1}", _selectedUnit.HitPoints, _selectedUnit.MaxHitPoints));
            }
            else
            {
                builder.AppendLine(BattleFormat("ui.battle.list_active_troops", "- Active Troops: {0:N0}/{1:N0}", _selectedUnit.TroopCount, _selectedUnit.MaxHitPoints));
                builder.AppendLine(BattleFormat("ui.battle.list_wounded_troops", "- Wounded Troops: {0}", FormatWoundedTroops(_selectedUnit)));
                builder.AppendLine(BattleFormat("ui.battle.list_morale", "- Morale: {0}", FormatMorale(_selectedUnit)));
            }

            builder.AppendLine(BattleFormat("ui.battle.list_reachable_tiles", "- Reachable Tiles: {0}", _movableGrids.Count));
            builder.AppendLine(BattleFormat("ui.battle.list_attackable_tiles", "- Attackable Tiles: {0}", _attackableGrids.Count));
            builder.AppendLine(BattleFormat("ui.battle.list_workable_tiles", "- Workable Tiles: {0}", _workableGrids.Count));
            builder.AppendLine(BattleFormat("ui.battle.list_strategy_targets", "- Strategy Targets: {0}", _strategyTargetGrids.Count));
            builder.AppendLine(BattleFormat("ui.battle.list_duel_targets", "- Duel Targets: {0}", _duelTargetGrids.Count));
            builder.AppendLine(BattleFormat("ui.battle.list_strategy", "- Strategy: {0}", FormatStrategyAvailability(_selectedUnit)));
            if (_selectedUnit.TroopType == TroopSupplyCart)
            {
                builder.AppendLine(BattleFormat("ui.battle.list_supply", "- Supply: {0}", HasSupplyTargets() ? BattleText("ui.battle.ready", "Ready") : BattleText("ui.battle.unavailable", "Unavailable")));
            }
            builder.AppendLine(BattleFormat("ui.battle.list_command_state", "- Command State: {0}", FormatCommandMode(_commandMode)));
            builder.AppendLine(BattleFormat("ui.battle.list_current_turn", "- Current Turn: {0}", FormatTeamName(GetCurrentTurnSideName())));
        }

        return builder.ToString().TrimEnd();
    }

    private (int Current, int Max) GetDisplayStructureDurability(Vector2I grid, BattleCellData cell)
    {
        if (cell.Structure != BattleStructureType.Gate || _mapData == null)
        {
            return (cell.StructureHealth, cell.StructureMaxHealth);
        }

        var group = GetConnectedGateGroup(grid);
        if (group.Count == 0)
        {
            return (cell.StructureHealth, cell.StructureMaxHealth);
        }

        var current = group
            .Select(gateGrid => _mapData.GetCell(gateGrid.X, gateGrid.Y).StructureHealth)
            .Min();
        return (current, BattleCellData.GateMaxHealth);
    }

    private void RegisterOccupant(BattleGridKey grid, string displayName, string category, string shortLabel, string teamName, string officerName, string troopType, int troopCount, int moveRange, int attackRange, BattlePieceMarker? marker)
    {
        if (!_occupantsByGrid.TryGetValue(grid, out var occupants))
        {
            occupants = new List<BattleOccupantInfo>();
            _occupantsByGrid[grid] = occupants;
        }

        var morale = GetInitialMorale(category, troopType);
        var weaponAmmo = GetInitialWeaponAmmo(category, troopType);
        occupants.Add(new BattleOccupantInfo(displayName, category, shortLabel, teamName, officerName, troopType, troopCount, troopCount, troopCount, WoundedTroops: 0, MessTurns: 0, IsHidden: false, morale, weaponAmmo, weaponAmmo, moveRange, attackRange, marker, BattleSpriteDirection.SouthEast));
    }

    private static void UpdateMarkerStrengthBar(BattleOccupantInfo occupant)
    {
        if (occupant.Marker == null)
        {
            return;
        }

        if (occupant.Category == CategoryUnit)
        {
            occupant.Marker.SetupTroopSegmentBar(occupant.TroopCount, occupant.WoundedTroops, occupant.MaxHitPoints);
            return;
        }

        occupant.Marker.SetupHealthBar(occupant.HitPoints, occupant.MaxHitPoints);
    }

    private static void UpdateMarkerStatusIndicator(BattleOccupantInfo occupant)
    {
        if (occupant.Marker == null)
        {
            return;
        }

        if (IsMessed(occupant))
        {
            occupant.Marker.SetupStatusIndicator("MESS", new Color(1.0f, 0.34f, 0.16f, 0.92f));
            return;
        }

        if (occupant.IsHidden)
        {
            occupant.Marker.SetupStatusIndicator("HIDE", new Color(0.16f, 0.72f, 0.38f, 0.92f), drawRing: false);
            return;
        }

        occupant.Marker.SetupStatusIndicator(string.Empty, Colors.Transparent);
    }

    private static int? GetInitialMorale(string category, string troopType)
    {
        if (category != CategoryUnit)
        {
            return null;
        }

        return troopType == TroopWorker ? WorkerMorale : DefaultUnitMorale;
    }

    private static int? GetInitialWeaponAmmo(string category, string troopType)
    {
        return troopType switch
        {
            TroopArcher => ArcherMaxWeaponAmmo,
            TroopCrossbow => CrossbowMaxWeaponAmmo,
            TroopCatapult when category == CategorySiegeEngine => CatapultMaxWeaponAmmo,
            _ => null
        };
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetOccupantsAtGrid(Vector2I grid)
    {
        foreach (var (gridKey, occupants) in _occupantsByGrid)
        {
            if (gridKey.X != grid.X || gridKey.Y != grid.Y)
            {
                continue;
            }

            foreach (var occupant in occupants)
            {
                yield return (gridKey, occupant);
            }
        }
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetOccupantsAtSelectedGrid(Vector2I grid)
    {
        if (_selectedGridKey.HasValue)
        {
            if (_occupantsByGrid.TryGetValue(_selectedGridKey.Value, out var selectedOccupants))
            {
                foreach (var occupant in selectedOccupants)
                {
                    yield return (_selectedGridKey.Value, occupant);
                }
            }

            yield break;
        }

        foreach (var occupant in GetOccupantsAtGrid(grid))
        {
            yield return occupant;
        }
    }

    private bool TryMoveSelectedUnit()
    {
        if (!_selectedGrid.HasValue || !_selectedUnitGrid.HasValue || _selectedUnit == null || _groundLayer == null)
        {
            return false;
        }

        if (!IsCurrentTurnPiece(_selectedUnit))
        {
            return false;
        }

        var destinationGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        var sourceGrid = _selectedUnitGrid.Value;
        if (destinationGrid == sourceGrid || !_movableGrids.Contains(destinationGrid))
        {
            return false;
        }

        if (!_occupantsByGrid.TryGetValue(sourceGrid, out var sourceOccupants))
        {
            return false;
        }

        var movingOccupant = sourceOccupants.FirstOrDefault(static occupant => occupant.Marker != null && IsBattlePiece(occupant));
        if (movingOccupant == null || movingOccupant.Marker == null)
        {
            return false;
        }

        sourceOccupants.Remove(movingOccupant);
        if (sourceOccupants.Count == 0)
        {
            _occupantsByGrid.Remove(sourceGrid);
        }

        if (!_occupantsByGrid.TryGetValue(destinationGrid, out var destinationOccupants))
        {
            destinationOccupants = new List<BattleOccupantInfo>();
            _occupantsByGrid[destinationGrid] = destinationOccupants;
        }

        if (!TryBuildMovePath(sourceGrid, destinationGrid, GetEffectiveMoveRange(movingOccupant), out var movePath))
        {
            movePath = [destinationGrid];
        }

        movePath = ExpandMovePathWithCarLadderWaypoints(sourceGrid, movePath, movingOccupant);
        var pathPositions = movePath.Select(GetMarkerPosition).ToArray();
        var pathDirections = BuildPathDirections(sourceGrid, movePath);
        var pathModulates = BuildMovePathModulates(sourceGrid, movePath, movingOccupant);
        var moveDirection = pathDirections.Length > 0
            ? pathDirections[^1]
            : GetInfantryDirection(sourceGrid.Grid, destinationGrid.Grid);
        var remainsHidden = movingOccupant.IsHidden && IsForestGrid(destinationGrid);
        var movedOccupant = movingOccupant with
        {
            Marker = movingOccupant.Marker,
            FacingDirection = moveDirection,
            IsHidden = remainsHidden
        };
        destinationOccupants.Add(movedOccupant);
        UpdateMarkerStatusIndicator(movedOccupant);
        RegisterBattleDepthEntry(
            movedOccupant.Marker!,
            destinationGrid,
            movedOccupant.Category == CategorySiegeEngine ? BattleDepthRenderKind.SiegeEngine : BattleDepthRenderKind.Unit);
        RefreshBattleDepthLayerOrder();
        ClearOccludedUnitSilhouettes();
        ApplyMoveAnimation(movedOccupant, moveDirection, GetMarkerPosition(destinationGrid), pathPositions, pathDirections, pathModulates, RefreshOccludedUnitSilhouettes);

        _selectedUnitGrid = destinationGrid;
        _selectedUnit = movedOccupant;
        _selectedGrid = destinationGrid.Grid;
        _selectedGridKey = destinationGrid;
        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        HideCommandMenu();
        AppendBattleLog(movedOccupant, "Move", $"{FormatLogUnit(movedOccupant)} {sourceGrid} -> {destinationGrid}");
        if (movingOccupant.IsHidden && !remainsHidden)
        {
            AppendBattleLog(movedOccupant, "Status", $"{FormatLogUnit(movedOccupant)} leaves forest and is no longer hidden");
        }
        RefreshHiddenUnitVisibility();

        return true;
    }

    private Color?[]? BuildMovePathModulates(BattleGridKey sourceGrid, IReadOnlyList<BattleGridKey> movePath, BattleOccupantInfo movingOccupant)
    {
        if (movePath.Count == 0)
        {
            return null;
        }

        var silhouetteColor = GetOccludedUnitSilhouetteColor(movingOccupant);
        var pathModulates = new Color?[movePath.Count];
        var hasSilhouetteSegment = false;
        var previousGrid = sourceGrid;
        for (var index = 0; index < movePath.Count; index++)
        {
            var nextGrid = movePath[index];
            if (ShouldUseMovingSegmentSilhouette(previousGrid, nextGrid))
            {
                pathModulates[index] = silhouetteColor;
                hasSilhouetteSegment = true;
            }

            previousGrid = nextGrid;
        }

        return hasSilhouetteSegment ? pathModulates : null;
    }

    private bool ShouldUseMovingSegmentSilhouette(BattleGridKey sourceGrid, BattleGridKey destinationGrid)
    {
        return IsGateVerticalLayerMove(sourceGrid, destinationGrid) ||
               IsUnitOccludedByCastleVisual(destinationGrid);
    }

    private bool IsGateVerticalLayerMove(BattleGridKey sourceGrid, BattleGridKey destinationGrid)
    {
        if (_mapData == null ||
            sourceGrid.Grid != destinationGrid.Grid ||
            !IsWithinMap(sourceGrid.Grid))
        {
            return false;
        }

        var isGateVerticalMove =
            (sourceGrid.Level == 0 && destinationGrid.Level == 2) ||
            (sourceGrid.Level == 2 && destinationGrid.Level == 0);
        if (!isGateVerticalMove)
        {
            return false;
        }

        var cell = _mapData.GetCell(sourceGrid.X, sourceGrid.Y);
        return cell.Structure == BattleStructureType.Gate;
    }

    private bool TryAttackSelectedTarget()
    {
        if (!_selectedGrid.HasValue || !_selectedUnitGrid.HasValue || _selectedUnit == null)
        {
            return false;
        }

        var targetGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        if (!_attackableGrids.Contains(targetGrid))
        {
            return false;
        }

        var attackDirection = GetInfantryDirection(_selectedUnitGrid.Value.Grid, targetGrid.Grid);
        if (_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants) &&
            GetAttackTargetForAttack(targetOccupants, _selectedUnit.TeamName, targetGrid) == null)
        {
            return false;
        }

        var isWeakCloseAttack = CanUseAmmoDepletedWeakAttack(_selectedUnit);
        var attackDamage = GetAttackDamage(_selectedUnit);
        var attackingUnit = _selectedUnit with { FacingDirection = attackDirection };
        if (!TrySpendNormalAttackWeaponAmmo(attackingUnit, out attackingUnit))
        {
            return false;
        }

        ReplaceOccupantAtGrid(_selectedUnitGrid.Value, _selectedUnit, attackingUnit);
        _selectedUnit = attackingUnit;

        var shouldTemporarilyRevealOccludedUnits =
            IsUnitOccludedByCastleVisual(_selectedUnitGrid.Value) ||
            IsUnitOccludedByCastleVisual(targetGrid);
        if (shouldTemporarilyRevealOccludedUnits)
        {
            ClearOccludedUnitSilhouettes();
        }

        var attackAnimationDuration = ApplyAttackAnimation(attackingUnit, attackDirection);
        var hurtAnimationDuration = ApplyTargetHurtAnimation(_selectedUnitGrid.Value, targetGrid, attackingUnit);
        var arrowEffectDuration = IsArrowProjectileAttacker(attackingUnit) && !isWeakCloseAttack
            ? PlayArrowProjectileEffect(_selectedUnitGrid.Value, targetGrid)
            : 0.0;
        var catapultEffectDuration = attackingUnit.Category == CategorySiegeEngine && attackingUnit.TroopType == TroopCatapult && !isWeakCloseAttack
            ? PlayCatapultProjectileEffect(_selectedUnitGrid.Value, targetGrid)
            : 0.0;
        var effectDelaySeconds = Math.Max(
            Math.Max(attackAnimationDuration, hurtAnimationDuration),
            Math.Max(arrowEffectDuration, catapultEffectDuration));
        AppendBattleLog(attackingUnit, "Attack", $"{FormatLogUnit(attackingUnit)} attacks {targetGrid}{FormatNormalAttackAmmoLog(attackingUnit, isWeakCloseAttack)}");
        ApplyAttackDamage(attackingUnit, targetGrid, effectDelaySeconds, attackDamage);
        if (shouldTemporarilyRevealOccludedUnits)
        {
            RefreshOccludedUnitSilhouettesAfterDelay(effectDelaySeconds);
        }

        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        return true;
    }

    private bool TryPerformWorkerWork()
    {
        if (_mapData == null ||
            _selectedUnit?.TroopType != TroopWorker ||
            IsMessed(_selectedUnit) ||
            !_selectedUnitGrid.HasValue ||
            !_selectedGrid.HasValue)
        {
            return false;
        }

        var targetGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        if (targetGrid.Level != 0 || !_workableGrids.Contains(targetGrid))
        {
            return false;
        }

        var sourceGrid = _selectedUnitGrid.Value;
        var targetCell = _mapData.GetCell(targetGrid.X, targetGrid.Y);
        if (!ApplyWorkerWork(targetGrid.Grid, targetCell, out var removedWoodFence))
        {
            return false;
        }

        var workDirection = GetInfantryDirection(sourceGrid.Grid, targetGrid.Grid);
        var workingUnit = _selectedUnit with { FacingDirection = workDirection };
        ReplaceOccupantAtGrid(sourceGrid, _selectedUnit, workingUnit);
        _selectedUnit = workingUnit;
        workingUnit.Marker?.PlayAction(
            GetWorkerWorkScene(workDirection),
            GetWorkerIdleScene(workDirection),
            WorkerWorkAnimationDurationSeconds);
        AppendBattleLog(workingUnit, "Action", $"{FormatLogUnit(workingUnit)} {FormatWorkerWorkAction(_workerWorkAction, removedWoodFence)} at {targetGrid}");

        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        return true;
    }

    private bool ApplyWorkerWork(Vector2I targetGrid, BattleCellData targetCell, out bool removedWoodFence)
    {
        removedWoodFence = false;
        if (_mapData == null)
        {
            return false;
        }

        if (_workerWorkAction == WorkerWorkAction.WoodFence)
        {
            if (targetCell.Structure == BattleStructureType.WoodenFence)
            {
                targetCell.Structure = BattleStructureType.None;
                targetCell.BlocksMovement = false;
                targetCell.WoodenFenceFlipHorizontally = false;
                removedWoodFence = true;
                RefreshWorkerObjectLayers();
                return true;
            }

            if (!CanInstallWoodFence(targetGrid, targetCell))
            {
                return false;
            }

            targetCell.Structure = BattleStructureType.WoodenFence;
            targetCell.StructureMaxHealth = BattleCellData.WoodenFenceMaxHealth;
            targetCell.StructureHealth = targetCell.StructureMaxHealth;
            targetCell.BlocksMovement = true;
            targetCell.WoodenFenceFlipHorizontally = GD.Randf() >= 0.5f;
            RefreshWorkerObjectLayers();
            return true;
        }

        if (targetCell.Terrain == BattleTerrainType.Moat &&
            _mapData.ScenarioDefinition.ScenarioType == BattleScenarioType.MoatSiegeBattle)
        {
            targetCell.Terrain = BattleTerrainType.Bridge;
            targetCell.HasBridgeVisual = true;
            targetCell.BridgeFlipHorizontally = _mapData.ScenarioDefinition.DefaultStructureFacing == BattleStructureFacing.NorthWest;
            targetCell.BridgeMaxHealth = BattleCellData.BridgeMaxDurability;
            targetCell.BridgeHealth = BattleCellData.BridgeConstructionStep;
            targetCell.BlocksMovement = true;
            RefreshWorkerObjectLayers();
            return true;
        }

        if (targetCell.IsBridgeDamaged)
        {
            targetCell.BridgeHealth = Math.Min(targetCell.BridgeMaxHealth, targetCell.BridgeHealth + WorkerBridgeRepairAmount);
            targetCell.BlocksMovement = targetCell.BridgeHealth < targetCell.BridgeMaxHealth;
            return true;
        }

        if (targetCell.Structure == BattleStructureType.Gate && targetCell.HasStructureHealth && targetCell.StructureHealth < targetCell.StructureMaxHealth)
        {
            foreach (var gateGrid in GetConnectedGateGroup(targetGrid))
            {
                var gateCell = _mapData.GetCell(gateGrid.X, gateGrid.Y);
                gateCell.StructureHealth = Math.Min(gateCell.StructureMaxHealth, gateCell.StructureHealth + WorkerGateRepairAmount);
                gateCell.IsGateOpen = false;
                gateCell.BlocksMovement = true;
                if (_castleLayer != null)
                {
                    BattleTileMapBuilder.SetCastleGateVisual(_castleLayer, gateGrid, isOpen: false);
                }

                RefreshCastleDepthVisual(gateGrid);
            }

            RefreshBattleDepthLayerOrder();
            RefreshOccludedUnitSilhouettes();
            return true;
        }

        if (targetCell.Structure == BattleStructureType.Trap)
        {
            targetCell.Structure = BattleStructureType.None;
            targetCell.BlocksMovement = false;
            targetCell.StructureMaxHealth = 0;
            targetCell.StructureHealth = 0;
            RefreshWorkerObjectLayers();
            return true;
        }

        return false;
    }

    private void RefreshWorkerObjectLayers()
    {
        if (_mapData == null)
        {
            return;
        }

        ConfigureOptionalTileMapLayer(_moatLayer, BattleTileLayerKind.Moat);
        if (_objectLayer != null)
        {
            BattleTileMapBuilder.ConfigureLayer(_objectLayer, _mapData, BattleTileLayerKind.Object);
            _objectLayer.Visible = true;
        }
    }

    private void ReplaceOccupantAtGrid(BattleGridKey grid, BattleOccupantInfo oldOccupant, BattleOccupantInfo newOccupant)
    {
        if (!_occupantsByGrid.TryGetValue(grid, out var occupants))
        {
            return;
        }

        var index = occupants.IndexOf(oldOccupant);
        if (index >= 0)
        {
            occupants[index] = newOccupant;
            UpdateMarkerStatusIndicator(newOccupant);
        }
    }

    private bool TryGetCurrentOccupantAtGrid(BattleGridKey grid, BattleOccupantInfo occupant, out BattleOccupantInfo currentOccupant)
    {
        currentOccupant = occupant;
        if (!_occupantsByGrid.TryGetValue(grid, out var occupants))
        {
            return false;
        }

        if (occupants.Contains(occupant))
        {
            return true;
        }

        if (occupant.Marker == null)
        {
            return false;
        }

        var matchingOccupant = occupants.FirstOrDefault(candidate => candidate.Marker == occupant.Marker);
        if (matchingOccupant == null)
        {
            return false;
        }

        currentOccupant = matchingOccupant;
        return true;
    }

    private static void ApplyMoveAnimation(BattleOccupantInfo occupant, BattleSpriteDirection direction, Vector2 destinationPosition, Vector2[]? pathPositions = null, BattleSpriteDirection[]? pathDirections = null, Color?[]? pathModulates = null, Action? onComplete = null)
    {
        if (occupant.Marker == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopInfantry)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetInfantryMoveScene), pathModulates, GetScaledMoveDuration(InfantryMoveAnimationDurationSeconds, pathPositions), GetInfantryMoveScene(direction), GetInfantryIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopSpearman)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetSpearmanMoveScene), pathModulates, GetScaledMoveDuration(SpearmanMoveAnimationDurationSeconds, pathPositions), GetSpearmanMoveScene(direction), GetSpearmanIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopRam)
        {
            var carIdleScene = GetCarIdleScene(direction);
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, null, pathModulates, GetScaledMoveDuration(CarMoveAnimationDurationSeconds, pathPositions), carIdleScene, carIdleScene, onComplete);
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopSupplyCart)
        {
            var supplyCarIdleScene = GetSupplyCarIdleScene(direction);
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, null, pathModulates, GetScaledMoveDuration(CarMoveAnimationDurationSeconds, pathPositions), supplyCarIdleScene, supplyCarIdleScene, onComplete);
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopLadder)
        {
            var carLadderIdleScene = GetCarLadderIdleScene(direction);
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, null, pathModulates, GetScaledMoveDuration(CarMoveAnimationDurationSeconds, pathPositions), carLadderIdleScene, carLadderIdleScene, onComplete);
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopArcher)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetArcherMoveScene), pathModulates, GetScaledMoveDuration(ArcherMoveAnimationDurationSeconds, pathPositions), GetArcherMoveScene(direction), GetArcherIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopCavalry)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, null, pathModulates, GetScaledMoveDuration(CavalryMoveAnimationDurationSeconds, pathPositions), GetCavalryMoveScene(direction), GetCavalryIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopWorker)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetWorkerMoveScene), pathModulates, GetScaledMoveDuration(InfantryMoveAnimationDurationSeconds, pathPositions), GetWorkerMoveScene(direction), GetWorkerIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopCatapult)
        {
            var catapultIdleScene = GetCatapultIdleScene(direction);
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, null, pathModulates, GetScaledMoveDuration(CatapultMoveAnimationDurationSeconds, pathPositions), catapultIdleScene, catapultIdleScene, onComplete);
            return;
        }

        occupant.Marker.Position = destinationPosition;
        onComplete?.Invoke();
    }

    private static void MoveMarker(BattlePieceMarker marker, Vector2 destinationPosition, Vector2[]? pathPositions, string[]? pathMoveScenePaths, Color?[]? pathModulates, double duration, string moveScenePath, string idleScenePath, Action? onComplete = null)
    {
        if (pathPositions is { Length: > 0 })
        {
            if (pathMoveScenePaths is { Length: > 0 })
            {
                marker.MoveAlong(pathPositions, duration, pathMoveScenePaths, idleScenePath, onComplete, pathModulates);
                return;
            }

            marker.MoveAlong(pathPositions, duration, moveScenePath, idleScenePath, onComplete, pathModulates);
            return;
        }

        marker.MoveTo(destinationPosition, duration, moveScenePath, idleScenePath, onComplete);
    }

    private static double GetScaledMoveDuration(double baseDuration, Vector2[]? pathPositions)
    {
        return baseDuration * Math.Max(1, pathPositions?.Length ?? 1);
    }

    private static string[]? GetMoveScenePathArray(BattleSpriteDirection[]? directions, Func<BattleSpriteDirection, string> getMoveScene)
    {
        if (directions is not { Length: > 0 })
        {
            return null;
        }

        var scenePaths = new string[directions.Length];
        for (var index = 0; index < directions.Length; index++)
        {
            scenePaths[index] = getMoveScene(directions[index]);
        }

        return scenePaths;
    }

    private double ApplyAttackAnimation(BattleOccupantInfo occupant, BattleSpriteDirection direction)
    {
        if (occupant.Marker == null)
        {
            return 0.0;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopCatapult)
        {
            occupant.Marker.PlayAction(
                GetCatapultAttackScene(direction),
                GetCatapultIdleScene(direction),
                CatapultAttackAnimationDurationSeconds);
            return CatapultAttackAnimationDurationSeconds;
        }

        if (occupant.Category != CategoryUnit)
        {
            return 0.0;
        }

        if (occupant.TroopType == TroopInfantry)
        {
            occupant.Marker.PlayAction(
                GetInfantryAttackScene(direction),
                GetInfantryIdleScene(direction),
                InfantryAttackAnimationDurationSeconds);
            return InfantryAttackAnimationDurationSeconds;
        }

        if (occupant.TroopType == TroopSpearman)
        {
            occupant.Marker.PlayAction(
                GetSpearmanAttackScene(direction),
                GetSpearmanIdleScene(direction),
                SpearmanAttackAnimationDurationSeconds);
            return SpearmanAttackAnimationDurationSeconds;
        }

        if (occupant.TroopType == TroopArcher)
        {
            occupant.Marker.PlayAction(
                GetArcherAttackScene(direction),
                GetArcherIdleScene(direction),
                ArcherAttackAnimationDurationSeconds);
            return ArcherAttackAnimationDurationSeconds;
        }

        if (occupant.TroopType == TroopCavalry)
        {
            occupant.Marker.PlayAction(
                GetCavalryAttackScene(direction),
                GetCavalryIdleScene(direction),
                CavalryAttackAnimationDurationSeconds);
            return CavalryAttackAnimationDurationSeconds;
        }

        if (occupant.TroopType == TroopWorker)
        {
            occupant.Marker.PlayAction(
                GetWorkerAttackScene(direction),
                GetWorkerIdleScene(direction),
                WorkerAttackAnimationDurationSeconds);
            return WorkerAttackAnimationDurationSeconds;
        }

        return 0.0;
    }

    private double ApplyTargetHurtAnimation(BattleGridKey attackerGrid, BattleGridKey targetGrid, BattleOccupantInfo? attacker = null)
    {
        if (IsClosedGateStructureTarget(targetGrid))
        {
            return 0.0;
        }

        if (!_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            return 0.0;
        }

        var target = attacker == null
            ? GetAttackTarget(targetOccupants)
            : GetAttackTargetForAttack(targetOccupants, attacker.TeamName, targetGrid);
        if (target?.Marker == null)
        {
            return 0.0;
        }

        if (target.Category == CategorySiegeEngine)
        {
            return 0.0;
        }

        var hurtDirection = GetInfantryDirection(attackerGrid.Grid, targetGrid.Grid);
        if (target.TroopType == TroopSpearman)
        {
            target.Marker.PlayAction(
                GetSpearmanHurtScene(hurtDirection),
                GetSpearmanIdleScene(target.FacingDirection),
                SpearmanHurtAnimationDurationSeconds);
            return SpearmanHurtAnimationDurationSeconds;
        }

        if (target.TroopType == TroopArcher)
        {
            target.Marker.PlayAction(
                GetArcherHurtScene(hurtDirection),
                GetArcherIdleScene(target.FacingDirection),
                ArcherHurtAnimationDurationSeconds);
            return ArcherHurtAnimationDurationSeconds;
        }

        if (target.TroopType == TroopCavalry)
        {
            target.Marker.PlayAction(
                GetCavalryHurtScene(hurtDirection),
                GetCavalryIdleScene(target.FacingDirection),
                CavalryHurtAnimationDurationSeconds);
            return CavalryHurtAnimationDurationSeconds;
        }

        if (target.TroopType == TroopWorker)
        {
            target.Marker.PlayAction(
                GetWorkerHurtScene(hurtDirection),
                GetWorkerIdleScene(target.FacingDirection),
                WorkerHurtAnimationDurationSeconds);
            return WorkerHurtAnimationDurationSeconds;
        }

        target.Marker.PlayAction(
            GetInfantryHurtScene(hurtDirection),
            GetInfantryIdleScene(target.FacingDirection),
            InfantryHurtAnimationDurationSeconds);
        return InfantryHurtAnimationDurationSeconds;
    }

    private void ApplyAttackDamage(BattleOccupantInfo attacker, BattleGridKey targetGrid, double effectDelaySeconds, int? damageOverride = null)
    {
        if (IsClosedGateStructureTarget(targetGrid))
        {
            ApplyStructureAttackDamage(attacker, targetGrid);
            return;
        }

        if (!_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            ApplyStructureAttackDamage(attacker, targetGrid);
            return;
        }

        var target = GetAttackTargetForAttack(targetOccupants, attacker.TeamName, targetGrid);
        if (target == null)
        {
            return;
        }

        var damage = damageOverride ?? GetAttackDamage(attacker);
        if (damage <= 0)
        {
            return;
        }

        var casualtyResult = ApplyUnitCasualties(targetGrid, target, damage, NormalDamageKilledRatio);
        var updatedTarget = casualtyResult.UpdatedTarget;

        ShowDamagePopup(targetGrid, casualtyResult.ActualDamage);
        AppendBattleLog(
            target,
            "Hurt",
            $"{FormatLogUnit(target)} got {casualtyResult.ActualDamage:N0} hurt by {FormatLogUnit(attacker)} at {targetGrid} ({FormatCasualtyResult(casualtyResult)})");
        ConfigureHud();
        RefreshInfoPanel();
        if (updatedTarget.HitPoints <= 0)
        {
            DestroyOccupantAfterDelay(targetGrid, updatedTarget, effectDelaySeconds);
        }
    }

    private void ApplyStructureAttackDamage(BattleOccupantInfo attacker, BattleGridKey targetGrid)
    {
        if (_mapData == null || targetGrid.Level != 0 || !IsWithinMap(targetGrid.Grid))
        {
            return;
        }

        var cell = _mapData.GetCell(targetGrid.X, targetGrid.Y);
        if (cell.HasBridgeHealth)
        {
            var bridgeDamage = GetStructureAttackDamage(attacker);
            if (bridgeDamage <= 0)
            {
                return;
            }

            var actualBridgeDamage = _mapData.ApplyBridgeDamage(targetGrid.Grid, bridgeDamage);
            if (actualBridgeDamage <= 0)
            {
                return;
            }

            if (!cell.HasBridgeHealth)
            {
                RefreshWorkerObjectLayers();
            }

            ShowDamagePopup(targetGrid, actualBridgeDamage);
            RefreshInfoPanel();
            return;
        }

        if (cell.Structure == BattleStructureType.WoodenFence && cell.HasStructureHealth)
        {
            var fenceDamage = GetStructureAttackDamage(attacker);
            if (fenceDamage <= 0)
            {
                return;
            }

            var actualFenceDamage = _mapData.ApplyWoodenFenceDamage(targetGrid.Grid, fenceDamage);
            if (actualFenceDamage <= 0)
            {
                return;
            }

            if (cell.Structure != BattleStructureType.WoodenFence)
            {
                RefreshWorkerObjectLayers();
            }

            ShowDamagePopup(targetGrid, actualFenceDamage);
            RefreshInfoPanel();
            return;
        }

        if (cell.Structure != BattleStructureType.Gate || !cell.HasStructureHealth || cell.IsBroken)
        {
            return;
        }

        var damage = GetStructureAttackDamage(attacker);
        if (damage <= 0)
        {
            return;
        }

        var actualDamage = ApplyGateGroupDamage(targetGrid.Grid, damage);
        ShowDamagePopup(targetGrid, actualDamage);
        RefreshInfoPanel();
    }

    private BattleCasualtyResult ApplyUnitCasualties(
        BattleGridKey targetGrid,
        BattleOccupantInfo target,
        int damage,
        float killedRatio)
    {
        var actualDamage = Mathf.Min(target.HitPoints, damage);
        if (actualDamage <= 0)
        {
            return new BattleCasualtyResult(target, 0, 0, 0);
        }

        if (target.Category != CategoryUnit)
        {
            var remainingHp = Mathf.Max(0, target.HitPoints - actualDamage);
            var updatedTarget = target with { HitPoints = remainingHp };
            UpdateMarkerStrengthBar(updatedTarget);
            ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
            if (_selectedUnit == target)
            {
                _selectedUnit = updatedTarget;
            }

            return new BattleCasualtyResult(updatedTarget, actualDamage, actualDamage, 0);
        }

        var activeLoss = Mathf.Min(target.TroopCount, actualDamage);
        var killedTroops = Mathf.Clamp(Mathf.RoundToInt(activeLoss * killedRatio), 0, activeLoss);
        var woundedTroops = activeLoss - killedTroops;
        var remainingTroops = Mathf.Max(0, target.TroopCount - activeLoss);
        ApplyTeamTroopLoss(target, activeLoss);
        var updatedUnit = target with
        {
            TroopCount = remainingTroops,
            HitPoints = remainingTroops,
            WoundedTroops = target.WoundedTroops + woundedTroops
        };
        UpdateMarkerStrengthBar(updatedUnit);
        ReplaceOccupantAtGrid(targetGrid, target, updatedUnit);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedUnit;
        }

        return new BattleCasualtyResult(updatedUnit, activeLoss, killedTroops, woundedTroops);
    }

    private static string FormatCasualtyResult(BattleCasualtyResult result)
    {
        if (result.WoundedTroops <= 0)
        {
            return $"killed {result.KilledTroops:N0}";
        }

        return $"killed {result.KilledTroops:N0}, wounded {result.WoundedTroops:N0}";
    }

    private bool IsClosedGateStructureTarget(BattleGridKey targetGrid)
    {
        if (_mapData == null || targetGrid.Level != 0 || !IsWithinMap(targetGrid.Grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(targetGrid.X, targetGrid.Y);
        return cell.Structure == BattleStructureType.Gate &&
               cell.HasStructureHealth &&
               !cell.IsGateOpen &&
               !cell.IsBroken;
    }

    private int ApplyGateGroupDamage(Vector2I gateGrid, int damage)
    {
        if (_mapData == null)
        {
            return 0;
        }

        var gateGroup = GetConnectedGateGroup(gateGrid);
        if (gateGroup.Count == 0)
        {
            return 0;
        }

        var groupHealth = gateGroup
            .Select(grid => _mapData.GetCell(grid.X, grid.Y).StructureHealth)
            .Min();
        var actualDamage = Mathf.Min(groupHealth, damage);
        var remainingHealth = Mathf.Max(0, groupHealth - damage);
        foreach (var groupGateGrid in gateGroup)
        {
            var gateCell = _mapData.GetCell(groupGateGrid.X, groupGateGrid.Y);
            gateCell.StructureHealth = remainingHealth;
        }

        if (remainingHealth <= 0)
        {
            OpenGateGroup(gateGroup);
            return actualDamage;
        }

        foreach (var groupGateGrid in gateGroup)
        {
            RefreshCastleDepthVisual(groupGateGrid);
        }

        return actualDamage;
    }

    private void ShowDamagePopup(BattleGridKey targetGrid, int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        ShowBattlePopup(
            targetGrid,
            $"-{damage:N0}",
            new Color(1.0f, 0.10f, 0.06f, 1.0f),
            new Color(0.05f, 0.02f, 0.01f, 0.95f),
            new Vector2(-18.0f, -88.0f),
            DamagePopupDurationSeconds,
            22);
    }

    private void ShowRepairPopup(BattleGridKey targetGrid, int repairAmount)
    {
        if (repairAmount <= 0)
        {
            return;
        }

        ShowBattlePopup(
            targetGrid,
            $"+{repairAmount:N0}",
            new Color(0.18f, 0.95f, 0.34f, 1.0f),
            new Color(0.01f, 0.08f, 0.03f, 0.95f),
            new Vector2(-18.0f, -88.0f),
            DamagePopupDurationSeconds,
            22);
    }

    private void ShowMoralePopup(BattleGridKey targetGrid, int moraleDelta, double initialDelaySeconds = 0.0)
    {
        if (moraleDelta == 0)
        {
            return;
        }

        if (initialDelaySeconds > 0.0)
        {
            ShowMoralePopupAfterDelay(targetGrid, moraleDelta, initialDelaySeconds);
            return;
        }

        var sign = moraleDelta > 0 ? "+" : "-";
        var fontColor = moraleDelta > 0
            ? new Color(0.42f, 0.82f, 1.0f, 1.0f)
            : new Color(1.0f, 0.72f, 0.18f, 1.0f);
        var outlineColor = moraleDelta > 0
            ? new Color(0.01f, 0.07f, 0.13f, 0.95f)
            : new Color(0.12f, 0.06f, 0.01f, 0.95f);
        ShowBattlePopup(
            targetGrid,
            BattleFormat("ui.battle.morale_popup", "Morale {0}{1}", sign, Math.Abs(moraleDelta)),
            fontColor,
            outlineColor,
            new Vector2(-30.0f, -112.0f),
            MoralePopupDurationSeconds,
            20);
    }

    private async void ShowMoralePopupAfterDelay(BattleGridKey targetGrid, int moraleDelta, double delaySeconds)
    {
        await ToSignal(GetTree().CreateTimer(delaySeconds), SceneTreeTimer.SignalName.Timeout);
        if (!GodotObject.IsInstanceValid(this))
        {
            return;
        }

        ShowMoralePopup(targetGrid, moraleDelta);
    }

    private void ShowHireOfficerPopup(BattleGridKey targetGrid)
    {
        ShowBattlePopup(
            targetGrid,
            BattleText("ui.battle.hire_success_popup", "Hired"),
            new Color(1.0f, 0.88f, 0.28f, 1.0f),
            new Color(0.12f, 0.07f, 0.01f, 0.95f),
            new Vector2(-34.0f, -138.0f),
            HireOfficerEffectDurationSeconds,
            22,
            HireOfficerPopupDelaySeconds);
    }

    private void ShowBattlePopup(
        BattleGridKey targetGrid,
        string text,
        Color fontColor,
        Color outlineColor,
        Vector2 offset,
        double durationSeconds,
        int fontSize,
        double initialDelaySeconds = 0.0)
    {
        if (_battleDepthLayer == null)
        {
            return;
        }

        var popup = new Label
        {
            Text = text,
            Position = GetMarkerPosition(targetGrid) + offset,
            ZIndex = 500,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = initialDelaySeconds > 0.0
                ? new Color(fontColor.R, fontColor.G, fontColor.B, 0.0f)
                : fontColor
        };
        popup.AddThemeColorOverride("font_color", fontColor);
        popup.AddThemeColorOverride("font_outline_color", outlineColor);
        popup.AddThemeConstantOverride("outline_size", 4);
        popup.AddThemeFontSizeOverride("font_size", fontSize);
        _battleDepthLayer.AddChild(popup);

        var tween = popup.CreateTween();
        if (initialDelaySeconds > 0.0)
        {
            tween.TweenInterval(initialDelaySeconds);
            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(popup))
                {
                    popup.Modulate = fontColor;
                }
            }));
        }

        tween.SetParallel(true);
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(popup, "position", popup.Position + new Vector2(0.0f, -34.0f), durationSeconds);
        tween.TweenProperty(popup, "modulate:a", 0.0f, durationSeconds);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => popup.QueueFree()));
    }

    private double PlayHireOfficerEffect(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (_battleDepthLayer == null)
        {
            return 0.0;
        }

        var sourcePosition = GetMarkerPosition(sourceGrid) + new Vector2(0.0f, -42.0f);
        var targetPosition = GetMarkerPosition(targetGrid) + new Vector2(0.0f, -34.0f);
        var link = new Line2D
        {
            Width = 4.0f,
            DefaultColor = new Color(1.0f, 0.76f, 0.24f, 1.0f),
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
            ZIndex = 518
        };
        link.AddPoint(sourcePosition);
        link.AddPoint(sourcePosition.Lerp(targetPosition, 0.5f) + new Vector2(0.0f, -28.0f));
        link.AddPoint(targetPosition);
        _battleDepthLayer.AddChild(link);

        var linkTween = link.CreateTween();
        linkTween.TweenProperty(link, "modulate:a", 0.92f, HireOfficerEffectDurationSeconds * 0.18);
        linkTween.TweenInterval(HireOfficerEffectDurationSeconds * 0.36);
        linkTween.TweenProperty(link, "modulate:a", 0.0f, HireOfficerEffectDurationSeconds * 0.28);
        linkTween.TweenCallback(Callable.From(() => link.QueueFree()));

        var ring = new Line2D
        {
            Width = 5.0f,
            DefaultColor = new Color(1.0f, 0.86f, 0.28f, 1.0f),
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
            Position = targetPosition + new Vector2(0.0f, 16.0f),
            ZIndex = 519
        };
        ring.AddPoint(new Vector2(0.0f, -22.0f));
        ring.AddPoint(new Vector2(34.0f, 0.0f));
        ring.AddPoint(new Vector2(0.0f, 22.0f));
        ring.AddPoint(new Vector2(-34.0f, 0.0f));
        ring.AddPoint(new Vector2(0.0f, -22.0f));
        _battleDepthLayer.AddChild(ring);

        var ringTween = ring.CreateTween();
        ringTween.SetParallel(true);
        ringTween.TweenProperty(ring, "modulate:a", 0.95f, HireOfficerEffectDurationSeconds * 0.16);
        ringTween.TweenProperty(ring, "scale", new Vector2(1.35f, 1.35f), HireOfficerEffectDurationSeconds * 0.62);
        ringTween.SetParallel(false);
        ringTween.TweenProperty(ring, "modulate:a", 0.0f, HireOfficerEffectDurationSeconds * 0.22);
        ringTween.TweenCallback(Callable.From(() => ring.QueueFree()));

        var motes = new[]
        {
            new Vector2(-24.0f, -8.0f),
            new Vector2(-9.0f, -18.0f),
            new Vector2(10.0f, -15.0f),
            new Vector2(25.0f, -5.0f)
        };
        for (var index = 0; index < motes.Length; index++)
        {
            var mote = new ColorRect
            {
                Color = new Color(1.0f, 0.72f, 0.18f, 0.92f),
                Position = targetPosition + motes[index],
                Size = new Vector2(7.0f, 7.0f),
                PivotOffset = new Vector2(3.5f, 3.5f),
                Rotation = index * 0.42f,
                ZIndex = 520,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _battleDepthLayer.AddChild(mote);

            var moteTween = mote.CreateTween();
            moteTween.SetParallel(true);
            moteTween.TweenProperty(mote, "position", mote.Position + new Vector2(0.0f, -26.0f - index * 3.0f), HireOfficerEffectDurationSeconds * 0.72);
            moteTween.TweenProperty(mote, "rotation", mote.Rotation + 2.8f, HireOfficerEffectDurationSeconds * 0.72);
            moteTween.SetParallel(false);
            moteTween.TweenProperty(mote, "modulate:a", 0.0f, HireOfficerEffectDurationSeconds * 0.2);
            moteTween.TweenCallback(Callable.From(() => mote.QueueFree()));
        }

        ShowHireOfficerPopup(targetGrid);
        return HireOfficerEffectDurationSeconds;
    }

    private double PlayDropStoneEffect(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (_battleDepthLayer == null)
        {
            return 0.0;
        }

        var sourcePosition = GetMarkerPosition(sourceGrid) + new Vector2(0.0f, -16.0f);
        var targetPosition = GetMarkerPosition(targetGrid) + new Vector2(0.0f, -20.0f);
        var offsets = new[]
        {
            new Vector2(-12.0f, -6.0f),
            new Vector2(4.0f, -14.0f),
            new Vector2(14.0f, -4.0f)
        };

        for (var index = 0; index < offsets.Length; index++)
        {
            var stone = new ColorRect
            {
                Color = new Color(0.32f, 0.27f, 0.20f, 1.0f),
                Position = sourcePosition + offsets[index],
                Size = new Vector2(10.0f, 8.0f),
                PivotOffset = new Vector2(5.0f, 4.0f),
                Rotation = index * 0.55f,
                ZIndex = 520,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _battleDepthLayer.AddChild(stone);

            var tween = stone.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(stone, "position", targetPosition + offsets[index] * 0.45f, DropStoneEffectDurationSeconds);
            tween.TweenProperty(stone, "rotation", stone.Rotation + 4.0f + index, DropStoneEffectDurationSeconds);
            tween.TweenProperty(stone, "scale", new Vector2(1.35f, 1.35f), DropStoneEffectDurationSeconds);
            tween.SetParallel(false);
            tween.TweenCallback(Callable.From(() => stone.QueueFree()));
        }

        var impact = new ColorRect
        {
            Color = new Color(1.0f, 0.70f, 0.22f, 1.0f),
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
            Position = targetPosition - new Vector2(22.0f, 3.0f),
            Size = new Vector2(44.0f, 6.0f),
            PivotOffset = new Vector2(22.0f, 3.0f),
            Rotation = 0.35f,
            ZIndex = 519,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _battleDepthLayer.AddChild(impact);

        var impactTween = impact.CreateTween();
        impactTween.TweenInterval(DropStoneEffectDurationSeconds * 0.72);
        impactTween.SetParallel(true);
        impactTween.TweenProperty(impact, "modulate:a", 1.0f, 0.06);
        impactTween.TweenProperty(impact, "scale", new Vector2(1.5f, 1.5f), 0.18);
        impactTween.SetParallel(false);
        impactTween.TweenProperty(impact, "modulate:a", 0.0f, 0.18);
        impactTween.TweenCallback(Callable.From(() => impact.QueueFree()));

        return DropStoneEffectDurationSeconds;
    }

    private double PlayPourOilEffect(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (_battleDepthLayer == null)
        {
            return 0.0;
        }

        var sourcePosition = GetMarkerPosition(sourceGrid) + new Vector2(4.0f, -12.0f);
        var targetPosition = GetMarkerPosition(targetGrid) + new Vector2(0.0f, -16.0f);
        var streamOffsets = new[] { -8.0f, 3.0f, 12.0f };
        foreach (var offsetX in streamOffsets)
        {
            var oilStream = new ColorRect
            {
                Color = new Color(0.96f, 0.31f, 0.05f, 0.92f),
                Position = sourcePosition + new Vector2(offsetX, 0.0f),
                Size = new Vector2(7.0f, 18.0f),
                PivotOffset = new Vector2(3.5f, 9.0f),
                Rotation = 0.38f,
                ZIndex = 520,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _battleDepthLayer.AddChild(oilStream);

            var streamTween = oilStream.CreateTween();
            streamTween.SetParallel(true);
            streamTween.TweenProperty(oilStream, "position", targetPosition + new Vector2(offsetX * 0.45f, 0.0f), PourOilEffectDurationSeconds * 0.7);
            streamTween.TweenProperty(oilStream, "scale", new Vector2(1.45f, 1.8f), PourOilEffectDurationSeconds * 0.7);
            streamTween.SetParallel(false);
            streamTween.TweenProperty(oilStream, "modulate:a", 0.0f, PourOilEffectDurationSeconds * 0.3);
            streamTween.TweenCallback(Callable.From(() => oilStream.QueueFree()));
        }

        var oilSplash = new ColorRect
        {
            Color = new Color(1.0f, 0.58f, 0.08f, 1.0f),
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
            Position = targetPosition - new Vector2(26.0f, 7.0f),
            Size = new Vector2(52.0f, 14.0f),
            PivotOffset = new Vector2(26.0f, 7.0f),
            Rotation = -0.18f,
            ZIndex = 519,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _battleDepthLayer.AddChild(oilSplash);

        var splashTween = oilSplash.CreateTween();
        splashTween.TweenInterval(PourOilEffectDurationSeconds * 0.58);
        splashTween.SetParallel(true);
        splashTween.TweenProperty(oilSplash, "modulate:a", 0.95f, 0.08);
        splashTween.TweenProperty(oilSplash, "scale", new Vector2(1.35f, 1.5f), 0.2);
        splashTween.SetParallel(false);
        splashTween.TweenProperty(oilSplash, "modulate:a", 0.0f, 0.2);
        splashTween.TweenCallback(Callable.From(() => oilSplash.QueueFree()));

        return PourOilEffectDurationSeconds;
    }

    private double PlayCatapultProjectileEffect(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (_battleDepthLayer == null)
        {
            return 0.0;
        }

        var sourcePosition = GetMarkerPosition(sourceGrid) + new Vector2(0.0f, -20.0f);
        var targetPosition = GetMarkerPosition(targetGrid) + new Vector2(0.0f, -18.0f);
        var arcPeakPosition = sourcePosition.Lerp(targetPosition, 0.5f) + new Vector2(0.0f, -84.0f);
        _catapultStoneTexture ??= GD.Load<Texture2D>(CatapultStoneTexturePath);
        if (_catapultStoneTexture == null)
        {
            return 0.0;
        }

        var projectile = new Sprite2D
        {
            Texture = _catapultStoneTexture,
            Position = sourcePosition,
            Rotation = 0.25f,
            Scale = new Vector2(0.68f, 1.18f),
            ZIndex = 520
        };
        _battleDepthLayer.AddChild(projectile);

        var projectileMovementTween = projectile.CreateTween();
        projectileMovementTween.TweenProperty(projectile, "position", arcPeakPosition, CatapultProjectileEffectDurationSeconds * 0.5);
        projectileMovementTween.TweenProperty(projectile, "position", targetPosition, CatapultProjectileEffectDurationSeconds * 0.5);
        projectileMovementTween.TweenCallback(Callable.From(() => projectile.QueueFree()));

        var projectileRotationTween = projectile.CreateTween();
        projectileRotationTween.SetParallel(true);
        projectileRotationTween.TweenProperty(projectile, "rotation", projectile.Rotation + 8.0f, CatapultProjectileEffectDurationSeconds);
        projectileRotationTween.TweenProperty(projectile, "scale", new Vector2(0.92f, 1.48f), CatapultProjectileEffectDurationSeconds);

        var impact = new ColorRect
        {
            Color = new Color(1.0f, 0.76f, 0.30f, 1.0f),
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
            Position = targetPosition - new Vector2(32.0f, 5.0f),
            Size = new Vector2(64.0f, 10.0f),
            PivotOffset = new Vector2(32.0f, 5.0f),
            Rotation = 0.18f,
            ZIndex = 519,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _battleDepthLayer.AddChild(impact);

        var impactTween = impact.CreateTween();
        impactTween.TweenInterval(CatapultProjectileEffectDurationSeconds * 0.78);
        impactTween.SetParallel(true);
        impactTween.TweenProperty(impact, "modulate:a", 1.0f, 0.06);
        impactTween.TweenProperty(impact, "scale", new Vector2(1.6f, 1.6f), 0.16);
        impactTween.SetParallel(false);
        impactTween.TweenProperty(impact, "modulate:a", 0.0f, 0.16);
        impactTween.TweenCallback(Callable.From(() => impact.QueueFree()));

        return CatapultProjectileEffectDurationSeconds;
    }

    private double PlayArrowProjectileEffect(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (_battleDepthLayer == null)
        {
            return 0.0;
        }

        var sourcePosition = GetMarkerPosition(sourceGrid) + new Vector2(0.0f, -22.0f);
        var targetPosition = GetMarkerPosition(targetGrid) + new Vector2(0.0f, -18.0f);
        var travel = targetPosition - sourcePosition;
        if (travel.LengthSquared() < 1.0f)
        {
            return 0.0;
        }

        var direction = travel.Normalized();
        var arrow = new Node2D
        {
            Position = sourcePosition,
            Rotation = travel.Angle(),
            ZIndex = 521
        };
        var shaft = new Line2D
        {
            Width = 2.0f,
            DefaultColor = new Color("e7d6a3")
        };
        shaft.AddPoint(new Vector2(-16.0f, 0.0f));
        shaft.AddPoint(new Vector2(8.0f, 0.0f));
        var arrowhead = new Polygon2D
        {
            Polygon = [new Vector2(14.0f, 0.0f), new Vector2(4.0f, -5.0f), new Vector2(4.0f, 5.0f)],
            Color = new Color("b67635")
        };
        arrow.AddChild(shaft);
        arrow.AddChild(arrowhead);
        _battleDepthLayer.AddChild(arrow);

        var tween = arrow.CreateTween();
        tween.TweenProperty(arrow, "position", targetPosition - direction * 14.0f, ArrowProjectileEffectDurationSeconds);
        tween.TweenCallback(Callable.From(() => arrow.QueueFree()));
        return ArrowProjectileEffectDurationSeconds;
    }

    private static bool IsArrowProjectileAttacker(BattleOccupantInfo occupant)
    {
        return occupant.Category == CategoryUnit &&
               (occupant.TroopType == TroopArcher || occupant.TroopType == TroopCrossbow);
    }

    private async void DestroyOccupantAfterDelay(BattleGridKey grid, BattleOccupantInfo occupant, double delaySeconds)
    {
        if (delaySeconds > 0.0)
        {
            await ToSignal(GetTree().CreateTimer(delaySeconds), SceneTreeTimer.SignalName.Timeout);
        }

        if (occupant.TroopType == TroopSupplyCart && IsOccupantAtGrid(grid, occupant))
        {
            ApplySupplyCartDestroyedMoralePenalty(occupant);
        }

        RemoveOccupant(grid, occupant);
        RefreshBattleDepthLayerOrder();
        RefreshOccludedUnitSilhouettes();
        ConfigureHud();
    }

    private bool IsOccupantAtGrid(BattleGridKey grid, BattleOccupantInfo occupant)
    {
        return _occupantsByGrid.TryGetValue(grid, out var occupants) && occupants.Contains(occupant);
    }

    private void RemoveOccupant(BattleGridKey grid, BattleOccupantInfo occupant)
    {
        if (!_occupantsByGrid.TryGetValue(grid, out var occupants))
        {
            return;
        }

        if (!occupants.Remove(occupant))
        {
            return;
        }

        ApplyTeamSiegeUnitDelta(occupant.Category, occupant.TeamName, -1);
        ApplyTeamGeneralDelta(occupant.Category, occupant.TeamName, occupant.OfficerName, -1);
        if (occupant.Marker != null)
        {
            _battleDepthEntries.Remove(occupant.Marker);
            occupant.Marker.QueueFree();
        }

        if (occupants.Count == 0)
        {
            _occupantsByGrid.Remove(grid);
        }

        if (_selectedUnit == occupant)
        {
            _selectedUnit = null;
            _selectedUnitGrid = null;
            _selectedGridKey = null;
        }
    }

    private static BattleOccupantInfo? GetAttackTarget(IEnumerable<BattleOccupantInfo> occupants)
    {
        return occupants.FirstOrDefault(static occupant => occupant.Marker != null && IsBattlePiece(occupant));
    }

    private static BattleOccupantInfo? GetFireDamageTarget(IEnumerable<BattleOccupantInfo> occupants)
    {
        // Fire is a tile effect: hidden units on the burning tile are still hurt.
        return occupants.FirstOrDefault(static occupant => occupant.Marker != null && IsBattlePiece(occupant));
    }

    private BattleOccupantInfo? GetAttackTarget(IEnumerable<BattleOccupantInfo> occupants, string attackerTeamName)
    {
        return occupants.FirstOrDefault(occupant =>
            occupant.Marker != null &&
            IsBattlePiece(occupant) &&
            occupant.TeamName != attackerTeamName &&
            !IsHiddenFromSide(occupant, attackerTeamName));
    }

    private BattleOccupantInfo? GetAttackTargetForAttack(IEnumerable<BattleOccupantInfo> occupants, string attackerTeamName, BattleGridKey targetGrid)
    {
        var canHitHiddenInForest = IsForestGrid(targetGrid);
        return occupants.FirstOrDefault(occupant =>
            occupant.Marker != null &&
            IsBattlePiece(occupant) &&
            occupant.TeamName != attackerTeamName &&
            (canHitHiddenInForest || !IsHiddenFromSide(occupant, attackerTeamName)));
    }

    private static int GetAttackDamage(BattleOccupantInfo attacker)
    {
        if (CanUseAmmoDepletedWeakAttack(attacker))
        {
            return Mathf.Max(1, Mathf.RoundToInt(GetBaseAttackDamage(attacker) * AmmoDepletedWeakAttackDamageRatio));
        }

        return GetBaseAttackDamage(attacker);
    }

    private static int GetBaseAttackDamage(BattleOccupantInfo attacker)
    {
        if (attacker.Category == CategorySiegeEngine)
        {
            return attacker.TroopType switch
            {
                TroopRam => RamAttackDamage,
                TroopCatapult => CatapultAttackDamage,
                _ => 0
            };
        }

        return attacker.TroopType switch
        {
            TroopInfantry => InfantryAttackDamage,
            TroopSpearman => SpearmanAttackDamage,
            TroopArcher or TroopCrossbow => ArcherAttackDamage,
            TroopCavalry => CavalryAttackDamage,
            TroopWorker => WorkerAttackDamage,
            _ => 0
        };
    }

    private static int GetStructureAttackDamage(BattleOccupantInfo attacker)
    {
        if (attacker.Category == CategorySiegeEngine)
        {
            return attacker.TroopType switch
            {
                TroopRam => RamStructureDamage,
                TroopCatapult => CatapultStructureDamage,
                _ => 0
            };
        }

        return attacker.TroopType switch
        {
            TroopInfantry => InfantryStructureDamage,
            TroopSpearman => SpearmanStructureDamage,
            TroopArcher or TroopCrossbow => ArcherStructureDamage,
            TroopCavalry => CavalryStructureDamage,
            TroopWorker => WorkerStructureDamage,
            _ => 0
        };
    }

    private async void RefreshOccludedUnitSilhouettesAfterDelay(double durationSeconds)
    {
        if (durationSeconds > 0.0)
        {
            await ToSignal(GetTree().CreateTimer(durationSeconds), SceneTreeTimer.SignalName.Timeout);
        }

        RefreshOccludedUnitSilhouettes();
    }

    private static string GetInitialInfantryDirectionScene(string teamName)
    {
        _ = teamName;
        return InfantryIdleSouthEastScenePath;
    }

    private static BattleSpriteDirection GetInfantryDirection(Vector2I sourceGrid, Vector2I destinationGrid)
    {
        var delta = destinationGrid - sourceGrid;
        if (delta == Vector2I.Zero)
        {
            return BattleSpriteDirection.SouthEast;
        }

        if (delta.Y == 0)
        {
            return delta.X > 0
                ? BattleSpriteDirection.SouthEast
                : BattleSpriteDirection.NorthWest;
        }

        if (delta.X == 0)
        {
            return delta.Y > 0
                ? BattleSpriteDirection.SouthWest
                : BattleSpriteDirection.NorthEast;
        }

        if (delta.X > 0)
        {
            return delta.Y > 0
                ? BattleSpriteDirection.SouthEast
                : BattleSpriteDirection.NorthEast;
        }

        if (delta.X < 0)
        {
            return delta.Y > 0
                ? BattleSpriteDirection.SouthWest
                : BattleSpriteDirection.NorthWest;
        }

        return delta.Y > 0
            ? BattleSpriteDirection.SouthWest
            : BattleSpriteDirection.NorthEast;
    }

    private static string GetInfantryIdleScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => InfantryIdleNorthEastScenePath,
            BattleSpriteDirection.NorthWest => InfantryIdleNorthWestScenePath,
            BattleSpriteDirection.SouthWest => InfantryIdleSouthWestScenePath,
            _ => InfantryIdleSouthEastScenePath
        };
    }

    private static string GetInfantryMoveScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => InfantryMoveNorthEastScenePath,
            BattleSpriteDirection.NorthWest => InfantryMoveNorthWestScenePath,
            BattleSpriteDirection.SouthWest => InfantryMoveSouthWestScenePath,
            _ => InfantryMoveSouthEastScenePath
        };
    }

    private static string GetInfantryAttackScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => InfantryAttackNorthEastScenePath,
            BattleSpriteDirection.NorthWest => InfantryAttackNorthWestScenePath,
            BattleSpriteDirection.SouthWest => InfantryAttackSouthWestScenePath,
            _ => InfantryAttackSouthEastScenePath
        };
    }

    private static string GetInfantryHurtScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => InfantryHurtNorthEastScenePath,
            BattleSpriteDirection.NorthWest => InfantryHurtNorthWestScenePath,
            BattleSpriteDirection.SouthWest => InfantryHurtSouthWestScenePath,
            _ => InfantryHurtSouthEastScenePath
        };
    }

    private static string GetSpearmanIdleScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => SpearmanIdleNorthEastScenePath,
            BattleSpriteDirection.NorthWest => SpearmanIdleNorthWestScenePath,
            BattleSpriteDirection.SouthWest => SpearmanIdleSouthWestScenePath,
            _ => SpearmanIdleSouthEastScenePath
        };
    }

    private static string GetSpearmanMoveScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => SpearmanMoveNorthEastScenePath,
            BattleSpriteDirection.NorthWest => SpearmanMoveNorthWestScenePath,
            BattleSpriteDirection.SouthWest => SpearmanMoveSouthWestScenePath,
            _ => SpearmanMoveSouthEastScenePath
        };
    }

    private static string GetSpearmanAttackScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => SpearmanAttackNorthEastScenePath,
            BattleSpriteDirection.NorthWest => SpearmanAttackNorthWestScenePath,
            BattleSpriteDirection.SouthWest => SpearmanAttackSouthWestScenePath,
            _ => SpearmanAttackSouthEastScenePath
        };
    }

    private static string GetSpearmanHurtScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => SpearmanHurtNorthEastScenePath,
            BattleSpriteDirection.NorthWest => SpearmanHurtNorthWestScenePath,
            BattleSpriteDirection.SouthWest => SpearmanHurtSouthWestScenePath,
            _ => SpearmanHurtSouthEastScenePath
        };
    }

    private static string GetCarIdleScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => CarIdleNorthEastScenePath,
            BattleSpriteDirection.NorthWest => CarIdleNorthWestScenePath,
            BattleSpriteDirection.SouthWest => CarIdleSouthWestScenePath,
            _ => CarIdleSouthEastScenePath
        };
    }

    private static string GetSupplyCarIdleScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => SupplyCarIdleNorthEastScenePath,
            BattleSpriteDirection.NorthWest => SupplyCarIdleNorthWestScenePath,
            BattleSpriteDirection.SouthWest => SupplyCarIdleSouthWestScenePath,
            _ => SupplyCarIdleSouthEastScenePath
        };
    }

    private static string GetCarLadderIdleScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => CarLadderIdleNorthEastScenePath,
            BattleSpriteDirection.NorthWest => CarLadderIdleNorthWestScenePath,
            BattleSpriteDirection.SouthWest => CarLadderIdleSouthWestScenePath,
            _ => CarLadderIdleSouthEastScenePath
        };
    }

    private static string GetCatapultIdleScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => CatapultIdleNorthEastScenePath,
            BattleSpriteDirection.NorthWest => CatapultIdleNorthWestScenePath,
            BattleSpriteDirection.SouthWest => CatapultIdleSouthWestScenePath,
            _ => CatapultIdleSouthEastScenePath
        };
    }

    private static string GetCatapultAttackScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => CatapultAttackNorthEastScenePath,
            BattleSpriteDirection.NorthWest => CatapultAttackNorthWestScenePath,
            BattleSpriteDirection.SouthWest => CatapultAttackSouthWestScenePath,
            _ => CatapultAttackSouthEastScenePath
        };
    }

    private static string GetWorkerIdleScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => WorkerIdleNorthEastScenePath,
            BattleSpriteDirection.NorthWest => WorkerIdleNorthWestScenePath,
            BattleSpriteDirection.SouthWest => WorkerIdleSouthWestScenePath,
            _ => WorkerIdleSouthEastScenePath
        };
    }

    private static string GetWorkerMoveScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => WorkerMoveNorthEastScenePath,
            BattleSpriteDirection.NorthWest => WorkerMoveNorthWestScenePath,
            BattleSpriteDirection.SouthWest => WorkerMoveSouthWestScenePath,
            _ => WorkerMoveSouthEastScenePath
        };
    }

    private static string GetWorkerWorkScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => WorkerWorkNorthEastScenePath,
            BattleSpriteDirection.NorthWest => WorkerWorkNorthWestScenePath,
            BattleSpriteDirection.SouthWest => WorkerWorkSouthWestScenePath,
            _ => WorkerWorkSouthEastScenePath
        };
    }

    private static string GetWorkerHurtScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => WorkerHurtNorthEastScenePath,
            BattleSpriteDirection.NorthWest => WorkerHurtNorthWestScenePath,
            BattleSpriteDirection.SouthWest => WorkerHurtSouthWestScenePath,
            _ => WorkerHurtSouthEastScenePath
        };
    }

    private static string GetWorkerAttackScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => WorkerAttackNorthEastScenePath,
            BattleSpriteDirection.NorthWest => WorkerAttackNorthWestScenePath,
            BattleSpriteDirection.SouthWest => WorkerAttackSouthWestScenePath,
            _ => WorkerAttackSouthEastScenePath
        };
    }

    private static string GetArcherIdleScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => ArcherIdleNorthEastScenePath,
            BattleSpriteDirection.NorthWest => ArcherIdleNorthWestScenePath,
            BattleSpriteDirection.SouthWest => ArcherIdleSouthWestScenePath,
            _ => ArcherIdleSouthEastScenePath
        };
    }

    private static string GetArcherMoveScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => ArcherMoveNorthEastScenePath,
            BattleSpriteDirection.NorthWest => ArcherMoveNorthWestScenePath,
            BattleSpriteDirection.SouthWest => ArcherMoveSouthWestScenePath,
            _ => ArcherMoveSouthEastScenePath
        };
    }

    private static string GetArcherAttackScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => ArcherAttackNorthEastScenePath,
            BattleSpriteDirection.NorthWest => ArcherAttackNorthWestScenePath,
            BattleSpriteDirection.SouthWest => ArcherAttackSouthWestScenePath,
            _ => ArcherAttackSouthEastScenePath
        };
    }

    private static string GetArcherHurtScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => ArcherHurtNorthEastScenePath,
            BattleSpriteDirection.NorthWest => ArcherHurtNorthWestScenePath,
            BattleSpriteDirection.SouthWest => ArcherHurtSouthWestScenePath,
            _ => ArcherHurtSouthEastScenePath
        };
    }

    private static string GetCavalryIdleScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => CavalryIdleNorthEastScenePath,
            BattleSpriteDirection.NorthWest => CavalryIdleNorthWestScenePath,
            BattleSpriteDirection.SouthWest => CavalryIdleSouthWestScenePath,
            _ => CavalryIdleSouthEastScenePath
        };
    }

    private static string GetCavalryMoveScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => CavalryMoveNorthEastScenePath,
            BattleSpriteDirection.NorthWest => CavalryMoveNorthWestScenePath,
            BattleSpriteDirection.SouthWest => CavalryMoveSouthWestScenePath,
            _ => CavalryMoveSouthEastScenePath
        };
    }

    private static string GetCavalryAttackScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => CavalryAttackNorthEastScenePath,
            BattleSpriteDirection.NorthWest => CavalryAttackNorthWestScenePath,
            BattleSpriteDirection.SouthWest => CavalryAttackSouthWestScenePath,
            _ => CavalryAttackSouthEastScenePath
        };
    }

    private static string GetCavalryHurtScene(BattleSpriteDirection direction)
    {
        return direction switch
        {
            BattleSpriteDirection.NorthEast => CavalryHurtNorthEastScenePath,
            BattleSpriteDirection.NorthWest => CavalryHurtNorthWestScenePath,
            BattleSpriteDirection.SouthWest => CavalryHurtSouthWestScenePath,
            _ => CavalryHurtSouthEastScenePath
        };
    }

    private void UpdateUnitSelection()
    {
        _selectedUnit = null;
        _selectedUnitGrid = null;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _chargeTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        _commandMode = BattleCommandMode.None;

        if (!_selectedGrid.HasValue || _mapData == null)
        {
            return;
        }

        var selectedGridKey = _selectedGridKey;
        if (!selectedGridKey.HasValue)
        {
            return;
        }

        if (!_occupantsByGrid.TryGetValue(selectedGridKey.Value, out var occupants))
        {
            return;
        }

        var selectedUnit = occupants.FirstOrDefault(occupant => IsBattlePiece(occupant) && IsVisibleToCurrentTurnSide(occupant));
        if (selectedUnit == null)
        {
            return;
        }

        _selectedUnit = selectedUnit;
        _selectedUnitGrid = selectedGridKey.Value;
        if (IsCurrentTurnPiece(selectedUnit))
        {
            _commandMode = BattleCommandMode.AwaitingCommand;
        }
    }

    private IEnumerable<BattleGridKey> CalculateReachableGrids(BattleGridKey startGrid, int moveRange)
    {
        if (_mapData == null || moveRange <= 0)
        {
            yield break;
        }

        var frontier = new Queue<(BattleGridKey Grid, int RemainingMove)>();
        var bestRemaining = new Dictionary<BattleGridKey, int> { [startGrid] = moveRange };
        frontier.Enqueue((startGrid, moveRange));

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var step in GetMovementNeighbors(current.Grid))
            {
                var neighbor = step.Grid;
                if (!IsWithinMap(neighbor.Grid))
                {
                    continue;
                }

                var cell = _mapData.GetCell(neighbor.X, neighbor.Y);
                if (!CanEnterCell(current.Grid, neighbor, cell, step.UsesLadderBridge))
                {
                    continue;
                }

                if (IsCellBlockingMovement(cell))
                {
                    if (!CanTraverseBlockedCell(neighbor, cell, step.UsesLadderBridge))
                    {
                        continue;
                    }
                }

                if (neighbor != startGrid && HasBlockingOccupant(neighbor))
                {
                    continue;
                }

                var moveCost = GetMoveCost(cell);
                var remainingMove = current.RemainingMove - moveCost;
                if (remainingMove < 0)
                {
                    continue;
                }

                if (bestRemaining.TryGetValue(neighbor, out var knownRemaining) && knownRemaining >= remainingMove)
                {
                    continue;
                }

                bestRemaining[neighbor] = remainingMove;
                frontier.Enqueue((neighbor, remainingMove));

                if (neighbor != startGrid)
                {
                    yield return neighbor;
                }
            }
        }
    }

    private bool TryBuildMovePath(BattleGridKey startGrid, BattleGridKey destinationGrid, int moveRange, out List<BattleGridKey> path)
    {
        path = [];
        if (_mapData == null || moveRange <= 0)
        {
            return false;
        }

        var frontier = new PriorityQueue<BattleGridKey, (int EstimatedTotalCost, int LayerChanges, int GateVerticalSteps, int Steps, int Sequence)>();
        var bestCost = new Dictionary<BattleGridKey, int> { [startGrid] = 0 };
        var pathStatsByGrid = new Dictionary<BattleGridKey, (int LayerChanges, int GateVerticalSteps, int Steps)>
        {
            [startGrid] = (0, 0, 0)
        };
        var previousByGrid = new Dictionary<BattleGridKey, BattleGridKey>();
        var sequence = 0;
        frontier.Enqueue(startGrid, (EstimateMoveCost(startGrid, destinationGrid), 0, 0, 0, sequence++));

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == destinationGrid)
            {
                path = RebuildMovePath(startGrid, destinationGrid, previousByGrid);
                return path.Count > 0;
            }

            foreach (var step in GetMovementNeighbors(current))
            {
                var neighbor = step.Grid;
                if (!IsWithinMap(neighbor.Grid))
                {
                    continue;
                }

                var cell = _mapData.GetCell(neighbor.X, neighbor.Y);
                if (!CanEnterCell(current, neighbor, cell, step.UsesLadderBridge))
                {
                    continue;
                }

                if (IsCellBlockingMovement(cell) && !CanTraverseBlockedCell(neighbor, cell, step.UsesLadderBridge))
                {
                    continue;
                }

                if (neighbor != startGrid && HasBlockingOccupant(neighbor))
                {
                    continue;
                }

                var moveCost = GetMoveCost(cell);
                var newCost = bestCost[current] + moveCost;
                if (newCost > moveRange)
                {
                    continue;
                }

                var currentStats = pathStatsByGrid[current];
                var newStats = (
                    LayerChanges: currentStats.LayerChanges + (current.Level == neighbor.Level ? 0 : 1),
                    GateVerticalSteps: currentStats.GateVerticalSteps + (IsGateVerticalLayerMove(current, neighbor) ? 1 : 0),
                    Steps: currentStats.Steps + 1);
                if (bestCost.TryGetValue(neighbor, out var knownCost) &&
                    (knownCost < newCost ||
                     knownCost == newCost && !IsBetterPathTieBreak(newStats, pathStatsByGrid[neighbor])))
                {
                    continue;
                }

                bestCost[neighbor] = newCost;
                pathStatsByGrid[neighbor] = newStats;
                previousByGrid[neighbor] = current;
                var estimatedTotalCost = newCost + EstimateMoveCost(neighbor, destinationGrid);
                frontier.Enqueue(neighbor, (estimatedTotalCost, newStats.LayerChanges, newStats.GateVerticalSteps, newStats.Steps, sequence++));
            }
        }

        return false;
    }

    private static int EstimateMoveCost(BattleGridKey fromGrid, BattleGridKey toGrid)
    {
        return Mathf.Abs(fromGrid.X - toGrid.X) +
               Mathf.Abs(fromGrid.Y - toGrid.Y) +
               (fromGrid.Level == toGrid.Level ? 0 : 1);
    }

    private static bool IsBetterPathTieBreak(
        (int LayerChanges, int GateVerticalSteps, int Steps) candidate,
        (int LayerChanges, int GateVerticalSteps, int Steps) known)
    {
        return candidate.LayerChanges < known.LayerChanges ||
               candidate.LayerChanges == known.LayerChanges && candidate.GateVerticalSteps < known.GateVerticalSteps ||
               candidate.LayerChanges == known.LayerChanges && candidate.GateVerticalSteps == known.GateVerticalSteps && candidate.Steps < known.Steps;
    }

    private static List<BattleGridKey> RebuildMovePath(BattleGridKey startGrid, BattleGridKey destinationGrid, IReadOnlyDictionary<BattleGridKey, BattleGridKey> previousByGrid)
    {
        var path = new List<BattleGridKey>();
        var current = destinationGrid;
        while (current != startGrid)
        {
            path.Add(current);
            if (!previousByGrid.TryGetValue(current, out current))
            {
                return [];
            }
        }

        path.Reverse();
        return path;
    }

    private List<BattleGridKey> ExpandMovePathWithCarLadderWaypoints(BattleGridKey sourceGrid, IReadOnlyList<BattleGridKey> movePath, BattleOccupantInfo movingOccupant)
    {
        if (_mapData == null || movePath.Count == 0 || !CanUseCarLadderBridge(movingOccupant))
        {
            return movePath.ToList();
        }

        var expandedPath = new List<BattleGridKey>();
        var previousGrid = sourceGrid;
        foreach (var nextGrid in movePath)
        {
            if (TryGetCarLadderGridForTransition(previousGrid, nextGrid, out var ladderGrid))
            {
                AddPathGrid(expandedPath, ladderGrid, previousGrid);
            }

            AddPathGrid(expandedPath, nextGrid, previousGrid);
            previousGrid = nextGrid;
        }

        return expandedPath;
    }

    private bool TryGetCarLadderGridForTransition(BattleGridKey fromGrid, BattleGridKey toGrid, out BattleGridKey ladderGrid)
    {
        ladderGrid = default;
        if (_mapData == null || !IsWithinMap(fromGrid.Grid) || !IsWithinMap(toGrid.Grid))
        {
            return false;
        }

        if (!ShouldUseCarLadderBridgeForMove(fromGrid, toGrid))
        {
            return false;
        }

        foreach (var candidateLadderGrid in GetUsableCarLadderGrids())
        {
            var groundGrids = GetCarLadderGroundEndpoints(candidateLadderGrid.Grid).Select(ToGroundGridKey).ToList();
            var wallWalkGrids = GetCarLadderWallTopEndpoints(candidateLadderGrid.Grid).Select(ToWallWalkGridKey).ToList();
            var matchesGroundToWall = fromGrid.Level != 2 &&
                                      groundGrids.Contains(fromGrid) &&
                                      wallWalkGrids.Contains(toGrid);
            var matchesWallToGround = fromGrid.Level == 2 &&
                                      wallWalkGrids.Contains(fromGrid) &&
                                      groundGrids.Contains(toGrid);
            if (!matchesGroundToWall && !matchesWallToGround)
            {
                continue;
            }

            ladderGrid = candidateLadderGrid;
            return true;
        }

        return false;
    }

    private IEnumerable<(BattleGridKey Grid, bool UsesLadderBridge)> GetMovementNeighbors(BattleGridKey grid)
    {
        var verticalGateStep = ResolveGateVerticalStepGridKey(grid);
        if (verticalGateStep.HasValue)
        {
            yield return (verticalGateStep.Value, false);
        }

        foreach (var neighbor in GetOrthogonalNeighbors(grid.Grid))
        {
            var neighborKey = ResolveStepGridKey(grid, neighbor);
            if (neighborKey.HasValue)
            {
                yield return (neighborKey.Value, false);
            }
        }

        foreach (var bridgeNeighbor in GetCarLadderBridgeNeighbors(grid))
        {
            yield return (bridgeNeighbor, true);
        }
    }

    private BattleGridKey? ResolveGateVerticalStepGridKey(BattleGridKey grid)
    {
        if (_mapData == null || !IsWithinMap(grid.Grid) || !CanUseGateVerticalStep(grid))
        {
            return null;
        }

        return grid.Level switch
        {
            2 => ToGroundGridKey(grid.Grid),
            0 => ToWallWalkGridKey(grid.Grid),
            _ => null
        };
    }

    private bool CanUseGateVerticalStep(BattleGridKey grid)
    {
        if (_mapData == null || _selectedUnit == null || !IsWithinMap(grid.Grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (cell.Structure != BattleStructureType.Gate)
        {
            return false;
        }

        if (cell.IsGateOpen || cell.IsBroken)
        {
            return true;
        }

        if (_selectedUnit.Category != CategoryUnit)
        {
            return false;
        }

        return IsDefenderPiece(_selectedUnit) || grid.Level == 2;
    }

    private IEnumerable<BattleGridKey> GetCarLadderBridgeNeighbors(BattleGridKey grid)
    {
        if (_mapData == null || _selectedUnit == null || !CanUseCarLadderBridge(_selectedUnit) || !IsWithinMap(grid.Grid))
        {
            yield break;
        }

        var currentCell = _mapData.GetCell(grid.X, grid.Y);
        foreach (var ladderGrid in GetUsableCarLadderGrids())
        {
            var bridgeGroundGrids = GetCarLadderGroundEndpoints(ladderGrid.Grid).Select(ToGroundGridKey).ToList();
            var bridgeWallWalkGrids = GetCarLadderWallTopEndpoints(ladderGrid.Grid).Select(ToWallWalkGridKey).ToList();
            if (grid.Level == 2)
            {
                if (!bridgeWallWalkGrids.Contains(grid))
                {
                    continue;
                }

                foreach (var groundGrid in bridgeGroundGrids)
                {
                    yield return groundGrid;
                }

                continue;
            }

            if (!bridgeGroundGrids.Contains(grid))
            {
                continue;
            }

            foreach (var wallWalkGrid in bridgeWallWalkGrids)
            {
                yield return wallWalkGrid;
            }
        }
    }

    private bool TryGetCarLadderBridgePath(BattleGridKey sourceGrid, BattleGridKey destinationGrid, BattleOccupantInfo movingOccupant, out Vector2[] pathPositions, out BattleSpriteDirection[] pathDirections)
    {
        pathPositions = [];
        pathDirections = [];
        if (_mapData == null || !CanUseCarLadderBridge(movingOccupant) || !IsWithinMap(sourceGrid.Grid) || !IsWithinMap(destinationGrid.Grid))
        {
            return false;
        }

        if (!ShouldUseCarLadderBridgeForMove(sourceGrid, destinationGrid))
        {
            return false;
        }

        if (sourceGrid.Level == destinationGrid.Level ||
            (sourceGrid.Level != 2 && destinationGrid.Level != 2))
        {
            return false;
        }

        foreach (var ladderGrid in GetUsableCarLadderGrids())
        {
            var groundGrids = GetCarLadderGroundEndpoints(ladderGrid.Grid).Select(ToGroundGridKey).ToList();
            var wallWalkGrids = GetCarLadderWallTopEndpoints(ladderGrid.Grid).Select(ToWallWalkGridKey).ToList();
            var pathGrids = new List<BattleGridKey>();
            if (sourceGrid.Level != 2 && wallWalkGrids.Contains(destinationGrid))
            {
                var entryGrid = GetNearestGrid(sourceGrid, groundGrids);
                if (!entryGrid.HasValue)
                {
                    continue;
                }

                AddPathGrid(pathGrids, entryGrid.Value, sourceGrid);
                AddPathGrid(pathGrids, ladderGrid, sourceGrid);
                AddPathGrid(pathGrids, destinationGrid, sourceGrid);
            }
            else if (sourceGrid.Level == 2 && wallWalkGrids.Contains(sourceGrid))
            {
                var exitGrid = GetNearestGrid(destinationGrid, groundGrids);
                if (!exitGrid.HasValue)
                {
                    continue;
                }

                AddPathGrid(pathGrids, ladderGrid, sourceGrid);
                AddPathGrid(pathGrids, exitGrid.Value, sourceGrid);
                AddPathGrid(pathGrids, destinationGrid, sourceGrid);
            }
            else
            {
                continue;
            }

            if (pathGrids.Count == 0)
            {
                continue;
            }

            pathPositions = pathGrids.Select(GetMarkerPosition).ToArray();
            pathDirections = BuildPathDirections(sourceGrid, pathGrids);
            return true;
        }

        return false;
    }

    private static BattleGridKey? GetNearestGrid(BattleGridKey fromGrid, IEnumerable<BattleGridKey> candidates)
    {
        BattleGridKey? nearestGrid = null;
        var nearestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var distance = Mathf.Abs(candidate.X - fromGrid.X) + Mathf.Abs(candidate.Y - fromGrid.Y);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestGrid = candidate;
            nearestDistance = distance;
        }

        return nearestGrid;
    }

    private static void AddPathGrid(List<BattleGridKey> pathGrids, BattleGridKey grid, BattleGridKey sourceGrid)
    {
        if (grid == sourceGrid || (pathGrids.Count > 0 && pathGrids[^1] == grid))
        {
            return;
        }

        pathGrids.Add(grid);
    }

    private static BattleSpriteDirection[] BuildPathDirections(BattleGridKey sourceGrid, IReadOnlyList<BattleGridKey> pathGrids)
    {
        var directions = new BattleSpriteDirection[pathGrids.Count];
        var previousGrid = sourceGrid;
        for (var index = 0; index < pathGrids.Count; index++)
        {
            directions[index] = GetInfantryDirection(previousGrid.Grid, pathGrids[index].Grid);
            previousGrid = pathGrids[index];
        }

        return directions;
    }

    private static bool ShouldUseCarLadderBridgeForMove(BattleGridKey sourceGrid, BattleGridKey destinationGrid)
    {
        return sourceGrid.Level != destinationGrid.Level &&
               (sourceGrid.Level == 2 || destinationGrid.Level == 2);
    }

    private IEnumerable<BattleGridKey> GetUsableCarLadderGrids()
    {
        foreach (var (grid, occupants) in _occupantsByGrid)
        {
            if (!IsWithinMap(grid.Grid))
            {
                continue;
            }

            if (occupants.Any(static occupant =>
                    occupant.Category == CategorySiegeEngine &&
                    occupant.TroopType == TroopLadder &&
                    occupant.Marker != null))
            {
                yield return grid;
            }
        }
    }

    private IEnumerable<Vector2I> GetCarLadderGroundEndpoints(Vector2I ladderGrid)
    {
        if (_mapData == null)
        {
            yield break;
        }

        foreach (var neighbor in GetOrthogonalNeighbors(ladderGrid))
        {
            if (!IsWithinMap(neighbor))
            {
                continue;
            }

            var cell = _mapData.GetCell(neighbor.X, neighbor.Y);
            if (!IsWallTopGrid(neighbor) && !cell.IsBlockingStructure)
            {
                yield return neighbor;
            }
        }
    }

    private IEnumerable<Vector2I> GetCarLadderWallTopEndpoints(Vector2I ladderGrid)
    {
        if (_mapData == null)
        {
            yield break;
        }

        foreach (var direction in GetOrthogonalDirections())
        {
            var adjacentGrid = ladderGrid + direction;
            if (!IsWithinMap(adjacentGrid))
            {
                continue;
            }

            if (!IsWallTopGrid(adjacentGrid))
            {
                continue;
            }

            yield return adjacentGrid;
        }
    }

    private static IEnumerable<Vector2I> GetOrthogonalDirections()
    {
        yield return new Vector2I(1, 0);
        yield return new Vector2I(-1, 0);
        yield return new Vector2I(0, 1);
        yield return new Vector2I(0, -1);
    }

    private IEnumerable<Vector2I> GetOrthogonalNeighbors(Vector2I grid)
    {
        yield return new Vector2I(grid.X + 1, grid.Y);
        yield return new Vector2I(grid.X - 1, grid.Y);
        yield return new Vector2I(grid.X, grid.Y + 1);
        yield return new Vector2I(grid.X, grid.Y - 1);
    }

    private IEnumerable<Vector2I> GetAdjacentEightNeighbors(Vector2I grid)
    {
        for (var y = -1; y <= 1; y++)
        {
            for (var x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                yield return new Vector2I(grid.X + x, grid.Y + y);
            }
        }
    }

    private bool HasBlockingOccupant(BattleGridKey grid)
    {
        if (!_occupantsByGrid.TryGetValue(grid, out var occupants))
        {
            return false;
        }

        return occupants.Any(static occupant => IsBattlePiece(occupant));
    }

    private static bool IsBattlePiece(BattleOccupantInfo occupant)
    {
        return occupant.Category == CategoryUnit || occupant.Category == CategorySiegeEngine;
    }

    private static bool CanUseCarLadderBridge(BattleOccupantInfo occupant)
    {
        return IsAttackerPiece(occupant) &&
               occupant.Category == CategoryUnit &&
               (occupant.TroopType == TroopInfantry || occupant.TroopType == TroopSpearman || occupant.TroopType == TroopArcher);
    }

    private static int GetMoveCost(BattleCellData cell)
    {
        return cell.Terrain switch
        {
            BattleTerrainType.Forest => 2,
            _ => 1
        };
    }

    private void RefreshHighlights()
    {
        ClearHighlightDepthVisuals();
        if (_battleDepthLayer == null || _groundLayer == null)
        {
            return;
        }

        foreach (var grid in _movableGrids)
        {
            var visualKind = grid.Level == 2
                ? BattleHighlightVisualKind.WallTopMovable
                : BattleHighlightVisualKind.Movable;
            AddHighlightDepthVisual(grid, visualKind);
        }

        foreach (var grid in _attackableGrids)
        {
            AddHighlightDepthVisual(grid, BattleHighlightVisualKind.Attackable);
        }

        foreach (var grid in _workableGrids)
        {
            AddHighlightDepthVisual(grid, BattleHighlightVisualKind.Workable);
        }

        foreach (var grid in _strategyTargetGrids)
        {
            AddHighlightDepthVisual(grid, BattleHighlightVisualKind.Attackable);
        }

        foreach (var grid in _duelTargetGrids)
        {
            AddHighlightDepthVisual(grid, BattleHighlightVisualKind.Attackable);
        }

        foreach (var grid in _chargeTargetGrids)
        {
            AddHighlightDepthVisual(grid, BattleHighlightVisualKind.Attackable);
        }

        foreach (var grid in _hireOfficerTargetGrids)
        {
            AddHighlightDepthVisual(grid, BattleHighlightVisualKind.Attackable);
        }

        if (_selectedGridKey.HasValue && ShouldDisplaySelectedGridHighlight(_selectedGridKey.Value))
        {
            AddHighlightDepthVisual(_selectedGridKey.Value, BattleHighlightVisualKind.Selected);
        }
        else if (_selectedGrid.HasValue)
        {
            var defaultSelectedGridKey = GetDefaultGridKey(_selectedGrid.Value);
            if (ShouldDisplaySelectedGridHighlight(defaultSelectedGridKey))
            {
                AddHighlightDepthVisual(defaultSelectedGridKey, BattleHighlightVisualKind.Selected);
            }
        }

        RefreshBattleDepthLayerOrder();
    }

    private bool ShouldDisplaySelectedGridHighlight(BattleGridKey selectedGridKey)
    {
        return !_selectedUnitGrid.HasValue ||
               _selectedUnit == null ||
               selectedGridKey != _selectedUnitGrid.Value ||
               selectedGridKey.Level != 2 ||
               !IsBattlePiece(_selectedUnit);
    }

    private void AddHighlightDepthVisual(BattleGridKey grid, BattleHighlightVisualKind visualKind)
    {
        if (_battleDepthLayer == null)
        {
            return;
        }

        var visual = new BattleHighlightRenderer
        {
            Name = $"Highlight_{visualKind}_{grid.X}_{grid.Y}_L{grid.Level}",
            Position = GetHighlightPosition(grid),
            ZIndex = 0
        };
        visual.Configure(visualKind);
        _battleDepthLayer.AddChild(visual);
        _highlightDepthVisuals.Add(visual);
        RegisterBattleDepthEntry(visual, grid, ToBattleDepthRenderKind(visualKind));
    }

    private static BattleDepthRenderKind ToBattleDepthRenderKind(BattleHighlightVisualKind visualKind)
    {
        return visualKind switch
        {
            BattleHighlightVisualKind.Movable or BattleHighlightVisualKind.WallTopMovable => BattleDepthRenderKind.MoveHighlight,
            BattleHighlightVisualKind.Attackable => BattleDepthRenderKind.AttackHighlight,
            BattleHighlightVisualKind.Workable => BattleDepthRenderKind.MoveHighlight,
            BattleHighlightVisualKind.Selected => BattleDepthRenderKind.SelectedHighlight,
            _ => BattleDepthRenderKind.MoveHighlight
        };
    }

    private void OnMoveButtonPressed()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !IsCurrentTurnPiece(_selectedUnit) ||
            HasUsedChargeThisTurn(_selectedUnit))
        {
            return;
        }

        _commandMode = BattleCommandMode.MoveSelect;
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _movableGrids.Clear();
        _duelTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        foreach (var grid in CalculateReachableGrids(_selectedUnitGrid.Value, GetEffectiveMoveRange(_selectedUnit)))
        {
            _movableGrids.Add(grid);
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
        RefreshOccludedUnitSilhouettes();
    }

    private void OnAttackButtonPressed()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !CanUseAttackCommand(_selectedUnit))
        {
            return;
        }

        _commandMode = BattleCommandMode.AttackSelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _duelTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        foreach (var grid in CalculateAttackableGrids(_selectedUnitGrid.Value, _selectedUnit))
        {
            _attackableGrids.Add(grid);
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private void OnChargeButtonPressed()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !CanUseChargeCommand(_selectedUnit))
        {
            return;
        }

        _commandMode = BattleCommandMode.ChargeSelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _chargeTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        foreach (var grid in CalculateChargeTargetGrids(_selectedUnitGrid.Value, _selectedUnit))
        {
            _chargeTargetGrids.Add(grid);
        }

        if (_chargeTargetGrids.Count == 0)
        {
            _commandMode = BattleCommandMode.AwaitingCommand;
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private IEnumerable<BattleGridKey> CalculateChargeTargetGrids(BattleGridKey cavalryGrid, BattleOccupantInfo cavalry)
    {
        if (!CanUseChargeCommand(cavalry) || cavalryGrid.Level != 0)
        {
            yield break;
        }

        foreach (var targetGrid in GetOrthogonalNeighborGridKeys(cavalryGrid))
        {
            if (!TryGetChargeDestinationGrid(cavalryGrid, targetGrid, out _) ||
                !_occupantsByGrid.TryGetValue(targetGrid, out var occupants))
            {
                continue;
            }

            var target = GetAttackTarget(occupants, cavalry.TeamName);
            if (target != null && IsAttackerPiece(target) != IsAttackerPiece(cavalry))
            {
                yield return targetGrid;
            }
        }
    }

    private bool TryExecuteSelectedCharge()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !_selectedGrid.HasValue ||
            !CanUseChargeCommand(_selectedUnit))
        {
            return false;
        }

        var sourceGrid = _selectedUnitGrid.Value;
        var targetGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        if (!_chargeTargetGrids.Contains(targetGrid) ||
            !TryGetChargeDestinationGrid(sourceGrid, targetGrid, out var destinationGrid) ||
            !_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            return false;
        }

        var target = GetAttackTarget(targetOccupants, _selectedUnit.TeamName);
        if (target == null)
        {
            return false;
        }

        ExecuteCharge(sourceGrid, _selectedUnit, targetGrid, target, destinationGrid);
        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        _chargeTargetGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        ConfigureHud();
        RefreshHiddenUnitVisibility();
        RefreshInfoPanel();
        RefreshBattleResultState();
        RefreshHighlights();
        RefreshBattleLogPanel();
        return true;
    }

    private void ExecuteCharge(BattleGridKey sourceGrid, BattleOccupantInfo cavalry, BattleGridKey targetGrid, BattleOccupantInfo target, BattleGridKey destinationGrid)
    {
        var direction = GetInfantryDirection(sourceGrid.Grid, targetGrid.Grid);
        var damage = target.TroopType == TroopSpearman ? CavalryChargeVsSpearmanDamage : CavalryChargeDamage;
        var targetHurtDuration = ApplyTargetHurtAnimation(sourceGrid, targetGrid, cavalry);
        var casualtyResult = ApplyUnitCasualties(targetGrid, target, damage, NormalDamageKilledRatio);
        ShowDamagePopup(targetGrid, casualtyResult.ActualDamage);
        AppendBattleLog(
            cavalry,
            "Charge",
            $"{FormatLogUnit(cavalry)} charges through {FormatLogUnit(target)} {sourceGrid} -> {destinationGrid}: {casualtyResult.ActualDamage:N0} damage ({FormatCasualtyResult(casualtyResult)})");
        if (casualtyResult.UpdatedTarget.HitPoints <= 0)
        {
            DestroyOccupantAfterDelay(targetGrid, casualtyResult.UpdatedTarget, targetHurtDuration);
        }

        var movedCavalry = MoveChargingCavalry(sourceGrid, cavalry, targetGrid, destinationGrid, direction);
        _selectedUnit = movedCavalry;
        _selectedUnitGrid = destinationGrid;
        _selectedGrid = destinationGrid.Grid;
        _selectedGridKey = destinationGrid;
        if (movedCavalry.Marker != null)
        {
            _chargeUsedByMarkerThisTurn.Add(movedCavalry.Marker);
        }

        var moveDuration = GetScaledMoveDuration(
            CavalryMoveAnimationDurationSeconds,
            new[] { GetMarkerPosition(targetGrid), GetMarkerPosition(destinationGrid) });
        if (target.TroopType == TroopSpearman)
        {
            var counterResult = ApplyUnitCasualties(destinationGrid, movedCavalry, CavalryChargeSpearmanCounterDamage, NormalDamageKilledRatio);
            ShowDamagePopup(destinationGrid, counterResult.ActualDamage);
            AppendBattleLog(
                target,
                "Counter",
                $"{FormatLogUnit(target)} counters {FormatLogUnit(movedCavalry)} charge: {counterResult.ActualDamage:N0} damage ({FormatCasualtyResult(counterResult)})");
            if (counterResult.UpdatedTarget.HitPoints <= 0)
            {
                DestroyOccupantAfterDelay(destinationGrid, counterResult.UpdatedTarget, moveDuration);
            }
        }

        ConfigureHud();
    }

    private BattleOccupantInfo MoveChargingCavalry(
        BattleGridKey sourceGrid,
        BattleOccupantInfo cavalry,
        BattleGridKey targetGrid,
        BattleGridKey destinationGrid,
        BattleSpriteDirection direction)
    {
        if (!_occupantsByGrid.TryGetValue(sourceGrid, out var sourceOccupants) ||
            !sourceOccupants.Remove(cavalry))
        {
            return cavalry;
        }

        if (sourceOccupants.Count == 0)
        {
            _occupantsByGrid.Remove(sourceGrid);
        }

        if (!_occupantsByGrid.TryGetValue(destinationGrid, out var destinationOccupants))
        {
            destinationOccupants = new List<BattleOccupantInfo>();
            _occupantsByGrid[destinationGrid] = destinationOccupants;
        }

        var remainsHidden = cavalry.IsHidden && IsForestGrid(destinationGrid);
        var movedCavalry = cavalry with
        {
            FacingDirection = direction,
            IsHidden = remainsHidden
        };
        destinationOccupants.Add(movedCavalry);
        UpdateMarkerStatusIndicator(movedCavalry);
        RegisterBattleDepthEntry(
            movedCavalry.Marker!,
            destinationGrid,
            movedCavalry.Category == CategorySiegeEngine ? BattleDepthRenderKind.SiegeEngine : BattleDepthRenderKind.Unit);

        var movePath = new[] { targetGrid, destinationGrid };
        var pathPositions = movePath.Select(GetMarkerPosition).ToArray();
        var pathDirections = new[] { direction, direction };
        var pathModulates = BuildMovePathModulates(sourceGrid, movePath, movedCavalry);
        ApplyMoveAnimation(
            movedCavalry,
            direction,
            GetMarkerPosition(destinationGrid),
            pathPositions,
            pathDirections,
            pathModulates,
            RefreshOccludedUnitSilhouettes);
        return movedCavalry;
    }

    private IEnumerable<BattleGridKey> GetOrthogonalNeighborGridKeys(BattleGridKey sourceGrid)
    {
        foreach (var neighbor in GetOrthogonalNeighbors(sourceGrid.Grid))
        {
            yield return new BattleGridKey(neighbor.X, neighbor.Y, sourceGrid.Level);
        }
    }

    private bool TryGetChargeDestinationGrid(BattleGridKey sourceGrid, BattleGridKey targetGrid, out BattleGridKey destinationGrid)
    {
        destinationGrid = default;
        if (_mapData == null ||
            _selectedUnit == null ||
            sourceGrid.Level != 0 ||
            targetGrid.Level != 0)
        {
            return false;
        }

        var delta = targetGrid.Grid - sourceGrid.Grid;
        if (Mathf.Abs(delta.X) + Mathf.Abs(delta.Y) != 1)
        {
            return false;
        }

        var destination = targetGrid.Grid + delta;
        if (!IsWithinMap(targetGrid.Grid) || !IsWithinMap(destination))
        {
            return false;
        }

        destinationGrid = new BattleGridKey(destination.X, destination.Y, 0);
        if (HasBlockingOccupant(destinationGrid))
        {
            return false;
        }

        var targetCell = _mapData.GetCell(targetGrid.X, targetGrid.Y);
        var destinationCell = _mapData.GetCell(destinationGrid.X, destinationGrid.Y);
        if (targetCell.Terrain == BattleTerrainType.Forest ||
            destinationCell.Terrain == BattleTerrainType.Forest)
        {
            return false;
        }

        if (!CanEnterCell(sourceGrid, targetGrid, targetCell) ||
            !CanEnterCell(targetGrid, destinationGrid, destinationCell))
        {
            return false;
        }

        if (IsCellBlockingMovement(destinationCell) &&
            !CanTraverseBlockedCell(destinationGrid, destinationCell))
        {
            return false;
        }

        return true;
    }

    private void OnDuelButtonPressed()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue)
        {
            return;
        }

        _commandMode = BattleCommandMode.DuelSelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        foreach (var grid in CalculateDuelTargetGrids(_selectedUnitGrid.Value, _selectedUnit))
        {
            _duelTargetGrids.Add(grid);
        }

        if (_duelTargetGrids.Count == 0)
        {
            _commandMode = BattleCommandMode.AwaitingCommand;
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private IEnumerable<BattleGridKey> CalculateDuelTargetGrids(BattleGridKey challengerGrid, BattleOccupantInfo challenger)
    {
        if (!CanStartDuel(challenger))
        {
            yield break;
        }

        foreach (var targetGrid in GetDuelCandidateGridKeys(challengerGrid))
        {
            if (!IsWithinMap(targetGrid.Grid) ||
                !_occupantsByGrid.TryGetValue(targetGrid, out var occupants))
            {
                continue;
            }

            var opponent = GetAttackTarget(occupants, challenger.TeamName);
            if (opponent != null &&
                CanStartDuel(opponent) &&
                IsAttackerPiece(opponent) != IsAttackerPiece(challenger))
            {
                yield return targetGrid;
            }
        }
    }

    private IEnumerable<BattleGridKey> GetDuelCandidateGridKeys(BattleGridKey challengerGrid)
    {
        for (var offsetY = -2; offsetY <= 2; offsetY++)
        {
            for (var offsetX = -2; offsetX <= 2; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var range = Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY));
                if (range > 2)
                {
                    continue;
                }

                var grid = new Vector2I(challengerGrid.X + offsetX, challengerGrid.Y + offsetY);
                if (!IsWithinMap(grid))
                {
                    continue;
                }

                yield return new BattleGridKey(grid.X, grid.Y, 0);
                yield return new BattleGridKey(grid.X, grid.Y, 2);
            }
        }
    }

    private bool TryExecuteSelectedDuel()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || !_selectedGrid.HasValue)
        {
            return false;
        }

        var targetGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        if (!_duelTargetGrids.Contains(targetGrid) ||
            !_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            return false;
        }

        var opponent = GetAttackTarget(targetOccupants, _selectedUnit.TeamName);
        if (opponent == null || !CanStartDuel(_selectedUnit) || !CanStartDuel(opponent))
        {
            return false;
        }

        ExecuteDuel(_selectedUnitGrid.Value, _selectedUnit, targetGrid, opponent);
        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        HideCommandMenu();
        ConfigureHud();
        RefreshInfoPanel();
        RefreshHighlights();
        return true;
    }

    private void ExecuteDuel(BattleGridKey challengerGrid, BattleOccupantInfo challenger, BattleGridKey opponentGrid, BattleOccupantInfo opponent)
    {
        var challengerScore = GetDuelBattleScore(challenger);
        var opponentScore = GetDuelBattleScore(opponent);
        if (!DoesOpponentAcceptDuel(challengerScore, opponentScore))
        {
            UpdateUnitMorale(challengerGrid, challenger, 2, out _);
            UpdateUnitMorale(opponentGrid, opponent, -5, out _);
            AppendBattleLog(
                challenger,
                "Duel",
                $"{FormatLogUnit(challenger)} challenges {FormatLogUnit(opponent)}, but opponent refuses. Morale {FormatLogUnit(challenger)} +2, {FormatLogUnit(opponent)} -5");
            return;
        }

        AppendBattleLog(challenger, "Duel", $"{FormatLogUnit(challenger)} duels {FormatLogUnit(opponent)} ({challengerScore} vs {opponentScore})");
        var scoreDelta = challengerScore - opponentScore;
        if (Mathf.Abs(scoreDelta) <= 5)
        {
            UpdateUnitMorale(challengerGrid, challenger, 3, out _);
            UpdateUnitMorale(opponentGrid, opponent, 3, out _);
            AppendBattleLog(challenger, "Duel", $"Draw: {FormatLogUnit(challenger)} and {FormatLogUnit(opponent)} both keep battle team. Morale +3");
            return;
        }

        var winnerGrid = scoreDelta > 0 ? challengerGrid : opponentGrid;
        var winner = scoreDelta > 0 ? challenger : opponent;
        var loserGrid = scoreDelta > 0 ? opponentGrid : challengerGrid;
        var loser = scoreDelta > 0 ? opponent : challenger;
        UpdateUnitMorale(winnerGrid, winner, 10, out _);
        AppendBattleLog(winner, "Duel", $"{FormatLogUnit(winner)} wins. {FormatLogUnit(loser)} captured; losing team leaves battle. Winner morale +10");
        ApplyRetreatTroopLoss(loser);
        RemoveOccupant(loserGrid, loser);
        if (_selectedUnit == loser)
        {
            _selectedUnit = null;
            _selectedUnitGrid = null;
            _selectedGrid = null;
            _selectedGridKey = null;
        }
    }

    private bool UpdateUnitMorale(BattleGridKey grid, BattleOccupantInfo unit, int delta, out BattleOccupantInfo updatedUnit)
    {
        updatedUnit = unit;
        if (unit.Morale == null)
        {
            return false;
        }

        var updatedMorale = Mathf.Clamp(unit.Morale.Value + delta, 0, 120);
        var actualDelta = updatedMorale - unit.Morale.Value;
        updatedUnit = unit with { Morale = updatedMorale };
        ReplaceOccupantAtGrid(grid, unit, updatedUnit);
        if (_selectedUnit == unit)
        {
            _selectedUnit = updatedUnit;
        }

        ShowMoralePopup(grid, actualDelta);
        return true;
    }

    private void ResolveDailyBattleSupply()
    {
        ResolveDailyBattleSupplyForTeam(TeamAInfo.Name, ref _teamAGold, ref _teamAFood, _teamATotalTroops);
        ResolveDailyBattleSupplyForTeam(TeamBInfo.Name, ref _teamBGold, ref _teamBFood, _teamBTotalTroops);
    }

    private void ResolveDailyBattleSupplyForTeam(string teamName, ref int gold, ref int food, int activeTroops)
    {
        var dailyFoodNeed = CalculateDailyFoodNeed(activeTroops);
        var dailyGoldNeed = CalculateDailyGoldNeed(activeTroops);
        if (dailyFoodNeed <= 0 && dailyGoldNeed <= 0)
        {
            return;
        }

        var foodBefore = food;
        var goldBefore = gold;
        food = Math.Max(0, food - dailyFoodNeed);
        gold = Math.Max(0, gold - dailyGoldNeed);
        AppendBattleLog(
            teamName,
            "Supply",
            $"Daily upkeep: food -{Math.Min(foodBefore, dailyFoodNeed):N0}/{dailyFoodNeed:N0}, gold -{Math.Min(goldBefore, dailyGoldNeed):N0}/{dailyGoldNeed:N0}");

        if (dailyFoodNeed <= 0)
        {
            return;
        }

        if (foodBefore < dailyFoodNeed)
        {
            ApplyTeamMoralePenalty(teamName, StarvingMoralePenalty, "food shortage");
            return;
        }

        if (food < dailyFoodNeed)
        {
            ApplyTeamMoralePenalty(teamName, LowFoodMoralePenalty, "low food");
        }
    }

    private static int CalculateDailyFoodNeed(int activeTroops)
    {
        return CalculateScaledResourceNeed(activeTroops, DailyFoodPer100ActiveTroops);
    }

    private static int CalculateDailyGoldNeed(int activeTroops)
    {
        return CalculateScaledResourceNeed(activeTroops, DailyGoldPer100ActiveTroops);
    }

    private static int CalculateScaledResourceNeed(int activeTroops, int per100Troops)
    {
        return activeTroops <= 0 || per100Troops <= 0
            ? 0
            : Mathf.CeilToInt(activeTroops / 100.0f * per100Troops);
    }

    private void ApplyTeamMoralePenalty(string teamName, int penalty, string reason, double popupDelaySeconds = 0.0)
    {
        if (penalty <= 0)
        {
            return;
        }

        var affectedUnits = new List<BattleOccupantInfo>();
        foreach (var (grid, occupants) in _occupantsByGrid.ToList())
        {
            foreach (var unit in occupants.ToList())
            {
                if (unit.Category != CategoryUnit ||
                    unit.TeamName != teamName ||
                    unit.Morale == null)
                {
                    continue;
                }

                var updatedMorale = Mathf.Clamp(unit.Morale.Value - penalty, 0, 120);
                var actualDelta = updatedMorale - unit.Morale.Value;
                var updatedUnit = unit with { Morale = updatedMorale };
                ReplaceOccupantAtGrid(grid, unit, updatedUnit);
                if (_selectedUnit == unit)
                {
                    _selectedUnit = updatedUnit;
                }

                ShowMoralePopup(grid, actualDelta, popupDelaySeconds);
                affectedUnits.Add(updatedUnit);
            }
        }

        if (affectedUnits.Count > 0)
        {
            AppendBattleLog(teamName, "Supply", $"{reason}: {affectedUnits.Count} battle team(s) morale -{penalty}");
        }
    }

    private void ApplyTeamMoraleBonus(string teamName, int bonus, string reason, double popupDelaySeconds = 0.0)
    {
        if (bonus <= 0)
        {
            return;
        }

        var affectedUnits = new List<BattleOccupantInfo>();
        foreach (var (grid, occupants) in _occupantsByGrid.ToList())
        {
            foreach (var unit in occupants.ToList())
            {
                if (unit.Category != CategoryUnit ||
                    unit.TeamName != teamName ||
                    unit.Morale == null)
                {
                    continue;
                }

                var updatedMorale = Mathf.Clamp(unit.Morale.Value + bonus, 0, 120);
                var actualDelta = updatedMorale - unit.Morale.Value;
                var updatedUnit = unit with { Morale = updatedMorale };
                ReplaceOccupantAtGrid(grid, unit, updatedUnit);
                if (_selectedUnit == unit)
                {
                    _selectedUnit = updatedUnit;
                }

                ShowMoralePopup(grid, actualDelta, popupDelaySeconds);
                affectedUnits.Add(updatedUnit);
            }
        }

        if (affectedUnits.Count > 0)
        {
            AppendBattleLog(teamName, "Supply", $"{reason}: {affectedUnits.Count} battle team(s) morale +{bonus}");
        }
    }

    private (int GoldLoss, int FoodLoss) ApplySupplyCartLostResourcePenalty(BattleOccupantInfo supplyCart, string reason)
    {
        var goldLoss = Mathf.CeilToInt(GetTeamGold(supplyCart.TeamName) * (SupplyCartDestroyedResourceLossPercent / 100.0f));
        var foodLoss = Mathf.CeilToInt(GetTeamFood(supplyCart.TeamName) * (SupplyCartDestroyedResourceLossPercent / 100.0f));
        ApplyTeamResourceDelta(supplyCart.TeamName, -goldLoss, -foodLoss);

        AppendBattleLog(
            supplyCart,
            "Supply",
            $"{FormatLogUnit(supplyCart)} {reason}. Team supplies lost {SupplyCartDestroyedResourceLossPercent}%: gold -{goldLoss:N0}, food -{foodLoss:N0}");
        return (goldLoss, foodLoss);
    }

    private int GetTeamGold(string teamName)
    {
        return teamName.Contains("Attacker") ? _teamAGold : _teamBGold;
    }

    private int GetTeamFood(string teamName)
    {
        return teamName.Contains("Attacker") ? _teamAFood : _teamBFood;
    }

    private void ApplyTeamResourceDelta(string teamName, int goldDelta, int foodDelta)
    {
        if (teamName.Contains("Attacker"))
        {
            _teamAGold = Math.Max(0, _teamAGold + goldDelta);
            _teamAFood = Math.Max(0, _teamAFood + foodDelta);
            return;
        }

        _teamBGold = Math.Max(0, _teamBGold + goldDelta);
        _teamBFood = Math.Max(0, _teamBFood + foodDelta);
    }

    private static bool UsesWeaponAmmo(BattleOccupantInfo unit)
    {
        return unit.MaxWeaponAmmo.HasValue;
    }

    private static bool HasWeaponAmmo(BattleOccupantInfo unit)
    {
        return !UsesWeaponAmmo(unit) || unit.WeaponAmmo.GetValueOrDefault() > 0;
    }

    private static bool CanUseAmmoDepletedWeakAttack(BattleOccupantInfo unit)
    {
        return ((unit.Category == CategoryUnit && unit.TroopType is TroopArcher or TroopCrossbow) ||
                (unit.Category == CategorySiegeEngine && unit.TroopType == TroopCatapult)) &&
               unit.MaxWeaponAmmo.HasValue &&
               unit.WeaponAmmo.GetValueOrDefault() <= 0;
    }

    private static bool CanUseNormalAttackWithCurrentAmmo(BattleOccupantInfo unit)
    {
        return HasWeaponAmmo(unit) || CanUseAmmoDepletedWeakAttack(unit);
    }

    private static bool ShouldSpendWeaponAmmoForNormalAttack(BattleOccupantInfo unit)
    {
        return UsesWeaponAmmo(unit) && !CanUseAmmoDepletedWeakAttack(unit);
    }

    private static bool TrySpendWeaponAmmo(BattleOccupantInfo unit, out BattleOccupantInfo updatedUnit)
    {
        updatedUnit = unit;
        if (!UsesWeaponAmmo(unit))
        {
            return true;
        }

        var currentAmmo = unit.WeaponAmmo.GetValueOrDefault();
        if (currentAmmo <= 0)
        {
            return false;
        }

        updatedUnit = unit with { WeaponAmmo = currentAmmo - 1 };
        return true;
    }

    private static bool TrySpendNormalAttackWeaponAmmo(BattleOccupantInfo unit, out BattleOccupantInfo updatedUnit)
    {
        updatedUnit = unit;
        return !ShouldSpendWeaponAmmoForNormalAttack(unit) || TrySpendWeaponAmmo(unit, out updatedUnit);
    }

    private bool RefillWeaponAmmo(BattleGridKey targetGrid, BattleOccupantInfo target, out int refilledAmmo)
    {
        refilledAmmo = 0;
        if (!TryGetCurrentOccupantAtGrid(targetGrid, target, out var currentTarget))
        {
            return false;
        }

        target = currentTarget;
        if (!target.WeaponAmmo.HasValue ||
            !target.MaxWeaponAmmo.HasValue ||
            target.WeaponAmmo.Value >= target.MaxWeaponAmmo.Value)
        {
            return false;
        }

        refilledAmmo = target.MaxWeaponAmmo.Value - target.WeaponAmmo.Value;
        var updatedTarget = target with { WeaponAmmo = target.MaxWeaponAmmo.Value };
        ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedTarget;
        }

        return true;
    }

    private void ApplySupplyCartDestroyedMoralePenalty(BattleOccupantInfo supplyCart)
    {
        ApplySupplyCartLostPenalty(supplyCart, "destroyed");
    }

    private (int GoldLoss, int FoodLoss) ApplySupplyCartLostPenalty(BattleOccupantInfo supplyCart, string reason)
    {
        var resourceLoss = ApplySupplyCartLostResourcePenalty(supplyCart, reason);
        var affectedUnits = new List<BattleOccupantInfo>();
        foreach (var (grid, occupants) in _occupantsByGrid.ToList())
        {
            foreach (var unit in occupants.ToList())
            {
                if (unit.Category != CategoryUnit ||
                    unit.TeamName != supplyCart.TeamName ||
                    unit.Morale == null)
                {
                    continue;
                }

                var updatedMorale = Mathf.FloorToInt(unit.Morale.Value * 0.5f);
                var actualDelta = updatedMorale - unit.Morale.Value;
                var updatedUnit = unit with { Morale = updatedMorale };
                ReplaceOccupantAtGrid(grid, unit, updatedUnit);
                if (_selectedUnit == unit)
                {
                    _selectedUnit = updatedUnit;
                }

                ShowMoralePopup(grid, actualDelta);
                affectedUnits.Add(updatedUnit);
            }
        }

        if (affectedUnits.Count > 0)
        {
            AppendBattleLog(
                supplyCart,
                "Supply",
                $"{FormatLogUnit(supplyCart)} {reason}. {affectedUnits.Count} friendly battle team(s) morale -50%");
        }

        return resourceLoss;
    }

    private static bool CanStartDuel(BattleOccupantInfo unit)
    {
        return unit.Category == CategoryUnit &&
               unit.Marker != null &&
               !IsMessed(unit) &&
               IsGeneralCountedPiece(unit.Category, unit.OfficerName);
    }

    private static bool DoesOpponentAcceptDuel(int challengerScore, int opponentScore)
    {
        return opponentScore + 12 >= challengerScore;
    }

    private static int GetDuelBattleScore(BattleOccupantInfo unit)
    {
        return GetOfficerBattleAttribute(unit.OfficerName);
    }

    private static int GetOfficerBattleAttribute(string officerName)
    {
        return officerName switch
        {
            "Dong Zhuo" => 88,
            "Xiahou Yuan" => 86,
            "Cao Chun" => 82,
            "Cao Hong" => 80,
            "Guo Si" => 78,
            "Zhang He" => 76,
            "Li Jue" => 72,
            _ => 70
        };
    }

    private void OnUnionAttackButtonPressed()
    {
        if (!TryGetBestUnionAttackCandidate(out var candidate) ||
            _selectedUnit == null ||
            !_selectedUnitGrid.HasValue)
        {
            return;
        }

        var shouldTemporarilyRevealOccludedUnits =
            IsUnitOccludedByCastleVisual(candidate.TargetGrid) ||
            candidate.Participants.Any(participant => IsUnitOccludedByCastleVisual(participant.Grid));
        if (shouldTemporarilyRevealOccludedUnits)
        {
            ClearOccludedUnitSilhouettes();
        }

        var maxAttackAnimationDuration = 0.0;
        BattleOccupantInfo? updatedSelectedUnit = null;
        var updatedParticipants = new List<UnionAttackParticipant>();
        foreach (var participant in candidate.Participants)
        {
            var attackDirection = GetInfantryDirection(participant.Grid.Grid, candidate.TargetGrid.Grid);
            var attackingUnit = participant.Occupant with { FacingDirection = attackDirection };
            if (!TrySpendNormalAttackWeaponAmmo(attackingUnit, out attackingUnit))
            {
                continue;
            }

            ReplaceOccupantAtGrid(participant.Grid, participant.Occupant, attackingUnit);
            updatedParticipants.Add(new UnionAttackParticipant(participant.Grid, attackingUnit));
            maxAttackAnimationDuration = Math.Max(maxAttackAnimationDuration, ApplyAttackAnimation(attackingUnit, attackDirection));
            if (participant.Grid == _selectedUnitGrid.Value)
            {
                updatedSelectedUnit = attackingUnit;
            }
        }

        if (updatedParticipants.Count < 2)
        {
            return;
        }

        if (updatedSelectedUnit != null)
        {
            _selectedUnit = updatedSelectedUnit;
        }

        var hurtAnimationDuration = ApplyTargetHurtAnimation(_selectedUnitGrid.Value, candidate.TargetGrid, _selectedUnit!);
        var effectDelaySeconds = Math.Max(maxAttackAnimationDuration, hurtAnimationDuration);
        AppendBattleLog(
            _selectedUnit!,
            "Attack",
            $"Union x{updatedParticipants.Count}: {string.Join(", ", updatedParticipants.Select(participant => FormatLogUnit(participant.Occupant)))} -> {candidate.TargetGrid}");
        ApplyAttackDamage(_selectedUnit!, candidate.TargetGrid, effectDelaySeconds, GetUnionAttackDamage(candidate.Participants));
        if (shouldTemporarilyRevealOccludedUnits)
        {
            RefreshOccludedUnitSilhouettesAfterDelay(effectDelaySeconds);
        }

        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private void OnRetreatButtonPressed()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || !IsBattlePiece(_selectedUnit))
        {
            return;
        }

        var retreatingUnit = _selectedUnit;
        var retreatingGrid = _selectedUnitGrid.Value;
        ApplyRetreatTroopLoss(retreatingUnit);
        AppendBattleLog(retreatingUnit, "Retreat", $"{FormatLogUnit(retreatingUnit)} retreats from {retreatingGrid}");
        RemoveOccupant(retreatingGrid, retreatingUnit);

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        _selectedGrid = null;
        _selectedGridKey = null;
        _selectedUnit = null;
        _selectedUnitGrid = null;
        HideCommandMenu();
        RefreshBattleDepthLayerOrder();
        RefreshOccludedUnitSilhouettes();
        ConfigureHud();
        RefreshHighlights();
    }

    private void OnSupplyButtonPressed()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            _selectedUnit.Marker == null ||
            _selectedUnit.TroopType != TroopSupplyCart ||
            _supplyUsedByMarkerThisTurn.Contains(_selectedUnit.Marker))
        {
            return;
        }

        var suppliedUnits = GetSupplyMoraleTargets(_selectedUnitGrid.Value, _selectedUnit).ToList();
        var recoveryTargets = GetWoundedRecoveryTargets(_selectedUnitGrid.Value, _selectedUnit).ToList();
        var repairTargets = GetSupplyRepairTargets(_selectedUnitGrid.Value, _selectedUnit).ToList();
        if (suppliedUnits.Count == 0 && recoveryTargets.Count == 0 && repairTargets.Count == 0)
        {
            return;
        }

        var suppliedTargetCount = 0;
        foreach (var (targetGrid, target) in suppliedUnits)
        {
            if (UpdateUnitMorale(targetGrid, target, SupplyCartMoraleRestore, out _))
            {
                suppliedTargetCount++;
            }
        }

        var recoveredTroops = 0;
        var recoveredTargetCount = 0;
        foreach (var (targetGrid, target) in recoveryTargets)
        {
            var targetRecoveredTroops = RecoverWoundedTroops(targetGrid, target, SupplyCartWoundedRecoveryAmount);
            if (targetRecoveredTroops > 0)
            {
                recoveredTroops += targetRecoveredTroops;
                recoveredTargetCount++;
            }
        }

        var repairedHp = 0;
        var repairedTargetCount = 0;
        foreach (var (targetGrid, target) in repairTargets)
        {
            var targetRepairedHp = RepairSiegeEngine(targetGrid, target, SupplyCartRepairAmount);
            if (targetRepairedHp > 0)
            {
                repairedHp += targetRepairedHp;
                repairedTargetCount++;
            }
        }

        var logParts = new List<string>();
        if (suppliedTargetCount > 0)
        {
            logParts.Add($"{suppliedTargetCount} unit(s) morale +{SupplyCartMoraleRestore}");
        }

        if (recoveredTargetCount > 0)
        {
            logParts.Add($"{recoveredTargetCount} unit(s) recovered {recoveredTroops:N0} wounded");
        }

        if (repairedTargetCount > 0)
        {
            logParts.Add($"{repairedTargetCount} car(s) repaired +{repairedHp:N0} HP");
        }

        if (logParts.Count == 0)
        {
            return;
        }

        _supplyUsedByMarkerThisTurn.Add(_selectedUnit.Marker);

        AppendBattleLog(
            _selectedUnit,
            "Recovery",
            $"{FormatLogUnit(_selectedUnit)} recovery / repair: {string.Join(", ", logParts)}");

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        ConfigureHud();
        RefreshInfoPanel();
        RefreshHighlights();
        RefreshBattleLogPanel();
    }

    private void OnResupplyWeaponButtonPressed()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            _selectedUnit.Marker == null ||
            _selectedUnit.TroopType != TroopSupplyCart ||
            _supplyUsedByMarkerThisTurn.Contains(_selectedUnit.Marker))
        {
            return;
        }

        var resupplyTargets = GetWeaponResupplyTargets(_selectedUnitGrid.Value, _selectedUnit).ToList();
        if (resupplyTargets.Count == 0)
        {
            return;
        }

        var refilledAmmo = 0;
        foreach (var (targetGrid, target) in resupplyTargets)
        {
            if (RefillWeaponAmmo(targetGrid, target, out var targetRefilledAmmo))
            {
                refilledAmmo += targetRefilledAmmo;
            }
        }

        _supplyUsedByMarkerThisTurn.Add(_selectedUnit.Marker);
        AppendBattleLog(
            _selectedUnit,
            "Supply",
            $"{FormatLogUnit(_selectedUnit)} resupplies {resupplyTargets.Count} weapon unit(s). Ammo +{refilledAmmo:N0}");

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
        RefreshBattleLogPanel();
    }

    private void OnCaptureSupplyCartButtonPressed()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !TryGetCapturableSupplyCartTarget(out var targetGrid, out var supplyCart) ||
            supplyCart == null)
        {
            return;
        }

        var captor = _selectedUnit;
        var previousTeamName = supplyCart.TeamName;
        var capturedResources = ApplySupplyCartLostPenalty(supplyCart, "captured");
        ApplyTeamResourceDelta(captor.TeamName, capturedResources.GoldLoss, capturedResources.FoodLoss);
        ApplyTeamMoraleBonus(captor.TeamName, SupplyCartCaptureMoraleBonus, "captured supplies");
        ConvertBattlePieceToSide(targetGrid, supplyCart, captor.TeamName);
        AppendBattleLog(
            captor,
            "Supply",
            $"{FormatLogUnit(captor)} captures {FormatLogUnit(supplyCart)} at {targetGrid}: gold +{capturedResources.GoldLoss:N0}, food +{capturedResources.FoodLoss:N0}. Supply Cart changes side: {previousTeamName} -> {captor.TeamName}");

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        HideCommandMenu();
        ConfigureHud();
        RefreshInfoPanel();
        RefreshHighlights();
        RefreshBattleLogPanel();
        RefreshBattleDepthLayerOrder();
        RefreshOccludedUnitSilhouettes();
    }

    private void OnHireOfficerButtonPressed()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue)
        {
            return;
        }

        _commandMode = BattleCommandMode.HireOfficerSelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _chargeTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        foreach (var grid in CalculateHireOfficerTargetGrids(_selectedUnitGrid.Value))
        {
            _hireOfficerTargetGrids.Add(grid);
        }

        if (_hireOfficerTargetGrids.Count == 0)
        {
            _commandMode = BattleCommandMode.AwaitingCommand;
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private bool TryExecuteSelectedHireOfficer()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !_selectedGrid.HasValue)
        {
            return false;
        }

        var targetGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        if (!_hireOfficerTargetGrids.Contains(targetGrid) ||
            !TryGetHireOfficerTargetAtGrid(targetGrid, out var target) ||
            target == null)
        {
            return false;
        }

        var recruiter = _selectedUnit;
        var cost = GetHireOfficerGoldCost(target);
        if (GetTeamGold(recruiter.TeamName) < cost)
        {
            AppendBattleLog(recruiter, "Hire", $"{FormatLogUnit(recruiter)} cannot hire {FormatLogUnit(target)}: gold is not enough ({cost:N0})");
            return false;
        }

        ApplyTeamResourceDelta(recruiter.TeamName, -cost, 0);
        var previousTeamName = target.TeamName;
        PlayHireOfficerEffect(_selectedUnitGrid.Value, targetGrid);
        ConvertBattlePieceToSide(targetGrid, target, recruiter.TeamName);
        ApplyTeamMoraleBonus(recruiter.TeamName, HireOfficerTeamMoraleBonus, $"hired {FormatLogUnit(target)}", HireOfficerMoralePopupDelaySeconds);
        ApplyTeamMoralePenalty(previousTeamName, HireOfficerTeamMoralePenalty, $"{FormatLogUnit(target)} hired away", HireOfficerMoralePopupDelaySeconds);
        AppendBattleLog(
            recruiter,
            "Hire",
            $"{FormatLogUnit(recruiter)} hires {FormatLogUnit(target)} for {cost:N0} gold. {target.DisplayName} joins {recruiter.TeamName}. Morale {recruiter.TeamName} +{HireOfficerTeamMoraleBonus}, {previousTeamName} -{HireOfficerTeamMoralePenalty}");

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        HideCommandMenu();
        ConfigureHud();
        RefreshInfoPanel();
        RefreshHighlights();
        RefreshBattleLogPanel();
        RefreshBattleDepthLayerOrder();
        RefreshOccludedUnitSilhouettes();
        return true;
    }

    private bool TryGetCapturableSupplyCartTarget(out BattleGridKey targetGrid, out BattleOccupantInfo? supplyCart)
    {
        targetGrid = default;
        supplyCart = default;
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || _selectedUnit.Category != CategoryUnit)
        {
            return false;
        }

        var selectedGrid = _selectedUnitGrid.Value;
        var selectedIsAttacker = IsAttackerPiece(_selectedUnit);
        foreach (var (grid, occupants) in GetAdjacentOccupantEntries(selectedGrid))
        {
            var cart = occupants.FirstOrDefault(occupant =>
                occupant.Category == CategorySiegeEngine &&
                occupant.TroopType == TroopSupplyCart &&
                IsAttackerPiece(occupant) != selectedIsAttacker);
            if (cart != null)
            {
                targetGrid = grid;
                supplyCart = cart;
                return true;
            }
        }

        return false;
    }

    private bool TryGetHireOfficerTarget(out BattleGridKey targetGrid, out BattleOccupantInfo? target)
    {
        targetGrid = default;
        target = default;
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || _selectedUnit.Category != CategoryUnit)
        {
            return false;
        }

        var selectedGrid = _selectedUnitGrid.Value;
        var selectedIsAttacker = IsAttackerPiece(_selectedUnit);
        foreach (var (grid, occupants) in GetHireOfficerRangeOccupantEntries(selectedGrid))
        {
            var candidate = occupants
                .Where(occupant =>
                    CanHireOfficerTarget(occupant) &&
                    !IsHiddenFromSide(occupant, _selectedUnit.TeamName) &&
                    IsAttackerPiece(occupant) != selectedIsAttacker)
                .OrderByDescending(occupant => GetOfficerBattleAttribute(occupant.OfficerName))
                .FirstOrDefault();
            if (candidate != null)
            {
                targetGrid = grid;
                target = candidate;
                return true;
            }
        }

        return false;
    }

    private IEnumerable<BattleGridKey> CalculateHireOfficerTargetGrids(BattleGridKey recruiterGrid)
    {
        if (_selectedUnit == null || _selectedUnit.Category != CategoryUnit)
        {
            yield break;
        }

        foreach (var (grid, occupants) in GetHireOfficerRangeOccupantEntries(recruiterGrid))
        {
            if (TryGetHireOfficerCandidateFromOccupants(occupants, _selectedUnit, out _))
            {
                yield return grid;
            }
        }
    }

    private bool TryGetHireOfficerTargetAtGrid(BattleGridKey targetGrid, out BattleOccupantInfo? target)
    {
        target = default;
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !_hireOfficerTargetGrids.Contains(targetGrid) ||
            !_occupantsByGrid.TryGetValue(targetGrid, out var occupants))
        {
            return false;
        }

        return TryGetHireOfficerCandidateFromOccupants(occupants, _selectedUnit, out target);
    }

    private bool TryGetHireOfficerCandidateFromOccupants(
        IEnumerable<BattleOccupantInfo> occupants,
        BattleOccupantInfo recruiter,
        out BattleOccupantInfo? target)
    {
        var recruiterIsAttacker = IsAttackerPiece(recruiter);
        target = occupants
            .Where(occupant =>
                CanHireOfficerTarget(occupant) &&
                !IsHiddenFromSide(occupant, recruiter.TeamName) &&
                IsAttackerPiece(occupant) != recruiterIsAttacker)
            .OrderByDescending(occupant => GetOfficerBattleAttribute(occupant.OfficerName))
            .FirstOrDefault();
        return target != null;
    }

    private IEnumerable<KeyValuePair<BattleGridKey, List<BattleOccupantInfo>>> GetHireOfficerRangeOccupantEntries(BattleGridKey sourceGrid)
    {
        foreach (var entry in _occupantsByGrid)
        {
            if (entry.Key.Level == sourceGrid.Level &&
                entry.Key != sourceGrid &&
                GetChebyshevDistance(sourceGrid.Grid, entry.Key.Grid) <= HireOfficerRange)
            {
                yield return entry;
            }
        }
    }

    private IEnumerable<KeyValuePair<BattleGridKey, List<BattleOccupantInfo>>> GetAdjacentOccupantEntries(BattleGridKey sourceGrid)
    {
        foreach (var entry in _occupantsByGrid)
        {
            if (entry.Key.Level == sourceGrid.Level && IsTouchingGrid(sourceGrid, entry.Key))
            {
                yield return entry;
            }
        }
    }

    private static bool CanHireOfficerTarget(BattleOccupantInfo occupant)
    {
        return IsGeneralCountedPiece(occupant.Category, occupant.OfficerName) &&
               occupant.Marker != null &&
               !IsMessed(occupant);
    }

    private static int GetHireOfficerGoldCost(BattleOccupantInfo target)
    {
        _ = target;
        return HireOfficerGoldCost;
    }

    private void ConvertBattlePieceToSide(BattleGridKey grid, BattleOccupantInfo target, string newTeamName)
    {
        ApplyTeamTroopDelta(target.Category, target.TeamName, -target.TroopCount);
        ApplyTeamGeneralDelta(target.Category, target.TeamName, target.OfficerName, -1);
        ApplyTeamSiegeUnitDelta(target.Category, target.TeamName, -1);

        var converted = target with { TeamName = newTeamName };
        ReplaceOccupantAtGrid(grid, target, converted);
        ApplyTeamTroopDelta(converted.Category, converted.TeamName, converted.TroopCount);
        ApplyTeamGeneralDelta(converted.Category, converted.TeamName, converted.OfficerName, 1);
        ApplyTeamSiegeUnitDelta(converted.Category, converted.TeamName, 1);
        RefreshMarkerTeamVisual(converted);
        RefreshHiddenUnitVisibility();
    }

    private static void RefreshMarkerTeamVisual(BattleOccupantInfo occupant)
    {
        if (occupant.Marker == null)
        {
            return;
        }

        occupant.Marker.Setup(
            occupant.ShortLabel,
            GetSavedMarkerFillColor(occupant),
            GetSavedMarkerBorderColor(occupant),
            occupant.Marker.Radius);
        occupant.Marker.SetupNamePlate(string.IsNullOrWhiteSpace(occupant.OfficerName) ? occupant.DisplayName : occupant.OfficerName);
        occupant.Marker.SetupTeamArrow(GetTeamArrowColor(occupant.TeamName));
        UpdateMarkerStrengthBar(occupant);
    }

    private bool HasSupplyTargets()
    {
        return _selectedUnit != null &&
               _selectedUnitGrid.HasValue &&
               _selectedUnit.TroopType == TroopSupplyCart &&
               _selectedUnit.Marker != null &&
               !_supplyUsedByMarkerThisTurn.Contains(_selectedUnit.Marker) &&
               (GetSupplyMoraleTargets(_selectedUnitGrid.Value, _selectedUnit).Any() ||
                GetWoundedRecoveryTargets(_selectedUnitGrid.Value, _selectedUnit).Any() ||
                GetSupplyRepairTargets(_selectedUnitGrid.Value, _selectedUnit).Any());
    }

    private bool HasWeaponResupplyTargets()
    {
        return _selectedUnit != null &&
               _selectedUnitGrid.HasValue &&
               _selectedUnit.TroopType == TroopSupplyCart &&
               _selectedUnit.Marker != null &&
               !_supplyUsedByMarkerThisTurn.Contains(_selectedUnit.Marker) &&
               GetWeaponResupplyTargets(_selectedUnitGrid.Value, _selectedUnit).Any();
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetSupplyMoraleTargets(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart)
    {
        foreach (var neighborGrid in GetAdjacentEightNeighbors(supplyGrid.Grid))
        {
            if (!IsWithinMap(neighborGrid))
            {
                continue;
            }

            foreach (var (targetGrid, target) in GetOccupantsAtGrid(neighborGrid))
            {
                if (targetGrid.Level != supplyGrid.Level ||
                    target.Morale == null ||
                    target.Morale.Value >= 120 ||
                    target.TeamName != supplyCart.TeamName ||
                    target.TroopType == TroopSupplyCart)
                {
                    continue;
                }

                yield return (targetGrid, target);
            }
        }
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetSupplyRepairTargets(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart)
    {
        foreach (var occupant in GetOccupantsAtGrid(supplyGrid.Grid))
        {
            if (IsSupplyRepairTarget(supplyGrid, supplyCart, occupant.Grid, occupant.Occupant, allowSelf: true))
            {
                yield return occupant;
            }
        }

        foreach (var neighborGrid in GetAdjacentEightNeighbors(supplyGrid.Grid))
        {
            if (!IsWithinMap(neighborGrid))
            {
                continue;
            }

            foreach (var occupant in GetOccupantsAtGrid(neighborGrid))
            {
                if (IsSupplyRepairTarget(supplyGrid, supplyCart, occupant.Grid, occupant.Occupant, allowSelf: false))
                {
                    yield return occupant;
                }
            }
        }
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetWoundedRecoveryTargets(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart)
    {
        foreach (var neighborGrid in GetAdjacentEightNeighbors(supplyGrid.Grid))
        {
            if (!IsWithinMap(neighborGrid))
            {
                continue;
            }

            foreach (var (targetGrid, target) in GetOccupantsAtGrid(neighborGrid))
            {
                if (targetGrid.Level != supplyGrid.Level ||
                    target.TeamName != supplyCart.TeamName ||
                    target.Category != CategoryUnit ||
                    target.WoundedTroops <= 0)
                {
                    continue;
                }

                yield return (targetGrid, target);
            }
        }
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetWeaponResupplyTargets(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart)
    {
        foreach (var neighborGrid in GetAdjacentEightNeighbors(supplyGrid.Grid))
        {
            if (!IsWithinMap(neighborGrid))
            {
                continue;
            }

            foreach (var occupant in GetOccupantsAtGrid(neighborGrid))
            {
                if (IsWeaponResupplyTarget(supplyGrid, supplyCart, occupant.Grid, occupant.Occupant))
                {
                    yield return occupant;
                }
            }
        }
    }

    private static bool IsSupplyRepairTarget(
        BattleGridKey supplyGrid,
        BattleOccupantInfo supplyCart,
        BattleGridKey targetGrid,
        BattleOccupantInfo target,
        bool allowSelf)
    {
        var isSelf = target.Marker != null && target.Marker == supplyCart.Marker;
        return targetGrid.Level == supplyGrid.Level &&
               target.TeamName == supplyCart.TeamName &&
               target.Category == CategorySiegeEngine &&
               target.HitPoints < target.MaxHitPoints &&
               (allowSelf || !isSelf);
    }

    private static bool IsWeaponResupplyTarget(
        BattleGridKey supplyGrid,
        BattleOccupantInfo supplyCart,
        BattleGridKey targetGrid,
        BattleOccupantInfo target)
    {
        return targetGrid.Level == supplyGrid.Level &&
               target.TeamName == supplyCart.TeamName &&
               target.WeaponAmmo.HasValue &&
               target.MaxWeaponAmmo.HasValue &&
               target.WeaponAmmo.Value < target.MaxWeaponAmmo.Value;
    }

    private int RepairSiegeEngine(BattleGridKey targetGrid, BattleOccupantInfo target, int repairAmount)
    {
        if (!TryGetCurrentOccupantAtGrid(targetGrid, target, out var currentTarget))
        {
            return 0;
        }

        target = currentTarget;
        if (target.Category != CategorySiegeEngine || target.HitPoints >= target.MaxHitPoints)
        {
            return 0;
        }

        var actualRepair = Mathf.Min(repairAmount, target.MaxHitPoints - target.HitPoints);
        var updatedTarget = target with
        {
            HitPoints = target.HitPoints + actualRepair
        };
        UpdateMarkerStrengthBar(updatedTarget);
        ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedTarget;
        }

        ShowRepairPopup(targetGrid, actualRepair);
        return actualRepair;
    }

    private int RecoverWoundedTroops(BattleGridKey targetGrid, BattleOccupantInfo target, int recoveryAmount)
    {
        if (!TryGetCurrentOccupantAtGrid(targetGrid, target, out var currentTarget))
        {
            return 0;
        }

        target = currentTarget;
        if (target.Category != CategoryUnit || target.WoundedTroops <= 0)
        {
            return 0;
        }

        var missingActiveCapacity = Mathf.Max(0, target.MaxHitPoints - target.TroopCount);
        var actualRecovery = Mathf.Min(recoveryAmount, Mathf.Min(target.WoundedTroops, missingActiveCapacity));
        if (actualRecovery <= 0)
        {
            return 0;
        }

        var updatedTarget = target with
        {
            TroopCount = target.TroopCount + actualRecovery,
            HitPoints = target.HitPoints + actualRecovery,
            WoundedTroops = target.WoundedTroops - actualRecovery
        };
        UpdateMarkerStrengthBar(updatedTarget);
        ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
        ApplyTeamTroopDelta(target.Category, target.TeamName, actualRecovery);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedTarget;
        }

        ShowRepairPopup(targetGrid, actualRecovery);
        return actualRecovery;
    }

    private void ApplyRetreatTroopLoss(BattleOccupantInfo retreatingUnit)
    {
        var retreatTroops = Mathf.Max(0, retreatingUnit.TroopCount);
        ApplyTeamTroopLoss(retreatingUnit, retreatTroops);
    }

    private void ApplyTeamTroopLoss(BattleOccupantInfo unit, int troopLoss)
    {
        if (troopLoss <= 0)
        {
            return;
        }

        ApplyTeamTroopDelta(unit.Category, unit.TeamName, -troopLoss);
    }

    private void ApplyTeamTroopDelta(string category, string teamName, int delta)
    {
        if (category != CategoryUnit || delta == 0)
        {
            return;
        }

        if (teamName.Contains("Attacker"))
        {
            _teamATotalTroops = Mathf.Max(0, _teamATotalTroops + delta);
        }
        else if (teamName.Contains("Defender"))
        {
            _teamBTotalTroops = Mathf.Max(0, _teamBTotalTroops + delta);
        }
    }

    private void ApplyTeamSiegeUnitDelta(string category, string teamName, int delta)
    {
        if (category != CategorySiegeEngine || delta == 0)
        {
            return;
        }

        if (teamName.Contains("Attacker"))
        {
            _teamASiegeUnits = Mathf.Max(0, _teamASiegeUnits + delta);
        }
        else if (teamName.Contains("Defender"))
        {
            _teamBSiegeUnits = Mathf.Max(0, _teamBSiegeUnits + delta);
        }
    }

    private void ApplyTeamGeneralDelta(string category, string teamName, string officerName, int delta)
    {
        if (!IsGeneralCountedPiece(category, officerName) || delta == 0)
        {
            return;
        }

        if (teamName.Contains("Attacker"))
        {
            _teamAGenerals = Mathf.Max(0, _teamAGenerals + delta);
        }
        else if (teamName.Contains("Defender"))
        {
            _teamBGenerals = Mathf.Max(0, _teamBGenerals + delta);
        }
    }

    private static bool IsGeneralCountedPiece(string category, string officerName)
    {
        return category == CategoryUnit &&
               !string.Equals(officerName, "Worker", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildBattleOccupantStableId(BattleOccupantInfo occupant)
    {
        return string.Join("|", occupant.DisplayName, occupant.OfficerName, occupant.TroopType, occupant.ShortLabel, occupant.Category);
    }

    private static string BuildBattleOccupantStableId(BattleOccupantSaveData saveData)
    {
        return string.Join("|", saveData.DisplayName, saveData.OfficerName, saveData.TroopType, saveData.ShortLabel, saveData.Category);
    }

    private bool TryGetBestUnionAttackCandidate(out UnionAttackCandidate candidate)
    {
        candidate = new UnionAttackCandidate(default, new List<UnionAttackParticipant>());
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !CanJoinUnionAttack(_selectedUnit))
        {
            return false;
        }

        var selectedGrid = _selectedUnitGrid.Value;
        var selectedIsAttacker = IsAttackerPiece(_selectedUnit);
        var candidates = new List<UnionAttackCandidate>();
        foreach (var targetEntry in _occupantsByGrid)
        {
            var targetGrid = targetEntry.Key;
            if (targetGrid.Level != selectedGrid.Level || !IsTouchingGrid(selectedGrid, targetGrid))
            {
                continue;
            }

            var target = GetAttackTarget(targetEntry.Value, _selectedUnit.TeamName);
            if (target == null ||
                target.Marker == null ||
                IsAttackerPiece(target) == selectedIsAttacker)
            {
                continue;
            }

            var participants = CollectUnionAttackParticipants(targetGrid, selectedGrid, selectedIsAttacker);
            if (participants.Count >= 2)
            {
                candidates.Add(new UnionAttackCandidate(targetGrid, participants));
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        candidate = candidates
            .OrderByDescending(current => current.Participants.Count)
            .ThenBy(current => GetManhattanDistance(selectedGrid.Grid, current.TargetGrid.Grid))
            .First();
        return true;
    }

    private List<UnionAttackParticipant> CollectUnionAttackParticipants(
        BattleGridKey targetGrid,
        BattleGridKey selectedGrid,
        bool selectedIsAttacker)
    {
        var participants = new List<UnionAttackParticipant>();
        if (_selectedUnit != null)
        {
            participants.Add(new UnionAttackParticipant(selectedGrid, _selectedUnit));
        }

        var supportParticipants = new List<UnionAttackParticipant>();
        foreach (var sourceEntry in _occupantsByGrid)
        {
            var sourceGrid = sourceEntry.Key;
            if (sourceGrid == selectedGrid ||
                sourceGrid.Level != targetGrid.Level ||
                !IsTouchingGrid(sourceGrid, targetGrid))
            {
                continue;
            }

            var supportUnit = sourceEntry.Value.FirstOrDefault(occupant =>
                CanJoinUnionAttack(occupant) &&
                IsAttackerPiece(occupant) == selectedIsAttacker);
            if (supportUnit != null)
            {
                supportParticipants.Add(new UnionAttackParticipant(sourceGrid, supportUnit));
            }
        }

        participants.AddRange(
            supportParticipants
                .OrderBy(participant => GetManhattanDistance(participant.Grid.Grid, selectedGrid.Grid))
                .Take(3));
        return participants;
    }

    private bool CanJoinUnionAttack(BattleOccupantInfo occupant)
    {
        return occupant.Marker != null &&
               occupant.Category == CategoryUnit &&
               !IsMessed(occupant) &&
               !HasUsedChargeThisTurn(occupant) &&
               IsUnionAttackTroopType(occupant.TroopType) &&
               GetAttackDamage(occupant) > 0 &&
               CanUseNormalAttackWithCurrentAmmo(occupant);
    }

    private bool CanUseAttackCommand(BattleOccupantInfo occupant)
    {
        return occupant.TroopType != TroopSupplyCart &&
               IsBattlePiece(occupant) &&
               !IsMessed(occupant) &&
               !HasUsedChargeThisTurn(occupant) &&
               GetEffectiveAttackRange(occupant) > 0 &&
               GetAttackDamage(occupant) > 0 &&
               CanUseNormalAttackWithCurrentAmmo(occupant);
    }

    private bool CanUseChargeCommand(BattleOccupantInfo occupant)
    {
        return occupant.Marker != null &&
               occupant.Category == CategoryUnit &&
               occupant.TroopType == TroopCavalry &&
               !IsMessed(occupant) &&
               !HasUsedChargeThisTurn(occupant) &&
               IsCurrentTurnPiece(occupant);
    }

    private bool HasUsedChargeThisTurn(BattleOccupantInfo occupant)
    {
        return occupant.Marker != null && _chargeUsedByMarkerThisTurn.Contains(occupant.Marker);
    }

    private static bool IsUnionAttackTroopType(string troopType)
    {
        return troopType is TroopInfantry or TroopSpearman or TroopCavalry or TroopArcher or TroopCrossbow or TroopWorker;
    }

    private static int GetUnionAttackDamage(IReadOnlyList<UnionAttackParticipant> participants)
    {
        var damage = 0;
        for (var index = 0; index < participants.Count; index++)
        {
            var participantDamage = GetAttackDamage(participants[index].Occupant);
            damage += index == 0
                ? participantDamage
                : Mathf.Max(1, participantDamage / 2);
        }

        return damage;
    }

    private static bool IsTouchingGrid(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (sourceGrid.Level != targetGrid.Level)
        {
            return false;
        }

        return GetManhattanDistance(sourceGrid.Grid, targetGrid.Grid) == 1;
    }

    private static int GetManhattanDistance(Vector2I a, Vector2I b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }

    private static int GetChebyshevDistance(Vector2I a, Vector2I b)
    {
        return Math.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
    }

    private void OnHideButtonPressed()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !IsCurrentTurnPiece(_selectedUnit) ||
            !CanHideSelectedUnit())
        {
            return;
        }

        var grid = _selectedUnitGrid.Value;
        var hiddenUnit = _selectedUnit with { IsHidden = true };
        ReplaceOccupantAtGrid(grid, _selectedUnit, hiddenUnit);
        _selectedUnit = hiddenUnit;
        AppendBattleLog(hiddenUnit, "Status", $"{FormatLogUnit(hiddenUnit)} hides in forest at {grid}");

        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        HideCommandMenu();
        RefreshHiddenUnitVisibility();
        RefreshInfoPanel();
        RefreshHighlights();
        RefreshBattleLogPanel();
    }

    private void OnDropStoneButtonPressed()
    {
        TryUseWallTopAttack(DropStoneAttackDamage, isDropStone: true);
    }

    private void OnPourOilButtonPressed()
    {
        TryUseWallTopAttack(PourOilAttackDamage, isDropStone: false);
    }

    private void TryUseWallTopAttack(int damage, bool isDropStone)
    {
        if (!TryGetWallTopAttackGrid(out var targetGrid) || _selectedUnit == null || !_selectedUnitGrid.HasValue ||
            !TryConsumeWallTopAttackUse(isDropStone))
        {
            return;
        }

        var attackDirection = GetInfantryDirection(_selectedUnitGrid.Value.Grid, targetGrid.Grid);
        var attackingUnit = _selectedUnit with { FacingDirection = attackDirection };
        ReplaceOccupantAtGrid(_selectedUnitGrid.Value, _selectedUnit, attackingUnit);
        _selectedUnit = attackingUnit;

        var shouldTemporarilyRevealOccludedUnits =
            IsUnitOccludedByCastleVisual(_selectedUnitGrid.Value) ||
            IsUnitOccludedByCastleVisual(targetGrid);
        if (shouldTemporarilyRevealOccludedUnits)
        {
            ClearOccludedUnitSilhouettes();
        }

        var hasEnemyTarget = HasEnemyBattleTarget(targetGrid, attackingUnit);
        var hurtAnimationDuration = hasEnemyTarget
            ? ApplyTargetHurtAnimation(_selectedUnitGrid.Value, targetGrid, attackingUnit)
            : 0.0;
        var specialEffectDuration = isDropStone
            ? PlayDropStoneEffect(_selectedUnitGrid.Value, targetGrid)
            : PlayPourOilEffect(_selectedUnitGrid.Value, targetGrid);
        var effectDelaySeconds = Math.Max(hurtAnimationDuration, specialEffectDuration);
        AppendBattleLog(attackingUnit, "Action", $"{FormatLogUnit(attackingUnit)} {(isDropStone ? "Drop Stone" : "Pour Oil")} -> {targetGrid}");
        if (hasEnemyTarget)
        {
            ApplyAttackDamage(attackingUnit, targetGrid, effectDelaySeconds, damage);
        }
        if (shouldTemporarilyRevealOccludedUnits)
        {
            RefreshOccludedUnitSilhouettesAfterDelay(effectDelaySeconds);
        }

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private bool TryGetWallTopAttackGrid(out BattleGridKey targetGrid)
    {
        targetGrid = default;
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || _selectedUnitGrid.Value.Level != 2)
        {
            return false;
        }

        var sourceGrid = _selectedUnitGrid.Value;
        if (!IsWallTopGrid(sourceGrid.Grid))
        {
            return false;
        }

        var cell = _mapData?.GetCell(sourceGrid.X, sourceGrid.Y);
        var facing = cell?.StructureFacing ?? ResolveScenarioDefinition().DefaultStructureFacing;
        var targetOffset = facing == BattleStructureFacing.NorthWest
            ? Vector2I.Right
            : Vector2I.Down;
        var candidate = new BattleGridKey(sourceGrid.X + targetOffset.X, sourceGrid.Y + targetOffset.Y, 0);
        if (!IsWithinMap(candidate.Grid))
        {
            return false;
        }

        targetGrid = candidate;
        return true;
    }

    private bool HasEnemyBattleTarget(BattleGridKey targetGrid, BattleOccupantInfo attacker)
    {
        return _occupantsByGrid.TryGetValue(targetGrid, out var occupants) &&
               occupants.Any(occupant =>
                   occupant.Marker != null &&
                   IsBattlePiece(occupant) &&
                   (IsForestGrid(targetGrid) || !IsHiddenFromSide(occupant, attacker.TeamName)) &&
                   IsAttackerPiece(occupant) != IsAttackerPiece(attacker));
    }

    private int GetWallTopAttackUsesRemaining(bool isDropStone)
    {
        if (_selectedUnit?.Marker == null)
        {
            return 0;
        }

        if (!_wallTopAttackAmmoByMarker.TryGetValue(_selectedUnit.Marker, out var ammo))
        {
            return isDropStone ? DropStoneUsesPerUnit : PourOilUsesPerUnit;
        }

        return isDropStone ? ammo.DropStoneUses : ammo.PourOilUses;
    }

    private bool TryConsumeWallTopAttackUse(bool isDropStone)
    {
        if (_selectedUnit?.Marker == null)
        {
            return false;
        }

        var marker = _selectedUnit.Marker;
        var ammo = _wallTopAttackAmmoByMarker.TryGetValue(marker, out var existingAmmo)
            ? existingAmmo
            : new WallTopAttackAmmo(DropStoneUsesPerUnit, PourOilUsesPerUnit);
        if (isDropStone)
        {
            if (ammo.DropStoneUses <= 0)
            {
                return false;
            }

            ammo = ammo with { DropStoneUses = ammo.DropStoneUses - 1 };
        }
        else
        {
            if (ammo.PourOilUses <= 0)
            {
                return false;
            }

            ammo = ammo with { PourOilUses = ammo.PourOilUses - 1 };
        }

        _wallTopAttackAmmoByMarker[marker] = ammo;
        return true;
    }

    private void OnStrategyButtonPressed()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue)
        {
            return;
        }

        _selectedStrategyAction = ResolveStrategyAction(_selectedUnit);
        _commandMode = BattleCommandMode.StrategySelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        if (_selectedStrategyAction == BattleStrategyAction.None)
        {
            _commandMode = BattleCommandMode.AwaitingCommand;
            HideCommandMenu();
            RefreshInfoPanel();
            RefreshHighlights();
            return;
        }

        var strategyTargets = _selectedStrategyAction == BattleStrategyAction.Fire
            ? CalculateFireStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit)
            : CalculateMentalStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit);
        foreach (var targetGrid in strategyTargets)
        {
            _strategyTargetGrids.Add(targetGrid);
        }

        if (_strategyTargetGrids.Count == 0)
        {
            _commandMode = BattleCommandMode.AwaitingCommand;
            HideCommandMenu();
            RefreshInfoPanel();
            RefreshHighlights();
            return;
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private IEnumerable<BattleGridKey> CalculateFireStrategyTargetGrids(BattleGridKey sourceGrid, BattleOccupantInfo attacker)
    {
        var strategyAttacker = attacker with { AttackRange = attacker.AttackRange + FireStrategyRangeBonus };
        foreach (var grid in CalculateAttackableGrids(sourceGrid, strategyAttacker))
        {
            if (grid.Level != 0 || !IsWithinMap(grid.Grid) || _mapData == null)
            {
                continue;
            }

            var cell = _mapData.GetCell(grid.X, grid.Y);
            if (CanCellIgnite(cell))
            {
                yield return grid;
            }
        }
    }

    private bool TryExecuteSelectedStrategy()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || !_selectedGrid.HasValue || _selectedUnit.Marker == null)
        {
            return false;
        }

        var targetGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        if (!_strategyTargetGrids.Contains(targetGrid))
        {
            return false;
        }

        if (_selectedStrategyAction == BattleStrategyAction.Mental)
        {
            return TryExecuteMentalStrategy(targetGrid);
        }

        if (!CanUseFireStrategy(_selectedUnit))
        {
            return false;
        }

        var actingMarker = _selectedUnit.Marker;
        if (actingMarker == null)
        {
            return false;
        }

        var attackDirection = GetInfantryDirection(_selectedUnitGrid.Value.Grid, targetGrid.Grid);
        var actingUnit = _selectedUnit with { FacingDirection = attackDirection };
        if (!TrySpendWeaponAmmo(actingUnit, out actingUnit))
        {
            return false;
        }

        if (!IgniteBattleFire(targetGrid))
        {
            return false;
        }

        if (!TrySpendStrategyPlan(_selectedUnit.TeamName))
        {
            return false;
        }

        ReplaceOccupantAtGrid(_selectedUnitGrid.Value, _selectedUnit, actingUnit);
        _selectedUnit = actingUnit;
        _strategyUsedByMarkerThisTurn.Add(actingMarker);
        var shouldTemporarilyRevealOccludedUnits =
            IsUnitOccludedByCastleVisual(_selectedUnitGrid.Value) ||
            IsUnitOccludedByCastleVisual(targetGrid);
        if (shouldTemporarilyRevealOccludedUnits)
        {
            ClearOccludedUnitSilhouettes();
        }

        var effectDelaySeconds = PlayStrategyActionEffects(_selectedUnitGrid.Value, targetGrid, actingUnit, attackDirection);
        AppendBattleLog(_selectedUnit, "Strategy", $"{FormatLogUnit(_selectedUnit)} ignites fire at {targetGrid}{FormatWeaponAmmoLog(_selectedUnit)}");
        if (shouldTemporarilyRevealOccludedUnits)
        {
            RefreshOccludedUnitSilhouettesAfterDelay(effectDelaySeconds);
        }

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        HideCommandMenu();
        ConfigureHud();
        RefreshInfoPanel();
        RefreshHighlights();
        return true;
    }

    private BattleStrategyAction ResolveStrategyAction(BattleOccupantInfo occupant)
    {
        if (CanUseFireStrategy(occupant))
        {
            return BattleStrategyAction.Fire;
        }

        if (CanUseMentalStrategy(occupant))
        {
            return BattleStrategyAction.Mental;
        }

        return BattleStrategyAction.None;
    }

    private IEnumerable<BattleGridKey> CalculateMentalStrategyTargetGrids(BattleGridKey sourceGrid, BattleOccupantInfo actor)
    {
        if (!CanUseMentalStrategy(actor))
        {
            yield break;
        }

        foreach (var (targetGrid, target) in GetMentalStrategyCandidates(sourceGrid, actor))
        {
            if (CanApplyMessStrategy(actor, target) || CanApplyCalmStrategy(actor, target))
            {
                yield return targetGrid;
            }
        }
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetMentalStrategyCandidates(BattleGridKey sourceGrid, BattleOccupantInfo actor)
    {
        foreach (var entry in _occupantsByGrid)
        {
            var targetGrid = entry.Key;
            if (targetGrid.Level != sourceGrid.Level ||
                GetChebyshevDistance(sourceGrid.Grid, targetGrid.Grid) > MessStrategyRange)
            {
                continue;
            }

            foreach (var target in entry.Value)
            {
                if (target.Marker != null &&
                    target.Category == CategoryUnit &&
                    !IsHiddenFromSide(target, actor.TeamName))
                {
                    yield return (targetGrid, target);
                }
            }
        }
    }

    private bool TryExecuteMentalStrategy(BattleGridKey targetGrid)
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || _selectedUnit.Marker == null)
        {
            return false;
        }

        var target = GetOccupantsAtGrid(targetGrid.Grid)
            .Where(entry => entry.Grid == targetGrid)
            .Select(entry => entry.Occupant)
            .FirstOrDefault(occupant =>
                occupant.Marker != null &&
                occupant.Category == CategoryUnit &&
                !IsHiddenFromSide(occupant, _selectedUnit.TeamName));
        if (target == null)
        {
            return false;
        }

        var success = target.TeamName == _selectedUnit.TeamName
            ? TryApplyCalmStrategy(targetGrid, target)
            : TryApplyMessStrategy(targetGrid, target, _selectedUnit);
        if (!success)
        {
            return false;
        }

        if (!TrySpendStrategyPlan(_selectedUnit.TeamName))
        {
            return false;
        }

        _strategyUsedByMarkerThisTurn.Add(_selectedUnit.Marker);
        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        HideCommandMenu();
        ConfigureHud();
        RefreshInfoPanel();
        RefreshHighlights();
        return true;
    }

    private bool TryApplyMessStrategy(BattleGridKey targetGrid, BattleOccupantInfo target, BattleOccupantInfo actor)
    {
        if (!CanApplyMessStrategy(actor, target) ||
            !TryGetCurrentOccupantAtGrid(targetGrid, target, out var currentTarget))
        {
            return false;
        }

        target = currentTarget;
        var successChance = GetMessStrategySuccessChance(actor, target);
        if (GD.Randf() * 100.0f > successChance)
        {
            AppendBattleLog(actor, "Strategy", $"{FormatLogUnit(actor)} uses Mess against {FormatLogUnit(target)}, but it fails ({successChance:N0}%)");
            return true;
        }

        var updatedMorale = target.Morale.HasValue
            ? Mathf.Clamp(target.Morale.Value - MessStrategyMoraleDamage, 0, 120)
            : target.Morale;
        var updatedTarget = target with
        {
            MessTurns = Math.Max(target.MessTurns, MessStrategyDurationTurns),
            Morale = updatedMorale
        };
        ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedTarget;
        }

        if (target.Morale.HasValue && updatedMorale.HasValue)
        {
            ShowMoralePopup(targetGrid, updatedMorale.Value - target.Morale.Value);
        }

        AppendBattleLog(actor, "Strategy", $"{FormatLogUnit(actor)} uses Mess against {FormatLogUnit(target)}: status Mess {updatedTarget.MessTurns} turns, morale -{MessStrategyMoraleDamage} ({successChance:N0}%)");
        return true;
    }

    private bool TryApplyCalmStrategy(BattleGridKey targetGrid, BattleOccupantInfo target)
    {
        if (_selectedUnit == null ||
            !CanApplyCalmStrategy(_selectedUnit, target) ||
            !TryGetCurrentOccupantAtGrid(targetGrid, target, out var currentTarget))
        {
            return false;
        }

        target = currentTarget;
        var updatedMorale = target.Morale.HasValue
            ? Mathf.Clamp(target.Morale.Value + CalmStrategyMoraleRestore, 0, 120)
            : target.Morale;
        var updatedTarget = target with
        {
            MessTurns = 0,
            Morale = updatedMorale
        };
        ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedTarget;
        }

        if (target.Morale.HasValue && updatedMorale.HasValue)
        {
            ShowMoralePopup(targetGrid, updatedMorale.Value - target.Morale.Value);
        }

        AppendBattleLog(_selectedUnit, "Strategy", $"{FormatLogUnit(_selectedUnit)} uses Calm on {FormatLogUnit(target)}: Mess cleared, morale +{CalmStrategyMoraleRestore}");
        return true;
    }

    private bool CanUseMentalStrategy(BattleOccupantInfo? occupant)
    {
        return occupant?.Marker != null &&
               occupant.Category == CategoryUnit &&
               HasStrategyPlans(occupant.TeamName) &&
               !_strategyUsedByMarkerThisTurn.Contains(occupant.Marker) &&
               !IsMessed(occupant);
    }

    private static bool CanApplyMessStrategy(BattleOccupantInfo actor, BattleOccupantInfo target)
    {
        return actor.TeamName != target.TeamName &&
               target.Category == CategoryUnit &&
               !IsMessed(target);
    }

    private static bool CanApplyCalmStrategy(BattleOccupantInfo actor, BattleOccupantInfo target)
    {
        return actor.TeamName == target.TeamName &&
               target.Category == CategoryUnit &&
               IsMessed(target);
    }

    private static float GetMessStrategySuccessChance(BattleOccupantInfo actor, BattleOccupantInfo target)
    {
        var officerDelta = GetOfficerBattleAttribute(actor.OfficerName) - GetOfficerBattleAttribute(target.OfficerName);
        var targetMorale = target.Morale ?? DefaultUnitMorale;
        var lowMoraleBonus = targetMorale <= MessMoraleThreshold ? 20 : 0;
        return Mathf.Clamp(55.0f + (officerDelta * 0.5f) + ((50 - targetMorale) * 0.5f) + lowMoraleBonus, 25.0f, 90.0f);
    }

    private double PlayStrategyActionEffects(
        BattleGridKey sourceGrid,
        BattleGridKey targetGrid,
        BattleOccupantInfo actingUnit,
        BattleSpriteDirection attackDirection)
    {
        if (actingUnit.Category == CategorySiegeEngine && actingUnit.TroopType == TroopCatapult)
        {
            var attackAnimationDuration = ApplyAttackAnimation(actingUnit, attackDirection);
            var projectileDuration = PlayCatapultProjectileEffect(sourceGrid, targetGrid);
            return Math.Max(attackAnimationDuration, projectileDuration);
        }

        if (IsArrowProjectileAttacker(actingUnit))
        {
            var attackAnimationDuration = ApplyAttackAnimation(actingUnit, attackDirection);
            var projectileDuration = PlayArrowProjectileEffect(sourceGrid, targetGrid);
            return Math.Max(attackAnimationDuration, projectileDuration);
        }

        return 0.0;
    }

    private bool CanUseFireStrategy(BattleOccupantInfo? occupant)
    {
        if (occupant?.Marker == null || !CanUseUnitTypeFireStrategy(occupant))
        {
            return false;
        }

        return !_strategyUsedByMarkerThisTurn.Contains(occupant.Marker) &&
               HasStrategyPlans(occupant.TeamName) &&
               !IsMessed(occupant) &&
               HasWeaponAmmo(occupant);
    }

    private static bool CanUseUnitTypeFireStrategy(BattleOccupantInfo occupant)
    {
        return occupant.TroopType is TroopArcher or TroopCrossbow ||
               (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopCatapult);
    }

    private bool IgniteBattleFire(BattleGridKey grid)
    {
        if (_mapData == null || grid.Level != 0 || !IsWithinMap(grid.Grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (!CanCellIgnite(cell))
        {
            return false;
        }

        var duration = GetInitialFireDuration(GetCurrentBattleWeather(), cell);
        var burnTurns = 0;
        if (_activeFireByGrid.TryGetValue(grid, out var existingFire))
        {
            duration = Math.Max(duration, existingFire.RemainingTurns);
            burnTurns = existingFire.BurnTurns;
        }

        _activeFireByGrid[grid] = new BattleFireState(duration, burnTurns);
        RefreshFireVisual(grid);
        RefreshBattleDepthLayerOrder();
        return true;
    }

    private void ResolveBattleFireAtTurnEnd()
    {
        if (_mapData == null || _activeFireByGrid.Count == 0)
        {
            return;
        }

        var weather = GetCurrentBattleWeather();
        var activeFires = _activeFireByGrid.ToArray();
        var pendingNewFires = new HashSet<BattleGridKey>();
        var expiredFires = new List<BattleGridKey>();

        foreach (var (grid, state) in activeFires)
        {
            ApplyBattleFireDamage(grid, weather);

            if (CanFireSpread(weather, grid, state))
            {
                foreach (var spreadGrid in GetFireSpreadTargets(grid))
                {
                    if (!_activeFireByGrid.ContainsKey(spreadGrid))
                    {
                        pendingNewFires.Add(spreadGrid);
                    }
                }
            }

            var remainingTurns = state.RemainingTurns - 1;
            if (remainingTurns <= 0)
            {
                expiredFires.Add(grid);
            }
            else
            {
                _activeFireByGrid[grid] = state with
                {
                    RemainingTurns = remainingTurns,
                    BurnTurns = state.BurnTurns + 1
                };
                RefreshFireVisual(grid);
            }
        }

        foreach (var grid in expiredFires)
        {
            _activeFireByGrid.Remove(grid);
            RemoveFireVisual(grid);
        }

        foreach (var spreadGrid in pendingNewFires)
        {
            IgniteBattleFire(spreadGrid);
        }

        RefreshInfoPanel();
    }

    private void ApplyBattleFireDamage(BattleGridKey targetGrid, BattleWeatherType weather)
    {
        ApplyBattleFireDamageToOccupants(targetGrid, GetFireDamagePerTurn(weather));
        ApplyBattleFireDamageToStructure(targetGrid);
    }

    private void ApplyBattleFireDamageToOccupants(BattleGridKey targetGrid, int damage)
    {
        if (damage <= 0 || !_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            return;
        }

        var target = GetFireDamageTarget(targetOccupants);
        if (target == null)
        {
            return;
        }

        var casualtyResult = ApplyUnitCasualties(targetGrid, target, damage, FireDamageKilledRatio);
        var updatedTarget = casualtyResult.UpdatedTarget;

        ShowDamagePopup(targetGrid, casualtyResult.ActualDamage);
        AppendBattleLog(target, "Hurt", $"Fire burns {FormatLogUnit(target)} for {casualtyResult.ActualDamage:N0} at {targetGrid} ({FormatCasualtyResult(casualtyResult)})");
        ConfigureHud();
        if (updatedTarget.HitPoints <= 0)
        {
            DestroyOccupantAfterDelay(targetGrid, updatedTarget, 0.0);
        }
    }

    private void ApplyBattleFireDamageToStructure(BattleGridKey targetGrid)
    {
        if (_mapData == null || targetGrid.Level != 0 || !IsWithinMap(targetGrid.Grid))
        {
            return;
        }

        var cell = _mapData.GetCell(targetGrid.X, targetGrid.Y);
        if (cell.HasBridgeHealth)
        {
            var actualBridgeDamage = _mapData.ApplyBridgeDamage(targetGrid.Grid, FireDamageToBridge);
            if (actualBridgeDamage > 0)
            {
                ShowDamagePopup(targetGrid, actualBridgeDamage);
                if (!cell.HasBridgeHealth)
                {
                    RefreshWorkerObjectLayers();
                }
            }

            return;
        }

        if (cell.Structure == BattleStructureType.WoodenFence && cell.HasStructureHealth)
        {
            var actualFenceDamage = _mapData.ApplyWoodenFenceDamage(targetGrid.Grid, FireDamageToWoodenFence);
            if (actualFenceDamage > 0)
            {
                ShowDamagePopup(targetGrid, actualFenceDamage);
                if (cell.Structure != BattleStructureType.WoodenFence)
                {
                    RefreshWorkerObjectLayers();
                }
            }

            return;
        }

        if (cell.Structure == BattleStructureType.Gate && cell.HasStructureHealth && !cell.IsBroken)
        {
            var actualDamage = ApplyGateGroupDamage(targetGrid.Grid, FireDamageToGate);
            if (actualDamage > 0)
            {
                ShowDamagePopup(targetGrid, actualDamage);
            }
        }
    }

    private IEnumerable<BattleGridKey> GetFireSpreadTargets(BattleGridKey sourceGrid)
    {
        if (_mapData == null)
        {
            yield break;
        }

        var sourceCell = _mapData.GetCell(sourceGrid.X, sourceGrid.Y);
        var maxSpreadTargets = GetFireSpreadTargetCount(sourceCell, GetCurrentBattleWindPower());
        if (maxSpreadTargets <= 0)
        {
            yield break;
        }

        var windOffset = GetWindGridOffset(GetCurrentBattleWindDirection());
        var candidates = GetFireSpreadOffsets()
            .Select(offset => new BattleGridKey(sourceGrid.X + offset.X, sourceGrid.Y + offset.Y, 0))
            .Where(candidate => IsWithinMap(candidate.Grid))
            .Select(candidate => (Grid: candidate, Cell: _mapData.GetCell(candidate.X, candidate.Y)))
            .Where(candidate => CanCellIgnite(candidate.Cell))
            .OrderByDescending(candidate => GetFireSpreadScore(candidate.Grid, candidate.Cell, sourceGrid, windOffset))
            .ThenBy(candidate => candidate.Grid.Y)
            .ThenBy(candidate => candidate.Grid.X)
            .Take(maxSpreadTargets)
            .ToList();

        foreach (var (grid, _) in candidates)
        {
            yield return grid;
        }
    }

    private void RefreshFireVisual(BattleGridKey grid)
    {
        if (_battleDepthLayer == null || _mapData == null)
        {
            return;
        }

        if (!_fireVisualsByGrid.TryGetValue(grid, out var fireRoot))
        {
            fireRoot = CreateFireVisual(grid);
            _battleDepthLayer.AddChild(fireRoot);
            _fireVisualsByGrid[grid] = fireRoot;
        }
        else
        {
            fireRoot.Position = GetFireVisualPosition(grid);
        }

        RegisterBattleDepthEntry(fireRoot, grid, BattleDepthRenderKind.FireEffect);
    }

    private void RemoveFireVisual(BattleGridKey grid)
    {
        if (!_fireVisualsByGrid.TryGetValue(grid, out var fireRoot))
        {
            return;
        }

        _fireVisualsByGrid.Remove(grid);
        _battleDepthEntries.Remove(fireRoot);
        fireRoot.QueueFree();
    }

    private Node2D CreateFireVisual(BattleGridKey grid)
    {
        var fireRoot = new Node2D
        {
            Name = $"Fire_{grid.X}_{grid.Y}",
            Position = GetFireVisualPosition(grid),
            ZIndex = 0
        };

        fireRoot.AddChild(CreateFireFlamePolygon(new Vector2(-12.0f, -6.0f), 9.0f, 16.0f, new Color(1.0f, 0.45f, 0.08f, 0.95f)));
        fireRoot.AddChild(CreateFireFlamePolygon(new Vector2(0.0f, -12.0f), 11.0f, 20.0f, new Color(1.0f, 0.76f, 0.14f, 0.96f)));
        fireRoot.AddChild(CreateFireFlamePolygon(new Vector2(12.0f, -5.0f), 8.0f, 15.0f, new Color(0.98f, 0.30f, 0.04f, 0.92f)));
        return fireRoot;
    }

    private static Polygon2D CreateFireFlamePolygon(Vector2 offset, float halfWidth, float height, Color color)
    {
        return new Polygon2D
        {
            Position = offset,
            Color = color,
            Polygon = new[]
            {
                new Vector2(0.0f, -height),
                new Vector2(halfWidth, 0.0f),
                new Vector2(0.0f, 6.0f),
                new Vector2(-halfWidth, 0.0f)
            }
        };
    }

    private Vector2 GetFireVisualPosition(BattleGridKey grid)
    {
        var center = _groundLayer?.MapToLocal(grid.Grid) ?? BattleMapRenderer.GridToWorld(grid.Grid);
        return center + new Vector2(0.0f, -6.0f);
    }

    private static bool CanCellIgnite(BattleCellData cell)
    {
        if (cell.Terrain is BattleTerrainType.Moat or BattleTerrainType.WallWalk)
        {
            return false;
        }

        if (cell.Structure is BattleStructureType.Wall or BattleStructureType.Tower or BattleStructureType.RockBig or BattleStructureType.RockSmall)
        {
            return false;
        }

        return true;
    }

    private static int GetInitialFireDuration(BattleWeatherType weather, BattleCellData cell)
    {
        var baseDuration = weather switch
        {
            BattleWeatherType.Rain => FireStrategyBaseDurationRain,
            BattleWeatherType.Cloudy => FireStrategyBaseDurationCloudy,
            _ => FireStrategyBaseDurationSunny
        };

        return Math.Max(1, baseDuration + GetFireDurationBonus(cell));
    }

    private static int GetFireDamagePerTurn(BattleWeatherType weather)
    {
        return weather switch
        {
            BattleWeatherType.Rain => FireDamagePerTurnRain,
            BattleWeatherType.Cloudy => FireDamagePerTurnCloudy,
            _ => FireDamagePerTurnSunny
        };
    }

    private bool CanFireSpread(BattleWeatherType weather, BattleGridKey grid, BattleFireState state)
    {
        if (_mapData == null || weather == BattleWeatherType.Rain || state.RemainingTurns <= 1)
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        var interval = GetFireSpreadInterval(cell, GetCurrentBattleWindPower());
        return interval > 0 && state.BurnTurns % interval == 0;
    }

    private static IEnumerable<Vector2I> GetFireSpreadOffsets()
    {
        yield return new Vector2I(1, 0);
        yield return new Vector2I(-1, 0);
        yield return new Vector2I(0, 1);
        yield return new Vector2I(0, -1);
        yield return new Vector2I(1, 1);
        yield return new Vector2I(1, -1);
        yield return new Vector2I(-1, 1);
        yield return new Vector2I(-1, -1);
    }

    private static int GetFireSpreadTargetCount(BattleCellData sourceCell, BattleWindPower windPower)
    {
        if (windPower == BattleWindPower.Calm)
        {
            return CanTerrainCarrySlowFire(sourceCell) ? 1 : 0;
        }

        var baseCount = sourceCell.Terrain switch
        {
            BattleTerrainType.Forest => 3,
            BattleTerrainType.Grass => 2,
            BattleTerrainType.Plain or BattleTerrainType.Courtyard => 1,
            BattleTerrainType.Road or BattleTerrainType.Bridge => 1,
            _ => 0
        };

        if (sourceCell.Structure == BattleStructureType.WoodenFence)
        {
            baseCount = Math.Max(baseCount, 2);
        }

        var windAdjustment = windPower == BattleWindPower.Strong ? 1 : 0;

        return Math.Clamp(baseCount + windAdjustment, 0, FireMaxSpreadCandidates);
    }

    private static int GetFireSpreadInterval(BattleCellData sourceCell, BattleWindPower windPower)
    {
        if (windPower == BattleWindPower.Calm)
        {
            return CanTerrainCarrySlowFire(sourceCell) ? 3 : 0;
        }

        var baseInterval = sourceCell.Terrain switch
        {
            BattleTerrainType.Forest => 1,
            BattleTerrainType.Grass => 1,
            BattleTerrainType.Plain or BattleTerrainType.Courtyard => 2,
            BattleTerrainType.Road or BattleTerrainType.Bridge => 3,
            _ => 0
        };

        if (sourceCell.Structure == BattleStructureType.WoodenFence)
        {
            baseInterval = 1;
        }

        if (baseInterval <= 0)
        {
            return 0;
        }

        var windAdjustment = windPower == BattleWindPower.Strong ? -1 : 0;

        return Math.Max(1, baseInterval + windAdjustment);
    }

    private static bool CanTerrainCarrySlowFire(BattleCellData cell)
    {
        return cell.Structure == BattleStructureType.WoodenFence ||
               cell.Terrain is BattleTerrainType.Forest or BattleTerrainType.Grass;
    }

    private static int GetFireDurationBonus(BattleCellData cell)
    {
        if (cell.Structure == BattleStructureType.WoodenFence)
        {
            return 1;
        }

        return cell.Terrain switch
        {
            BattleTerrainType.Forest => 2,
            BattleTerrainType.Grass => 1,
            BattleTerrainType.Road or BattleTerrainType.Bridge => -1,
            _ => 0
        };
    }

    private static int GetFireSpreadScore(BattleGridKey candidate, BattleCellData candidateCell, BattleGridKey sourceGrid, Vector2I windOffset)
    {
        var offset = new Vector2I(candidate.X - sourceGrid.X, candidate.Y - sourceGrid.Y);
        return GetFireTerrainSpreadScore(candidateCell) + GetFireWindSpreadScore(offset, windOffset);
    }

    private static int GetFireTerrainSpreadScore(BattleCellData cell)
    {
        if (cell.Structure == BattleStructureType.WoodenFence)
        {
            return 7;
        }

        return cell.Terrain switch
        {
            BattleTerrainType.Forest => 8,
            BattleTerrainType.Grass => 6,
            BattleTerrainType.Plain or BattleTerrainType.Courtyard => 4,
            BattleTerrainType.Road or BattleTerrainType.Bridge => 2,
            _ => 0
        };
    }

    private static int GetFireWindSpreadScore(Vector2I offset, Vector2I windOffset)
    {
        if (offset == windOffset)
        {
            return 6;
        }

        var dot = (offset.X * windOffset.X) + (offset.Y * windOffset.Y);
        if (dot > 0)
        {
            return 3;
        }

        return dot == 0 ? 1 : -2;
    }

    private BattleWeatherType GetCurrentBattleWeather()
    {
        return _currentBattleWeather ?? ResolveScenarioDefinition().Weather;
    }

    private BattleTimeOfDay GetCurrentBattleTimeOfDay()
    {
        return _currentBattleTimeOfDay ?? ResolveScenarioDefinition().TimeOfDay;
    }

    private BattleWindDirection GetCurrentBattleWindDirection()
    {
        return _currentBattleWindDirection ?? ResolveScenarioDefinition().WindDirection;
    }

    private BattleWindPower GetCurrentBattleWindPower()
    {
        return _currentBattleWindPower ?? ResolveScenarioDefinition().WindPower;
    }

    private static BattleWeatherType GetNextBattleWeather(BattleWeatherType weather)
    {
        return weather switch
        {
            BattleWeatherType.Sunny => BattleWeatherType.Cloudy,
            BattleWeatherType.Cloudy => BattleWeatherType.Rain,
            _ => BattleWeatherType.Sunny
        };
    }

    private static BattleTimeOfDay GetNextBattleTimeOfDay(BattleTimeOfDay timeOfDay)
    {
        return timeOfDay switch
        {
            BattleTimeOfDay.Dawn => BattleTimeOfDay.Morning,
            BattleTimeOfDay.Morning => BattleTimeOfDay.Afternoon,
            BattleTimeOfDay.Afternoon => BattleTimeOfDay.Night,
            _ => BattleTimeOfDay.Dawn
        };
    }

    private static BattleWindDirection GetNextBattleWindDirection(BattleWindDirection direction)
    {
        return direction switch
        {
            BattleWindDirection.NorthEast => BattleWindDirection.NorthWest,
            BattleWindDirection.NorthWest => BattleWindDirection.SouthWest,
            BattleWindDirection.SouthWest => BattleWindDirection.SouthEast,
            _ => BattleWindDirection.NorthEast
        };
    }

    private static BattleWindPower GetNextBattleWindPower(BattleWindPower power)
    {
        return power switch
        {
            BattleWindPower.Calm => BattleWindPower.Breeze,
            BattleWindPower.Breeze => BattleWindPower.Strong,
            _ => BattleWindPower.Calm
        };
    }

    private string FormatBattleWeather(BattleWeatherType weather)
    {
        return weather switch
        {
            BattleWeatherType.Cloudy => BattleText("ui.battle.weather_cloudy", "Cloudy"),
            BattleWeatherType.Rain => BattleText("ui.battle.weather_rain", "Rain"),
            _ => BattleText("ui.battle.weather_sunny", "Sunny")
        };
    }

    private string FormatBattleTimeOfDay(BattleTimeOfDay timeOfDay)
    {
        return timeOfDay switch
        {
            BattleTimeOfDay.Dawn => BattleText("ui.battle.time_dawn", "Dawn"),
            BattleTimeOfDay.Afternoon => BattleText("ui.battle.time_afternoon", "Afternoon"),
            BattleTimeOfDay.Night => BattleText("ui.battle.time_night", "Night"),
            _ => BattleText("ui.battle.time_morning", "Morning")
        };
    }

    private void ApplyTimeOfDayVisual(bool animate)
    {
        if (_timeOfDayOverlay == null)
        {
            return;
        }

        var targetColor = GetTimeOfDayOverlayColor(GetCurrentBattleTimeOfDay());
        _timeOfDayOverlayTween?.Kill();
        if (!animate)
        {
            _timeOfDayOverlay.Color = targetColor;
            return;
        }

        _timeOfDayOverlayTween = _timeOfDayOverlay.CreateTween();
        _timeOfDayOverlayTween.SetEase(Tween.EaseType.InOut);
        _timeOfDayOverlayTween.SetTrans(Tween.TransitionType.Sine);
        _timeOfDayOverlayTween.TweenProperty(_timeOfDayOverlay, "color", targetColor, 0.45);
    }

    private static Color GetTimeOfDayOverlayColor(BattleTimeOfDay timeOfDay)
    {
        return timeOfDay switch
        {
            BattleTimeOfDay.Dawn => new Color(1.0f, 0.52f, 0.24f, 0.16f),
            BattleTimeOfDay.Afternoon => new Color(1.0f, 0.86f, 0.32f, 0.08f),
            BattleTimeOfDay.Night => new Color(0.03f, 0.08f, 0.22f, 0.42f),
            _ => new Color(1.0f, 0.95f, 0.78f, 0.04f)
        };
    }

    private void BuildWeatherVisuals()
    {
        if (_weatherOverlay == null || _rainStreaks.Count > 0)
        {
            return;
        }

        const int rainStreakCount = 76;
        for (var index = 0; index < rainStreakCount; index++)
        {
            var streak = new ColorRect
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Color = new Color(0.72f, 0.84f, 1.0f, 0.44f),
                Size = new Vector2(index % 3 == 0 ? 1.4f : 1.0f, 30.0f + (index % 5) * 5.0f),
                Rotation = -0.34f,
                Visible = false
            };
            _weatherOverlay.AddChild(streak);
            _rainStreaks.Add(streak);
        }
    }

    private void ApplyWeatherVisual(bool animate)
    {
        if (_weatherOverlay == null)
        {
            return;
        }

        var targetColor = GetWeatherOverlayColor(GetCurrentBattleWeather());
        _weatherOverlayTween?.Kill();
        if (!animate)
        {
            _weatherOverlay.Color = targetColor;
        }
        else
        {
            _weatherOverlayTween = _weatherOverlay.CreateTween();
            _weatherOverlayTween.SetEase(Tween.EaseType.InOut);
            _weatherOverlayTween.SetTrans(Tween.TransitionType.Sine);
            _weatherOverlayTween.TweenProperty(_weatherOverlay, "color", targetColor, 0.35);
        }

        var showRain = GetCurrentBattleWeather() == BattleWeatherType.Rain;
        foreach (var streak in _rainStreaks)
        {
            streak.Visible = showRain;
        }
    }

    private void UpdateWeatherVisual(double delta)
    {
        if (_weatherOverlay == null || GetCurrentBattleWeather() != BattleWeatherType.Rain)
        {
            return;
        }

        _weatherEffectTime += delta;
        var overlaySize = _weatherOverlay.Size;
        if (overlaySize.X <= 0.0f || overlaySize.Y <= 0.0f)
        {
            overlaySize = GetViewportRect().Size;
        }

        var travelHeight = overlaySize.Y + 120.0f;
        var travelWidth = overlaySize.X + 180.0f;
        for (var index = 0; index < _rainStreaks.Count; index++)
        {
            var streak = _rainStreaks[index];
            var speed = 280.0f + (index % 7) * 36.0f;
            var phase = (float)((_weatherEffectTime * speed) + index * 53.0);
            var y = PositiveModulo(phase, travelHeight) - 80.0f;
            var xBase = PositiveModulo((index * 97.0f) + (float)_weatherEffectTime * 46.0f, travelWidth) - 90.0f;
            streak.Position = new Vector2(xBase, y);
        }
    }

    private static Color GetWeatherOverlayColor(BattleWeatherType weather)
    {
        return weather switch
        {
            BattleWeatherType.Cloudy => new Color(0.34f, 0.43f, 0.52f, 0.18f),
            BattleWeatherType.Rain => new Color(0.18f, 0.28f, 0.42f, 0.30f),
            _ => new Color(1.0f, 1.0f, 1.0f, 0.0f)
        };
    }

    private static float PositiveModulo(float value, float modulus)
    {
        if (modulus <= 0.0f)
        {
            return 0.0f;
        }

        var result = value % modulus;
        return result < 0.0f ? result + modulus : result;
    }

    private string FormatBattleWindDirection(BattleWindDirection direction)
    {
        return direction switch
        {
            BattleWindDirection.NorthWest => BattleText("ui.battle.wind_north_west", "NorthWest"),
            BattleWindDirection.SouthEast => BattleText("ui.battle.wind_south_east", "SouthEast"),
            BattleWindDirection.SouthWest => BattleText("ui.battle.wind_south_west", "SouthWest"),
            _ => BattleText("ui.battle.wind_north_east", "NorthEast")
        };
    }

    private string FormatBattleWindDirectionShort(BattleWindDirection direction)
    {
        return direction switch
        {
            BattleWindDirection.NorthWest => BattleText("ui.battle.wind_north_west_short", "NW"),
            BattleWindDirection.SouthEast => BattleText("ui.battle.wind_south_east_short", "SE"),
            BattleWindDirection.SouthWest => BattleText("ui.battle.wind_south_west_short", "SW"),
            _ => BattleText("ui.battle.wind_north_east_short", "NE")
        };
    }

    private string FormatBattleWindPower(BattleWindPower power)
    {
        return power switch
        {
            BattleWindPower.Calm => BattleText("ui.battle.wind_power_calm", "Calm"),
            BattleWindPower.Strong => BattleText("ui.battle.wind_power_strong", "Strong"),
            _ => BattleText("ui.battle.wind_power_breeze", "Breeze")
        };
    }

    private static Vector2I GetWindGridOffset(BattleWindDirection direction)
    {
        return direction switch
        {
            BattleWindDirection.NorthEast => new Vector2I(1, -1),
            BattleWindDirection.NorthWest => new Vector2I(-1, -1),
            BattleWindDirection.SouthWest => new Vector2I(-1, 1),
            _ => new Vector2I(1, 1)
        };
    }

    private void OnWorkButtonPressed()
    {
        BeginWorkerWorkSelection(WorkerWorkAction.General);
    }

    private void OnInstallWoodFenceButtonPressed()
    {
        BeginWorkerWorkSelection(WorkerWorkAction.WoodFence);
    }

    private void OnUninstallWoodFenceButtonPressed()
    {
        BeginWorkerWorkSelection(WorkerWorkAction.WoodFence);
    }

    private void BeginWorkerWorkSelection(WorkerWorkAction workAction)
    {
        if (_selectedUnit?.TroopType != TroopWorker || IsMessed(_selectedUnit) || !_selectedUnitGrid.HasValue)
        {
            return;
        }

        _workerWorkAction = workAction;
        _commandMode = BattleCommandMode.WorkSelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        foreach (var targetGrid in GetOrthogonalNeighbors(_selectedUnitGrid.Value.Grid))
        {
            if (!IsWithinMap(targetGrid))
            {
                continue;
            }

            var targetCell = _mapData?.GetCell(targetGrid.X, targetGrid.Y);
            if (targetCell != null && IsWorkerWorkTarget(targetGrid, targetCell))
            {
                _workableGrids.Add(new BattleGridKey(targetGrid.X, targetGrid.Y, 0));
            }
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private bool IsWorkerWorkTarget(Vector2I targetGrid, BattleCellData cell)
    {
        return IsWorkerWorkTargetForAction(targetGrid, cell, _workerWorkAction);
    }

    private bool IsWorkerWorkTargetForAction(Vector2I targetGrid, BattleCellData cell, WorkerWorkAction workAction)
    {
        if (workAction == WorkerWorkAction.WoodFence)
        {
            return cell.Structure == BattleStructureType.WoodenFence || CanInstallWoodFence(targetGrid, cell);
        }

        return (cell.Terrain == BattleTerrainType.Moat && _mapData?.ScenarioDefinition.ScenarioType == BattleScenarioType.MoatSiegeBattle) ||
               cell.IsBridgeDamaged ||
               (cell.Structure == BattleStructureType.Gate && cell.HasStructureHealth && cell.StructureHealth < cell.StructureMaxHealth) ||
               cell.Structure == BattleStructureType.Trap;
    }

    private bool CanInstallWoodFence(Vector2I targetGrid, BattleCellData cell)
    {
        return cell.Structure == BattleStructureType.None &&
               cell.Terrain is not (BattleTerrainType.Moat or BattleTerrainType.Bridge or BattleTerrainType.WallWalk) &&
               !HasBlockingOccupant(new BattleGridKey(targetGrid.X, targetGrid.Y, 0));
    }

    private bool HasWorkerWorkTarget(WorkerWorkAction workAction)
    {
        return _selectedUnitGrid.HasValue &&
               _mapData != null &&
               _selectedUnit != null &&
               !IsMessed(_selectedUnit) &&
               GetOrthogonalNeighbors(_selectedUnitGrid.Value.Grid)
                   .Where(IsWithinMap)
                   .Any(grid => IsWorkerWorkTargetForAction(grid, _mapData.GetCell(grid.X, grid.Y), workAction));
    }

    private void OnOpenGateButtonPressed()
    {
        if (!TryGetSwitchableGate(out var gateGrid) || _mapData == null)
        {
            return;
        }

        ToggleGateGroup(GetConnectedGateGroup(gateGrid));

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private void OpenGateGroup(IEnumerable<Vector2I> gateGroup)
    {
        if (_mapData == null)
        {
            return;
        }

        foreach (var groupGateGrid in gateGroup)
        {
            var gateCell = _mapData.GetCell(groupGateGrid.X, groupGateGrid.Y);
            gateCell.IsGateOpen = true;
            if (_castleLayer != null)
            {
                BattleTileMapBuilder.SetCastleGateVisual(_castleLayer, groupGateGrid, isOpen: true);
            }

            RefreshCastleDepthVisual(groupGateGrid);
        }

        RefreshOccludedUnitSilhouettes();
    }

    private void ToggleGateGroup(IEnumerable<Vector2I> gateGroup)
    {
        if (_mapData == null)
        {
            return;
        }

        var gates = gateGroup.ToList();
        if (gates.Count == 0)
        {
            return;
        }

        var shouldOpen = gates.Any(grid => !_mapData.GetCell(grid.X, grid.Y).IsGateOpen);
        foreach (var groupGateGrid in gates)
        {
            var gateCell = _mapData.GetCell(groupGateGrid.X, groupGateGrid.Y);
            if (gateCell.IsBroken)
            {
                gateCell.IsGateOpen = true;
            }
            else
            {
                gateCell.IsGateOpen = shouldOpen;
            }

            if (_castleLayer != null)
            {
                BattleTileMapBuilder.SetCastleGateVisual(_castleLayer, groupGateGrid, isOpen: gateCell.IsGateOpen);
            }

            RefreshCastleDepthVisual(groupGateGrid);
        }

        RefreshOccludedUnitSilhouettes();
    }

    private bool TryGetSwitchableGate(out Vector2I gateGrid)
    {
        gateGrid = default;
        if (_mapData == null ||
            _selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            _selectedUnit.Category != CategoryUnit)
        {
            return false;
        }

        var unitGrid = _selectedUnitGrid.Value;
        if (!IsWithinMap(unitGrid.Grid))
        {
            return false;
        }

        var unitCell = _mapData.GetCell(unitGrid.X, unitGrid.Y);
        if (unitGrid.Level == 0 && unitCell.Structure == BattleStructureType.Gate && !unitCell.IsBroken)
        {
            gateGrid = unitGrid.Grid;
            return true;
        }

        return false;
    }

    private List<Vector2I> GetConnectedGateGroup(Vector2I startGateGrid)
    {
        var group = new List<Vector2I>();
        if (_mapData == null || !IsWithinMap(startGateGrid))
        {
            return group;
        }

        var startCell = _mapData.GetCell(startGateGrid.X, startGateGrid.Y);
        if (startCell.Structure != BattleStructureType.Gate)
        {
            return group;
        }

        var visited = new HashSet<Vector2I> { startGateGrid };
        var frontier = new Queue<Vector2I>();
        frontier.Enqueue(startGateGrid);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            group.Add(current);

            foreach (var neighbor in GetOrthogonalNeighbors(current))
            {
                if (!IsWithinMap(neighbor) || !visited.Add(neighbor))
                {
                    continue;
                }

                var neighborCell = _mapData.GetCell(neighbor.X, neighbor.Y);
                if (neighborCell.Structure != BattleStructureType.Gate)
                {
                    continue;
                }

                frontier.Enqueue(neighbor);
            }
        }

        return group;
    }

    private IEnumerable<BattleGridKey> CalculateAttackableGrids(BattleGridKey startGrid, BattleOccupantInfo attacker)
    {
        if (!CanUseNormalAttackWithCurrentAmmo(attacker))
        {
            yield break;
        }

        var attackRange = GetEffectiveAttackRange(attacker);
        if (attackRange <= 0)
        {
            yield break;
        }

        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var grid = new Vector2I(x, y);
                foreach (var gridKey in GetAttackCandidateGridKeys(startGrid, attacker, grid))
                {
                    var distance = Mathf.Abs(gridKey.X - startGrid.X) + Mathf.Abs(gridKey.Y - startGrid.Y);
                    if (distance > 0 &&
                        distance <= attackRange &&
                        IsAttackLevelCompatible(startGrid, attacker, gridKey) &&
                        !IsClosedGateExteriorAttackBlocked(startGrid, gridKey))
                    {
                        yield return gridKey;
                    }
                }
            }
        }
    }

    private int GetEffectiveAttackRange(BattleOccupantInfo attacker)
    {
        if (CanUseAmmoDepletedWeakAttack(attacker))
        {
            return 1;
        }

        if (GetCurrentBattleTimeOfDay() == BattleTimeOfDay.Night && IsRangedBattleAttacker(attacker))
        {
            return Mathf.Max(1, attacker.AttackRange - 1);
        }

        return attacker.AttackRange;
    }

    private static bool IsRangedBattleAttacker(BattleOccupantInfo attacker)
    {
        if (attacker.Category == CategoryUnit)
        {
            return attacker.TroopType is TroopArcher or TroopCrossbow;
        }

        return attacker.Category == CategorySiegeEngine && attacker.TroopType == TroopCatapult;
    }

    private bool IsClosedGateExteriorAttackBlocked(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (_mapData == null || sourceGrid.Level != 0 || !IsWithinMap(sourceGrid.Grid))
        {
            return false;
        }

        var sourceCell = _mapData.GetCell(sourceGrid.X, sourceGrid.Y);
        if (sourceCell.Structure != BattleStructureType.Gate || sourceCell.IsGateOpen || sourceCell.IsBroken)
        {
            return false;
        }

        return targetGrid.Level == 0 && !IsInsideCityGroundGrid(targetGrid.Grid);
    }

    private IEnumerable<BattleGridKey> GetAttackCandidateGridKeys(BattleGridKey sourceGrid, BattleOccupantInfo attacker, Vector2I targetGrid)
    {
        if (_mapData == null || !IsWithinMap(targetGrid))
        {
            yield break;
        }

        if (IsCrossLevelRangedAttacker(attacker, sourceGrid))
        {
            if (IsWallTopGrid(targetGrid))
            {
                yield return ToWallWalkGridKey(targetGrid);
                if (IsAttackableStructureGroundGrid(targetGrid) || IsOpenGateGroundGrid(targetGrid))
                {
                    yield return ToGroundGridKey(targetGrid);
                }
            }
            else
            {
                yield return ToGroundGridKey(targetGrid);
            }

            yield break;
        }

        if (sourceGrid.Level == 2)
        {
            if (IsWallTopGrid(targetGrid))
            {
                yield return ToWallWalkGridKey(targetGrid);
                if (IsAttackableStructureGroundGrid(targetGrid) || IsOpenGateGroundGrid(targetGrid))
                {
                    yield return ToGroundGridKey(targetGrid);
                }
            }

            yield break;
        }

        yield return ToGroundGridKey(targetGrid);
    }

    private bool IsAttackLevelCompatible(BattleGridKey sourceGrid, BattleOccupantInfo attacker, BattleGridKey targetGrid)
    {
        if (_mapData == null || !IsWithinMap(targetGrid.Grid))
        {
            return false;
        }

        if (sourceGrid.Level == targetGrid.Level)
        {
            return targetGrid.Level != 2 || IsWallTopGrid(targetGrid.Grid);
        }

        if (!IsCrossLevelRangedAttacker(attacker, sourceGrid))
        {
            return false;
        }

        return (sourceGrid.Level, targetGrid.Level) is (0, 2) or (2, 0) &&
               (targetGrid.Level != 2 || IsWallTopGrid(targetGrid.Grid)) &&
               (targetGrid.Level != 0 ||
                !IsWallTopGrid(targetGrid.Grid) ||
                IsAttackableStructureGroundGrid(targetGrid.Grid) ||
                IsOpenGateGroundGrid(targetGrid.Grid));
    }

    private bool IsAttackableStructureGroundGrid(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return ((cell.Structure == BattleStructureType.Gate || cell.Structure == BattleStructureType.WoodenFence) &&
                cell.HasStructureHealth &&
                !cell.IsBroken) ||
               cell.HasBridgeHealth;
    }

    private bool IsOpenGateGroundGrid(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return cell.Structure == BattleStructureType.Gate && (cell.IsGateOpen || cell.IsBroken);
    }

    private static bool IsCrossLevelRangedAttacker(BattleOccupantInfo attacker, BattleGridKey sourceGrid)
    {
        if (attacker.Category == CategoryUnit)
        {
            return attacker.TroopType is TroopArcher or TroopCrossbow;
        }

        return attacker.Category == CategorySiegeEngine &&
               attacker.TroopType == TroopCatapult &&
               sourceGrid.Level == 0;
    }

    private void ShowCommandMenu(Vector2 screenPosition)
    {
        if (_commandMenu == null)
        {
            return;
        }

        var canCommandSelectedUnit = _selectedUnit != null && IsCurrentTurnPiece(_selectedUnit);

        if (_unitMenuInfoLabel != null)
        {
            if (_selectedUnit != null)
            {
                var strengthText = _selectedUnit.Category == CategorySiegeEngine
                    ? BattleFormat("ui.battle.menu_hp", "HP: {0}/{1}", _selectedUnit.HitPoints, _selectedUnit.MaxHitPoints)
                    : BattleFormat("ui.battle.menu_active_wounded", "Active: {0:N0}\nWounded: {1}", _selectedUnit.TroopCount, FormatWoundedTroops(_selectedUnit));
                var officerText = string.IsNullOrWhiteSpace(_selectedUnit.OfficerName)
                    ? "-"
                    : _selectedUnit.OfficerName;
                _unitMenuInfoLabel.Text =
                    BattleFormat("ui.battle.menu_team", "Team: {0}", FormatTeamName(_selectedUnit.TeamName)) + "\n" +
                    BattleFormat("ui.battle.menu_officer", "Officer: {0}", officerText) + "\n" +
                    BattleFormat("ui.battle.menu_type", "Type: {0}", FormatTroopType(_selectedUnit.TroopType)) + "\n" +
                    BattleFormat("ui.battle.menu_status", "Status: {0}", FormatBattleStatus(_selectedUnit)) + "\n" +
                    BattleFormat("ui.battle.menu_command", "Command: {0}", canCommandSelectedUnit ? BattleText("ui.battle.ready", "Ready") : BattleFormat("ui.battle.not_acting_side", "Not Acting Side ({0})", FormatTeamName(GetCurrentTurnSideName()))) + "\n" +
                    BattleFormat("ui.battle.menu_morale", "Morale: {0}", FormatMorale(_selectedUnit)) + "\n" +
                    BattleFormat("ui.battle.menu_ammo", "Ammo: {0}", FormatWeaponAmmo(_selectedUnit)) + "\n" +
                    strengthText;
            }
            else
            {
                _unitMenuInfoLabel.Text = BuildEmptyUnitMenuInfoText();
            }
        }

        if (_openGateButton != null)
        {
            if (canCommandSelectedUnit && TryGetSwitchableGate(out var switchGateGrid) && _mapData != null)
            {
                var switchGateCell = _mapData.GetCell(switchGateGrid.X, switchGateGrid.Y);
                _openGateButton.Text = switchGateCell.IsGateOpen
                    ? BattleText("ui.battle.close_gate", "Close Gate")
                    : BattleText("ui.battle.open_gate", "Open Gate");
                _openGateButton.Visible = true;
            }
            else
            {
                _openGateButton.Visible = false;
            }
        }

        var canUseWallTopAttack = TryGetWallTopAttackGrid(out _);
        if (_dropStoneButton != null)
        {
            var canDropStone = canCommandSelectedUnit &&
                               _selectedUnitGrid?.Level == 2 &&
                               IsWallTopGrid(_selectedUnitGrid.Value.Grid) &&
                               canUseWallTopAttack &&
                               GetWallTopAttackUsesRemaining(isDropStone: true) > 0;
            _dropStoneButton.Visible = canDropStone;
            _dropStoneButton.Text = BattleFormat("ui.battle.drop_stone_count", "Drop Stone ({0})", GetWallTopAttackUsesRemaining(isDropStone: true));
            _dropStoneButton.Disabled = !canDropStone;
        }

        if (_pourOilButton != null)
        {
            var canPourOil = canCommandSelectedUnit &&
                             _selectedUnitGrid?.Level == 2 &&
                             IsWallTopGrid(_selectedUnitGrid.Value.Grid) &&
                             canUseWallTopAttack &&
                             GetWallTopAttackUsesRemaining(isDropStone: false) > 0;
            _pourOilButton.Visible = canPourOil;
            _pourOilButton.Text = BattleFormat("ui.battle.pour_oil_count", "Pour Oil ({0})", GetWallTopAttackUsesRemaining(isDropStone: false));
            _pourOilButton.Disabled = !canPourOil;
        }

        if (_workButton != null)
        {
            var hasBridgeWorkTarget = canCommandSelectedUnit &&
                                      _selectedUnit?.TroopType == TroopWorker &&
                                      HasWorkerWorkTarget(WorkerWorkAction.General);
            _workButton.Visible = hasBridgeWorkTarget;
            _workButton.Text = BattleText("ui.battle.bridge", "Bridge");
            _workButton.Disabled = !hasBridgeWorkTarget;
        }

        if (_installWoodFenceButton != null)
        {
            var hasWoodFenceWorkTarget = canCommandSelectedUnit &&
                                         _selectedUnit?.TroopType == TroopWorker &&
                                         HasWorkerWorkTarget(WorkerWorkAction.WoodFence);
            _installWoodFenceButton.Visible = hasWoodFenceWorkTarget;
            _installWoodFenceButton.Text = BattleText("ui.battle.wood_fence", "Wood Fence");
            _installWoodFenceButton.Disabled = !hasWoodFenceWorkTarget;
        }

        if (_uninstallWoodFenceButton != null)
        {
            _uninstallWoodFenceButton.Visible = false;
            _uninstallWoodFenceButton.Disabled = false;
        }

        if (_supplyButton != null)
        {
            var hasSupplyTargets = canCommandSelectedUnit &&
                                   _selectedUnit?.TroopType == TroopSupplyCart &&
                                   HasSupplyTargets();
            _supplyButton.Visible = hasSupplyTargets;
            _supplyButton.Text = BattleFormat("ui.battle.recovery_repair", "Recovery / Repair (+{0} Morale, +{1} HP)", SupplyCartMoraleRestore, SupplyCartRepairAmount);
            _supplyButton.Disabled = !hasSupplyTargets;
        }

        if (_resupplyWeaponButton != null)
        {
            var hasWeaponResupplyTargets = canCommandSelectedUnit &&
                                           _selectedUnit?.TroopType == TroopSupplyCart &&
                                           HasWeaponResupplyTargets();
            _resupplyWeaponButton.Visible = hasWeaponResupplyTargets;
            _resupplyWeaponButton.Text = BattleText("ui.battle.resupply_weapon", "Resupply Weapon");
            _resupplyWeaponButton.Disabled = !hasWeaponResupplyTargets;
        }

        if (_captureSupplyCartButton != null)
        {
            var hasCaptureTarget = canCommandSelectedUnit && TryGetCapturableSupplyCartTarget(out _, out _);
            _captureSupplyCartButton.Visible = hasCaptureTarget;
            _captureSupplyCartButton.Text = BattleText("ui.battle.capture_cart", "Capture Cart");
            _captureSupplyCartButton.Disabled = !hasCaptureTarget;
        }

        if (_hireOfficerButton != null)
        {
            var hasHireTarget = canCommandSelectedUnit &&
                                _selectedUnitGrid.HasValue &&
                                CalculateHireOfficerTargetGrids(_selectedUnitGrid.Value).Any();
            var hireCost = _selectedUnit == null ? 0 : GetHireOfficerGoldCost(_selectedUnit);
            var canHireOfficer = hasHireTarget &&
                                 _selectedUnit != null &&
                                 GetTeamGold(_selectedUnit.TeamName) >= hireCost;
            _hireOfficerButton.Visible = canHireOfficer;
            _hireOfficerButton.Text = BattleText("ui.battle.hire_officer", "Hire Officer");
            _hireOfficerButton.Disabled = !canHireOfficer;
        }

        if (_attackButton != null)
        {
            var hasAttackTarget = canCommandSelectedUnit &&
                                  _selectedUnit != null &&
                                  _selectedUnitGrid.HasValue &&
                                  CanUseAttackCommand(_selectedUnit) &&
                                  CalculateAttackableGrids(_selectedUnitGrid.Value, _selectedUnit).Any();
            _attackButton.Visible = hasAttackTarget;
            _attackButton.Text = _selectedUnit != null && CanUseAmmoDepletedWeakAttack(_selectedUnit)
                ? BattleText("ui.battle.attack_weak_close", "Attack (Weak Close, Ammo 0)")
                : _selectedUnit?.MaxWeaponAmmo.HasValue == true
                ? BattleFormat("ui.battle.attack_ammo", "Attack (Ammo {0})", FormatWeaponAmmo(_selectedUnit))
                : BattleText("ui.battle.attack", "Attack");
            _attackButton.Disabled = !hasAttackTarget;
        }

        if (_strategyButton != null)
        {
            var strategyAction = canCommandSelectedUnit && _selectedUnit != null ? ResolveStrategyAction(_selectedUnit) : BattleStrategyAction.None;
            var hasStrategyTarget = strategyAction != BattleStrategyAction.None &&
                                    _selectedUnit != null &&
                                    _selectedUnitGrid.HasValue &&
                                    (strategyAction == BattleStrategyAction.Fire
                                        ? CalculateFireStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit).Any()
                                        : CalculateMentalStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit).Any());
            _strategyButton.Visible = hasStrategyTarget;
            _strategyButton.Text = strategyAction == BattleStrategyAction.Fire && _selectedUnit?.MaxWeaponAmmo.HasValue == true
                ? BattleFormat("ui.battle.strategy_fire_ammo", "Strategy (Fire, Ammo {0})", FormatWeaponAmmo(_selectedUnit))
                : strategyAction == BattleStrategyAction.Fire
                    ? BattleText("ui.battle.strategy_fire", "Strategy (Fire)")
                    : BattleFormat("ui.battle.strategy_mess_calm", "Strategy (Mess / Calm, Range {0})", MessStrategyRange);
            _strategyButton.Disabled = !hasStrategyTarget;
        }

        if (_unionAttackButton != null)
        {
            if (canCommandSelectedUnit && TryGetBestUnionAttackCandidate(out var unionAttackCandidate))
            {
                _unionAttackButton.Visible = true;
                _unionAttackButton.Text = BattleFormat("ui.battle.union_attack_count", "Union Attack ({0})", unionAttackCandidate.Participants.Count);
                _unionAttackButton.Disabled = false;
            }
            else
            {
                _unionAttackButton.Visible = false;
                _unionAttackButton.Disabled = false;
                _unionAttackButton.Text = BattleText("ui.battle.union_attack", "Union Attack");
            }
        }

        if (_chargeButton != null)
        {
            var hasChargeTarget = canCommandSelectedUnit &&
                                  _selectedUnit != null &&
                                  _selectedUnitGrid.HasValue &&
                                  CanUseChargeCommand(_selectedUnit) &&
                                  CalculateChargeTargetGrids(_selectedUnitGrid.Value, _selectedUnit).Any();
            _chargeButton.Visible = canCommandSelectedUnit &&
                                    _selectedUnit != null &&
                                    _selectedUnit.TroopType == TroopCavalry &&
                                    hasChargeTarget;
            _chargeButton.Disabled = !hasChargeTarget;
            _chargeButton.Text = BattleText("ui.battle.charge", "Charge");
        }

        if (_duelButton != null)
        {
            var hasDuelTarget = canCommandSelectedUnit &&
                                _selectedUnit != null &&
                                _selectedUnitGrid.HasValue &&
                                CalculateDuelTargetGrids(_selectedUnitGrid.Value, _selectedUnit).Any();
            _duelButton.Visible = hasDuelTarget;
            _duelButton.Disabled = !hasDuelTarget;
            _duelButton.Text = BattleText("ui.battle.duel", "Duel");
        }

        if (_retreatButton != null)
        {
            _retreatButton.Visible = canCommandSelectedUnit && _selectedUnit != null && IsBattlePiece(_selectedUnit);
            _retreatButton.Disabled = _selectedUnit == null || !IsBattlePiece(_selectedUnit);
            _retreatButton.Text = BattleText("ui.battle.retreat", "Retreat");
        }

        if (_hideButton != null)
        {
            var isHideCandidate = canCommandSelectedUnit && _selectedUnit != null && IsBattlePiece(_selectedUnit);
            var canHide = isHideCandidate && CanHideSelectedUnit();
            _hideButton.Visible = canHide;
            _hideButton.Text = _selectedUnit?.IsHidden == true
                ? BattleText("ui.battle.hidden", "Hidden")
                : BattleText("ui.battle.hide", "Hide");
            _hideButton.Disabled = !canHide;
        }

        if (_moveButton != null)
        {
            var hasMoveTarget = canCommandSelectedUnit &&
                                _selectedUnit != null &&
                                _selectedUnitGrid.HasValue &&
                                IsBattlePiece(_selectedUnit) &&
                                !HasUsedChargeThisTurn(_selectedUnit) &&
                                CalculateReachableGrids(_selectedUnitGrid.Value, GetEffectiveMoveRange(_selectedUnit)).Any();
            _moveButton.Visible = hasMoveTarget;
            _moveButton.Disabled = !hasMoveTarget;
            _moveButton.Text = BattleText("ui.battle.move", "Move");
        }

        UpdateCommandScrollLayout();
        var desiredPosition = screenPosition + new Vector2(12.0f, 12.0f);
        _commandMenu.Position = ClampCommandMenuPosition(desiredPosition);
        _commandMenu.Visible = true;
        ResizeCommandMenuAfterLayout(desiredPosition);
    }

    private void UpdateCommandScrollLayout()
    {
        if (_commandScroll == null)
        {
            return;
        }

        var visibleCommandCount = GetCommandActionButtons().Count(static button => button.Visible);
        var visibleRows = Math.Min(visibleCommandCount, 4);
        var scrollHeight = visibleRows <= 0
            ? 0.0f
            : (visibleRows * 31.0f) + ((visibleRows - 1) * 8.0f);
        _commandScroll.CustomMinimumSize = new Vector2(0.0f, scrollHeight);
        _commandScroll.ScrollVertical = 0;
    }

    private IEnumerable<Button> GetCommandActionButtons()
    {
        foreach (var button in new[]
        {
            _moveButton,
            _attackButton,
            _unionAttackButton,
            _chargeButton,
            _duelButton,
            _retreatButton,
            _hideButton,
            _dropStoneButton,
            _pourOilButton,
            _workButton,
            _installWoodFenceButton,
            _uninstallWoodFenceButton,
            _supplyButton,
            _resupplyWeaponButton,
            _captureSupplyCartButton,
            _hireOfficerButton,
            _strategyButton,
            _openGateButton
        })
        {
            if (button != null)
            {
                yield return button;
            }
        }
    }

    private void HideCommandMenu()
    {
        if (_commandMenu != null)
        {
            _commandMenu.Visible = false;
        }

        _isDraggingCommandMenu = false;

        if (_unitMenuInfoLabel != null)
        {
            _unitMenuInfoLabel.Text = BuildEmptyUnitMenuInfoText();
        }

        if (_openGateButton != null)
        {
            _openGateButton.Visible = false;
        }

        if (_attackButton != null)
        {
            _attackButton.Visible = true;
            _attackButton.Disabled = false;
            _attackButton.Text = BattleText("ui.battle.attack", "Attack");
        }

        if (_moveButton != null)
        {
            _moveButton.Visible = true;
            _moveButton.Disabled = false;
            _moveButton.Text = BattleText("ui.battle.move", "Move");
        }

        if (_dropStoneButton != null)
        {
            _dropStoneButton.Visible = false;
        }

        if (_pourOilButton != null)
        {
            _pourOilButton.Visible = false;
        }

        if (_workButton != null)
        {
            _workButton.Visible = false;
        }

        if (_installWoodFenceButton != null)
        {
            _installWoodFenceButton.Visible = false;
        }

        if (_uninstallWoodFenceButton != null)
        {
            _uninstallWoodFenceButton.Visible = false;
        }

        if (_supplyButton != null)
        {
            _supplyButton.Visible = false;
            _supplyButton.Disabled = false;
            _supplyButton.Text = BattleText("ui.battle.recovery_repair_short", "Recovery / Repair");
        }

        if (_resupplyWeaponButton != null)
        {
            _resupplyWeaponButton.Visible = false;
            _resupplyWeaponButton.Disabled = false;
            _resupplyWeaponButton.Text = BattleText("ui.battle.resupply_weapon", "Resupply Weapon");
        }

        if (_strategyButton != null)
        {
            _strategyButton.Visible = false;
            _strategyButton.Disabled = false;
            _strategyButton.Text = BattleText("ui.battle.strategy", "Strategy");
        }

        if (_unionAttackButton != null)
        {
            _unionAttackButton.Visible = false;
            _unionAttackButton.Disabled = false;
            _unionAttackButton.Text = BattleText("ui.battle.union_attack", "Union Attack");
        }

        if (_chargeButton != null)
        {
            _chargeButton.Visible = false;
            _chargeButton.Disabled = false;
            _chargeButton.Text = BattleText("ui.battle.charge", "Charge");
        }

        if (_duelButton != null)
        {
            _duelButton.Visible = false;
            _duelButton.Disabled = false;
            _duelButton.Text = BattleText("ui.battle.duel", "Duel");
        }

        if (_retreatButton != null)
        {
            _retreatButton.Visible = false;
            _retreatButton.Disabled = false;
            _retreatButton.Text = BattleText("ui.battle.retreat", "Retreat");
        }

        if (_hideButton != null)
        {
            _hideButton.Visible = false;
            _hideButton.Disabled = false;
            _hideButton.Text = BattleText("ui.battle.hide", "Hide");
        }
    }

    private void CancelCommandAction(bool clearSelection)
    {
        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _chargeTargetGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();

        if (clearSelection)
        {
            _selectedUnit = null;
            _selectedUnitGrid = null;
            _selectedGridKey = null;
        }
    }

    private void ResolveBattleStatusAtTurnStart(string actingSideName)
    {
        foreach (var (grid, occupants) in _occupantsByGrid.ToList())
        {
            foreach (var unit in occupants.ToList())
            {
                if (unit.TeamName != actingSideName || unit.Category != CategoryUnit)
                {
                    continue;
                }

                var currentUnit = unit;
                if (IsMessed(currentUnit))
                {
                    currentUnit = ApplyMessDesertion(grid, currentUnit);
                }

                if (currentUnit.HitPoints <= 0)
                {
                    continue;
                }

                TryApplyLowMoraleMessAtTurnStart(grid, currentUnit);
            }
        }
    }

    private void ResolveBattleStatusAtTurnEnd(string endingSideName)
    {
        foreach (var (grid, occupants) in _occupantsByGrid.ToList())
        {
            foreach (var unit in occupants.ToList())
            {
                if (unit.TeamName != endingSideName || !IsMessed(unit))
                {
                    continue;
                }

                var updatedUnit = unit with { MessTurns = Math.Max(0, unit.MessTurns - 1) };
                ReplaceOccupantAtGrid(grid, unit, updatedUnit);
                if (_selectedUnit == unit)
                {
                    _selectedUnit = updatedUnit;
                }
            }
        }
    }

    private BattleOccupantInfo ApplyMessDesertion(BattleGridKey grid, BattleOccupantInfo unit)
    {
        var deserters = Math.Max(1, Mathf.FloorToInt(unit.TroopCount * (MessDesertionPercent / 100.0f)));
        deserters = Math.Min(deserters, unit.TroopCount);
        if (deserters <= 0)
        {
            return unit;
        }

        var updatedUnit = unit with
        {
            TroopCount = unit.TroopCount - deserters,
            HitPoints = Math.Max(0, unit.HitPoints - deserters)
        };
        UpdateMarkerStrengthBar(updatedUnit);
        ReplaceOccupantAtGrid(grid, unit, updatedUnit);
        ApplyTeamTroopLoss(unit, deserters);
        ShowDamagePopup(grid, deserters);
        AppendBattleLog(unit, "Status", $"{FormatLogUnit(unit)} is in Mess: {deserters:N0} troop(s) leave battle");
        if (_selectedUnit == unit)
        {
            _selectedUnit = updatedUnit;
        }

        if (updatedUnit.HitPoints <= 0)
        {
            DestroyOccupantAfterDelay(grid, updatedUnit, 0.0);
        }

        return updatedUnit;
    }

    private bool TryApplyLowMoraleMessAtTurnStart(BattleGridKey grid, BattleOccupantInfo unit)
    {
        if (IsMessed(unit) ||
            unit.Morale == null ||
            unit.Morale.Value > MessMoraleThreshold ||
            !TryGetCurrentOccupantAtGrid(grid, unit, out var currentUnit))
        {
            return false;
        }

        unit = currentUnit;
        var morale = unit.Morale.GetValueOrDefault(DefaultUnitMorale);
        if (morale > MessMoraleThreshold)
        {
            return false;
        }

        var chance = Mathf.Clamp(45.0f + (MessMoraleThreshold - morale) * 3.0f, 45.0f, 75.0f);
        if (GD.Randf() * 100.0f > chance)
        {
            return false;
        }

        var updatedUnit = unit with { MessTurns = Math.Max(unit.MessTurns, 1) };
        ReplaceOccupantAtGrid(grid, unit, updatedUnit);
        if (_selectedUnit == unit)
        {
            _selectedUnit = updatedUnit;
        }

        AppendBattleLog(unit, "Status", $"{FormatLogUnit(unit)} morale is too low and falls into Mess ({chance:N0}%)");
        return true;
    }

    private void OnEndTurnButtonPressed()
    {
        if (_isBattleFinished)
        {
            return;
        }

        var endingSideName = GetCurrentTurnSideName();
        CancelCommandAction(clearSelection: true);
        ResolveBattleFireAtTurnEnd();
        ResolveBattleStatusAtTurnEnd(endingSideName);
        _strategyUsedByMarkerThisTurn.Clear();
        _supplyUsedByMarkerThisTurn.Clear();
        _chargeUsedByMarkerThisTurn.Clear();

        if (_currentTurnSide == BattleTurnSide.TeamA)
        {
            _currentTurnSide = BattleTurnSide.TeamB;
        }
        else
        {
            _currentTurnSide = BattleTurnSide.TeamA;
            ResolveDailyBattleSupply();
            _turnNumber++;
        }

        var actingSideName = GetCurrentTurnSideName();
        ResolveBattleStatusAtTurnStart(actingSideName);
        AppendBattleLog(actingSideName, "Turn", $"Acting side: {actingSideName}");
        ConfigureHud();
        RefreshBattleLogPanel();
        RefreshCoordinateLabel();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private void OnWeatherButtonPressed()
    {
        _currentBattleWeather = GetNextBattleWeather(GetCurrentBattleWeather());
        ConfigureHud();
        ApplyWeatherVisual(animate: true);
        RefreshHighlights();
    }

    private void OnTimeButtonPressed()
    {
        _currentBattleTimeOfDay = GetNextBattleTimeOfDay(GetCurrentBattleTimeOfDay());
        ConfigureHud();
        ApplyTimeOfDayVisual(animate: true);
        RefreshHighlights();
    }

    private void OnWindButtonPressed()
    {
        _currentBattleWindDirection = GetNextBattleWindDirection(GetCurrentBattleWindDirection());
        ConfigureHud();
    }

    private void OnWindPowerButtonPressed()
    {
        _currentBattleWindPower = GetNextBattleWindPower(GetCurrentBattleWindPower());
        ConfigureHud();
    }

    private string FormatCommandMode(BattleCommandMode commandMode)
    {
        return commandMode switch
        {
            BattleCommandMode.MoveSelect => BattleText("ui.battle.command_move_select", "Select Move Target"),
            BattleCommandMode.AttackSelect => BattleText("ui.battle.command_attack_select", "Select Attack Target"),
            BattleCommandMode.WorkSelect => BattleText("ui.battle.command_work_select", "Select Work Target"),
            BattleCommandMode.StrategySelect => BattleText("ui.battle.command_strategy_select", "Strategy Pending"),
            BattleCommandMode.DuelSelect => BattleText("ui.battle.command_duel_select", "Select Duel Target"),
            BattleCommandMode.ChargeSelect => BattleText("ui.battle.command_charge_select", "Select Charge Target"),
            BattleCommandMode.HireOfficerSelect => BattleText("ui.battle.command_hire_officer_select", "Select Hire Target"),
            BattleCommandMode.AwaitingCommand => BattleText("ui.battle.command_awaiting", "Awaiting Command"),
            _ => BattleText("ui.battle.none", "None")
        };
    }

    private string GetCurrentTurnSideName()
    {
        return _currentTurnSide == BattleTurnSide.TeamA ? "Team A / Attacker" : "Team B / Defender";
    }

    private string FormatTeamName(string teamName)
    {
        if (teamName.Contains("Attacker", StringComparison.OrdinalIgnoreCase))
        {
            return BattleText("ui.battle.team_attacker", "Team A / Attacker");
        }

        if (teamName.Contains("Defender", StringComparison.OrdinalIgnoreCase))
        {
            return BattleText("ui.battle.team_defender", "Team B / Defender");
        }

        return teamName;
    }

    private bool IsCurrentTurnPiece(BattleOccupantInfo occupant)
    {
        return occupant.TeamName == GetCurrentTurnSideName();
    }

    private bool CanTraverseBlockedCell(BattleGridKey grid, BattleCellData cell, bool usesLadderBridge = false)
    {
        _ = grid;

        if (grid.Level == 2 && IsWallTopGrid(grid.Grid))
        {
            return true;
        }

        if (!cell.IsBlockingStructure)
        {
            return true;
        }

        if (_selectedUnit == null)
        {
            return false;
        }

        if (cell.Structure == BattleStructureType.Gate && _selectedUnit.Category == CategoryUnit)
        {
            return IsDefenderPiece(_selectedUnit) || grid.Level == 0;
        }

        if (usesLadderBridge && grid.Level == 2 && IsWallTopGrid(grid.Grid) && CanUseCarLadderBridge(_selectedUnit))
        {
            return true;
        }

        return false;
    }

    private bool CanEnterCell(BattleGridKey sourceGrid, BattleGridKey destinationGrid, BattleCellData cell, bool usesLadderBridge = false)
    {
        _ = destinationGrid;

        if (_selectedUnit == null)
        {
            return false;
        }

        if (_selectedUnit.Category == CategorySiegeEngine)
        {
            return CanSiegeEngineEnterCell(sourceGrid, destinationGrid);
        }

        if (destinationGrid.Level == 2 && _selectedUnit.TroopType == TroopCavalry)
        {
            return false;
        }

        if (_selectedUnit.TroopType == TroopCavalry &&
            cell.Terrain == BattleTerrainType.Forest)
        {
            return false;
        }

        if (IsClosedGateGroundMoveBlocked(sourceGrid, destinationGrid))
        {
            return false;
        }

        var sourceCell = _mapData != null && IsWithinMap(sourceGrid.Grid)
            ? _mapData.GetCell(sourceGrid.X, sourceGrid.Y)
            : null;
        var sourceIsWallWalk = sourceGrid.Level == 2;
        var sourceIsCourtyard = sourceCell?.Terrain == BattleTerrainType.Courtyard &&
                                IsInsideCityGroundGrid(sourceGrid.Grid);
        if (destinationGrid.Level == 2 &&
            IsAttackerPiece(_selectedUnit) &&
            !sourceIsWallWalk &&
            !sourceIsCourtyard &&
            !(usesLadderBridge && CanUseCarLadderBridge(_selectedUnit)))
        {
            return false;
        }

        return true;
    }

    private bool IsClosedGateGroundMoveBlocked(BattleGridKey sourceGrid, BattleGridKey destinationGrid)
    {
        if (_mapData == null ||
            sourceGrid.Level != 0 ||
            destinationGrid.Level != 0 ||
            sourceGrid.Grid == destinationGrid.Grid)
        {
            return false;
        }

        var sourceIsGate = IsGateGrid(sourceGrid.Grid);
        var destinationIsGate = IsGateGrid(destinationGrid.Grid);
        if (sourceIsGate == destinationIsGate)
        {
            return false;
        }

        var gateGrid = sourceIsGate ? sourceGrid.Grid : destinationGrid.Grid;
        var otherGrid = sourceIsGate ? destinationGrid.Grid : sourceGrid.Grid;
        var gateCell = _mapData.GetCell(gateGrid.X, gateGrid.Y);
        if (gateCell.IsGateOpen || gateCell.IsBroken)
        {
            return false;
        }

        return !IsInsideCityGroundGrid(otherGrid);
    }

    private bool CanSiegeEngineEnterCell(BattleGridKey sourceGrid, BattleGridKey destinationGrid)
    {
        if (destinationGrid.Level != 0)
        {
            return false;
        }

        if (!IsInsideCityGroundGrid(destinationGrid.Grid))
        {
            return true;
        }

        return IsGateGroundPassage(sourceGrid.Grid) || IsInsideCityGroundGrid(sourceGrid.Grid);
    }

    private bool IsInsideCityGroundGrid(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return cell.Terrain == BattleTerrainType.Courtyard && !IsWallTopGrid(grid);
    }

    private static bool IsCellBlockingMovement(BattleCellData cell)
    {
        return cell.IsBlockingStructure;
    }

    private static bool IsAttackerPiece(BattleOccupantInfo occupant)
    {
        return occupant.TeamName.Contains("Attacker");
    }

    private static bool IsDefenderPiece(BattleOccupantInfo occupant)
    {
        return occupant.TeamName.Contains("Defender");
    }

    private Vector2 ClampCommandMenuPosition(Vector2 desiredPosition)
    {
        if (_commandMenu == null)
        {
            return desiredPosition;
        }

        var viewportSize = GetViewportRect().Size;
        var menuSize = _commandMenu.Size;
        var maxX = Mathf.Max(0.0f, viewportSize.X - menuSize.X);
        var maxY = Mathf.Max(0.0f, viewportSize.Y - menuSize.Y);
        return new Vector2(
            Mathf.Clamp(desiredPosition.X, 0.0f, maxX),
            Mathf.Clamp(desiredPosition.Y, 0.0f, maxY));
    }

    private void OnCommandMenuTitleGuiInput(InputEvent @event)
    {
        if (_commandMenu == null)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                if (mouseButton.Pressed)
                {
                    _isDraggingCommandMenu = true;
                    _commandMenuDragOffset = mouseButton.GlobalPosition - _commandMenu.Position;
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    _isDraggingCommandMenu = false;
                }

                break;
            case InputEventMouseMotion mouseMotion when _isDraggingCommandMenu:
                _commandMenu.Position = ClampCommandMenuPosition(mouseMotion.GlobalPosition - _commandMenuDragOffset);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private async void ResizeCommandMenuAfterLayout(Vector2 desiredPosition)
    {
        if (_commandMenu == null || !_commandMenu.Visible)
        {
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (_commandMenu == null || !_commandMenu.Visible)
        {
            return;
        }

        _commandMenu.ResetSize();
        var minimumSize = _commandMenu.GetCombinedMinimumSize();
        _commandMenu.Size = new Vector2(
            Mathf.Max(_commandMenu.CustomMinimumSize.X, minimumSize.X),
            Mathf.Max(_commandMenu.CustomMinimumSize.Y, minimumSize.Y));
        _commandMenu.Position = ClampCommandMenuPosition(desiredPosition);
    }

    private string FormatTerrain(BattleTerrainType terrain)
    {
        return terrain switch
        {
            BattleTerrainType.Road => BattleText("ui.battle.terrain_road", "Road"),
            BattleTerrainType.Courtyard => BattleText("ui.battle.terrain_courtyard", "Courtyard"),
            BattleTerrainType.Forest => BattleText("ui.battle.terrain_forest", "Forest"),
            BattleTerrainType.WallWalk => BattleText("ui.battle.terrain_wall_walk", "Wall Top"),
            BattleTerrainType.Moat => BattleText("ui.battle.terrain_moat", "Moat"),
            BattleTerrainType.Bridge => BattleText("ui.battle.terrain_bridge", "Bridge"),
            BattleTerrainType.Grass => BattleText("ui.battle.terrain_grass", "Grass"),
            _ => BattleText("ui.battle.terrain_plain", "Plain")
        };
    }

    private string FormatStructure(BattleStructureType structure)
    {
        return structure switch
        {
            BattleStructureType.Wall => BattleText("ui.battle.structure_wall", "Wall"),
            BattleStructureType.Gate => BattleText("ui.battle.structure_gate", "Gate"),
            BattleStructureType.Tower => BattleText("ui.battle.structure_tower", "Tower"),
            BattleStructureType.Building => BattleText("ui.battle.structure_building", "Building"),
            BattleStructureType.WoodenFence => BattleText("ui.battle.structure_wooden_fence", "Wooden Fence"),
            BattleStructureType.Trap => BattleText("ui.battle.structure_trap", "Trap"),
            BattleStructureType.Tree => BattleText("ui.battle.structure_tree", "Tree"),
            BattleStructureType.RockBig => BattleText("ui.battle.structure_large_rock", "Large Rock"),
            BattleStructureType.RockSmall => BattleText("ui.battle.structure_small_rock", "Small Rock"),
            _ => BattleText("ui.battle.none", "None")
        };
    }

    private string FormatDeploymentZone(BattleDeploymentZone zone)
    {
        return zone switch
        {
            BattleDeploymentZone.Attacker => BattleText("ui.battle.deployment_attacker", "Attacker Zone"),
            BattleDeploymentZone.Defender => BattleText("ui.battle.deployment_defender", "Defender Zone"),
            _ => BattleText("ui.battle.none", "None")
        };
    }

    private string FormatStructureFacing(BattleStructureFacing facing)
    {
        return facing switch
        {
            BattleStructureFacing.NorthEast => BattleText("ui.battle.wind_north_east", "NorthEast"),
            BattleStructureFacing.NorthWest => BattleText("ui.battle.wind_north_west", "NorthWest"),
            _ => BattleText("ui.battle.none", "None")
        };
    }

    private string FormatGateSegment(BattleGateSegment gateSegment)
    {
        return gateSegment switch
        {
            BattleGateSegment.Left => BattleText("ui.battle.left", "Left"),
            BattleGateSegment.Right => BattleText("ui.battle.right", "Right"),
            _ => BattleText("ui.battle.none", "None")
        };
    }

    private Rect2 GetMapBounds()
    {
        var topLeft = BattleMapRenderer.GridToWorld(new Vector2I(0, 0));
        var topRight = BattleMapRenderer.GridToWorld(new Vector2I(BattleMapData.Width - 1, 0));
        var bottomLeft = BattleMapRenderer.GridToWorld(new Vector2I(0, BattleMapData.Height - 1));
        var bottomRight = BattleMapRenderer.GridToWorld(new Vector2I(BattleMapData.Width - 1, BattleMapData.Height - 1));

        var minX = Mathf.Min(Mathf.Min(topLeft.X, topRight.X), Mathf.Min(bottomLeft.X, bottomRight.X)) - MapPaddingLeft;
        var maxX = Mathf.Max(Mathf.Max(topLeft.X, topRight.X), Mathf.Max(bottomLeft.X, bottomRight.X)) + MapPaddingRight;
        var minY = Mathf.Min(Mathf.Min(topLeft.Y, topRight.Y), Mathf.Min(bottomLeft.Y, bottomRight.Y)) - MapPaddingTop;
        var maxY = Mathf.Max(Mathf.Max(topLeft.Y, topRight.Y), Mathf.Max(bottomLeft.Y, bottomRight.Y)) + MapPaddingBottom;

        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }

    private Rect2 GetVisibleWorldRect()
    {
        var viewportSize = GetViewportRect().Size;
        var zoom = _camera?.Zoom ?? Vector2.One;
        var center = _camera?.GlobalPosition ?? GetViewportRect().GetCenter();
        var worldSize = new Vector2(viewportSize.X * zoom.X, viewportSize.Y * zoom.Y);

        return new Rect2(center - (worldSize * 0.5f), worldSize);
    }

    private static float ClampAxis(float mapOrigin, float mapMin, float mapMax, float viewMin, float viewMax)
    {
        var mapSpan = mapMax - mapMin;
        var viewSpan = viewMax - viewMin;

        if (mapSpan <= viewSpan)
        {
            return ((viewMin + viewMax) * 0.5f) - ((mapMin + mapMax) * 0.5f);
        }

        var minPosition = viewMax - mapMax;
        var maxPosition = viewMin - mapMin;
        return Mathf.Clamp(mapOrigin, minPosition, maxPosition);
    }

    private sealed record BattleOccupantInfo(
        string DisplayName,
        string Category,
        string ShortLabel,
        string TeamName,
        string OfficerName,
        string TroopType,
        int TroopCount,
        int HitPoints,
        int MaxHitPoints,
        int WoundedTroops,
        int MessTurns,
        bool IsHidden,
        int? Morale,
        int? WeaponAmmo,
        int? MaxWeaponAmmo,
        int MoveRange,
        int AttackRange,
        BattlePieceMarker? Marker,
        BattleSpriteDirection FacingDirection);
    private readonly record struct WallTopAttackAmmo(int DropStoneUses, int PourOilUses);
    private readonly record struct BattleFireState(int RemainingTurns, int BurnTurns);
    private sealed class BattleSaveData
    {
        public int Version { get; set; } = 1;
        public BattleScenarioType ScenarioType { get; set; }
        public bool UseEditorAuthoredLayout { get; set; }
        public int TurnNumber { get; set; }
        public BattleTurnSide CurrentTurnSide { get; set; }
        public BattleTimeOfDay CurrentBattleTimeOfDay { get; set; }
        public BattleWeatherType CurrentBattleWeather { get; set; }
        public BattleWindDirection CurrentBattleWindDirection { get; set; }
        public BattleWindPower CurrentBattleWindPower { get; set; }
        public int TeamAStrategyPlans { get; set; }
        public int TeamBStrategyPlans { get; set; }
        public int TeamAGold { get; set; } = InitialTeamAGold;
        public int TeamAFood { get; set; } = InitialTeamAFood;
        public int TeamBGold { get; set; } = InitialTeamBGold;
        public int TeamBFood { get; set; } = InitialTeamBFood;
        public bool ShowSelfTeamLogOnly { get; set; }
        public float BattleLogExpandedWidth { get; set; }
        public float BattleLogExpandedHeight { get; set; }
        public bool IsBattleLogMinimized { get; set; }
        public List<BattleCellSaveData> Cells { get; set; } = new();
        public List<BattleOccupantSaveData> Occupants { get; set; } = new();
        public List<string> StrategyUsedUnitIds { get; set; } = new();
        public List<string> SupplyUsedUnitIds { get; set; } = new();
        public List<string> ChargeUsedUnitIds { get; set; } = new();
        public List<BattleWallTopAmmoSaveData> WallTopAmmo { get; set; } = new();
        public List<BattleFireSaveData> ActiveFires { get; set; } = new();
        public List<BattleLogSaveData> Logs { get; set; } = new();
    }

    private sealed class BattleCellSaveData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public BattleTerrainType Terrain { get; set; }
        public BattleStructureType Structure { get; set; }
        public BattleDeploymentZone DeploymentZone { get; set; }
        public bool BlocksMovement { get; set; }
        public bool IsGateOpen { get; set; }
        public int HeightLevel { get; set; }
        public int StructureMaxHealth { get; set; }
        public int StructureHealth { get; set; }
        public BattleStructureFacing StructureFacing { get; set; }
        public BattleGateSegment GateSegment { get; set; }
        public int CastleSourceId { get; set; }
        public int CastleAtlasX { get; set; }
        public int CastleAtlasY { get; set; }
        public int CastleAlternativeTile { get; set; }
        public bool HideGroundOccupantWithForeground { get; set; }
        public bool HideGroundOccupantWhenGateOpen { get; set; }
        public bool HasBridgeVisual { get; set; }
        public bool BridgeFlipHorizontally { get; set; }
        public bool WoodenFenceFlipHorizontally { get; set; }
        public int BuildingAtlasX { get; set; }
        public int BuildingAtlasY { get; set; }
        public int BridgeMaxHealth { get; set; }
        public int BridgeHealth { get; set; }

        public static BattleCellSaveData FromCell(BattleCellData cell)
        {
            return new BattleCellSaveData
            {
                X = cell.Grid.X,
                Y = cell.Grid.Y,
                Terrain = cell.Terrain,
                Structure = cell.Structure,
                DeploymentZone = cell.DeploymentZone,
                BlocksMovement = cell.BlocksMovement,
                IsGateOpen = cell.IsGateOpen,
                HeightLevel = cell.HeightLevel,
                StructureMaxHealth = cell.StructureMaxHealth,
                StructureHealth = cell.StructureHealth,
                StructureFacing = cell.StructureFacing,
                GateSegment = cell.GateSegment,
                CastleSourceId = cell.CastleSourceId,
                CastleAtlasX = cell.CastleAtlasCoords.X,
                CastleAtlasY = cell.CastleAtlasCoords.Y,
                CastleAlternativeTile = cell.CastleAlternativeTile,
                HideGroundOccupantWithForeground = cell.HideGroundOccupantWithForeground,
                HideGroundOccupantWhenGateOpen = cell.HideGroundOccupantWhenGateOpen,
                HasBridgeVisual = cell.HasBridgeVisual,
                BridgeFlipHorizontally = cell.BridgeFlipHorizontally,
                WoodenFenceFlipHorizontally = cell.WoodenFenceFlipHorizontally,
                BuildingAtlasX = cell.BuildingAtlasCoords.X,
                BuildingAtlasY = cell.BuildingAtlasCoords.Y,
                BridgeMaxHealth = cell.BridgeMaxHealth,
                BridgeHealth = cell.BridgeHealth
            };
        }

        public void ApplyTo(BattleCellData cell)
        {
            cell.Terrain = Terrain;
            cell.Structure = Structure;
            cell.DeploymentZone = DeploymentZone;
            cell.BlocksMovement = BlocksMovement;
            cell.IsGateOpen = IsGateOpen;
            cell.HeightLevel = HeightLevel;
            cell.StructureMaxHealth = StructureMaxHealth;
            cell.StructureHealth = StructureHealth;
            cell.StructureFacing = StructureFacing;
            cell.GateSegment = GateSegment;
            cell.CastleSourceId = CastleSourceId;
            cell.CastleAtlasCoords = new Vector2I(CastleAtlasX, CastleAtlasY);
            cell.CastleAlternativeTile = CastleAlternativeTile;
            cell.HideGroundOccupantWithForeground = HideGroundOccupantWithForeground;
            cell.HideGroundOccupantWhenGateOpen = HideGroundOccupantWhenGateOpen;
            cell.HasBridgeVisual = HasBridgeVisual;
            cell.BridgeFlipHorizontally = BridgeFlipHorizontally;
            cell.WoodenFenceFlipHorizontally = WoodenFenceFlipHorizontally;
            cell.BuildingAtlasCoords = new Vector2I(BuildingAtlasX, BuildingAtlasY);
            cell.BridgeMaxHealth = BridgeMaxHealth;
            cell.BridgeHealth = BridgeHealth;
        }
    }

    private sealed class BattleOccupantSaveData
    {
        public string UnitId { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Level { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ShortLabel { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string OfficerName { get; set; } = string.Empty;
        public string TroopType { get; set; } = string.Empty;
        public int TroopCount { get; set; }
        public int HitPoints { get; set; }
        public int MaxHitPoints { get; set; }
        public int WoundedTroops { get; set; }
        public int MessTurns { get; set; }
        public bool IsHidden { get; set; }
        public int? Morale { get; set; }
        public int? WeaponAmmo { get; set; }
        public int? MaxWeaponAmmo { get; set; }
        public int MoveRange { get; set; }
        public int AttackRange { get; set; }
        public BattleSpriteDirection FacingDirection { get; set; }

        public static BattleOccupantSaveData FromOccupant(BattleGridKey grid, BattleOccupantInfo occupant)
        {
            var saveData = new BattleOccupantSaveData
            {
                X = grid.X,
                Y = grid.Y,
                Level = grid.Level,
                DisplayName = occupant.DisplayName,
                Category = occupant.Category,
                ShortLabel = occupant.ShortLabel,
                TeamName = occupant.TeamName,
                OfficerName = occupant.OfficerName,
                TroopType = occupant.TroopType,
                TroopCount = occupant.TroopCount,
                HitPoints = occupant.HitPoints,
                MaxHitPoints = occupant.MaxHitPoints,
                WoundedTroops = occupant.WoundedTroops,
                MessTurns = occupant.MessTurns,
                IsHidden = occupant.IsHidden,
                Morale = occupant.Morale,
                WeaponAmmo = occupant.WeaponAmmo,
                MaxWeaponAmmo = occupant.MaxWeaponAmmo,
                MoveRange = occupant.MoveRange,
                AttackRange = occupant.AttackRange,
                FacingDirection = occupant.FacingDirection
            };
            saveData.UnitId = BuildUnitId(saveData);
            return saveData;
        }

        public BattleOccupantInfo ToOccupant(BattlePieceMarker marker)
        {
            UnitId = string.IsNullOrWhiteSpace(UnitId) ? BuildUnitId(this) : UnitId;
            return new BattleOccupantInfo(
                DisplayName,
                Category,
                ShortLabel,
                TeamName,
                OfficerName,
                TroopType,
                TroopCount,
                HitPoints,
                MaxHitPoints,
                WoundedTroops,
                MessTurns,
                IsHidden,
                Morale,
                WeaponAmmo,
                MaxWeaponAmmo,
                MoveRange,
                AttackRange,
                marker,
                FacingDirection);
        }

        private static string BuildUnitId(BattleOccupantSaveData saveData)
        {
            return string.Join("|", saveData.TeamName, saveData.DisplayName, saveData.OfficerName, saveData.TroopType, saveData.ShortLabel, saveData.Category);
        }
    }

    private sealed class BattleWallTopAmmoSaveData
    {
        public string UnitId { get; set; } = string.Empty;
        public int DropStoneUses { get; set; }
        public int PourOilUses { get; set; }
    }

    private sealed class BattleFireSaveData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Level { get; set; }
        public int RemainingTurns { get; set; }
        public int BurnTurns { get; set; }
    }

    private sealed class BattleLogSaveData
    {
        public int Turn { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    private sealed record BattleHudTeamInfo(string Name, int TotalTroops, int WoundedTroops, int TotalGenerals, int TotalSiegeUnits, int StrategyPlans, int TotalGold, int TotalFood);

    private enum BattleCommandMode
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

    private enum BattleStrategyAction
    {
        None,
        Fire,
        Mental
    }

    private enum WorkerWorkAction
    {
        General,
        WoodFence
    }

    private enum BattleTurnSide
    {
        TeamA,
        TeamB
    }

    private enum BattleSpriteDirection
    {
        NorthEast,
        NorthWest,
        SouthEast,
        SouthWest
    }
}
