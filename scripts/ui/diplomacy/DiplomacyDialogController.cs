using System;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class DiplomacyDialogController : FloatingOverlayController
{
    private readonly DiplomacyUiContext _context;
    private OptionButton? _actionOption;
    private OptionButton? _targetFactionOption;
    private SpinBox? _durationSpinBox;
    private SpinBox? _goldSpinBox;
    private SpinBox? _foodSpinBox;
    private SpinBox? _horseSpinBox;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Label? _relationInfoLabel;
    private Label? _summaryLabel;
    private Label? _warningLabel;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    private Vector2 _dialogSize = new(500.0f, 460.0f);
    protected override Vector2 MinimumOverlaySize => _dialogSize;

    public DiplomacyDialogController(DiplomacyUiContext context)
        : base(context, "res://scenes/ui/diplomacy/DiplomacyDialog.tscn")
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
        if (_context.SelectedCity == null || _context.Localization == null)
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

        SetOverlayTitleText(_context.Localization.T("ui.diplomacy"));
        SetLabelText("HeaderSection/ActionRow/ActionLabel", _context.Localization.T("ui.diplomacy_action"));
        SetLabelText("HeaderSection/TargetFactionRow/TargetFactionLabel", _context.Localization.T("ui.diplomacy_target_faction"));
        SetLabelText("MiddleSection/DurationRow/DurationLabel", _context.Localization.T("ui.diplomacy_duration"));
        SetLabelText("MiddleSection/GoldRow/GoldLabel", _context.Localization.T("ui.diplomacy_gift_gold"));
        SetLabelText("MiddleSection/FoodRow/FoodLabel", _context.Localization.T("ui.diplomacy_gift_food"));
        SetLabelText("MiddleSection/HorseRow/HorseLabel", _context.Localization.T("ui.diplomacy_gift_horse"));
        SetLabelText("FooterSection/OfficerListLabel", _context.Localization.T("ui.diplomacy_officer"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_diplomacy");
        }

        RefreshActionOptionTexts();
        RefreshTargetFactionOptionTexts();
        UpdateInputState();
        UpdateRelationInfo();
        UpdateSelectedOfficerSummary();
        UpdateSummary();
        UpdateConfirmButtonState();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _actionOption = root.GetNodeOrNull<OptionButton>("HeaderSection/ActionRow/ActionOption");
        _targetFactionOption = root.GetNodeOrNull<OptionButton>("HeaderSection/TargetFactionRow/TargetFactionOption");
        _durationSpinBox = root.GetNodeOrNull<SpinBox>("MiddleSection/DurationRow/DurationSpinBox");
        _goldSpinBox = root.GetNodeOrNull<SpinBox>("MiddleSection/GoldRow/GoldSpinBox");
        _foodSpinBox = root.GetNodeOrNull<SpinBox>("MiddleSection/FoodRow/FoodSpinBox");
        _horseSpinBox = root.GetNodeOrNull<SpinBox>("MiddleSection/HorseRow/HorseSpinBox");
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("FooterSection/OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("FooterSection/OfficerSelectorRow/SelectOfficerButton");
        _relationInfoLabel = root.GetNodeOrNull<Label>("MiddleSection/RelationInfoLabel");
        _summaryLabel = root.GetNodeOrNull<Label>("FooterSection/SummaryLabel");
        _warningLabel = root.GetNodeOrNull<Label>("FooterSection/WarningLabel");
        _confirmButton = root.GetNodeOrNull<Button>("FooterSection/FooterRow/ConfirmButton");
        ApplyButtonThemes();
        if (!_signalsConnected)
        {
            if (_actionOption != null) _actionOption.ItemSelected += _ => OnActionChanged();
            if (_targetFactionOption != null) _targetFactionOption.ItemSelected += _ => OnTargetChanged();
            if (_durationSpinBox != null) _durationSpinBox.ValueChanged += _ => UpdateSummary();
            if (_goldSpinBox != null) _goldSpinBox.ValueChanged += _ => OnResourceChanged();
            if (_foodSpinBox != null) _foodSpinBox.ValueChanged += _ => OnResourceChanged();
            if (_horseSpinBox != null) _horseSpinBox.ValueChanged += _ => OnResourceChanged();
            if (_selectOfficerButton != null) _selectOfficerButton.Pressed += OnSelectOfficerPressed;
            if (_confirmButton != null) _confirmButton.Pressed += OnConfirmPressed;
            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        if (world == null || city == null || localization == null ||
            _actionOption == null || _targetFactionOption == null || _durationSpinBox == null || _goldSpinBox == null)
        {
            return;
        }

        _actionOption.Clear();
        AddActionOption(DiplomacyActionType.Alliance);
        AddActionOption(DiplomacyActionType.Truce);
        AddActionOption(DiplomacyActionType.Gift);
        AddActionOption(DiplomacyActionType.Demand);
        AddActionOption(DiplomacyActionType.BreakPact);

        _targetFactionOption.Clear();
        foreach (var faction in world.Factions.Where(faction =>
                     faction.Id != city.OwnerFactionId &&
                     world.Cities.Any(mapCity => mapCity.OwnerFactionId == faction.Id)))
        {
            _targetFactionOption.AddItem(localization.GetFactionName(world, faction.Id));
            _targetFactionOption.SetItemMetadata(_targetFactionOption.ItemCount - 1, faction.Id);
        }

        _durationSpinBox.Value = 3;
        _goldSpinBox.Value = 0;
        _goldSpinBox.MaxValue = city.Gold;
        if (_foodSpinBox != null)
        {
            _foodSpinBox.Value = 0;
            _foodSpinBox.MaxValue = city.Food;
        }
        if (_horseSpinBox != null)
        {
            _horseSpinBox.Value = 0;
            _horseSpinBox.MaxValue = city.Horses;
        }

        var candidateOfficerIds = DiplomacyUiHelpers.GetAvailableOfficerIds(_context.TurnManager, city);
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        UpdateSelectedOfficerSummary();
        SetWarning(string.Empty);
        UpdateInputState();
        UpdateRelationInfo();
        UpdateSummary();
        UpdateConfirmButtonState();
        UpdateDialogSize();
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName);
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void ApplyButtonThemes()
    {
        if (_selectOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_selectOfficerButton);
        }

        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }
    }

    private void AddActionOption(DiplomacyActionType actionType)
    {
        if (_actionOption == null || _context.Localization == null)
        {
            return;
        }

        _actionOption.AddItem(_context.Localization.T(DiplomacyUiHelpers.GetActionLocaleKey(actionType)));
        _actionOption.SetItemMetadata(_actionOption.ItemCount - 1, (int)actionType);
    }

    private void RefreshActionOptionTexts()
    {
        if (_actionOption == null || _context.Localization == null)
        {
            return;
        }

        var selectedActionType = GetSelectedActionType();
        _actionOption.Clear();
        AddActionOption(DiplomacyActionType.Alliance);
        AddActionOption(DiplomacyActionType.Truce);
        AddActionOption(DiplomacyActionType.Gift);
        AddActionOption(DiplomacyActionType.Demand);
        AddActionOption(DiplomacyActionType.BreakPact);
        SelectActionOption(selectedActionType);
    }

    private void RefreshTargetFactionOptionTexts()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        if (_targetFactionOption == null || world == null || city == null || localization == null)
        {
            return;
        }

        var selectedFactionId = GetSelectedTargetFactionId();
        _targetFactionOption.Clear();
        foreach (var faction in world.Factions.Where(faction =>
                     faction.Id != city.OwnerFactionId &&
                     world.Cities.Any(mapCity => mapCity.OwnerFactionId == faction.Id)))
        {
            _targetFactionOption.AddItem(localization.GetFactionName(world, faction.Id));
            _targetFactionOption.SetItemMetadata(_targetFactionOption.ItemCount - 1, faction.Id);
        }

        SelectTargetFactionOption(selectedFactionId);
    }

    private DiplomacyActionType GetSelectedActionType()
    {
        if (_actionOption == null || _actionOption.ItemCount == 0 || _actionOption.Selected < 0)
        {
            return DiplomacyActionType.Alliance;
        }

        var metadata = _actionOption.GetItemMetadata(_actionOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (DiplomacyActionType)metadata.AsInt32()
            : DiplomacyActionType.Alliance;
    }

    private int GetSelectedTargetFactionId()
    {
        if (_targetFactionOption == null || _targetFactionOption.ItemCount == 0 || _targetFactionOption.Selected < 0)
        {
            return -1;
        }

        var metadata = _targetFactionOption.GetItemMetadata(_targetFactionOption.Selected);
        return metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : -1;
    }

    private void SelectActionOption(DiplomacyActionType actionType)
    {
        if (_actionOption == null)
        {
            return;
        }

        for (var index = 0; index < _actionOption.ItemCount; index += 1)
        {
            var metadata = _actionOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == (int)actionType)
            {
                _actionOption.Select(index);
                return;
            }
        }

        if (_actionOption.ItemCount > 0)
        {
            _actionOption.Select(0);
        }
    }

    private void SelectTargetFactionOption(int factionId)
    {
        if (_targetFactionOption == null)
        {
            return;
        }

        for (var index = 0; index < _targetFactionOption.ItemCount; index += 1)
        {
            var metadata = _targetFactionOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == factionId)
            {
                _targetFactionOption.Select(index);
                return;
            }
        }

        if (_targetFactionOption.ItemCount > 0)
        {
            _targetFactionOption.Select(0);
        }
    }

    private void OnActionChanged()
    {
        UpdateInputState();
        UpdateSummary();
        UpdateConfirmButtonState();
        UpdateDialogSize();
    }

    private void OnTargetChanged()
    {
        UpdateRelationInfo();
        UpdateSummary();
    }

    private void OnResourceChanged()
    {
        UpdateSummary();
        UpdateConfirmButtonState();
    }

    private void UpdateSummary()
    {
        if (_context.TurnManager?.World == null || _context.Localization == null || _summaryLabel == null)
        {
            return;
        }

        var actionType = GetSelectedActionType();
        var targetFactionId = GetSelectedTargetFactionId();
        var targetFaction = _context.TurnManager.World.GetFaction(targetFactionId);
        var targetFactionName = targetFaction != null ? _context.Localization.GetFactionName(_context.TurnManager.World, targetFaction.Id) : "-";
        var duration = Math.Max(1, (int)Math.Round(_durationSpinBox?.Value ?? 1));
        var gold = Math.Max(0, (int)Math.Round(_goldSpinBox?.Value ?? 0));
        var food = Math.Max(0, (int)Math.Round(_foodSpinBox?.Value ?? 0));
        var horses = Math.Max(0, (int)Math.Round(_horseSpinBox?.Value ?? 0));
        var actionName = _context.Localization.T(DiplomacyUiHelpers.GetActionLocaleKey(actionType));
        var summaryKey = actionType switch
        {
            DiplomacyActionType.Gift => "fmt.diplomacy_summary_gift",
            DiplomacyActionType.Demand => "fmt.diplomacy_summary_demand",
            DiplomacyActionType.BreakPact => "fmt.diplomacy_summary_break_pact",
            _ => "fmt.diplomacy_summary_treaty"
        };
        _summaryLabel.Text = actionType switch
        {
            DiplomacyActionType.Gift => _context.Localization.Format(summaryKey, actionName, targetFactionName, DiplomacyUiHelpers.BuildDemandResourceSummary(_context.Localization, gold, food, horses)),
            DiplomacyActionType.Demand => _context.Localization.Format(summaryKey, actionName, targetFactionName, DiplomacyUiHelpers.BuildDemandResourceSummary(_context.Localization, gold, food, horses)),
            DiplomacyActionType.BreakPact => _context.Localization.Format(summaryKey, actionName, targetFactionName),
            _ => _context.Localization.Format(summaryKey, actionName, targetFactionName, duration)
        };
    }

    private void UpdateRelationInfo()
    {
        if (_context.TurnManager?.World == null || _context.Localization == null || _relationInfoLabel == null || _context.SelectedCity == null)
        {
            return;
        }

        var targetFactionId = GetSelectedTargetFactionId();
        var relation = _context.TurnManager.World.GetDiplomacyRelation(_context.SelectedCity.OwnerFactionId, targetFactionId);
        var status = relation?.Status ?? DiplomacyStatusType.Neutral;
        var remainingMonths = status == DiplomacyStatusType.Neutral ? "-" : (relation?.RemainingMonths ?? 0).ToString();
        var relationScore = relation?.RelationScore ?? 0;

        _relationInfoLabel.Text =
            $"{_context.Localization.T("ui.relation_status")}: {_context.GetStatusText(status)}\n" +
            $"{_context.Localization.T("ui.remaining_months")}: {remainingMonths}\n" +
            $"{_context.Localization.T("ui.relation_score")}: {relationScore}";
    }

    private void UpdateConfirmButtonState()
    {
        if (_confirmButton == null)
        {
            return;
        }

        var actionType = GetSelectedActionType();
        var hasOfficer = _selectedOfficerId > 0;
        var hasTarget = GetSelectedTargetFactionId() > 0;
        var hasRequiredResource = actionType switch
        {
            DiplomacyActionType.Gift => (_goldSpinBox?.Value ?? 0) > 0 || (_foodSpinBox?.Value ?? 0) > 0 || (_horseSpinBox?.Value ?? 0) > 0,
            DiplomacyActionType.Demand => (_goldSpinBox?.Value ?? 0) > 0 || (_foodSpinBox?.Value ?? 0) > 0 || (_horseSpinBox?.Value ?? 0) > 0,
            _ => true
        };
        _confirmButton.Disabled = !hasOfficer || !hasTarget || !hasRequiredResource;
    }

    private void UpdateInputState()
    {
        if (_context.Localization == null)
        {
            return;
        }

        var actionType = GetSelectedActionType();
        SetRowVisible("DurationRow", actionType is DiplomacyActionType.Alliance or DiplomacyActionType.Truce);
        SetRowVisible("GoldRow", actionType is DiplomacyActionType.Gift or DiplomacyActionType.Demand);
        SetRowVisible("FoodRow", actionType is DiplomacyActionType.Gift or DiplomacyActionType.Demand);
        SetRowVisible("HorseRow", actionType is DiplomacyActionType.Gift or DiplomacyActionType.Demand);
        SetLabelText("MiddleSection/GoldRow/GoldLabel", actionType == DiplomacyActionType.Demand ? _context.Localization.T("ui.diplomacy_demand_gold") : _context.Localization.T("ui.diplomacy_gift_gold"));
        SetLabelText("MiddleSection/FoodRow/FoodLabel", actionType == DiplomacyActionType.Demand ? _context.Localization.T("ui.diplomacy_demand_food") : _context.Localization.T("ui.diplomacy_gift_food"));
        SetLabelText("MiddleSection/HorseRow/HorseLabel", actionType == DiplomacyActionType.Demand ? _context.Localization.T("ui.diplomacy_demand_horse") : _context.Localization.T("ui.diplomacy_gift_horse"));
    }

    private void UpdateDialogSize()
    {
        var desiredHeight = GetSelectedActionType() switch
        {
            DiplomacyActionType.Gift or DiplomacyActionType.Demand => 520,
            DiplomacyActionType.BreakPact => 420,
            _ => 460
        };

        _dialogSize = new Vector2(500.0f, desiredHeight);
        if (OverlayRoot != null)
        {
            UpdateOverlayLayoutNow();
        }
    }

    private void SetRowVisible(string rowName, bool visible)
    {
        var row = GetOverlayContentNode<Control>($"MiddleSection/{rowName}");
        if (row != null)
        {
            row.Visible = visible;
        }
    }

    private void SetWarning(string text)
    {
        if (_warningLabel != null)
        {
            _warningLabel.Text = text;
        }
    }

    private void OnConfirmPressed()
    {
        var city = _context.SelectedCity;
        var turnManager = _context.TurnManager;
        var commandResolver = _context.CommandResolver;
        var localization = _context.Localization;
        if (city == null || turnManager == null || commandResolver == null || localization == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            SetWarning(localization.T("ui.select_officer_warning"));
            return;
        }

        var targetFactionId = GetSelectedTargetFactionId();
        if (targetFactionId <= 0)
        {
            SetWarning(localization.T("ui.diplomacy_target_required_warning"));
            return;
        }

        var actionType = GetSelectedActionType();
        var gold = Math.Max(0, (int)Math.Round(_goldSpinBox?.Value ?? 0));
        var food = Math.Max(0, (int)Math.Round(_foodSpinBox?.Value ?? 0));
        var horses = Math.Max(0, (int)Math.Round(_horseSpinBox?.Value ?? 0));
        if (actionType == DiplomacyActionType.Gift && gold <= 0 && food <= 0 && horses <= 0)
        {
            SetWarning(localization.T("ui.diplomacy_gift_resource_required_warning"));
            return;
        }

        if (actionType == DiplomacyActionType.Demand && gold <= 0 && food <= 0 && horses <= 0)
        {
            SetWarning(localization.T("ui.diplomacy_resource_required_warning"));
            return;
        }

        var result = commandResolver.Execute(new CommandRequest
        {
            Type = CommandType.Diplomacy,
            ActorFactionId = turnManager.GetPlayerFactionId(),
            SourceCityId = city.Id,
            TargetFactionId = targetFactionId,
            OfficerIds = new System.Collections.Generic.List<int> { _selectedOfficerId },
            GoldToSend = gold,
            FoodToSend = food,
            HorsesToSend = horses,
            DurationMonths = Math.Max(1, (int)Math.Round(_durationSpinBox?.Value ?? 1)),
            DiplomacyActionType = actionType
        });

        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        if (result.Success)
        {
            HideOverlay();
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
            return;
        }

        SetWarning(_context.GetLocalizedResultMessage(result));
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var candidateOfficerIds = DiplomacyUiHelpers.GetAvailableOfficerIds(_context.TurnManager, _context.SelectedCity);
        if (candidateOfficerIds.Count == 0)
        {
            SetWarning(localization.T("ui.select_officer_warning"));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.diplomacy_officer"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Charm,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
                UpdateConfirmButtonState();
                SetWarning(string.Empty);
            },
            () => _context.Localization?.T("ui.diplomacy_officer") ?? localization.T("ui.diplomacy_officer"));
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_selectedOfficerLabel == null || _context.Localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.diplomacy_officer")}: {officerName}";
    }
}
