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
    private void EnsurePersonnelDialogWidgets()
    {
        if (_personnelDialog == null)
        {
            return;
        }

        var existingRoot = _personnelDialog.GetNodeOrNull<VBoxContainer>("PersonnelDialogRoot");
        if (existingRoot != null)
        {
            _personnelCommandOption = existingRoot.GetNodeOrNull<OptionButton>("CommandOption");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "PersonnelDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 130.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _personnelDialog.AddChild(root);
        root.AddChild(new Label { Name = "CommandLabel" });
        _personnelCommandOption = new OptionButton
        {
            Name = "CommandOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_personnelCommandOption);
    }

    private void ShowPersonnelDialog()
    {
        if (_personnelDialog == null || _localization == null)
        {
            return;
        }

        EnsurePersonnelDialogWidgets();
        UpdatePersonnelDialogText();
        PopulatePersonnelDialog();
        _personnelDialog.PopupCentered(new Vector2I(440, 170));
    }

    private void PopulatePersonnelDialog()
    {
        if (_personnelCommandOption == null || _localization == null)
        {
            return;
        }

        _personnelCommandOption.Clear();
        AddPersonnelCommandOption("command.personnel.give_bonus");
        AddPersonnelCommandOption("command.personnel.assign_title");
        AddPersonnelCommandOption("command.personnel.request_item");
        AddPersonnelCommandOption("command.personnel.hire_officer");
    }

    private void AddPersonnelCommandOption(string localeKey)
    {
        if (_personnelCommandOption == null || _localization == null)
        {
            return;
        }

        _personnelCommandOption.AddItem(_localization.T(localeKey));
        _personnelCommandOption.SetItemMetadata(_personnelCommandOption.ItemCount - 1, localeKey);
    }

    private void UpdatePersonnelDialogText()
    {
        if (_personnelDialog == null || _localization == null)
        {
            return;
        }

        _personnelDialog.Title = _localization.T("ui.personnel");
        _personnelDialog.OkButtonText = _localization.T("ui.confirm_personnel");
        var label = _personnelDialog.GetNodeOrNull<Label>("PersonnelDialogRoot/CommandLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.personnel_command");
        }
    }

    private void OnPersonnelDialogConfirmed()
    {
        if (_localization == null || _personnelCommandOption == null)
        {
            return;
        }

        var metadata = _personnelCommandOption.GetItemMetadata(_personnelCommandOption.Selected);
        var commandKey = metadata.VariantType == Variant.Type.String ? metadata.AsString() : string.Empty;
        if (commandKey == "command.personnel.give_bonus")
        {
            ShowPersonnelBonusDialog();
            return;
        }

        if (commandKey == "command.personnel.assign_title")
        {
            ShowAssignRoleDialog();
            return;
        }

        if (commandKey == "command.personnel.request_item")
        {
            ShowRequestItemDialog();
            return;
        }

        if (commandKey == "command.personnel.hire_officer")
        {
            ShowHireOfficerDialog();
            return;
        }

        AddLog(_localization.Format("log.personnel_command_selected", _personnelCommandOption.GetItemText(_personnelCommandOption.Selected)));
    }

    private void EnsurePersonnelBonusDialogWidgets()
    {
        if (_personnelBonusDialog == null)
        {
            return;
        }

        var existingRoot = _personnelBonusDialog.GetNodeOrNull<VBoxContainer>("PersonnelBonusDialogRoot");
        if (existingRoot != null)
        {
            _personnelBonusOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerTable");
            _personnelBonusGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
            _personnelBonusFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
            _personnelBonusItemOption = existingRoot.GetNodeOrNull<OptionButton>("ItemRow/ItemOption");
            _personnelBonusSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "PersonnelBonusDialogRoot",
            CustomMinimumSize = new Vector2(460.0f, 360.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _personnelBonusDialog.AddChild(root);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _personnelBonusOfficerList = new Tree
        {
            Name = "OfficerTable",
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0.0f, 150.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _personnelBonusOfficerList.ItemSelected += OnPersonnelBonusOfficerTableSelected;
        root.AddChild(_personnelBonusOfficerList);

        var goldRow = CreatePersonnelBonusFormRow("GoldRow", "GoldLabel");
        _personnelBonusGoldSpinBox = CreateMoveSpinBox("GoldSpinBox");
        _personnelBonusGoldSpinBox.Step = 100;
        _personnelBonusGoldSpinBox.ValueChanged += _ => UpdatePersonnelBonusSummary();
        goldRow.AddChild(_personnelBonusGoldSpinBox);
        root.AddChild(goldRow);

        var foodRow = CreatePersonnelBonusFormRow("FoodRow", "FoodLabel");
        _personnelBonusFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        _personnelBonusFoodSpinBox.Step = 500;
        _personnelBonusFoodSpinBox.ValueChanged += _ => UpdatePersonnelBonusSummary();
        foodRow.AddChild(_personnelBonusFoodSpinBox);
        root.AddChild(foodRow);

        var itemRow = CreatePersonnelBonusFormRow("ItemRow", "ItemLabel");
        _personnelBonusItemOption = new OptionButton
        {
            Name = "ItemOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _personnelBonusItemOption.ItemSelected += _ => UpdatePersonnelBonusSummary();
        itemRow.AddChild(_personnelBonusItemOption);
        root.AddChild(itemRow);

        _personnelBonusSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_personnelBonusSummaryLabel);
    }

    private void ShowPersonnelBonusDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _personnelBonusDialog == null || _localization == null)
        {
            return;
        }

        EnsurePersonnelBonusDialogWidgets();
        UpdatePersonnelBonusDialogText();
        PopulatePersonnelBonusDialog();
        _personnelBonusDialog.PopupCentered(new Vector2I(480, 400));
    }

    private void PopulatePersonnelBonusDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _personnelBonusOfficerList == null)
        {
            return;
        }

        _personnelBonusOfficerList.Clear();
        ConfigureCompactOfficerTableColumns(_personnelBonusOfficerList);
        var tableRoot = _personnelBonusOfficerList.CreateItem();
        var rowIndex = 0;
        foreach (var officerId in _selectedCity.OfficerIds)
        {
            var officer = _turnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (IsFactionRuler(_turnManager.World, officer))
            {
                continue;
            }

            var row = _personnelBonusOfficerList.CreateItem(tableRoot);
            PopulateCompactOfficerTableRow(row, officer, rowIndex);
            rowIndex += 1;
        }

        ConfigureMoveSpinBox(_personnelBonusGoldSpinBox, _selectedCity.Gold, 0);
        ConfigureMoveSpinBox(_personnelBonusFoodSpinBox, _selectedCity.Food, 0);
        if (_personnelBonusGoldSpinBox != null)
        {
            _personnelBonusGoldSpinBox.Step = 100;
        }
        if (_personnelBonusFoodSpinBox != null)
        {
            _personnelBonusFoodSpinBox.Step = 500;
        }

        PopulateFactionInventoryOption(_personnelBonusItemOption);
        UpdatePersonnelBonusSummary();
    }

    private void OnPersonnelBonusOfficerTableSelected()
    {
        if (_personnelBonusOfficerList == null)
        {
            return;
        }

        var selectedItem = _personnelBonusOfficerList.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        var root = _personnelBonusOfficerList.GetRoot();
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
                ApplyViewTableSelectedRowStyle(row, _personnelBonusOfficerList.Columns);
            }
            else
            {
                ApplyViewTableRowStriping(row, rowIndex, _personnelBonusOfficerList.Columns);
            }

            row = row.GetNext();
            rowIndex += 1;
        }
    }

    private void UpdatePersonnelBonusDialogText()
    {
        if (_personnelBonusDialog == null || _localization == null)
        {
            return;
        }

        _personnelBonusDialog.Title = _localization.T("command.personnel.give_bonus");
        _personnelBonusDialog.OkButtonText = _localization.T("ui.confirm_personnel_bonus");
        SetPersonnelBonusDialogLabelText("OfficerListLabel", _localization.T("ui.personnel_bonus_officer"));
        SetPersonnelBonusDialogLabelText("GoldLabel", _localization.T("ui.personnel_bonus_gold"));
        SetPersonnelBonusDialogLabelText("FoodLabel", _localization.T("ui.personnel_bonus_food"));
        SetPersonnelBonusDialogLabelText("ItemLabel", _localization.T("ui.personnel_bonus_item"));
    }

    private void SetPersonnelBonusDialogLabelText(string nodeName, string text)
    {
        var label = _personnelBonusDialog?.GetNodeOrNull<Label>($"PersonnelBonusDialogRoot/{nodeName}") ??
                    _personnelBonusDialog?.GetNodeOrNull<Label>($"PersonnelBonusDialogRoot/GoldRow/{nodeName}") ??
                    _personnelBonusDialog?.GetNodeOrNull<Label>($"PersonnelBonusDialogRoot/FoodRow/{nodeName}") ??
                    _personnelBonusDialog?.GetNodeOrNull<Label>($"PersonnelBonusDialogRoot/ItemRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private static HBoxContainer CreatePersonnelBonusFormRow(string rowName, string labelName)
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

    private void UpdatePersonnelBonusSummary()
    {
        if (_personnelBonusSummaryLabel == null || _personnelBonusGoldSpinBox == null || _personnelBonusFoodSpinBox == null || _localization == null)
        {
            return;
        }

        var gold = (int)_personnelBonusGoldSpinBox.Value;
        var food = (int)_personnelBonusFoodSpinBox.Value;
        var gain = gold / 100 + food / 500;
        var item = GetSelectedItemFromOption(_personnelBonusItemOption);
        _personnelBonusSummaryLabel.Text = item == null
            ? _localization.Format("fmt.personnel_bonus_preview", gain)
            : _localization.Format("fmt.personnel_bonus_preview_with_item", gain + Math.Max(1, item.LoyaltyBonus), _localization.GetItemName(item));
    }

    private void OnPersonnelBonusDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedTreeMetadataIds(_personnelBonusOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenPersonnelBonusDialog();
            return;
        }

        var result = _commandResolver.ExecutePersonnelBonus(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            selectedOfficerIds[0],
            (int)(_personnelBonusGoldSpinBox?.Value ?? 0),
            (int)(_personnelBonusFoodSpinBox?.Value ?? 0),
            GetSelectedItemFromOption(_personnelBonusItemOption)?.Id ?? 0);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }

    private void ReopenPersonnelBonusDialog()
    {
        CallDeferred(nameof(ReopenPersonnelBonusDialogDeferred));
    }

    private void ReopenPersonnelBonusDialogDeferred()
    {
        _personnelBonusDialog?.PopupCentered(new Vector2I(480, 400));
    }

    private void EnsureAssignRoleDialogWidgets()
    {
        if (_assignRoleDialog == null)
        {
            return;
        }

        var existingRoot = _assignRoleDialog.GetNodeOrNull<VBoxContainer>("AssignRoleDialogRoot");
        if (existingRoot != null)
        {
            _assignRoleOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerTable");
            _assignRoleOption = existingRoot.GetNodeOrNull<OptionButton>("RoleOption");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "AssignRoleDialogRoot",
            CustomMinimumSize = new Vector2(460.0f, 330.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _assignRoleDialog.AddChild(root);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _assignRoleOfficerList = new Tree
        {
            Name = "OfficerTable",
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0.0f, 160.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _assignRoleOfficerList.ItemSelected += OnAssignRoleOfficerTableSelected;
        root.AddChild(_assignRoleOfficerList);

        root.AddChild(new Label { Name = "RoleLabel" });
        _assignRoleOption = new OptionButton
        {
            Name = "RoleOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_assignRoleOption);
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
        _assignRoleDialog.PopupCentered(new Vector2I(480, 370));
    }

    private void PopulateAssignRoleDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return;
        }

        if (_assignRoleOfficerList != null)
        {
            _assignRoleOfficerList.Clear();
            ConfigureCompactOfficerTableColumns(_assignRoleOfficerList, includeStatus: true, includeCity: false, includeLoyalty: false, includeStats: true);
            var tableRoot = _assignRoleOfficerList.CreateItem();
            var rowIndex = 0;
            foreach (var officerId in _selectedCity.OfficerIds)
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null || IsFactionRuler(_turnManager.World, officer))
                {
                    continue;
                }

                var row = _assignRoleOfficerList.CreateItem(tableRoot);
                PopulateCompactOfficerTableRow(row, officer, rowIndex, includeStatus: true, includeCity: false, includeLoyalty: false, includeStats: true);
                rowIndex += 1;
            }
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

    private void OnAssignRoleOfficerTableSelected()
    {
        if (_assignRoleOfficerList == null)
        {
            return;
        }

        var selectedItem = _assignRoleOfficerList.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        var root = _assignRoleOfficerList.GetRoot();
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
                ApplyViewTableSelectedRowStyle(row, _assignRoleOfficerList.Columns);
            }
            else
            {
                ApplyViewTableRowStriping(row, rowIndex, _assignRoleOfficerList.Columns);
            }

            row = row.GetNext();
            rowIndex += 1;
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
        _assignRoleDialog.OkButtonText = _localization.T("ui.confirm_assign_role");
        SetAssignRoleDialogLabelText("OfficerListLabel", _localization.T("ui.assign_role_officer"));
        SetAssignRoleDialogLabelText("RoleLabel", _localization.T("ui.assign_role_title"));
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

        var selectedOfficerIds = GetSelectedTreeMetadataIds(_assignRoleOfficerList);
        if (selectedOfficerIds.Count == 0)
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
            selectedOfficerIds[0],
            role);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
    }

    private void ReopenAssignRoleDialog()
    {
        CallDeferred(nameof(ReopenAssignRoleDialogDeferred));
    }

    private void ReopenAssignRoleDialogDeferred()
    {
        _assignRoleDialog?.PopupCentered(new Vector2I(480, 370));
    }

    private void EnsureRequestItemDialogWidgets()
    {
        if (_requestItemDialog == null)
        {
            return;
        }

        var existingRoot = _requestItemDialog.GetNodeOrNull<VBoxContainer>("RequestItemDialogRoot");
        if (existingRoot != null)
        {
            _requestItemOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerTable");
            _requestItemOption = existingRoot.GetNodeOrNull<OptionButton>("ItemRow/ItemOption");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "RequestItemDialogRoot",
            CustomMinimumSize = new Vector2(460.0f, 330.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _requestItemDialog.AddChild(root);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _requestItemOfficerList = new Tree
        {
            Name = "OfficerTable",
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0.0f, 160.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _requestItemOfficerList.ItemSelected += OnRequestItemOfficerTableSelected;
        root.AddChild(_requestItemOfficerList);

        var itemRow = CreatePersonnelBonusFormRow("ItemRow", "ItemLabel");
        _requestItemOption = new OptionButton
        {
            Name = "ItemOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        itemRow.AddChild(_requestItemOption);
        root.AddChild(itemRow);
    }

    private void ShowRequestItemDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _requestItemDialog == null || _localization == null)
        {
            return;
        }

        EnsureRequestItemDialogWidgets();
        UpdateRequestItemDialogText();
        PopulateRequestItemDialog();
        _requestItemDialog.PopupCentered(new Vector2I(480, 370));
    }

    private void PopulateRequestItemDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _requestItemOfficerList == null)
        {
            return;
        }

        _requestItemOfficerList.Clear();
        ConfigureCompactOfficerTableColumns(_requestItemOfficerList, includeStatus: true, includeCity: false, includeLoyalty: false, includeStats: true);
        var tableRoot = _requestItemOfficerList.CreateItem();
        var rowIndex = 0;
        foreach (var officerId in _selectedCity.OfficerIds)
        {
            var officer = _turnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (!_turnManager.World.Items.Any(item => item.EquippedOfficerId == officer.Id))
            {
                continue;
            }

            var row = _requestItemOfficerList.CreateItem(tableRoot);
            PopulateCompactOfficerTableRow(row, officer, rowIndex, includeStatus: true, includeCity: false, includeLoyalty: false, includeStats: true);
            rowIndex += 1;
        }

        PopulateRequestItemOption();
    }

    private void UpdateRequestItemDialogText()
    {
        if (_requestItemDialog == null || _localization == null)
        {
            return;
        }

        _requestItemDialog.Title = _localization.T("command.personnel.request_item");
        _requestItemDialog.OkButtonText = _localization.T("ui.confirm_request_item");
        SetRequestItemDialogLabelText("OfficerListLabel", _localization.T("ui.request_item_officer"));
        SetRequestItemDialogLabelText("ItemLabel", _localization.T("ui.request_item"));
    }

    private void SetRequestItemDialogLabelText(string nodeName, string text)
    {
        var label = _requestItemDialog?.GetNodeOrNull<Label>($"RequestItemDialogRoot/{nodeName}") ??
                    _requestItemDialog?.GetNodeOrNull<Label>($"RequestItemDialogRoot/ItemRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void OnRequestItemOfficerTableSelected()
    {
        if (_requestItemOfficerList == null)
        {
            return;
        }

        var selectedItem = _requestItemOfficerList.GetSelected();
        if (selectedItem != null)
        {
            var root = _requestItemOfficerList.GetRoot();
            if (root != null)
            {
                var row = root.GetFirstChild();
                var rowIndex = 0;
                while (row != null)
                {
                    if (row == selectedItem)
                    {
                        ApplyViewTableSelectedRowStyle(row, _requestItemOfficerList.Columns);
                    }
                    else
                    {
                        ApplyViewTableRowStriping(row, rowIndex, _requestItemOfficerList.Columns);
                    }

                    row = row.GetNext();
                    rowIndex += 1;
                }
            }
        }

        PopulateRequestItemOption();
    }

    private void PopulateRequestItemOption()
    {
        if (_requestItemOption == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        _requestItemOption.Clear();
        _requestItemOption.AddItem(_localization.T("ui.no_item"));
        _requestItemOption.SetItemMetadata(0, 0);

        var officerId = GetSelectedTreeMetadataIds(_requestItemOfficerList).FirstOrDefault();
        if (officerId <= 0)
        {
            _requestItemOption.Select(0);
            return;
        }

        foreach (var item in _turnManager.World.Items
                     .Where(item => item.EquippedOfficerId == officerId)
                     .OrderBy(item => _localization.GetItemName(item)))
        {
            var row = _localization.Format(
                "fmt.item_option",
                _localization.GetItemName(item),
                _localization.GetItemType(item),
                _localization.GetItemRarity(item));
            _requestItemOption.AddItem(row);
            _requestItemOption.SetItemMetadata(_requestItemOption.ItemCount - 1, item.Id);
        }

        _requestItemOption.Select(0);
    }

    private void OnRequestItemDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var officerId = GetSelectedTreeMetadataIds(_requestItemOfficerList).FirstOrDefault();
        if (officerId <= 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenRequestItemDialog();
            return;
        }

        var item = GetSelectedItemFromOption(_requestItemOption);
        if (item == null)
        {
            AddLog(_localization?.T("ui.select_item_warning") ?? string.Empty);
            ReopenRequestItemDialog();
            return;
        }

        var result = _commandResolver.ExecuteRecallOfficerItem(_turnManager.GetPlayerFactionId(), _selectedCity.Id, officerId, item.Id);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }

    private void ReopenRequestItemDialog()
    {
        CallDeferred(nameof(ReopenRequestItemDialogDeferred));
    }

    private void ReopenRequestItemDialogDeferred()
    {
        _requestItemDialog?.PopupCentered(new Vector2I(480, 370));
    }

    private void EnsureHireOfficerDialogWidgets()
    {
        if (_hireOfficerDialog == null)
        {
            return;
        }

        var existingRoot = _hireOfficerDialog.GetNodeOrNull<VBoxContainer>("HireOfficerDialogRoot");
        if (existingRoot != null)
        {
            existingRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _hireOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerTable");
            _hireOfficerGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
            _hireOfficerFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
            _hireOfficerItemOption = existingRoot.GetNodeOrNull<OptionButton>("ItemRow/ItemOption");
            _hireOfficerSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            _hireOfficerConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmButton");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "HireOfficerDialogRoot",
            CustomMinimumSize = new Vector2(560.0f, 0.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 8);
        _hireOfficerDialog.AddChild(root);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _hireOfficerList = new Tree
        {
            Name = "OfficerTable",
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0.0f, 328.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        _hireOfficerList.ItemSelected += OnHireOfficerTableSelected;
        _hireOfficerList.ColumnTitleClicked += OnHireOfficerTableColumnTitleClicked;
        root.AddChild(_hireOfficerList);

        var goldRow = CreateHireOfficerFormRow("GoldRow", "GoldLabel");
        root.AddChild(goldRow);
        _hireOfficerGoldSpinBox = CreateMoveSpinBox("GoldSpinBox");
        _hireOfficerGoldSpinBox.Step = 100;
        _hireOfficerGoldSpinBox.ValueChanged += _ => UpdateHireOfficerSummary();
        _hireOfficerGoldSpinBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        goldRow.AddChild(_hireOfficerGoldSpinBox);

        var foodRow = CreateHireOfficerFormRow("FoodRow", "FoodLabel");
        root.AddChild(foodRow);
        _hireOfficerFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        _hireOfficerFoodSpinBox.Step = 500;
        _hireOfficerFoodSpinBox.ValueChanged += _ => UpdateHireOfficerSummary();
        _hireOfficerFoodSpinBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        foodRow.AddChild(_hireOfficerFoodSpinBox);

        var itemRow = CreateHireOfficerFormRow("ItemRow", "ItemLabel");
        root.AddChild(itemRow);
        _hireOfficerItemOption = new OptionButton
        {
            Name = "ItemOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _hireOfficerItemOption.ItemSelected += _ => UpdateHireOfficerSummary();
        itemRow.AddChild(_hireOfficerItemOption);

        _hireOfficerSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_hireOfficerSummaryLabel);

        var confirmRow = new HBoxContainer
        {
            Name = "ConfirmRow",
            Alignment = BoxContainer.AlignmentMode.End
        };
        root.AddChild(confirmRow);

        _hireOfficerConfirmButton = new Button
        {
            Name = "ConfirmButton",
            CustomMinimumSize = new Vector2(110.0f, 0.0f)
        };
        _hireOfficerConfirmButton.Pressed += OnHireOfficerDialogConfirmed;
        confirmRow.AddChild(_hireOfficerConfirmButton);
    }

    private void ShowHireOfficerDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _hireOfficerDialog == null || _localization == null)
        {
            return;
        }

        EnsureHireOfficerDialogWidgets();
        UpdateHireOfficerDialogText();
        PopulateHireOfficerDialog();
        ShowHireOfficerDialogAtComputedSize();
    }

    private void PopulateHireOfficerDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _hireOfficerList == null || _localization == null)
        {
            return;
        }

        _hireOfficerList.Clear();
        ConfigureHireOfficerTableColumns();
        var tableRoot = _hireOfficerList.CreateItem();
        var playerFactionId = _turnManager.GetPlayerFactionId();
        var rowIndex = 0;
        foreach (var officer in GetOrderedHireOfficerCandidates(_turnManager.World, playerFactionId))
        {
            var row = _hireOfficerList.CreateItem(tableRoot);
            PopulateHireOfficerTableRow(row, officer);
            ApplyViewTableRowStriping(row, rowIndex, _hireOfficerList.Columns);
            rowIndex += 1;
        }

        if (rowIndex == 0)
        {
            var row = _hireOfficerList.CreateItem(tableRoot);
            row.SetText(0, _localization.T("ui.no_hireable_officer"));
            row.SetSelectable(0, false);
        }

        ConfigureMoveSpinBox(_hireOfficerGoldSpinBox, Math.Max(0, _selectedCity.Gold - HireOfficerGoldCost), 0);
        ConfigureMoveSpinBox(_hireOfficerFoodSpinBox, _selectedCity.Food, 0);
        if (_hireOfficerGoldSpinBox != null)
        {
            _hireOfficerGoldSpinBox.Step = 100;
        }
        if (_hireOfficerFoodSpinBox != null)
        {
            _hireOfficerFoodSpinBox.Step = 500;
        }

        PopulateFactionInventoryOption(_hireOfficerItemOption);
        UpdateHireOfficerDialogLayout();
        UpdateHireOfficerSummary();
        if (_hireOfficerConfirmButton != null)
        {
            _hireOfficerConfirmButton.Disabled = rowIndex == 0;
        }
    }

    private void UpdateHireOfficerDialogText()
    {
        if (_hireOfficerDialog == null || _localization == null)
        {
            return;
        }

        _hireOfficerDialog.Title = _localization.T("command.personnel.hire_officer");
        var label = _hireOfficerDialog.GetNodeOrNull<Label>("HireOfficerDialogRoot/OfficerListLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.hire_officer_target");
        }
        SetHireOfficerDialogLabelText("GoldLabel", _localization.T("ui.hire_officer_gold_offer"));
        SetHireOfficerDialogLabelText("FoodLabel", _localization.T("ui.hire_officer_food_offer"));
        SetHireOfficerDialogLabelText("ItemLabel", _localization.T("ui.hire_officer_item_offer"));
        if (_hireOfficerConfirmButton != null)
        {
            _hireOfficerConfirmButton.Text = _localization.T("ui.confirm_hire_officer");
        }
    }

    private void SetHireOfficerDialogLabelText(string nodeName, string text)
    {
        var label = _hireOfficerDialog?.GetNodeOrNull<Label>($"HireOfficerDialogRoot/GoldRow/{nodeName}") ??
                    _hireOfficerDialog?.GetNodeOrNull<Label>($"HireOfficerDialogRoot/FoodRow/{nodeName}") ??
                    _hireOfficerDialog?.GetNodeOrNull<Label>($"HireOfficerDialogRoot/ItemRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void UpdateHireOfficerSummary()
    {
        if (_hireOfficerSummaryLabel == null || _localization == null)
        {
            return;
        }

        var goldOffer = (int)(_hireOfficerGoldSpinBox?.Value ?? 0);
        var foodOffer = (int)(_hireOfficerFoodSpinBox?.Value ?? 0);
        var item = GetSelectedItemFromOption(_hireOfficerItemOption);
        _hireOfficerSummaryLabel.Text = item == null
            ? _localization.Format(
                "fmt.hire_officer_preview",
                HireOfficerGoldCost,
                goldOffer,
                foodOffer)
            : _localization.Format(
                "fmt.hire_officer_preview_with_item",
                HireOfficerGoldCost,
                goldOffer,
                foodOffer,
                _localization.GetItemName(item));
    }

    private static bool IsHireOfficerCandidate(WorldState world, int playerFactionId, OfficerData officer)
    {
        if (IsFactionRuler(world, officer))
        {
            return false;
        }

        if (!IsOfficerOldEnoughToJoin(world, officer))
        {
            return false;
        }

        if (FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
        {
            return true;
        }

        var sourceCity = officer.CityId > 0 ? world.GetCity(officer.CityId) : null;
        return sourceCity == null || sourceCity.OwnerFactionId != playerFactionId;
    }

    private void OnHireOfficerTableColumnTitleClicked(long column, long mouseButtonIndex)
    {
        var nextField = GetHireOfficerSortFieldForColumn((int)column);
        if (_hireOfficerSortField == nextField)
        {
            _hireOfficerSortAscending = !_hireOfficerSortAscending;
        }
        else
        {
            _hireOfficerSortField = nextField;
            _hireOfficerSortAscending = GetDefaultHireOfficerSortAscending(nextField);
        }

        PopulateHireOfficerDialog();
    }

    private void OnHireOfficerTableSelected()
    {
        if (_hireOfficerList == null)
        {
            return;
        }

        var selectedItem = _hireOfficerList.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        var root = _hireOfficerList.GetRoot();
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
                ApplyViewTableSelectedRowStyle(row, _hireOfficerList.Columns);
            }
            else
            {
                ApplyViewTableRowStriping(row, rowIndex, _hireOfficerList.Columns);
            }

            row = row.GetNext();
            rowIndex += 1;
        }
    }

    private void OnHireOfficerDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var selectedOfficerId = GetSelectedHireOfficerId();
        if (selectedOfficerId <= 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenHireOfficerDialog();
            return;
        }

        var result = _commandResolver.ExecuteHireOfficer(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            selectedOfficerId,
            (int)(_hireOfficerGoldSpinBox?.Value ?? 0),
            (int)(_hireOfficerFoodSpinBox?.Value ?? 0),
            GetSelectedItemFromOption(_hireOfficerItemOption)?.Id ?? 0);
        AddLog(GetLocalizedResultMessage(result));
        _hireOfficerDialog?.Hide();
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }

    private void ReopenHireOfficerDialog()
    {
        CallDeferred(nameof(ReopenHireOfficerDialogDeferred));
    }

    private void ReopenHireOfficerDialogDeferred()
    {
        ShowHireOfficerDialogAtComputedSize();
    }

    private void UpdateHireOfficerDialogLayout()
    {
        if (_hireOfficerDialog == null || _hireOfficerList == null)
        {
            return;
        }

        var root = _hireOfficerDialog.GetNodeOrNull<VBoxContainer>("HireOfficerDialogRoot");
        const int visibleRows = 10;
        var listHeight = 48 + visibleRows * 28;

        _hireOfficerList.CustomMinimumSize = new Vector2(0.0f, listHeight);
        if (root != null)
        {
            root.CustomMinimumSize = new Vector2(560.0f, 0.0f);
            root.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        }
    }

    private Vector2I GetHireOfficerDialogSize()
    {
        const int visibleRows = 10;
        var height = 330 + visibleRows * 28;
        return new Vector2I(900, height);
    }

    private void ShowHireOfficerDialogAtComputedSize()
    {
        if (_hireOfficerDialog == null)
        {
            return;
        }

        var size = GetHireOfficerDialogSize();
        _hireOfficerDialog.ResetSize();
        _hireOfficerDialog.Size = size;
        _hireOfficerDialog.MinSize = size;
        var viewportRect = GetViewport().GetVisibleRect();
        var position = new Vector2I(
            Math.Max((int)((viewportRect.Size.X - size.X) / 2.0f), 0),
            Math.Max((int)((viewportRect.Size.Y - size.Y) / 2.0f), 0));
        _hireOfficerDialog.Position = position;
        _hireOfficerDialog.Popup();
    }

    private static HBoxContainer CreateHireOfficerFormRow(string rowName, string labelName)
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

    private int GetSelectedHireOfficerId()
    {
        var selectedItem = _hireOfficerList?.GetSelected();
        if (selectedItem == null)
        {
            return 0;
        }

        var metadata = selectedItem.GetMetadata(0);
        return metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : 0;
    }

    private int GetHireOfficerCandidateCount()
    {
        if (_turnManager?.World == null)
        {
            return 0;
        }

        return GetOrderedHireOfficerCandidates(_turnManager.World, _turnManager.GetPlayerFactionId()).Count;
    }

    private List<OfficerData> GetOrderedHireOfficerCandidates(WorldState world, int playerFactionId)
    {
        var candidates = world.Officers
            .Where(officer => IsHireOfficerCandidate(world, playerFactionId, officer))
            .ToList();

        IOrderedEnumerable<OfficerData> ordered = (_hireOfficerSortField, _hireOfficerSortAscending) switch
        {
            (HireOfficerSortField.Name, true) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenBy(officer => _localization?.GetOfficerName(officer) ?? officer.Name),
            (HireOfficerSortField.Name, false) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenByDescending(officer => _localization?.GetOfficerName(officer) ?? officer.Name),
            (HireOfficerSortField.Role, true) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenBy(officer => _localization?.GetOfficerRole(officer) ?? officer.Role),
            (HireOfficerSortField.Role, false) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenByDescending(officer => _localization?.GetOfficerRole(officer) ?? officer.Role),
            (HireOfficerSortField.City, true) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenBy(officer => GetHireOfficerCitySortText(world, officer)),
            (HireOfficerSortField.City, false) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenByDescending(officer => GetHireOfficerCitySortText(world, officer)),
            (HireOfficerSortField.Owner, true) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenBy(officer => GetHireOfficerOwnerSortText(world, officer)),
            (HireOfficerSortField.Owner, false) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenByDescending(officer => GetHireOfficerOwnerSortText(world, officer)),
            (HireOfficerSortField.Loyalty, true) => candidates
                .OrderBy(officer => IsHireOfficerLoyaltyDash(world, officer) ? 0 : 1)
                .ThenBy(officer => GetHireOfficerLoyaltyValue(world, officer))
                .ThenBy(officer => _localization?.GetOfficerName(officer) ?? officer.Name),
            (HireOfficerSortField.Loyalty, false) => candidates
                .OrderBy(officer => IsHireOfficerLoyaltyDash(world, officer) ? 0 : 1)
                .ThenByDescending(officer => GetHireOfficerLoyaltyValue(world, officer))
                .ThenBy(officer => _localization?.GetOfficerName(officer) ?? officer.Name),
            (HireOfficerSortField.Strength, true) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenBy(officer => officer.Strength),
            (HireOfficerSortField.Strength, false) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenByDescending(officer => officer.Strength),
            (HireOfficerSortField.Intelligence, true) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenBy(officer => officer.Intelligence),
            (HireOfficerSortField.Intelligence, false) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenByDescending(officer => officer.Intelligence),
            (HireOfficerSortField.Charm, true) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenBy(officer => officer.Charm),
            (HireOfficerSortField.Charm, false) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenByDescending(officer => officer.Charm),
            (HireOfficerSortField.Leadership, true) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenBy(officer => officer.Leadership),
            (HireOfficerSortField.Leadership, false) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenByDescending(officer => officer.Leadership),
            (HireOfficerSortField.Combat, true) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenBy(officer => officer.Combat),
            (HireOfficerSortField.Combat, false) => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenByDescending(officer => officer.Combat),
            _ => candidates
                .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
                .ThenBy(officer => _localization?.GetOfficerName(officer) ?? officer.Name)
        };

        return ordered
            .ThenBy(officer => officer.Id)
            .ToList();
    }

    private void ConfigureHireOfficerTableColumns()
    {
        if (_hireOfficerList == null || _localization == null)
        {
            return;
        }

        _hireOfficerList.Columns = 10;
        SetHireOfficerTableColumn(0, _localization.T("ui.officers"), 150, HireOfficerSortField.Name);
        SetHireOfficerTableColumn(1, _localization.T("ui.role"), 110, HireOfficerSortField.Role);
        SetHireOfficerTableColumn(2, _localization.T("ui.city"), 140, HireOfficerSortField.City);
        SetHireOfficerTableColumn(3, _localization.T("ui.faction_owner"), 140, HireOfficerSortField.Owner);
        SetHireOfficerTableColumn(4, _localization.T("ui.loyalty"), 80, HireOfficerSortField.Loyalty);
        SetHireOfficerTableColumn(5, _localization.T("ui.strength"), 80, HireOfficerSortField.Strength);
        SetHireOfficerTableColumn(6, _localization.T("ui.intelligence"), 80, HireOfficerSortField.Intelligence);
        SetHireOfficerTableColumn(7, _localization.T("ui.charm"), 80, HireOfficerSortField.Charm);
        SetHireOfficerTableColumn(8, _localization.T("ui.leadership"), 90, HireOfficerSortField.Leadership);
        SetHireOfficerTableColumn(9, _localization.T("ui.combat"), 80, HireOfficerSortField.Combat);
    }

    private void SetHireOfficerTableColumn(int column, string title, int minWidth, HireOfficerSortField field)
    {
        _hireOfficerList?.SetColumnTitle(column, BuildHireOfficerSortableColumnTitle(title, field));
        _hireOfficerList?.SetColumnCustomMinimumWidth(column, minWidth);
        _hireOfficerList?.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
    }

    private void PopulateHireOfficerTableRow(TreeItem row, OfficerData officer)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return;
        }

        var world = _turnManager.World;
        var sourceCity = officer.CityId > 0 ? world.GetCity(officer.CityId) : null;
        var isFreeOfficer = FreeOfficerMovement.IsFreeOfficer(world, officer);
        var freeOfficerText = _localization.T("ui.free_officer");
        row.SetMetadata(0, officer.Id);
        row.SetText(0, _localization.GetOfficerName(officer));
        row.SetText(1, _localization.GetOfficerRole(officer));
        row.SetText(2, sourceCity != null ? _localization.GetCityName(sourceCity) : freeOfficerText);
        row.SetText(3, isFreeOfficer || sourceCity == null ? freeOfficerText : _localization.GetFactionName(world, sourceCity.OwnerFactionId));
        row.SetText(4, BuildOfficerLoyaltyTableText(world, officer));
        row.SetText(5, officer.Strength.ToString());
        row.SetText(6, officer.Intelligence.ToString());
        row.SetText(7, officer.Charm.ToString());
        row.SetText(8, officer.Leadership.ToString());
        row.SetText(9, officer.Combat.ToString());
    }

    private string BuildHireOfficerSortableColumnTitle(string title, HireOfficerSortField field)
    {
        if (_hireOfficerSortField != field)
        {
            return title;
        }

        return _hireOfficerSortAscending ? $"{title} ↑" : $"{title} ↓";
    }

    private static HireOfficerSortField GetHireOfficerSortFieldForColumn(int column)
    {
        return column switch
        {
            1 => HireOfficerSortField.Role,
            2 => HireOfficerSortField.City,
            3 => HireOfficerSortField.Owner,
            4 => HireOfficerSortField.Loyalty,
            5 => HireOfficerSortField.Strength,
            6 => HireOfficerSortField.Intelligence,
            7 => HireOfficerSortField.Charm,
            8 => HireOfficerSortField.Leadership,
            9 => HireOfficerSortField.Combat,
            _ => HireOfficerSortField.Name
        };
    }

    private static bool GetDefaultHireOfficerSortAscending(HireOfficerSortField field)
    {
        return field switch
        {
            HireOfficerSortField.Loyalty => false,
            HireOfficerSortField.Strength => false,
            HireOfficerSortField.Intelligence => false,
            HireOfficerSortField.Charm => false,
            HireOfficerSortField.Leadership => false,
            HireOfficerSortField.Combat => false,
            _ => true
        };
    }

    private string GetHireOfficerCitySortText(WorldState world, OfficerData officer)
    {
        var sourceCity = officer.CityId > 0 ? world.GetCity(officer.CityId) : null;
        return sourceCity != null ? _localization?.GetCityName(sourceCity) ?? sourceCity.NameEn : (_localization?.T("ui.free_officer") ?? "Free Officer");
    }

    private string GetHireOfficerOwnerSortText(WorldState world, OfficerData officer)
    {
        var sourceCity = officer.CityId > 0 ? world.GetCity(officer.CityId) : null;
        if (FreeOfficerMovement.IsFreeOfficer(world, officer) || sourceCity == null)
        {
            return _localization?.T("ui.free_officer") ?? "Free Officer";
        }

        return _localization?.GetFactionName(world, sourceCity.OwnerFactionId) ?? sourceCity.OwnerFactionId.ToString();
    }

    private static int GetHireOfficerLoyaltyValue(WorldState world, OfficerData officer)
    {
        return FreeOfficerMovement.IsFreeOfficer(world, officer) || IsFactionRuler(world, officer) ? 0 : officer.Loyalty;
    }

    private static bool IsHireOfficerLoyaltyDash(WorldState world, OfficerData officer)
    {
        return FreeOfficerMovement.IsFreeOfficer(world, officer) || IsFactionRuler(world, officer);
    }

    private void PopulateFactionInventoryOption(OptionButton? option)
    {
        if (option == null || _selectedCity == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        option.Clear();
        option.AddItem(_localization.T("ui.no_item"));
        option.SetItemMetadata(0, 0);

        foreach (var item in _turnManager.World.Items
                     .Where(item => item.OwnerFactionId == _selectedCity.OwnerFactionId && item.EquippedOfficerId <= 0)
                     .OrderBy(item => _localization.GetItemName(item)))
        {
            var row = _localization.Format(
                "fmt.item_option",
                _localization.GetItemName(item),
                _localization.GetItemType(item),
                _localization.GetItemRarity(item));
            option.AddItem(row);
            option.SetItemMetadata(option.ItemCount - 1, item.Id);
        }

        option.Select(0);
    }

    private ItemData? GetSelectedItemFromOption(OptionButton? option)
    {
        if (option == null || _turnManager?.World == null)
        {
            return null;
        }

        var selectedIndex = option.Selected;
        if (selectedIndex < 0)
        {
            return null;
        }

        var metadata = option.GetItemMetadata(selectedIndex);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return null;
        }

        var itemId = metadata.AsInt32();
        return itemId > 0 ? _turnManager.World.GetItem(itemId) : null;
    }


}
