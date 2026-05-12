using System;
using System.Collections.Generic;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

public partial class MapController : Node2D
{
    private const float FrameOuterInset = 10.0f;
    private const float FrameWoodInset = 12.0f;
    private const float FrameParchmentInset = 22.0f;
    private const float DragOverscrollResistance = 0.3f;
    private const float ReturnSpringStrength = 18.0f;
    private const float ReturnSpringDamping = 0.82f;
    private sealed partial class MapPresentationLayer : Node2D
    {
        private readonly Func<Rect2> _mapViewportRectProvider;

        public MapPresentationLayer(Func<Rect2> mapViewportRectProvider)
        {
            _mapViewportRectProvider = mapViewportRectProvider;
        }

        public bool IsOverlay { get; set; }

        public override void _Draw()
        {
            var viewport = GetViewportRect();
            if (viewport.Size == Vector2.Zero)
            {
                return;
            }

            var outerFrame = new Rect2(
                new Vector2(FrameOuterInset, FrameOuterInset),
                viewport.Size - new Vector2(FrameOuterInset * 2.0f, FrameOuterInset * 2.0f));
            var woodInner = outerFrame.Grow(-FrameWoodInset);
            var parchmentRect = _mapViewportRectProvider();

            if (IsOverlay)
            {
                DrawRectBands(new Rect2(Vector2.Zero, viewport.Size), outerFrame, new Color("241f1a"));
                DrawRectBands(outerFrame, woodInner, new Color("4b3928"));
                DrawRectBands(woodInner, parchmentRect, new Color("6a543d"));

                var edge = 28.0f;
                var vignette = new Color(0.03f, 0.025f, 0.02f, 0.22f);
                DrawRect(new Rect2(0.0f, 0.0f, viewport.Size.X, edge), vignette);
                DrawRect(new Rect2(0.0f, viewport.Size.Y - edge, viewport.Size.X, edge), vignette);
                DrawRect(new Rect2(0.0f, 0.0f, edge, viewport.Size.Y), vignette);
                DrawRect(new Rect2(viewport.Size.X - edge, 0.0f, edge, viewport.Size.Y), vignette);

                var highlight = new Color("b59a72", 0.34f);
                var shadow = new Color("2e2117", 0.48f);
                DrawFrameLine(outerFrame, highlight, shadow);
                DrawFrameLine(woodInner, new Color("cfb48b", 0.18f), new Color("3a2b1d", 0.32f));
                DrawFrameLine(parchmentRect, new Color("efe1bc", 0.12f), new Color("45372a", 0.16f));

                DrawCornerPlate(outerFrame.Position + new Vector2(12.0f, 12.0f), false, false);
                DrawCornerPlate(new Vector2(outerFrame.End.X - 12.0f, outerFrame.Position.Y + 12.0f), true, false);
                DrawCornerPlate(new Vector2(outerFrame.Position.X + 12.0f, outerFrame.End.Y - 12.0f), false, true);
                DrawCornerPlate(outerFrame.End - new Vector2(12.0f, 12.0f), true, true);
                return;
            }

            DrawRect(new Rect2(Vector2.Zero, viewport.Size), new Color("241f1a"));
            DrawRect(outerFrame, new Color("4b3928"));
            DrawRect(woodInner, new Color("6a543d"));
            DrawRect(parchmentRect, new Color("6e6657", 0.42f));
        }

        private void DrawFrameLine(Rect2 rect, Color lightColor, Color darkColor)
        {
            DrawLine(rect.Position, new Vector2(rect.End.X, rect.Position.Y), lightColor, 2.0f);
            DrawLine(rect.Position, new Vector2(rect.Position.X, rect.End.Y), lightColor, 2.0f);
            DrawLine(new Vector2(rect.Position.X, rect.End.Y), rect.End, darkColor, 2.0f);
            DrawLine(new Vector2(rect.End.X, rect.Position.Y), rect.End, darkColor, 2.0f);
        }

        private void DrawRectBands(Rect2 outerRect, Rect2 innerRect, Color color)
        {
            DrawRect(new Rect2(outerRect.Position, new Vector2(outerRect.Size.X, innerRect.Position.Y - outerRect.Position.Y)), color);
            DrawRect(new Rect2(new Vector2(outerRect.Position.X, innerRect.End.Y), new Vector2(outerRect.Size.X, outerRect.End.Y - innerRect.End.Y)), color);
            DrawRect(new Rect2(new Vector2(outerRect.Position.X, innerRect.Position.Y), new Vector2(innerRect.Position.X - outerRect.Position.X, innerRect.Size.Y)), color);
            DrawRect(new Rect2(new Vector2(innerRect.End.X, innerRect.Position.Y), new Vector2(outerRect.End.X - innerRect.End.X, innerRect.Size.Y)), color);
        }

        private void DrawCornerPlate(Vector2 anchor, bool flipX, bool flipY)
        {
            var width = 34.0f;
            var thickness = 8.0f;
            var horizontalOrigin = new Vector2(
                flipX ? anchor.X - width : anchor.X,
                flipY ? anchor.Y - thickness : anchor.Y);
            var verticalOrigin = new Vector2(
                flipX ? anchor.X - thickness : anchor.X,
                flipY ? anchor.Y - width : anchor.Y);

            var plateColor = new Color("8b734e", 0.72f);
            var rivetColor = new Color("d2bb8d", 0.78f);
            DrawRect(new Rect2(horizontalOrigin, new Vector2(width, thickness)), plateColor);
            DrawRect(new Rect2(verticalOrigin, new Vector2(thickness, width)), plateColor);

            var rivetOffsetA = new Vector2(
                flipX ? -10.0f : 10.0f,
                flipY ? -4.0f : 4.0f);
            var rivetOffsetB = new Vector2(
                flipX ? -4.0f : 4.0f,
                flipY ? -10.0f : 10.0f);
            DrawCircle(anchor + rivetOffsetA, 1.8f, rivetColor);
            DrawCircle(anchor + rivetOffsetB, 1.8f, rivetColor);
        }
    }

    private const float CityClickRadius = 22.0f;

    private MapPresentationLayer? _backdropLayer;
    private MapPresentationLayer? _overlayLayer;
    private Node2D? _worldRoot;
    private Node2D? _citiesLayer;
    private Node2D? _routesLayer;
    private Sprite2D? _backgroundSprite;

    private readonly List<(CityData City, CityNode Node)> _cityNodes = new();
    private LocalizationService? _localization;
    private WorldState? _world;
    private int _selectedCityId = -1;

    private bool _isDragging;
    private Vector2 _lastMousePosition;
    private Rect2 _backgroundBounds = new Rect2();
    private Vector2 _springVelocity = Vector2.Zero;

    public event Action<CityData>? CitySelected;

    public override void _Ready()
    {
        EnsurePresentationLayers();
        _worldRoot = GetNodeOrNull<Node2D>("WorldRoot");
        _citiesLayer = GetNodeOrNull<Node2D>("WorldRoot/CitiesLayer");
        _routesLayer = GetNodeOrNull<Node2D>("WorldRoot/RoutesLayer");
        _backgroundSprite = GetNodeOrNull<Sprite2D>("WorldRoot/BackgroundSprite");
        GetViewport().SizeChanged += OnViewportSizeChanged;

        TryUseUserMapTexture();
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
        if (_isDragging || _worldRoot == null)
        {
            return;
        }

        var currentPosition = _worldRoot.Position;
        var clampedPosition = GetClampedWorldRootPosition(currentPosition);
        var displacement = clampedPosition - currentPosition;
        if (displacement.LengthSquared() < 0.01f && _springVelocity.LengthSquared() < 0.01f)
        {
            _worldRoot.Position = clampedPosition;
            _springVelocity = Vector2.Zero;
            return;
        }

        var dt = (float)delta;
        _springVelocity = (_springVelocity + displacement * ReturnSpringStrength * dt) * Mathf.Pow(ReturnSpringDamping, dt * 60.0f);
        _worldRoot.Position += _springVelocity * dt;
    }

    public override void _ExitTree()
    {
        var viewport = GetViewport();
        if (viewport != null)
        {
            viewport.SizeChanged -= OnViewportSizeChanged;
        }

        if (_localization != null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }

    public void BindWorld(WorldState world, LocalizationService localization)
    {
        _world = world;
        _localization = localization;
        _localization.LanguageChanged -= OnLanguageChanged;
        _localization.LanguageChanged += OnLanguageChanged;

        if (_citiesLayer == null || _worldRoot == null)
        {
            return;
        }

        foreach (Node child in _citiesLayer.GetChildren())
        {
            child.QueueFree();
        }

        _cityNodes.Clear();

        var centerOffset = CalculateCenterOffset();
        _worldRoot.Position = centerOffset;
        ClampWorldRootPosition();

        foreach (var city in world.Cities)
        {
            var cityNode = new CityNode
            {
                Name = $"City_{city.Id}",
                Position = new Vector2(city.MapX, city.MapY)
            };
            cityNode.Bind(city, BuildCityLabel(city));
            _citiesLayer.AddChild(cityNode);
            _cityNodes.Add((city, cityNode));
        }

        if (_routesLayer != null)
        {
            foreach (Node child in _routesLayer.GetChildren())
            {
                child.QueueFree();
            }

            var routeRenderer = new RouteRenderer
            {
                Name = "RouteRenderer"
            };
            routeRenderer.Bind(world);
            _routesLayer.AddChild(routeRenderer);
        }

        var initialCityId = GetInitialSelectedCityId(world);
        if (initialCityId > 0)
        {
            SelectCity(initialCityId);
        }
    }

    public void RefreshVisuals()
    {
        foreach (var entry in _cityNodes)
        {
            entry.Node.SetDisplayLabel(BuildCityLabel(entry.City));
            entry.Node.QueueRedraw();
        }

        if (_selectedCityId > 0)
        {
            SelectCity(_selectedCityId);
        }
    }

    public void SelectCityById(int cityId)
    {
        if (_world == null)
        {
            return;
        }

        SelectCity(cityId);
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            if (mouseButton.Pressed)
            {
                _isDragging = true;
                _springVelocity = Vector2.Zero;
                _lastMousePosition = mouseButton.Position;
                GetViewport().SetInputAsHandled();
            }
            else
            {
                _isDragging = false;
            }

            return;
        }

        if (!mouseButton.Pressed || mouseButton.ButtonIndex != MouseButton.Left || _isDragging)
        {
            return;
        }

        var clickPos = mouseButton.Position;
        CityData? pickedCity = null;

        foreach (var entry in _cityNodes)
        {
            if (entry.Node.GlobalPosition.DistanceTo(clickPos) <= CityClickRadius)
            {
                pickedCity = entry.City;
                break;
            }
        }

        if (pickedCity != null)
        {
            SelectCity(pickedCity.Id);
            GetViewport().SetInputAsHandled();
        }
    }

    private void HandleMouseMotion(InputEventMouseMotion mouseMotion)
    {
        if (!_isDragging || _worldRoot == null)
        {
            return;
        }

        var delta = mouseMotion.Position - _lastMousePosition;
        _worldRoot.Position = ApplyElasticDrag(_worldRoot.Position, delta);
        _lastMousePosition = mouseMotion.Position;
        GetViewport().SetInputAsHandled();
    }

    private void SelectCity(int cityId)
    {
        _selectedCityId = cityId;
        CityData? selected = null;

        foreach (var entry in _cityNodes)
        {
            var isMatch = entry.City.Id == cityId;
            entry.Node.SetSelected(isMatch);
            if (isMatch)
            {
                selected = entry.City;
            }
        }

        if (selected != null)
        {
            CitySelected?.Invoke(selected);
        }
    }

    private static int GetInitialSelectedCityId(WorldState world)
    {
        var playerFactionId = -1;
        foreach (var faction in world.Factions)
        {
            if (faction.IsPlayer)
            {
                playerFactionId = faction.Id;
                break;
            }
        }

        if (playerFactionId > 0)
        {
            foreach (var city in world.Cities)
            {
                if (city.OwnerFactionId == playerFactionId && city.OfficerIds.Count > 0)
                {
                    return city.Id;
                }
            }

            foreach (var city in world.Cities)
            {
                if (city.OwnerFactionId == playerFactionId)
                {
                    return city.Id;
                }
            }
        }

        return world.Cities.Count > 0 ? world.Cities[0].Id : -1;
    }

    private void OnLanguageChanged()
    {
        foreach (var entry in _cityNodes)
        {
            entry.Node.SetDisplayLabel(BuildCityLabel(entry.City));
        }

        if (_selectedCityId > 0)
        {
            SelectCity(_selectedCityId);
        }
    }

    private string BuildCityLabel(CityData city)
    {
        if (_localization == null || _world == null)
        {
            return $"{city.Name}({city.Id})";
        }

        var cityName = _localization.GetCityName(city);
        var ownerName = _localization.GetFactionName(_world, city.OwnerFactionId);
        return $"{cityName}({city.Id})\n{ownerName}";
    }

    private Vector2 CalculateCenterOffset()
    {
        var bounds = GetBackgroundBounds();
        if (bounds.Size == Vector2.Zero)
        {
            return Vector2.Zero;
        }

        var mapViewport = GetMapViewportRect();
        if (mapViewport.Size == Vector2.Zero)
        {
            mapViewport = new Rect2(
                new Vector2(FrameOuterInset + FrameWoodInset + FrameParchmentInset, FrameOuterInset + FrameWoodInset + FrameParchmentInset),
                new Vector2(1600.0f, 900.0f) - new Vector2((FrameOuterInset + FrameWoodInset + FrameParchmentInset) * 2.0f, (FrameOuterInset + FrameWoodInset + FrameParchmentInset) * 2.0f));
        }

        return mapViewport.GetCenter() - bounds.GetCenter();
    }

    private void TryUseUserMapTexture()
    {
        if (_backgroundSprite == null)
        {
            return;
        }

        var preferredPaths = new[]
        {
            "res://assets/map/china_map.png",
            "res://assets/map/san4_generated_v2.png",
            "res://assets/map/san4_generated.png",
            "res://assets/map/san4_map.png"
        };

        foreach (var path in preferredPaths)
        {
            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            var texture = ResourceLoader.Load<Texture2D>(path);
            if (texture == null)
            {
                continue;
            }

            _backgroundSprite.Texture = texture;
            _backgroundSprite.Position = Vector2.Zero;
            _backgroundSprite.Scale = Vector2.One;
            UpdateBackgroundBounds();
            GD.Print($"Loaded map texture: {path}");
            return;
        }
    }

    private void EnsurePresentationLayers()
    {
        _backdropLayer = GetNodeOrNull<MapPresentationLayer>("BackdropLayer");
        if (_backdropLayer == null)
        {
            _backdropLayer = new MapPresentationLayer(GetMapViewportRect)
            {
                Name = "BackdropLayer",
                ZIndex = -100
            };
            AddChild(_backdropLayer);
            MoveChild(_backdropLayer, 0);
        }

        _overlayLayer = GetNodeOrNull<MapPresentationLayer>("OverlayLayer");
        if (_overlayLayer == null)
        {
            _overlayLayer = new MapPresentationLayer(GetMapViewportRect)
            {
                Name = "OverlayLayer",
                ZIndex = 100
            };
            AddChild(_overlayLayer);
        }

        _backdropLayer.IsOverlay = false;
        _overlayLayer.IsOverlay = true;
        _backdropLayer.QueueRedraw();
        _overlayLayer.QueueRedraw();
    }

    private void ClampWorldRootPosition()
    {
        if (_worldRoot == null)
        {
            return;
        }

        _worldRoot.Position = GetClampedWorldRootPosition(_worldRoot.Position);
    }

    private Vector2 GetClampedWorldRootPosition(Vector2 sourcePosition)
    {
        var mapViewport = GetMapViewportRect();
        if (mapViewport.Size == Vector2.Zero)
        {
            return sourcePosition;
        }

        var bounds = GetBackgroundBounds();
        if (bounds.Size == Vector2.Zero)
        {
            return sourcePosition;
        }

        return new Vector2(
            ClampAxisToViewport(sourcePosition.X, bounds.Position.X, bounds.End.X, mapViewport.Position.X, mapViewport.End.X),
            ClampAxisToViewport(sourcePosition.Y, bounds.Position.Y, bounds.End.Y, mapViewport.Position.Y, mapViewport.End.Y));
    }

    private Vector2 ApplyElasticDrag(Vector2 sourcePosition, Vector2 delta)
    {
        var clampedStart = GetClampedWorldRootPosition(sourcePosition);
        var rawTarget = sourcePosition + delta;
        var clampedTarget = GetClampedWorldRootPosition(rawTarget);
        var overscroll = rawTarget - clampedTarget;

        return new Vector2(
            ComputeElasticAxis(rawTarget.X, clampedTarget.X, overscroll.X, Mathf.IsEqualApprox(sourcePosition.X, clampedStart.X), delta.X),
            ComputeElasticAxis(rawTarget.Y, clampedTarget.Y, overscroll.Y, Mathf.IsEqualApprox(sourcePosition.Y, clampedStart.Y), delta.Y));
    }

    private static float ComputeElasticAxis(float rawTarget, float clampedTarget, float overscroll, bool startedInsideBounds, float delta)
    {
        if (Mathf.IsZeroApprox(overscroll))
        {
            return rawTarget;
        }

        if (startedInsideBounds)
        {
            return clampedTarget + (overscroll * DragOverscrollResistance);
        }

        return rawTarget - (delta * (1.0f - DragOverscrollResistance));
    }

    private Rect2 GetMapViewportRect()
    {
        var viewportSize = GetViewportRect().Size;
        if (viewportSize == Vector2.Zero)
        {
            return new Rect2();
        }

        var inset = FrameOuterInset + FrameWoodInset + FrameParchmentInset;
        var size = viewportSize - new Vector2(inset * 2.0f, inset * 2.0f);
        return new Rect2(new Vector2(inset, inset), size);
    }

    private static float ClampAxisToViewport(float currentRootPosition, float contentMin, float contentMax, float viewportMin, float viewportMax)
    {
        var alignToFarEdge = viewportMax - contentMax;
        var alignToNearEdge = viewportMin - contentMin;
        var minRootPosition = Mathf.Min(alignToFarEdge, alignToNearEdge);
        var maxRootPosition = Mathf.Max(alignToFarEdge, alignToNearEdge);
        return Mathf.Clamp(currentRootPosition, minRootPosition, maxRootPosition);
    }

    private Rect2 GetBackgroundBounds()
    {
        if (_backgroundBounds.Size != Vector2.Zero)
        {
            return _backgroundBounds;
        }

        UpdateBackgroundBounds();
        return _backgroundBounds;
    }

    private void UpdateBackgroundBounds()
    {
        if (_backgroundSprite?.Texture == null)
        {
            _backgroundBounds = new Rect2();
            return;
        }

        var size = _backgroundSprite.Texture.GetSize() * _backgroundSprite.Scale;
        var origin = _backgroundSprite.Position;
        if (_backgroundSprite.Centered)
        {
            origin -= size * 0.5f;
        }

        _backgroundBounds = new Rect2(origin, size);
    }

    private void OnViewportSizeChanged()
    {
        _backdropLayer?.QueueRedraw();
        _overlayLayer?.QueueRedraw();
        ClampWorldRootPosition();
    }
}

