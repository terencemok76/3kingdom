using System;
using System.Collections.Generic;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

public partial class MapController : Node2D
{
    private const float CityClickRadius = 22.0f;

    private Node2D? _citiesLayer;
    private Node2D? _routesLayer;

    private readonly List<(CityData City, CityNode Node)> _cityNodes = new();
    private LocalizationService? _localization;
    private int _selectedCityId = -1;

    public event Action<CityData>? CitySelected;

    public override void _Ready()
    {
        _citiesLayer = GetNodeOrNull<Node2D>("CitiesLayer");
        _routesLayer = GetNodeOrNull<Node2D>("RoutesLayer");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton)
        {
            return;
        }

        if (!mouseButton.Pressed || mouseButton.ButtonIndex != MouseButton.Left)
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
        _localization = localization;
        _localization.LanguageChanged -= OnLanguageChanged;
        _localization.LanguageChanged += OnLanguageChanged;

        if (_citiesLayer == null)
        {
            return;
        }

        foreach (Node child in _citiesLayer.GetChildren())
        {
            child.QueueFree();
        }

        _cityNodes.Clear();

        var offset = CalculateCenterOffset(world);

        foreach (var city in world.Cities)
        {
            var cityNode = new CityNode
            {
                Name = $"City_{city.Id}",
                Position = new Vector2(city.MapX, city.MapY) + offset
            };
            cityNode.Bind(city, localization.GetCityName(city));
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
            routeRenderer.Bind(world, offset);
            _routesLayer.AddChild(routeRenderer);
        }

        if (world.Cities.Count > 0)
        {
            SelectCity(world.Cities[0].Id);
        }
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

    private void OnLanguageChanged()
    {
        if (_localization == null)
        {
            return;
        }

        foreach (var entry in _cityNodes)
        {
            entry.Node.SetDisplayName(_localization.GetCityName(entry.City));
        }

        if (_selectedCityId > 0)
        {
            SelectCity(_selectedCityId);
        }
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
}
