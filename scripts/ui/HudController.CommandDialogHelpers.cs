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

    private static List<int> GetSelectedTreeMetadataIds(Tree? tree)
    {
        var result = new List<int>();
        var selectedItem = tree?.GetSelected();
        if (selectedItem == null)
        {
            return result;
        }

        var metadata = selectedItem.GetMetadata(0);
        if (metadata.VariantType == Variant.Type.Int)
        {
            result.Add(metadata.AsInt32());
        }

        return result;
    }

    private static List<int> GetCheckedTreeMetadataIds(Tree? tree, int metadataColumn = 1, int checkColumn = 0)
    {
        var result = new List<int>();
        var root = tree?.GetRoot();
        var row = root?.GetFirstChild();
        while (row != null)
        {
            if (row.IsChecked(checkColumn))
            {
                var metadata = row.GetMetadata(metadataColumn);
                if (metadata.VariantType == Variant.Type.Int)
                {
                    result.Add(metadata.AsInt32());
                }
            }

            row = row.GetNext();
        }

        return result;
    }

    private void ConfigureCompactOfficerTableColumns(Tree? tree, bool includeStatus = true, bool includeCity = false, bool includeLoyalty = true, bool includeStats = true, bool includeCheck = false)
    {
        if (tree == null || _localization == null)
        {
            return;
        }

        var column = 0;
        var totalColumns = (includeCheck ? 1 : 0) +
                           2 +
                           ((includeStatus || includeCity) ? 1 : 0) +
                           (includeLoyalty ? 1 : 0) +
                           (includeStats ? 3 : 0);
        tree.Columns = totalColumns;
        if (includeCheck)
        {
            tree.SetColumnTitle(column, string.Empty);
            tree.SetColumnCustomMinimumWidth(column, 32);
            tree.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
            column += 1;
        }

        tree.SetColumnTitle(column, _localization.T("ui.officers"));
        tree.SetColumnCustomMinimumWidth(column, 130);
        tree.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
        column += 1;
        tree.SetColumnTitle(column, _localization.T("ui.role"));
        tree.SetColumnCustomMinimumWidth(column, 90);
        tree.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
        column += 1;

        if (includeStatus)
        {
            tree.SetColumnTitle(column, _localization.T("ui.status"));
            tree.SetColumnCustomMinimumWidth(column, 90);
            tree.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
            column += 1;
        }
        else if (includeCity)
        {
            tree.SetColumnTitle(column, _localization.T("ui.city"));
            tree.SetColumnCustomMinimumWidth(column, 100);
            tree.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
            column += 1;
        }

        if (includeLoyalty)
        {
            tree.SetColumnTitle(column, _localization.T("ui.loyalty"));
            tree.SetColumnCustomMinimumWidth(column, 70);
            tree.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
            column += 1;
        }

        if (includeStats)
        {
            tree.SetColumnTitle(column, _localization.T("ui.strength"));
            tree.SetColumnCustomMinimumWidth(column, 70);
            tree.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
            column += 1;
            tree.SetColumnTitle(column, _localization.T("ui.intelligence"));
            tree.SetColumnCustomMinimumWidth(column, 70);
            tree.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
            column += 1;
            tree.SetColumnTitle(column, _localization.T("ui.charm"));
            tree.SetColumnCustomMinimumWidth(column, 70);
            tree.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
        }
    }

    private void PopulateCompactOfficerTableRow(TreeItem row, OfficerData officer, int rowIndex, bool includeStatus = true, bool includeCity = false, bool includeLoyalty = true, bool includeStats = true, bool includeCheck = false)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return;
        }

        var world = _turnManager.World;
        var column = 0;
        if (includeCheck)
        {
            row.SetCellMode(column, TreeItem.TreeCellMode.Check);
            row.SetEditable(column, true);
            row.SetChecked(column, false);
            column += 1;
        }

        row.SetMetadata(includeCheck ? 1 : 0, officer.Id);
        row.SetText(column, _localization.GetOfficerName(officer));
        column += 1;
        row.SetText(column, _localization.GetOfficerRole(officer));
        column += 1;

        if (includeStatus)
        {
            row.SetText(column, _localization.GetOfficerStatus(world, officer));
            column += 1;
        }
        else if (includeCity)
        {
            var city = world.GetCity(officer.CityId);
            row.SetText(column, city != null ? _localization.GetCityName(city) : "-");
            column += 1;
        }

        if (includeLoyalty)
        {
            row.SetText(column, BuildOfficerLoyaltyTableText(world, officer));
            column += 1;
        }

        if (includeStats)
        {
            row.SetText(column, officer.Strength.ToString());
            column += 1;
            row.SetText(column, officer.Intelligence.ToString());
            column += 1;
            row.SetText(column, officer.Charm.ToString());
        }

        var totalColumns = (includeCheck ? 1 : 0) +
                           2 +
                           ((includeStatus || includeCity) ? 1 : 0) +
                           (includeLoyalty ? 1 : 0) +
                           (includeStats ? 3 : 0);
        ApplyViewTableRowStriping(row, rowIndex, totalColumns);
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
        if (_selectedCity == null || _turnManager?.World == null || _officerListDialog == null || _officerListTable == null || _localization == null)
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
        _pendingRecruitTroopType = TroopType.Infantry;
        _officerListMode = OfficerListMode.CommandSelection;
        ConfigureOfficerListDialogLayout(isCommandSelection: true);
        SetOfficerListDialogTitle(_localization.Format("fmt.select_officer_for_command", GetCommandName(commandType)));
        _officerListDialog.OkButtonText = _localization.T("ui.confirm_officer_selection");
        if (_officerListConfirmButton != null)
        {
            _officerListConfirmButton.Text = _localization.T("ui.confirm_officer_selection");
        }
        UpdateOfficerListToolbar();
        _officerListTable.Visible = true;
        ConfigureOfficerListAuxRow(commandType);
        _officerListTable.Clear();
        var isRecruitCommand = commandType == CommandType.Recruit;
        if (isRecruitCommand)
        {
            ConfigureRecruitOfficerTableColumns();
        }
        else
        {
            ConfigureCompactOfficerTableColumns(_officerListTable);
        }
        var tableRoot = _officerListTable.CreateItem();
        var rowIndex = 0;

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

            var row = _officerListTable.CreateItem(tableRoot);
            if (isRecruitCommand)
            {
                PopulateRecruitOfficerTableRow(row, officer, rowIndex);
            }
            else
            {
                PopulateCompactOfficerTableRow(row, officer, rowIndex);
            }
            rowIndex += 1;
        }

        if (rowIndex == 0)
        {
            AddLog(_localization.Format("ui.no_available_officer_for_command", GetCommandName(commandType)));
            return;
        }

        var visibleRows = Math.Clamp(rowIndex, 1, 6);
        var popupHeight = 220 + visibleRows * 28 + (commandType == CommandType.Recruit ? 40 : 0);
        _officerListDialog.PopupCentered(new Vector2I(620, popupHeight));
    }

    private void ShowOfficerSelectorDialog(
        string title,
        IEnumerable<int> candidateOfficerIds,
        OfficerSelectorPrimaryStat primaryStat,
        Action<int> onConfirmed)
    {
        if (_turnManager?.World == null || _officerListDialog == null || _officerListTable == null || _localization == null)
        {
            return;
        }

        var candidates = candidateOfficerIds
            .Distinct()
            .Select(id => _turnManager.World.GetOfficer(id))
            .Where(officer => officer != null)
            .Cast<OfficerData>()
            .OrderByDescending(GetOfficerSelectorPrimaryStatValue)
            .ThenByDescending(officer => officer.Intelligence)
            .ThenByDescending(officer => officer.Politics)
            .ThenByDescending(officer => officer.Charm)
            .ThenBy(officer => _localization.GetOfficerName(officer))
            .ToList();
        if (candidates.Count == 0)
        {
            AddLog(_localization.T("ui.select_officer_warning"));
            return;
        }

        _genericOfficerSelectorCandidateIds.Clear();
        _genericOfficerSelectorCandidateIds.AddRange(candidates.Select(officer => officer.Id));
        _genericOfficerSelectorPrimaryStat = primaryStat;
        _genericOfficerSelectorConfirmedAction = onConfirmed;
        _officerListMode = OfficerListMode.GenericSelection;
        _pendingOfficerCommand = CommandType.Pass;

        ConfigureOfficerListDialogLayout(isCommandSelection: true);
        SetOfficerListDialogTitle(title);
        _officerListDialog.OkButtonText = _localization.T("ui.confirm_officer_selection");
        if (_officerListConfirmButton != null)
        {
            _officerListConfirmButton.Text = _localization.T("ui.confirm_officer_selection");
        }

        UpdateOfficerListToolbar();
        _officerListTable.Visible = true;
        if (_officerListAuxRow != null)
        {
            _officerListAuxRow.Visible = false;
        }

        _officerListTable.Clear();
        ConfigureOfficerSelectorTableColumns(primaryStat);
        var tableRoot = _officerListTable.CreateItem();
        for (var rowIndex = 0; rowIndex < candidates.Count; rowIndex += 1)
        {
            var row = _officerListTable.CreateItem(tableRoot);
            PopulateOfficerSelectorTableRow(row, candidates[rowIndex], rowIndex, primaryStat);
        }

        var visibleRows = Math.Clamp(candidates.Count, 1, 6);
        var popupHeight = 220 + visibleRows * 28;
        _officerListDialog.PopupCentered(new Vector2I(620, popupHeight));
    }

    private void ConfigureOfficerSelectorTableColumns(OfficerSelectorPrimaryStat primaryStat)
    {
        if (_officerListTable == null || _localization == null)
        {
            return;
        }

        _officerListTable.Columns = 4;
        _officerListTable.SetColumnTitle(0, _localization.T("ui.officers"));
        _officerListTable.SetColumnCustomMinimumWidth(0, 150);
        _officerListTable.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _officerListTable.SetColumnTitle(1, _localization.T("ui.role"));
        _officerListTable.SetColumnCustomMinimumWidth(1, 100);
        _officerListTable.SetColumnTitleAlignment(1, HorizontalAlignment.Left);
        _officerListTable.SetColumnTitle(2, _localization.T("ui.status"));
        _officerListTable.SetColumnCustomMinimumWidth(2, 100);
        _officerListTable.SetColumnTitleAlignment(2, HorizontalAlignment.Left);
        _officerListTable.SetColumnTitle(3, GetOfficerSelectorPrimaryStatTitle(primaryStat));
        _officerListTable.SetColumnCustomMinimumWidth(3, 90);
        _officerListTable.SetColumnTitleAlignment(3, HorizontalAlignment.Left);
    }

    private void PopulateOfficerSelectorTableRow(TreeItem row, OfficerData officer, int rowIndex, OfficerSelectorPrimaryStat primaryStat)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return;
        }

        row.SetMetadata(0, officer.Id);
        row.SetText(0, _localization.GetOfficerName(officer));
        row.SetText(1, _localization.GetOfficerRole(officer));
        row.SetText(2, _localization.GetOfficerStatus(_turnManager.World, officer));
        row.SetText(3, GetOfficerSelectorPrimaryStatValue(officer, primaryStat).ToString());
        ApplyViewTableRowStriping(row, rowIndex, 4);
    }

    private string GetOfficerSelectorPrimaryStatTitle(OfficerSelectorPrimaryStat primaryStat)
    {
        if (_localization == null)
        {
            return primaryStat.ToString();
        }

        return primaryStat switch
        {
            OfficerSelectorPrimaryStat.Politics => _localization.T("ui.politics"),
            OfficerSelectorPrimaryStat.Charm => _localization.T("ui.charm"),
            OfficerSelectorPrimaryStat.Intelligence => _localization.T("ui.intelligence"),
            _ => primaryStat.ToString()
        };
    }

    private int GetOfficerSelectorPrimaryStatValue(OfficerData officer)
    {
        return GetOfficerSelectorPrimaryStatValue(officer, _genericOfficerSelectorPrimaryStat);
    }

    private static int GetOfficerSelectorPrimaryStatValue(OfficerData officer, OfficerSelectorPrimaryStat primaryStat)
    {
        return primaryStat switch
        {
            OfficerSelectorPrimaryStat.Politics => officer.Politics,
            OfficerSelectorPrimaryStat.Charm => officer.Charm,
            OfficerSelectorPrimaryStat.Intelligence => officer.Intelligence,
            _ => officer.Politics
        };
    }

    private void ConfigureOfficerListAuxRow(CommandType commandType)
    {
        if (_officerListAuxRow == null || _officerListAuxLabel == null || _officerListAuxOption == null || _localization == null)
        {
            return;
        }

        var isRecruit = commandType == CommandType.Recruit;
        _officerListAuxRow.Visible = isRecruit;
        if (!isRecruit)
        {
            return;
        }

        _officerListAuxLabel.Text = _localization.T("ui.recruit_troop_type");
        _officerListAuxOption.Clear();
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
            _officerListAuxOption.AddItem(GetTroopTypeDisplayName(troopType));
            _officerListAuxOption.SetItemMetadata(_officerListAuxOption.ItemCount - 1, (int)troopType);
        }

        _officerListAuxOption.Select(0);
    }

    private string GetTroopTypeDisplayName(TroopType troopType)
    {
        if (_localization == null)
        {
            return troopType.ToString();
        }

        return troopType switch
        {
            TroopType.Infantry => _localization.T("troop_type.infantry"),
            TroopType.Spearman => _localization.T("troop_type.spearman"),
            TroopType.Cavalry => _localization.T("troop_type.cavalry"),
            TroopType.Archer => _localization.T("troop_type.archer"),
            TroopType.Crossbow => _localization.T("troop_type.crossbow"),
            TroopType.Siege => _localization.T("troop_type.siege"),
            _ => troopType.ToString()
        };
    }

    private void OnOfficerListAuxOptionSelected(long index)
    {
        if (_officerListAuxOption == null)
        {
            return;
        }

        var metadata = _officerListAuxOption.GetItemMetadata((int)index);
        if (metadata.VariantType == Variant.Type.Int)
        {
            _pendingRecruitTroopType = (TroopType)metadata.AsInt32();
        }
    }

    private void ConfigureRecruitOfficerTableColumns()
    {
        if (_officerListTable == null || _localization == null)
        {
            return;
        }

        _officerListTable.Columns = 4;
        _officerListTable.SetColumnTitle(0, _localization.T("ui.officers"));
        _officerListTable.SetColumnCustomMinimumWidth(0, 150);
        _officerListTable.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _officerListTable.SetColumnTitle(1, _localization.T("ui.role"));
        _officerListTable.SetColumnCustomMinimumWidth(1, 100);
        _officerListTable.SetColumnTitleAlignment(1, HorizontalAlignment.Left);
        _officerListTable.SetColumnTitle(2, _localization.T("ui.status"));
        _officerListTable.SetColumnCustomMinimumWidth(2, 100);
        _officerListTable.SetColumnTitleAlignment(2, HorizontalAlignment.Left);
        _officerListTable.SetColumnTitle(3, _localization.T("ui.charm"));
        _officerListTable.SetColumnCustomMinimumWidth(3, 80);
        _officerListTable.SetColumnTitleAlignment(3, HorizontalAlignment.Left);
    }

    private void PopulateRecruitOfficerTableRow(TreeItem row, OfficerData officer, int rowIndex)
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
            CommandType.Search => _localization.T("command.civil.investigate_people"),
            CommandType.CivilRelief => _localization.T("command.civil.relief"),
            CommandType.Spy => _localization.T("ui.spy"),
            _ => commandType.ToString()
        };
    }

    private void ConfigureOfficerListDialogLayout(bool isCommandSelection)
    {
        var root = _officerListDialog?.GetNodeOrNull<VBoxContainer>("OfficerListDialogRoot");
        var confirmRow = _officerListDialog?.GetNodeOrNull<CenterContainer>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListConfirmRow");
        if (root != null)
        {
            root.CustomMinimumSize = isCommandSelection
                ? new Vector2(420.0f, 220.0f)
                : new Vector2(420.0f, 280.0f);
        }

        if (_officerListTable != null)
        {
            _officerListTable.CustomMinimumSize = isCommandSelection
                ? new Vector2(560.0f, 180.0f)
                : new Vector2(920.0f, 260.0f);
        }

        if (confirmRow != null)
        {
            confirmRow.CustomMinimumSize = isCommandSelection
                ? new Vector2(0.0f, 28.0f)
                : new Vector2(0.0f, 34.0f);
        }
    }



}
