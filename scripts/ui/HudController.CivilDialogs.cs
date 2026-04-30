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
    private void EnsureCivilDialogWidgets()
    {
        if (_civilDialog == null)
        {
            return;
        }

        var existingRoot = _civilDialog.GetNodeOrNull<VBoxContainer>("CivilDialogRoot");
        if (existingRoot != null)
        {
            _civilCommandOption = existingRoot.GetNodeOrNull<OptionButton>("CommandOption");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "CivilDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 120.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _civilDialog.AddChild(root);
        root.AddChild(new Label { Name = "CommandLabel" });
        _civilCommandOption = new OptionButton
        {
            Name = "CommandOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_civilCommandOption);
    }

    private void ShowCivilDialog()
    {
        if (_civilDialog == null || _localization == null)
        {
            return;
        }

        EnsureCivilDialogWidgets();
        UpdateCivilDialogText();
        PopulateCivilDialog();
        _civilDialog.PopupCentered(new Vector2I(440, 160));
    }

    private void PopulateCivilDialog()
    {
        if (_civilCommandOption == null || _localization == null)
        {
            return;
        }

        _civilCommandOption.Clear();
        AddCivilCommandOption("civil.command.relief");
        AddCivilCommandOption("civil.command.investigate_people");
    }

    private void AddCivilCommandOption(string localeKey)
    {
        if (_civilCommandOption == null || _localization == null)
        {
            return;
        }

        _civilCommandOption.AddItem(_localization.T(localeKey));
        _civilCommandOption.SetItemMetadata(_civilCommandOption.ItemCount - 1, localeKey);
    }

    private void UpdateCivilDialogText()
    {
        if (_civilDialog == null || _localization == null)
        {
            return;
        }

        _civilDialog.Title = _localization.T("ui.civil");
        _civilDialog.OkButtonText = _localization.T("ui.confirm_civil");
        var label = _civilDialog.GetNodeOrNull<Label>("CivilDialogRoot/CommandLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.civil_command");
        }
    }

    private void OnCivilDialogConfirmed()
    {
        if (_localization == null || _civilCommandOption == null)
        {
            return;
        }

        var metadata = _civilCommandOption.GetItemMetadata(_civilCommandOption.Selected);
        var commandKey = metadata.VariantType == Variant.Type.String ? metadata.AsString() : string.Empty;
        if (commandKey == "civil.command.relief")
        {
            ShowCivilReliefDialog();
            return;
        }

        if (commandKey == "civil.command.investigate_people")
        {
            ExecuteCivilInvestigation();
            return;
        }

        AddLog(_localization.Format("log.civil_command_selected", _civilCommandOption.GetItemText(_civilCommandOption.Selected)));
    }

    private void ExecuteCivilInvestigation()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var result = _commandResolver.ExecuteCivilInvestigation(_turnManager.GetPlayerFactionId(), _selectedCity.Id);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }

    private void EnsureCivilReliefDialogWidgets()
    {
        if (_civilReliefDialog == null)
        {
            return;
        }

        var existingRoot = _civilReliefDialog.GetNodeOrNull<VBoxContainer>("CivilReliefDialogRoot");
        if (existingRoot != null)
        {
            _civilReliefGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldSpinBox");
            _civilReliefFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodSpinBox");
            _civilReliefSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "CivilReliefDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 230.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _civilReliefDialog.AddChild(root);

        root.AddChild(new Label { Name = "GoldLabel" });
        _civilReliefGoldSpinBox = CreateMoveSpinBox("GoldSpinBox");
        _civilReliefGoldSpinBox.Step = 100;
        _civilReliefGoldSpinBox.ValueChanged += _ => UpdateCivilReliefSummary();
        root.AddChild(_civilReliefGoldSpinBox);

        root.AddChild(new Label { Name = "FoodLabel" });
        _civilReliefFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        _civilReliefFoodSpinBox.Step = 1000;
        _civilReliefFoodSpinBox.ValueChanged += _ => UpdateCivilReliefSummary();
        root.AddChild(_civilReliefFoodSpinBox);

        _civilReliefSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_civilReliefSummaryLabel);
    }

    private void ShowCivilReliefDialog()
    {
        if (_selectedCity == null || _civilReliefDialog == null || _localization == null)
        {
            return;
        }

        EnsureCivilReliefDialogWidgets();
        UpdateCivilReliefDialogText();
        PopulateCivilReliefDialog();
        _civilReliefDialog.PopupCentered(new Vector2I(440, 260));
    }

    private void PopulateCivilReliefDialog()
    {
        if (_selectedCity == null)
        {
            return;
        }

        ConfigureMoveSpinBox(_civilReliefGoldSpinBox, _selectedCity.Gold, 0);
        ConfigureMoveSpinBox(_civilReliefFoodSpinBox, _selectedCity.Food, 0);
        if (_civilReliefGoldSpinBox != null)
        {
            _civilReliefGoldSpinBox.Step = 100;
        }
        if (_civilReliefFoodSpinBox != null)
        {
            _civilReliefFoodSpinBox.Step = 1000;
        }

        UpdateCivilReliefSummary();
    }

    private void UpdateCivilReliefDialogText()
    {
        if (_civilReliefDialog == null || _localization == null)
        {
            return;
        }

        _civilReliefDialog.Title = _localization.T("civil.command.relief");
        _civilReliefDialog.OkButtonText = _localization.T("ui.confirm_civil_relief");
        SetCivilReliefDialogLabelText("GoldLabel", _localization.T("ui.civil_relief_gold"));
        SetCivilReliefDialogLabelText("FoodLabel", _localization.T("ui.civil_relief_food"));
    }

    private void SetCivilReliefDialogLabelText(string nodeName, string text)
    {
        var label = _civilReliefDialog?.GetNodeOrNull<Label>($"CivilReliefDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void UpdateCivilReliefSummary()
    {
        if (_civilReliefSummaryLabel == null || _civilReliefGoldSpinBox == null || _civilReliefFoodSpinBox == null || _localization == null)
        {
            return;
        }

        var gold = (int)_civilReliefGoldSpinBox.Value;
        var food = (int)_civilReliefFoodSpinBox.Value;
        var gain = gold / 100 * 10 + food / 1000 * 10;
        _civilReliefSummaryLabel.Text = _localization.Format("fmt.civil_relief_preview", gain);
    }

    private void OnCivilReliefDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var result = _commandResolver.ExecuteCivilRelief(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            (int)(_civilReliefGoldSpinBox?.Value ?? 0),
            (int)(_civilReliefFoodSpinBox?.Value ?? 0));
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }


}
