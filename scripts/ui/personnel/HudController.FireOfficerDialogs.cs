using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private void EnsureFireOfficerDialogWidgets()
    {
        if (_fireOfficerDialog == null)
        {
            return;
        }

        var existingRoot = _fireOfficerDialog.GetNodeOrNull<VBoxContainer>("FireOfficerDialogRoot");
        if (existingRoot == null)
        {
            GD.PushError("FireOfficerDialogRoot not found in FireOfficerDialog.tscn.");
            return;
        }

        _fireOfficerSelectedOfficerLabel = existingRoot.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _fireOfficerSelectOfficerButton = existingRoot.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _fireOfficerConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_fireOfficerDialogSignalsConnected)
        {
            if (_fireOfficerSelectOfficerButton != null)
            {
                _fireOfficerSelectOfficerButton.Pressed += OnFireOfficerSelectOfficerPressed;
            }
            if (_fireOfficerConfirmButton != null)
            {
                _fireOfficerConfirmButton.Pressed += OnFireOfficerDialogConfirmed;
            }
            _fireOfficerDialogSignalsConnected = true;
        }
    }

    private void ShowFireOfficerDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _fireOfficerDialog == null || _localization == null)
        {
            return;
        }

        if (GetFireOfficerCandidateIds().Count == 0)
        {
            AddLog(_localization.Format("ui.no_available_officer_for_command", _localization.T("command.personnel.fire_officer")));
            return;
        }

        EnsureFireOfficerDialogWidgets();
        UpdateFireOfficerDialogText();
        PopulateFireOfficerDialog();
        PopupDialogUsingSceneSize(_fireOfficerDialog);
    }

    private void PopulateFireOfficerDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return;
        }

        var candidateOfficerIds = GetFireOfficerCandidateIds();
        if (!candidateOfficerIds.Contains(_fireOfficerSelectedOfficerId))
        {
            _fireOfficerSelectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }
        UpdateFireOfficerSelectedOfficerSummary();
    }

    private List<int> GetFireOfficerCandidateIds()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return new List<int>();
        }

        var availableOfficerIds = GetAvailableOfficerIdsForOrder();
        return _selectedCity.OfficerIds
            .Where(officerId => availableOfficerIds.Contains(officerId))
            .Where(officerId =>
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                return officer != null && !IsFactionRuler(_turnManager.World, officer);
            })
            .ToList();
    }

    private void UpdateFireOfficerDialogText()
    {
        if (_fireOfficerDialog == null || _localization == null)
        {
            return;
        }

        _fireOfficerDialog.Title = _localization.T("command.personnel.fire_officer");
        SetFireOfficerDialogLabelText("OfficerListLabel", _localization.T("ui.officers"));
        if (_fireOfficerSelectOfficerButton != null)
        {
            _fireOfficerSelectOfficerButton.Text = _localization.T("ui.select_officer");
        }
        if (_fireOfficerConfirmButton != null)
        {
            _fireOfficerConfirmButton.Text = _localization.T("ui.confirm_personnel");
        }
        UpdateFireOfficerSelectedOfficerSummary();
    }

    private void SetFireOfficerDialogLabelText(string nodeName, string text)
    {
        var label = _fireOfficerDialog?.GetNodeOrNull<Label>($"FireOfficerDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void OnFireOfficerDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        if (_fireOfficerSelectedOfficerId <= 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenFireOfficerDialog();
            return;
        }

        var result = _commandResolver.ExecuteFireOfficer(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            _fireOfficerSelectedOfficerId);
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
        _fireOfficerDialog?.Hide();
    }

    private void ReopenFireOfficerDialog()
    {
        CallDeferred(nameof(ReopenFireOfficerDialogDeferred));
    }

    private void ReopenFireOfficerDialogDeferred()
    {
        PopupDialogUsingSceneSize(_fireOfficerDialog);
    }

    private void OnFireOfficerSelectOfficerPressed()
    {
        if (_localization == null)
        {
            return;
        }

        var candidateOfficerIds = GetFireOfficerCandidateIds();
        if (candidateOfficerIds.Count == 0)
        {
            AddLog(_localization.T("ui.select_officer_warning"));
            return;
        }

        ShowOfficerSelectorDialog(
            _localization.T("command.personnel.fire_officer"),
            candidateOfficerIds,
            OfficerSelectorPrimaryStat.Politics,
            SelectFireOfficerById);
    }

    private void SelectFireOfficerById(int officerId)
    {
        _fireOfficerSelectedOfficerId = officerId;
        UpdateFireOfficerSelectedOfficerSummary();
    }

    private void UpdateFireOfficerSelectedOfficerSummary()
    {
        if (_fireOfficerSelectedOfficerLabel == null || _localization == null)
        {
            return;
        }

        var officer = _fireOfficerSelectedOfficerId > 0 ? _turnManager?.World?.GetOfficer(_fireOfficerSelectedOfficerId) : null;
        var officerName = officer != null ? _localization.GetOfficerName(officer) : _localization.T("ui.unassigned");
        _fireOfficerSelectedOfficerLabel.Text = $"{_localization.T("ui.officers")}: {officerName}";
    }
}
