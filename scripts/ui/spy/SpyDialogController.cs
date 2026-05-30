using System.Linq;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class SpyDialogController : FloatingOverlayController
{
    private readonly SpyUiContext _context;
    private OptionButton? _actionOption;
    private OptionButton? _targetCityOption;
    private OptionButton? _targetOfficerOption;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Label? _summaryLabel;
    private Label? _warningLabel;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(510.0f, 290.0f);

    public SpyDialogController(SpyUiContext context)
        : base(context, "res://scenes/ui/spy/SpyDialog.tscn")
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

        SetOverlayTitleText(_context.Localization.T("ui.spy"));
        SetLabelText("ActionRow/ActionLabel", _context.Localization.T("ui.spy_action"));
        SetLabelText("TargetCityRow/TargetCityLabel", _context.Localization.T("ui.spy_target_city"));
        SetLabelText("TargetOfficerRow/TargetOfficerLabel", _context.Localization.T("ui.spy_target_officer"));
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.spy_officer"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }

        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_spy");
        }

        var selectedTargetOfficerId = GetSelectedTargetOfficerId();
        RefreshActionOptionTexts();
        RefreshTargetCityOptionTexts();
        PopulateTargetOfficerOptions(selectedTargetOfficerId);
        UpdateSelectedOfficerSummary();
        UpdateSummary();
        UpdateConfirmButtonState();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _actionOption = root.GetNodeOrNull<OptionButton>("ActionRow/ActionOption");
        _targetCityOption = root.GetNodeOrNull<OptionButton>("TargetCityRow/TargetCityOption");
        _targetOfficerOption = root.GetNodeOrNull<OptionButton>("TargetOfficerRow/TargetOfficerOption");
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _warningLabel = root.GetNodeOrNull<Label>("WarningLabel");
        _confirmButton = root.GetNodeOrNull<Button>("FooterRow/ConfirmButton");
        ApplyButtonThemes();
        ConnectSignals();
    }

    private void Populate()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        if (world == null || city == null || localization == null || _actionOption == null || _targetCityOption == null)
        {
            return;
        }

        _actionOption.Clear();
        AddActionOption(SpyActionType.Reconnaissance);
        AddActionOption(SpyActionType.Sabotage);
        AddActionOption(SpyActionType.Incite);
        AddActionOption(SpyActionType.Assassination);

        _targetCityOption.Clear();
        foreach (var targetCity in world.Cities.Where(target => target.OwnerFactionId != city.OwnerFactionId))
        {
            var ownerName = localization.GetFactionName(world, targetCity.OwnerFactionId);
            _targetCityOption.AddItem($"{localization.GetCityName(targetCity)} | {ownerName}");
            _targetCityOption.SetItemMetadata(_targetCityOption.ItemCount - 1, targetCity.Id);
        }

        var candidateOfficerIds = _context.GetAvailableSpyOfficerIds();
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        PopulateTargetOfficerOptions();
        SetWarning(string.Empty);
        UpdateSelectedOfficerSummary();
        UpdateSummary();
        UpdateConfirmButtonState();
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

    private void AddActionOption(SpyActionType actionType)
    {
        if (_actionOption == null || _context.Localization == null)
        {
            return;
        }

        var key = actionType switch
        {
            SpyActionType.Reconnaissance => "command.spy.reconnaissance",
            SpyActionType.Sabotage => "command.spy.sabotage",
            SpyActionType.Incite => "command.spy.incite",
            SpyActionType.Assassination => "command.spy.assassination",
            _ => "command.spy.reconnaissance"
        };
        _actionOption.AddItem(_context.Localization.T(key));
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
        AddActionOption(SpyActionType.Reconnaissance);
        AddActionOption(SpyActionType.Sabotage);
        AddActionOption(SpyActionType.Incite);
        AddActionOption(SpyActionType.Assassination);
        SelectActionOption(selectedActionType);
    }

    private void RefreshTargetCityOptionTexts()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        if (world == null || city == null || localization == null || _targetCityOption == null)
        {
            return;
        }

        var selectedTargetCityId = GetSelectedTargetCityId();
        _targetCityOption.Clear();
        foreach (var targetCity in world.Cities.Where(target => target.OwnerFactionId != city.OwnerFactionId))
        {
            var ownerName = localization.GetFactionName(world, targetCity.OwnerFactionId);
            _targetCityOption.AddItem($"{localization.GetCityName(targetCity)} | {ownerName}");
            _targetCityOption.SetItemMetadata(_targetCityOption.ItemCount - 1, targetCity.Id);
        }

        SelectTargetCityOption(selectedTargetCityId);
    }

    private SpyActionType GetSelectedActionType()
    {
        if (_actionOption == null || _actionOption.Selected < 0)
        {
            return SpyActionType.Reconnaissance;
        }

        var metadata = _actionOption.GetItemMetadata(_actionOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (SpyActionType)metadata.AsInt32()
            : SpyActionType.Reconnaissance;
    }

    private int GetSelectedTargetCityId()
    {
        if (_targetCityOption == null || _targetCityOption.ItemCount == 0 || _targetCityOption.Selected < 0)
        {
            return -1;
        }

        var metadata = _targetCityOption.GetItemMetadata(_targetCityOption.Selected);
        return metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : -1;
    }

    private int GetSelectedTargetOfficerId()
    {
        if (_targetOfficerOption == null || _targetOfficerOption.ItemCount == 0 || _targetOfficerOption.Selected < 0)
        {
            return -1;
        }

        var metadata = _targetOfficerOption.GetItemMetadata(_targetOfficerOption.Selected);
        return metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : -1;
    }

    private void SelectActionOption(SpyActionType actionType)
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

    private void SelectTargetCityOption(int cityId)
    {
        if (_targetCityOption == null)
        {
            return;
        }

        for (var index = 0; index < _targetCityOption.ItemCount; index += 1)
        {
            var metadata = _targetCityOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == cityId)
            {
                _targetCityOption.Select(index);
                return;
            }
        }

        if (_targetCityOption.ItemCount > 0)
        {
            _targetCityOption.Select(0);
        }
    }

    private string GetSelectedTargetOfficerName()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || localization == null)
        {
            return "-";
        }

        var officer = world.GetOfficer(GetSelectedTargetOfficerId());
        return officer != null ? localization.GetOfficerName(officer) : "-";
    }

    private void UpdateSummary()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || localization == null || _summaryLabel == null)
        {
            return;
        }

        var actionType = GetSelectedActionType();
        var targetCity = world.GetCity(GetSelectedTargetCityId());
        var targetCityName = targetCity != null ? localization.GetCityName(targetCity) : "-";
        var actionName = localization.T(actionType switch
        {
            SpyActionType.Reconnaissance => "command.spy.reconnaissance",
            SpyActionType.Sabotage => "command.spy.sabotage",
            SpyActionType.Incite => "command.spy.incite",
            SpyActionType.Assassination => "command.spy.assassination",
            _ => "command.spy.reconnaissance"
        });

        if (actionType == SpyActionType.Assassination)
        {
            _summaryLabel.Text = localization.Format("fmt.spy_assassination_summary", actionName, targetCityName, GetSelectedTargetOfficerName());
            return;
        }

        _summaryLabel.Text = localization.Format("fmt.spy_summary", actionName, targetCityName);
    }

    private void UpdateConfirmButtonState()
    {
        if (_confirmButton == null)
        {
            return;
        }

        var hasOfficer = _selectedOfficerId > 0;
        var hasTarget = GetSelectedTargetCityId() > 0;
        var needsTargetOfficer = GetSelectedActionType() == SpyActionType.Assassination;
        var hasTargetOfficer = !needsTargetOfficer || GetSelectedTargetOfficerId() > 0;
        _confirmButton.Disabled = !hasOfficer || !hasTarget || !hasTargetOfficer;
    }

    private void SetWarning(string text)
    {
        if (_warningLabel != null)
        {
            _warningLabel.Text = text;
        }
    }

    private void ConnectSignals()
    {
        if (_signalsConnected)
        {
            return;
        }

        if (_actionOption != null)
        {
            _actionOption.ItemSelected += _ =>
            {
                PopulateTargetOfficerOptions();
                UpdateSummary();
                UpdateConfirmButtonState();
            };
        }

        if (_targetCityOption != null)
        {
            _targetCityOption.ItemSelected += _ =>
            {
                PopulateTargetOfficerOptions();
                UpdateSummary();
                UpdateConfirmButtonState();
            };
        }

        if (_targetOfficerOption != null)
        {
            _targetOfficerOption.ItemSelected += _ =>
            {
                UpdateSummary();
                UpdateConfirmButtonState();
            };
        }

        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Pressed += OnSelectOfficerPressed;
        }

        if (_confirmButton != null)
        {
            _confirmButton.Pressed += OnConfirmPressed;
        }

        _signalsConnected = true;
    }

    private void PopulateTargetOfficerOptions(int preferredOfficerId = -1)
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || _targetOfficerOption == null || localization == null)
        {
            return;
        }

        _targetOfficerOption.Clear();
        _targetOfficerOption.AddItem(localization.T("ui.none"));
        _targetOfficerOption.SetItemMetadata(0, -1);

        var targetCity = world.GetCity(GetSelectedTargetCityId());
        if (targetCity != null)
        {
            foreach (var officerId in targetCity.OfficerIds)
            {
                var officer = world.GetOfficer(officerId);
                if (officer == null || officer.DeathYear > 0 && world.Year > officer.DeathYear)
                {
                    continue;
                }

                _targetOfficerOption.AddItem($"{localization.GetOfficerName(officer)} | {localization.GetOfficerRole(officer)}");
                _targetOfficerOption.SetItemMetadata(_targetOfficerOption.ItemCount - 1, officer.Id);
            }
        }

        var isAssassination = GetSelectedActionType() == SpyActionType.Assassination;
        _targetOfficerOption.Disabled = !isAssassination;
        if (isAssassination && preferredOfficerId > 0)
        {
            for (var index = 1; index < _targetOfficerOption.ItemCount; index += 1)
            {
                var metadata = _targetOfficerOption.GetItemMetadata(index);
                if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == preferredOfficerId)
                {
                    _targetOfficerOption.Select(index);
                    return;
                }
            }
        }

        if (isAssassination && _targetOfficerOption.ItemCount > 1)
        {
            _targetOfficerOption.Select(1);
        }
        else
        {
            _targetOfficerOption.Select(0);
        }
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var candidateOfficerIds = _context.GetAvailableSpyOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            SetWarning(localization.T("ui.select_officer_warning"));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.spy_officer"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Intelligence,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
                UpdateConfirmButtonState();
                SetWarning(string.Empty);
            },
            () => _context.Localization?.T("ui.spy_officer") ?? localization.T("ui.spy_officer"));
    }

    private void OnConfirmPressed()
    {
        var localization = _context.Localization;
        if (_context.SelectedCity == null || _context.TurnManager == null || _context.CommandResolver == null || localization == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            SetWarning(localization.T("ui.select_officer_warning"));
            return;
        }

        var targetCityId = GetSelectedTargetCityId();
        if (targetCityId <= 0)
        {
            SetWarning(localization.T("ui.spy_target_required_warning"));
            return;
        }

        var actionType = GetSelectedActionType();
        var targetOfficerId = GetSelectedTargetOfficerId();
        if (actionType == SpyActionType.Assassination && targetOfficerId <= 0)
        {
            SetWarning(localization.T("ui.spy_target_officer_required_warning"));
            return;
        }

        var result = _context.ExecuteSpyCommand(targetCityId, _selectedOfficerId, actionType, targetOfficerId);
        var resultMessage = _context.GetLocalizedResultMessage(result);
        _context.AddLog(resultMessage, isPlayerRelated: true);
        if (result.Success)
        {
            HideOverlay();
            _context.UiEventHub.PublishCityStateChanged(_context.SelectedCity.Id, _context.SelectedCity.OwnerFactionId);
            _context.UiEventHub.PublishOfficerStateChanged(_selectedOfficerId, _context.SelectedCity.Id, _context.SelectedCity.OwnerFactionId);
            return;
        }

        SetWarning(resultMessage);
    }

    private void UpdateSelectedOfficerSummary()
    {
        var localization = _context.Localization;
        if (_selectedOfficerLabel == null || localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? localization.GetOfficerName(officer) : localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{localization.T("ui.spy_officer")}: {officerName}";
    }
}
