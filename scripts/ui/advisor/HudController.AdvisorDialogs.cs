using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private void EnsureAdvisorButton()
    {
        var commandButtons = GetNodeOrNull<GridContainer>("Root/LeftPanel/CommandButtons");
        if (commandButtons == null)
        {
            return;
        }

        _advisorButton = commandButtons.GetNodeOrNull<Button>("AdvisorButton");
        if (_advisorButton != null)
        {
            return;
        }

        _advisorButton = new Button
        {
            Name = "AdvisorButton",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None
        };

        if (_personnelButton != null)
        {
            CopyButtonTheme(_personnelButton, _advisorButton);
        }

        commandButtons.AddChild(_advisorButton);
    }

    private void EnsureAdvisorDialogWidgets()
    {
        if (_advisorDialog == null)
        {
            return;
        }

        var existingRoot = _advisorDialog.GetNodeOrNull<VBoxContainer>("AdvisorDialogRoot");
        if (existingRoot != null)
        {
            _advisorSelectedOfficerLabel = existingRoot.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
            _advisorSelectOfficerButton = existingRoot.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
            _advisorPositionOption = existingRoot.GetNodeOrNull<OptionButton>("PositionOption");
            _advisorSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            _advisorConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
            if (!_advisorDialogSignalsConnected)
            {
                if (_advisorSelectOfficerButton != null)
                {
                    _advisorSelectOfficerButton.Pressed += OnAdvisorSelectOfficerPressed;
                }

                if (_advisorPositionOption != null)
                {
                    _advisorPositionOption.ItemSelected += _ => UpdateAdvisorSummary();
                }

                if (_advisorConfirmButton != null)
                {
                    _advisorConfirmButton.Pressed += OnAdvisorDialogConfirmed;
                }

                _advisorDialogSignalsConnected = true;
            }
            return;
        }

        var root = new VBoxContainer
        {
            Name = "AdvisorDialogRoot",
            CustomMinimumSize = new Vector2(480.0f, 380.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _advisorDialog.AddChild(root);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        var officerSelectorRow = new HBoxContainer
        {
            Name = "OfficerSelectorRow",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        officerSelectorRow.AddThemeConstantOverride("separation", 8);
        _advisorSelectedOfficerLabel = new Label
        {
            Name = "SelectedOfficerLabel",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        officerSelectorRow.AddChild(_advisorSelectedOfficerLabel);
        _advisorSelectOfficerButton = new Button
        {
            Name = "SelectOfficerButton",
            FocusMode = Control.FocusModeEnum.None
        };
        officerSelectorRow.AddChild(_advisorSelectOfficerButton);
        root.AddChild(officerSelectorRow);

        root.AddChild(new Label { Name = "PositionLabel" });
        _advisorPositionOption = new OptionButton
        {
            Name = "PositionOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_advisorPositionOption);

        _advisorSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_advisorSummaryLabel);

        var confirmRow = new HBoxContainer
        {
            Name = "ConfirmRow",
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _advisorConfirmButton = new Button
        {
            Name = "ConfirmButton",
            FocusMode = Control.FocusModeEnum.None
        };
        confirmRow.AddChild(_advisorConfirmButton);
        root.AddChild(confirmRow);

        _advisorSelectOfficerButton.Pressed += OnAdvisorSelectOfficerPressed;
        _advisorPositionOption.ItemSelected += _ => UpdateAdvisorSummary();
        _advisorConfirmButton.Pressed += OnAdvisorDialogConfirmed;
        _advisorDialogSignalsConnected = true;
    }

    private void ShowAdvisorDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _advisorDialog == null || _localization == null)
        {
            return;
        }

        EnsureAdvisorDialogWidgets();
        UpdateAdvisorDialogText();
        PopulateAdvisorDialog();
        PopupDialogUsingSceneSize(_advisorDialog);
    }

    private void PopulateAdvisorDialog()
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
        if (!candidateOfficerIds.Contains(_advisorSelectedOfficerId))
        {
            _advisorSelectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        if (_advisorPositionOption != null)
        {
            _advisorPositionOption.Clear();
            AddAdvisorPositionOption("Chancellor");
            AddAdvisorPositionOption("ChiefStrategist");
        }

        UpdateAdvisorSummary();
    }

    private void AddAdvisorPositionOption(string position)
    {
        if (_advisorPositionOption == null || _localization == null)
        {
            return;
        }

        var displayName = position == "Chancellor"
            ? _localization.T("ui.chancellor")
            : _localization.T("ui.chief_strategist");
        _advisorPositionOption.AddItem(displayName);
        _advisorPositionOption.SetItemMetadata(_advisorPositionOption.ItemCount - 1, position);
    }

    private void UpdateAdvisorDialogText()
    {
        if (_advisorDialog == null || _localization == null)
        {
            return;
        }

        _advisorDialog.Title = _localization.T("ui.advisor_assign_title");
        SetAdvisorDialogLabelText("OfficerListLabel", _localization.T("ui.advisor_assign_officer"));
        SetAdvisorDialogLabelText("PositionLabel", _localization.T("ui.advisor_position"));
        if (_advisorSelectOfficerButton != null)
        {
            _advisorSelectOfficerButton.Text = _localization.T("ui.select_officer");
        }
        if (_advisorConfirmButton != null)
        {
            _advisorConfirmButton.Text = _localization.T("ui.confirm_advisor_assign");
        }
        UpdateAdvisorSummary();
    }

    private void SetAdvisorDialogLabelText(string nodeName, string text)
    {
        var label = _advisorDialog?.GetNodeOrNull<Label>($"AdvisorDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void UpdateAdvisorSummary()
    {
        if (_advisorSummaryLabel == null || _selectedCity == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        var faction = _turnManager.World.GetFaction(_selectedCity.OwnerFactionId);
        if (faction == null)
        {
            _advisorSummaryLabel.Text = string.Empty;
            return;
        }

        var chancellor = _turnManager.World.GetOfficer(faction.ChancellorOfficerId);
        var chiefStrategist = _turnManager.World.GetOfficer(faction.ChiefStrategistOfficerId);
        var chancellorName = chancellor != null ? _localization.GetOfficerName(chancellor) : _localization.T("ui.unassigned");
        var chiefStrategistName = chiefStrategist != null ? _localization.GetOfficerName(chiefStrategist) : _localization.T("ui.unassigned");
        var selectedOfficer = _advisorSelectedOfficerId > 0 ? _turnManager.World.GetOfficer(_advisorSelectedOfficerId) : null;
        var selectedOfficerName = selectedOfficer != null ? _localization.GetOfficerName(selectedOfficer) : _localization.T("ui.none");
        Variant? positionMetadata = null;
        if (_advisorPositionOption != null &&
            _advisorPositionOption.ItemCount > 0 &&
            _advisorPositionOption.Selected >= 0)
        {
            positionMetadata = _advisorPositionOption.GetItemMetadata(_advisorPositionOption.Selected);
        }

        var position = positionMetadata?.VariantType == Variant.Type.String ? positionMetadata.Value.AsString() : "Chancellor";
        var positionName = position == "Chancellor" ? _localization.T("ui.chancellor") : _localization.T("ui.chief_strategist");
        if (_advisorSelectedOfficerLabel != null)
        {
            _advisorSelectedOfficerLabel.Text = $"{_localization.T("ui.advisor_assign_officer")}: {selectedOfficerName}";
        }

        _advisorSummaryLabel.Text =
            $"{_localization.T("ui.chancellor")}: {chancellorName}\n" +
            $"{_localization.T("ui.chief_strategist")}: {chiefStrategistName}\n" +
            $"{_localization.T("ui.advisor_pending_assignment")}: {selectedOfficerName} -> {positionName}";
    }

    private void OnAdvisorDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        if (_advisorSelectedOfficerId <= 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenAdvisorDialog();
            return;
        }

        Variant? positionMetadata = null;
        if (_advisorPositionOption != null &&
            _advisorPositionOption.ItemCount > 0 &&
            _advisorPositionOption.Selected >= 0)
        {
            positionMetadata = _advisorPositionOption.GetItemMetadata(_advisorPositionOption.Selected);
        }

        var position = positionMetadata?.VariantType == Variant.Type.String ? positionMetadata.Value.AsString() : "Chancellor";
        var result = _commandResolver.ExecuteAssignFactionAdvisor(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            _advisorSelectedOfficerId,
            position);
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        RefreshSelectedCity();
        _advisorDialog?.Hide();
    }

    private void ReopenAdvisorDialog()
    {
        CallDeferred(nameof(ReopenAdvisorDialogDeferred));
    }

    private void ReopenAdvisorDialogDeferred()
    {
        PopupDialogUsingSceneSize(_advisorDialog);
    }

    private void OnAdvisorSelectOfficerPressed()
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
            _localization.T("ui.advisor_assign_officer"),
            candidateOfficerIds,
            OfficerSelectorPrimaryStat.Politics,
            SelectAdvisorOfficerById);
    }

    private void SelectAdvisorOfficerById(int officerId)
    {
        _advisorSelectedOfficerId = officerId;
        UpdateAdvisorSummary();
    }
}
