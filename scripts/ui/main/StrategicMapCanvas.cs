using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;

namespace ThreeKingdom.UI;

internal partial class StrategicMapCanvas : Control
{
    private enum RouteVisualStyle
    {
        Normal,
        Selected,
        SourceToTarget
    }

    private const float HorizontalPadding = 42.0f;
    private const float VerticalPadding = 34.0f;
    private const float LayoutNodeWidth = 66.0f;
    private const float LayoutNodeHeight = 32.0f;
    private const float MinimumNodeGap = 14.0f;
    private const int OverlapRelaxationPasses = 64;
    private static readonly string[] FallbackFactionColors =
    {
        "3f7f4c", "8a3e2f", "2f5f8a", "b9932f", "7b4fa3", "2f8a83",
        "a35f2f", "6a3a8d", "3d6f9f", "9a4f66", "5a8c3d", "a8842c"
    };

    private readonly Dictionary<int, Vector2> _cityCenters = new();
    private readonly List<(Vector2 Start, Vector2 End, RouteVisualStyle Style)> _routes = new();
    private WorldState? _world;
    private LocalizationService? _localization;
    private StrategicMapLayer _layer;
    private StrategicMapFactionFilter _filter;
    private int _playerFactionId;
    private int _selectedCityId;
    private HashSet<int>? _selectableCityIds;
    private int _sourceCityId;
    private bool _useAttackTooltip;
    private Action<int>? _citySelected;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        Resized += Rebuild;
    }

    public void Configure(
        WorldState world,
        LocalizationService? localization,
        StrategicMapLayer layer,
        StrategicMapFactionFilter filter,
        int playerFactionId,
        int selectedCityId,
        IReadOnlyCollection<int>? selectableCityIds,
        int sourceCityId,
        bool useAttackTooltip,
        Action<int> citySelected)
    {
        _world = world;
        _localization = localization;
        _layer = layer;
        _filter = filter;
        _playerFactionId = playerFactionId;
        _selectedCityId = selectedCityId;
        _selectableCityIds = selectableCityIds == null ? null : new HashSet<int>(selectableCityIds);
        _sourceCityId = sourceCityId;
        _useAttackTooltip = useAttackTooltip;
        _citySelected = citySelected;
        Rebuild();
    }

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        DrawRect(bounds, new Color("27251f"));
        DrawRect(new Rect2(new Vector2(3, 3), Size - new Vector2(6, 6)), new Color("79705c"), false, 2.0f);

        var inner = new Rect2(new Vector2(12, 12), Size - new Vector2(24, 24));
        DrawRect(inner, new Color("393c32"));
        for (var index = 1; index < 6; index++)
        {
            var y = inner.Position.Y + inner.Size.Y * index / 6.0f;
            DrawLine(new Vector2(inner.Position.X, y), new Vector2(inner.End.X, y), new Color(0.7f, 0.68f, 0.57f, 0.06f), 1.0f);
        }

        foreach (var route in _routes)
        {
            var isSourceToTarget = route.Style == RouteVisualStyle.SourceToTarget;
            var isSelected = route.Style == RouteVisualStyle.Selected;
            DrawLine(
                route.Start,
                route.End,
                isSourceToTarget || isSelected ? new Color("e6bf66") : new Color(0.72f, 0.7f, 0.61f, 0.48f),
                isSourceToTarget ? 3.5f : isSelected ? 3.0f : 1.5f,
                true);
        }
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _cityCenters.Clear();
        _routes.Clear();
        if (_world == null || Size.X < 100.0f || Size.Y < 100.0f)
        {
            QueueRedraw();
            return;
        }

        var cities = _world.Cities.ToList();
        if (cities.Count == 0)
        {
            QueueRedraw();
            return;
        }

        var minX = _world.Cities.Min(city => city.MapX);
        var maxX = _world.Cities.Max(city => city.MapX);
        var minY = _world.Cities.Min(city => city.MapY);
        var maxY = _world.Cities.Max(city => city.MapY);
        var spanX = Mathf.Max(1.0f, maxX - minX);
        var spanY = Mathf.Max(1.0f, maxY - minY);
        var usableWidth = Mathf.Max(1.0f, Size.X - HorizontalPadding * 2.0f);
        var usableHeight = Mathf.Max(1.0f, Size.Y - VerticalPadding * 2.0f);

        foreach (var city in cities)
        {
            _cityCenters[city.Id] = new Vector2(
                HorizontalPadding + (city.MapX - minX) / spanX * usableWidth,
                VerticalPadding + (city.MapY - minY) / spanY * usableHeight);
        }

        RelaxOverlaps(cities);
        BuildRoutes(cities);
        foreach (var city in cities)
        {
            AddCityButton(city);
        }

        QueueRedraw();
    }

    private void RelaxOverlaps(IReadOnlyList<CityData> cities)
    {
        var requiredCenterDistanceX = LayoutNodeWidth + MinimumNodeGap;
        var requiredCenterDistanceY = LayoutNodeHeight + MinimumNodeGap;
        for (var pass = 0; pass < OverlapRelaxationPasses; pass++)
        {
            var adjusted = false;
            for (var leftIndex = 0; leftIndex < cities.Count; leftIndex++)
            {
                for (var rightIndex = leftIndex + 1; rightIndex < cities.Count; rightIndex++)
                {
                    var leftId = cities[leftIndex].Id;
                    var rightId = cities[rightIndex].Id;
                    var delta = _cityCenters[rightId] - _cityCenters[leftId];
                    var overlapX = requiredCenterDistanceX - Mathf.Abs(delta.X);
                    var overlapY = requiredCenterDistanceY - Mathf.Abs(delta.Y);
                    if (overlapX <= 0.0f || overlapY <= 0.0f)
                    {
                        continue;
                    }

                    adjusted = true;
                    if (overlapX < overlapY)
                    {
                        var direction = Mathf.IsZeroApprox(delta.X) ? (leftIndex % 2 == 0 ? 1.0f : -1.0f) : Mathf.Sign(delta.X);
                        var push = direction * (overlapX * 0.5f + 0.5f);
                        _cityCenters[leftId] = ClampCenter(_cityCenters[leftId] - new Vector2(push, 0.0f));
                        _cityCenters[rightId] = ClampCenter(_cityCenters[rightId] + new Vector2(push, 0.0f));
                    }
                    else
                    {
                        var direction = Mathf.IsZeroApprox(delta.Y) ? (leftIndex % 2 == 0 ? 1.0f : -1.0f) : Mathf.Sign(delta.Y);
                        var push = direction * (overlapY * 0.5f + 0.5f);
                        _cityCenters[leftId] = ClampCenter(_cityCenters[leftId] - new Vector2(0.0f, push));
                        _cityCenters[rightId] = ClampCenter(_cityCenters[rightId] + new Vector2(0.0f, push));
                    }
                }
            }

            if (!adjusted)
            {
                break;
            }
        }
    }

    private Vector2 ClampCenter(Vector2 center) => new(
        Mathf.Clamp(center.X, HorizontalPadding, Mathf.Max(HorizontalPadding, Size.X - HorizontalPadding)),
        Mathf.Clamp(center.Y, VerticalPadding, Mathf.Max(VerticalPadding, Size.Y - VerticalPadding)));

    private void BuildRoutes(IReadOnlyList<CityData> cities)
    {
        var visibleIds = cities.Select(city => city.Id).ToHashSet();
        foreach (var city in cities)
        {
            foreach (var connectedId in city.ConnectedCityIds)
            {
                if (connectedId <= city.Id || !visibleIds.Contains(connectedId))
                {
                    continue;
                }

                var isSourceSelection = _sourceCityId > 0 && _selectableCityIds != null;
                var connectsSourceToSelectedTarget = isSourceSelection && _selectedCityId > 0 &&
                    ((city.Id == _sourceCityId && connectedId == _selectedCityId) ||
                     (connectedId == _sourceCityId && city.Id == _selectedCityId));
                var style = connectsSourceToSelectedTarget
                    ? RouteVisualStyle.SourceToTarget
                    : !isSourceSelection && (city.Id == _selectedCityId || connectedId == _selectedCityId)
                        ? RouteVisualStyle.Selected
                        : RouteVisualStyle.Normal;
                _routes.Add((_cityCenters[city.Id], _cityCenters[connectedId], style));
            }
        }
    }

    private void AddCityButton(CityData city)
    {
        var selected = city.Id == _selectedCityId;
        var isSource = city.Id == _sourceCityId;
        var selectable = _selectableCityIds == null || _selectableCityIds.Contains(city.Id);
        var buttonSize = selected ? new Vector2(66, 32) : new Vector2(58, 28);
        var button = new Button
        {
            Text = GetCityName(city),
            Position = _cityCenters[city.Id] - buttonSize * 0.5f,
            Size = buttonSize,
            CustomMinimumSize = buttonSize,
            FocusMode = FocusModeEnum.None,
            Disabled = !selectable,
            MouseDefaultCursorShape = selectable ? CursorShape.PointingHand : CursorShape.Arrow,
            TooltipText = BuildTooltip(city)
        };

        button.AddThemeFontSizeOverride("font_size", selected ? 13 : 12);
        var emphasized = selectable && MatchesFilter(city);
        var fillColor = isSource ? new Color("2563eb") : emphasized ? GetLayerColor(city) : new Color("5b5c58");
        var usesDiplomacyColor = emphasized && _layer == StrategicMapLayer.Diplomacy;
        var borderColor = isSource
            ? new Color("93c5fd")
            : usesDiplomacyColor
            ? fillColor.Lightened(0.28f)
            : emphasized ? GetFactionColor(city) : new Color("92938d");
        ApplyCityTheme(button, fillColor, borderColor, selected, emphasized || isSource, usesDiplomacyColor || isSource, isSource);
        button.Pressed += () => _citySelected?.Invoke(city.Id);
        AddChild(button);
    }

    private void ApplyCityTheme(
        Button button,
        Color fill,
        Color borderColor,
        bool selected,
        bool emphasized,
        bool useFullFillColor,
        bool isSource)
    {
        var normalBackground = useFullFillColor ? fill : fill.Darkened(0.2f);
        var normal = CreateButtonStyle(normalBackground, selected ? new Color("f2d27c") : borderColor, selected ? 3 : 2);
        var hover = CreateButtonStyle(fill.Lightened(useFullFillColor ? 0.2f : 0.12f), new Color("f2d27c"), 3);
        var pressed = CreateButtonStyle(fill.Darkened(0.05f), Colors.White, 3);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("focus", hover);
        button.AddThemeStyleboxOverride(
            "disabled",
            isSource
                ? CreateButtonStyle(new Color("1d4ed8"), new Color("93c5fd"), 3)
                : CreateButtonStyle(new Color("494a47"), new Color("747570"), 1));
        button.AddThemeColorOverride("font_color", emphasized ? Colors.White : new Color("d0d0ca"));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", isSource ? Colors.White : new Color("a2a39e"));
    }

    private static StyleBoxFlat CreateButtonStyle(Color background, Color border, int borderWidth) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = borderWidth,
        BorderWidthTop = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthBottom = borderWidth,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,
        ContentMarginLeft = 3.0f,
        ContentMarginRight = 3.0f
    };

    private Color GetLayerColor(CityData city)
    {
        if (_layer == StrategicMapLayer.Faction)
        {
            return GetFactionColor(city);
        }

        if (_layer == StrategicMapLayer.Diplomacy)
        {
            return GetDiplomacyColor(city);
        }

        if (_layer == StrategicMapLayer.Military)
        {
            if (!CanViewMilitaryIntel(city))
            {
                return new Color("5b5c58");
            }

            var strength = Mathf.Clamp((city.Troops / 10000.0f + city.Defense / 100.0f) * 0.5f, 0.0f, 1.0f);
            return new Color(0.32f + strength * 0.42f, 0.18f + strength * 0.12f, 0.13f, 1.0f);
        }

        var development = Mathf.Clamp((city.Farm + city.Commercial) / 200.0f, 0.0f, 1.0f);
        return new Color(0.18f + development * 0.22f, 0.3f + development * 0.36f, 0.16f, 1.0f);
    }

    private Color GetFactionColor(CityData city)
    {
        if (city.OwnerFactionId <= 0)
        {
            return new Color("777777");
        }

        var faction = _world?.Factions.FirstOrDefault(item => item.Id == city.OwnerFactionId);
        if (faction != null && !string.IsNullOrWhiteSpace(faction.MapColorHex))
        {
            return new Color(faction.MapColorHex);
        }

        return new Color(FallbackFactionColors[Mathf.PosMod(city.OwnerFactionId - 1, FallbackFactionColors.Length)]);
    }

    private Color GetDiplomacyColor(CityData city)
    {
        if (city.OwnerFactionId <= 0)
        {
            return new Color("3f3f46");
        }

        if (city.OwnerFactionId == _playerFactionId)
        {
            return new Color("2563eb");
        }

        var relation = _world?.GetDiplomacyRelation(_playerFactionId, city.OwnerFactionId);
        return relation?.Status switch
        {
            DiplomacyStatusType.Alliance => new Color("16a34a"),
            DiplomacyStatusType.Truce => new Color("ea580c"),
            _ when (relation?.RelationScore ?? 0) < 0 => new Color("dc2626"),
            _ when (relation?.RelationScore ?? 0) > 0 => new Color("0891b2"),
            _ => new Color("64748b")
        };
    }

    private string BuildTooltip(CityData city)
    {
        if (_useAttackTooltip)
        {
            return CanViewMilitaryIntel(city)
                ? Format("fmt.strategic_map.tooltip.known_troops", GetCityName(city), GetRulerName(city), city.Troops)
                : Format("fmt.strategic_map.tooltip.unknown_troops", GetCityName(city), GetRulerName(city));
        }

        var factionName = _world == null
            ? "-"
            : _localization?.GetFactionName(_world, city.OwnerFactionId) ?? "-";
        var header = Format("fmt.strategic_map.tooltip.header", GetCityName(city), factionName);
        return _layer switch
        {
            StrategicMapLayer.Military when CanViewMilitaryIntel(city) =>
                Format("fmt.strategic_map.tooltip.military", header, city.Troops, city.Defense),
            StrategicMapLayer.Military => Format("fmt.strategic_map.tooltip.military_unknown", header),
            StrategicMapLayer.Diplomacy =>
                Format("fmt.strategic_map.tooltip.diplomacy", header, GetDiplomacyLabel(city)),
            StrategicMapLayer.Development =>
                Format("fmt.strategic_map.tooltip.development", header, city.Farm, city.Commercial),
            _ => header
        };
    }

    private bool CanViewMilitaryIntel(CityData city) =>
        _world != null &&
        (_world.ViewAllInformationEnabled || _world.CanFactionViewCity(_playerFactionId, city.Id));

    private string GetRulerName(CityData city)
    {
        if (_world == null || city.OwnerFactionId <= 0)
        {
            return T("ui.strategic_map.relation.unclaimed");
        }

        var faction = _world.GetFaction(city.OwnerFactionId);
        var ruler = faction == null ? null : _world.GetOfficer(faction.RulerOfficerId);
        return ruler == null
            ? T("ui.unknown")
            : _localization?.GetOfficerName(ruler) ?? ruler.Name;
    }

    private string GetDiplomacyLabel(CityData city)
    {
        if (city.OwnerFactionId <= 0)
        {
            return T("ui.strategic_map.relation.unclaimed");
        }

        if (city.OwnerFactionId == _playerFactionId)
        {
            return T("ui.strategic_map.relation.self");
        }

        var relation = _world?.GetDiplomacyRelation(_playerFactionId, city.OwnerFactionId);
        var score = relation?.RelationScore ?? 0;
        var relationText = relation?.Status switch
        {
            DiplomacyStatusType.Alliance => T("ui.strategic_map.relation.alliance"),
            DiplomacyStatusType.Truce => T("ui.strategic_map.relation.truce"),
            _ when score < 0 => T("ui.strategic_map.relation.hostile"),
            _ when score > 0 => T("ui.strategic_map.relation.friendly"),
            _ => T("ui.strategic_map.relation.neutral")
        };
        return Format("fmt.strategic_map.relation_score", relationText, score);
    }

    private string GetCityName(CityData city) => _localization?.GetCityName(city) ?? city.Name;

    private bool MatchesFilter(CityData city) => _filter switch
    {
        StrategicMapFactionFilter.Self => city.OwnerFactionId == _playerFactionId,
        StrategicMapFactionFilter.Friendly => IsFriendlyFaction(city.OwnerFactionId),
        StrategicMapFactionFilter.Enemy => city.OwnerFactionId > 0 && city.OwnerFactionId != _playerFactionId,
        StrategicMapFactionFilter.Neutral => city.OwnerFactionId <= 0,
        _ => true
    };

    private bool IsFriendlyFaction(int factionId)
    {
        if (_world == null || factionId <= 0 || factionId == _playerFactionId)
        {
            return false;
        }

        var relation = _world.GetDiplomacyRelation(_playerFactionId, factionId);
        return relation?.Status == DiplomacyStatusType.Alliance ||
               (relation?.Status != DiplomacyStatusType.Truce && (relation?.RelationScore ?? 0) > 0);
    }

    private string T(string key) => _localization?.T(key) ?? key;

    private string Format(string key, params object[] args) => _localization?.Format(key, args) ?? key;
}
