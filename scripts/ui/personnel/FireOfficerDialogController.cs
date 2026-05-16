using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ThreeKingdom.UI;

internal sealed class FireOfficerDialogController
{
    private readonly PersonnelUiContext _context;
    private Window? _dialog;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;

    public FireOfficerDialogController(PersonnelUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        _dialog = _context.CreateWindow("res://scenes/ui/personnel/FireOfficerDialog.tscn", dialog => dialog.Hide());
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

        if (GetCandidateOfficerIds().Count == 0)
        {
            _context.AddLog(_context.Localization.Format("ui.no_available_officer_for_command", _context.Localization.T("command.personnel.fire_officer")));
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

        _dialog.Title = _context.Localization.T("command.personnel.fire_officer");
        var label = _dialog.GetNodeOrNull<Label>("FireOfficerDialogRoot/OfficerListLabel");
        if (label != null)
        {
            label.Text = _context.Localization.T("ui.officers");
        }
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_personnel");
        }
        UpdateSelectedOfficerSummary();
    }

    private void EnsureWidgets()
    {
        if (_dialog == null)
        {
            return;
        }

        var root = _dialog.GetNodeOrNull<VBoxContainer>("FireOfficerDialogRoot");
        if (root == null)
        {
            GD.PushError("FireOfficerDialogRoot not found in FireOfficerDialog.tscn.");
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

    private void Populate()
    {
        var candidateOfficerIds = GetCandidateOfficerIds();
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }
        UpdateSelectedOfficerSummary();
    }

    private List<int> GetCandidateOfficerIds()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        if (city == null || world == null)
        {
            return new List<int>();
        }

        var availableOfficerIds = _context.GetAvailableOfficerIdsForOrder();
        return city.OfficerIds
            .Where(availableOfficerIds.Contains)
            .Where(officerId =>
            {
                var officer = world.GetOfficer(officerId);
                return officer != null && !_context.IsFactionRuler(world, officer);
            })
            .ToList();
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

        var result = commandResolver.ExecuteFireOfficer(turnManager.GetPlayerFactionId(), city.Id, _selectedOfficerId);
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.RefreshSelectedCity();
        _context.RefreshMapVisuals();
        _dialog?.Hide();
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var candidateOfficerIds = GetCandidateOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            _context.AddLog(localization.T("ui.select_officer_warning"));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("command.personnel.fire_officer"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Politics,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
            });
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
