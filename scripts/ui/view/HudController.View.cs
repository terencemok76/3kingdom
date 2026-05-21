using System.Collections.Generic;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController : CanvasLayer
{
    private void OnOfficerListTableColumnTitleClicked(long column, long mouseButtonIndex)
    {
        if (_officerListMode != OfficerListMode.View)
        {
            return;
        }

        var nextField = GetViewTableSortFieldForColumn((int)column);
        if (_viewTableSortField == nextField)
        {
            _viewTableSortAscending = !_viewTableSortAscending;
        }
        else
        {
            _viewTableSortField = nextField;
            _viewTableSortAscending = IsAscendingDefaultSortField(nextField);
        }

        _viewUiController?.RefreshOfficerListContent();
    }

    private void OnOfficerListDialogConfirmed()
    {
        if (_officerListMode != OfficerListMode.CommandSelection)
        {
            _officerListDialog?.Hide();
            return;
        }

        var selectedItem = _officerListTable?.GetSelected();
        if (selectedItem == null)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? "Select one officer first.");
            return;
        }

        var metadata = selectedItem.GetMetadata(0);
        if (metadata.VariantType != Variant.Type.Int)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? "Select one officer first.");
            return;
        }

        var result = ExecutePlayerCommand(
            _pendingOfficerCommand,
            officerIds: new List<int> { metadata.AsInt32() },
            recruitTroopType: _pendingOfficerCommand == CommandType.Recruit ? _pendingRecruitTroopType : TroopType.Infantry);
        if (result.Success)
        {
            _officerListDialog?.Hide();
        }
    }

    private void SetOfficerListDialogTitle(string title)
    {
        var titleLabel = _officerListDialog?.GetNodeOrNull<Label>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar/TitleLabel");
        if (titleLabel != null)
        {
            titleLabel.Text = title;
        }
    }
}
