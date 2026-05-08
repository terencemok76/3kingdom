using System;
using System.Linq;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController : CanvasLayer
{
    private void EnsureDiplomacyDialogWidgets()
    {
        if (_diplomacyDialog == null)
        {
            return;
        }

        var existingRoot = _diplomacyDialog.GetNodeOrNull<VBoxContainer>("DiplomacyDialogRoot");
        if (existingRoot != null)
        {
            _diplomacyActionOption = existingRoot.GetNodeOrNull<OptionButton>("ActionRow/ActionOption");
            _diplomacyTargetFactionOption = existingRoot.GetNodeOrNull<OptionButton>("TargetFactionRow/TargetFactionOption");
            _diplomacyDurationSpinBox = existingRoot.GetNodeOrNull<SpinBox>("DurationRow/DurationSpinBox");
            _diplomacyGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
            _diplomacyOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerList");
            _diplomacyRelationInfoLabel = existingRoot.GetNodeOrNull<Label>("RelationInfoLabel");
            _diplomacySummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            _diplomacyWarningLabel = existingRoot.GetNodeOrNull<Label>("WarningLabel");
            _diplomacyConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmButton");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "DiplomacyDialogRoot",
            CustomMinimumSize = new Vector2(760.0f, 500.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        _diplomacyDialog.AddChild(root);

        root.AddChild(CreateDiplomacyFormRow("ActionRow", "ActionLabel"));
        _diplomacyActionOption = new OptionButton
        {
            Name = "ActionOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _diplomacyActionOption.ItemSelected += _ =>
        {
            UpdateDiplomacyDialogInputState();
            UpdateDiplomacySummary();
            UpdateDiplomacyConfirmButtonState();
        };
        root.GetNode<HBoxContainer>("ActionRow").AddChild(_diplomacyActionOption);

        root.AddChild(CreateDiplomacyFormRow("TargetFactionRow", "TargetFactionLabel"));
        _diplomacyTargetFactionOption = new OptionButton
        {
            Name = "TargetFactionOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _diplomacyTargetFactionOption.ItemSelected += _ =>
        {
            UpdateDiplomacyRelationInfo();
            UpdateDiplomacySummary();
        };
        root.GetNode<HBoxContainer>("TargetFactionRow").AddChild(_diplomacyTargetFactionOption);

        root.AddChild(CreateDiplomacyFormRow("DurationRow", "DurationLabel"));
        _diplomacyDurationSpinBox = new SpinBox
        {
            Name = "DurationSpinBox",
            MinValue = 1,
            MaxValue = 12,
            Step = 1,
            Value = 3,
            Rounded = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _diplomacyDurationSpinBox.ValueChanged += _ => UpdateDiplomacySummary();
        root.GetNode<HBoxContainer>("DurationRow").AddChild(_diplomacyDurationSpinBox);

        root.AddChild(CreateDiplomacyFormRow("GoldRow", "GoldLabel"));
        _diplomacyGoldSpinBox = new SpinBox
        {
            Name = "GoldSpinBox",
            MinValue = 0,
            MaxValue = 9999,
            Step = 100,
            Value = 0,
            Rounded = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _diplomacyGoldSpinBox.ValueChanged += _ =>
        {
            UpdateDiplomacySummary();
            UpdateDiplomacyConfirmButtonState();
        };
        root.GetNode<HBoxContainer>("GoldRow").AddChild(_diplomacyGoldSpinBox);

        _diplomacyRelationInfoLabel = new Label
        {
            Name = "RelationInfoLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_diplomacyRelationInfoLabel);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _diplomacyOfficerList = new Tree
        {
            Name = "OfficerList",
            Columns = 6,
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0.0f, 170.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _diplomacyOfficerList.ItemSelected += OnDiplomacyOfficerTableSelected;
        root.AddChild(_diplomacyOfficerList);

        _diplomacySummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_diplomacySummaryLabel);

        _diplomacyWarningLabel = new Label
        {
            Name = "WarningLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_diplomacyWarningLabel);

        var footer = new HBoxContainer
        {
            Name = "FooterRow",
            Alignment = BoxContainer.AlignmentMode.Center
        };
        root.AddChild(footer);
        _diplomacyConfirmButton = new Button
        {
            Name = "ConfirmButton"
        };
        _diplomacyConfirmButton.Pressed += OnDiplomacyConfirmPressed;
        footer.AddChild(_diplomacyConfirmButton);
    }

    private void ShowDiplomacyDialog()
    {
        if (_selectedCity == null || _diplomacyDialog == null || _localization == null)
        {
            return;
        }

        EnsureDiplomacyDialogWidgets();
        UpdateDiplomacyDialogText();
        PopulateDiplomacyDialog();
        _diplomacyDialog.PopupCentered(new Vector2I(820, 560));
    }

    private void PopulateDiplomacyDialog()
    {
        if (_turnManager?.World == null || _selectedCity == null || _localization == null ||
            _diplomacyActionOption == null || _diplomacyTargetFactionOption == null ||
            _diplomacyDurationSpinBox == null || _diplomacyGoldSpinBox == null || _diplomacyOfficerList == null)
        {
            return;
        }

        _diplomacyActionOption.Clear();
        AddDiplomacyActionOption(DiplomacyActionType.Alliance);
        AddDiplomacyActionOption(DiplomacyActionType.Truce);
        AddDiplomacyActionOption(DiplomacyActionType.Gift);
        AddDiplomacyActionOption(DiplomacyActionType.Demand);
        AddDiplomacyActionOption(DiplomacyActionType.BreakPact);

        _diplomacyTargetFactionOption.Clear();
        foreach (var faction in _turnManager.World.Factions.Where(faction =>
                     faction.Id != _selectedCity.OwnerFactionId &&
                     _turnManager.World.Cities.Any(city => city.OwnerFactionId == faction.Id)))
        {
            _diplomacyTargetFactionOption.AddItem(_localization.GetFactionName(_turnManager.World, faction.Id));
            _diplomacyTargetFactionOption.SetItemMetadata(_diplomacyTargetFactionOption.ItemCount - 1, faction.Id);
        }

        _diplomacyDurationSpinBox.Value = 3;
        _diplomacyGoldSpinBox.Value = 0;
        _diplomacyGoldSpinBox.MaxValue = _selectedCity.Gold;

        ConfigureDiplomacyOfficerTableColumns();
        _diplomacyOfficerList.Clear();
        var root = _diplomacyOfficerList.CreateItem();
        var rowIndex = 0;
        foreach (var officerId in _selectedCity.OfficerIds)
        {
            var officer = _turnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (officer.Id == _turnManager.World.GetFaction(_selectedCity.OwnerFactionId)?.RulerOfficerId)
            {
                continue;
            }

            if (officer.LastAssignedYear == _turnManager.World.Year &&
                officer.LastAssignedMonth == _turnManager.World.Month)
            {
                continue;
            }

            var row = _diplomacyOfficerList.CreateItem(root);
            PopulateDiplomacyOfficerTableRow(row, officer, rowIndex);
            rowIndex += 1;
        }

        SelectFirstDiplomacyOfficerRow();
        SetDiplomacyWarning(string.Empty);
        UpdateDiplomacyDialogInputState();
        UpdateDiplomacyRelationInfo();
        UpdateDiplomacySummary();
        UpdateDiplomacyConfirmButtonState();
    }

    private void UpdateDiplomacyDialogText()
    {
        if (_diplomacyDialog == null || _localization == null)
        {
            return;
        }

        _diplomacyDialog.Title = _localization.T("ui.diplomacy");
        SetDiplomacyDialogLabelText("ActionLabel", _localization.T("ui.diplomacy_action"));
        SetDiplomacyDialogLabelText("TargetFactionLabel", _localization.T("ui.diplomacy_target_faction"));
        SetDiplomacyDialogLabelText("DurationLabel", _localization.T("ui.diplomacy_duration"));
        SetDiplomacyDialogLabelText("GoldLabel", _localization.T("ui.diplomacy_gift_gold"));
        SetDiplomacyDialogLabelText("OfficerListLabel", _localization.T("ui.diplomacy_officer"));
        if (_diplomacyConfirmButton != null)
        {
            _diplomacyConfirmButton.Text = _localization.T("ui.confirm_diplomacy");
        }
    }

    private void SetDiplomacyDialogLabelText(string nodeName, string text)
    {
        var root = _diplomacyDialog?.GetNodeOrNull<Control>("DiplomacyDialogRoot");
        var label = root?.FindChild(nodeName, true, false) as Label;
        if (label != null)
        {
            label.Text = text;
        }
    }

    private static HBoxContainer CreateDiplomacyFormRow(string rowName, string labelName)
    {
        var row = new HBoxContainer
        {
            Name = rowName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label
        {
            Name = labelName,
            CustomMinimumSize = new Vector2(120.0f, 0.0f)
        });
        return row;
    }

    private void AddDiplomacyActionOption(DiplomacyActionType actionType)
    {
        if (_diplomacyActionOption == null || _localization == null)
        {
            return;
        }

        var key = actionType switch
        {
            DiplomacyActionType.Alliance => "command.diplomacy.alliance",
            DiplomacyActionType.Truce => "command.diplomacy.truce",
            DiplomacyActionType.Gift => "command.diplomacy.gift",
            DiplomacyActionType.Demand => "command.diplomacy.demand",
            DiplomacyActionType.BreakPact => "command.diplomacy.break_pact",
            _ => "command.diplomacy.alliance"
        };
        _diplomacyActionOption.AddItem(_localization.T(key));
        _diplomacyActionOption.SetItemMetadata(_diplomacyActionOption.ItemCount - 1, (int)actionType);
    }

    private void ConfigureDiplomacyOfficerTableColumns()
    {
        if (_diplomacyOfficerList == null || _localization == null)
        {
            return;
        }

        _diplomacyOfficerList.Columns = 5;
        _diplomacyOfficerList.SetColumnTitle(0, _localization.T("ui.officers"));
        _diplomacyOfficerList.SetColumnCustomMinimumWidth(0, 130);
        _diplomacyOfficerList.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _diplomacyOfficerList.SetColumnTitle(1, _localization.T("ui.role"));
        _diplomacyOfficerList.SetColumnCustomMinimumWidth(1, 90);
        _diplomacyOfficerList.SetColumnTitleAlignment(1, HorizontalAlignment.Left);
        _diplomacyOfficerList.SetColumnTitle(2, _localization.T("ui.charm"));
        _diplomacyOfficerList.SetColumnCustomMinimumWidth(2, 70);
        _diplomacyOfficerList.SetColumnTitleAlignment(2, HorizontalAlignment.Left);
        _diplomacyOfficerList.SetColumnTitle(3, _localization.T("ui.politics"));
        _diplomacyOfficerList.SetColumnCustomMinimumWidth(3, 70);
        _diplomacyOfficerList.SetColumnTitleAlignment(3, HorizontalAlignment.Left);
        _diplomacyOfficerList.SetColumnTitle(4, _localization.T("ui.intelligence"));
        _diplomacyOfficerList.SetColumnCustomMinimumWidth(4, 70);
        _diplomacyOfficerList.SetColumnTitleAlignment(4, HorizontalAlignment.Left);
    }

    private void PopulateDiplomacyOfficerTableRow(TreeItem row, OfficerData officer, int rowIndex)
    {
        if (_localization == null)
        {
            return;
        }

        row.SetMetadata(0, officer.Id);
        row.SetText(0, _localization.GetOfficerName(officer));
        row.SetText(1, _localization.GetOfficerRole(officer));
        row.SetText(2, officer.Charm.ToString());
        row.SetText(3, officer.Politics.ToString());
        row.SetText(4, officer.Intelligence.ToString());
        ApplyViewTableRowStriping(row, rowIndex, 5);
    }

    private void SelectFirstDiplomacyOfficerRow()
    {
        var row = _diplomacyOfficerList?.GetRoot()?.GetFirstChild();
        if (row == null)
        {
            return;
        }

        row.Select(0);
        OnDiplomacyOfficerTableSelected();
    }

    private void OnDiplomacyOfficerTableSelected()
    {
        if (_diplomacyOfficerList == null)
        {
            return;
        }

        var selectedItem = _diplomacyOfficerList.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        var root = _diplomacyOfficerList.GetRoot();
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
                ApplyViewTableSelectedRowStyle(row, _diplomacyOfficerList.Columns);
            }
            else
            {
                ApplyViewTableRowStriping(row, rowIndex, _diplomacyOfficerList.Columns);
            }

            row = row.GetNext();
            rowIndex += 1;
        }

        UpdateDiplomacyConfirmButtonState();
    }

    private DiplomacyActionType GetSelectedDiplomacyActionType()
    {
        if (_diplomacyActionOption == null)
        {
            return DiplomacyActionType.Alliance;
        }

        var metadata = _diplomacyActionOption.GetItemMetadata(_diplomacyActionOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (DiplomacyActionType)metadata.AsInt32()
            : DiplomacyActionType.Alliance;
    }

    private int GetSelectedDiplomacyTargetFactionId()
    {
        if (_diplomacyTargetFactionOption == null || _diplomacyTargetFactionOption.ItemCount == 0)
        {
            return -1;
        }

        var metadata = _diplomacyTargetFactionOption.GetItemMetadata(_diplomacyTargetFactionOption.Selected);
        return metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : -1;
    }

    private void UpdateDiplomacySummary()
    {
        if (_turnManager?.World == null || _localization == null || _diplomacySummaryLabel == null)
        {
            return;
        }

        var actionType = GetSelectedDiplomacyActionType();
        var targetFactionId = GetSelectedDiplomacyTargetFactionId();
        var targetFaction = _turnManager.World.GetFaction(targetFactionId);
        var targetFactionName = targetFaction != null ? _localization.GetFactionName(_turnManager.World, targetFaction.Id) : "-";
        var duration = Math.Max(1, (int)Math.Round(_diplomacyDurationSpinBox?.Value ?? 1));
        var gold = Math.Max(0, (int)Math.Round(_diplomacyGoldSpinBox?.Value ?? 0));
        var actionName = _localization.T(actionType switch
        {
            DiplomacyActionType.Alliance => "command.diplomacy.alliance",
            DiplomacyActionType.Truce => "command.diplomacy.truce",
            DiplomacyActionType.Gift => "command.diplomacy.gift",
            DiplomacyActionType.Demand => "command.diplomacy.demand",
            DiplomacyActionType.BreakPact => "command.diplomacy.break_pact",
            _ => "command.diplomacy.alliance"
        });
        var summaryKey = actionType switch
        {
            DiplomacyActionType.Gift => "fmt.diplomacy_summary_gift",
            DiplomacyActionType.Demand => "fmt.diplomacy_summary_demand",
            DiplomacyActionType.BreakPact => "fmt.diplomacy_summary_break_pact",
            _ => "fmt.diplomacy_summary_treaty"
        };
        _diplomacySummaryLabel.Text = actionType switch
        {
            DiplomacyActionType.Gift => _localization.Format(summaryKey, actionName, targetFactionName, gold),
            DiplomacyActionType.Demand => _localization.Format(summaryKey, actionName, targetFactionName, gold),
            DiplomacyActionType.BreakPact => _localization.Format(summaryKey, actionName, targetFactionName),
            _ => _localization.Format(summaryKey, actionName, targetFactionName, duration)
        };
    }

    private void UpdateDiplomacyRelationInfo()
    {
        if (_turnManager?.World == null || _localization == null || _diplomacyRelationInfoLabel == null || _selectedCity == null)
        {
            return;
        }

        var targetFactionId = GetSelectedDiplomacyTargetFactionId();
        var relation = _turnManager.World.GetDiplomacyRelation(_selectedCity.OwnerFactionId, targetFactionId);
        var status = relation?.Status ?? DiplomacyStatusType.Neutral;
        var remainingMonths = status == DiplomacyStatusType.Neutral
            ? "-"
            : (relation?.RemainingMonths ?? 0).ToString();
        var relationScore = relation?.RelationScore ?? 0;

        _diplomacyRelationInfoLabel.Text =
            $"{_localization.T("ui.relation_status")}: {GetDiplomacyStatusText(status)}\n" +
            $"{_localization.T("ui.remaining_months")}: {remainingMonths}\n" +
            $"{_localization.T("ui.relation_score")}: {relationScore}";
    }

    private void UpdateDiplomacyConfirmButtonState()
    {
        if (_diplomacyConfirmButton == null)
        {
            return;
        }

        var actionType = GetSelectedDiplomacyActionType();
        var hasOfficer = GetSelectedTreeMetadataIds(_diplomacyOfficerList).Count > 0;
        var hasTarget = GetSelectedDiplomacyTargetFactionId() > 0;
        var hasGold = actionType is not (DiplomacyActionType.Gift or DiplomacyActionType.Demand) ||
                      (_diplomacyGoldSpinBox?.Value ?? 0) > 0;
        _diplomacyConfirmButton.Disabled = !hasOfficer || !hasTarget || !hasGold;
    }

    private void UpdateDiplomacyDialogInputState()
    {
        if (_localization == null)
        {
            return;
        }

        var actionType = GetSelectedDiplomacyActionType();
        SetDiplomacyRowVisible("DurationRow", actionType is DiplomacyActionType.Alliance or DiplomacyActionType.Truce);
        SetDiplomacyRowVisible("GoldRow", actionType is DiplomacyActionType.Gift or DiplomacyActionType.Demand);
        SetDiplomacyDialogLabelText(
            "GoldLabel",
            actionType == DiplomacyActionType.Demand
                ? _localization.T("ui.diplomacy_demand_gold")
                : _localization.T("ui.diplomacy_gift_gold"));
    }

    private void SetDiplomacyRowVisible(string rowName, bool visible)
    {
        var root = _diplomacyDialog?.GetNodeOrNull<Control>("DiplomacyDialogRoot");
        var row = root?.FindChild(rowName, true, false) as Control;
        if (row != null)
        {
            row.Visible = visible;
        }
    }

    private void SetDiplomacyWarning(string text)
    {
        if (_diplomacyWarningLabel != null)
        {
            _diplomacyWarningLabel.Text = text;
        }
    }

    private void OnDiplomacyConfirmPressed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null || _localization == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedTreeMetadataIds(_diplomacyOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            SetDiplomacyWarning(_localization.T("ui.select_officer_warning"));
            return;
        }

        var targetFactionId = GetSelectedDiplomacyTargetFactionId();
        if (targetFactionId <= 0)
        {
            SetDiplomacyWarning(_localization.T("ui.diplomacy_target_required_warning"));
            return;
        }

        var actionType = GetSelectedDiplomacyActionType();
        var gold = Math.Max(0, (int)Math.Round(_diplomacyGoldSpinBox?.Value ?? 0));
        if (actionType is DiplomacyActionType.Gift or DiplomacyActionType.Demand && gold <= 0)
        {
            SetDiplomacyWarning(_localization.T("ui.diplomacy_gold_required_warning"));
            return;
        }

        var result = _commandResolver.Execute(new CommandRequest
        {
            Type = CommandType.Diplomacy,
            ActorFactionId = _turnManager.GetPlayerFactionId(),
            SourceCityId = _selectedCity.Id,
            TargetFactionId = targetFactionId,
            OfficerIds = selectedOfficerIds,
            GoldToSend = gold,
            DurationMonths = Math.Max(1, (int)Math.Round(_diplomacyDurationSpinBox?.Value ?? 1)),
            DiplomacyActionType = actionType
        });

        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        if (result.Success)
        {
            _diplomacyDialog?.Hide();
            RefreshSelectedCity();
            return;
        }

        SetDiplomacyWarning(GetLocalizedResultMessage(result));
    }
}
