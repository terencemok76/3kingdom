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
        if (existingRoot != null)
        {
            _successionSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            _successionSelectedOfficerLabel = existingRoot.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
            _successionSelectOfficerButton = existingRoot.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
            _successionWarningLabel = existingRoot.GetNodeOrNull<Label>("WarningLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "SuccessionDialogRoot",
            CustomMinimumSize = new Vector2(720.0f, 420.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        _successionDialog.AddChild(root);

        _successionSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_successionSummaryLabel);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        var officerSelectorRow = new HBoxContainer
        {
            Name = "OfficerSelectorRow",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        officerSelectorRow.AddThemeConstantOverride("separation", 8);
        _successionSelectedOfficerLabel = new Label
        {
            Name = "SelectedOfficerLabel",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        officerSelectorRow.AddChild(_successionSelectedOfficerLabel);
        _successionSelectOfficerButton = new Button
        {
            Name = "SelectOfficerButton",
            FocusMode = Control.FocusModeEnum.None
        };
        _successionSelectOfficerButton.Pressed += OnSuccessionSelectOfficerPressed;
        officerSelectorRow.AddChild(_successionSelectOfficerButton);
        root.AddChild(officerSelectorRow);

        _successionWarningLabel = new Label
        {
            Name = "WarningLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_successionWarningLabel);
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
        _successionDialog.OkButtonText = _localization.T("ui.confirm_succession");
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

        _successionDialog.PopupCentered(new Vector2I(760, 240));
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
            _successionDialog?.PopupCentered(new Vector2I(760, 240));
            return;
        }

        var result = _commandResolver.ResolvePlayerSuccession(_pendingSuccessionFactionId, _successionSelectedOfficerId);
        if (!result.Success)
        {
            _successionWarningLabel!.Text = GetLocalizedResultMessage(result);
            _successionDialog?.PopupCentered(new Vector2I(760, 240));
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
