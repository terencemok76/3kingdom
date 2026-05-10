using System.Collections.Generic;
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
            _advisorOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerTable");
            _advisorPositionOption = existingRoot.GetNodeOrNull<OptionButton>("PositionOption");
            _advisorSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
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
        _advisorOfficerList = new Tree
        {
            Name = "OfficerTable",
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0.0f, 170.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _advisorOfficerList.ItemSelected += OnAdvisorOfficerTableSelected;
        root.AddChild(_advisorOfficerList);

        root.AddChild(new Label { Name = "PositionLabel" });
        _advisorPositionOption = new OptionButton
        {
            Name = "PositionOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _advisorPositionOption.ItemSelected += _ => UpdateAdvisorSummary();
        root.AddChild(_advisorPositionOption);

        _advisorSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_advisorSummaryLabel);
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
        _advisorDialog.PopupCentered(new Vector2I(500, 420));
    }

    private void PopulateAdvisorDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return;
        }

        if (_advisorOfficerList != null)
        {
            _advisorOfficerList.Clear();
            ConfigureCompactOfficerTableColumns(_advisorOfficerList, includeStatus: true, includeCity: false, includeLoyalty: true, includeStats: true);
            var tableRoot = _advisorOfficerList.CreateItem();
            var rowIndex = 0;
            foreach (var officerId in _selectedCity.OfficerIds)
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null || IsFactionRuler(_turnManager.World, officer))
                {
                    continue;
                }

                var row = _advisorOfficerList.CreateItem(tableRoot);
                PopulateCompactOfficerTableRow(row, officer, rowIndex, includeStatus: true, includeCity: false, includeLoyalty: true, includeStats: true);
                rowIndex += 1;
            }
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
        _advisorDialog.OkButtonText = _localization.T("ui.confirm_advisor_assign");
        SetAdvisorDialogLabelText("OfficerListLabel", _localization.T("ui.advisor_assign_officer"));
        SetAdvisorDialogLabelText("PositionLabel", _localization.T("ui.advisor_position"));
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

    private void OnAdvisorOfficerTableSelected()
    {
        if (_advisorOfficerList == null)
        {
            return;
        }

        var selectedItem = _advisorOfficerList.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        var root = _advisorOfficerList.GetRoot();
        if (root == null)
        {
            return;
        }

        var row = root.GetFirstChild();
        var rowIndex = 0;
        while (row != null)
        {
            if (row == selectedItem)
            {
                ApplyViewTableSelectedRowStyle(row, _advisorOfficerList.Columns);
            }
            else
            {
                ApplyViewTableRowStriping(row, rowIndex, _advisorOfficerList.Columns);
            }

            row = row.GetNext();
            rowIndex += 1;
        }

        UpdateAdvisorSummary();
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
        var selectedOfficerIds = GetSelectedTreeMetadataIds(_advisorOfficerList);
        var selectedOfficer = selectedOfficerIds.Count > 0 ? _turnManager.World.GetOfficer(selectedOfficerIds[0]) : null;
        var selectedOfficerName = selectedOfficer != null ? _localization.GetOfficerName(selectedOfficer) : _localization.T("ui.none");
        var positionMetadata = _advisorPositionOption?.GetItemMetadata(_advisorPositionOption.Selected);
        var position = positionMetadata?.VariantType == Variant.Type.String ? positionMetadata.Value.AsString() : "Chancellor";
        var positionName = position == "Chancellor" ? _localization.T("ui.chancellor") : _localization.T("ui.chief_strategist");

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

        var selectedOfficerIds = GetSelectedTreeMetadataIds(_advisorOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenAdvisorDialog();
            return;
        }

        var positionMetadata = _advisorPositionOption?.GetItemMetadata(_advisorPositionOption.Selected);
        var position = positionMetadata?.VariantType == Variant.Type.String ? positionMetadata.Value.AsString() : "Chancellor";
        var result = _commandResolver.ExecuteAssignFactionAdvisor(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            selectedOfficerIds[0],
            position);
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        RefreshSelectedCity();
    }

    private void ReopenAdvisorDialog()
    {
        CallDeferred(nameof(ReopenAdvisorDialogDeferred));
    }

    private void ReopenAdvisorDialogDeferred()
    {
        _advisorDialog?.PopupCentered(new Vector2I(500, 420));
    }
}
