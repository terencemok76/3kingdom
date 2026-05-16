using System.Collections.Generic;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class VisitCitizenDialogController
{
    private readonly CivilUiContext _context;
    private Window? _dialog;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;

    public VisitCitizenDialogController(CivilUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        _dialog = _context.CreateWindow("res://scenes/ui/civil/VisitCitizenDialog.tscn", dialog => dialog.Hide());
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

        var candidateOfficerIds = _context.GetAvailableCityOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            _context.AddLog(_context.Localization.Format("ui.no_available_officer_for_command", _context.GetCommandName(CommandType.Search)));
            return;
        }

        EnsureWidgets();
        RefreshText();
        Populate(candidateOfficerIds);
        _context.PopupDialog(_dialog);
    }

    public void RefreshText()
    {
        if (_dialog == null || _context.Localization == null)
        {
            return;
        }

        _dialog.Title = _context.Localization.T("command.civil.investigate_people");
        var officerLabel = _dialog.GetNodeOrNull<Label>("VisitCitizenDialogRoot/OfficerListLabel");
        if (officerLabel != null)
        {
            officerLabel.Text = _context.Localization.T("ui.officers");
        }
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_officer_selection");
        }
        UpdateSelectedOfficerSummary();
    }

    private void EnsureWidgets()
    {
        if (_dialog == null)
        {
            return;
        }

        var root = _dialog.GetNodeOrNull<VBoxContainer>("VisitCitizenDialogRoot");
        if (root == null)
        {
            GD.PushError("VisitCitizenDialogRoot not found in VisitCitizenDialog.tscn.");
            return;
        }

        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_signalsConnected)
        {
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
    }

    private void Populate(List<int> candidateOfficerIds)
    {
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.Count > 0 ? candidateOfficerIds[0] : -1;
        }

        UpdateSelectedOfficerSummary();
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var candidateOfficerIds = _context.GetAvailableCityOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            _context.AddLog(localization.Format("ui.no_available_officer_for_command", _context.GetCommandName(CommandType.Search)));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("command.civil.investigate_people"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Charm,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
            });
    }

    private void OnConfirmPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            _context.AddLog(localization.T("ui.select_officer_warning"));
            _context.PopupDialog(_dialog);
            return;
        }

        var result = _context.ExecutePlayerCommand(CommandType.Search, officerIds: new List<int> { _selectedOfficerId });
        if (result.Success)
        {
            _dialog?.Hide();
        }
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_selectedOfficerLabel == null || _context.Localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.officers")}: {officerName}";
    }
}
