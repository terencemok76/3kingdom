using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using static ThreeKingdom.Battle.BattleAiSettings;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattlePresentationSettings;
using static ThreeKingdom.Battle.BattleResourcePaths;
using static ThreeKingdom.Battle.BattleUnitTypes;
using static ThreeKingdom.Battle.BattleUnitVisualCatalog;

namespace ThreeKingdom.Battle;

public readonly record struct BattleGridKey(int X, int Y, int Level)
{
    public Vector2I Grid => new(X, Y);

    public override string ToString() => $"({X}, {Y}, L{Level})";
}

internal enum BattleDepthRenderKind
{
    CastleVisual,
    BuildingVisual,
    FireEffect,
    MoveHighlight,
    AttackHighlight,
    SelectedHighlight,
    SiegeEngine,
    Unit
}

[Flags]
internal enum BattleAiControlledSides
{
    None = 0,
    Attacker = 1,
    Defender = 2
}

[Tool]
public partial class BattleSceneController : Node2D
{
    private const string CampaignBattleLoadingOverlayName = "CampaignBattleLoadingOverlay";
    public sealed record LaunchOptions(BattleScenarioType ScenarioType, bool UseEditorAuthoredLayout, int CampaignId = 0);

    private enum BattleOfficerSpeechEvent
    {
        Opening,
        ReinforcementArrival,
        EnemyRetreat,
        Attack,
        Charge,
        Union,
        Capture,
        Destroy,
        Critical,
        Retreat,
        TerrainForest,
        TerrainHill,
        TerrainBridge,
        TerrainSwamp,
        GateOpen,
        GateClose,
        GateBreach
    }

    private sealed class BattleOfficerSpeechCatalog
    {
        public List<BattleOfficerSpeechEntry> Entries { get; set; } = new();
    }

    private sealed class BattleOfficerSpeechEntry
    {
        public string Event { get; set; } = string.Empty;
        public string Persona { get; set; } = string.Empty;
        public int Priority { get; set; }
        public List<string> Keys { get; set; } = new();
    }

    public static LaunchOptions? PendingLaunchOptions { get; set; }

    // Camera2D zoom below 1.0 is closer; above 1.0 shows more of the battlefield.
    private static readonly BattleHudTeamInfo TeamAInfo = new(BattleTeamIdentity.AttackerName, 0, 0, 0, 0, 0, InitialTeamAGold, InitialTeamAFood);
    private static readonly BattleHudTeamInfo TeamBInfo = new(BattleTeamIdentity.DefenderName, 0, 0, 0, 0, 0, InitialTeamBGold, InitialTeamBFood);
    private static readonly JsonSerializerOptions OfficerSpeechJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private BattleMapData? _mapData;
    private Node2D? _mapRoot;
    private Camera2D? _camera;
    private TileMapLayer? _groundLayer;
    private TileMapLayer? _moatLayer;
    private TileMapLayer? _objectLayer;
    private TileMapLayer? _workerFenceLayer;
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
    private Control? _topBar;
    private Control? _tileInfoPanel;
    private bool _isTileInfoPanelDragPending;
    private bool _isDraggingTileInfoPanel;
    private bool _isResizingTileInfoPanel;
    private Vector2 _tileInfoPanelDragStartMouse;
    private Vector2 _tileInfoPanelDragOffset;
    private Vector2 _tileInfoPanelResizeStartMouse;
    private Vector2 _tileInfoPanelResizeStartSize;
    private Control? _battleLogPanel;
    private Control? _battleLogHeaderRow;
    private Control? _battleLogContent;
    private Control? _battleLogScroll;
    private Control? _battleLogResizeGrip;
    private Control? _battleResultOverlay;
    private Label? _battleResultLabel;
    private Control? _retreatNotice;
    private Label? _retreatNoticeLabel;
    private Control? _officerCaptureNotice;
    private Label? _officerCaptureNoticeLabel;
    private Control? _turnBanner;
    private Label? _turnBannerLabel;
    private Control? _turnInputBlocker;
    private Control? _officerSpeechOverlay;
    private TextureRect? _officerSpeechPortrait;
    private Label? _officerSpeechTeamNameLabel;
    private Label? _officerSpeechNameLabel;
    private Label? _officerSpeechTextLabel;
    private Button? _allLogButton;
    private Button? _selfLogButton;
    private Button? _copyLogButton;
    private Button? _minimizeLogButton;
    private Label? _battleLogLabel;
    private Label? _battleLogTitleLabel;
    private Label? _windowTitleLabel;
    private Label? _unitMenuInfoLabel;
    private TextureRect? _officerPortrait;
    private readonly Dictionary<string, Texture2D> _officerPortraitTextures = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Texture2D> _officerPortraitTexturesById = new();
    private bool _officerPortraitMappingsLoaded;
    private Button? _endTurnButton;
    private Button? _enableAiButton;
    private Button? _disableAiButton;
    private Button? _startRoundButton;
    private Button? _nextAiButton;
    private Button? _attackerOneDayFoodButton;
    private Button? _defenderOneDayFoodButton;
    private Button? _monthlyBattleLimitOneDayButton;
    private Label? _aiRoundStatusLabel;
    private Button? _timeButton;
    private Button? _weatherButton;
    private Button? _windButton;
    private Button? _windPowerButton;
    private Button? _battleOptionButton;
    private Button? _battleOptionDebugButton;
    private Control? _battleOptionOverlay;
    private Control? _battleDebugOverlay;
    private PanelContainer? _battleDebugPanel;
    private Control? _battleDebugTitleBar;
    private Label? _battleOptionTitleLabel;
    private Label? _battleDebugTitleLabel;
    private Button? _battleOptionCloseButton;
    private Button? _battleDebugCloseButton;
    private Button? _battleOptionSaveLoadButton;
    private Control? _campaignSaveLoadOverlay;
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
    private Button? _guardButton;
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
    private bool _isPanningMapFromTileInfoPanel;
    private bool _isDraggingCommandMenu;
    private bool _isDraggingBattleLog;
    private bool _isResizingBattleLog;
    private bool _isBattleLogMinimized;
    private bool _isDraggingBattleDebugDialog;
    private readonly BattleState _state = new();
    private readonly BattleInteractionState _interaction = new();
    private bool _isBattleFinished { get => _state.IsBattleFinished; set => _state.IsBattleFinished = value; }
    private int _retreatNoticeSerial;
    private int _officerCaptureNoticeSerial;
    private int _turnBannerSerial;
    // This is intentionally separate from the visual blocker: AI actions and
    // signal callbacks must observe the same turn-opening gate as pointer input.
    private bool _isTurnBannerActionLocked;
    // A player side can contain computer-controlled relief forces.  Keep this
    // separate from the side-level AI flag so their turn can run after the
    // player ends the turn without handing player units to the AI.
    private bool _isResolvingAlliedAiTurn;
    private int _alliedAiMovementAnimations;
    private ulong _alliedAiVisualUntilMsec;
    private OptionButton? _allyCooperationObjectiveOption;
    private int _officerSpeechSerial;
    private int _activeOfficerSpeechPriority;
    private readonly List<BattleOfficerSpeechEntry> _officerSpeechEntries = new();
    private readonly Dictionary<string, ulong> _officerSpeechLastShownAt = new(StringComparer.Ordinal);
    private readonly Random _officerSpeechRandom = new();
    private bool _attackerOutpostVictorySecured { get => _state.AttackerOutpostVictorySecured; set => _state.AttackerOutpostVictorySecured = value; }
    private Vector2 _lastMousePosition;
    private Vector2 _commandMenuDragOffset;
    private Vector2 _battleLogDragOffset;
    private Vector2 _battleDebugDialogDragOffset;
    private Vector2 _battleLogResizeStartMouse;
    private Vector2 _battleLogResizeStartSize;
    private Vector2I? _hoverGrid { get => _interaction.HoverGrid; set => _interaction.HoverGrid = value; }
    private Vector2I? _selectedGrid { get => _interaction.SelectedGrid; set => _interaction.SelectedGrid = value; }
    private BattleGridKey? _hoverGridKey { get => _interaction.HoverGridKey; set => _interaction.HoverGridKey = value; }
    private BattleGridKey? _selectedGridKey { get => _interaction.SelectedGridKey; set => _interaction.SelectedGridKey = value; }
    private BattleGridKey? _selectedUnitGrid { get => _interaction.SelectedUnitGrid; set => _interaction.SelectedUnitGrid = value; }
    private BattleOccupantInfo? _selectedUnit { get => _interaction.SelectedUnit; set => _interaction.SelectedUnit = value; }
    private HashSet<BattleGridKey> _movableGrids => _interaction.MovableGrids;
    private HashSet<BattleGridKey> _attackableGrids => _interaction.AttackableGrids;
    private HashSet<BattleGridKey> _workableGrids => _interaction.WorkableGrids;
    private HashSet<BattleGridKey> _strategyTargetGrids => _interaction.StrategyTargetGrids;
    private HashSet<BattleGridKey> _duelTargetGrids => _interaction.DuelTargetGrids;
    private HashSet<BattleGridKey> _chargeTargetGrids => _interaction.ChargeTargetGrids;
    private HashSet<BattleGridKey> _hireOfficerTargetGrids => _interaction.HireOfficerTargetGrids;
    private BattleUnitRepository _occupantsByGrid => _state.Units;
    private readonly Dictionary<Node2D, BattleDepthEntry> _battleDepthEntries = new();
    private readonly Dictionary<Vector2I, Sprite2D> _castleDepthSpritesByGrid = new();
    private readonly Dictionary<Vector2I, Sprite2D> _buildingDepthSpritesByGrid = new();
    private readonly Dictionary<Vector2I, Node2D> _staticOutpostFlagHostsByGrid = new();
    private readonly Dictionary<Vector2I, Node2D> _outpostOwnerFlagsByGrid = new();
    private readonly List<BattleHighlightRenderer> _highlightDepthVisuals = new();
    private readonly Dictionary<BattleGridKey, Node2D> _occludedUnitSilhouettesByGrid = new();
    private readonly HashSet<BattlePieceMarker> _markersRevealedForMovement = new();
    private readonly Dictionary<BattlePieceMarker, WallTopAttackAmmo> _wallTopAttackAmmoByMarker = new();
    private readonly Dictionary<BattleGridKey, BattleFireState> _activeFireByGrid = new();
    private readonly Dictionary<BattleGridKey, Node2D> _fireVisualsByGrid = new();
    private HashSet<BattlePieceMarker> _strategyUsedByMarkerThisTurn => _state.StrategyUsedByMarkerThisTurn;
    private HashSet<BattlePieceMarker> _supplyUsedByMarkerThisTurn => _state.SupplyUsedByMarkerThisTurn;
    private HashSet<BattlePieceMarker> _chargeUsedByMarkerThisTurn => _state.ChargeUsedByMarkerThisTurn;
    private HashSet<BattlePieceMarker> _actedByMarkerThisRound => _state.ActedByMarkerThisRound;
    private readonly Dictionary<BattlePieceMarker, AiBridgeEngineeringPlan> _aiBridgePlanByWorker = new();
    private AiGateSortiePlan? _aiGateSortiePlan;
    private readonly List<BattleLogEntry> _battleLogs = new();
    private readonly List<ColorRect> _rainStreaks = new();
    private BattleCommandMode _commandMode { get => _interaction.CommandMode; set => _interaction.CommandMode = value; }
    private BattleStrategyAction _selectedStrategyAction { get => _interaction.SelectedStrategyAction; set => _interaction.SelectedStrategyAction = value; }
    private WorkerWorkAction _workerWorkAction { get => _interaction.WorkerWorkAction; set => _interaction.WorkerWorkAction = value; }
    private int _turnNumber { get => _state.TurnNumber; set => _state.TurnNumber = value; }
    private BattleTurnSide _currentTurnSide { get => _state.CurrentTurnSide; set => _state.CurrentTurnSide = value; }
    private int _battleDateYear { get => _state.BattleDateYear; set => _state.BattleDateYear = value; }
    private int _battleDateMonth { get => _state.BattleDateMonth; set => _state.BattleDateMonth = value; }
    private int _battleDateDay { get => _state.BattleDateDay; set => _state.BattleDateDay = value; }
    private BattleAiControlledSides _aiControlledSides { get => _state.AiControlledSides; set => _state.AiControlledSides = value; }
    private bool _isFieldAiRoundStarted { get => _state.IsFieldAiRoundStarted; set => _state.IsFieldAiRoundStarted = value; }
    private BattleTimeOfDay? _currentBattleTimeOfDay { get => _state.CurrentBattleTimeOfDay; set => _state.CurrentBattleTimeOfDay = value; }
    private BattleWeatherType? _currentBattleWeather { get => _state.CurrentBattleWeather; set => _state.CurrentBattleWeather = value; }
    private BattleWindDirection? _currentBattleWindDirection { get => _state.CurrentBattleWindDirection; set => _state.CurrentBattleWindDirection = value; }
    private BattleWindPower? _currentBattleWindPower { get => _state.CurrentBattleWindPower; set => _state.CurrentBattleWindPower = value; }
    private uint _environmentSeed { get => _state.EnvironmentSeed; set => _state.EnvironmentSeed = value; }
    private int _environmentStep { get => _state.EnvironmentStep; set => _state.EnvironmentStep = value; }
    private int _teamATotalTroops { get => _state.TeamA.TotalTroops; set => _state.TeamA.TotalTroops = value; }
    private int _teamBTotalTroops { get => _state.TeamB.TotalTroops; set => _state.TeamB.TotalTroops = value; }
    private int _teamASiegeUnits { get => _state.TeamA.SiegeUnits; set => _state.TeamA.SiegeUnits = value; }
    private int _teamBSiegeUnits { get => _state.TeamB.SiegeUnits; set => _state.TeamB.SiegeUnits = value; }
    private int _teamAGenerals { get => _state.TeamA.Generals; set => _state.TeamA.Generals = value; }
    private int _teamBGenerals { get => _state.TeamB.Generals; set => _state.TeamB.Generals = value; }
    private int _teamAStrategyPlans { get => _state.TeamA.StrategyPlans; set => _state.TeamA.StrategyPlans = value; }
    private int _teamBStrategyPlans { get => _state.TeamB.StrategyPlans; set => _state.TeamB.StrategyPlans = value; }
    private int _teamAGold { get => _state.TeamA.Gold; set => _state.TeamA.Gold = value; }
    private int _teamAFood { get => _state.TeamA.Food; set => _state.TeamA.Food = value; }
    private int _teamAZeroFoodDays { get => _state.TeamA.ZeroFoodDays; set => _state.TeamA.ZeroFoodDays = value; }
    private int _teamBGold { get => _state.TeamB.Gold; set => _state.TeamB.Gold = value; }
    private int _teamBFood { get => _state.TeamB.Food; set => _state.TeamB.Food = value; }
    private int _teamBZeroFoodDays { get => _state.TeamB.ZeroFoodDays; set => _state.TeamB.ZeroFoodDays = value; }
    private bool _showSelfTeamLogOnly;
    private bool _battleBgmEnabled = true;
    private bool _battleSfxEnabled = true;
    private float _battleBgmVolume = 1.0f;
    private float _battleSfxVolume = 1.0f;
    private double _weatherEffectTime;
    private Vector2 _battleLogExpandedSize = new(360.0f, 370.0f);
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
    private readonly record struct AiOffensiveAction(BattleGridKey SourceGrid, BattleOccupantInfo Unit, BattleGridKey TargetGrid, UnionAttackCandidate? UnionCandidate, AiOutpostObjective? OutpostObjective, AiBridgeEngineeringPlan? BridgePlan, AiFenceEngineeringPlan? FencePlan, int Score, int Noise)
    {
        public BattleGridKey? MoveAttackDestination { get; init; }
        public AiSupplyPlan? SupplyPlan { get; init; }
        public AiBridgeRepairPlan? BridgeRepairPlan { get; init; }
        public AiGateRepairPlan? GateRepairPlan { get; init; }
        public AiExtinguishPlan? ExtinguishPlan { get; init; }
        public AiConfusionPlan? ConfusionPlan { get; init; }
        public AiFirePlan? FirePlan { get; init; }
        public bool IsHideAction { get; init; }
        public bool IsGuardAction { get; init; }
    }
    private readonly record struct AiOutpostObjective(BattleGridKey Grid, int Score, string Reason);
    private enum AiGateSortiePhase
    {
        Exit,
        Engage,
        Return,
        Close
    }
    private sealed record AiGateSortiePlan(
        BattleGridKey GateGrid,
        BattleGridKey TargetGrid,
        BattleGridKey ExitGrid,
        BattlePieceMarker SortieMarker,
        int StartingTroopCount,
        int ExecuteTurn,
        AiGateSortiePhase Phase);
    private readonly record struct AiSupplyPlan(BattleGridKey ActionGrid, bool MoveBeforeSupply, AiSupplyActionKind Kind, int Score, string Reason);
    private readonly record struct AiBridgeRepairPlan(BattleGridKey TargetGrid, int RepairAmount, int Score, string Reason);
    private readonly record struct AiGateRepairPlan(BattleGridKey TargetGrid, int RepairAmount, int Score);
    private enum AiSupplyActionKind
    {
        RecoveryRepair,
        WeaponResupply
    }
    private readonly record struct AiExtinguishPlan(BattleGridKey TargetGrid, int Score, int ProtectedUnits, int ProjectedDamage);
    private readonly record struct AiConfusionPlan(BattleGridKey TargetGrid, int TargetOfficerId, int Score, float SuccessChance);
    private readonly record struct AiFirePlan(BattleGridKey TargetGrid, int Score, int EnemyDamage, int FriendlyDamage, int EnemyTargets, int SpreadTargets);
    private sealed record AiBridgeEngineeringPlan(
        IReadOnlyList<Vector2I> Corridor,
        BattleGridKey WorkGrid,
        BattleGridKey ObjectiveGrid,
        BattleGridKey ActionGrid,
        bool CanWorkNow,
        int PathReduction,
        int Score);
    private enum AiFenceEngineeringAction
    {
        Build,
        Remove
    }
    private sealed record AiFenceEngineeringPlan(
        AiFenceEngineeringAction Action,
        BattleGridKey FenceGrid,
        BattleGridKey ActionGrid,
        bool CanWorkNow,
        BattleGridKey? ProtectedGrid,
        int PathImpact,
        int Score);
    private enum AiSurvivalActionKind
    {
        MoveToSafety,
        AdvanceToRetreatExit,
        Guard,
        Stay,
        Retreat
    }
    private readonly record struct AiSurvivalAction(BattleGridKey SourceGrid, BattleOccupantInfo Unit, BattleGridKey? DestinationGrid, AiSurvivalActionKind Kind, int Score, string Reason);

    [Export]
    public BattleScenarioType ScenarioType { get; set; } = BattleScenarioType.SiegeAssault;

    [Export]
    public Resource? ScenarioDefinition { get; set; }

    [Export]
    public int DropStoneUsesPerUnit { get; set; } = 3;

    [Export]
    public int PourOilUsesPerUnit { get; set; } = 2;

    [Export]
    public bool UseEditorAuthoredLayout { get; set; }

    [Export]
    public bool EnableEnvironmentDebugControls { get; set; }

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

        InitializeBattleRuntimeServices();
        ConnectBattleTurnAndDebugSignals();
        ConnectBattleOptionSignals();
        ConnectBattleCommandSignals();
        ConnectBattlePanelSignals();
        InitializeBattleSceneAndPresentation();
    }

    private void DismissCampaignBattleLoadingOverlay()
    {
        GetTree().Root.GetNodeOrNull<CanvasLayer>(CampaignBattleLoadingOverlayName)?.QueueFree();
    }

    private bool CanUseEnvironmentDebugControls => EnableEnvironmentDebugControls || IsStandaloneBattleAiTest;

    private void InitializeFieldAiTestDefaults()
    {
        if (IsStandaloneBattleAiTest)
        {
            _aiControlledSides = BattleAiControlledSides.Attacker | BattleAiControlledSides.Defender;
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
            : BattleMapData.Create(scenarioDefinition, _objectLayer);

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
        ClearBuildingDepthVisuals();
        // FieldBattle inherits the shared siege scene, whose CastleLayer has parent tiles.
        // Keep that inherited layer hidden in the editor preview so it matches the runtime field map.
        _castleLayer.Visible = scenarioDefinition.ScenarioType != BattleScenarioType.FieldBattle;
        RefreshBattleDepthLayerOrder();
    }

    private void RefreshDefenseOutpostFlag(Vector2I grid)
    {
        if (_mapData == null) return;

        Node2D? flagHost = null;
        if (_buildingDepthSpritesByGrid.TryGetValue(grid, out var sprite))
        {
            flagHost = sprite;
        }
        else if (_staticOutpostFlagHostsByGrid.TryGetValue(grid, out var staticFlagHost))
        {
            flagHost = staticFlagHost;
        }

        if (flagHost == null) return;

        flagHost.GetNodeOrNull<Node2D>("OwnerFlag")?.QueueFree();
        _outpostOwnerFlagsByGrid.Remove(grid);
        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (!cell.IsDefenseOutpost || cell.DefenseOutpostOwner == BattleOutpostOwner.None) return;

        var flagColor = cell.DefenseOutpostOwner == BattleOutpostOwner.Defender ? new Color("2d80c3") : new Color("c64236");
        var flagRoot = new Node2D { Name = "OwnerFlag" };
        flagRoot.AddChild(new Line2D { Points = [new Vector2(6, -74), new Vector2(6, -42)], DefaultColor = new Color("4a3423"), Width = 2.0f });
        flagRoot.AddChild(new Polygon2D { Polygon = [new Vector2(7, -73), new Vector2(28, -65), new Vector2(7, -57)], Color = flagColor });
        flagHost.AddChild(flagRoot);
        _outpostOwnerFlagsByGrid[grid] = flagRoot;
    }

    private bool ShouldKeepFieldOutpostInObjectLayer(BattleCellData cell)
    {
        return cell.IsDefenseOutpost &&
               _mapData?.ScenarioDefinition.ScenarioType == BattleScenarioType.FieldBattle;
    }

    private void CaptureDefenseOutpost(BattleGridKey grid, BattleOccupantInfo occupant)
    {
        if (_mapData == null || grid.Level != 0) return;
        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (!cell.IsDefenseOutpost) return;

        var newOwner = BattleTeamIdentity.ResolveSide(occupant.TeamName) == BattleTurnSide.TeamB
            ? BattleOutpostOwner.Defender
            : BattleOutpostOwner.Attacker;
        if (cell.DefenseOutpostOwner == newOwner) return;

        cell.DefenseOutpostOwner = newOwner;
        RefreshDefenseOutpostFlag(grid.Grid);
        AppendBattleLog(occupant, "Outpost", $"{FormatLogUnit(occupant)} captures defense outpost ({(newOwner == BattleOutpostOwner.Defender ? "Defender" : "Attacker")})");
        TryShowOfficerSpeech(occupant, BattleOfficerSpeechEvent.Capture);
    }


    private static bool IsMessed(BattleOccupantInfo unit)
    {
        return unit.MessTurns > 0;
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
        return GetBattleTeamState(teamName).StrategyPlans;
    }

    private bool TrySpendStrategyPlan(string teamName)
    {
        var team = GetBattleTeamState(teamName);
        if (team.StrategyPlans <= 0)
        {
            return false;
        }

        team.StrategyPlans--;
        return true;
    }

    private void RegisterOccupant(BattleGridKey grid, string displayName, string category, string shortLabel, string teamName, string officerName, string troopType, int troopCount, int moveRange, int attackRange, BattlePieceMarker? marker, int campaignTeamId = 0, int factionId = 0, CampaignControllerType controllerType = CampaignControllerType.Ai)
    {
        var morale = GetInitialMorale(category, troopType);
        var weaponAmmo = GetInitialWeaponAmmo(category, troopType);
        _occupantsByGrid.Add(grid, new BattleOccupantInfo(displayName, category, shortLabel, teamName, officerName, troopType, troopCount, troopCount, troopCount, WoundedTroops: 0, MessTurns: 0, IsHidden: false, morale, weaponAmmo, weaponAmmo, moveRange, attackRange, marker, BattleSpriteDirection.SouthEast, DefaultUnitEnergy, HasAttackedThisTurn: false, RemainingMoveRange: moveRange, IsGuarding: false, GuardCounterAvailable: false, GuardDamageReductionCount: 0, campaignTeamId, factionId, controllerType));
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

    private void UpdateMarkerStatusIndicator(BattleOccupantInfo occupant)
    {
        if (occupant.Marker == null)
        {
            return;
        }

        if (IsMessed(occupant))
        {
            occupant.Marker.SetupStatusIndicator(BattleText("ui.battle.status_mess_badge", "MESS"), new Color(1.0f, 0.34f, 0.16f, 0.92f));
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
        return _occupantsByGrid.GetAtGrid(grid);
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

    private void OnGuardActionRequested()
    {
        TryExecuteSelectedBattleAction(BattleActionKind.Guard);
    }

    private void OnHideActionRequested()
    {
        TryExecuteSelectedBattleAction(BattleActionKind.Hide);
    }

    private void OnRetreatActionRequested()
    {
        TryExecuteSelectedBattleAction(BattleActionKind.Retreat);
    }

    private void ReplaceOccupantAtGrid(BattleGridKey grid, BattleOccupantInfo oldOccupant, BattleOccupantInfo newOccupant)
    {
        if (_occupantsByGrid.Replace(grid, oldOccupant, newOccupant))
        {
            UpdateMarkerStatusIndicator(newOccupant);
        }
    }

    private bool TryGetCurrentOccupantAtGrid(BattleGridKey grid, BattleOccupantInfo occupant, out BattleOccupantInfo currentOccupant)
    {
        return _occupantsByGrid.TryGetCurrent(grid, occupant, out currentOccupant);
    }

    private bool IsOccupantAtGrid(BattleGridKey grid, BattleOccupantInfo occupant)
    {
        return _occupantsByGrid.Contains(grid, occupant);
    }

    private void RemoveOccupant(BattleGridKey grid, BattleOccupantInfo occupant)
    {
        if (!_occupantsByGrid.Remove(grid, occupant))
        {
            return;
        }

        HandleCampaignTeamRemoved(occupant);

        ApplyTeamSiegeUnitDelta(occupant.Category, occupant.TeamName, -1);
        ApplyTeamGeneralDelta(occupant.Category, occupant.TeamName, occupant.OfficerName, -1);
        if (occupant.Marker != null)
        {
            _battleDepthEntries.Remove(occupant.Marker);
            occupant.Marker.QueueFree();
        }

        if (_selectedUnit == occupant)
        {
            _selectedUnit = null;
            _selectedUnitGrid = null;
            _selectedGridKey = null;
        }
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

    private int GetDuelBattleScore(BattleOccupantInfo unit)
    {
        return GetOfficerBattleAttribute(unit.OfficerName);
    }

    private int GetOfficerBattleAttribute(string officerName)
    {
        return GetBattleOfficerData(officerName)?.Combat ?? BattleOfficerAiProfiles.GetCombatAttribute(officerName);
    }

    private int GetOfficerCommandRating(BattleOccupantInfo unit)
    {
        if (!IsGeneralCountedPiece(unit.Category, unit.OfficerName))
        {
            return 50;
        }

        var officer = GetBattleOfficerData(unit.OfficerName);
        if (officer != null)
        {
            return Mathf.RoundToInt(officer.Leadership * 0.45f + officer.Combat * 0.35f + officer.Strength * 0.20f);
        }

        // Prototype-only officers have no complete WorldState data.  Their
        // tactical combat attribute remains a useful neutral fallback.
        return GetOfficerBattleAttribute(unit.OfficerName);
    }

    private float GetOfficerCommandMultiplier(BattleOccupantInfo unit)
    {
        var rawBonus = (GetOfficerCommandRating(unit) - 50) / 200.0f;
        return Mathf.Clamp(1.0f + rawBonus, OfficerCommandMinimumMultiplier, OfficerCommandMaximumMultiplier);
    }

    private void ExecuteSelectedGuard()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || !CanUseGuard(_selectedUnit))
        {
            return;
        }

        var guardedUnit = _selectedUnit with
        {
            Energy = _selectedUnit.Energy - NormalAttackEnergyCost,
            HasAttackedThisTurn = true,
            IsGuarding = true,
            GuardCounterAvailable = true,
            GuardDamageReductionCount = 0
        };
        ReplaceOccupantAtGrid(_selectedUnitGrid.Value, _selectedUnit, guardedUnit);
        _selectedUnit = guardedUnit;
        MarkUnitActed(guardedUnit);
        AppendBattleLog(guardedUnit, "Guard", $"{FormatLogUnit(guardedUnit)} enters guard stance.");
        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private void ExecuteSelectedRetreat()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !CanRetreatFromGrid(_selectedUnitGrid.Value, _selectedUnit))
        {
            return;
        }

        var retreatingUnit = _selectedUnit;
        var retreatingGrid = _selectedUnitGrid.Value;
        if (TryHandleCampaignRetreat(retreatingUnit, retreatingGrid))
        {
            return;
        }

        CompleteSelectedRetreat(retreatingUnit, retreatingGrid, campaignReturnHandled: false);
    }

    private void CompleteSelectedRetreat(
        BattleOccupantInfo retreatingUnit,
        BattleGridKey retreatingGrid,
        bool campaignReturnHandled,
        bool isArrival = false)
    {
        if (!campaignReturnHandled)
        {
            ApplyRetreatTroopLoss(retreatingUnit);
        }

        ShowRetreatNotice(retreatingUnit, retreatingGrid, isArrival);
        AppendBattleLog(
            retreatingUnit,
            isArrival ? "Arrive" : "Retreat",
            isArrival
                ? $"{FormatLogUnit(retreatingUnit)} arrives at the destination city."
                : $"{FormatLogUnit(retreatingUnit)} retreats from {retreatingGrid}");
        RemoveOccupant(retreatingGrid, retreatingUnit);
        // A retreating/arriving escort team is no longer on the battlefield.
        // Rebuild the live HUD totals so its troops are excluded from both the
        // display and subsequent per-period escort supply calculations.
        RecalculateBattleHudTotals();

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
        MarkUnitActed(recruiter);

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

    private IEnumerable<KeyValuePair<BattleGridKey, IReadOnlyList<BattleOccupantInfo>>> GetHireOfficerRangeOccupantEntries(BattleGridKey sourceGrid)
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

    private IEnumerable<KeyValuePair<BattleGridKey, IReadOnlyList<BattleOccupantInfo>>> GetAdjacentOccupantEntries(BattleGridKey sourceGrid)
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

    private void RefreshMarkerTeamVisual(BattleOccupantInfo occupant)
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
        occupant.Marker.SetupNamePlate(FormatMarkerName(occupant.OfficerName, occupant.DisplayName, occupant.TroopType));
        occupant.Marker.SetupTeamArrow(GetTeamArrowColor(occupant.TeamName, occupant.CampaignTeamId));
        UpdateMarkerStrengthBar(occupant);
    }

    private void ApplyRetreatTroopLoss(BattleOccupantInfo retreatingUnit)
    {
        HandleCampaignTeamRetreat(retreatingUnit);
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
        if (category != CategoryUnit || delta == 0 || !TryGetBattleTeamState(teamName, out var team))
        {
            return;
        }

        team.TotalTroops = Mathf.Max(0, team.TotalTroops + delta);
    }

    private void ApplyTeamSiegeUnitDelta(string category, string teamName, int delta)
    {
        if (category != CategorySiegeEngine || delta == 0 || !TryGetBattleTeamState(teamName, out var team))
        {
            return;
        }

        team.SiegeUnits = Mathf.Max(0, team.SiegeUnits + delta);
    }

    private void ApplyTeamGeneralDelta(string category, string teamName, string officerName, int delta)
    {
        if (!IsGeneralCountedPiece(category, officerName) ||
            delta == 0 ||
            !TryGetBattleTeamState(teamName, out var team))
        {
            return;
        }

        team.Generals = Mathf.Max(0, team.Generals + delta);
    }

    private static bool IsGeneralCountedPiece(string category, string officerName)
    {
        return category == CategoryUnit &&
               !string.IsNullOrWhiteSpace(officerName) &&
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

    private void ExecuteSelectedHide()
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
        MarkUnitActed(hiddenUnit);
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
        MarkUnitActed(attackingUnit);
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

    private static bool IsCellBlockingMovement(BattleCellData cell)
    {
        return cell.IsBlockingStructure;
    }

    private static bool IsAttackerPiece(BattleOccupantInfo occupant)
    {
        return BattleTeamIdentity.IsAttacker(occupant.TeamName);
    }

    private static bool IsDefenderPiece(BattleOccupantInfo occupant)
    {
        return BattleTeamIdentity.IsDefender(occupant.TeamName);
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

    private void HandleCommandMenuDragInput(InputEvent @event)
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
        // CommandMenu is a NinePatchRect whose content is full-anchored.  That
        // layout does not feed its height back into the parent's minimum size,
        // so measure the button stack directly before sizing the frame.
        var menuButtons = _commandMenu.GetNodeOrNull<Control>("MenuMargin/MenuButtons");
        var minimumSize = menuButtons?.GetCombinedMinimumSize() ?? Vector2.Zero;
        minimumSize += new Vector2(52.0f, 48.0f);
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
            BattleTerrainType.River => BattleText("ui.battle.terrain_river", "River"),
            BattleTerrainType.Swamp => BattleText("ui.battle.terrain_swamp", "Swamp"),
            BattleTerrainType.Coast => BattleText("ui.battle.terrain_coast", "Coast"),
            BattleTerrainType.Hill => BattleText("ui.battle.terrain_hill", "Hill"),
            BattleTerrainType.Mountain => BattleText("ui.battle.terrain_mountain", "Mountain"),
            BattleTerrainType.Farm => BattleText("ui.battle.terrain_farm", "Farm"),
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
        var usableScreenRect = GetMapUsableScreenRect(viewportSize);
        var viewportCenter = viewportSize * 0.5f;
        var screenCenter = usableScreenRect.GetCenter();
        var cameraCenter = _camera?.GlobalPosition ?? viewportCenter;
        var center = cameraCenter + ((screenCenter - viewportCenter) * zoom);
        var worldSize = usableScreenRect.Size * zoom;

        return new Rect2(center - (worldSize * 0.5f), worldSize);
    }

    private Rect2 GetMapUsableScreenRect(Vector2 viewportSize)
    {
        // The battle HUD floats above the battlefield. Do not reserve its height
        // for camera clamping, otherwise transparent gaps between the cards show
        // the viewport clear color instead of the map.
        const float top = 0.0f;
        var right = viewportSize.X;
        foreach (var panel in new[] { _tileInfoPanel, _battleLogPanel })
        {
            if (panel?.Visible != true)
            {
                continue;
            }

            var panelRect = panel.GetGlobalRect();
            if (panelRect.Position.X > 0.0f && panelRect.Position.X < right)
            {
                right = panelRect.Position.X;
            }
        }

        // Keep a usable area even in unusually narrow windows or while a panel is resized.
        right = Mathf.Max(160.0f, right);
        return new Rect2(0.0f, top, right, Mathf.Max(160.0f, viewportSize.Y - top));
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

    private readonly record struct WallTopAttackAmmo(int DropStoneUses, int PourOilUses);
    private readonly record struct BattleFireState(int RemainingTurns, int BurnTurns);

    private sealed record BattleHudTeamInfo(string Name, int TotalTroops, int WoundedTroops, int TotalGenerals, int TotalSiegeUnits, int StrategyPlans, int TotalGold, int TotalFood);

}
