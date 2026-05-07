using System;
using System.Linq;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController : CanvasLayer
{
    private void EnsureSpyDialogWidgets()
    {
        if (_spyDialog == null)
        {
            return;
        }

        var existingRoot = _spyDialog.GetNodeOrNull<VBoxContainer>("SpyDialogRoot");
        if (existingRoot != null)
        {
            _spyActionOption = existingRoot.GetNodeOrNull<OptionButton>("ActionRow/ActionOption");
            _spyTargetCityOption = existingRoot.GetNodeOrNull<OptionButton>("TargetCityRow/TargetCityOption");
            _spyOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerList");
            _spySummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            _spyWarningLabel = existingRoot.GetNodeOrNull<Label>("WarningLabel");
            _spyConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmButton");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "SpyDialogRoot",
            CustomMinimumSize = new Vector2(760.0f, 470.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        _spyDialog.AddChild(root);

        root.AddChild(CreateDiplomacyFormRow("ActionRow", "ActionLabel"));
        _spyActionOption = new OptionButton
        {
            Name = "ActionOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _spyActionOption.ItemSelected += _ =>
        {
            UpdateSpySummary();
            UpdateSpyConfirmButtonState();
        };
        root.GetNode<HBoxContainer>("ActionRow").AddChild(_spyActionOption);

        root.AddChild(CreateDiplomacyFormRow("TargetCityRow", "TargetCityLabel"));
        _spyTargetCityOption = new OptionButton
        {
            Name = "TargetCityOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _spyTargetCityOption.ItemSelected += _ =>
        {
            UpdateSpySummary();
            UpdateSpyConfirmButtonState();
        };
        root.GetNode<HBoxContainer>("TargetCityRow").AddChild(_spyTargetCityOption);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _spyOfficerList = new Tree
        {
            Name = "OfficerList",
            Columns = 5,
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0.0f, 170.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _spyOfficerList.ItemSelected += OnSpyOfficerTableSelected;
        root.AddChild(_spyOfficerList);

        _spySummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_spySummaryLabel);

        _spyWarningLabel = new Label
        {
            Name = "WarningLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_spyWarningLabel);

        var footer = new HBoxContainer
        {
            Name = "FooterRow",
            Alignment = BoxContainer.AlignmentMode.Center
        };
        root.AddChild(footer);
        _spyConfirmButton = new Button
        {
            Name = "ConfirmButton"
        };
        _spyConfirmButton.Pressed += OnSpyConfirmPressed;
        footer.AddChild(_spyConfirmButton);
    }

    private void ShowSpyDialog()
    {
        if (_selectedCity == null || _spyDialog == null || _localization == null)
        {
            return;
        }

        EnsureSpyDialogWidgets();
        UpdateSpyDialogText();
        PopulateSpyDialog();
        _spyDialog.PopupCentered(new Vector2I(820, 520));
    }

    private void PopulateSpyDialog()
    {
        if (_turnManager?.World == null || _selectedCity == null || _localization == null ||
            _spyActionOption == null || _spyTargetCityOption == null || _spyOfficerList == null)
        {
            return;
        }

        _spyActionOption.Clear();
        AddSpyActionOption(SpyActionType.Reconnaissance);
        AddSpyActionOption(SpyActionType.Sabotage);
        AddSpyActionOption(SpyActionType.Incite);

        _spyTargetCityOption.Clear();
        foreach (var city in _turnManager.World.Cities.Where(city => city.OwnerFactionId != _selectedCity.OwnerFactionId))
        {
            var ownerName = _localization.GetFactionName(_turnManager.World, city.OwnerFactionId);
            _spyTargetCityOption.AddItem($"{_localization.GetCityName(city)} | {ownerName}");
            _spyTargetCityOption.SetItemMetadata(_spyTargetCityOption.ItemCount - 1, city.Id);
        }

        ConfigureSpyOfficerTableColumns();
        _spyOfficerList.Clear();
        var root = _spyOfficerList.CreateItem();
        var rowIndex = 0;
        foreach (var officerId in _selectedCity.OfficerIds)
        {
            var officer = _turnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (officer.LastAssignedYear == _turnManager.World.Year &&
                officer.LastAssignedMonth == _turnManager.World.Month)
            {
                continue;
            }

            if (HasActiveInternalAffairsSchedule(officer.Id))
            {
                continue;
            }

            var row = _spyOfficerList.CreateItem(root);
            PopulateSpyOfficerTableRow(row, officer, rowIndex);
            rowIndex += 1;
        }

        SelectFirstSpyOfficerRow();
        SetSpyWarning(string.Empty);
        UpdateSpySummary();
        UpdateSpyConfirmButtonState();
    }

    private void UpdateSpyDialogText()
    {
        if (_spyDialog == null || _localization == null)
        {
            return;
        }

        _spyDialog.Title = _localization.T("ui.spy");
        SetSpyDialogLabelText("ActionLabel", _localization.T("ui.spy_action"));
        SetSpyDialogLabelText("TargetCityLabel", _localization.T("ui.spy_target_city"));
        SetSpyDialogLabelText("OfficerListLabel", _localization.T("ui.spy_officer"));
        if (_spyConfirmButton != null)
        {
            _spyConfirmButton.Text = _localization.T("ui.confirm_spy");
        }
    }

    private void SetSpyDialogLabelText(string nodeName, string text)
    {
        var root = _spyDialog?.GetNodeOrNull<Control>("SpyDialogRoot");
        var label = root?.FindChild(nodeName, true, false) as Label;
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void AddSpyActionOption(SpyActionType actionType)
    {
        if (_spyActionOption == null || _localization == null)
        {
            return;
        }

        var key = actionType switch
        {
            SpyActionType.Reconnaissance => "command.spy.reconnaissance",
            SpyActionType.Sabotage => "command.spy.sabotage",
            SpyActionType.Incite => "command.spy.incite",
            _ => "command.spy.reconnaissance"
        };
        _spyActionOption.AddItem(_localization.T(key));
        _spyActionOption.SetItemMetadata(_spyActionOption.ItemCount - 1, (int)actionType);
    }

    private void ConfigureSpyOfficerTableColumns()
    {
        if (_spyOfficerList == null || _localization == null)
        {
            return;
        }

        _spyOfficerList.Columns = 5;
        _spyOfficerList.SetColumnTitle(0, _localization.T("ui.officers"));
        _spyOfficerList.SetColumnCustomMinimumWidth(0, 140);
        _spyOfficerList.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _spyOfficerList.SetColumnTitle(1, _localization.T("ui.role"));
        _spyOfficerList.SetColumnCustomMinimumWidth(1, 90);
        _spyOfficerList.SetColumnTitleAlignment(1, HorizontalAlignment.Left);
        _spyOfficerList.SetColumnTitle(2, _localization.T("ui.intelligence"));
        _spyOfficerList.SetColumnCustomMinimumWidth(2, 80);
        _spyOfficerList.SetColumnTitleAlignment(2, HorizontalAlignment.Left);
        _spyOfficerList.SetColumnTitle(3, _localization.T("ui.charm"));
        _spyOfficerList.SetColumnCustomMinimumWidth(3, 80);
        _spyOfficerList.SetColumnTitleAlignment(3, HorizontalAlignment.Left);
        _spyOfficerList.SetColumnTitle(4, _localization.T("ui.politics"));
        _spyOfficerList.SetColumnCustomMinimumWidth(4, 80);
        _spyOfficerList.SetColumnTitleAlignment(4, HorizontalAlignment.Left);
    }

    private void PopulateSpyOfficerTableRow(TreeItem row, OfficerData officer, int rowIndex)
    {
        if (_localization == null)
        {
            return;
        }

        row.SetMetadata(0, officer.Id);
        row.SetText(0, _localization.GetOfficerName(officer));
        row.SetText(1, _localization.GetOfficerRole(officer));
        row.SetText(2, officer.Intelligence.ToString());
        row.SetText(3, officer.Charm.ToString());
        row.SetText(4, officer.Politics.ToString());
        ApplyViewTableRowStriping(row, rowIndex, 5);
    }

    private void SelectFirstSpyOfficerRow()
    {
        var row = _spyOfficerList?.GetRoot()?.GetFirstChild();
        if (row == null)
        {
            return;
        }

        row.Select(0);
        OnSpyOfficerTableSelected();
    }

    private void OnSpyOfficerTableSelected()
    {
        if (_spyOfficerList == null)
        {
            return;
        }

        var selectedItem = _spyOfficerList.GetSelected();
        var root = _spyOfficerList.GetRoot();
        if (selectedItem == null || root == null)
        {
            return;
        }

        var row = root.GetFirstChild();
        var rowIndex = 0;
        while (row != null)
        {
            if (row == selectedItem)
            {
                ApplyViewTableSelectedRowStyle(row, _spyOfficerList.Columns);
            }
            else
            {
                ApplyViewTableRowStriping(row, rowIndex, _spyOfficerList.Columns);
            }

            row = row.GetNext();
            rowIndex += 1;
        }

        UpdateSpyConfirmButtonState();
    }

    private SpyActionType GetSelectedSpyActionType()
    {
        if (_spyActionOption == null)
        {
            return SpyActionType.Reconnaissance;
        }

        var metadata = _spyActionOption.GetItemMetadata(_spyActionOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (SpyActionType)metadata.AsInt32()
            : SpyActionType.Reconnaissance;
    }

    private int GetSelectedSpyTargetCityId()
    {
        if (_spyTargetCityOption == null || _spyTargetCityOption.ItemCount == 0)
        {
            return -1;
        }

        var metadata = _spyTargetCityOption.GetItemMetadata(_spyTargetCityOption.Selected);
        return metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : -1;
    }

    private void UpdateSpySummary()
    {
        if (_turnManager?.World == null || _localization == null || _spySummaryLabel == null)
        {
            return;
        }

        var actionType = GetSelectedSpyActionType();
        var targetCityId = GetSelectedSpyTargetCityId();
        var targetCity = _turnManager.World.GetCity(targetCityId);
        var targetCityName = targetCity != null ? _localization.GetCityName(targetCity) : "-";
        var actionName = _localization.T(actionType switch
        {
            SpyActionType.Reconnaissance => "command.spy.reconnaissance",
            SpyActionType.Sabotage => "command.spy.sabotage",
            SpyActionType.Incite => "command.spy.incite",
            _ => "command.spy.reconnaissance"
        });
        _spySummaryLabel.Text = _localization.Format("fmt.spy_summary", actionName, targetCityName);
    }

    private void UpdateSpyConfirmButtonState()
    {
        if (_spyConfirmButton == null)
        {
            return;
        }

        var hasOfficer = GetSelectedTreeMetadataIds(_spyOfficerList).Count > 0;
        var hasTarget = GetSelectedSpyTargetCityId() > 0;
        _spyConfirmButton.Disabled = !hasOfficer || !hasTarget;
    }

    private void SetSpyWarning(string text)
    {
        if (_spyWarningLabel != null)
        {
            _spyWarningLabel.Text = text;
        }
    }

    private void OnSpyConfirmPressed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null || _localization == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedTreeMetadataIds(_spyOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            SetSpyWarning(_localization.T("ui.select_officer_warning"));
            return;
        }

        var targetCityId = GetSelectedSpyTargetCityId();
        if (targetCityId <= 0)
        {
            SetSpyWarning(_localization.T("ui.spy_target_required_warning"));
            return;
        }

        var result = _commandResolver.Execute(new CommandRequest
        {
            Type = CommandType.Spy,
            ActorFactionId = _turnManager.GetPlayerFactionId(),
            SourceCityId = _selectedCity.Id,
            TargetCityId = targetCityId,
            OfficerIds = selectedOfficerIds,
            SpyActionType = GetSelectedSpyActionType()
        });

        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        if (result.Success)
        {
            _spyDialog?.Hide();
            RefreshSelectedCity();
            return;
        }

        SetSpyWarning(GetLocalizedResultMessage(result));
    }
}
