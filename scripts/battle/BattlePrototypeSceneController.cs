using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ThreeKingdom.Core;

namespace ThreeKingdom.Battle;

[Tool]
public partial class BattlePrototypeSceneController : Node2D
{
    private const float MapPaddingLeft = 220.0f;
    private const float MapPaddingTop = 220.0f;
    private const float MapPaddingRight = 220.0f;
    private const float MapPaddingBottom = 320.0f;
    private const float DefaultUnitVisualLift = -16.0f;
    private const float WallWalkUnitVisualLift = -58.0f;
    private const float WallWalkHighlightVisualLift = -42.0f;
    private const string CategoryUnit = "Unit";
    private const string CategorySiegeEngine = "SiegeEngine";
    private const string TroopInfantry = "Infantry";
    private const string TroopSpearman = "Spearman";
    private const string TroopArcher = "Archer";
    private const string TroopCavalry = "Cavalry";
    private const string TroopCrossbow = "Crossbow";
    private const string TroopGuard = "Guard";
    private const string TroopRam = "Ram";
    private const string TroopLadder = "Ladder";
    private const string TroopCatapult = "Catapult";
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
    private const string CatapultAttackSouthEastScenePath = "res://scenes/battle/unit/CatapultAttackSe.tscn";
    private const string CatapultAttackSouthWestScenePath = "res://scenes/battle/unit/CatapultAttackSw.tscn";
    private const string CatapultAttackNorthEastScenePath = "res://scenes/battle/unit/CatapultAttackNe.tscn";
    private const string CatapultAttackNorthWestScenePath = "res://scenes/battle/unit/CatapultAttackNw.tscn";
    private const double InfantryMoveAnimationDurationSeconds = 0.8;
    private const double SpearmanMoveAnimationDurationSeconds = 0.8;
    private const double ArcherMoveAnimationDurationSeconds = 0.8;
    private const double CavalryMoveAnimationDurationSeconds = 0.8;
    private const double InfantryAttackAnimationDurationSeconds = 0.62;
    private const double SpearmanAttackAnimationDurationSeconds = 0.72;
    private const double ArcherAttackAnimationDurationSeconds = 0.62;
    private const double CavalryAttackAnimationDurationSeconds = 0.5;
    private const double CatapultAttackAnimationDurationSeconds = 0.72;
    private const double InfantryHurtAnimationDurationSeconds = 0.5;
    private const double SpearmanHurtAnimationDurationSeconds = 0.65;
    private const double ArcherHurtAnimationDurationSeconds = 0.5;
    private const double CavalryHurtAnimationDurationSeconds = 0.5;
    private const double CarMoveAnimationDurationSeconds = 0.8;
    private const double CatapultMoveAnimationDurationSeconds = 0.8;
    private static readonly BattleHudTeamInfo TeamAInfo = new("Team A / 攻方", 18000, 8200, 26000);
    private static readonly BattleHudTeamInfo TeamBInfo = new("Team B / 守方", 12500, 6400, 19800);
    private const string BattleDateText = "191年 4月 4日";
    private const string WeatherText = "晴";

    private BattlePrototypeMapData? _mapData;
    private Node2D? _mapRoot;
    private Camera2D? _camera;
    private TileMapLayer? _groundLayer;
    private TileMapLayer? _objectLayer;
    private TileMapLayer? _castleLayer;
    private TileMapLayer? _overlayLayer;
    private BattlePrototypeHighlightRenderer? _highlightLayer;
    private Control? _commandMenu;
    private Label? _windowTitleLabel;
    private Label? _unitMenuInfoLabel;
    private Button? _endTurnButton;
    private Button? _moveButton;
    private Button? _attackButton;
    private Button? _strategyButton;
    private Button? _openGateButton;
    private bool _isDraggingMap;
    private bool _isDraggingCommandMenu;
    private Vector2 _lastMousePosition;
    private Vector2 _commandMenuDragOffset;
    private Vector2I? _hoverGrid;
    private Vector2I? _selectedGrid;
    private Vector2I? _selectedUnitGrid;
    private BattleOccupantInfo? _selectedUnit;
    private readonly HashSet<Vector2I> _movableGrids = new();
    private readonly HashSet<Vector2I> _attackableGrids = new();
    private readonly Dictionary<Vector2I, List<BattleOccupantInfo>> _occupantsByGrid = new();
    private BattleCommandMode _commandMode = BattleCommandMode.None;
    private int _turnNumber = 1;
    private BattleTurnSide _currentTurnSide = BattleTurnSide.TeamA;
    private bool _editorBakePrototypeLayout;
    private bool _editorClearTileLayout;

    [Export]
    public bool EditorBakePrototypeLayout
    {
        get => _editorBakePrototypeLayout;
        set
        {
            if (!value || !Engine.IsEditorHint())
            {
                _editorBakePrototypeLayout = value;
                return;
            }

            CacheSceneNodes();
            BakePrototypeLayoutInEditor();
            _editorBakePrototypeLayout = false;
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

    public override void _Ready()
    {
        CacheSceneNodes();
        if (Engine.IsEditorHint())
        {
            return;
        }

        if (_endTurnButton != null)
        {
            _endTurnButton.Pressed += OnEndTurnButtonPressed;
        }

        if (_moveButton != null)
        {
            _moveButton.Pressed += OnMoveButtonPressed;
        }

        if (_attackButton != null)
        {
            _attackButton.Pressed += OnAttackButtonPressed;
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
        PopulateMarkers();
        ConfigureHud();

        if (_mapRoot != null)
        {
            _mapRoot.Position = GetClampedMapPosition(_mapRoot.Position);
        }
    }

    private void CacheSceneNodes()
    {
        _mapRoot ??= GetNodeOrNull<Node2D>("MapRoot");
        _camera ??= GetNodeOrNull<Camera2D>("Camera2D");
        _groundLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/GroundLayer");
        _objectLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/ObjectLayer");
        _castleLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/CastleLayer");
        _overlayLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/OverlayLayer");
        _highlightLayer ??= GetNodeOrNull<BattlePrototypeHighlightRenderer>("MapRoot/HighlightLayer");
        _commandMenu ??= GetNodeOrNull<Control>("UiLayer/CommandMenu");
        _windowTitleLabel ??= GetNodeOrNull<Label>("UiLayer/CommandMenu/MenuMargin/MenuButtons/WindowTitleLabel");
        _unitMenuInfoLabel ??= GetNodeOrNull<Label>("UiLayer/CommandMenu/MenuMargin/MenuButtons/UnitMenuInfoLabel");
        _endTurnButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/EndTurnButton");
        _moveButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/MoveButton");
        _attackButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/AttackButton");
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

        if (HasEditorAuthoredLayout())
        {
            BattlePrototypeTileMapBuilder.AssignLayerTileSet(_groundLayer, BattlePrototypeTileLayerKind.Ground);
            BattlePrototypeTileMapBuilder.AssignLayerTileSet(_objectLayer, BattlePrototypeTileLayerKind.Object);
            BattlePrototypeTileMapBuilder.AssignLayerTileSet(_castleLayer, BattlePrototypeTileLayerKind.Castle);
            BattlePrototypeTileMapBuilder.AssignLayerTileSet(_overlayLayer, BattlePrototypeTileLayerKind.DeploymentOverlay);
            _mapData = BattlePrototypeMapData.CreateFromTileMapLayers(_groundLayer, _objectLayer, _castleLayer, _overlayLayer);
            return;
        }

        _mapData = BattlePrototypeMapData.CreateSiegeAssault();
        ConfigureTileMapLayer("MapRoot/GroundLayer", BattlePrototypeTileLayerKind.Ground);
        ConfigureTileMapLayer("MapRoot/ObjectLayer", BattlePrototypeTileLayerKind.Object);
        ConfigureTileMapLayer("MapRoot/CastleLayer", BattlePrototypeTileLayerKind.Castle);
        ConfigureTileMapLayer("MapRoot/OverlayLayer", BattlePrototypeTileLayerKind.DeploymentOverlay);
    }

    private bool HasEditorAuthoredLayout()
    {
        return (_groundLayer?.GetUsedCells().Count ?? 0) > 0 ||
               (_objectLayer?.GetUsedCells().Count ?? 0) > 0 ||
               (_castleLayer?.GetUsedCells().Count ?? 0) > 0 ||
               (_overlayLayer?.GetUsedCells().Count ?? 0) > 0;
    }

    private void BakePrototypeLayoutInEditor()
    {
        if (_groundLayer == null || _objectLayer == null || _castleLayer == null || _overlayLayer == null)
        {
            return;
        }

        _mapData = BattlePrototypeMapData.CreateSiegeAssault();
        BattlePrototypeTileMapBuilder.ConfigureLayer(_groundLayer, _mapData, BattlePrototypeTileLayerKind.Ground);
        BattlePrototypeTileMapBuilder.ConfigureLayer(_objectLayer, _mapData, BattlePrototypeTileLayerKind.Object);
        BattlePrototypeTileMapBuilder.ConfigureLayer(_castleLayer, _mapData, BattlePrototypeTileLayerKind.Castle);
        BattlePrototypeTileMapBuilder.ConfigureLayer(_overlayLayer, _mapData, BattlePrototypeTileLayerKind.DeploymentOverlay);
    }

    private void ClearTileLayoutInEditor()
    {
        ClearLayer(_groundLayer, BattlePrototypeTileLayerKind.Ground);
        ClearLayer(_objectLayer, BattlePrototypeTileLayerKind.Object);
        ClearLayer(_castleLayer, BattlePrototypeTileLayerKind.Castle);
        ClearLayer(_overlayLayer, BattlePrototypeTileLayerKind.DeploymentOverlay);
    }

    private static void ClearLayer(TileMapLayer? layer, BattlePrototypeTileLayerKind layerKind)
    {
        if (layer == null)
        {
            return;
        }

        BattlePrototypeTileMapBuilder.AssignLayerTileSet(layer, layerKind);
        foreach (var coords in layer.GetUsedCells())
        {
            layer.EraseCell(coords);
        }

        layer.UpdateInternals();
    }

    private void ConfigureTileMapLayer(string path, BattlePrototypeTileLayerKind layerKind)
    {
        var tileMapLayer = GetNodeOrNull<TileMapLayer>(path);
        if (tileMapLayer == null)
        {
            return;
        }

        BattlePrototypeTileMapBuilder.ConfigureLayer(tileMapLayer, _mapData!, layerKind);
    }

    private void PopulateMarkers()
    {
        CreateMarker("MapRoot/UnitLayer/AttackerA", new Vector2I(10, 20), "步", "攻方步兵 A", CategoryUnit, "Team A / 攻方", "夏侯淵", TroopInfantry, 6200, new Color("ad4832"), new Color("f0d6a8"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Spearman", new Vector2I(8, 18), "槍", "攻方槍兵", CategoryUnit, "Team A / 攻方", "曹洪", TroopSpearman, 4200, new Color("9b5931"), new Color("f0d6a8"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/AttackerB", new Vector2I(12, 18), "弓", "攻方弓兵 B", CategoryUnit, "Team A / 攻方", "張郃", TroopArcher, 5400, new Color("b96d2c"), new Color("f0d6a8"), moveRange: 4, attackRange: 3);
        CreateMarker("MapRoot/UnitLayer/AttackerC", new Vector2I(14, 20), "騎", "攻方騎兵 C", CategoryUnit, "Team A / 攻方", "曹純", TroopCavalry, 4800, new Color("8f3f31"), new Color("f0d6a8"), moveRange: 6, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Ram", new Vector2I(12, 16), "衝", "衝車", CategorySiegeEngine, "Team A / 攻方", "樂進", TroopRam, 900, new Color("7a4a20"), new Color("ead7aa"), 21.0f, moveRange: 3, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Ladder", new Vector2I(10, 15), "梯", "雲梯隊", CategorySiegeEngine, "Team A / 攻方", "于禁", TroopLadder, 800, new Color("8c7b44"), new Color("ead7aa"), 21.0f, moveRange: 3, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Catapult", new Vector2I(14, 15), "投", "投石機", CategorySiegeEngine, "Team A / 攻方", "劉曄", TroopCatapult, 600, new Color("6e5131"), new Color("ead7aa"), 21.0f, moveRange: 2, attackRange: 4);

        CreateMarker("MapRoot/UnitLayer/DefenderA", new Vector2I(10, 6), "守", "守軍步兵 A", CategoryUnit, "Team B / 守方", "董卓", TroopInfantry, 5100, new Color("326b8d"), new Color("e0f0ff"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/DefenderB", new Vector2I(14, 6), "弩", "守軍弩兵 B", CategoryUnit, "Team B / 守方", "李傕", TroopCrossbow, 4300, new Color("245f76"), new Color("e0f0ff"), moveRange: 4, attackRange: 3);
        CreateMarker("MapRoot/UnitLayer/DefenderC", new Vector2I(12, 6), "將", "守軍主將", CategoryUnit, "Team B / 守方", "郭汜", TroopGuard, 3100, new Color("274e8a"), new Color("e0f0ff"), moveRange: 4, attackRange: 1);
    }

    private void CreateMarker(string path, Vector2I grid, string label, string displayName, string category, string teamName, string officerName, string troopType, int troopCount, Color fillColor, Color borderColor, float radius = 19.0f, int moveRange = 0, int attackRange = 1)
    {
        var marker = GetNodeOrNull<BattlePieceMarker>(path);
        if (marker == null)
        {
            return;
        }

        marker.Position = GetMarkerPosition(grid);
        marker.Setup(label, fillColor, borderColor, radius);
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

        RegisterOccupant(grid, displayName, category, label, teamName, officerName, troopType, troopCount, moveRange, attackRange, marker);
    }

    private Vector2 GetMarkerPosition(Vector2I grid)
    {
        var gridCenter = _groundLayer?.MapToLocal(grid) ?? BattlePrototypeMapRenderer.GridToWorld(grid);
        return gridCenter + new Vector2(0.0f, GetUnitVisualLift(grid));
    }

    private float GetUnitVisualLift(Vector2I grid)
    {
        if (_mapData != null && IsWithinMap(grid) && _mapData.GetCell(grid.X, grid.Y).Terrain == BattleTerrainType.WallWalk)
        {
            return WallWalkUnitVisualLift;
        }

        return DefaultUnitVisualLift;
    }

    private Vector2 GetHighlightPosition(Vector2I grid)
    {
        var gridCenter = _groundLayer?.MapToLocal(grid) ?? BattlePrototypeMapRenderer.GridToWorld(grid);
        if (_mapData != null && IsWithinMap(grid) && _mapData.GetCell(grid.X, grid.Y).Terrain == BattleTerrainType.WallWalk)
        {
            return gridCenter + new Vector2(0.0f, WallWalkHighlightVisualLift);
        }

        return gridCenter;
    }

    private void ConfigureHud()
    {
        var titleLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/TitleLabel");
        var summaryLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/SummaryLabel");
        var teamBLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/TeamBLabel");
        var coordinateLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/CoordinateLabel");

        if (titleLabel != null)
        {
            titleLabel.Text = $"日期: {BattleDateText}   天氣: {WeatherText}   回合: {_turnNumber}   行動方: {GetCurrentTurnSideName()}";
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
                CancelCommandAction(clearSelection: true);
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
        Vector2I? newHoverGrid = ResolvePointerGrid(localMouse);
        if (_hoverGrid == newHoverGrid)
        {
            return;
        }

        _hoverGrid = newHoverGrid;
        RefreshCoordinateLabel();
    }

    private Vector2I? ResolvePointerGrid(Vector2 localMouse)
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

        var markerGrid = ResolvePointerMarkerGrid(localMouse);
        if (markerGrid.HasValue)
        {
            return markerGrid.Value;
        }

        var groundCandidate = _groundLayer.LocalToMap(localMouse);
        return IsWithinMap(groundCandidate) ? groundCandidate : null;
    }

    private Vector2I? ResolvePointerHighlightedGrid(Vector2 localMouse)
    {
        var activeGrids = _commandMode switch
        {
            BattleCommandMode.MoveSelect => _movableGrids,
            BattleCommandMode.AttackSelect => _attackableGrids,
            _ => null
        };

        if (activeGrids == null)
        {
            return null;
        }

        foreach (var grid in activeGrids)
        {
            if (PointInDiamond(localMouse, GetHighlightPosition(grid), 46.0f, 24.0f))
            {
                return grid;
            }
        }

        return null;
    }

    private static bool PointInDiamond(Vector2 point, Vector2 center, float halfWidth, float halfHeight)
    {
        var dx = Mathf.Abs(point.X - center.X) / halfWidth;
        var dy = Mathf.Abs(point.Y - center.Y) / halfHeight;
        return dx + dy <= 1.0f;
    }

    private Vector2I? ResolvePointerMarkerGrid(Vector2 localMouse)
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

    private bool IsWithinMap(Vector2I grid)
    {
        return grid.X >= 0 &&
               grid.X < BattlePrototypeMapData.Width &&
               grid.Y >= 0 &&
               grid.Y < BattlePrototypeMapData.Height;
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
        return $"Hover: {FormatGrid(_hoverGrid)}    Click: {FormatGrid(_selectedGrid)}";
    }

    private static string BuildTeamHudText(BattleHudTeamInfo info)
    {
        return $"{info.Name}   兵力: {info.TotalTroops:N0}   金: {info.TotalGold:N0}   糧: {info.TotalFood:N0}";
    }

    private static string FormatGrid(Vector2I? grid)
    {
        return grid.HasValue ? $"({grid.Value.X}, {grid.Value.Y})" : "-";
    }

    private string BuildInfoText()
    {
        if (!_selectedGrid.HasValue || _mapData == null)
        {
            return "Tile Info\n座標: -\n請先 click 一格查看。\n會顯示地形、建物、部署區與單位資訊。";
        }

        var grid = _selectedGrid.Value;
        var cell = _mapData.GetCell(grid.X, grid.Y);
        var builder = new StringBuilder();
        builder.AppendLine("Tile Info");
        builder.AppendLine($"座標: ({grid.X}, {grid.Y})");
        builder.AppendLine($"地形: {FormatTerrain(cell.Terrain)}");
        builder.AppendLine($"建物: {FormatStructure(cell.Structure)}");
        if (cell.HasStructureHealth)
        {
            builder.AppendLine($"耐久: {cell.StructureHealth}/{cell.StructureMaxHealth}");
            builder.AppendLine($"狀態: {(cell.IsBroken ? "已破壞" : "完整")}");
        }

        builder.AppendLine($"部署區: {FormatDeploymentZone(cell.DeploymentZone)}");
        builder.AppendLine($"高度: {cell.HeightLevel}");
        builder.AppendLine($"移動阻擋: {(IsCellBlockingMovement(cell) ? "是" : "否")}");
        if (cell.Structure == BattleStructureType.Gate)
        {
            builder.AppendLine($"Gate: {(cell.IsGateOpen ? "Open" : "Closed")}");
        }

        builder.AppendLine("Occupants");

        if (_occupantsByGrid.TryGetValue(grid, out var occupants) && occupants.Count > 0)
        {
            foreach (var occupant in occupants)
            {
                builder.AppendLine($"- {occupant.Category}: {occupant.DisplayName} [{occupant.ShortLabel}]");
            }
        }
        else
        {
            builder.AppendLine("- 無");
        }

        if (_selectedUnit != null && _selectedUnitGrid.HasValue)
        {
            builder.AppendLine("Selected Piece");
            builder.AppendLine($"- {_selectedUnit.DisplayName} [{_selectedUnit.ShortLabel}]");
            builder.AppendLine($"- 類型: {_selectedUnit.Category}");
            builder.AppendLine($"- 所在格: ({_selectedUnitGrid.Value.X}, {_selectedUnitGrid.Value.Y})");
            builder.AppendLine($"- 移動力: {_selectedUnit.MoveRange}");
            builder.AppendLine($"- 攻擊距離: {_selectedUnit.AttackRange}");
            builder.AppendLine($"- 可移動格數: {_movableGrids.Count}");
            builder.AppendLine($"- 可攻擊格數: {_attackableGrids.Count}");
            builder.AppendLine($"- 指令狀態: {FormatCommandMode(_commandMode)}");
            builder.AppendLine($"- 當前回合: {GetCurrentTurnSideName()}");
        }

        return builder.ToString().TrimEnd();
    }

    private void RegisterOccupant(Vector2I grid, string displayName, string category, string shortLabel, string teamName, string officerName, string troopType, int troopCount, int moveRange, int attackRange, BattlePieceMarker? marker)
    {
        if (!_occupantsByGrid.TryGetValue(grid, out var occupants))
        {
            occupants = new List<BattleOccupantInfo>();
            _occupantsByGrid[grid] = occupants;
        }

        occupants.Add(new BattleOccupantInfo(displayName, category, shortLabel, teamName, officerName, troopType, troopCount, moveRange, attackRange, marker, BattleSpriteDirection.SouthEast));
    }

    private bool TryMoveSelectedUnit()
    {
        if (!_selectedGrid.HasValue || !_selectedUnitGrid.HasValue || _selectedUnit == null || _groundLayer == null)
        {
            return false;
        }

        var destinationGrid = _selectedGrid.Value;
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
        var moveDirection = pathDirections.Length > 0
            ? pathDirections[^1]
            : GetInfantryDirection(sourceGrid, destinationGrid);
        var movedOccupant = movingOccupant with { Marker = movingOccupant.Marker, FacingDirection = moveDirection };
        destinationOccupants.Add(movedOccupant);
        ApplyMoveAnimation(movedOccupant, moveDirection, GetMarkerPosition(destinationGrid), pathPositions, pathDirections);

        _selectedUnitGrid = destinationGrid;
        _selectedUnit = movedOccupant;
        _selectedGrid = destinationGrid;
        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        HideCommandMenu();

        return true;
    }

    private bool TryAttackSelectedTarget()
    {
        if (!_selectedGrid.HasValue || !_selectedUnitGrid.HasValue || _selectedUnit == null)
        {
            return false;
        }

        var targetGrid = _selectedGrid.Value;
        if (!_attackableGrids.Contains(targetGrid))
        {
            return false;
        }

        var attackDirection = GetInfantryDirection(_selectedUnitGrid.Value, targetGrid);
        var attackingUnit = _selectedUnit with { FacingDirection = attackDirection };
        ReplaceOccupantAtGrid(_selectedUnitGrid.Value, _selectedUnit, attackingUnit);
        _selectedUnit = attackingUnit;

        ApplyAttackAnimation(attackingUnit, attackDirection);
        ApplyTargetHurtAnimation(_selectedUnitGrid.Value, targetGrid);
        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        HideCommandMenu();
        return true;
    }

    private void ReplaceOccupantAtGrid(Vector2I grid, BattleOccupantInfo oldOccupant, BattleOccupantInfo newOccupant)
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

    private static void ApplyMoveAnimation(BattleOccupantInfo occupant, BattleSpriteDirection direction, Vector2 destinationPosition, Vector2[]? pathPositions = null, BattleSpriteDirection[]? pathDirections = null)
    {
        if (occupant.Marker == null)
        {
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopInfantry)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetInfantryMoveScene), InfantryMoveAnimationDurationSeconds, GetInfantryMoveScene(direction), GetInfantryIdleScene(direction));
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopSpearman)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetSpearmanMoveScene), SpearmanMoveAnimationDurationSeconds, GetSpearmanMoveScene(direction), GetSpearmanIdleScene(direction));
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopRam)
        {
            var carIdleScene = GetCarIdleScene(direction);
            occupant.Marker.MoveTo(
                destinationPosition,
                CarMoveAnimationDurationSeconds,
                carIdleScene,
                carIdleScene);
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopLadder)
        {
            var carLadderIdleScene = GetCarLadderIdleScene(direction);
            occupant.Marker.MoveTo(
                destinationPosition,
                CarMoveAnimationDurationSeconds,
                carLadderIdleScene,
                carLadderIdleScene);
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopArcher)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetArcherMoveScene), ArcherMoveAnimationDurationSeconds, GetArcherMoveScene(direction), GetArcherIdleScene(direction));
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopCavalry)
        {
            occupant.Marker.MoveTo(
                destinationPosition,
                CavalryMoveAnimationDurationSeconds,
                GetCavalryMoveScene(direction),
                GetCavalryIdleScene(direction));
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopCatapult)
        {
            var catapultIdleScene = GetCatapultIdleScene(direction);
            occupant.Marker.MoveTo(
                destinationPosition,
                CatapultMoveAnimationDurationSeconds,
                catapultIdleScene,
                catapultIdleScene);
            return;
        }

        occupant.Marker.Position = destinationPosition;
    }

    private static void MoveMarker(BattlePieceMarker marker, Vector2 destinationPosition, Vector2[]? pathPositions, string[]? pathMoveScenePaths, double duration, string moveScenePath, string idleScenePath)
    {
        if (pathPositions is { Length: > 0 })
        {
            if (pathMoveScenePaths is { Length: > 0 })
            {
                marker.MoveAlong(pathPositions, duration, pathMoveScenePaths, idleScenePath);
                return;
            }

            marker.MoveAlong(pathPositions, duration, moveScenePath, idleScenePath);
            return;
        }

        marker.MoveTo(destinationPosition, duration, moveScenePath, idleScenePath);
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

    private static void ApplyAttackAnimation(BattleOccupantInfo occupant, BattleSpriteDirection direction)
    {
        if (occupant.Marker == null)
        {
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopCatapult)
        {
            occupant.Marker.PlayAction(
                GetCatapultAttackScene(direction),
                GetCatapultIdleScene(direction),
                CatapultAttackAnimationDurationSeconds);
            return;
        }

        if (occupant.Category != CategoryUnit)
        {
            return;
        }

        if (occupant.TroopType == TroopInfantry)
        {
            occupant.Marker.PlayAction(
                GetInfantryAttackScene(direction),
                GetInfantryIdleScene(direction),
                InfantryAttackAnimationDurationSeconds);
            return;
        }

        if (occupant.TroopType == TroopSpearman)
        {
            occupant.Marker.PlayAction(
                GetSpearmanAttackScene(direction),
                GetSpearmanIdleScene(direction),
                SpearmanAttackAnimationDurationSeconds);
            return;
        }

        if (occupant.TroopType == TroopArcher)
        {
            occupant.Marker.PlayAction(
                GetArcherAttackScene(direction),
                GetArcherIdleScene(direction),
                ArcherAttackAnimationDurationSeconds);
            return;
        }

        if (occupant.TroopType == TroopCavalry)
        {
            occupant.Marker.PlayAction(
                GetCavalryAttackScene(direction),
                GetCavalryIdleScene(direction),
                CavalryAttackAnimationDurationSeconds);
        }
    }

    private void ApplyTargetHurtAnimation(Vector2I attackerGrid, Vector2I targetGrid)
    {
        if (!_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            return;
        }

        var target = targetOccupants.FirstOrDefault(static occupant =>
            occupant.Marker != null &&
            occupant.Category == CategoryUnit &&
            (occupant.TroopType == TroopInfantry || occupant.TroopType == TroopSpearman || occupant.TroopType == TroopArcher || occupant.TroopType == TroopCavalry));
        if (target?.Marker == null)
        {
            return;
        }

        var hurtDirection = GetInfantryDirection(attackerGrid, targetGrid);
        if (target.TroopType == TroopSpearman)
        {
            target.Marker.PlayAction(
                GetSpearmanHurtScene(hurtDirection),
                GetSpearmanIdleScene(target.FacingDirection),
                SpearmanHurtAnimationDurationSeconds);
            return;
        }

        if (target.TroopType == TroopArcher)
        {
            target.Marker.PlayAction(
                GetArcherHurtScene(hurtDirection),
                GetArcherIdleScene(target.FacingDirection),
                ArcherHurtAnimationDurationSeconds);
            return;
        }

        if (target.TroopType == TroopCavalry)
        {
            target.Marker.PlayAction(
                GetCavalryHurtScene(hurtDirection),
                GetCavalryIdleScene(target.FacingDirection),
                CavalryHurtAnimationDurationSeconds);
            return;
        }

        target.Marker.PlayAction(
            GetInfantryHurtScene(hurtDirection),
            GetInfantryIdleScene(target.FacingDirection),
            InfantryHurtAnimationDurationSeconds);
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
        _commandMode = BattleCommandMode.None;

        if (!_selectedGrid.HasValue || _mapData == null)
        {
            return;
        }

        if (!_occupantsByGrid.TryGetValue(_selectedGrid.Value, out var occupants))
        {
            return;
        }

        var selectedUnit = occupants.FirstOrDefault(static occupant => IsBattlePiece(occupant));
        if (selectedUnit == null)
        {
            return;
        }

        _selectedUnit = selectedUnit;
        _selectedUnitGrid = _selectedGrid;
        _commandMode = BattleCommandMode.AwaitingCommand;
    }

    private IEnumerable<Vector2I> CalculateReachableGrids(Vector2I startGrid, int moveRange)
    {
        if (_mapData == null || moveRange <= 0)
        {
            yield break;
        }

        var frontier = new Queue<(Vector2I Grid, int RemainingMove)>();
        var bestRemaining = new Dictionary<Vector2I, int> { [startGrid] = moveRange };
        frontier.Enqueue((startGrid, moveRange));

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var step in GetMovementNeighbors(current.Grid))
            {
                var neighbor = step.Grid;
                if (!IsWithinMap(neighbor))
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

    private bool TryBuildMovePath(Vector2I startGrid, Vector2I destinationGrid, int moveRange, out List<Vector2I> path)
    {
        path = [];
        if (_mapData == null || moveRange <= 0)
        {
            return false;
        }

        var frontier = new Queue<(Vector2I Grid, int RemainingMove)>();
        var bestRemaining = new Dictionary<Vector2I, int> { [startGrid] = moveRange };
        var previousByGrid = new Dictionary<Vector2I, Vector2I>();
        frontier.Enqueue((startGrid, moveRange));

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current.Grid == destinationGrid)
            {
                path = RebuildMovePath(startGrid, destinationGrid, previousByGrid);
                return path.Count > 0;
            }

            foreach (var step in GetMovementNeighbors(current.Grid))
            {
                var neighbor = step.Grid;
                if (!IsWithinMap(neighbor))
                {
                    continue;
                }

                var cell = _mapData.GetCell(neighbor.X, neighbor.Y);
                if (!CanEnterCell(current.Grid, neighbor, cell, step.UsesLadderBridge))
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

                var remainingMove = current.RemainingMove - GetMoveCost(cell);
                if (remainingMove < 0)
                {
                    continue;
                }

                if (bestRemaining.TryGetValue(neighbor, out var knownRemaining) && knownRemaining >= remainingMove)
                {
                    continue;
                }

                bestRemaining[neighbor] = remainingMove;
                previousByGrid[neighbor] = current.Grid;
                frontier.Enqueue((neighbor, remainingMove));
            }
        }

        return false;
    }

    private static List<Vector2I> RebuildMovePath(Vector2I startGrid, Vector2I destinationGrid, IReadOnlyDictionary<Vector2I, Vector2I> previousByGrid)
    {
        var path = new List<Vector2I>();
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

    private List<Vector2I> ExpandMovePathWithCarLadderWaypoints(Vector2I sourceGrid, IReadOnlyList<Vector2I> movePath, BattleOccupantInfo movingOccupant)
    {
        if (_mapData == null || movePath.Count == 0 || !CanUseCarLadderBridge(movingOccupant))
        {
            return movePath.ToList();
        }

        var expandedPath = new List<Vector2I>();
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

    private bool TryGetCarLadderGridForTransition(Vector2I fromGrid, Vector2I toGrid, out Vector2I ladderGrid)
    {
        ladderGrid = default;
        if (_mapData == null || !IsWithinMap(fromGrid) || !IsWithinMap(toGrid))
        {
            return false;
        }

        var fromCell = _mapData.GetCell(fromGrid.X, fromGrid.Y);
        var toCell = _mapData.GetCell(toGrid.X, toGrid.Y);
        if (!ShouldUseCarLadderBridgeForMove(fromCell, toCell))
        {
            return false;
        }

        foreach (var candidateLadderGrid in GetUsableCarLadderGrids())
        {
            var groundGrids = GetCarLadderGroundEndpoints(candidateLadderGrid).ToList();
            var wallWalkGrids = GetCarLadderWallWalkEndpoints(candidateLadderGrid).ToList();
            var matchesGroundToWall = fromCell.Terrain != BattleTerrainType.WallWalk &&
                                      groundGrids.Contains(fromGrid) &&
                                      wallWalkGrids.Contains(toGrid);
            var matchesWallToGround = fromCell.Terrain == BattleTerrainType.WallWalk &&
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

    private IEnumerable<(Vector2I Grid, bool UsesLadderBridge)> GetMovementNeighbors(Vector2I grid)
    {
        foreach (var neighbor in GetOrthogonalNeighbors(grid))
        {
            yield return (neighbor, false);
        }

        foreach (var bridgeNeighbor in GetCarLadderBridgeNeighbors(grid))
        {
            yield return (bridgeNeighbor, true);
        }
    }

    private IEnumerable<Vector2I> GetCarLadderBridgeNeighbors(Vector2I grid)
    {
        if (_mapData == null || _selectedUnit == null || !CanUseCarLadderBridge(_selectedUnit) || !IsWithinMap(grid))
        {
            yield break;
        }

        var currentCell = _mapData.GetCell(grid.X, grid.Y);
        foreach (var ladderGrid in GetUsableCarLadderGrids())
        {
            var bridgeGroundGrids = GetCarLadderGroundEndpoints(ladderGrid).ToList();
            var bridgeWallWalkGrids = GetCarLadderWallWalkEndpoints(ladderGrid).ToList();
            if (currentCell.Terrain == BattleTerrainType.WallWalk)
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

    private bool TryGetCarLadderBridgePath(Vector2I sourceGrid, Vector2I destinationGrid, BattleOccupantInfo movingOccupant, out Vector2[] pathPositions, out BattleSpriteDirection[] pathDirections)
    {
        pathPositions = [];
        pathDirections = [];
        if (_mapData == null || !CanUseCarLadderBridge(movingOccupant) || !IsWithinMap(sourceGrid) || !IsWithinMap(destinationGrid))
        {
            return false;
        }

        var sourceCell = _mapData.GetCell(sourceGrid.X, sourceGrid.Y);
        var destinationCell = _mapData.GetCell(destinationGrid.X, destinationGrid.Y);
        if (!ShouldUseCarLadderBridgeForMove(sourceCell, destinationCell))
        {
            return false;
        }

        if (sourceCell.Terrain == destinationCell.Terrain ||
            (sourceCell.Terrain != BattleTerrainType.WallWalk && destinationCell.Terrain != BattleTerrainType.WallWalk))
        {
            return false;
        }

        foreach (var ladderGrid in GetUsableCarLadderGrids())
        {
            var groundGrids = GetCarLadderGroundEndpoints(ladderGrid).ToList();
            var wallWalkGrids = GetCarLadderWallWalkEndpoints(ladderGrid).ToList();
            var pathGrids = new List<Vector2I>();
            if (sourceCell.Terrain != BattleTerrainType.WallWalk && wallWalkGrids.Contains(destinationGrid))
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
            else if (sourceCell.Terrain == BattleTerrainType.WallWalk && wallWalkGrids.Contains(sourceGrid))
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

    private static Vector2I? GetNearestGrid(Vector2I fromGrid, IEnumerable<Vector2I> candidates)
    {
        Vector2I? nearestGrid = null;
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

    private static void AddPathGrid(List<Vector2I> pathGrids, Vector2I grid, Vector2I sourceGrid)
    {
        if (grid == sourceGrid || (pathGrids.Count > 0 && pathGrids[^1] == grid))
        {
            return;
        }

        pathGrids.Add(grid);
    }

    private static BattleSpriteDirection[] BuildPathDirections(Vector2I sourceGrid, IReadOnlyList<Vector2I> pathGrids)
    {
        var directions = new BattleSpriteDirection[pathGrids.Count];
        var previousGrid = sourceGrid;
        for (var index = 0; index < pathGrids.Count; index++)
        {
            directions[index] = GetInfantryDirection(previousGrid, pathGrids[index]);
            previousGrid = pathGrids[index];
        }

        return directions;
    }

    private static bool ShouldUseCarLadderBridgeForMove(BattlePrototypeCellData sourceCell, BattlePrototypeCellData destinationCell)
    {
        if (sourceCell.Terrain == BattleTerrainType.WallWalk)
        {
            return !IsCastleInnerTerrain(destinationCell.Terrain);
        }

        if (destinationCell.Terrain == BattleTerrainType.WallWalk)
        {
            return !IsCastleInnerTerrain(sourceCell.Terrain);
        }

        return false;
    }

    private static bool IsCastleInnerTerrain(BattleTerrainType terrain)
    {
        return terrain == BattleTerrainType.Courtyard;
    }

    private IEnumerable<Vector2I> GetUsableCarLadderGrids()
    {
        foreach (var (grid, occupants) in _occupantsByGrid)
        {
            if (!IsWithinMap(grid))
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
            if (cell.Terrain != BattleTerrainType.WallWalk && !cell.IsBlockingStructure)
            {
                yield return neighbor;
            }
        }
    }

    private IEnumerable<Vector2I> GetCarLadderWallWalkEndpoints(Vector2I ladderGrid)
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

            var adjacentCell = _mapData.GetCell(adjacentGrid.X, adjacentGrid.Y);
            if (adjacentCell.Terrain == BattleTerrainType.WallWalk)
            {
                yield return adjacentGrid;
                continue;
            }

            if (adjacentCell.Structure is not (BattleStructureType.Wall or BattleStructureType.Gate))
            {
                continue;
            }

            var behindWallGrid = adjacentGrid + direction;
            if (!IsWithinMap(behindWallGrid))
            {
                continue;
            }

            var behindWallCell = _mapData.GetCell(behindWallGrid.X, behindWallGrid.Y);
            if (behindWallCell.Terrain == BattleTerrainType.WallWalk)
            {
                yield return behindWallGrid;
            }
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

    private bool HasBlockingOccupant(Vector2I grid)
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

    private static int GetMoveCost(BattlePrototypeCellData cell)
    {
        return cell.Terrain switch
        {
            BattleTerrainType.Forest => 2,
            _ => 1
        };
    }

    private void RefreshHighlights()
    {
        if (_highlightLayer == null || _groundLayer == null)
        {
            return;
        }

        Vector2? selectedCenter = _selectedGrid.HasValue ? GetHighlightPosition(_selectedGrid.Value) : null;
        var movableCenters = _movableGrids.Select(GetHighlightPosition);
        var attackCenters = _attackableGrids.Select(GetHighlightPosition);
        _highlightLayer.SetHighlights(selectedCenter, movableCenters, attackCenters);
    }

    private void OnMoveButtonPressed()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue)
        {
            return;
        }

        _commandMode = BattleCommandMode.MoveSelect;
        _attackableGrids.Clear();
        _movableGrids.Clear();
        foreach (var grid in CalculateReachableGrids(_selectedUnitGrid.Value, _selectedUnit.MoveRange))
        {
            _movableGrids.Add(grid);
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
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
        foreach (var grid in CalculateAttackableGrids(_selectedUnitGrid.Value, _selectedUnit.AttackRange))
        {
            _attackableGrids.Add(grid);
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
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
        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private void OnOpenGateButtonPressed()
    {
        if (!TryGetAdjacentOpenableGate(out var gateGrid) || _mapData == null)
        {
            return;
        }

        foreach (var groupGateGrid in GetConnectedGateGroup(gateGrid))
        {
            var gateCell = _mapData.GetCell(groupGateGrid.X, groupGateGrid.Y);
            gateCell.IsGateOpen = true;
            if (_castleLayer != null)
            {
                BattlePrototypeTileMapBuilder.SetCastleGateVisual(_castleLayer, groupGateGrid, isOpen: true);
            }
        }

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private bool TryGetAdjacentOpenableGate(out Vector2I gateGrid)
    {
        gateGrid = default;
        if (_mapData == null ||
            _selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            _selectedUnit.Category != CategoryUnit ||
            !IsAttackerPiece(_selectedUnit))
        {
            return false;
        }

        var unitGrid = _selectedUnitGrid.Value;
        if (!IsWithinMap(unitGrid))
        {
            return false;
        }

        var unitCell = _mapData.GetCell(unitGrid.X, unitGrid.Y);
        if (unitCell.Terrain is not (BattleTerrainType.Courtyard or BattleTerrainType.WallWalk))
        {
            return false;
        }

        foreach (var neighbor in GetOrthogonalNeighbors(unitGrid))
        {
            if (!IsWithinMap(neighbor))
            {
                continue;
            }

            var cell = _mapData.GetCell(neighbor.X, neighbor.Y);
            if (cell.Structure == BattleStructureType.Gate && !cell.IsGateOpen && !cell.IsBroken)
            {
                gateGrid = neighbor;
                return true;
            }
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

    private IEnumerable<Vector2I> CalculateAttackableGrids(Vector2I startGrid, int attackRange)
    {
        if (attackRange <= 0)
        {
            yield break;
        }

        for (var y = 0; y < BattlePrototypeMapData.Height; y++)
        {
            for (var x = 0; x < BattlePrototypeMapData.Width; x++)
            {
                var grid = new Vector2I(x, y);
                var distance = Mathf.Abs(grid.X - startGrid.X) + Mathf.Abs(grid.Y - startGrid.Y);
                if (distance > 0 && distance <= attackRange)
                {
                    yield return grid;
                }
            }
        }
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
                _unitMenuInfoLabel.Text =
                    $"Team: {_selectedUnit.TeamName}\n" +
                    $"Officer: {_selectedUnit.OfficerName}\n" +
                    $"Type: {_selectedUnit.TroopType}\n" +
                    $"Troops: {_selectedUnit.TroopCount:N0}";
            }
            else
            {
                _unitMenuInfoLabel.Text = "Team: -\nOfficer: -\nType: -\nTroops: -";
            }
        }

        if (_openGateButton != null)
        {
            _openGateButton.Visible = TryGetAdjacentOpenableGate(out _);
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
    }

    private void CancelCommandAction(bool clearSelection)
    {
        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        HideCommandMenu();

        if (clearSelection)
        {
            _selectedUnit = null;
            _selectedUnitGrid = null;
        }
    }

    private void OnEndTurnButtonPressed()
    {
        CancelCommandAction(clearSelection: true);

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

    private static string FormatCommandMode(BattleCommandMode commandMode)
    {
        return commandMode switch
        {
            BattleCommandMode.MoveSelect => "選擇移動目標",
            BattleCommandMode.AttackSelect => "選擇攻擊目標",
            BattleCommandMode.StrategySelect => "策略指令待定",
            BattleCommandMode.AwaitingCommand => "等待指令",
            _ => "無"
        };
    }

    private string GetCurrentTurnSideName()
    {
        return _currentTurnSide == BattleTurnSide.TeamA ? "Team A / 攻方" : "Team B / 守方";
    }

    private bool CanTraverseBlockedCell(Vector2I grid, BattlePrototypeCellData cell, bool usesLadderBridge = false)
    {
        _ = grid;

        if (!cell.IsBlockingStructure)
        {
            return true;
        }

        if (_selectedUnit == null)
        {
            return false;
        }

        if (cell.Structure == BattleStructureType.Gate && IsDefenderPiece(_selectedUnit) && _selectedUnit.Category == CategoryUnit)
        {
            return true;
        }

        if (usesLadderBridge && cell.Terrain == BattleTerrainType.WallWalk && CanUseCarLadderBridge(_selectedUnit))
        {
            return true;
        }

        return false;
    }

    private bool CanEnterCell(Vector2I sourceGrid, Vector2I destinationGrid, BattlePrototypeCellData cell, bool usesLadderBridge = false)
    {
        _ = destinationGrid;

        if (_selectedUnit == null)
        {
            return false;
        }

        var sourceCell = _mapData != null && IsWithinMap(sourceGrid)
            ? _mapData.GetCell(sourceGrid.X, sourceGrid.Y)
            : null;
        var sourceIsWallWalk = sourceCell?.Terrain == BattleTerrainType.WallWalk;
        var sourceIsCourtyard = sourceCell?.Terrain == BattleTerrainType.Courtyard;
        if (cell.Terrain == BattleTerrainType.WallWalk &&
            IsAttackerPiece(_selectedUnit) &&
            !sourceIsWallWalk &&
            !sourceIsCourtyard &&
            !(usesLadderBridge && CanUseCarLadderBridge(_selectedUnit)))
        {
            return false;
        }

        return true;
    }

    private static bool IsCellBlockingMovement(BattlePrototypeCellData cell)
    {
        return cell.IsBlockingStructure;
    }

    private static bool IsAttackerPiece(BattleOccupantInfo occupant)
    {
        return occupant.TeamName.Contains("攻方");
    }

    private static bool IsDefenderPiece(BattleOccupantInfo occupant)
    {
        return occupant.TeamName.Contains("守方");
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
            BattleTerrainType.Road => "道路",
            BattleTerrainType.Courtyard => "城內庭地",
            BattleTerrainType.Forest => "林地",
            BattleTerrainType.WallWalk => "城牆步道",
            BattleTerrainType.Grass => "草地",
            _ => "平地"
        };
    }

    private static string FormatStructure(BattleStructureType structure)
    {
        return structure switch
        {
            BattleStructureType.Wall => "城牆",
            BattleStructureType.Gate => "城門",
            BattleStructureType.Tower => "箭塔",
            BattleStructureType.Building => "建物",
            BattleStructureType.Tree => "樹木",
            BattleStructureType.RockBig => "大岩石",
            BattleStructureType.RockSmall => "小岩石",
            _ => "無"
        };
    }

    private static string FormatDeploymentZone(BattleDeploymentZone zone)
    {
        return zone switch
        {
            BattleDeploymentZone.Attacker => "攻方部署區",
            BattleDeploymentZone.Defender => "守方部署區",
            _ => "無"
        };
    }

    private Rect2 GetMapBounds()
    {
        var topLeft = BattlePrototypeMapRenderer.GridToWorld(new Vector2I(0, 0));
        var topRight = BattlePrototypeMapRenderer.GridToWorld(new Vector2I(BattlePrototypeMapData.Width - 1, 0));
        var bottomLeft = BattlePrototypeMapRenderer.GridToWorld(new Vector2I(0, BattlePrototypeMapData.Height - 1));
        var bottomRight = BattlePrototypeMapRenderer.GridToWorld(new Vector2I(BattlePrototypeMapData.Width - 1, BattlePrototypeMapData.Height - 1));

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
        int MoveRange,
        int AttackRange,
        BattlePieceMarker? Marker,
        BattleSpriteDirection FacingDirection);
    private sealed record BattleHudTeamInfo(string Name, int TotalTroops, int TotalGold, int TotalFood);

    private enum BattleCommandMode
    {
        None,
        AwaitingCommand,
        MoveSelect,
        AttackSelect,
        StrategySelect
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
