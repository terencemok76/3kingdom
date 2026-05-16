using System.Linq;
using Godot;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private void EnsureAssignRoleDialogWidgets()
    {
        if (_assignRoleDialog == null)
        {
            return;
        }

        var existingRoot = _assignRoleDialog.GetNodeOrNull<VBoxContainer>("AssignRoleDialogRoot");
        if (existingRoot == null)
        {
            GD.PushError("AssignRoleDialogRoot not found in AssignRoleDialog.tscn.");
            return;
        }

        _assignRoleSelectedOfficerLabel = existingRoot.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _assignRoleSelectOfficerButton = existingRoot.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _assignRoleOption = existingRoot.GetNodeOrNull<OptionButton>("RoleOption");
        _assignRoleConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_assignRoleDialogSignalsConnected)
        {
            if (_assignRoleSelectOfficerButton != null)
            {
                _assignRoleSelectOfficerButton.Pressed += OnAssignRoleSelectOfficerPressed;
            }
            if (_assignRoleConfirmButton != null)
            {
                _assignRoleConfirmButton.Pressed += OnAssignRoleDialogConfirmed;
            }
            _assignRoleDialogSignalsConnected = true;
        }
    }

    private void ShowAssignRoleDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _assignRoleDialog == null || _localization == null)
        {
            return;
        }

        EnsureAssignRoleDialogWidgets();
        UpdateAssignRoleDialogText();
        PopulateAssignRoleDialog();
        PopupDialogUsingSceneSize(_assignRoleDialog);
    }

    private void PopulateAssignRoleDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return;
        }

        var candidateOfficerIds = _selectedCity.OfficerIds
            .Where(officerId =>
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                return officer != null && !IsFactionRuler(_turnManager.World, officer);
            })
            .ToList();
        if (!candidateOfficerIds.Contains(_assignRoleSelectedOfficerId))
        {
            _assignRoleSelectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        if (_assignRoleOption != null)
        {
            _assignRoleOption.Clear();
            AddAssignRoleOption("General");
            AddAssignRoleOption("Strategist");
            AddAssignRoleOption("Advisor");
            AddAssignRoleOption("Governor");
        }
    }

    private void AddAssignRoleOption(string role)
    {
        if (_assignRoleOption == null || _localization == null)
        {
            return;
        }

        _assignRoleOption.AddItem(GetRoleDisplayName(role));
        _assignRoleOption.SetItemMetadata(_assignRoleOption.ItemCount - 1, role);
    }

    private void UpdateAssignRoleDialogText()
    {
        if (_assignRoleDialog == null || _localization == null)
        {
            return;
        }

        _assignRoleDialog.Title = _localization.T("command.personnel.assign_title");
        SetAssignRoleDialogLabelText("OfficerListLabel", _localization.T("ui.assign_role_officer"));
        SetAssignRoleDialogLabelText("RoleLabel", _localization.T("ui.assign_role_title"));
        if (_assignRoleSelectOfficerButton != null)
        {
            _assignRoleSelectOfficerButton.Text = _localization.T("ui.select_officer");
        }
        if (_assignRoleConfirmButton != null)
        {
            _assignRoleConfirmButton.Text = _localization.T("ui.confirm_assign_role");
        }
        UpdateAssignRoleSelectedOfficerSummary();
    }

    private void SetAssignRoleDialogLabelText(string nodeName, string text)
    {
        var label = _assignRoleDialog?.GetNodeOrNull<Label>($"AssignRoleDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private string GetRoleDisplayName(string role)
    {
        if (_localization == null)
        {
            return role;
        }

        return role.ToLowerInvariant() switch
        {
            "general" => _localization.T("role.general"),
            "strategist" => _localization.T("role.strategist"),
            "advisor" => _localization.T("role.advisor"),
            "governor" => _localization.T("role.governor"),
            _ => role
        };
    }

    private void OnAssignRoleDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        if (_assignRoleSelectedOfficerId <= 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenAssignRoleDialog();
            return;
        }

        var roleMetadata = _assignRoleOption?.GetItemMetadata(_assignRoleOption.Selected);
        var role = roleMetadata?.VariantType == Variant.Type.String ? roleMetadata.Value.AsString() : "General";
        var result = _commandResolver.ExecuteAssignOfficerRole(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            _assignRoleSelectedOfficerId,
            role);
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        RefreshSelectedCity();
        _assignRoleDialog?.Hide();
    }

    private void ReopenAssignRoleDialog()
    {
        CallDeferred(nameof(ReopenAssignRoleDialogDeferred));
    }

    private void ReopenAssignRoleDialogDeferred()
    {
        PopupDialogUsingSceneSize(_assignRoleDialog);
    }

    private void OnAssignRoleSelectOfficerPressed()
    {
        if (_selectedCity == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        var candidateOfficerIds = _selectedCity.OfficerIds
            .Where(officerId =>
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                return officer != null && !IsFactionRuler(_turnManager.World, officer);
            })
            .ToList();
        if (candidateOfficerIds.Count == 0)
        {
            AddLog(_localization.T("ui.select_officer_warning"));
            return;
        }

        ShowOfficerSelectorDialog(
            _localization.T("ui.assign_role_officer"),
            candidateOfficerIds,
            OfficerSelectorPrimaryStat.Politics,
            SelectAssignRoleOfficerById);
    }

    private void SelectAssignRoleOfficerById(int officerId)
    {
        _assignRoleSelectedOfficerId = officerId;
        UpdateAssignRoleSelectedOfficerSummary();
    }

    private void UpdateAssignRoleSelectedOfficerSummary()
    {
        if (_assignRoleSelectedOfficerLabel == null || _localization == null)
        {
            return;
        }

        var officer = _assignRoleSelectedOfficerId > 0 ? _turnManager?.World?.GetOfficer(_assignRoleSelectedOfficerId) : null;
        var officerName = officer != null ? _localization.GetOfficerName(officer) : _localization.T("ui.unassigned");
        _assignRoleSelectedOfficerLabel.Text = $"{_localization.T("ui.assign_role_officer")}: {officerName}";
    }
}
