using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ThreeKingdom.Core;
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
    public sealed record LaunchOptions(BattleScenarioType ScenarioType, bool UseEditorAuthoredLayout);

    private enum BattleOfficerSpeechEvent
    {
        Opening,
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
        TerrainSwamp
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
    private static readonly BattleHudTeamInfo TeamAInfo = new("Team A / Attacker", 0, 0, 0, 0, 0, InitialTeamAGold, InitialTeamAFood);
    private static readonly BattleHudTeamInfo TeamBInfo = new("Team B / Defender", 0, 0, 0, 0, 0, InitialTeamBGold, InitialTeamBFood);
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
    private Control? _officerSpeechOverlay;
    private TextureRect? _officerSpeechPortrait;
    private Label? _officerSpeechTeamNameLabel;
    private Label? _officerSpeechNameLabel;
    private Label? _officerSpeechTextLabel;
    private Button? _allLogButton;
    private Button? _selfLogButton;
    private Button? _minimizeLogButton;
    private Label? _battleLogLabel;
    private Label? _battleLogTitleLabel;
    private Label? _windowTitleLabel;
    private Label? _unitMenuInfoLabel;
    private TextureRect? _officerPortrait;
    private readonly Dictionary<string, Texture2D> _officerPortraitTextures = new(StringComparer.Ordinal);
    private Button? _endTurnButton;
    private Button? _enableAiButton;
    private Button? _disableAiButton;
    private Button? _startRoundButton;
    private Button? _nextAiButton;
    private Button? _attackerOneDayFoodButton;
    private Button? _defenderOneDayFoodButton;
    private Label? _aiRoundStatusLabel;
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
    private bool _isDraggingCommandMenu;
    private bool _isDraggingBattleLog;
    private bool _isResizingBattleLog;
    private bool _isBattleLogMinimized;
    private readonly BattleState _state = new();
    private bool _isBattleFinished { get => _state.IsBattleFinished; set => _state.IsBattleFinished = value; }
    private int _retreatNoticeSerial;
    private int _officerCaptureNoticeSerial;
    private int _turnBannerSerial;
    private int _officerSpeechSerial;
    private int _activeOfficerSpeechPriority;
    private readonly List<BattleOfficerSpeechEntry> _officerSpeechEntries = new();
    private readonly Dictionary<string, ulong> _officerSpeechLastShownAt = new(StringComparer.Ordinal);
    private readonly Random _officerSpeechRandom = new();
    private bool _attackerOutpostVictorySecured { get => _state.AttackerOutpostVictorySecured; set => _state.AttackerOutpostVictorySecured = value; }
    private Vector2 _lastMousePosition;
    private Vector2 _commandMenuDragOffset;
    private Vector2 _battleLogDragOffset;
    private Vector2 _battleLogResizeStartMouse;
    private Vector2 _battleLogResizeStartSize;
    private Vector2I? _hoverGrid { get => _state.HoverGrid; set => _state.HoverGrid = value; }
    private Vector2I? _selectedGrid { get => _state.SelectedGrid; set => _state.SelectedGrid = value; }
    private BattleGridKey? _hoverGridKey { get => _state.HoverGridKey; set => _state.HoverGridKey = value; }
    private BattleGridKey? _selectedGridKey { get => _state.SelectedGridKey; set => _state.SelectedGridKey = value; }
    private BattleGridKey? _selectedUnitGrid { get => _state.SelectedUnitGrid; set => _state.SelectedUnitGrid = value; }
    private BattleOccupantInfo? _selectedUnit { get => _state.SelectedUnit; set => _state.SelectedUnit = value; }
    private HashSet<BattleGridKey> _movableGrids => _state.MovableGrids;
    private HashSet<BattleGridKey> _attackableGrids => _state.AttackableGrids;
    private HashSet<BattleGridKey> _workableGrids => _state.WorkableGrids;
    private HashSet<BattleGridKey> _strategyTargetGrids => _state.StrategyTargetGrids;
    private HashSet<BattleGridKey> _duelTargetGrids => _state.DuelTargetGrids;
    private HashSet<BattleGridKey> _chargeTargetGrids => _state.ChargeTargetGrids;
    private HashSet<BattleGridKey> _hireOfficerTargetGrids => _state.HireOfficerTargetGrids;
    private BattleUnitRepository _occupantsByGrid => _state.Units;
    private readonly Dictionary<Node2D, BattleDepthEntry> _battleDepthEntries = new();
    private readonly Dictionary<Vector2I, Sprite2D> _castleDepthSpritesByGrid = new();
    private readonly Dictionary<Vector2I, Sprite2D> _buildingDepthSpritesByGrid = new();
    private readonly Dictionary<Vector2I, Node2D> _staticOutpostFlagHostsByGrid = new();
    private readonly Dictionary<Vector2I, Node2D> _outpostOwnerFlagsByGrid = new();
    private readonly List<BattleHighlightRenderer> _highlightDepthVisuals = new();
    private readonly Dictionary<BattleGridKey, Node2D> _occludedUnitSilhouettesByGrid = new();
    private readonly Dictionary<BattlePieceMarker, WallTopAttackAmmo> _wallTopAttackAmmoByMarker = new();
    private readonly Dictionary<BattleGridKey, BattleFireState> _activeFireByGrid = new();
    private readonly Dictionary<BattleGridKey, Node2D> _fireVisualsByGrid = new();
    private HashSet<BattlePieceMarker> _strategyUsedByMarkerThisTurn => _state.StrategyUsedByMarkerThisTurn;
    private HashSet<BattlePieceMarker> _supplyUsedByMarkerThisTurn => _state.SupplyUsedByMarkerThisTurn;
    private HashSet<BattlePieceMarker> _chargeUsedByMarkerThisTurn => _state.ChargeUsedByMarkerThisTurn;
    private HashSet<BattlePieceMarker> _actedByMarkerThisRound => _state.ActedByMarkerThisRound;
    private readonly Dictionary<BattlePieceMarker, AiBridgeEngineeringPlan> _aiBridgePlanByWorker = new();
    private readonly List<BattleLogEntry> _battleLogs = new();
    private readonly List<ColorRect> _rainStreaks = new();
    private BattleCommandMode _commandMode { get => _state.CommandMode; set => _state.CommandMode = value; }
    private BattleStrategyAction _selectedStrategyAction { get => _state.SelectedStrategyAction; set => _state.SelectedStrategyAction = value; }
    private WorkerWorkAction _workerWorkAction { get => _state.WorkerWorkAction; set => _state.WorkerWorkAction = value; }
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
    private readonly record struct AiOffensiveAction(BattleGridKey SourceGrid, BattleOccupantInfo Unit, BattleGridKey TargetGrid, UnionAttackCandidate? UnionCandidate, AiOutpostObjective? OutpostObjective, AiBridgeEngineeringPlan? BridgePlan, AiFenceEngineeringPlan? FencePlan, int Score, int Noise)
    {
        public BattleGridKey? MoveAttackDestination { get; init; }
        public AiSupplyPlan? SupplyPlan { get; init; }
        public AiBridgeRepairPlan? BridgeRepairPlan { get; init; }
        public AiExtinguishPlan? ExtinguishPlan { get; init; }
        public AiFirePlan? FirePlan { get; init; }
        public bool IsHideAction { get; init; }
        public bool IsGuardAction { get; init; }
    }
    private readonly record struct AiOutpostObjective(BattleGridKey Grid, int Score, string Reason);
    private readonly record struct AiSupplyPlan(BattleGridKey ActionGrid, bool MoveBeforeSupply, AiSupplyActionKind Kind, int Score, string Reason);
    private readonly record struct AiBridgeRepairPlan(BattleGridKey TargetGrid, int RepairAmount, int Score, string Reason);
    private enum AiSupplyActionKind
    {
        RecoveryRepair,
        WeaponResupply
    }
    private readonly record struct AiExtinguishPlan(BattleGridKey TargetGrid, int Score, int ProtectedUnits, int ProjectedDamage);
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
        LoadOfficerSpeechCatalog();
        ApplyPendingLaunchOptions();
        StartBattleBgm();

        if (_endTurnButton != null)
        {
            _endTurnButton.Pressed += OnEndTurnButtonPressed;
        }

        if (_enableAiButton != null)
        {
            _enableAiButton.Pressed += OnEnableAiButtonPressed;
        }

        if (_disableAiButton != null)
        {
            _disableAiButton.Pressed += OnDisableAiButtonPressed;
        }

        if (_startRoundButton != null)
        {
            _startRoundButton.Pressed += OnStartRoundButtonPressed;
        }

        if (_nextAiButton != null)
        {
            _nextAiButton.Pressed += OnNextAiButtonPressed;
        }

        if (_attackerOneDayFoodButton != null)
        {
            _attackerOneDayFoodButton.Pressed += OnAttackerOneDayFoodButtonPressed;
        }

        if (_defenderOneDayFoodButton != null)
        {
            _defenderOneDayFoodButton.Pressed += OnDefenderOneDayFoodButtonPressed;
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

        if (_guardButton != null)
        {
            _guardButton.Pressed += OnGuardActionRequested;
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
            _retreatButton.Pressed += OnRetreatActionRequested;
        }

        if (_hideButton != null)
        {
            _hideButton.Pressed += OnHideActionRequested;
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
            _supplyButton.Pressed += OnSupplyActionRequested;
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
        BuildBuildingDepthVisuals();
        PopulateMarkers();
        InitializeFieldAiTestDefaults();
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

        ShowOpeningOfficerSpeechAfterDelay();
        ShowTurnBanner();
    }

    private void InitializeFieldAiTestDefaults()
    {
        if (ScenarioType == BattleScenarioType.FieldBattle)
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

        var newOwner = occupant.TeamName.Contains("Defender", StringComparison.OrdinalIgnoreCase) ? BattleOutpostOwner.Defender : BattleOutpostOwner.Attacker;
        if (cell.DefenseOutpostOwner == newOwner) return;

        cell.DefenseOutpostOwner = newOwner;
        RefreshDefenseOutpostFlag(grid.Grid);
        AppendBattleLog(occupant, "Outpost", $"{FormatLogUnit(occupant)} captures defense outpost ({(newOwner == BattleOutpostOwner.Defender ? "Defender" : "Attacker")})");
        TryShowOfficerSpeech(occupant, BattleOfficerSpeechEvent.Capture);
    }


    private void ConfigureHud()
    {
        var titleLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/TitleLabel");
        var summaryLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/SummaryLabel");
        var teamBLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/TeamBLabel");
        var coordinateLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/CoordinateLabel");

        var scenarioName = FormatScenarioName(ResolveScenarioDefinition().DisplayName);
        if (titleLabel != null)
        {
            titleLabel.Text = BattleFormat(
                "ui.battle.title",
                "Scenario: {0}   Date: {1}   Turn: {2}   Acting Side: {3}",
                scenarioName,
                FormatBattleDate(),
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

        ConfigureFieldAiTestControls();

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

    private string FormatScenarioName(string scenarioName)
    {
        return scenarioName switch
        {
            "Field Battle (Luoyang)" => BattleText("ui.battle.scenario.field_luoyang", "Field Battle (Luoyang)"),
            "Field Battle (Hanzhong)" => BattleText("ui.battle.scenario.field_hanzhong", "Field Battle (Hanzhong)"),
            "Field Battle (Ye)" => BattleText("ui.battle.scenario.field_ye", "Field Battle (Ye)"),
            "Field Battle (Jinyang)" => BattleText("ui.battle.scenario.field_jinyang", "Field Battle (Jinyang)"),
            "Field Battle (Xiapi)" => BattleText("ui.battle.scenario.field_xiapi", "Field Battle (Xiapi)"),
            "Field Battle (Jianye)" => BattleText("ui.battle.scenario.field_jianye", "Field Battle (Jianye)"),
            "Field Battle (Xiangyang)" => BattleText("ui.battle.scenario.field_xiangyang", "Field Battle (Xiangyang)"),
            "Field Battle (Jiangling)" => BattleText("ui.battle.scenario.field_jiangling", "Field Battle (Jiangling)"),
            _ => scenarioName
        };
    }

    private string FormatBattleDate()
    {
        return BattleFormat("ui.battle.date", "{0} Apr {1}", _battleDateYear, _battleDateDay, _battleDateMonth);
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

    private void RegisterOccupant(BattleGridKey grid, string displayName, string category, string shortLabel, string teamName, string officerName, string troopType, int troopCount, int moveRange, int attackRange, BattlePieceMarker? marker)
    {
        var morale = GetInitialMorale(category, troopType);
        var weaponAmmo = GetInitialWeaponAmmo(category, troopType);
        _occupantsByGrid.Add(grid, new BattleOccupantInfo(displayName, category, shortLabel, teamName, officerName, troopType, troopCount, troopCount, troopCount, WoundedTroops: 0, MessTurns: 0, IsHidden: false, morale, weaponAmmo, weaponAmmo, moveRange, attackRange, marker, BattleSpriteDirection.SouthEast, DefaultUnitEnergy, HasAttackedThisTurn: false, RemainingMoveRange: moveRange, IsGuarding: false, GuardCounterAvailable: false, GuardDamageReductionCount: 0));
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

    private void OnSupplyActionRequested()
    {
        TryExecuteSelectedBattleAction(BattleActionKind.Supply);
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

    private bool TryPerformWork()
    {
        if (_mapData == null ||
            _selectedUnit == null ||
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
        var workEnergyCost = GetWorkEnergyCost(_selectedUnit, targetCell, _workerWorkAction);
        if (_selectedUnit.Energy < workEnergyCost)
        {
            return false;
        }

        var isWorker = _selectedUnit.TroopType == TroopWorker;
        if (!ApplyWork(_selectedUnit, targetGrid.Grid, targetCell, out var removedWoodFence))
        {
            return false;
        }

        var workDirection = GetInfantryDirection(sourceGrid.Grid, targetGrid.Grid);
        var workingUnit = _selectedUnit with
        {
            FacingDirection = workDirection,
            Energy = _selectedUnit.Energy - workEnergyCost,
            HasAttackedThisTurn = _selectedUnit.HasAttackedThisTurn || !isWorker
        };
        ReplaceOccupantAtGrid(sourceGrid, _selectedUnit, workingUnit);
        _selectedUnit = workingUnit;
        if (isWorker)
        {
            workingUnit.Marker?.PlayAction(
                GetWorkerWorkScene(workDirection),
                GetWorkerIdleScene(workDirection),
                WorkerWorkAnimationDurationSeconds);
        }

        var workDescription = isWorker
            ? FormatWorkerWorkAction(_workerWorkAction, removedWoodFence)
            : "repairs bridge";
        AppendBattleLog(workingUnit, "Action", $"{FormatLogUnit(workingUnit)} {workDescription} at {targetGrid} (energy {workEnergyCost})");

        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        MarkUnitActed(workingUnit);
        return true;
    }

    private static int GetWorkerWorkEnergyCost(BattleCellData targetCell, WorkerWorkAction workAction)
    {
        if (workAction != WorkerWorkAction.WoodFence)
        {
            return 0;
        }

        return targetCell.Structure == BattleStructureType.WoodenFence
            ? WorkerRemoveWoodFenceEnergyCost
            : WorkerInstallWoodFenceEnergyCost;
    }

    private static int GetWorkEnergyCost(BattleOccupantInfo unit, BattleCellData targetCell, WorkerWorkAction workAction)
    {
        return unit.TroopType == TroopWorker
            ? GetWorkerWorkEnergyCost(targetCell, workAction)
            : BattleBridgeSystem.EmergencyRepairEnergyCost;
    }

    private bool ApplyWork(BattleOccupantInfo unit, Vector2I targetGrid, BattleCellData targetCell, out bool removedWoodFence)
    {
        if (unit.TroopType == TroopWorker)
        {
            return ApplyWorkerWork(targetGrid, targetCell, out removedWoodFence);
        }

        removedWoodFence = false;
        if (!BattleBridgeSystem.CanEmergencyRepair(unit) ||
            !BattleBridgeSystem.IsEmergencyRepairTarget(targetCell))
        {
            return false;
        }

        var repairedHp = BattleBridgeSystem.ApplyRepair(targetCell, BattleBridgeSystem.EmergencyRepairAmount);
        if (repairedHp <= 0)
        {
            return false;
        }

        ShowRepairPopup(GetDefaultGridKey(targetGrid), repairedHp);
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

        if ((targetCell.Terrain == BattleTerrainType.Moat &&
             _mapData.ScenarioDefinition.ScenarioType == BattleScenarioType.MoatSiegeBattle) ||
            (targetCell.Terrain == BattleTerrainType.River &&
             _mapData.ScenarioDefinition.ScenarioType == BattleScenarioType.FieldBattle))
        {
            var isWoodenBridge = targetCell.Terrain == BattleTerrainType.River;
            targetCell.Terrain = BattleTerrainType.Bridge;
            targetCell.HasBridgeVisual = true;
            targetCell.BridgeFlipHorizontally = _mapData.ScenarioDefinition.DefaultStructureFacing == BattleStructureFacing.NorthWest;
            targetCell.BridgeAtlasSourceId = 8;
            targetCell.BridgeAtlasCoords = new Vector2I(0, 0);
            targetCell.IsWoodenBridge = isWoodenBridge;
            targetCell.IsBridgeUnderConstruction = true;
            targetCell.BridgeRestoresToRiver = isWoodenBridge;
            targetCell.BridgeMaxHealth = isWoodenBridge ? BattleCellData.WoodenBridgeMaxDurability : BattleCellData.BridgeMaxDurability;
            targetCell.BridgeHealth = isWoodenBridge ? BattleCellData.WoodenBridgeConstructionStep : BattleCellData.BridgeConstructionStep;
            targetCell.BlocksMovement = true;
            RefreshWorkerObjectLayers();
            return true;
        }

        if (targetCell.IsBridgeDamaged)
        {
            var repairedHp = BattleBridgeSystem.ApplyRepair(targetCell, WorkerBridgeRepairAmount);
            if (repairedHp > 0)
            {
                ShowRepairPopup(GetDefaultGridKey(targetGrid), repairedHp);
                return true;
            }
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
        RefreshWorkerFenceLayer();
    }

    private void RefreshWorkerFenceLayer()
    {
        if (_workerFenceLayer != null && _mapData != null)
        {
            BattleTileMapBuilder.ConfigureWorkerFenceLayer(_workerFenceLayer, _mapData);
            _workerFenceLayer.Visible = true;
        }
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

    private void ResolveDailyBattleSupply()
    {
        (_teamAGold, _teamAFood) = ResolveDailyBattleSupplyForTeam(TeamAInfo.Name, _teamAGold, _teamAFood, _teamATotalTroops);
        (_teamBGold, _teamBFood) = ResolveDailyBattleSupplyForTeam(TeamBInfo.Name, _teamBGold, _teamBFood, _teamBTotalTroops);
        UpdateTeamZeroFoodDays(TeamAInfo.Name);
        UpdateTeamZeroFoodDays(TeamBInfo.Name);
    }

    private (int Gold, int Food) ResolveDailyBattleSupplyForTeam(string teamName, int gold, int food, int activeTroops)
    {
        var result = BattleSupplySystem.ResolveDailyUpkeep(gold, food, activeTroops);
        if (result.FoodNeed <= 0 && result.GoldNeed <= 0)
        {
            return (gold, food);
        }

        AppendBattleLog(
            teamName,
            "Supply",
            $"Daily upkeep: food -{result.FoodSpent:N0}/{result.FoodNeed:N0}, gold -{result.GoldSpent:N0}/{result.GoldNeed:N0}");

        if (result.FoodNeed <= 0)
        {
            return (result.Gold, result.Food);
        }

        if (result.IsFoodShortage)
        {
            ApplyTeamMoralePenalty(teamName, StarvingMoralePenalty, "food shortage");
            ApplyTeamStarvationDesertion(teamName);
            return (result.Gold, result.Food);
        }

        if (result.IsLowFood)
        {
            ApplyTeamMoralePenalty(teamName, LowFoodMoralePenalty, "low food");
        }

        return (result.Gold, result.Food);
    }

    private void ApplyTeamStarvationDesertion(string teamName)
    {
        var affectedUnits = GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName == teamName &&
                            entry.Occupant.Category == CategoryUnit &&
                            entry.Occupant.TroopCount > 0)
            .ToList();
        foreach (var (grid, unit) in affectedUnits)
        {
            if (!TryGetCurrentOccupantAtGrid(grid, unit, out var currentUnit))
            {
                continue;
            }

            var deserters = Math.Max(1, Mathf.FloorToInt(currentUnit.TroopCount * (StarvationDesertionPercent / 100.0f)));
            deserters = Math.Min(deserters, currentUnit.TroopCount);
            var updatedUnit = currentUnit with
            {
                TroopCount = currentUnit.TroopCount - deserters,
                HitPoints = Math.Max(0, currentUnit.HitPoints - deserters)
            };
            UpdateMarkerStrengthBar(updatedUnit);
            ReplaceOccupantAtGrid(grid, currentUnit, updatedUnit);
            ApplyTeamTroopLoss(currentUnit, deserters);
            ShowDamagePopup(grid, deserters);
            AppendBattleLog(updatedUnit, "Supply", $"Food shortage: {deserters:N0} troop(s) leave battle.");
            if (_selectedUnit == currentUnit)
            {
                _selectedUnit = updatedUnit;
            }

            if (updatedUnit.HitPoints <= 0)
            {
                DestroyOccupantAfterDelay(grid, updatedUnit, 0.0);
            }
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
        return BattleSupplySystem.CalculateScaledResourceNeed(activeTroops, per100Troops);
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
        return GetBattleTeamState(teamName).Gold;
    }

    private int GetTeamFood(string teamName)
    {
        return GetBattleTeamState(teamName).Food;
    }

    private void UpdateTeamZeroFoodDays(string teamName)
    {
        var hasActiveTroops = GetTeamActiveTroops(teamName) > 0;
        var zeroFoodDays = hasActiveTroops && GetTeamFood(teamName) <= 0
            ? GetTeamZeroFoodDays(teamName) + 1
            : 0;
        SetTeamZeroFoodDays(teamName, zeroFoodDays);
    }

    private int GetTeamZeroFoodDays(string teamName)
    {
        return GetBattleTeamState(teamName).ZeroFoodDays;
    }

    private void SetTeamZeroFoodDays(string teamName, int zeroFoodDays)
    {
        GetBattleTeamState(teamName).ZeroFoodDays = Math.Max(0, zeroFoodDays);
    }

    private int GetTeamEnergyCap(string teamName)
    {
        if (GetTeamFood(teamName) > 0)
        {
            return DefaultUnitEnergy;
        }

        return Math.Max(1, GetTeamZeroFoodDays(teamName)) switch
        {
            1 => FirstZeroFoodEnergyCap,
            2 => SecondZeroFoodEnergyCap,
            _ => SustainedZeroFoodEnergyCap
        };
    }

    private int GetTeamActiveTroops(string teamName)
    {
        return GetBattleTeamState(teamName).TotalTroops;
    }

    private bool IsAiAttackerHiddenEnemyFortressMission(string teamName)
    {
        return teamName.Contains("Attacker") &&
               !GetAllBattlePieces().Any(entry =>
                   entry.Occupant.TeamName != teamName &&
                   !IsHiddenFromSide(entry.Occupant, teamName));
    }

    private int GetAiEnemyFoodPressureScore(string teamName)
    {
        var enemyTeamName = teamName.Contains("Attacker") ? TeamBInfo.Name : TeamAInfo.Name;
        var enemyDailyFoodNeed = CalculateDailyFoodNeed(GetTeamActiveTroops(enemyTeamName));
        var ownDailyFoodNeed = CalculateDailyFoodNeed(GetTeamActiveTroops(teamName));
        if (enemyDailyFoodNeed <= 0 || ownDailyFoodNeed <= 0)
        {
            return 0;
        }

        var enemyFoodDays = GetTeamFood(enemyTeamName) / (float)enemyDailyFoodNeed;
        var ownFoodDays = GetTeamFood(teamName) / (float)ownDailyFoodNeed;
        if (enemyFoodDays >= 2.0f || ownFoodDays <= enemyFoodDays)
        {
            return 0;
        }

        return enemyFoodDays <= 1.0f
            ? AiCriticalEnemyFoodPressureScore
            : AiLowEnemyFoodPressureScore;
    }

    private void ApplyTeamResourceDelta(string teamName, int goldDelta, int foodDelta)
    {
        var team = GetBattleTeamState(teamName);
        team.Gold = Math.Max(0, team.Gold + goldDelta);
        team.Food = Math.Max(0, team.Food + foodDelta);
        if (team.Food > 0)
        {
            team.ZeroFoodDays = 0;
        }
    }

    private BattleTeamState GetBattleTeamState(string teamName)
    {
        return teamName.Contains("Attacker", StringComparison.OrdinalIgnoreCase)
            ? _state.TeamA
            : _state.TeamB;
    }

    private bool TryGetBattleTeamState(string teamName, out BattleTeamState team)
    {
        if (teamName.Contains("Attacker", StringComparison.OrdinalIgnoreCase))
        {
            team = _state.TeamA;
            return true;
        }

        if (teamName.Contains("Defender", StringComparison.OrdinalIgnoreCase))
        {
            team = _state.TeamB;
            return true;
        }

        team = null!;
        return false;
    }

    private static bool UsesWeaponAmmo(BattleOccupantInfo unit)
    {
        return BattleSupplySystem.UsesWeaponAmmo(unit);
    }

    private static bool HasWeaponAmmo(BattleOccupantInfo unit)
    {
        return BattleSupplySystem.HasWeaponAmmo(unit);
    }

    private static bool CanUseAmmoDepletedWeakAttack(BattleOccupantInfo unit)
    {
        return BattleSupplySystem.CanUseAmmoDepletedWeakAttack(unit);
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
        return BattleSupplySystem.TrySpendWeaponAmmo(unit, out updatedUnit);
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
        if (!BattleSupplySystem.TryRefillWeaponAmmo(target, out var updatedTarget, out refilledAmmo))
        {
            return false;
        }

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
        return BattleOfficerAiProfiles.GetCombatAttribute(officerName);
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
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || !IsBattlePiece(_selectedUnit))
        {
            return;
        }

        var retreatingUnit = _selectedUnit;
        var retreatingGrid = _selectedUnitGrid.Value;
        ApplyRetreatTroopLoss(retreatingUnit);
        ShowRetreatNotice(retreatingUnit);
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

    private void ExecuteSelectedSupply()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            _selectedUnit.Marker == null ||
            _selectedUnit.TroopType != TroopSupplyCart ||
            _selectedUnit.Energy < SupplyActionEnergyCost ||
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

        if (!TrySpendSupplyActionEnergy())
        {
            return;
        }

        _supplyUsedByMarkerThisTurn.Add(_selectedUnit.Marker!);
        MarkUnitActed(_selectedUnit);

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
        TryExecuteSelectedBattleAction(BattleActionKind.ResupplyWeapon);
    }

    private void ExecuteSelectedWeaponResupply()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            _selectedUnit.Marker == null ||
            _selectedUnit.TroopType != TroopSupplyCart ||
            _selectedUnit.Energy < SupplyActionEnergyCost ||
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

        if (!TrySpendSupplyActionEnergy())
        {
            return;
        }

        _supplyUsedByMarkerThisTurn.Add(_selectedUnit.Marker!);
        MarkUnitActed(_selectedUnit);
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
        MarkUnitActed(captor);
        TryShowOfficerSpeech(captor, BattleOfficerSpeechEvent.Capture);
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
        occupant.Marker.SetupTeamArrow(GetTeamArrowColor(occupant.TeamName));
        UpdateMarkerStrengthBar(occupant);
    }

    private bool HasSupplyTargets()
    {
        return _selectedUnit != null &&
               _selectedUnitGrid.HasValue &&
               _selectedUnit.TroopType == TroopSupplyCart &&
               _selectedUnit.Marker != null &&
               _selectedUnit.Energy >= SupplyActionEnergyCost &&
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
               _selectedUnit.Energy >= SupplyActionEnergyCost &&
               !_supplyUsedByMarkerThisTurn.Contains(_selectedUnit.Marker) &&
               GetWeaponResupplyTargets(_selectedUnitGrid.Value, _selectedUnit).Any();
    }

    private bool TrySpendSupplyActionEnergy()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || _selectedUnit.Energy < SupplyActionEnergyCost)
        {
            return false;
        }

        var updatedSupplyCart = _selectedUnit with { Energy = _selectedUnit.Energy - SupplyActionEnergyCost };
        ReplaceOccupantAtGrid(_selectedUnitGrid.Value, _selectedUnit, updatedSupplyCart);
        _selectedUnit = updatedSupplyCart;
        return true;
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
        if (!BattleSupplySystem.TryRepairSiegeEngine(target, repairAmount, out var updatedTarget, out var actualRepair))
        {
            return 0;
        }

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
        if (!BattleSupplySystem.TryRecoverWoundedTroops(target, recoveryAmount, out var updatedTarget, out var actualRecovery))
        {
            return 0;
        }

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

    private void OnStrategyButtonPressed()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue)
        {
            return;
        }

        _selectedStrategyAction = ResolveStrategyAction(_selectedUnit, _selectedUnitGrid);
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

        var strategyTargets = _selectedStrategyAction switch
        {
            BattleStrategyAction.Extinguish => CalculateExtinguishStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit),
            BattleStrategyAction.Fire => CalculateFireStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit),
            _ => CalculateMentalStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit)
        };
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
        foreach (var grid in CalculateAttackableGrids(sourceGrid, strategyAttacker, allowHillRangeBonus: false))
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

        if (_selectedStrategyAction == BattleStrategyAction.Extinguish)
        {
            return TryExecuteExtinguishStrategy(targetGrid);
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
        MarkUnitActed(_selectedUnit);
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

    private BattleStrategyAction ResolveStrategyAction(BattleOccupantInfo occupant, BattleGridKey? sourceGrid = null)
    {
        if (sourceGrid.HasValue && CanUseExtinguishStrategy(occupant, sourceGrid.Value))
        {
            return BattleStrategyAction.Extinguish;
        }

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

    private IEnumerable<BattleGridKey> CalculateExtinguishStrategyTargetGrids(BattleGridKey sourceGrid, BattleOccupantInfo officer)
    {
        if (!CanUseExtinguishStrategy(officer, sourceGrid))
        {
            yield break;
        }

        foreach (var fireGrid in _activeFireByGrid.Keys)
        {
            if (fireGrid.Level == sourceGrid.Level &&
                GetChebyshevDistance(sourceGrid.Grid, fireGrid.Grid) <= ExtinguishFireRange)
            {
                yield return fireGrid;
            }
        }
    }

    private bool TryExecuteExtinguishStrategy(BattleGridKey targetGrid)
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || _selectedUnit.Marker == null ||
            !CanUseExtinguishStrategy(_selectedUnit, _selectedUnitGrid.Value) ||
            !_activeFireByGrid.ContainsKey(targetGrid) ||
            GetChebyshevDistance(_selectedUnitGrid.Value.Grid, targetGrid.Grid) > ExtinguishFireRange)
        {
            return false;
        }

        var actingUnit = _selectedUnit with { Energy = _selectedUnit.Energy - ExtinguishFireEnergyCost };
        ReplaceOccupantAtGrid(_selectedUnitGrid.Value, _selectedUnit, actingUnit);
        _selectedUnit = actingUnit;
        _strategyUsedByMarkerThisTurn.Add(actingUnit.Marker);

        var successChance = GetExtinguishFireSuccessChance(actingUnit, targetGrid);
        var success = GD.Randf() * 100.0f <= successChance;
        if (success)
        {
            _activeFireByGrid.Remove(targetGrid);
            RemoveFireVisual(targetGrid);
            RefreshBattleDepthLayerOrder();
            ShowBattlePopup(targetGrid, BattleText("ui.battle.extinguish_success_popup", "Fire Extinguished"), new Color(0.25f, 0.78f, 1.0f, 1.0f), new Color(0.01f, 0.10f, 0.16f, 0.95f), new Vector2(-46.0f, -108.0f), 1.6, 22);
            AppendBattleLog(actingUnit, "Strategy", $"{FormatLogUnit(actingUnit)} extinguishes fire at {targetGrid} (success {successChance:N0}%, energy {ExtinguishFireEnergyCost})");
        }
        else
        {
            ShowBattlePopup(targetGrid, BattleText("ui.battle.extinguish_fail_popup", "Extinguish Failed"), new Color(1.0f, 0.72f, 0.20f, 1.0f), new Color(0.16f, 0.06f, 0.01f, 0.95f), new Vector2(-46.0f, -108.0f), 1.6, 22);
            AppendBattleLog(actingUnit, "Strategy", $"{FormatLogUnit(actingUnit)} fails to extinguish fire at {targetGrid} ({successChance:N0}%, energy {ExtinguishFireEnergyCost})");
        }

        MarkUnitActed(actingUnit);
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

    private bool CanUseExtinguishStrategy(BattleOccupantInfo? occupant, BattleGridKey sourceGrid)
    {
        return occupant?.Marker != null &&
               IsGeneralCountedPiece(occupant.Category, occupant.OfficerName) &&
               occupant.Energy >= ExtinguishFireEnergyCost &&
               !_strategyUsedByMarkerThisTurn.Contains(occupant.Marker) &&
               !IsMessed(occupant) &&
               _activeFireByGrid.Keys.Any(fireGrid =>
                   fireGrid.Level == sourceGrid.Level &&
                   GetChebyshevDistance(sourceGrid.Grid, fireGrid.Grid) <= ExtinguishFireRange);
    }

    private float GetExtinguishFireSuccessChance(BattleOccupantInfo officer, BattleGridKey targetGrid)
    {
        var weatherBonus = GetCurrentBattleWeather() switch
        {
            BattleWeatherType.Rain => 18,
            BattleWeatherType.Cloudy => 8,
            _ => 0
        };
        var terrainBonus = _mapData?.GetCell(targetGrid.X, targetGrid.Y).Terrain == BattleTerrainType.Forest ? -8 : 0;
        return Mathf.Clamp(25.0f + (GetOfficerTacticalIntelligence(officer.OfficerName) * 0.6f) + weatherBonus + terrainBonus, 35.0f, 90.0f);
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
        MarkUnitActed(_selectedUnit);
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

    private void ApplyBattleFireEntryDamage(BattleGridKey grid, BattleOccupantInfo enteringOccupant)
    {
        if (grid.Level != 0 ||
            !_activeFireByGrid.ContainsKey(grid) ||
            !TryGetCurrentOccupantAtGrid(grid, enteringOccupant, out var currentOccupant))
        {
            return;
        }

        ApplyBattleFireDamageToOccupants(grid, GetFireDamagePerTurn(GetCurrentBattleWeather()), currentOccupant);
    }

    private bool IsAiSafeMovementDestination(BattleGridKey grid)
    {
        return grid.Level != 0 || !_activeFireByGrid.ContainsKey(ToGroundGridKey(grid.Grid));
    }

    private void ApplyBattleFireDamageToOccupants(BattleGridKey targetGrid, int damage, BattleOccupantInfo? targetOverride = null)
    {
        if (damage <= 0 || !_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            return;
        }

        var target = targetOverride == null
            ? GetFireDamageTarget(targetOccupants)
            : TryGetCurrentOccupantAtGrid(targetGrid, targetOverride, out var currentTarget)
                ? currentTarget
                : null;
        if (target == null)
        {
            return;
        }

        var casualtyResult = ApplyUnitCasualties(targetGrid, target, damage, FireDamageKilledRatio, ignoresBuildingCover: true);
        var updatedTarget = casualtyResult.UpdatedTarget;

        ShowDamagePopup(targetGrid, casualtyResult.ActualDamage);
        AppendBattleLog(target, "Hurt", $"Fire burns {FormatLogUnit(target)} for {casualtyResult.ActualDamage:N0} at {targetGrid} ({FormatCasualtyResult(casualtyResult)})");
        ConfigureHud();
        if (updatedTarget.HitPoints <= 0)
        {
            DestroyOccupantAfterDelay(targetGrid, updatedTarget, 0.0, captureOfficer: true);
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
            var wasWoodenBridge = cell.IsWoodenBridge;
            var actualBridgeDamage = _mapData.ApplyBridgeDamage(targetGrid.Grid, FireDamageToBridge);
            if (actualBridgeDamage > 0)
            {
                ShowDamagePopup(targetGrid, actualBridgeDamage);
                if (!cell.HasBridgeHealth)
                {
                    RefreshWorkerObjectLayers();
                    if (wasWoodenBridge)
                    {
                        AppendBattleLog(GetCurrentTurnSideName(), "Destroy", $"Wooden bridge destroyed by fire at {targetGrid}.");
                        ShowWoodenBridgeDestroyedNotice(targetGrid);
                    }
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
        if (cell.Terrain is BattleTerrainType.Moat or BattleTerrainType.River or BattleTerrainType.Swamp or BattleTerrainType.Coast or BattleTerrainType.WallWalk or BattleTerrainType.Mountain)
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
        return BattleEnvironmentSystem.GetNextWeather(weather);
    }

    private static BattleTimeOfDay GetNextBattleTimeOfDay(BattleTimeOfDay timeOfDay)
    {
        return BattleEnvironmentSystem.GetNextTimeOfDay(timeOfDay);
    }

    private static BattleWindDirection GetNextBattleWindDirection(BattleWindDirection direction)
    {
        return BattleEnvironmentSystem.GetNextWindDirection(direction);
    }

    private static BattleWindPower GetNextBattleWindPower(BattleWindPower power)
    {
        return BattleEnvironmentSystem.GetNextWindPower(power);
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
        BeginWorkSelection(WorkerWorkAction.General);
    }

    private void OnInstallWoodFenceButtonPressed()
    {
        BeginWorkSelection(WorkerWorkAction.WoodFence);
    }

    private void OnUninstallWoodFenceButtonPressed()
    {
        BeginWorkSelection(WorkerWorkAction.WoodFence);
    }

    private void BeginWorkSelection(WorkerWorkAction workAction)
    {
        if (_selectedUnit == null ||
            IsMessed(_selectedUnit) ||
            !_selectedUnitGrid.HasValue ||
            !CanUseWorkAction(_selectedUnit, workAction))
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
            if (targetCell != null &&
                IsWorkTargetForAction(_selectedUnit, targetGrid, targetCell, workAction) &&
                _selectedUnit.Energy >= GetWorkEnergyCost(_selectedUnit, targetCell, workAction))
            {
                _workableGrids.Add(new BattleGridKey(targetGrid.X, targetGrid.Y, 0));
            }
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private static bool CanUseWorkAction(BattleOccupantInfo unit, WorkerWorkAction workAction)
    {
        return unit.TroopType == TroopWorker ||
               (workAction == WorkerWorkAction.General && BattleBridgeSystem.CanEmergencyRepair(unit));
    }

    private bool IsWorkTargetForAction(
        BattleOccupantInfo unit,
        Vector2I targetGrid,
        BattleCellData cell,
        WorkerWorkAction workAction)
    {
        if (unit.TroopType != TroopWorker)
        {
            return workAction == WorkerWorkAction.General &&
                   BattleBridgeSystem.CanEmergencyRepair(unit) &&
                   BattleBridgeSystem.IsEmergencyRepairTarget(cell);
        }

        if (workAction == WorkerWorkAction.WoodFence)
        {
            return cell.Structure == BattleStructureType.WoodenFence || CanInstallWoodFence(targetGrid, cell);
        }

        return (cell.Terrain == BattleTerrainType.Moat && _mapData?.ScenarioDefinition.ScenarioType == BattleScenarioType.MoatSiegeBattle) ||
               (cell.Terrain == BattleTerrainType.River && _mapData?.ScenarioDefinition.ScenarioType == BattleScenarioType.FieldBattle) ||
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

    private bool HasWorkTarget(BattleOccupantInfo unit, WorkerWorkAction workAction)
    {
        return _selectedUnitGrid.HasValue &&
               _mapData != null &&
               !IsMessed(unit) &&
               CanUseWorkAction(unit, workAction) &&
               GetOrthogonalNeighbors(_selectedUnitGrid.Value.Grid)
                   .Where(IsWithinMap)
                   .Any(grid =>
                   {
                       var cell = _mapData.GetCell(grid.X, grid.Y);
                       return IsWorkTargetForAction(unit, grid, cell, workAction) &&
                              unit.Energy >= GetWorkEnergyCost(unit, cell, workAction);
                   });
    }

    private void OnOpenGateButtonPressed()
    {
        if (!TryGetSwitchableGate(out var gateGrid) || _mapData == null)
        {
            return;
        }

        ToggleGateGroup(GetConnectedGateGroup(gateGrid));
        if (_selectedUnit != null)
        {
            MarkUnitActed(_selectedUnit);
        }

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

    private readonly record struct WallTopAttackAmmo(int DropStoneUses, int PourOilUses);
    private readonly record struct BattleFireState(int RemainingTurns, int BurnTurns);

    private sealed record BattleHudTeamInfo(string Name, int TotalTroops, int WoundedTroops, int TotalGenerals, int TotalSiegeUnits, int StrategyPlans, int TotalGold, int TotalFood);

}
