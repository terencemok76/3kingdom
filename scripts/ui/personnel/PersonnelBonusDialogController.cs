using System;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class PersonnelBonusDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private SpinBox? _goldSpinBox;
    private SpinBox? _foodSpinBox;
    private OptionButton? _itemOption;
    private Label? _summaryLabel;
    private Button? _advisorButton;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(480.0f, 260.0f);

    public PersonnelBonusDialogController(PersonnelUiContext context)
        : base(context, "res://scenes/ui/personnel/PersonnelBonusDialog.tscn")
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

        SetOverlayTitleText(_context.Localization.T("command.personnel.give_bonus"));
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.personnel_bonus_officer"));
        SetLabelText("GoldLabel", _context.Localization.T("ui.personnel_bonus_gold"));
        SetLabelText("FoodLabel", _context.Localization.T("ui.personnel_bonus_food"));
        SetLabelText("ItemLabel", _context.Localization.T("ui.personnel_bonus_item"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_personnel_bonus");
        }
        if (_advisorButton != null)
        {
            _advisorButton.Text = _context.Localization.T("ui.personnel_bonus_advice_button");
        }
        RefreshItemOptionTexts();
        UpdateSelectedOfficerSummary();
        UpdateSummary();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _goldSpinBox = root.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
        _foodSpinBox = root.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
        _itemOption = root.GetNodeOrNull<OptionButton>("ItemRow/ItemOption");
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
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
        if (_advisorButton != null)
        {
            _context.ApplyCommandButtonTheme(_advisorButton);
        }
        if (!_signalsConnected)
        {
            if (_selectOfficerButton != null)
            {
                _selectOfficerButton.Pressed += OnSelectOfficerPressed;
            }
            if (_goldSpinBox != null)
            {
                _goldSpinBox.ValueChanged += _ => UpdateSummary();
            }
            if (_foodSpinBox != null)
            {
                _foodSpinBox.ValueChanged += _ => UpdateSummary();
            }
            if (_itemOption != null)
            {
                _itemOption.ItemSelected += _ => UpdateSummary();
            }
            if (_confirmButton != null)
            {
                _confirmButton.Pressed += OnConfirmPressed;
            }
            if (_advisorButton != null)
            {
                _advisorButton.Pressed += OnAdvisorPressed;
            }
            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        var city = _context.SelectedCity;
        if (city == null)
        {
            return;
        }

        var candidateOfficerIds = _context.GetNonRulerCityOfficerIds();
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        _context.ConfigureMoveSpinBox(_goldSpinBox, city.Gold, 0);
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
        UpdateSelectedOfficerSummary();
        UpdateSummary();
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName) ??
                    GetOverlayContentNode<Label>($"GoldRow/{nodeName}") ??
                    GetOverlayContentNode<Label>($"FoodRow/{nodeName}") ??
                    GetOverlayContentNode<Label>($"ItemRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void UpdateSummary()
    {
        if (_summaryLabel == null || _goldSpinBox == null || _foodSpinBox == null || _context.Localization == null)
        {
            return;
        }

        var gold = (int)_goldSpinBox.Value;
        var food = (int)_foodSpinBox.Value;
        var gain = gold / 100 + food / 500;
        var item = _context.GetSelectedItemFromOption(_itemOption);
        _summaryLabel.Text = item == null
            ? _context.Localization.Format("fmt.personnel_bonus_preview", gain)
            : _context.Localization.Format("fmt.personnel_bonus_preview_with_item", gain + Math.Max(1, item.LoyaltyBonus), _context.Localization.GetItemName(item));
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

    private void OnAdvisorPressed()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (city == null || world == null || localization == null)
        {
            return;
        }

        var advisor = FindPersonnelAdvisor(world, city);
        var role = world.GetFaction(city.OwnerFactionId)?.ChancellorOfficerId == advisor?.Id
            ? localization.T("ui.chancellor")
            : localization.T("ui.local_place");
        _context.ShowAdvisorMessage(advisor, role, BuildBonusAdvice(world, city, localization));
    }

    private OfficerData? FindPersonnelAdvisor(WorldState world, CityData city)
    {
        var faction = world.GetFaction(city.OwnerFactionId);
        var chancellor = faction != null ? world.GetOfficer(faction.ChancellorOfficerId) : null;
        if (IsOfficerAvailableForAdvice(chancellor, world))
        {
            return chancellor;
        }

        return city.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => IsOfficerAvailableForAdvice(officer, world))
            .Cast<OfficerData>()
            .OrderByDescending(officer => officer.Politics)
            .ThenByDescending(officer => officer.Intelligence)
            .ThenBy(officer => officer.Id)
            .FirstOrDefault();
    }

    private static bool IsOfficerAvailableForAdvice(OfficerData? officer, WorldState world)
    {
        return officer != null &&
               officer.CaptiveFactionId <= 0 &&
               (officer.DeathYear <= 0 || world.Year <= officer.DeathYear);
    }

    private string BuildBonusAdvice(WorldState world, CityData city, LocalizationService localization)
    {
        var candidates = _context.GetNonRulerCityOfficerIds()
            .Select(world.GetOfficer)
            .Where(officer => officer != null)
            .Cast<OfficerData>()
            .ToList();
        if (candidates.Count == 0)
        {
            return localization.T("ui.personnel_bonus_advice_no_candidate");
        }

        var item = _context.GetSelectedItemFromOption(_itemOption);
        var recommended = candidates
            .OrderByDescending(officer => GetRewardPriority(officer, item))
            .ThenBy(officer => officer.Id)
            .First();
        var selected = world.GetOfficer(_selectedOfficerId) ?? recommended;
        var gold = Math.Max(0, (int)(_goldSpinBox?.Value ?? 0));
        var food = Math.Max(0, (int)(_foodSpinBox?.Value ?? 0));
        var loyaltyGain = gold / 100 + food / 500 + (item != null ? Math.Max(1, item.LoyaltyBonus) : 0);
        var expectedLoyalty = Math.Min(100, selected.Loyalty + loyaltyGain);
        var itemEffect = item == null
            ? localization.T("ui.personnel_bonus_advice_no_item")
            : localization.Format(
                "fmt.personnel_bonus_advice_item",
                localization.GetItemName(item),
                Math.Max(1, item.LoyaltyBonus));
        var comparison = selected.Id == recommended.Id
            ? localization.T("ui.personnel_bonus_advice_selected_best")
            : localization.T("ui.personnel_bonus_advice_change_target");
        return localization.Format(
            "fmt.personnel_bonus_advice",
            localization.GetOfficerName(recommended),
            recommended.Loyalty,
            recommended.Ambition,
            localization.GetOfficerName(selected),
            loyaltyGain,
            selected.Loyalty,
            expectedLoyalty,
            itemEffect,
            comparison);
    }

    private static int GetRewardPriority(OfficerData officer, ItemData? item)
    {
        var urgency = (100 - officer.Loyalty) * 4 + officer.Ambition;
        if (item == null)
        {
            return urgency;
        }

        var itemFit = item.ItemType switch
        {
            ItemType.Weapon => officer.Combat + officer.Strength,
            ItemType.Horse => officer.Leadership + officer.Combat,
            ItemType.Book => officer.Intelligence + officer.Politics,
            ItemType.Treasure => officer.Charm + officer.Politics,
            _ => 0
        };
        return urgency + itemFit / 5;
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
            ShowOverlay();
            return;
        }

        var result = commandResolver.ExecutePersonnelBonus(
            turnManager.GetPlayerFactionId(),
            city.Id,
            _selectedOfficerId,
            (int)(_goldSpinBox?.Value ?? 0),
            (int)(_foodSpinBox?.Value ?? 0),
            _context.GetSelectedItemFromOption(_itemOption)?.Id ?? 0);
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        if (result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
            _context.UiEventHub.PublishOfficerStateChanged(_selectedOfficerId, city.Id, city.OwnerFactionId);
            _context.RefreshMapVisuals();
        }
        HideOverlay();
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var candidateOfficerIds = _context.GetNonRulerCityOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            _context.AddLog(localization.T("ui.select_officer_warning"));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.personnel_bonus_officer"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Charm,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
            },
            titleFactory: () => _context.Localization?.T("ui.personnel_bonus_officer") ?? localization.T("ui.personnel_bonus_officer"),
            displayConfig: _context.BuildPersonnelOfficerSelectorDisplayConfig());
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_selectedOfficerLabel == null || _context.Localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.personnel_bonus_officer")}: {officerName}";
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
