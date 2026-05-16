using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class AdvisorAssignmentDialogController
{
    private readonly AdvisorUiContext _context;
    private Window? _dialog;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private OptionButton? _positionOption;
    private Label? _summaryLabel;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;

    public AdvisorAssignmentDialogController(AdvisorUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        _dialog = _context.CreateWindow("res://scenes/ui/advisor/AdvisorDialog.tscn", dialog => dialog.Hide());
        EnsureWidgets();
        _dialog.Hide();
    }

    public void Hide() => _dialog?.Hide();

    public void Show()
    {
        if (_context.SelectedCity == null || _context.TurnManager?.World == null || _dialog == null || _context.Localization == null)
        {
            return;
        }

        EnsureWidgets();
        RefreshText();
        Populate();
        _context.PopupDialog(_dialog);
    }

    public void RefreshText()
    {
        if (_dialog == null || _context.Localization == null)
        {
            return;
        }

        _dialog.Title = _context.Localization.T("ui.advisor_assign_title");
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.advisor_assign_officer"));
        SetLabelText("PositionLabel", _context.Localization.T("ui.advisor_position"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_advisor_assign");
        }
        UpdateSummary();
    }

    private void EnsureWidgets()
    {
        if (_dialog == null)
        {
            return;
        }

        var root = _dialog.GetNodeOrNull<VBoxContainer>("AdvisorDialogRoot");
        if (root == null)
        {
            GD.PushError("AdvisorDialogRoot not found in AdvisorDialog.tscn.");
            return;
        }

        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _positionOption = root.GetNodeOrNull<OptionButton>("PositionOption");
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_signalsConnected)
        {
            if (_selectOfficerButton != null)
            {
                _selectOfficerButton.Pressed += OnSelectOfficerPressed;
            }
            if (_positionOption != null)
            {
                _positionOption.ItemSelected += _ => UpdateSummary();
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
        var candidateOfficerIds = _context.GetNonRulerCityOfficerIds();
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.Count > 0 ? candidateOfficerIds[0] : -1;
        }

        if (_positionOption != null)
        {
            _positionOption.Clear();
            AddPositionOption("Chancellor");
            AddPositionOption("ChiefStrategist");
        }

        UpdateSummary();
    }

    private void AddPositionOption(string position)
    {
        if (_positionOption == null || _context.Localization == null)
        {
            return;
        }

        var displayName = position == "Chancellor"
            ? _context.Localization.T("ui.chancellor")
            : _context.Localization.T("ui.chief_strategist");
        _positionOption.AddItem(displayName);
        _positionOption.SetItemMetadata(_positionOption.ItemCount - 1, position);
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = _dialog?.GetNodeOrNull<Label>($"AdvisorDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void UpdateSummary()
    {
        if (_summaryLabel == null || _context.SelectedCity == null || _context.TurnManager?.World == null || _context.Localization == null)
        {
            return;
        }

        var faction = _context.TurnManager.World.GetFaction(_context.SelectedCity.OwnerFactionId);
        if (faction == null)
        {
            _summaryLabel.Text = string.Empty;
            return;
        }

        var chancellor = _context.TurnManager.World.GetOfficer(faction.ChancellorOfficerId);
        var chiefStrategist = _context.TurnManager.World.GetOfficer(faction.ChiefStrategistOfficerId);
        var chancellorName = chancellor != null ? _context.Localization.GetOfficerName(chancellor) : _context.Localization.T("ui.unassigned");
        var chiefStrategistName = chiefStrategist != null ? _context.Localization.GetOfficerName(chiefStrategist) : _context.Localization.T("ui.unassigned");
        var selectedOfficer = _selectedOfficerId > 0 ? _context.TurnManager.World.GetOfficer(_selectedOfficerId) : null;
        var selectedOfficerName = selectedOfficer != null ? _context.Localization.GetOfficerName(selectedOfficer) : _context.Localization.T("ui.none");
        Variant? positionMetadata = null;
        if (_positionOption != null && _positionOption.ItemCount > 0 && _positionOption.Selected >= 0)
        {
            positionMetadata = _positionOption.GetItemMetadata(_positionOption.Selected);
        }

        var position = positionMetadata?.VariantType == Variant.Type.String ? positionMetadata.Value.AsString() : "Chancellor";
        var positionName = position == "Chancellor" ? _context.Localization.T("ui.chancellor") : _context.Localization.T("ui.chief_strategist");
        if (_selectedOfficerLabel != null)
        {
            _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.advisor_assign_officer")}: {selectedOfficerName}";
        }

        _summaryLabel.Text =
            $"{_context.Localization.T("ui.chancellor")}: {chancellorName}\n" +
            $"{_context.Localization.T("ui.chief_strategist")}: {chiefStrategistName}\n" +
            $"{_context.Localization.T("ui.advisor_pending_assignment")}: {selectedOfficerName} -> {positionName}";
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
            _context.PopupDialog(_dialog);
            return;
        }

        Variant? positionMetadata = null;
        if (_positionOption != null && _positionOption.ItemCount > 0 && _positionOption.Selected >= 0)
        {
            positionMetadata = _positionOption.GetItemMetadata(_positionOption.Selected);
        }

        var position = positionMetadata?.VariantType == Variant.Type.String ? positionMetadata.Value.AsString() : "Chancellor";
        var result = commandResolver.ExecuteAssignFactionAdvisor(
            turnManager.GetPlayerFactionId(),
            city.Id,
            _selectedOfficerId,
            position);
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.RefreshSelectedCity();
        _dialog?.Hide();
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
            localization.T("ui.advisor_assign_officer"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Politics,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSummary();
            });
    }
}
