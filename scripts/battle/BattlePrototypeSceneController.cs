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
    MoveHighlight,
    AttackHighlight,
    SelectedHighlight,
    SiegeEngine,
    Unit
}

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
//    private static readonly Vector2 WallTopVisualOffset = new(32.0f, -8.0f);
    private static readonly Vector2 WallTopVisualOffset = new(32.0f, -60.0f);
    // A wall-top unit must render after the NE wall segments that overlap it from the right.
    private const int WallTopLevelDepthOffset = 3;
    private const int NorthEastWallOcclusionDepth = 2;
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
    private const int InfantryAttackDamage = 850;
    private const int SpearmanAttackDamage = 800;
    private const int ArcherAttackDamage = 900;
    private const int CavalryAttackDamage = 1100;
    private const int RamAttackDamage = 500;
    private const int CatapultAttackDamage = 1300;
    private const int InfantryStructureDamage = 180;
    private const int SpearmanStructureDamage = 160;
    private const int ArcherStructureDamage = 120;
    private const int CavalryStructureDamage = 220;
    private const int RamStructureDamage = 900;
    private const int CatapultStructureDamage = 700;
    private const int RamMaxHitPoints = 2800;
    private const int LadderMaxHitPoints = 2200;
    private const int CatapultMaxHitPoints = 1800;
    private const double DamagePopupDurationSeconds = 2.0;
    private static readonly BattleHudTeamInfo TeamAInfo = new("Team A / Attacker", 18000, 8200, 26000);
    private static readonly BattleHudTeamInfo TeamBInfo = new("Team B / Defender", 12500, 6400, 19800);
    private const string BattleDateText = "191 Apr 4";
    private const string WeatherText = "Sunny";

    private BattlePrototypeMapData? _mapData;
    private Node2D? _mapRoot;
    private Camera2D? _camera;
    private TileMapLayer? _groundLayer;
    private TileMapLayer? _objectLayer;
    private TileMapLayer? _castleLayer;
    private TileMapLayer? _overlayLayer;
    private BattlePrototypeHighlightRenderer? _highlightLayer;
    private Node2D? _battleDepthLayer;
    private Node2D? _occludedUnitSilhouetteLayer;
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
    private BattleGridKey? _hoverGridKey;
    private BattleGridKey? _selectedGridKey;
    private BattleGridKey? _selectedUnitGrid;
    private BattleOccupantInfo? _selectedUnit;
    private readonly HashSet<BattleGridKey> _movableGrids = new();
    private readonly HashSet<BattleGridKey> _attackableGrids = new();
    private readonly Dictionary<BattleGridKey, List<BattleOccupantInfo>> _occupantsByGrid = new();
    private readonly Dictionary<Node2D, BattleDepthEntry> _battleDepthEntries = new();
    private readonly Dictionary<Vector2I, Sprite2D> _castleDepthSpritesByGrid = new();
    private readonly List<BattlePrototypeHighlightRenderer> _highlightDepthVisuals = new();
    private readonly Dictionary<BattleGridKey, Node2D> _occludedUnitSilhouettesByGrid = new();
    private BattleCommandMode _commandMode = BattleCommandMode.None;
    private int _turnNumber = 1;
    private BattleTurnSide _currentTurnSide = BattleTurnSide.TeamA;
    private bool _editorBakePrototypeLayout;
    private bool _editorClearTileLayout;

    private readonly record struct BattleDepthEntry(Node2D Node, BattleGridKey Grid, BattleDepthRenderKind Kind, int LocalOrder);

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

    private void CacheSceneNodes()
    {
        _mapRoot ??= GetNodeOrNull<Node2D>("MapRoot");
        _camera ??= GetNodeOrNull<Camera2D>("Camera2D");
        _groundLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/GroundLayer");
        _objectLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/ObjectLayer");
        _castleLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/CastleLayer");
        _overlayLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/OverlayLayer");
        _highlightLayer ??= GetNodeOrNull<BattlePrototypeHighlightRenderer>("MapRoot/HighlightLayer");
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

        for (var y = 0; y < BattlePrototypeMapData.Height; y++)
        {
            for (var x = 0; x < BattlePrototypeMapData.Width; x++)
            {
                var cell = _mapData.GetCell(x, y);
                if (!BattlePrototypeTileMapBuilder.TryGetCastleSpriteSpec(cell, out var spec))
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

    private Sprite2D CreateCastleDepthSprite(Vector2I grid, BattlePrototypeTileMapBuilder.BattleTileSpriteSpec spec)
    {
        var sprite = new Sprite2D
        {
            Name = $"Castle_{grid.X}_{grid.Y}",
            Texture = spec.Texture,
            RegionEnabled = true,
            RegionRect = spec.Region,
            Centered = false,
            Offset = -spec.Pivot,
            Position = GetCastleDepthSpritePosition(grid),
            ZIndex = 0
        };
        return sprite;
    }

    private Vector2 GetCastleDepthSpritePosition(Vector2I grid)
    {
        return _groundLayer?.MapToLocal(grid) ?? BattlePrototypeMapRenderer.GridToWorld(grid);
    }

    private void RefreshCastleDepthVisual(Vector2I grid)
    {
        if (_battleDepthLayer == null || _mapData == null || !IsWithinMap(grid))
        {
            return;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (!BattlePrototypeTileMapBuilder.TryGetCastleSpriteSpec(cell, out var spec))
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
            BattleDepthRenderKind.SelectedHighlight => 1,
            BattleDepthRenderKind.SiegeEngine or
            BattleDepthRenderKind.Unit => 2,
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
            return !cell.IsGateOpen && !cell.IsBroken;
        }

        if (IsWallTopGrid(grid.Grid))
        {
            return false;
        }

        // NE-facing castle walls visually cover the L0 cells behind them:
        // wall grid (x, wallY) occludes (x, wallY - 1) and (x, wallY - 2).
        // Since this check starts from the occupant grid, look forward to y + depth.
        for (var yOffset = 1; yOffset <= NorthEastWallOcclusionDepth; yOffset++)
        {
            var blockingGrid = new Vector2I(grid.X, grid.Y + yOffset);
            if (IsWithinMap(blockingGrid) && IsWallTopGrid(blockingGrid))
            {
                return true;
            }
        }

        return false;
    }

    private static Color GetOccludedUnitSilhouetteColor(BattleOccupantInfo occupant)
    {
        return IsAttackerPiece(occupant)
            ? new Color(1.0f, 0.18f, 0.10f, 0.42f)
            : new Color(0.25f, 0.62f, 1.0f, 0.42f);
    }

    private void PopulateMarkers()
    {
        CreateMarker("MapRoot/UnitLayer/AttackerA", new Vector2I(10, 20), "I", "Attacker Infantry A", CategoryUnit, "Team A / Attacker", "Xiahou Yuan", TroopInfantry, 6200, new Color("ad4832"), new Color("f0d6a8"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Spearman", new Vector2I(8, 18), "S", "Attacker Spearman", CategoryUnit, "Team A / Attacker", "Cao Hong", TroopSpearman, 4200, new Color("9b5931"), new Color("f0d6a8"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/AttackerB", new Vector2I(12, 18), "A", "Attacker Archer B", CategoryUnit, "Team A / Attacker", "Zhang He", TroopArcher, 5400, new Color("b96d2c"), new Color("f0d6a8"), moveRange: 4, attackRange: 3);
        CreateMarker("MapRoot/UnitLayer/AttackerC", new Vector2I(14, 20), "C", "Attacker Cavalry C", CategoryUnit, "Team A / Attacker", "Cao Chun", TroopCavalry, 4800, new Color("8f3f31"), new Color("f0d6a8"), moveRange: 6, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Ram", new Vector2I(12, 16), "R", "Battering Ram", CategorySiegeEngine, "Team A / Attacker", "Yue Jin", TroopRam, RamMaxHitPoints, new Color("7a4a20"), new Color("ead7aa"), 21.0f, moveRange: 3, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Ladder", new Vector2I(10, 15), "L", "Siege Ladder", CategorySiegeEngine, "Team A / Attacker", "Yu Jin", TroopLadder, LadderMaxHitPoints, new Color("8c7b44"), new Color("ead7aa"), 21.0f, moveRange: 3, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Catapult", new Vector2I(14, 15), "T", "Catapult", CategorySiegeEngine, "Team A / Attacker", "Liu Ye", TroopCatapult, CatapultMaxHitPoints, new Color("6e5131"), new Color("ead7aa"), 21.0f, moveRange: 2, attackRange: 4);

        CreateMarker("MapRoot/UnitLayer/DefenderA", new Vector2I(10, 7), "D", "Defender Infantry A", CategoryUnit, "Team B / Defender", "Dong Zhuo", TroopInfantry, 5100, new Color("326b8d"), new Color("e0f0ff"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/DefenderB", new Vector2I(14, 7), "X", "Defender Crossbow B", CategoryUnit, "Team B / Defender", "Li Jue", TroopArcher, 4300, new Color("245f76"), new Color("e0f0ff"), moveRange: 4, attackRange: 3);
        CreateMarker("MapRoot/UnitLayer/DefenderC", new Vector2I(12, 7), "G", "Defender Commander", CategoryUnit, "Team B / Defender", "Guo Si", TroopSpearman, 3100, new Color("274e8a"), new Color("e0f0ff"), moveRange: 4, attackRange: 1);
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
        var grid = gridKey.Grid;
        var gridCenter = _groundLayer?.MapToLocal(grid) ?? BattlePrototypeMapRenderer.GridToWorld(grid);
        var wallTopOffset = gridKey.Level == 2 ? WallTopVisualOffset : Vector2.Zero;
        return gridCenter + wallTopOffset + new Vector2(0.0f, GetUnitVisualLift(gridKey));
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
        var grid = gridKey.Grid;
        var gridCenter = _groundLayer?.MapToLocal(grid) ?? BattlePrototypeMapRenderer.GridToWorld(grid);
        if (gridKey.Level == 2)
        {
            return gridCenter + WallTopVisualOffset + new Vector2(0.0f, WallWalkHighlightVisualLift);
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
            titleLabel.Text = $"Date: {BattleDateText}   Weather: {WeatherText}   Turn: {_turnNumber}   Acting Side: {GetCurrentTurnSideName()}";
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
               grid.X < BattlePrototypeMapData.Width &&
               grid.Y >= 0 &&
               grid.Y < BattlePrototypeMapData.Height;
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
            return "Tile Info\nCoordinate: -\nClick a tile to inspect terrain, structure, deployment zone, and units.";
        }

        var grid = _selectedGrid.Value;
        var cell = _mapData.GetCell(grid.X, grid.Y);
        var builder = new StringBuilder();
        builder.AppendLine("Tile Info");
        builder.AppendLine($"Coordinate: {FormatGrid(_selectedGridKey, _selectedGrid)}");
        builder.AppendLine($"Terrain: {FormatTerrain(cell.Terrain)}");
        builder.AppendLine($"Structure: {FormatStructure(cell.Structure)}");
        if (cell.HasStructureHealth)
        {
            var durability = GetDisplayStructureDurability(grid, cell);
            builder.AppendLine($"Durability: {durability.Current}/{durability.Max}");
            builder.AppendLine($"Status: {(cell.IsBroken ? "Broken" : "Intact")}");
        }

        builder.AppendLine($"Deployment: {FormatDeploymentZone(cell.DeploymentZone)}");
        builder.AppendLine($"Height: {cell.HeightLevel}");
        builder.AppendLine($"Blocks Move: {(IsCellBlockingMovement(cell) ? "Yes" : "No")}");
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
            builder.AppendLine($"- Command State: {FormatCommandMode(_commandMode)}");
            builder.AppendLine($"- Current Turn: {GetCurrentTurnSideName()}");
        }

        return builder.ToString().TrimEnd();
    }

    private (int Current, int Max) GetDisplayStructureDurability(Vector2I grid, BattlePrototypeCellData cell)
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
        return (current, BattlePrototypeCellData.GateMaxHealth);
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
        var onMoveComplete = CreateMoveCompleteCallback(movedOccupant, sourceGrid, destinationGrid);
        ApplyMoveAnimation(movedOccupant, moveDirection, GetMarkerPosition(destinationGrid), pathPositions, pathDirections, onMoveComplete);

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

    private Action CreateMoveCompleteCallback(BattleOccupantInfo movedOccupant, BattleGridKey sourceGrid, BattleGridKey destinationGrid)
    {
        if (movedOccupant.Marker == null || !ShouldUseOccludedMovingSilhouette(sourceGrid, destinationGrid))
        {
            return RefreshOccludedUnitSilhouettes;
        }

        var marker = movedOccupant.Marker;
        var originalModulate = marker.Modulate;
        marker.Modulate = GetOccludedUnitSilhouetteColor(movedOccupant);
        return () =>
        {
            marker.Modulate = originalModulate;
            RefreshOccludedUnitSilhouettes();
        };
    }

    private bool ShouldUseOccludedMovingSilhouette(BattleGridKey sourceGrid, BattleGridKey destinationGrid)
    {
        return IsUnitOccludedByCastleVisual(sourceGrid) &&
               IsUnitOccludedByCastleVisual(destinationGrid);
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
        ApplyAttackDamage(attackingUnit, targetGrid, Math.Max(attackAnimationDuration, hurtAnimationDuration));
        if (shouldTemporarilyRevealOccludedUnits)
        {
            RefreshOccludedUnitSilhouettesAfterDelay(Math.Max(attackAnimationDuration, hurtAnimationDuration));
        }

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        HideCommandMenu();
        return true;
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

    private static void ApplyMoveAnimation(BattleOccupantInfo occupant, BattleSpriteDirection direction, Vector2 destinationPosition, Vector2[]? pathPositions = null, BattleSpriteDirection[]? pathDirections = null, Action? onComplete = null)
    {
        if (occupant.Marker == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopInfantry)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetInfantryMoveScene), InfantryMoveAnimationDurationSeconds, GetInfantryMoveScene(direction), GetInfantryIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopSpearman)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetSpearmanMoveScene), SpearmanMoveAnimationDurationSeconds, GetSpearmanMoveScene(direction), GetSpearmanIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopRam)
        {
            var carIdleScene = GetCarIdleScene(direction);
            occupant.Marker.MoveTo(
                destinationPosition,
                CarMoveAnimationDurationSeconds,
                carIdleScene,
                carIdleScene,
                onComplete);
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopLadder)
        {
            var carLadderIdleScene = GetCarLadderIdleScene(direction);
            occupant.Marker.MoveTo(
                destinationPosition,
                CarMoveAnimationDurationSeconds,
                carLadderIdleScene,
                carLadderIdleScene,
                onComplete);
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopArcher)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetArcherMoveScene), ArcherMoveAnimationDurationSeconds, GetArcherMoveScene(direction), GetArcherIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopCavalry)
        {
            occupant.Marker.MoveTo(
                destinationPosition,
                CavalryMoveAnimationDurationSeconds,
                GetCavalryMoveScene(direction),
                GetCavalryIdleScene(direction),
                onComplete);
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopCatapult)
        {
            var catapultIdleScene = GetCatapultIdleScene(direction);
            occupant.Marker.MoveTo(
                destinationPosition,
                CatapultMoveAnimationDurationSeconds,
                catapultIdleScene,
                catapultIdleScene,
                onComplete);
            return;
        }

        occupant.Marker.Position = destinationPosition;
        onComplete?.Invoke();
    }

    private static void MoveMarker(BattlePieceMarker marker, Vector2 destinationPosition, Vector2[]? pathPositions, string[]? pathMoveScenePaths, double duration, string moveScenePath, string idleScenePath, Action? onComplete = null)
    {
        if (pathPositions is { Length: > 0 })
        {
            if (pathMoveScenePaths is { Length: > 0 })
            {
                marker.MoveAlong(pathPositions, duration, pathMoveScenePaths, idleScenePath, onComplete);
                return;
            }

            marker.MoveAlong(pathPositions, duration, moveScenePath, idleScenePath, onComplete);
            return;
        }

        marker.MoveTo(destinationPosition, duration, moveScenePath, idleScenePath, onComplete);
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

        target.Marker.PlayAction(
            GetInfantryHurtScene(hurtDirection),
            GetInfantryIdleScene(target.FacingDirection),
            InfantryHurtAnimationDurationSeconds);
        return InfantryHurtAnimationDurationSeconds;
    }

    private void ApplyAttackDamage(BattleOccupantInfo attacker, BattleGridKey targetGrid, double effectDelaySeconds)
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

        var damage = GetAttackDamage(attacker);
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

        var frontier = new Queue<(BattleGridKey Grid, int RemainingMove)>();
        var bestRemaining = new Dictionary<BattleGridKey, int> { [startGrid] = moveRange };
        var previousByGrid = new Dictionary<BattleGridKey, BattleGridKey>();
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
                if (!IsWithinMap(neighbor.Grid))
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
        ClearHighlightDepthVisuals();
        if (_battleDepthLayer == null || _groundLayer == null)
        {
            return;
        }

        foreach (var grid in _movableGrids)
        {
            var visualKind = grid.Level == 2
                ? BattlePrototypeHighlightVisualKind.WallTopMovable
                : BattlePrototypeHighlightVisualKind.Movable;
            AddHighlightDepthVisual(grid, visualKind);
        }

        foreach (var grid in _attackableGrids)
        {
            AddHighlightDepthVisual(grid, BattlePrototypeHighlightVisualKind.Attackable);
        }

        if (_selectedGridKey.HasValue)
        {
            AddHighlightDepthVisual(_selectedGridKey.Value, BattlePrototypeHighlightVisualKind.Selected);
        }
        else if (_selectedGrid.HasValue)
        {
            AddHighlightDepthVisual(GetDefaultGridKey(_selectedGrid.Value), BattlePrototypeHighlightVisualKind.Selected);
        }

        RefreshBattleDepthLayerOrder();
    }

    private void AddHighlightDepthVisual(BattleGridKey grid, BattlePrototypeHighlightVisualKind visualKind)
    {
        if (_battleDepthLayer == null)
        {
            return;
        }

        var visual = new BattlePrototypeHighlightRenderer
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

    private static BattleDepthRenderKind ToBattleDepthRenderKind(BattlePrototypeHighlightVisualKind visualKind)
    {
        return visualKind switch
        {
            BattlePrototypeHighlightVisualKind.Movable or BattlePrototypeHighlightVisualKind.WallTopMovable => BattleDepthRenderKind.MoveHighlight,
            BattlePrototypeHighlightVisualKind.Attackable => BattleDepthRenderKind.AttackHighlight,
            BattlePrototypeHighlightVisualKind.Selected => BattleDepthRenderKind.SelectedHighlight,
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
        foreach (var grid in CalculateAttackableGrids(_selectedUnitGrid.Value, _selectedUnit))
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
                BattlePrototypeTileMapBuilder.SetCastleGateVisual(_castleLayer, groupGateGrid, isOpen: true);
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
                BattlePrototypeTileMapBuilder.SetCastleGateVisual(_castleLayer, groupGateGrid, isOpen: gateCell.IsGateOpen);
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

        for (var y = 0; y < BattlePrototypeMapData.Height; y++)
        {
            for (var x = 0; x < BattlePrototypeMapData.Width; x++)
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
        return cell.Structure == BattleStructureType.Gate && cell.HasStructureHealth && !cell.IsBroken;
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
            _selectedGridKey = null;
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
            BattleCommandMode.MoveSelect => "Select Move Target",
            BattleCommandMode.AttackSelect => "Select Attack Target",
            BattleCommandMode.StrategySelect => "Strategy Pending",
            BattleCommandMode.AwaitingCommand => "Awaiting Command",
            _ => "None"
        };
    }

    private string GetCurrentTurnSideName()
    {
        return _currentTurnSide == BattleTurnSide.TeamA ? "Team A / Attacker" : "Team B / Defender";
    }

    private bool CanTraverseBlockedCell(BattleGridKey grid, BattlePrototypeCellData cell, bool usesLadderBridge = false)
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

    private bool CanEnterCell(BattleGridKey sourceGrid, BattleGridKey destinationGrid, BattlePrototypeCellData cell, bool usesLadderBridge = false)
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

        for (var y = grid.Y + 1; y < BattlePrototypeMapData.Height; y++)
        {
            var cell = _mapData.GetCell(grid.X, y);
            if (cell.Structure is BattleStructureType.Wall or BattleStructureType.Gate or BattleStructureType.Tower)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCellBlockingMovement(BattlePrototypeCellData cell)
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
        int HitPoints,
        int MaxHitPoints,
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
