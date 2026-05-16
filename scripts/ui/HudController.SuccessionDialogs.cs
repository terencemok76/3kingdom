using Godot;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private void EnsureSuccessionDialogWidgets()
    {
        if (_successionDialog == null)
        {
            return;
        }

        var existingRoot = _successionDialog.GetNodeOrNull<VBoxContainer>("SuccessionDialogRoot");
        if (existingRoot == null)
        {
            GD.PushError("SuccessionDialogRoot not found in SuccessionDialog.tscn.");
            return;
        }

        _successionSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
        _successionSelectedOfficerLabel = existingRoot.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _successionSelectOfficerButton = existingRoot.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _successionWarningLabel = existingRoot.GetNodeOrNull<Label>("WarningLabel");
        _successionConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_successionDialogSignalsConnected)
        {
            if (_successionSelectOfficerButton != null)
            {
                _successionSelectOfficerButton.Pressed += OnSuccessionSelectOfficerPressed;
            }
            if (_successionConfirmButton != null)
            {
                _successionConfirmButton.Pressed += OnSuccessionDialogConfirmed;
            }
            _successionDialogSignalsConnected = true;
        }
    }

    private bool HasPendingPlayerSuccession()
    {
        if (_turnManager?.World == null)
        {
            return false;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        return playerFactionId > 0 && _turnManager.World.GetPendingSuccession(playerFactionId) != null;
    }

    private void ShowSuccessionDialog()
    {
        if (_turnManager?.World == null || _localization == null || _successionDialog == null)
        {
            return;
        }

        var factionId = _turnManager.GetPlayerFactionId();
        var pendingSuccession = _turnManager.World.GetPendingSuccession(factionId);
        var faction = _turnManager.World.GetFaction(factionId);
        if (pendingSuccession == null || faction == null)
        {
            return;
        }

        _pendingSuccessionFactionId = factionId;
        EnsureSuccessionDialogWidgets();
        _successionDialog.Title = _localization.T("ui.succession");
        if (_successionConfirmButton != null)
        {
            _successionConfirmButton.Text = _localization.T("ui.confirm_succession");
        }
        _successionSummaryLabel!.Text = _localization.Format("ui.succession_summary", _localization.GetFactionName(_turnManager.World, factionId));
        _successionWarningLabel!.Text = string.Empty;
        if (_successionSelectOfficerButton != null)
        {
            _successionSelectOfficerButton.Text = _localization.T("ui.select_officer");
        }
        if (_successionSelectedOfficerLabel != null)
        {
            var candidateOfficer = pendingSuccession.CandidateOfficerIds.Contains(_successionSelectedOfficerId)
                ? _turnManager.World.GetOfficer(_successionSelectedOfficerId)
                : null;
            if (candidateOfficer == null)
            {
                _successionSelectedOfficerId = pendingSuccession.CandidateOfficerIds.FirstOrDefault();
                candidateOfficer = _turnManager.World.GetOfficer(_successionSelectedOfficerId);
            }

            var officerName = candidateOfficer != null ? _localization.GetOfficerName(candidateOfficer) : _localization.T("ui.unassigned");
            _successionSelectedOfficerLabel.Text = $"{_localization.T("ui.officers")}: {officerName}";
        }

        PopupDialogUsingSceneSize(_successionDialog);
    }

    private void OnSuccessionDialogConfirmed()
    {
        if (_commandResolver == null || _localization == null || _pendingSuccessionFactionId <= 0)
        {
            return;
        }

        if (_successionSelectedOfficerId <= 0)
        {
            _successionWarningLabel!.Text = _localization.T("ui.select_officer_warning");
            PopupDialogUsingSceneSize(_successionDialog);
            return;
        }

        var result = _commandResolver.ResolvePlayerSuccession(_pendingSuccessionFactionId, _successionSelectedOfficerId);
        if (!result.Success)
        {
            _successionWarningLabel!.Text = GetLocalizedResultMessage(result);
            PopupDialogUsingSceneSize(_successionDialog);
            return;
        }

        _pendingSuccessionFactionId = -1;
        _successionDialog?.Hide();
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        RefreshSelectedCity();
        ContinuePendingNonAttackResolution();
    }

    private void OnSuccessionDialogCloseRequested()
    {
        PlayUiClickSfx();

        if (_pendingSuccessionFactionId > 0)
        {
            ShowSuccessionDialog();
        }
    }

    private void OnSuccessionSelectOfficerPressed()
    {
        if (_turnManager?.World == null || _localization == null || _pendingSuccessionFactionId <= 0)
        {
            return;
        }

        var pendingSuccession = _turnManager.World.GetPendingSuccession(_pendingSuccessionFactionId);
        var candidateOfficerIds = pendingSuccession?.CandidateOfficerIds.ToList() ?? new System.Collections.Generic.List<int>();
        if (candidateOfficerIds.Count == 0)
        {
            _successionWarningLabel!.Text = _localization.T("ui.select_officer_warning");
            return;
        }

        ShowOfficerSelectorDialog(
            _localization.T("ui.succession"),
            candidateOfficerIds,
            OfficerSelectorPrimaryStat.Politics,
            SelectSuccessionOfficerById);
    }

    private void SelectSuccessionOfficerById(int officerId)
    {
        _successionSelectedOfficerId = officerId;
        if (_successionSelectedOfficerLabel != null && _localization != null)
        {
            var officer = _turnManager?.World?.GetOfficer(officerId);
            var officerName = officer != null ? _localization.GetOfficerName(officer) : _localization.T("ui.unassigned");
            _successionSelectedOfficerLabel.Text = $"{_localization.T("ui.officers")}: {officerName}";
        }

        if (_successionWarningLabel != null)
        {
            _successionWarningLabel.Text = string.Empty;
        }
    }
}
