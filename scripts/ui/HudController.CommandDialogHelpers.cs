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
    private static List<int> GetSelectedItemMetadataIds(ItemList? itemList)
    {
        var result = new List<int>();
        if (itemList == null)
        {
            return result;
        }

        for (var index = 0; index < itemList.ItemCount; index += 1)
        {
            if (!itemList.IsSelected(index))
            {
                continue;
            }

            var metadata = itemList.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int)
            {
                result.Add(metadata.AsInt32());
            }
        }

        return result;
    }

    private HashSet<int> GetAvailableOfficerIdsForOrder()
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return new HashSet<int>();
        }

        var result = new HashSet<int>();
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

            result.Add(officerId);
        }

        return result;
    }

    private bool HasActiveInternalAffairsSchedule(int officerId)
    {
        var world = _turnManager?.World;
        if (world == null)
        {
            return false;
        }

        return world.InternalAffairsSchedules.Any(schedule =>
            schedule.State == InternalAffairsScheduleState.Active &&
            schedule.OfficerId == officerId);
    }

    private void ShowOfficerCommandDialog(CommandType commandType)
    {
        if (_selectedCity == null || _turnManager?.World == null || _officerListDialog == null || _officerListView == null || _localization == null)
        {
            return;
        }

        var availableOfficerIds = GetAvailableOfficerIdsForOrder();
        if (availableOfficerIds.Count == 0)
        {
            AddLog(_localization.Format("ui.no_available_officer_for_command", GetCommandName(commandType)));
            return;
        }

        _pendingOfficerCommand = commandType;
        _officerListMode = OfficerListMode.CommandSelection;
        SetOfficerListDialogTitle(_localization.Format("fmt.select_officer_for_command", GetCommandName(commandType)));
        _officerListDialog.OkButtonText = _localization.T("ui.confirm_officer_selection");
        if (_officerListConfirmButton != null)
        {
            _officerListConfirmButton.Text = _localization.T("ui.confirm_officer_selection");
        }
        UpdateOfficerListToolbar();
        if (_officerListTable != null)
        {
            _officerListTable.Visible = false;
        }
        _officerListView.Visible = true;
        _officerListView.Clear();

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

            var itemIndex = _officerListView.AddItem(BuildOfficerListRowText(officer));
            _officerListView.SetItemMetadata(itemIndex, officer.Id);
        }

        if (_officerListView.ItemCount == 0)
        {
            AddLog(_localization.Format("ui.no_available_officer_for_command", GetCommandName(commandType)));
            return;
        }

        _officerListDialog.PopupCentered(new Vector2I(420, 320));
    }

    private string GetCommandName(CommandType commandType)
    {
        if (_localization == null)
        {
            return commandType.ToString();
        }

        return commandType switch
        {
            CommandType.Develop => _localization.T("ui.develop"),
            CommandType.Recruit => _localization.T("ui.recruit"),
            CommandType.Search => _localization.T("ui.search"),
            _ => commandType.ToString()
        };
    }



}
