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
        _militaryDialog.PopupCentered(new Vector2I(340, 150));
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
        _militaryDialog.OkButtonText = _localization.T("ui.confirm_military");
        var label = _militaryDialog.GetNodeOrNull<Label>("MilitaryDialogRoot/CommandLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.military_command");
        }
    }

    private void OnMilitaryDialogConfirmed()
    {
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

        ShowOfficerCommandDialog(CommandType.Recruit);
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


}
