using Godot;

namespace ThreeKingdom.Battle;

public partial class BattlePrototypeSceneController : Node2D
{
    private const float MapPaddingLeft = 220.0f;
    private const float MapPaddingTop = 220.0f;
    private const float MapPaddingRight = 220.0f;
    private const float MapPaddingBottom = 320.0f;

    private BattlePrototypeMapData? _mapData;
    private Node2D? _mapRoot;
    private Camera2D? _camera;
    private TileMapLayer? _groundLayer;
    private bool _isDraggingMap;
    private Vector2 _lastMousePosition;
    private Vector2I? _hoverGrid;
    private Vector2I? _selectedGrid;

    public override void _Ready()
    {
        _mapData = BattlePrototypeMapData.CreateSiegeAssault();
        _mapRoot = GetNodeOrNull<Node2D>("MapRoot");
        _camera = GetNodeOrNull<Camera2D>("Camera2D");
        _groundLayer = GetNodeOrNull<TileMapLayer>("MapRoot/GroundLayer");
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
        CreateMarker("MapRoot/UnitLayer/AttackerA", new Vector2I(10, 20), "步", new Color("ad4832"), new Color("f0d6a8"));
        CreateMarker("MapRoot/UnitLayer/AttackerB", new Vector2I(12, 18), "弓", new Color("b96d2c"), new Color("f0d6a8"));
        CreateMarker("MapRoot/UnitLayer/AttackerC", new Vector2I(14, 20), "騎", new Color("8f3f31"), new Color("f0d6a8"));
        CreateMarker("MapRoot/SiegeEngineLayer/Ram", new Vector2I(12, 16), "衝", new Color("7a4a20"), new Color("ead7aa"), 21.0f);
        CreateMarker("MapRoot/SiegeEngineLayer/Ladder", new Vector2I(10, 15), "梯", new Color("8c7b44"), new Color("ead7aa"), 21.0f);
        CreateMarker("MapRoot/SiegeEngineLayer/Catapult", new Vector2I(14, 15), "投", new Color("6e5131"), new Color("ead7aa"), 21.0f);

        CreateMarker("MapRoot/UnitLayer/DefenderA", new Vector2I(11, 5), "守", new Color("326b8d"), new Color("e0f0ff"));
        CreateMarker("MapRoot/UnitLayer/DefenderB", new Vector2I(13, 5), "弩", new Color("245f76"), new Color("e0f0ff"));
        CreateMarker("MapRoot/UnitLayer/DefenderC", new Vector2I(12, 3), "將", new Color("274e8a"), new Color("e0f0ff"));
    }

    private void CreateMarker(string path, Vector2I grid, string label, Color fillColor, Color borderColor, float radius = 19.0f)
    {
        var marker = GetNodeOrNull<BattlePieceMarker>(path);
        if (marker == null)
        {
            return;
        }

        marker.Position = BattlePrototypeMapRenderer.GridToWorld(grid) + new Vector2(0.0f, -16.0f);
        marker.Setup(label, fillColor, borderColor, radius);
    }

    private void ConfigureHud()
    {
        var titleLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/TitleLabel");
        var summaryLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/SummaryLabel");
        var layoutLabel = GetNodeOrNull<Label>("UiLayer/SidePanel/Margin/LayoutLabel");
        var coordinateLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/CoordinateLabel");

        if (titleLabel != null)
        {
            titleLabel.Text = "Phase 4 Battle Prototype";
        }

        if (summaryLabel != null)
        {
            summaryLabel.Text = "25x25 Isometric Siege  |  攻方: 3 部隊 + 3 攻城器  |  守方: 3 部隊  |  城門: 中央三格";
        }

        if (layoutLabel != null)
        {
            layoutLabel.Text =
                "Layer Plan\n" +
                "- Ground\n" +
                "- Terrain\n" +
                "- Structure\n" +
                "- Overlay\n\n" +
                "Rows 0-5: 城內\n" +
                "Rows 6-7: 城牆/城門\n" +
                "Rows 8-24: 城外攻方區";
        }

        if (coordinateLabel != null)
        {
            coordinateLabel.Text = BuildCoordinateText();
        }
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
        {
            UpdateHoverGrid();
            _selectedGrid = _hoverGrid;
            RefreshCoordinateLabel();
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

    private string BuildCoordinateText()
    {
        return $"Hover: {FormatGrid(_hoverGrid)}    Click: {FormatGrid(_selectedGrid)}";
    }

    private static string FormatGrid(Vector2I? grid)
    {
        return grid.HasValue ? $"({grid.Value.X}, {grid.Value.Y})" : "-";
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
}
