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
    private void EnsureInternalAffairsDialogWidgets()
    {
        if (_internalAffairsDialog == null)
        {
            return;
        }

        var existingRoot = _internalAffairsDialog.GetNodeOrNull<VBoxContainer>("InternalAffairsDialogRoot");
        if (existingRoot != null)
        {
            _internalAffairsJobOption = existingRoot.GetNodeOrNull<OptionButton>("JobRow/JobOption");
            _internalAffairsDurationSpinBox = existingRoot.GetNodeOrNull<SpinBox>("DurationRow/DurationSpinBox");
            _internalAffairsOfficerList = existingRoot.GetNodeOrNull<Tree>("OfficerTable");
            _internalAffairsScheduleList = existingRoot.GetNodeOrNull<ItemList>("ScheduleList");
            _internalAffairsTerminateButton = existingRoot.GetNodeOrNull<Button>("TerminateButton");
            _internalAffairsWarningLabel = existingRoot.GetNodeOrNull<Label>("WarningLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "InternalAffairsDialogRoot",
            CustomMinimumSize = new Vector2(460.0f, 520.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _internalAffairsDialog.AddChild(root);

        var jobRow = new HBoxContainer
        {
            Name = "JobRow",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        jobRow.AddThemeConstantOverride("separation", 8);
        var jobLabel = new Label
        {
            Name = "JobLabel",
            CustomMinimumSize = new Vector2(84.0f, 0.0f)
        };
        jobRow.AddChild(jobLabel);
        _internalAffairsJobOption = new OptionButton
        {
            Name = "JobOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        jobRow.AddChild(_internalAffairsJobOption);
        root.AddChild(jobRow);

        var durationRow = new HBoxContainer
        {
            Name = "DurationRow",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        durationRow.AddThemeConstantOverride("separation", 8);
        var durationLabel = new Label
        {
            Name = "DurationLabel",
            CustomMinimumSize = new Vector2(84.0f, 0.0f)
        };
        durationRow.AddChild(durationLabel);
        _internalAffairsDurationSpinBox = new SpinBox
        {
            Name = "DurationSpinBox",
            MinValue = 1,
            MaxValue = 24,
            Step = 1,
            Value = 3,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        durationRow.AddChild(_internalAffairsDurationSpinBox);
        root.AddChild(durationRow);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _internalAffairsOfficerList = new Tree
        {
            Name = "OfficerTable",
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0.0f, 130.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _internalAffairsOfficerList.ItemSelected += OnInternalAffairsOfficerTableSelected;
        root.AddChild(_internalAffairsOfficerList);

        root.AddChild(new Label { Name = "ScheduleListLabel" });
        _internalAffairsScheduleList = new ItemList
        {
            Name = "ScheduleList",
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0.0f, 130.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_internalAffairsScheduleList);

        _internalAffairsTerminateButton = new Button
        {
            Name = "TerminateButton",
            FocusMode = Control.FocusModeEnum.None
        };
        _internalAffairsTerminateButton.Pressed += OnInternalAffairsTerminatePressed;
        root.AddChild(_internalAffairsTerminateButton);

        _internalAffairsWarningLabel = new Label
        {
            Name = "WarningLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _internalAffairsWarningLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.2f, 0.15f, 1.0f));
        root.AddChild(_internalAffairsWarningLabel);
    }

    private void ShowInternalAffairsDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _internalAffairsDialog == null || _localization == null)
        {
            return;
        }

        EnsureInternalAffairsDialogWidgets();
        UpdateInternalAffairsDialogText();
        PopulateInternalAffairsDialog();
        SetInternalAffairsWarning(string.Empty);
        _internalAffairsDialog.PopupCentered(new Vector2I(500, 560));
    }

    private void PopulateInternalAffairsDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        if (_internalAffairsJobOption != null)
        {
            _internalAffairsJobOption.Clear();
            AddInternalAffairsJobOption(InternalAffairsJobType.Farm);
            AddInternalAffairsJobOption(InternalAffairsJobType.Commercial);
            AddInternalAffairsJobOption(InternalAffairsJobType.Defend);
            AddInternalAffairsJobOption(InternalAffairsJobType.WaterControl);
            AddInternalAffairsJobOption(InternalAffairsJobType.Construction);
        }

        if (_internalAffairsOfficerList != null)
        {
            _internalAffairsOfficerList.Clear();
            ConfigureInternalAffairsOfficerTableColumns();
            var tableRoot = _internalAffairsOfficerList.CreateItem();
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

                var row = _internalAffairsOfficerList.CreateItem(tableRoot);
                PopulateInternalAffairsOfficerTableRow(row, officer, rowIndex);
                rowIndex += 1;
            }
        }

        RefreshInternalAffairsScheduleList();
    }

    private void OnInternalAffairsOfficerTableSelected()
    {
        if (_internalAffairsOfficerList == null)
        {
            return;
        }

        var selectedItem = _internalAffairsOfficerList.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        var root = _internalAffairsOfficerList.GetRoot();
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
                ApplyViewTableSelectedRowStyle(row, _internalAffairsOfficerList.Columns);
            }
            else
            {
                ApplyViewTableRowStriping(row, rowIndex, _internalAffairsOfficerList.Columns);
            }

            row = row.GetNext();
            rowIndex += 1;
        }
    }

    private void AddInternalAffairsJobOption(InternalAffairsJobType jobType)
    {
        if (_internalAffairsJobOption == null)
        {
            return;
        }

        _internalAffairsJobOption.AddItem(GetInternalAffairsJobName(jobType));
        _internalAffairsJobOption.SetItemMetadata(_internalAffairsJobOption.ItemCount - 1, (int)jobType);
    }

    private void RefreshInternalAffairsScheduleList()
    {
        if (_selectedCity == null || _turnManager?.World == null || _internalAffairsScheduleList == null || _localization == null)
        {
            return;
        }

        _internalAffairsScheduleList.Clear();
        foreach (var schedule in _turnManager.World.InternalAffairsSchedules)
        {
            if (schedule.State != InternalAffairsScheduleState.Active || schedule.CityId != _selectedCity.Id)
            {
                continue;
            }

            var officer = _turnManager.World.GetOfficer(schedule.OfficerId);
            var officerName = officer != null ? _localization.GetOfficerName(officer) : "-";
            var itemIndex = _internalAffairsScheduleList.AddItem(
                _localization.Format("fmt.internal_affairs_schedule_row", GetInternalAffairsJobName(schedule.JobType), officerName, schedule.RemainingMonths));
            _internalAffairsScheduleList.SetItemMetadata(itemIndex, schedule.Id);
        }
    }

    private void UpdateInternalAffairsDialogText()
    {
        if (_internalAffairsDialog == null || _localization == null)
        {
            return;
        }

        _internalAffairsDialog.Title = _localization.T("ui.internal_affairs");
        _internalAffairsDialog.OkButtonText = _localization.T("ui.confirm_internal_affairs");
        SetInternalAffairsDialogLabelText("JobLabel", _localization.T("ui.internal_affairs_job"));
        SetInternalAffairsDialogLabelText("DurationLabel", _localization.T("ui.internal_affairs_duration"));
        SetInternalAffairsDialogLabelText("OfficerListLabel", _localization.T("ui.internal_affairs_officer"));
        SetInternalAffairsDialogLabelText("ScheduleListLabel", _localization.T("ui.internal_affairs_active_schedules"));
        if (_internalAffairsTerminateButton != null)
        {
            _internalAffairsTerminateButton.Text = _localization.T("ui.terminate_internal_affairs");
        }
    }

    private void SetInternalAffairsDialogLabelText(string nodeName, string text)
    {
        var root = _internalAffairsDialog?.GetNodeOrNull<Control>("InternalAffairsDialogRoot");
        var label = root?.FindChild(nodeName, recursive: true, owned: false) as Label;
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void ConfigureInternalAffairsOfficerTableColumns()
    {
        if (_internalAffairsOfficerList == null || _localization == null)
        {
            return;
        }

        _internalAffairsOfficerList.Columns = 4;
        _internalAffairsOfficerList.SetColumnTitle(0, _localization.T("ui.officers"));
        _internalAffairsOfficerList.SetColumnCustomMinimumWidth(0, 130);
        _internalAffairsOfficerList.SetColumnTitle(1, _localization.T("ui.role"));
        _internalAffairsOfficerList.SetColumnCustomMinimumWidth(1, 100);
        _internalAffairsOfficerList.SetColumnTitle(2, _localization.T("ui.status"));
        _internalAffairsOfficerList.SetColumnCustomMinimumWidth(2, 100);
        _internalAffairsOfficerList.SetColumnTitle(3, _localization.T("ui.politics"));
        _internalAffairsOfficerList.SetColumnCustomMinimumWidth(3, 80);
    }

    private void PopulateInternalAffairsOfficerTableRow(TreeItem row, OfficerData officer, int rowIndex)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return;
        }

        row.SetMetadata(0, officer.Id);
        row.SetText(0, _localization.GetOfficerName(officer));
        row.SetText(1, _localization.GetOfficerRole(officer));
        row.SetText(2, _localization.GetOfficerStatus(_turnManager.World, officer));
        row.SetText(3, officer.Politics.ToString());
        ApplyViewTableRowStriping(row, rowIndex, 4);
    }

    private void OnInternalAffairsDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager?.World == null || _commandResolver == null || _localization == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedTreeMetadataIds(_internalAffairsOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            SetInternalAffairsWarning(_localization.T("ui.select_officer_warning"));
            ReopenInternalAffairsDialog();
            return;
        }

        var jobType = GetSelectedInternalAffairsJobType();
        var months = Math.Max(1, (int)(_internalAffairsDurationSpinBox?.Value ?? 1));
        var result = _commandResolver.ScheduleInternalAffairs(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            selectedOfficerIds[0],
            jobType,
            months);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        RefreshInternalAffairsScheduleList();
        _mapController?.RefreshVisuals();
    }

    private void OnInternalAffairsTerminatePressed()
    {
        if (_turnManager?.World == null || _commandResolver == null || _localization == null)
        {
            return;
        }

        var selectedIds = GetSelectedItemMetadataIds(_internalAffairsScheduleList);
        if (selectedIds.Count == 0)
        {
            SetInternalAffairsWarning(_localization.T("ui.select_internal_affairs_schedule_warning"));
            return;
        }

        var result = _commandResolver.TerminateInternalAffairsSchedule(_turnManager.GetPlayerFactionId(), selectedIds[0]);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        PopulateInternalAffairsDialog();
    }

    private InternalAffairsJobType GetSelectedInternalAffairsJobType()
    {
        if (_internalAffairsJobOption == null)
        {
            return InternalAffairsJobType.Farm;
        }

        var metadata = _internalAffairsJobOption.GetItemMetadata(_internalAffairsJobOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (InternalAffairsJobType)metadata.AsInt32()
            : InternalAffairsJobType.Farm;
    }

    private string GetInternalAffairsJobName(InternalAffairsJobType jobType)
    {
        if (_localization == null)
        {
            return jobType.ToString();
        }

        return jobType switch
        {
            InternalAffairsJobType.Farm => _localization.T("command.internal_affairs.farm"),
            InternalAffairsJobType.Commercial => _localization.T("command.internal_affairs.commercial"),
            InternalAffairsJobType.Defend => _localization.T("command.internal_affairs.defend"),
            InternalAffairsJobType.WaterControl => _localization.T("command.internal_affairs.disaster_prevention"),
            InternalAffairsJobType.Construction => _localization.T("command.internal_affairs.construction"),
            _ => jobType.ToString()
        };
    }

    private void SetInternalAffairsWarning(string text)
    {
        if (_internalAffairsWarningLabel != null)
        {
            _internalAffairsWarningLabel.Text = text;
        }
    }

    private void ReopenInternalAffairsDialog()
    {
        CallDeferred(nameof(ReopenInternalAffairsDialogDeferred));
    }

    private void ReopenInternalAffairsDialogDeferred()
    {
        _internalAffairsDialog?.PopupCentered(new Vector2I(500, 560));
    }


}
