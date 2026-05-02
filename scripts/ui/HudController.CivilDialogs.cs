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
        if (_civilCommandOption == null || _localization == null || _turnManager?.World == null || _selectedCity == null)
        {
            return;
        }

        _civilCommandOption.Clear();
        var world = _turnManager.World;
        var reliefUsed = _selectedCity.LastCivilReliefYear == world.Year && _selectedCity.LastCivilReliefMonth == world.Month;
        var investigateUsed = _selectedCity.LastSearchYear == world.Year && _selectedCity.LastSearchMonth == world.Month;

        AddCivilCommandOption("command.civil.relief", reliefUsed);
        AddCivilCommandOption("command.civil.investigate_people", investigateUsed);
        SelectFirstEnabledCivilCommandOption();
    }

    private void AddCivilCommandOption(string localeKey, bool disabled)
    {
        if (_civilCommandOption == null || _localization == null)
        {
            return;
        }

        var text = disabled
            ? _localization.Format("fmt.command_used_this_month", _localization.T(localeKey))
            : _localization.T(localeKey);
        _civilCommandOption.AddItem(text);
        var index = _civilCommandOption.ItemCount - 1;
        _civilCommandOption.SetItemMetadata(index, localeKey);
        _civilCommandOption.SetItemDisabled(index, disabled);
    }

    private void SelectFirstEnabledCivilCommandOption()
    {
        if (_civilCommandOption == null)
        {
            return;
        }

        for (var index = 0; index < _civilCommandOption.ItemCount; index += 1)
        {
            if (_civilCommandOption.IsItemDisabled(index))
            {
                continue;
            }

            _civilCommandOption.Select(index);
            return;
        }
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
        if (commandKey == "command.civil.relief")
        {
            ShowCivilReliefDialog();
            return;
        }

        if (commandKey == "command.civil.investigate_people")
        {
            ShowOfficerCommandDialog(CommandType.Search);
            return;
        }

        AddLog(_localization.Format("log.civil_command_selected", _civilCommandOption.GetItemText(_civilCommandOption.Selected)));
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
            _civilReliefOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerTable");
            _civilReliefGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
            _civilReliefFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
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

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _civilReliefOfficerList = new Tree
        {
            Name = "OfficerTable",
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0.0f, 188.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _civilReliefOfficerList.ItemSelected += OnCivilReliefOfficerTableSelected;
        root.AddChild(_civilReliefOfficerList);

        var goldRow = CreateCivilReliefFormRow("GoldRow", "GoldLabel");
        _civilReliefGoldSpinBox = CreateMoveSpinBox("GoldSpinBox");
        _civilReliefGoldSpinBox.Step = 100;
        _civilReliefGoldSpinBox.ValueChanged += _ =>
        {
            UpdateCivilReliefSummary();
            UpdateCivilReliefConfirmButtonState();
        };
        goldRow.AddChild(_civilReliefGoldSpinBox);
        root.AddChild(goldRow);

        var foodRow = CreateCivilReliefFormRow("FoodRow", "FoodLabel");
        _civilReliefFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        _civilReliefFoodSpinBox.Step = 1000;
        _civilReliefFoodSpinBox.ValueChanged += _ =>
        {
            UpdateCivilReliefSummary();
            UpdateCivilReliefConfirmButtonState();
        };
        foodRow.AddChild(_civilReliefFoodSpinBox);
        root.AddChild(foodRow);

        _civilReliefSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_civilReliefSummaryLabel);
    }

    private void ShowCivilReliefDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _civilReliefDialog == null || _localization == null)
        {
            return;
        }

        EnsureCivilReliefDialogWidgets();
        UpdateCivilReliefDialogText();
        PopulateCivilReliefDialog();
        _civilReliefDialog.PopupCentered(new Vector2I(480, 440));
    }

    private void PopulateCivilReliefDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return;
        }

        if (_civilReliefOfficerList != null)
        {
            _civilReliefOfficerList.Clear();
            ConfigureCivilReliefOfficerTableColumns();
            var tableRoot = _civilReliefOfficerList.CreateItem();
            var rowIndex = 0;
            var availableOfficerIds = GetAvailableOfficerIdsForOrder();
            foreach (var officerId in _selectedCity.OfficerIds)
            {
                if (!availableOfficerIds.Contains(officerId))
                {
                    continue;
                }

                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null)
                {
                    continue;
                }

                var row = _civilReliefOfficerList.CreateItem(tableRoot);
                PopulateCivilReliefOfficerTableRow(row, officer, rowIndex);
                rowIndex += 1;
            }
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
        UpdateCivilReliefConfirmButtonState();
    }

    private void UpdateCivilReliefDialogText()
    {
        if (_civilReliefDialog == null || _localization == null)
        {
            return;
        }

        _civilReliefDialog.Title = _localization.T("command.civil.relief");
        _civilReliefDialog.OkButtonText = _localization.T("ui.confirm_civil_relief");
        SetCivilReliefDialogLabelText("OfficerListLabel", _localization.T("ui.civil_relief_officer"));
        SetCivilReliefDialogLabelText("GoldLabel", _localization.T("ui.civil_relief_gold"));
        SetCivilReliefDialogLabelText("FoodLabel", _localization.T("ui.civil_relief_food"));
    }

    private void SetCivilReliefDialogLabelText(string nodeName, string text)
    {
        var label = _civilReliefDialog?.GetNodeOrNull<Label>($"CivilReliefDialogRoot/{nodeName}") ??
                    _civilReliefDialog?.GetNodeOrNull<Label>($"CivilReliefDialogRoot/GoldRow/{nodeName}") ??
                    _civilReliefDialog?.GetNodeOrNull<Label>($"CivilReliefDialogRoot/FoodRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private static HBoxContainer CreateCivilReliefFormRow(string rowName, string labelName)
    {
        var row = new HBoxContainer
        {
            Name = rowName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 10);

        var label = new Label
        {
            Name = labelName,
            CustomMinimumSize = new Vector2(84.0f, 0.0f),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.AddChild(label);
        return row;
    }

    private void ConfigureCivilReliefOfficerTableColumns()
    {
        if (_civilReliefOfficerList == null || _localization == null)
        {
            return;
        }

        _civilReliefOfficerList.Columns = 4;
        _civilReliefOfficerList.SetColumnTitle(0, _localization.T("ui.officers"));
        _civilReliefOfficerList.SetColumnCustomMinimumWidth(0, 130);
        _civilReliefOfficerList.SetColumnTitle(1, _localization.T("ui.role"));
        _civilReliefOfficerList.SetColumnCustomMinimumWidth(1, 100);
        _civilReliefOfficerList.SetColumnTitle(2, _localization.T("ui.status"));
        _civilReliefOfficerList.SetColumnCustomMinimumWidth(2, 100);
        _civilReliefOfficerList.SetColumnTitle(3, _localization.T("ui.charm"));
        _civilReliefOfficerList.SetColumnCustomMinimumWidth(3, 80);
    }

    private void PopulateCivilReliefOfficerTableRow(TreeItem row, OfficerData officer, int rowIndex)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return;
        }

        row.SetMetadata(0, officer.Id);
        row.SetText(0, _localization.GetOfficerName(officer));
        row.SetText(1, _localization.GetOfficerRole(officer));
        row.SetText(2, _localization.GetOfficerStatus(_turnManager.World, officer));
        row.SetText(3, officer.Charm.ToString());
        ApplyViewTableRowStriping(row, rowIndex, 4);
    }

    private void OnCivilReliefOfficerTableSelected()
    {
        if (_civilReliefOfficerList == null)
        {
            return;
        }

        var selectedItem = _civilReliefOfficerList.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        var root = _civilReliefOfficerList.GetRoot();
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
                ApplyViewTableSelectedRowStyle(row, _civilReliefOfficerList.Columns);
            }
            else
            {
                ApplyViewTableRowStriping(row, rowIndex, _civilReliefOfficerList.Columns);
            }

            row = row.GetNext();
            rowIndex += 1;
        }

        UpdateCivilReliefConfirmButtonState();
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

    private void UpdateCivilReliefConfirmButtonState()
    {
        var okButton = _civilReliefDialog?.GetOkButton();
        if (okButton == null)
        {
            return;
        }

        var hasOfficer = GetSelectedTreeMetadataIds(_civilReliefOfficerList).Count > 0;
        var gold = (int)(_civilReliefGoldSpinBox?.Value ?? 0);
        var food = (int)(_civilReliefFoodSpinBox?.Value ?? 0);
        var hasReliefAmount = gold > 0 || food > 0;
        var effectiveGain = gold / 100 * 10 + food / 1000 * 10;
        okButton.Disabled = !hasOfficer || !hasReliefAmount || effectiveGain <= 0;
    }

    private void OnCivilReliefDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedTreeMetadataIds(_civilReliefOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenCivilReliefDialog();
            return;
        }

        var result = _commandResolver.ExecuteCivilRelief(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            selectedOfficerIds[0],
            (int)(_civilReliefGoldSpinBox?.Value ?? 0),
            (int)(_civilReliefFoodSpinBox?.Value ?? 0));
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }

    private void ReopenCivilReliefDialog()
    {
        CallDeferred(nameof(ReopenCivilReliefDialogDeferred));
    }

    private void ReopenCivilReliefDialogDeferred()
    {
        _civilReliefDialog?.PopupCentered(new Vector2I(480, 440));
    }


}
