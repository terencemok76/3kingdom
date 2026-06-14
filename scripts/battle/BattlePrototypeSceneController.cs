using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ThreeKingdom.Battle;

public partial class BattlePrototypeSceneController : Node2D
{
    private const float MapPaddingLeft = 220.0f;
    private const float MapPaddingTop = 220.0f;
    private const float MapPaddingRight = 220.0f;
    private const float MapPaddingBottom = 320.0f;
    private static readonly BattleHudTeamInfo TeamAInfo = new("Team A / 攻方", 18000, 8200, 26000);
    private static readonly BattleHudTeamInfo TeamBInfo = new("Team B / 守方", 12500, 6400, 19800);
    private const string BattleDateText = "191年 4月 4日";
    private const string WeatherText = "晴";

    private BattlePrototypeMapData? _mapData;
    private Node2D? _mapRoot;
    private Camera2D? _camera;
    private TileMapLayer? _groundLayer;
    private BattlePrototypeHighlightRenderer? _highlightLayer;
    private Control? _commandMenu;
    private Label? _windowTitleLabel;
    private Label? _unitMenuInfoLabel;
    private Button? _endTurnButton;
    private Button? _moveButton;
    private Button? _attackButton;
    private Button? _strategyButton;
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

    public override void _Ready()
    {
        _mapData = BattlePrototypeMapData.CreateSiegeAssault();
        _mapRoot = GetNodeOrNull<Node2D>("MapRoot");
        _camera = GetNodeOrNull<Camera2D>("Camera2D");
        _groundLayer = GetNodeOrNull<TileMapLayer>("MapRoot/GroundLayer");
        _highlightLayer = GetNodeOrNull<BattlePrototypeHighlightRenderer>("MapRoot/HighlightLayer");
        _commandMenu = GetNodeOrNull<Control>("UiLayer/CommandMenu");
        _windowTitleLabel = GetNodeOrNull<Label>("UiLayer/CommandMenu/MenuMargin/MenuButtons/WindowTitleLabel");
        _unitMenuInfoLabel = GetNodeOrNull<Label>("UiLayer/CommandMenu/MenuMargin/MenuButtons/UnitMenuInfoLabel");
        _endTurnButton = GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/EndTurnButton");
        _moveButton = GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/MoveButton");
        _attackButton = GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/AttackButton");
        _strategyButton = GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/StrategyButton");
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

        if (_windowTitleLabel != null)
        {
            _windowTitleLabel.GuiInput += OnCommandMenuTitleGuiInput;
        }

        ConfigureMapLayers();
        PopulateMarkers();
        ConfigureHud();

        if (_mapRoot != null)
        {
            _mapRoot.Position = GetClampedMapPosition(_mapRoot.Position);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
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

    private void ConfigureMapLayers()
    {
        if (_mapData == null)
        {
            return;
        }

        ConfigureTileMapLayer("MapRoot/GroundLayer", BattlePrototypeTileLayerKind.Ground);
        ConfigureTileMapLayer("MapRoot/TerrainLayer", BattlePrototypeTileLayerKind.TerrainDetail);
        ConfigureRenderer("MapRoot/StructureLayer", BattlePrototypeRenderLayer.Structure);
        ConfigureTileMapLayer("MapRoot/OverlayLayer", BattlePrototypeTileLayerKind.DeploymentOverlay);
    }

    private void ConfigureRenderer(string path, BattlePrototypeRenderLayer renderLayer)
    {
        var renderer = GetNodeOrNull<BattlePrototypeMapRenderer>(path);
        renderer?.Configure(_mapData!, renderLayer);
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
        CreateMarker("MapRoot/UnitLayer/AttackerA", new Vector2I(10, 20), "步", "攻方步兵 A", "部隊", "Team A / 攻方", "夏侯淵", "步兵", 6200, new Color("ad4832"), new Color("f0d6a8"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/AttackerB", new Vector2I(12, 18), "弓", "攻方弓兵 B", "部隊", "Team A / 攻方", "張郃", "弓兵", 5400, new Color("b96d2c"), new Color("f0d6a8"), moveRange: 4, attackRange: 3);
        CreateMarker("MapRoot/UnitLayer/AttackerC", new Vector2I(14, 20), "騎", "攻方騎兵 C", "部隊", "Team A / 攻方", "曹純", "騎兵", 4800, new Color("8f3f31"), new Color("f0d6a8"), moveRange: 6, attackRange: 1);
        CreateMarker("MapRoot/SiegeEngineLayer/Ram", new Vector2I(12, 16), "衝", "衝車", "攻城器", "Team A / 攻方", "樂進", "衝車", 900, new Color("7a4a20"), new Color("ead7aa"), 21.0f, moveRange: 3, attackRange: 1);
        CreateMarker("MapRoot/SiegeEngineLayer/Ladder", new Vector2I(10, 15), "梯", "雲梯隊", "攻城器", "Team A / 攻方", "于禁", "雲梯", 800, new Color("8c7b44"), new Color("ead7aa"), 21.0f, moveRange: 3, attackRange: 1);
        CreateMarker("MapRoot/SiegeEngineLayer/Catapult", new Vector2I(14, 15), "投", "投石機", "攻城器", "Team A / 攻方", "劉曄", "投石車", 600, new Color("6e5131"), new Color("ead7aa"), 21.0f, moveRange: 2, attackRange: 4);

        CreateMarker("MapRoot/UnitLayer/DefenderA", new Vector2I(11, 5), "守", "守軍步兵 A", "部隊", "Team B / 守方", "董卓", "步兵", 5100, new Color("326b8d"), new Color("e0f0ff"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/DefenderB", new Vector2I(13, 5), "弩", "守軍弩兵 B", "部隊", "Team B / 守方", "李傕", "弩兵", 4300, new Color("245f76"), new Color("e0f0ff"), moveRange: 4, attackRange: 3);
        CreateMarker("MapRoot/UnitLayer/DefenderC", new Vector2I(12, 3), "將", "守軍主將", "部隊", "Team B / 守方", "郭汜", "親衛", 3100, new Color("274e8a"), new Color("e0f0ff"), moveRange: 4, attackRange: 1);
    }

    private void CreateMarker(string path, Vector2I grid, string label, string displayName, string category, string teamName, string officerName, string troopType, int troopCount, Color fillColor, Color borderColor, float radius = 19.0f, int moveRange = 0, int attackRange = 1)
    {
        var marker = GetNodeOrNull<BattlePieceMarker>(path);
        if (marker == null)
        {
            return;
        }

        var markerBasePosition = _groundLayer?.MapToLocal(grid) ?? BattlePrototypeMapRenderer.GridToWorld(grid);
        marker.Position = markerBasePosition + new Vector2(0.0f, -16.0f);
        marker.Setup(label, fillColor, borderColor, radius);
        RegisterOccupant(grid, displayName, category, label, teamName, officerName, troopType, troopCount, moveRange, attackRange, marker);
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
                if (!_selectedGrid.HasValue || !_attackableGrids.Contains(_selectedGrid.Value))
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
        var candidate = _groundLayer.LocalToMap(localMouse);
        Vector2I? newHoverGrid = IsWithinMap(candidate) ? candidate : null;
        if (_hoverGrid == newHoverGrid)
        {
            return;
        }

        _hoverGrid = newHoverGrid;
        RefreshCoordinateLabel();
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
        builder.AppendLine($"部署區: {FormatDeploymentZone(cell.DeploymentZone)}");
        builder.AppendLine($"高度: {cell.HeightLevel}");
        builder.AppendLine($"移動阻擋: {(cell.BlocksMovement ? "是" : "否")}");
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

        occupants.Add(new BattleOccupantInfo(displayName, category, shortLabel, teamName, officerName, troopType, troopCount, moveRange, attackRange, marker));
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

        var movingOccupant = sourceOccupants.FirstOrDefault(static occupant => occupant.Marker != null && (occupant.Category == "部隊" || occupant.Category == "攻城器"));
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

        var movedOccupant = movingOccupant with { Marker = movingOccupant.Marker };
        destinationOccupants.Add(movedOccupant);
        movingOccupant.Marker.Position = _groundLayer.MapToLocal(destinationGrid) + new Vector2(0.0f, -16.0f);

        _selectedUnitGrid = destinationGrid;
        _selectedUnit = movedOccupant;
        _selectedGrid = destinationGrid;
        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        HideCommandMenu();

        return true;
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

        var selectedUnit = occupants.FirstOrDefault(static occupant => occupant.Category is "部隊" or "攻城器");
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
            foreach (var neighbor in GetOrthogonalNeighbors(current.Grid))
            {
                if (!IsWithinMap(neighbor))
                {
                    continue;
                }

                var cell = _mapData.GetCell(neighbor.X, neighbor.Y);
                if (cell.BlocksMovement)
                {
                    continue;
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

        return occupants.Any(static occupant => occupant.Category is "部隊" or "攻城器");
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

        Vector2? selectedCenter = _selectedGrid.HasValue ? _groundLayer.MapToLocal(_selectedGrid.Value) : null;
        var movableCenters = _movableGrids.Select(_groundLayer.MapToLocal);
        var attackCenters = _attackableGrids.Select(_groundLayer.MapToLocal);
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
        BattlePieceMarker? Marker);
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
}
