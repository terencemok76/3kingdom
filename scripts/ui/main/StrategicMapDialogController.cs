using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Data;
using ThreeKingdom.Map;

namespace ThreeKingdom.UI;

internal sealed class StrategicMapSelectionRequest
{
    public string TitleKey { get; init; } = "ui.strategic_map.select_title";
    public string PromptKey { get; init; } = "ui.strategic_map.select_prompt";
    public StrategicMapLayer Layer { get; init; } = StrategicMapLayer.Faction;
    public StrategicMapFactionFilter FactionFilter { get; init; } = StrategicMapFactionFilter.All;
    public IReadOnlyCollection<int> SelectableCityIds { get; init; } = Array.Empty<int>();
    public int SourceCityId { get; init; }
    public int InitialCityId { get; init; }
    // Attack targeting needs a concise scouting readout; the normal map keeps layer-specific details.
    public bool UseAttackTooltip { get; init; }
    public Action<int> Confirmed { get; init; } = _ => { };
    public Action? Cancelled { get; init; }
}

internal sealed class StrategicMapDialogController : FloatingOverlayController
{
    private readonly MainHudUiContext _context;
    private OptionButton? _layerOption;
    private OptionButton? _factionFilterOption;
    private Label? _summaryLabel;
    private Control? _mapViewport;
    private StrategicMapCanvas? _mapCanvas;
    private Label? _selectedDetailLabel;
    private Button? _cancelSelectionButton;
    private Button? _confirmSelectionButton;
    private StrategicMapSelectionRequest? _selectionRequest;
    private int _pendingSelectedCityId;
    private bool _signalsConnected;

    protected override Vector2 MinimumOverlaySize => new(980.0f, 720.0f);

    public StrategicMapDialogController(MainHudUiContext context)
        : base(context, "res://scenes/ui/main/StrategicMapDialog.tscn")
    {
        _context = context;
    }

    public void Initialize() => InitializeOverlay();

    public void Show()
    {
        _selectionRequest = null;
        _pendingSelectedCityId = _context.SelectedCity?.Id ?? 0;
        RefreshContent();
        ShowOverlay();
    }

    public void ShowSelection(StrategicMapSelectionRequest request)
    {
        if (request.SelectableCityIds.Count == 0 || !EnsureOverlayReady())
        {
            return;
        }

        _selectionRequest = request;
        _pendingSelectedCityId = request.SelectableCityIds.Contains(request.InitialCityId)
            ? request.InitialCityId
            : request.SelectableCityIds.First();
        // The scene owns empty OptionButtons; populate them before selecting a request preset.
        ConfigureOptions();
        SelectOption(_layerOption, (int)request.Layer);
        SelectOption(_factionFilterOption, (int)request.FactionFilter);
        RefreshContent();
        ShowOverlay();
    }

    public void RefreshSelectedCity()
    {
        if (IsOverlayVisible)
        {
            RefreshContent();
        }
    }

    public void RefreshText()
    {
        if (IsOverlayVisible)
        {
            RefreshContent();
        }
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _layerOption = root.GetNodeOrNull<OptionButton>("FiltersRow/LayerOption");
        _factionFilterOption = root.GetNodeOrNull<OptionButton>("FiltersRow/FactionFilterOption");
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _mapViewport = root.GetNodeOrNull<Control>("SimpleMapFrame/MapViewport");
        _selectedDetailLabel = root.GetNodeOrNull<Label>("BottomRow/SelectedDetailLabel");
        _cancelSelectionButton = root.GetNodeOrNull<Button>("BottomRow/CancelSelectionButton");
        _confirmSelectionButton = root.GetNodeOrNull<Button>("BottomRow/ConfirmSelectionButton");
        if (_cancelSelectionButton != null) _context.ApplyCommandButtonTheme(_cancelSelectionButton);
        if (_confirmSelectionButton != null) _context.ApplyCommandButtonTheme(_confirmSelectionButton);
        if (_mapViewport != null && _mapCanvas == null)
        {
            _mapCanvas = new StrategicMapCanvas
            {
                Name = "StrategicMapCanvas",
                LayoutMode = 1,
                AnchorsPreset = (int)Control.LayoutPreset.FullRect,
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                GrowHorizontal = Control.GrowDirection.Both,
                GrowVertical = Control.GrowDirection.Both
            };
            _mapViewport.AddChild(_mapCanvas);
        }

        if (!_signalsConnected)
        {
            if (_layerOption != null) _layerOption.ItemSelected += _ => RefreshContent();
            if (_factionFilterOption != null) _factionFilterOption.ItemSelected += _ => RefreshContent();
            if (_cancelSelectionButton != null) _cancelSelectionButton.Pressed += OnCancelSelectionPressed;
            if (_confirmSelectionButton != null) _confirmSelectionButton.Pressed += OnConfirmSelectionPressed;
            _signalsConnected = true;
        }
    }

    protected override void OnOverlayCloseRequested()
    {
        _context.MapController?.SetStrategicMapPresentation(StrategicMapLayer.Faction, StrategicMapFactionFilter.All, false);
        if (_selectionRequest != null)
        {
            CancelSelection();
            return;
        }

        base.OnOverlayCloseRequested();
    }

    private void RefreshContent()
    {
        if (!EnsureOverlayReady())
        {
            return;
        }

        var world = _context.World;
        SetOverlayTitleText(T(_selectionRequest?.TitleKey ?? "ui.strategic_map.title"));
        ConfigureOptions();
        ConfigureSelectionActions();

        if (world == null)
        {
            if (_summaryLabel != null) _summaryLabel.Text = T("ui.strategic_map.no_data");
            return;
        }

        var layer = (StrategicMapLayer)(_layerOption?.Selected ?? 0);
        var factionFilter = (StrategicMapFactionFilter)(_factionFilterOption?.Selected ?? 0);
        _context.MapController?.SetStrategicMapPresentation(StrategicMapLayer.Faction, StrategicMapFactionFilter.All, false);

        if (_summaryLabel != null)
        {
            _summaryLabel.Text = BuildSummary(world, layer, factionFilter);
        }

        _mapCanvas?.Configure(
            world,
            _context.Localization,
            layer,
            factionFilter,
            _context.PlayerFactionId,
            GetActiveSelectedCity()?.Id ?? 0,
            _selectionRequest?.SelectableCityIds,
            _selectionRequest?.SourceCityId ?? 0,
            _selectionRequest?.UseAttackTooltip ?? false,
            cityId =>
            {
                if (_selectionRequest != null)
                {
                    _pendingSelectedCityId = cityId;
                }
                else
                {
                    _context.SelectCityById(cityId);
                }
                RefreshContent();
            });
        if (_selectedDetailLabel != null)
        {
            _selectedDetailLabel.Text = BuildSelectedDetail(world, layer);
        }
        UpdateOverlayLayoutNow();
    }

    private void ConfigureSelectionActions()
    {
        var selecting = _selectionRequest != null;
        if (_cancelSelectionButton != null)
        {
            _cancelSelectionButton.Visible = selecting;
            _cancelSelectionButton.Text = T("ui.strategic_map.cancel_selection");
        }

        if (_confirmSelectionButton != null)
        {
            _confirmSelectionButton.Visible = selecting;
            _confirmSelectionButton.Text = T("ui.strategic_map.confirm_selection");
            _confirmSelectionButton.Disabled = !selecting ||
                                               !_selectionRequest!.SelectableCityIds.Contains(_pendingSelectedCityId);
        }
    }

    private void ConfigureOptions()
    {
        PopulateOption(_layerOption, new[]
        {
            T("ui.strategic_map.layer.faction"),
            T("ui.strategic_map.layer.diplomacy"),
            T("ui.strategic_map.layer.military"),
            T("ui.strategic_map.layer.development")
        });
        PopulateOption(_factionFilterOption, new[]
        {
            T("ui.strategic_map.filter.all"),
            T("ui.strategic_map.filter.self"),
            T("ui.strategic_map.filter.friendly"),
            T("ui.strategic_map.filter.enemy"),
            T("ui.strategic_map.filter.neutral")
        });
    }

    private static void PopulateOption(OptionButton? option, string[] labels)
    {
        if (option == null)
        {
            return;
        }

        var selected = Mathf.Clamp(option.Selected, 0, labels.Length - 1);
        option.Clear();
        foreach (var label in labels) option.AddItem(label);
        option.Select(selected);
    }

    private static void SelectOption(OptionButton? option, int index)
    {
        if (option == null || option.ItemCount == 0)
        {
            return;
        }

        option.Select(Mathf.Clamp(index, 0, option.ItemCount - 1));
    }

    private string BuildSummary(WorldState world, StrategicMapLayer layer, StrategicMapFactionFilter factionFilter)
    {
        var emphasizedCount = world.Cities.Count(city =>
            MatchesFactionFilter(city, factionFilter) &&
            (_selectionRequest == null || _selectionRequest.SelectableCityIds.Contains(city.Id)));
        var selected = GetActiveSelectedCity();
        var layerText = layer switch
        {
            StrategicMapLayer.Diplomacy => T("ui.strategic_map.summary.diplomacy"),
            StrategicMapLayer.Military => T("ui.strategic_map.summary.military"),
            StrategicMapLayer.Development => T("ui.strategic_map.summary.development"),
            _ => T("ui.strategic_map.summary.faction")
        };
        if (_selectionRequest != null)
        {
            layerText = T(_selectionRequest.PromptKey);
        }
        var selectedText = selected == null
            ? T("ui.strategic_map.no_selection")
            : Format("fmt.strategic_map.selected_name", _context.Localization?.GetCityName(selected) ?? selected.Name);
        if (_selectionRequest?.SourceCityId > 0)
        {
            var sourceCity = world.GetCity(_selectionRequest.SourceCityId);
            var sourceName = sourceCity == null
                ? T("ui.strategic_map.no_selection")
                : _context.Localization?.GetCityName(sourceCity) ?? sourceCity.Name;
            return Format(
                "fmt.strategic_map.selection_summary",
                layerText,
                T("ui.strategic_map.source"),
                sourceName,
                T("ui.strategic_map.target"),
                selectedText);
        }
        if (_selectionRequest != null)
        {
            return Format("fmt.strategic_map.selection_only_summary", layerText, selectedText);
        }
        return Format("fmt.strategic_map.summary", layerText, selectedText, emphasizedCount, world.Cities.Count);
    }

    private bool MatchesFactionFilter(CityData city, StrategicMapFactionFilter filter)
    {
        var playerFactionId = _context.PlayerFactionId;
        return filter switch
        {
            StrategicMapFactionFilter.Self => city.OwnerFactionId == playerFactionId,
            StrategicMapFactionFilter.Friendly => IsFriendlyFaction(city.OwnerFactionId, playerFactionId),
            StrategicMapFactionFilter.Enemy => city.OwnerFactionId > 0 && city.OwnerFactionId != playerFactionId,
            StrategicMapFactionFilter.Neutral => city.OwnerFactionId <= 0,
            _ => true
        };
    }

    private bool IsFriendlyFaction(int factionId, int playerFactionId)
    {
        var world = _context.World;
        if (world == null || factionId <= 0 || factionId == playerFactionId)
        {
            return false;
        }

        var relation = world.GetDiplomacyRelation(playerFactionId, factionId);
        return relation?.Status == DiplomacyStatusType.Alliance ||
               (relation?.Status != DiplomacyStatusType.Truce && (relation?.RelationScore ?? 0) > 0);
    }

    private string BuildSelectedDetail(WorldState world, StrategicMapLayer layer)
    {
        var city = GetActiveSelectedCity();
        if (city == null)
        {
            return T("ui.strategic_map.select_city_prompt");
        }

        var factionName = _context.Localization?.GetFactionName(world, city.OwnerFactionId) ?? "-";
        var detail = layer switch
        {
            StrategicMapLayer.Diplomacy => Format("fmt.strategic_map.detail.diplomacy", factionName, GetDiplomacyLabel(world, city)),
            StrategicMapLayer.Military when CanViewMilitaryIntel(world, city) => Format("fmt.strategic_map.detail.military", city.Troops, city.Defense),
            StrategicMapLayer.Military => T("ui.strategic_map.detail.military_unknown"),
            StrategicMapLayer.Development => Format("fmt.strategic_map.detail.development", city.Farm, city.Commercial, city.Population),
            _ => Format("fmt.strategic_map.detail.faction", factionName)
        };
        var name = _context.Localization?.GetCityName(city) ?? city.Name;
        return Format("fmt.strategic_map.selected_detail", name, detail, city.ConnectedCityIds.Count);
    }

    private bool CanViewMilitaryIntel(WorldState world, CityData city) =>
        world.ViewAllInformationEnabled || world.CanFactionViewCity(_context.PlayerFactionId, city.Id);

    private string GetDiplomacyLabel(WorldState world, CityData city)
    {
        if (city.OwnerFactionId <= 0)
        {
            return T("ui.strategic_map.relation.unclaimed");
        }

        if (city.OwnerFactionId == _context.PlayerFactionId)
        {
            return T("ui.strategic_map.relation.self");
        }

        var relation = world.GetDiplomacyRelation(_context.PlayerFactionId, city.OwnerFactionId);
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

    private string T(string key) => _context.Localization?.T(key) ?? key;

    private string Format(string key, params object[] args) => _context.Localization?.Format(key, args) ?? key;

    private CityData? GetActiveSelectedCity() => _context.World?.GetCity(
        _selectionRequest == null ? _context.SelectedCity?.Id ?? 0 : _pendingSelectedCityId);

    private void OnConfirmSelectionPressed()
    {
        var request = _selectionRequest;
        if (request == null || !request.SelectableCityIds.Contains(_pendingSelectedCityId))
        {
            return;
        }

        var selectedCityId = _pendingSelectedCityId;
        _selectionRequest = null;
        HideOverlay();
        request.Confirmed(selectedCityId);
    }

    private void OnCancelSelectionPressed() => CancelSelection();

    private void CancelSelection()
    {
        var request = _selectionRequest;
        _selectionRequest = null;
        HideOverlay();
        request?.Cancelled?.Invoke();
    }
}
