using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;

namespace ThreeKingdom.UI;

public partial class HudController : CanvasLayer
{
    private void EnsureMilitaryDialogWidgets()
    {
        if (_militaryDialog == null)
        {
            return;
        }

        var existingRoot = _militaryDialog.GetNodeOrNull<VBoxContainer>("MilitaryDialogRoot");
        if (existingRoot != null)
        {
            _militaryCommandOption = existingRoot.GetNodeOrNull<OptionButton>("CommandOption");
            _militaryConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
            if (!_militaryDialogSignalsConnected && _militaryConfirmButton != null)
            {
                _militaryConfirmButton.Pressed += OnMilitaryDialogConfirmed;
                _militaryDialogSignalsConnected = true;
            }
            return;
        }

        var root = new VBoxContainer
        {
            Name = "MilitaryDialogRoot",
            CustomMinimumSize = new Vector2(320.0f, 110.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _militaryDialog.AddChild(root);

        root.AddChild(new Label { Name = "CommandLabel" });
        _militaryCommandOption = new OptionButton
        {
            Name = "CommandOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_militaryCommandOption);

        var confirmRow = new HBoxContainer
        {
            Name = "ConfirmRow",
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _militaryConfirmButton = new Button
        {
            Name = "ConfirmButton",
            FocusMode = Control.FocusModeEnum.None
        };
        _militaryConfirmButton.Pressed += OnMilitaryDialogConfirmed;
        _militaryDialogSignalsConnected = true;
        confirmRow.AddChild(_militaryConfirmButton);
        root.AddChild(confirmRow);
    }

    private void ShowMilitaryDialog()
    {
        if (_selectedCity == null || _militaryDialog == null || _localization == null)
        {
            return;
        }

        EnsureMilitaryDialogWidgets();
        UpdateMilitaryDialogText();
        PopulateMilitaryDialog();
        PopupDialogUsingSceneSize(_militaryDialog);
    }

    private void PopulateMilitaryDialog()
    {
        if (_militaryCommandOption == null || _localization == null)
        {
            return;
        }

        _militaryCommandOption.Clear();
        _militaryCommandOption.AddItem(_localization.T("ui.military_recruit"));
        _militaryCommandOption.SetItemMetadata(_militaryCommandOption.ItemCount - 1, (int)CommandType.Recruit);
        _militaryCommandOption.AddItem(_localization.T("ui.military_move"));
        _militaryCommandOption.SetItemMetadata(_militaryCommandOption.ItemCount - 1, (int)CommandType.Move);
        _militaryCommandOption.AddItem(_localization.T("ui.military_attack"));
        _militaryCommandOption.SetItemMetadata(_militaryCommandOption.ItemCount - 1, (int)CommandType.Attack);
    }

    private void UpdateMilitaryDialogText()
    {
        if (_militaryDialog == null || _localization == null)
        {
            return;
        }

        _militaryDialog.Title = _localization.T("ui.military");
        var label = _militaryDialog.GetNodeOrNull<Label>("MilitaryDialogRoot/CommandLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.military_command");
        }

        if (_militaryConfirmButton != null)
        {
            _militaryConfirmButton.Text = _localization.T("ui.confirm_military");
        }
    }

    private void OnMilitaryDialogConfirmed()
    {
        _militaryDialog?.Hide();
        var commandType = GetSelectedMilitaryCommandType();
        if (commandType == CommandType.Attack)
        {
            OpenAttackFlow();
            return;
        }

        if (commandType == CommandType.Move)
        {
            OpenMoveFlow();
            return;
        }

        ShowRecruitTroopDialog();
    }

    private CommandType GetSelectedMilitaryCommandType()
    {
        if (_militaryCommandOption == null)
        {
            return CommandType.Recruit;
        }

        var metadata = _militaryCommandOption.GetItemMetadata(_militaryCommandOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (CommandType)metadata.AsInt32()
            : CommandType.Recruit;
    }

    private void EnsureRecruitTroopDialogWidgets()
    {
        if (_recruitTroopDialog == null)
        {
            return;
        }

        var existingRoot = _recruitTroopDialog.GetNodeOrNull<VBoxContainer>("RecruitTroopDialogRoot");
        if (existingRoot != null)
        {
            _recruitTroopSelectedOfficerLabel = existingRoot.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
            _recruitTroopSelectOfficerButton = existingRoot.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
            _recruitTroopTypeOption = existingRoot.GetNodeOrNull<OptionButton>("TroopTypeOption");
            _recruitTroopConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
            if (!_recruitTroopDialogSignalsConnected)
            {
                if (_recruitTroopSelectOfficerButton != null)
                {
                    _recruitTroopSelectOfficerButton.Pressed += OnRecruitTroopSelectOfficerPressed;
                }

                if (_recruitTroopConfirmButton != null)
                {
                    _recruitTroopConfirmButton.Pressed += OnRecruitTroopDialogConfirmed;
                }

                _recruitTroopDialogSignalsConnected = true;
            }
            return;
        }

        var root = new VBoxContainer
        {
            Name = "RecruitTroopDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 160.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _recruitTroopDialog.AddChild(root);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        var officerSelectorRow = new HBoxContainer
        {
            Name = "OfficerSelectorRow",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        officerSelectorRow.AddThemeConstantOverride("separation", 8);
        _recruitTroopSelectedOfficerLabel = new Label
        {
            Name = "SelectedOfficerLabel",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        officerSelectorRow.AddChild(_recruitTroopSelectedOfficerLabel);
        _recruitTroopSelectOfficerButton = new Button
        {
            Name = "SelectOfficerButton",
            FocusMode = Control.FocusModeEnum.None
        };
        _recruitTroopSelectOfficerButton.Pressed += OnRecruitTroopSelectOfficerPressed;
        _recruitTroopDialogSignalsConnected = true;
        officerSelectorRow.AddChild(_recruitTroopSelectOfficerButton);
        root.AddChild(officerSelectorRow);

        root.AddChild(new Label { Name = "TroopTypeLabel" });
        _recruitTroopTypeOption = new OptionButton
        {
            Name = "TroopTypeOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_recruitTroopTypeOption);

        var confirmRow = new HBoxContainer
        {
            Name = "ConfirmRow",
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _recruitTroopConfirmButton = new Button
        {
            Name = "ConfirmButton",
            FocusMode = Control.FocusModeEnum.None
        };
        _recruitTroopConfirmButton.Pressed += OnRecruitTroopDialogConfirmed;
        confirmRow.AddChild(_recruitTroopConfirmButton);
        root.AddChild(confirmRow);
    }

    private void ShowRecruitTroopDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _recruitTroopDialog == null || _localization == null)
        {
            return;
        }

        EnsureRecruitTroopDialogWidgets();
        UpdateRecruitTroopDialogText();
        PopulateRecruitTroopDialog();
        PopupDialogUsingSceneSize(_recruitTroopDialog);
    }

    private void PopulateRecruitTroopDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return;
        }

        var candidateOfficerIds = _selectedCity.OfficerIds
            .Where(GetAvailableOfficerIdsForOrder().Contains)
            .ToList();
        if (!candidateOfficerIds.Contains(_recruitTroopSelectedOfficerId))
        {
            _recruitTroopSelectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        if (_recruitTroopTypeOption != null)
        {
            _recruitTroopTypeOption.Clear();
            foreach (var troopType in new[]
                     {
                         TroopType.Infantry,
                         TroopType.Spearman,
                         TroopType.Cavalry,
                         TroopType.Archer,
                         TroopType.Crossbow,
                         TroopType.Siege
                     })
            {
                _recruitTroopTypeOption.AddItem(GetTroopTypeDisplayName(troopType));
                _recruitTroopTypeOption.SetItemMetadata(_recruitTroopTypeOption.ItemCount - 1, (int)troopType);
            }

            _recruitTroopTypeOption.Select(0);
        }

        UpdateRecruitTroopSelectedOfficerSummary();
    }

    private void UpdateRecruitTroopDialogText()
    {
        if (_recruitTroopDialog == null || _localization == null)
        {
            return;
        }

        _recruitTroopDialog.Title = _localization.T("ui.military_recruit");
        var officerLabel = _recruitTroopDialog.GetNodeOrNull<Label>("RecruitTroopDialogRoot/OfficerListLabel");
        if (officerLabel != null)
        {
            officerLabel.Text = _localization.T("ui.officers");
        }

        var troopTypeLabel = _recruitTroopDialog.GetNodeOrNull<Label>("RecruitTroopDialogRoot/TroopTypeLabel");
        if (troopTypeLabel != null)
        {
            troopTypeLabel.Text = _localization.T("ui.recruit_troop_type");
        }

        if (_recruitTroopSelectOfficerButton != null)
        {
            _recruitTroopSelectOfficerButton.Text = _localization.T("ui.select_officer");
        }

        if (_recruitTroopConfirmButton != null)
        {
            _recruitTroopConfirmButton.Text = _localization.T("ui.confirm_officer_selection");
        }

        UpdateRecruitTroopSelectedOfficerSummary();
    }

    private void OnRecruitTroopSelectOfficerPressed()
    {
        if (_selectedCity == null || _localization == null)
        {
            return;
        }

        var candidateOfficerIds = _selectedCity.OfficerIds
            .Where(GetAvailableOfficerIdsForOrder().Contains)
            .ToList();
        if (candidateOfficerIds.Count == 0)
        {
            AddLog(_localization.Format("ui.no_available_officer_for_command", GetCommandName(CommandType.Recruit)));
            return;
        }

        ShowOfficerSelectorDialog(
            _localization.T("ui.military_recruit"),
            candidateOfficerIds,
            OfficerSelectorPrimaryStat.Strength,
            SelectRecruitTroopOfficerById);
    }

    private void SelectRecruitTroopOfficerById(int officerId)
    {
        _recruitTroopSelectedOfficerId = officerId;
        UpdateRecruitTroopSelectedOfficerSummary();
    }

    private void UpdateRecruitTroopSelectedOfficerSummary()
    {
        if (_recruitTroopSelectedOfficerLabel == null || _localization == null)
        {
            return;
        }

        var officer = _recruitTroopSelectedOfficerId > 0 ? _turnManager?.World?.GetOfficer(_recruitTroopSelectedOfficerId) : null;
        var officerName = officer != null ? _localization.GetOfficerName(officer) : _localization.T("ui.unassigned");
        _recruitTroopSelectedOfficerLabel.Text = $"{_localization.T("ui.officers")}: {officerName}";
    }

    private void OnRecruitTroopDialogConfirmed()
    {
        if (_localization == null)
        {
            return;
        }

        if (_recruitTroopSelectedOfficerId <= 0)
        {
            AddLog(_localization.T("ui.select_officer_warning"));
            ReopenRecruitTroopDialog();
            return;
        }

        var result = ExecutePlayerCommand(
            CommandType.Recruit,
            officerIds: new List<int> { _recruitTroopSelectedOfficerId },
            recruitTroopType: GetSelectedRecruitTroopType());
        if (result.Success)
        {
            _recruitTroopDialog?.Hide();
        }
    }

    private TroopType GetSelectedRecruitTroopType()
    {
        if (_recruitTroopTypeOption == null)
        {
            return TroopType.Infantry;
        }

        var metadata = _recruitTroopTypeOption.GetItemMetadata(_recruitTroopTypeOption.Selected);
        return metadata.VariantType == Variant.Type.Int ? (TroopType)metadata.AsInt32() : TroopType.Infantry;
    }

    private void ReopenRecruitTroopDialog()
    {
        CallDeferred(nameof(ReopenRecruitTroopDialogDeferred));
    }

    private void ReopenRecruitTroopDialogDeferred()
    {
        PopupDialogUsingSceneSize(_recruitTroopDialog);
    }


}
