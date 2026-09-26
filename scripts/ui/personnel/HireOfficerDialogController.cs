using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class HireOfficerDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Button? _clearOfficerButton;
    private Label? _selectedEnvoyLabel;
    private Button? _selectEnvoyButton;
    private SpinBox? _goldSpinBox;
    private SpinBox? _foodSpinBox;
    private OptionButton? _itemOption;
    private Label? _summaryLabel;
    private Label? _warningLabel;
    private Button? _advisorButton;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private int _selectedEnvoyOfficerId = -1;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(460.0f, 320.0f);

    public HireOfficerDialogController(PersonnelUiContext context)
        : base(context, "res://scenes/ui/personnel/HireOfficerDialog.tscn")
    {
        _context = context;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void Show()
    {
        if (_context.SelectedCity == null || _context.TurnManager?.World == null || _context.Localization == null)
        {
            return;
        }

        RefreshText();
        Populate();
        ShowOverlay();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("command.personnel.hire_officer"));
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.hire_officer_target"));
        SetLabelText("EnvoyLabel", _context.Localization.T("ui.hire_officer_envoy"));
        SetLabelText("GoldLabel", _context.Localization.T("ui.hire_officer_gold_offer"));
        SetLabelText("FoodLabel", _context.Localization.T("ui.hire_officer_food_offer"));
        SetLabelText("ItemLabel", _context.Localization.T("ui.hire_officer_item_offer"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_hire_officer");
        }
        if (_clearOfficerButton != null) _clearOfficerButton.Text = _context.Localization.T("ui.clear_selection");
        if (_selectEnvoyButton != null) _selectEnvoyButton.Text = _context.Localization.T("ui.select_officer");
        if (_advisorButton != null) _advisorButton.Text = _context.Localization.T("ui.personnel_advice_hire");
        UpdateSelectedOfficerSummary();
        UpdateSelectedEnvoySummary();
        RefreshItemOptionTexts();
        UpdateSummary();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _clearOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/ClearOfficerButton");
        _selectedEnvoyLabel = root.GetNodeOrNull<Label>("EnvoySelectorRow/SelectedEnvoyLabel");
        _selectEnvoyButton = root.GetNodeOrNull<Button>("EnvoySelectorRow/SelectEnvoyButton");
        _goldSpinBox = root.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
        _foodSpinBox = root.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
        _itemOption = root.GetNodeOrNull<OptionButton>("ItemRow/ItemOption");
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _warningLabel = root.GetNodeOrNull<Label>("WarningLabel");
        _advisorButton = root.GetNodeOrNull<Button>("ConfirmRow/AdvisorButton");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_selectOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_selectOfficerButton);
        }
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }
        if (_clearOfficerButton != null) _context.ApplyCommandButtonTheme(_clearOfficerButton);
        if (_advisorButton != null) _context.ApplyCommandButtonTheme(_advisorButton);
        if (_selectEnvoyButton != null) _context.ApplyCommandButtonTheme(_selectEnvoyButton);
        if (_signalsConnected)
        {
            return;
        }

        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Pressed += OnSelectOfficerPressed;
        }
        if (_clearOfficerButton != null) _clearOfficerButton.Pressed += OnClearOfficerPressed;
        if (_selectEnvoyButton != null) _selectEnvoyButton.Pressed += OnSelectEnvoyPressed;
        if (_goldSpinBox != null)
        {
            _goldSpinBox.ValueChanged += _ => UpdateSummary();
            _goldSpinBox.GetLineEdit().TextChanged += _ => UpdateSummary();
        }
        if (_foodSpinBox != null)
        {
            _foodSpinBox.ValueChanged += _ => UpdateSummary();
            _foodSpinBox.GetLineEdit().TextChanged += _ => UpdateSummary();
        }
        if (_itemOption != null)
        {
            _itemOption.ItemSelected += _ => UpdateSummary();
        }
        if (_confirmButton != null)
        {
            _confirmButton.Pressed += OnConfirmPressed;
        }
        if (_advisorButton != null) _advisorButton.Pressed += OnAdvisorPressed;
        _signalsConnected = true;
    }

    private void Populate()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        if (city == null || world == null)
        {
            return;
        }

        var playerFactionId = _context.TurnManager!.GetPlayerFactionId();
        var candidates = GetOrderedCandidates(world, playerFactionId);

        _context.ConfigureMoveSpinBox(_goldSpinBox, Math.Max(0, city.Gold - HudController.HireOfficerGoldCost), 0);
        _context.ConfigureMoveSpinBox(_foodSpinBox, city.Food, 0);
        if (_goldSpinBox != null)
        {
            _goldSpinBox.Step = 100;
        }
        if (_foodSpinBox != null)
        {
            _foodSpinBox.Step = 500;
        }

        _context.PopulateFactionInventoryOption(_itemOption);
        if (!candidates.Any(officer => officer.Id == _selectedOfficerId)) _selectedOfficerId = -1;
        if (!city.OfficerIds.Contains(_selectedEnvoyOfficerId)) _selectedEnvoyOfficerId = -1;

        UpdateSelectedOfficerSummary();
        UpdateSelectedEnvoySummary();
        UpdateSummary();
        if (_confirmButton != null)
        {
            _confirmButton.Disabled = candidates.Count == 0;
        }
    }

    private void OnClearOfficerPressed()
    {
        _selectedOfficerId = -1;
        if (_warningLabel != null) _warningLabel.Text = string.Empty;
        UpdateSelectedOfficerSummary();
        UpdateSummary();
    }

    private void OnAdvisorPressed()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (city == null || world == null || localization == null) return;

        var playerFactionId = _context.TurnManager!.GetPlayerFactionId();
        var candidates = GetOrderedCandidates(world, playerFactionId);
        var selected = candidates.FirstOrDefault(candidate => candidate.Id == _selectedOfficerId);
        var recommended = candidates.OrderByDescending(GetHireFit).FirstOrDefault();
        var goldOffer = GetEnteredWholeNumber(_goldSpinBox);
        var foodOffer = GetEnteredWholeNumber(_foodSpinBox);
        var giftedItem = _context.GetSelectedItemFromOption(_itemOption);
        var advisor = _context.FindPersonnelAdvisor();
        var role = advisor?.Id == world.GetFaction(city.OwnerFactionId)?.ChancellorOfficerId
            ? localization.T("ui.chancellor")
            : localization.T("ui.local_place");
        var message = recommended == null
            ? localization.T("ui.personnel_advice_hire_no_candidate")
            : selected == null
                ? localization.Format(
                    "fmt.personnel_advice_hire_recommend",
                    localization.GetOfficerName(recommended),
                    GetHireFit(recommended),
                    BuildHireOfficerStats(recommended, world, localization),
                    BuildHireAcceptanceAdvice(world, city, recommended, playerFactionId, goldOffer, foodOffer, giftedItem, localization))
                : localization.Format(
                    "fmt.personnel_advice_hire_compare",
                    localization.GetOfficerName(selected),
                    GetHireFit(selected),
                    BuildHireOfficerStats(selected, world, localization),
                    HudController.HireOfficerGoldCost,
                    goldOffer,
                    foodOffer,
                    giftedItem != null ? localization.GetItemName(giftedItem) : localization.T("ui.no_item"),
                    selected.Id == recommended.Id
                        ? localization.T("ui.personnel_advice_good_choice")
                        : localization.Format("ui.personnel_advice_hire_better_candidate", localization.GetOfficerName(recommended), GetHireFit(recommended)),
                    BuildHireAcceptanceAdvice(world, city, selected, playerFactionId, goldOffer, foodOffer, giftedItem, localization));
        _context.ShowAdvisorMessage(advisor, role, message);
    }

    private static int GetHireFit(OfficerData officer) =>
        officer.Leadership + officer.Strength + officer.Intelligence + officer.Politics + officer.Charm * 2 + officer.Combat;

    private static string BuildHireAcceptanceAdvice(WorldState world, CityData city, OfficerData officer, int playerFactionId, int goldOffer, int foodOffer, ItemData? giftedItem, LocalizationService localization)
    {
        var score = CommandResolver.GetHireAcceptanceScore(world, city, officer, playerFactionId, goldOffer, foodOffer, giftedItem);
        var threshold = CommandResolver.GetHireAcceptanceThreshold(world, officer);
        var result = score >= threshold
            ? localization.Format("fmt.personnel_advice_hire_accept", score, threshold, score - threshold)
            : localization.Format("fmt.personnel_advice_hire_refuse", score, threshold, threshold - score);
        var relationshipType = CommandResolver.GetHireRelationshipType(world, officer, playerFactionId);
        var relationshipName = relationshipType switch
        {
            "family,blood" => localization.T("ui.personnel_advice_hire_relation_blood"),
            "family,non-blood" => localization.T("ui.personnel_advice_hire_relation_non_blood"),
            "family" => localization.T("ui.personnel_advice_hire_relation_family"),
            _ => localization.T("ui.personnel_advice_hire_relation_none")
        };
        var relationshipBonus = CommandResolver.GetHireRelationshipBonus(world, officer, playerFactionId);
        return $"{result}\n{localization.Format("fmt.personnel_advice_hire_relationship", relationshipName, relationshipBonus)}";
    }

    private static string BuildHireOfficerStats(OfficerData officer, WorldState world, LocalizationService localization)
    {
        var faction = world.Factions.FirstOrDefault(candidate =>
            candidate.RulerOfficerId == officer.Id || candidate.OfficerIds.Contains(officer.Id));
        var factionName = faction == null ? localization.T("ui.free_officer") : localization.GetFactionName(world, faction.Id);
        return localization.Format(
            "fmt.personnel_advice_hire_stats",
            officer.Leadership,
            officer.Strength,
            officer.Intelligence,
            officer.Politics,
            officer.Charm,
            officer.Combat,
            factionName);
    }

    private void UpdateSummary()
    {
        if (_context.Localization == null || _summaryLabel == null)
        {
            return;
        }

        var goldOffer = GetEnteredWholeNumber(_goldSpinBox);
        var foodOffer = GetEnteredWholeNumber(_foodSpinBox);
        var item = _context.GetSelectedItemFromOption(_itemOption);
        var summary = item == null
            ? _context.Localization.Format("fmt.hire_officer_preview", HudController.HireOfficerGoldCost, goldOffer, foodOffer)
            : _context.Localization.Format("fmt.hire_officer_preview_with_item", HudController.HireOfficerGoldCost, goldOffer, foodOffer, _context.Localization.GetItemName(item));
        _summaryLabel.Text = summary;
    }

    private void RefreshItemOptionTexts()
    {
        if (_itemOption == null || _context.Localization == null)
        {
            return;
        }

        var selectedItemId = _context.GetSelectedItemFromOption(_itemOption)?.Id ?? 0;
        _context.PopulateFactionInventoryOption(_itemOption);
        SelectItemOption(selectedItemId);
    }

    private void OnSelectOfficerPressed()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || localization == null)
        {
            return;
        }

        var candidateIds = GetOrderedCandidates(world, _context.TurnManager!.GetPlayerFactionId())
            .Select(officer => officer.Id)
            .ToList();
        if (candidateIds.Count == 0)
        {
            _context.AddLog(localization.T("ui.no_hireable_officer"));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("command.personnel.hire_officer"),
            candidateIds,
            HudController.OfficerSelectorPrimaryStat.Charm,
            officerId =>
            {
                _selectedOfficerId = officerId;
                if (_warningLabel != null) _warningLabel.Text = string.Empty;
                UpdateSelectedOfficerSummary();
                UpdateSummary();
            },
            titleFactory: () => _context.Localization?.T("command.personnel.hire_officer") ?? localization.T("command.personnel.hire_officer"),
            displayConfig: BuildHireOfficerSelectorDisplayConfig(candidateIds),
            enableLocationMapSelector: true);
    }

    private void OnSelectEnvoyPressed()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        if (world == null || city == null || localization == null) return;
        var ids = city.OfficerIds.Where(id => id != _selectedOfficerId).ToList();
        _context.ShowOfficerSelectorDialog(localization.T("ui.hire_officer_envoy"), ids, HudController.OfficerSelectorPrimaryStat.Charm, id =>
        {
            _selectedEnvoyOfficerId = id;
            UpdateSelectedEnvoySummary();
        });
    }

    private HudController.OfficerSelectorDisplayConfig BuildHireOfficerSelectorDisplayConfig(IReadOnlyCollection<int> candidateOfficerIds)
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || localization == null)
        {
            throw new InvalidOperationException("Hire officer selector requires world and localization.");
        }

        var showCityColumn = candidateOfficerIds
            .Select(world.GetOfficer)
            .Any(officer => officer?.CityId > 0);
        var columns = new List<HudController.OfficerSelectorColumnDefinition>
        {
            new() { Title = localization.T("ui.officers"), MinWidth = 110 },
            new() { Title = localization.T("ui.role"), MinWidth = 75 },
            new() { Title = localization.T("ui.faction"), MinWidth = 100 }
        };
        if (showCityColumn)
        {
            columns.Add(new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.city"), MinWidth = 95 });
        }

        columns.AddRange(
        [
            new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.status"), MinWidth = 78 },
            new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.age"), MinWidth = 50 },
            new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.loyalty"), MinWidth = 55 },
            new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.ambition"), MinWidth = 55 },
            new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.leadership"), MinWidth = 55 },
            new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.strength"), MinWidth = 55 },
            new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.intelligence"), MinWidth = 55 },
            new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.politics"), MinWidth = 55 },
            new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.charm"), MinWidth = 55 },
            new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.combat"), MinWidth = 55 }
        ]);

        return new HudController.OfficerSelectorDisplayConfig
        {
            Columns = columns,
            BuildRowTexts = officer =>
            {
                var city = world.GetCity(officer.CityId);
                var faction = world.Factions.FirstOrDefault(candidate =>
                    candidate.RulerOfficerId == officer.Id || candidate.OfficerIds.Contains(officer.Id));
                var age = officer.BirthYear > 0 ? Math.Max(0, world.Year - officer.BirthYear) : 0;
                var rowTexts = new List<string>
                {
                    localization.GetOfficerName(officer),
                    localization.GetOfficerRole(officer),
                    faction != null ? localization.GetFactionName(world, faction.Id) : localization.T("ui.free_officer")
                };
                if (showCityColumn)
                {
                    rowTexts.Add(city != null ? localization.GetCityName(city) : "--");
                }

                rowTexts.AddRange(
                [
                    localization.GetOfficerStatus(world, officer),
                    age.ToString(),
                    faction != null ? officer.Loyalty.ToString() : "--",
                    officer.Ambition.ToString(),
                    officer.Leadership.ToString(),
                    officer.Strength.ToString(),
                    officer.Intelligence.ToString(),
                    officer.Politics.ToString(),
                    officer.Charm.ToString(),
                    officer.Combat.ToString()
                ]);
                return rowTexts;
            },
            PanelSize = new Vector2(1380.0f, 390.0f)
        };
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_selectedOfficerLabel == null || _context.Localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.hire_officer_target")}: {officerName}";
    }

    private void UpdateSelectedEnvoySummary()
    {
        if (_selectedEnvoyLabel == null || _context.Localization == null) return;
        var officer = _selectedEnvoyOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedEnvoyOfficerId) : null;
        _selectedEnvoyLabel.Text = $"{_context.Localization.T("ui.hire_officer_envoy")}: {(officer == null ? _context.Localization.T("ui.unassigned") : _context.Localization.GetOfficerName(officer))}";
    }

    private static bool IsCandidate(PersonnelUiContext context, WorldState world, int playerFactionId, OfficerData officer)
    {
        if (context.IsFactionRuler(world, officer))
        {
            return false;
        }

        if (!context.IsOfficerOldEnoughToJoin(world, officer))
        {
            return false;
        }

        if (FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
        {
            return true;
        }

        var sourceCity = officer.CityId > 0 ? world.GetCity(officer.CityId) : null;
        return sourceCity == null || sourceCity.OwnerFactionId != playerFactionId;
    }

    private void OnConfirmPressed()
    {
        var city = _context.SelectedCity;
        var turnManager = _context.TurnManager;
        var commandResolver = _context.CommandResolver;
        if (city == null || turnManager == null || commandResolver == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            _context.AddLog(_context.Localization?.T("ui.select_officer_warning") ?? string.Empty);
            if (_warningLabel != null)
            {
                _warningLabel.Text = _context.Localization?.T("ui.select_officer_warning") ?? string.Empty;
            }
            ShowOverlay();
            return;
        }

        if (_selectedEnvoyOfficerId <= 0)
        {
            if (_warningLabel != null) _warningLabel.Text = _context.Localization?.T("ui.hire_officer_envoy_required") ?? string.Empty;
            ShowOverlay();
            return;
        }

        var result = commandResolver.ScheduleHireOfficer(
            turnManager.GetPlayerFactionId(),
            city.Id,
            _selectedOfficerId,
            _selectedEnvoyOfficerId,
            GetEnteredWholeNumber(_goldSpinBox),
            GetEnteredWholeNumber(_foodSpinBox),
            _context.GetSelectedItemFromOption(_itemOption)?.Id ?? 0);
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        HideOverlay();
        if (result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
            _context.UiEventHub.PublishOfficerStateChanged(_selectedOfficerId, city.Id, city.OwnerFactionId);
            _context.RefreshMapVisuals();
        }
    }

    private List<OfficerData> GetOrderedCandidates(WorldState world, int playerFactionId)
    {
        return world.Officers
            // Officers without a city are not yet placed on the campaign map,
            // so they cannot be selected as a city-based hire target.
            .Where(officer => officer.CityId > 0 && IsCandidate(_context, world, playerFactionId, officer))
            .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
            .ThenByDescending(officer => officer.Charm)
            .ThenByDescending(officer => officer.Intelligence)
            .ThenBy(officer => _context.Localization?.GetOfficerName(officer) ?? officer.Name)
            .ThenBy(officer => officer.Id)
            .ToList();
    }

    private static int GetEnteredWholeNumber(SpinBox? spinBox)
    {
        if (spinBox == null)
        {
            return 0;
        }

        var text = spinBox.GetLineEdit().Text;
        return double.TryParse(text, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.CurrentCulture, out var enteredValue)
            ? Math.Clamp((int)Math.Round(enteredValue), (int)spinBox.MinValue, (int)spinBox.MaxValue)
            : (int)spinBox.Value;
    }

    private T? GetNodeFromOverlay<T>(string path) where T : class
        => GetOverlayContentNode<T>(path);

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetNodeFromOverlay<Label>(nodeName) ??
                    GetNodeFromOverlay<Label>($"GoldRow/{nodeName}") ??
                    GetNodeFromOverlay<Label>($"FoodRow/{nodeName}") ??
                    GetNodeFromOverlay<Label>($"ItemRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void SelectItemOption(int itemId)
    {
        if (_itemOption == null)
        {
            return;
        }

        for (var index = 0; index < _itemOption.ItemCount; index += 1)
        {
            var metadata = _itemOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == itemId)
            {
                _itemOption.Select(index);
                return;
            }
        }

        if (_itemOption.ItemCount > 0)
        {
            _itemOption.Select(0);
        }
    }
}
