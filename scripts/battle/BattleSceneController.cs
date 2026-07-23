using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    private const string CatapultStoneTexturePath = "res://assets/battle/object/catapult_stone.png";
    private const string NorthEastScenePath = "res://scenes/battle/BattleScene.tscn";
    private const string NorthWestScenePath = "res://scenes/battle/BattleSceneNorthWest.tscn";
    private const string NorthEastSiegeScenarioPath = "res://data/scenarios/battle/siege_ne.tres";
    private const string NorthEastMoatScenarioPath = "res://data/scenarios/battle/moat_siege.tres";
    private const string NorthWestSiegeScenarioPath = "res://data/scenarios/battle/siege_nw.tres";
    private const string NorthWestMoatScenarioPath = "res://data/scenarios/battle/moat_siege_nw.tres";
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
    private const int WorkerBridgeRepairAmount = 450;
    private const int WorkerGateRepairAmount = 600;
    private const int WorkerAttackDamage = 350;
    private const int DropStoneAttackDamage = 1200;
    private const int PourOilAttackDamage = 1000;
    private const double DropStoneEffectDurationSeconds = 0.48;
    private const double PourOilEffectDurationSeconds = 0.58;
    private const double ArrowProjectileEffectDurationSeconds = 0.42;
    private const double CatapultProjectileEffectDurationSeconds = 0.7;
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
    private const double DamagePopupDurationSeconds = 2.0;
    private static readonly BattleHudTeamInfo TeamAInfo = new("Team A / Attacker", 18000, 8200, 26000);
    private static readonly BattleHudTeamInfo TeamBInfo = new("Team B / Defender", 12500, 6400, 19800);
    private const string BattleDateText = "191 Apr 4";

    private BattleMapData? _mapData;
    private Node2D? _mapRoot;
    private Camera2D? _camera;
    private TileMapLayer? _groundLayer;
    private TileMapLayer? _moatLayer;
    private TileMapLayer? _objectLayer;
    private TileMapLayer? _castleLayer;
    private TileMapLayer? _overlayLayer;
    private BattleHighlightRenderer? _highlightLayer;
    private Texture2D? _catapultStoneTexture;
    private Node2D? _battleDepthLayer;
    private Node2D? _occludedUnitSilhouetteLayer;
    private Control? _commandMenu;
    private Label? _windowTitleLabel;
    private Label? _unitMenuInfoLabel;
    private Button? _endTurnButton;
    private Button? _weatherButton;
    private Button? _windButton;
    private Button? _windPowerButton;
    private Button? _moveButton;
    private Button? _attackButton;
    private Button? _dropStoneButton;
    private Button? _pourOilButton;
    private Button? _workButton;
    private Button? _installWoodFenceButton;
    private Button? _uninstallWoodFenceButton;
    private Button? _strategyButton;
    private Button? _openGateButton;
    private bool _isDraggingMap;
    private bool _isDraggingCommandMenu;
    private Vector2 _lastMousePosition;
    private Vector2 _commandMenuDragOffset;
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
    private readonly Dictionary<BattleGridKey, List<BattleOccupantInfo>> _occupantsByGrid = new();
    private readonly Dictionary<Node2D, BattleDepthEntry> _battleDepthEntries = new();
    private readonly Dictionary<Vector2I, Sprite2D> _castleDepthSpritesByGrid = new();
    private readonly List<BattleHighlightRenderer> _highlightDepthVisuals = new();
    private readonly Dictionary<BattleGridKey, Node2D> _occludedUnitSilhouettesByGrid = new();
    private readonly Dictionary<BattlePieceMarker, WallTopAttackAmmo> _wallTopAttackAmmoByMarker = new();
    private readonly Dictionary<BattleGridKey, BattleFireState> _activeFireByGrid = new();
    private readonly Dictionary<BattleGridKey, Node2D> _fireVisualsByGrid = new();
    private readonly HashSet<BattlePieceMarker> _strategyUsedByMarkerThisTurn = new();
    private BattleCommandMode _commandMode = BattleCommandMode.None;
    private WorkerWorkAction _workerWorkAction = WorkerWorkAction.General;
    private int _turnNumber = 1;
    private BattleTurnSide _currentTurnSide = BattleTurnSide.TeamA;
    private BattleWeatherType? _currentBattleWeather;
    private BattleWindDirection? _currentBattleWindDirection;
    private BattleWindPower? _currentBattleWindPower;
    private bool _editorBakeBattleLayout;
    private bool _editorClearTileLayout;
    private bool _editorRefreshBattleDepthPreview;

    private readonly record struct BattleDepthEntry(Node2D Node, BattleGridKey Grid, BattleDepthRenderKind Kind, int LocalOrder);

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

        ApplyPendingLaunchOptions();

        if (_endTurnButton != null)
        {
            _endTurnButton.Pressed += OnEndTurnButtonPressed;
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

        if (_moveButton != null)
        {
            _moveButton.Pressed += OnMoveButtonPressed;
        }

        if (_attackButton != null)
        {
            _attackButton.Pressed += OnAttackButtonPressed;
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

        if (_strategyButton != null)
        {
            _strategyButton.Pressed += OnStrategyButtonPressed;
        }

        if (_openGateButton != null)
        {
            _openGateButton.Pressed += OnOpenGateButtonPressed;
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

        if (_mapRoot != null)
        {
            _mapRoot.Position = GetClampedMapPosition(_mapRoot.Position);
        }
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
        _windowTitleLabel ??= GetNodeOrNull<Label>("UiLayer/CommandMenu/MenuMargin/MenuButtons/WindowTitleLabel");
        _unitMenuInfoLabel ??= GetNodeOrNull<Label>("UiLayer/CommandMenu/MenuMargin/MenuButtons/UnitMenuInfoLabel");
        _endTurnButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/EndTurnButton");
        _weatherButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/WeatherButton");
        _windButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/WindButton");
        _windPowerButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/WindPowerButton");
        _moveButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/MoveButton");
        _attackButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/AttackButton");
        _dropStoneButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/DropStoneButton");
        _pourOilButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/PourOilButton");
        _workButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/WorkButton");
        _installWoodFenceButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/InstallWoodFenceButton");
        _uninstallWoodFenceButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/UninstallWoodFenceButton");
        _strategyButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/StrategyButton");
        _openGateButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/OpenGateButton");
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

    public override void _Process(double delta)
    {
        UpdateHoverGrid();
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

        var loadedScenarioDefinition = GD.Load<BattleScenarioDefinition>(scenarioPath);
        if (loadedScenarioDefinition == null)
        {
            return false;
        }

        scenarioDefinition = loadedScenarioDefinition;
        return true;
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
        var sceneFilePath = GetTree().CurrentScene?.SceneFilePath;
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

            var occupant = occupants.FirstOrDefault(static candidate => candidate.Marker != null && IsBattlePiece(candidate));
            if (occupant?.Marker == null)
            {
                continue;
            }

            occupant.Marker.Visible = false;
            var silhouette = occupant.Marker.CreateSilhouetteVisual(GetOccludedUnitSilhouetteColor(occupant));
            if (silhouette == null)
            {
                occupant.Marker.Visible = true;
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
                    occupant.Marker.Visible = true;
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
        CreateMarker("MapRoot/UnitLayer/Ram", ResolveUnitSpawnGrid("Ram", new Vector2I(12, 16)), "R", "Battering Ram", CategorySiegeEngine, "Team A / Attacker", "Yue Jin", TroopRam, RamMaxHitPoints, new Color("7a4a20"), new Color("ead7aa"), 21.0f, moveRange: 3, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Ladder", ResolveUnitSpawnGrid("Ladder", new Vector2I(10, 15)), "L", "Siege Ladder", CategorySiegeEngine, "Team A / Attacker", "Yu Jin", TroopLadder, LadderMaxHitPoints, new Color("8c7b44"), new Color("ead7aa"), 21.0f, moveRange: 3, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Catapult", ResolveUnitSpawnGrid("Catapult", new Vector2I(14, 15)), "T", "Catapult", CategorySiegeEngine, "Team A / Attacker", "Liu Ye", TroopCatapult, CatapultMaxHitPoints, new Color("6e5131"), new Color("ead7aa"), 21.0f, moveRange: 2, attackRange: 4);

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
        marker.SetupNamePlate(officerName);
        marker.SetupTeamArrow(GetTeamArrowColor(teamName));
        marker.SetupHealthBar(troopCount, troopCount);
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

        RegisterOccupant(gridKey, displayName, category, label, teamName, officerName, troopType, troopCount, moveRange, attackRange, marker);
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
            titleLabel.Text = $"Scenario: {scenarioName}   Date: {BattleDateText}   Turn: {_turnNumber}   Acting Side: {GetCurrentTurnSideName()}";
        }

        if (_weatherButton != null)
        {
            _weatherButton.Text = $"Weather: {FormatBattleWeather(GetCurrentBattleWeather())}";
        }

        if (_windButton != null)
        {
            _windButton.Text = $"Wind: {FormatBattleWindDirection(GetCurrentBattleWindDirection())}";
        }

        if (_windPowerButton != null)
        {
            _windPowerButton.Text = $"Power: {FormatBattleWindPower(GetCurrentBattleWindPower())}";
        }

        if (summaryLabel != null)
        {
            summaryLabel.Text = BuildTeamHudText(TeamAInfo);
        }

        if (teamBLabel != null)
        {
            teamBLabel.Text = BuildTeamHudText(TeamBInfo);
        }

        if (coordinateLabel != null)
        {
            coordinateLabel.Text = BuildCoordinateText();
        }

        RefreshInfoPanel();
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
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
            _commandMenu.Position = ClampCommandMenuPosition(mouseMotion.Position - _commandMenuDragOffset);
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
        if (_commandMode is BattleCommandMode.MoveSelect or BattleCommandMode.AttackSelect or BattleCommandMode.StrategySelect)
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
        var infoLabel = GetNodeOrNull<Label>("UiLayer/SidePanel/Margin/PanelContent/InfoLabel");
        if (infoLabel == null)
        {
            return;
        }

        infoLabel.Text = BuildInfoText();
    }

    private string BuildCoordinateText()
    {
        return $"Hover: {FormatGrid(_hoverGridKey, _hoverGrid)}    Click: {FormatGrid(_selectedGridKey, _selectedGrid)}";
    }

    private static string BuildTeamHudText(BattleHudTeamInfo info)
    {
        return $"{info.Name}   Troops: {info.TotalTroops:N0}   Gold: {info.TotalGold:N0}   Food: {info.TotalFood:N0}";
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
            return $"Tile Info\nScenario: {ResolveScenarioDefinition().DisplayName}\nCoordinate: -\nClick a tile to inspect terrain, structure, deployment zone, and units.";
        }

        var grid = _selectedGrid.Value;
        var cell = _mapData.GetCell(grid.X, grid.Y);
        var builder = new StringBuilder();
        builder.AppendLine("Tile Info");
        builder.AppendLine($"Scenario: {ResolveScenarioDefinition().DisplayName}");
        builder.AppendLine($"Coordinate: {FormatGrid(_selectedGridKey, _selectedGrid)}");
        builder.AppendLine($"Terrain: {FormatTerrain(cell.Terrain)}");
        builder.AppendLine($"Structure: {FormatStructure(cell.Structure)}");
        if (cell.Structure != BattleStructureType.None)
        {
            builder.AppendLine($"Facing: {FormatStructureFacing(cell.StructureFacing)}");
        }
        if (cell.Structure == BattleStructureType.Gate)
        {
            builder.AppendLine($"Gate Segment: {FormatGateSegment(cell.GateSegment)}");
        }
        if (cell.HasStructureHealth)
        {
            var durability = GetDisplayStructureDurability(grid, cell);
            builder.AppendLine($"Durability: {durability.Current}/{durability.Max}");
            builder.AppendLine($"Status: {(cell.IsBroken ? "Broken" : "Intact")}");
        }
        if (cell.HasBridgeHealth)
        {
            builder.AppendLine($"Bridge HP: {cell.BridgeHealth}/{cell.BridgeMaxHealth}");
            builder.AppendLine($"Bridge Status: {(cell.IsBridgeDamaged ? "Damaged" : "Complete")}");
        }

        builder.AppendLine($"Deployment: {FormatDeploymentZone(cell.DeploymentZone)}");
        builder.AppendLine($"Height: {cell.HeightLevel}");
        builder.AppendLine($"Blocks Move: {(IsCellBlockingMovement(cell) ? "Yes" : "No")}");
        if (_activeFireByGrid.TryGetValue(ToGroundGridKey(grid), out var fireState))
        {
            builder.AppendLine($"Fire: Burning ({fireState.RemainingTurns} turn left)");
        }
        else
        {
            builder.AppendLine("Fire: None");
        }
        if (cell.Structure == BattleStructureType.Gate)
        {
            builder.AppendLine($"Gate: {(cell.IsGateOpen ? "Open" : "Closed")}");
        }

        builder.AppendLine("Occupants");

        var occupantsAtGrid = GetOccupantsAtSelectedGrid(grid).ToList();
        if (occupantsAtGrid.Count > 0)
        {
            foreach (var (gridKey, occupant) in occupantsAtGrid)
            {
                var hpText = occupant.Category == CategorySiegeEngine
                    ? $" HP {occupant.HitPoints}/{occupant.MaxHitPoints}"
                    : $" Troops {occupant.TroopCount:N0}/{occupant.MaxHitPoints:N0}";
                builder.AppendLine($"- {occupant.Category}: {occupant.DisplayName} [{occupant.ShortLabel}] L{gridKey.Level}{hpText}");
            }
        }
        else
        {
            builder.AppendLine("- None");
        }

        if (_selectedUnit != null && _selectedUnitGrid.HasValue)
        {
            builder.AppendLine("Selected Piece");
            builder.AppendLine($"- {_selectedUnit.DisplayName} [{_selectedUnit.ShortLabel}]");
            builder.AppendLine($"- Category: {_selectedUnit.Category}");
            builder.AppendLine($"- Grid: ({_selectedUnitGrid.Value.X}, {_selectedUnitGrid.Value.Y}, L{_selectedUnitGrid.Value.Level})");
            builder.AppendLine($"- Move Range: {_selectedUnit.MoveRange}");
            builder.AppendLine($"- Attack Range: {_selectedUnit.AttackRange}");
            if (_selectedUnit.Category == CategorySiegeEngine)
            {
                builder.AppendLine($"- HP: {_selectedUnit.HitPoints}/{_selectedUnit.MaxHitPoints}");
            }
            else
            {
                builder.AppendLine($"- Troops: {_selectedUnit.TroopCount:N0}/{_selectedUnit.MaxHitPoints:N0}");
            }

            builder.AppendLine($"- Reachable Tiles: {_movableGrids.Count}");
            builder.AppendLine($"- Attackable Tiles: {_attackableGrids.Count}");
            builder.AppendLine($"- Workable Tiles: {_workableGrids.Count}");
            builder.AppendLine($"- Strategy Targets: {_strategyTargetGrids.Count}");
            builder.AppendLine($"- Fire Strategy: {(CanUseFireStrategy(_selectedUnit) ? "Ready" : "Unavailable")}");
            builder.AppendLine($"- Command State: {FormatCommandMode(_commandMode)}");
            builder.AppendLine($"- Current Turn: {GetCurrentTurnSideName()}");
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

        occupants.Add(new BattleOccupantInfo(displayName, category, shortLabel, teamName, officerName, troopType, troopCount, troopCount, troopCount, moveRange, attackRange, marker, BattleSpriteDirection.SouthEast));
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

        if (!TryBuildMovePath(sourceGrid, destinationGrid, movingOccupant.MoveRange, out var movePath))
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
        var movedOccupant = movingOccupant with { Marker = movingOccupant.Marker, FacingDirection = moveDirection };
        destinationOccupants.Add(movedOccupant);
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
        _movableGrids.Clear();
        _attackableGrids.Clear();
        HideCommandMenu();

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

        var attackAnimationDuration = ApplyAttackAnimation(attackingUnit, attackDirection);
        var hurtAnimationDuration = ApplyTargetHurtAnimation(_selectedUnitGrid.Value, targetGrid);
        var arrowEffectDuration = IsArrowProjectileAttacker(attackingUnit)
            ? PlayArrowProjectileEffect(_selectedUnitGrid.Value, targetGrid)
            : 0.0;
        var catapultEffectDuration = attackingUnit.Category == CategorySiegeEngine && attackingUnit.TroopType == TroopCatapult
            ? PlayCatapultProjectileEffect(_selectedUnitGrid.Value, targetGrid)
            : 0.0;
        var effectDelaySeconds = Math.Max(
            Math.Max(attackAnimationDuration, hurtAnimationDuration),
            Math.Max(arrowEffectDuration, catapultEffectDuration));
        ApplyAttackDamage(attackingUnit, targetGrid, effectDelaySeconds);
        if (shouldTemporarilyRevealOccludedUnits)
        {
            RefreshOccludedUnitSilhouettesAfterDelay(effectDelaySeconds);
        }

        _commandMode = BattleCommandMode.None;
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
        if (!ApplyWorkerWork(targetGrid.Grid, targetCell))
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

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        return true;
    }

    private bool ApplyWorkerWork(Vector2I targetGrid, BattleCellData targetCell)
    {
        if (_mapData == null)
        {
            return false;
        }

        if (_workerWorkAction == WorkerWorkAction.InstallWoodFence)
        {
            if (!CanInstallWoodFence(targetGrid, targetCell))
            {
                return false;
            }

            targetCell.Structure = BattleStructureType.WoodenFence;
            targetCell.StructureMaxHealth = BattleCellData.WoodenFenceMaxHealth;
            targetCell.StructureHealth = targetCell.StructureMaxHealth;
            targetCell.BlocksMovement = true;
            RefreshWorkerObjectLayers();
            return true;
        }

        if (_workerWorkAction == WorkerWorkAction.UninstallWoodFence)
        {
            if (targetCell.Structure != BattleStructureType.WoodenFence)
            {
                return false;
            }

            targetCell.Structure = BattleStructureType.None;
            targetCell.BlocksMovement = false;
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
        }
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

    private double ApplyTargetHurtAnimation(BattleGridKey attackerGrid, BattleGridKey targetGrid)
    {
        if (IsClosedGateStructureTarget(targetGrid))
        {
            return 0.0;
        }

        if (!_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            return 0.0;
        }

        var target = GetAttackTarget(targetOccupants);
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

        var target = GetAttackTarget(targetOccupants);
        if (target == null)
        {
            ApplyStructureAttackDamage(attacker, targetGrid);
            return;
        }

        var damage = damageOverride ?? GetAttackDamage(attacker);
        if (damage <= 0)
        {
            return;
        }

        var actualDamage = Mathf.Min(target.HitPoints, damage);
        var remainingHp = Mathf.Max(0, target.HitPoints - damage);
        var remainingTroops = target.Category == CategoryUnit
            ? Mathf.Max(0, target.TroopCount - damage)
            : target.TroopCount;
        var updatedTarget = target with
        {
            TroopCount = remainingTroops,
            HitPoints = remainingHp
        };
        updatedTarget.Marker?.SetupHealthBar(updatedTarget.HitPoints, updatedTarget.MaxHitPoints);
        ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedTarget;
        }

        ShowDamagePopup(targetGrid, actualDamage);
        RefreshInfoPanel();
        if (remainingHp <= 0)
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
        if (_battleDepthLayer == null || damage <= 0)
        {
            return;
        }

        var popup = new Label
        {
            Text = $"-{damage:N0}",
            Position = GetMarkerPosition(targetGrid) + new Vector2(-18.0f, -88.0f),
            ZIndex = 500,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1.0f, 0.12f, 0.08f, 1.0f)
        };
        popup.AddThemeColorOverride("font_color", new Color(1.0f, 0.10f, 0.06f, 1.0f));
        popup.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.02f, 0.01f, 0.95f));
        popup.AddThemeConstantOverride("outline_size", 4);
        popup.AddThemeFontSizeOverride("font_size", 22);
        _battleDepthLayer.AddChild(popup);

        var tween = popup.CreateTween();
        tween.SetParallel(true);
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(popup, "position", popup.Position + new Vector2(0.0f, -34.0f), DamagePopupDurationSeconds);
        tween.TweenProperty(popup, "modulate:a", 0.0f, DamagePopupDurationSeconds);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => popup.QueueFree()));
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

        RemoveOccupant(grid, occupant);
        RefreshBattleDepthLayerOrder();
        RefreshOccludedUnitSilhouettes();
        RefreshInfoPanel();
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

    private static int GetAttackDamage(BattleOccupantInfo attacker)
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

        var selectedUnit = occupants.FirstOrDefault(static occupant => IsBattlePiece(occupant));
        if (selectedUnit == null)
        {
            return;
        }

        _selectedUnit = selectedUnit;
        _selectedUnitGrid = selectedGridKey.Value;
        _commandMode = BattleCommandMode.AwaitingCommand;
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
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue)
        {
            return;
        }

        _commandMode = BattleCommandMode.MoveSelect;
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _movableGrids.Clear();
        foreach (var grid in CalculateReachableGrids(_selectedUnitGrid.Value, _selectedUnit.MoveRange))
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
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue)
        {
            return;
        }

        _commandMode = BattleCommandMode.AttackSelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        foreach (var grid in CalculateAttackableGrids(_selectedUnitGrid.Value, _selectedUnit))
        {
            _attackableGrids.Add(grid);
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
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
            ? ApplyTargetHurtAnimation(_selectedUnitGrid.Value, targetGrid)
            : 0.0;
        var specialEffectDuration = isDropStone
            ? PlayDropStoneEffect(_selectedUnitGrid.Value, targetGrid)
            : PlayPourOilEffect(_selectedUnitGrid.Value, targetGrid);
        var effectDelaySeconds = Math.Max(hurtAnimationDuration, specialEffectDuration);
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

        _commandMode = BattleCommandMode.StrategySelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        if (!CanUseFireStrategy(_selectedUnit))
        {
            _commandMode = BattleCommandMode.AwaitingCommand;
            HideCommandMenu();
            RefreshInfoPanel();
            RefreshHighlights();
            return;
        }

        foreach (var targetGrid in CalculateFireStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit))
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
        if (!_strategyTargetGrids.Contains(targetGrid) || !CanUseFireStrategy(_selectedUnit))
        {
            return false;
        }

        if (!IgniteBattleFire(targetGrid))
        {
            return false;
        }

        _strategyUsedByMarkerThisTurn.Add(_selectedUnit.Marker);
        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
        return true;
    }

    private bool CanUseFireStrategy(BattleOccupantInfo? occupant)
    {
        if (occupant?.Marker == null || !CanUseUnitTypeFireStrategy(occupant))
        {
            return false;
        }

        return !_strategyUsedByMarkerThisTurn.Contains(occupant.Marker);
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

        var target = GetAttackTarget(targetOccupants);
        if (target == null)
        {
            return;
        }

        var actualDamage = Mathf.Min(target.HitPoints, damage);
        var remainingHp = Mathf.Max(0, target.HitPoints - damage);
        var remainingTroops = target.Category == CategoryUnit
            ? Mathf.Max(0, target.TroopCount - damage)
            : target.TroopCount;
        var updatedTarget = target with
        {
            TroopCount = remainingTroops,
            HitPoints = remainingHp
        };
        updatedTarget.Marker?.SetupHealthBar(updatedTarget.HitPoints, updatedTarget.MaxHitPoints);
        ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedTarget;
        }

        ShowDamagePopup(targetGrid, actualDamage);
        if (remainingHp <= 0)
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

    private static string FormatBattleWeather(BattleWeatherType weather)
    {
        return weather switch
        {
            BattleWeatherType.Cloudy => "Cloudy",
            BattleWeatherType.Rain => "Rain",
            _ => "Sunny"
        };
    }

    private static string FormatBattleWindDirection(BattleWindDirection direction)
    {
        return direction switch
        {
            BattleWindDirection.NorthWest => "NorthWest",
            BattleWindDirection.SouthEast => "SouthEast",
            BattleWindDirection.SouthWest => "SouthWest",
            _ => "NorthEast"
        };
    }

    private static string FormatBattleWindPower(BattleWindPower power)
    {
        return power switch
        {
            BattleWindPower.Calm => "Calm",
            BattleWindPower.Strong => "Strong",
            _ => "Breeze"
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
        BeginWorkerWorkSelection(WorkerWorkAction.InstallWoodFence);
    }

    private void OnUninstallWoodFenceButtonPressed()
    {
        BeginWorkerWorkSelection(WorkerWorkAction.UninstallWoodFence);
    }

    private void BeginWorkerWorkSelection(WorkerWorkAction workAction)
    {
        if (_selectedUnit?.TroopType != TroopWorker || !_selectedUnitGrid.HasValue)
        {
            return;
        }

        _workerWorkAction = workAction;
        _commandMode = BattleCommandMode.WorkSelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
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
        if (workAction == WorkerWorkAction.InstallWoodFence)
        {
            return CanInstallWoodFence(targetGrid, cell);
        }

        if (workAction == WorkerWorkAction.UninstallWoodFence)
        {
            return cell.Structure == BattleStructureType.WoodenFence;
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
        var attackRange = attacker.AttackRange;
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

        if (_unitMenuInfoLabel != null)
        {
            if (_selectedUnit != null)
            {
                var strengthText = _selectedUnit.Category == CategorySiegeEngine
                    ? $"HP: {_selectedUnit.HitPoints}/{_selectedUnit.MaxHitPoints}"
                    : $"Troops: {_selectedUnit.TroopCount:N0}";
                _unitMenuInfoLabel.Text =
                    $"Team: {_selectedUnit.TeamName}\n" +
                    $"Officer: {_selectedUnit.OfficerName}\n" +
                    $"Type: {_selectedUnit.TroopType}\n" +
                    strengthText;
            }
            else
            {
                _unitMenuInfoLabel.Text = "Team: -\nOfficer: -\nType: -\nTroops: -";
            }
        }

        if (_openGateButton != null)
        {
            if (TryGetSwitchableGate(out var switchGateGrid) && _mapData != null)
            {
                var switchGateCell = _mapData.GetCell(switchGateGrid.X, switchGateGrid.Y);
                _openGateButton.Text = switchGateCell.IsGateOpen ? "Close Gate" : "Open Gate";
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
            _dropStoneButton.Visible = _selectedUnitGrid?.Level == 2 && IsWallTopGrid(_selectedUnitGrid.Value.Grid);
            _dropStoneButton.Text = $"Drop Stone ({GetWallTopAttackUsesRemaining(isDropStone: true)})";
            _dropStoneButton.Disabled = !canUseWallTopAttack || GetWallTopAttackUsesRemaining(isDropStone: true) <= 0;
        }

        if (_pourOilButton != null)
        {
            _pourOilButton.Visible = _selectedUnitGrid?.Level == 2 && IsWallTopGrid(_selectedUnitGrid.Value.Grid);
            _pourOilButton.Text = $"Pour Oil ({GetWallTopAttackUsesRemaining(isDropStone: false)})";
            _pourOilButton.Disabled = !canUseWallTopAttack || GetWallTopAttackUsesRemaining(isDropStone: false) <= 0;
        }

        if (_workButton != null)
        {
            _workButton.Visible = _selectedUnit?.TroopType == TroopWorker;
            _workButton.Disabled = !HasWorkerWorkTarget(WorkerWorkAction.General);
        }

        if (_installWoodFenceButton != null)
        {
            _installWoodFenceButton.Visible = _selectedUnit?.TroopType == TroopWorker;
            _installWoodFenceButton.Disabled = !HasWorkerWorkTarget(WorkerWorkAction.InstallWoodFence);
        }

        if (_uninstallWoodFenceButton != null)
        {
            _uninstallWoodFenceButton.Visible = _selectedUnit?.TroopType == TroopWorker;
            _uninstallWoodFenceButton.Disabled = !HasWorkerWorkTarget(WorkerWorkAction.UninstallWoodFence);
        }

        if (_strategyButton != null)
        {
            var canUseStrategyUnit = _selectedUnit != null && CanUseUnitTypeFireStrategy(_selectedUnit);
            _strategyButton.Visible = canUseStrategyUnit;
            _strategyButton.Text = "Strategy (Fire)";
            _strategyButton.Disabled = !CanUseFireStrategy(_selectedUnit);
        }

        var desiredPosition = screenPosition + new Vector2(12.0f, 12.0f);
        _commandMenu.Position = ClampCommandMenuPosition(desiredPosition);
        _commandMenu.Visible = true;
        ResizeCommandMenuAfterLayout(desiredPosition);
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
            _unitMenuInfoLabel.Text = "Team: -\nOfficer: -\nType: -\nTroops: -";
        }

        if (_openGateButton != null)
        {
            _openGateButton.Visible = false;
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

        if (_strategyButton != null)
        {
            _strategyButton.Visible = false;
            _strategyButton.Disabled = false;
            _strategyButton.Text = "Strategy";
        }
    }

    private void CancelCommandAction(bool clearSelection)
    {
        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();

        if (clearSelection)
        {
            _selectedUnit = null;
            _selectedUnitGrid = null;
            _selectedGridKey = null;
        }
    }

    private void OnEndTurnButtonPressed()
    {
        CancelCommandAction(clearSelection: true);
        ResolveBattleFireAtTurnEnd();
        _strategyUsedByMarkerThisTurn.Clear();

        if (_currentTurnSide == BattleTurnSide.TeamA)
        {
            _currentTurnSide = BattleTurnSide.TeamB;
        }
        else
        {
            _currentTurnSide = BattleTurnSide.TeamA;
            _turnNumber++;
        }

        ConfigureHud();
        RefreshCoordinateLabel();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private void OnWeatherButtonPressed()
    {
        _currentBattleWeather = GetNextBattleWeather(GetCurrentBattleWeather());
        ConfigureHud();
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

    private static string FormatCommandMode(BattleCommandMode commandMode)
    {
        return commandMode switch
        {
            BattleCommandMode.MoveSelect => "Select Move Target",
            BattleCommandMode.AttackSelect => "Select Attack Target",
            BattleCommandMode.WorkSelect => "Select Work Target",
            BattleCommandMode.StrategySelect => "Strategy Pending",
            BattleCommandMode.AwaitingCommand => "Awaiting Command",
            _ => "None"
        };
    }

    private string GetCurrentTurnSideName()
    {
        return _currentTurnSide == BattleTurnSide.TeamA ? "Team A / Attacker" : "Team B / Defender";
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

    private static string FormatTerrain(BattleTerrainType terrain)
    {
        return terrain switch
        {
            BattleTerrainType.Road => "Road",
            BattleTerrainType.Courtyard => "Courtyard",
            BattleTerrainType.Forest => "Forest",
            BattleTerrainType.WallWalk => "Wall Top",
            BattleTerrainType.Moat => "Moat",
            BattleTerrainType.Bridge => "Bridge",
            BattleTerrainType.Grass => "Grass",
            _ => "Plain"
        };
    }

    private static string FormatStructure(BattleStructureType structure)
    {
        return structure switch
        {
            BattleStructureType.Wall => "Wall",
            BattleStructureType.Gate => "Gate",
            BattleStructureType.Tower => "Tower",
            BattleStructureType.Building => "Building",
            BattleStructureType.Tree => "Tree",
            BattleStructureType.RockBig => "Large Rock",
            BattleStructureType.RockSmall => "Small Rock",
            _ => "None"
        };
    }

    private static string FormatDeploymentZone(BattleDeploymentZone zone)
    {
        return zone switch
        {
            BattleDeploymentZone.Attacker => "Attacker Zone",
            BattleDeploymentZone.Defender => "Defender Zone",
            _ => "None"
        };
    }

    private static string FormatStructureFacing(BattleStructureFacing facing)
    {
        return facing switch
        {
            BattleStructureFacing.NorthEast => "NorthEast",
            BattleStructureFacing.NorthWest => "NorthWest",
            _ => "None"
        };
    }

    private static string FormatGateSegment(BattleGateSegment gateSegment)
    {
        return gateSegment switch
        {
            BattleGateSegment.Left => "Left",
            BattleGateSegment.Right => "Right",
            _ => "None"
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
        int MoveRange,
        int AttackRange,
        BattlePieceMarker? Marker,
        BattleSpriteDirection FacingDirection);
    private readonly record struct WallTopAttackAmmo(int DropStoneUses, int PourOilUses);
    private readonly record struct BattleFireState(int RemainingTurns, int BurnTurns);
    private sealed record BattleHudTeamInfo(string Name, int TotalTroops, int TotalGold, int TotalFood);

    private enum BattleCommandMode
    {
        None,
        AwaitingCommand,
        MoveSelect,
        AttackSelect,
        WorkSelect,
        StrategySelect
    }

    private enum WorkerWorkAction
    {
        General,
        InstallWoodFence,
        UninstallWoodFence
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
