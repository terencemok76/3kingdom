using System;
using System.Linq;
using Godot;

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
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_selectOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_selectOfficerButton);
        }
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
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
            titleFactory: () => _context.Localization?.T("ui.personnel_bonus_officer") ?? localization.T("ui.personnel_bonus_officer"));
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
