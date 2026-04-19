using System;
using System.Collections.Generic;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

public partial class MapController : Node2D
{
    private const float CityClickRadius = 22.0f;

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

    public event Action<CityData>? CitySelected;

    public override void _Ready()
    {
        _worldRoot = GetNodeOrNull<Node2D>("WorldRoot");
        _citiesLayer = GetNodeOrNull<Node2D>("WorldRoot/CitiesLayer");
        _routesLayer = GetNodeOrNull<Node2D>("WorldRoot/RoutesLayer");
        _backgroundSprite = GetNodeOrNull<Sprite2D>("WorldRoot/BackgroundSprite");

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

    public override void _ExitTree()
    {
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

        var centerOffset = CalculateCenterOffset(world);
        _worldRoot.Position = centerOffset;

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

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            if (mouseButton.Pressed)
            {
                _isDragging = true;
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
        _worldRoot.Position += delta;
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

    private Vector2 CalculateCenterOffset(WorldState world)
    {
        if (world.Cities.Count == 0)
        {
            return Vector2.Zero;
        }

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (var city in world.Cities)
        {
            if (city.MapX < minX)
            {
                minX = city.MapX;
            }

            if (city.MapY < minY)
            {
                minY = city.MapY;
            }

            if (city.MapX > maxX)
            {
                maxX = city.MapX;
            }

            if (city.MapY > maxY)
            {
                maxY = city.MapY;
            }
        }

        var mapCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        var viewportSize = GetViewportRect().Size;
        if (viewportSize == Vector2.Zero)
        {
            viewportSize = new Vector2(1600.0f, 900.0f);
        }

        var screenCenter = viewportSize * 0.5f;
        return screenCenter - mapCenter;
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
            GD.Print($"Loaded map texture: {path}");
            return;
        }
    }
}

