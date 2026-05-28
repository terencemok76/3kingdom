using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    internal Control? ViewOfficerListDialog
    {
        get => _officerListDialog;
        set => _officerListDialog = value;
    }

    internal HBoxContainer? ViewOfficerListToolbar
    {
        get => _officerListToolbar;
        set => _officerListToolbar = value;
    }

    internal HBoxContainer? ViewOfficerListAuxRow
    {
        get => _officerListAuxRow;
        set => _officerListAuxRow = value;
    }

    internal Label? ViewOfficerListAuxLabel
    {
        get => _officerListAuxLabel;
        set => _officerListAuxLabel = value;
    }

    internal OptionButton? ViewOfficerListAuxOption
    {
        get => _officerListAuxOption;
        set => _officerListAuxOption = value;
    }

    internal Button? ViewCityOfficersButton
    {
        get => _viewCityOfficersDialogButton;
        set => _viewCityOfficersDialogButton = value;
    }

    internal Button? ViewFactionOfficersButton
    {
        get => _viewFactionOfficersDialogButton;
        set => _viewFactionOfficersDialogButton = value;
    }

    internal Button? ViewFactionItemsButton
    {
        get => _viewFactionItemsDialogButton;
        set => _viewFactionItemsDialogButton = value;
    }

    internal Button? ViewDiplomacyRelationsButton
    {
        get => _viewDiplomacyRelationsDialogButton;
        set => _viewDiplomacyRelationsDialogButton = value;
    }

    internal Button? ViewCitiesButton
    {
        get => _viewCitiesDialogButton;
        set => _viewCitiesDialogButton = value;
    }

    internal Button? ViewOfficerListConfirmButton
    {
        get => _officerListConfirmButton;
        set => _officerListConfirmButton = value;
    }

    internal OptionButton? ViewCityListFilterOption
    {
        get => _cityListFilterOption;
        set => _cityListFilterOption = value;
    }

    internal OptionButton? ViewOfficerSortOption
    {
        get => _officerSortOption;
        set => _officerSortOption = value;
    }

    internal Tree? ViewOfficerListTable
    {
        get => _officerListTable;
        set => _officerListTable = value;
    }

    internal Control? ViewOfficerDetailDialog
    {
        get => _officerDetailDialog;
        set => _officerDetailDialog = value;
    }

    internal TextureRect? ViewOfficerPortraitRect
    {
        get => _officerPortraitRect;
        set => _officerPortraitRect = value;
    }

    internal Label? ViewOfficerPortraitPlaceholderLabel
    {
        get => _officerPortraitPlaceholderLabel;
        set => _officerPortraitPlaceholderLabel = value;
    }

    internal RichTextLabel? ViewOfficerDetailText
    {
        get => _officerDetailText;
        set => _officerDetailText = value;
    }

    internal void ViewApplyOfficerListTheme() => ApplyOfficerListDialogTheme();
    internal void ViewPlayUiClickSfx() => PlayUiClickSfx();
    internal void ViewEnsureOfficerDetailWidgets() => EnsureOfficerDetailWidgets();
    internal void ViewCloseOfficerListDialog() => CloseOfficerListDialog();
    internal void ViewHandleOfficerListAuxSelected(long index) => OnOfficerListAuxOptionSelected(index);
    internal void ViewHandleCityListFilterSelected(long index) => OnCityListFilterOptionSelected(index);
    internal void ViewHandleOfficerSortSelected(long index) => OnOfficerSortOptionSelected(index);
    internal void ViewHandleOfficerListColumnTitleClicked(long column, long mouseButtonIndex) => OnOfficerListTableColumnTitleClicked(column, mouseButtonIndex);
    internal void ViewHandleOfficerListConfirmPressed() => OnOfficerListDialogConfirmed();
    internal string ViewUnknownInfoText => UnknownInfoText;
    internal bool ViewCanOpenMainDialog() => _selectedCity != null && _turnManager?.World != null && _officerListDialog != null && _officerListTable != null;
    internal bool ViewIsOfficerListInViewMode() => _officerListMode == OfficerListMode.View;
    internal bool ViewIsOfficerListSelectionOnlyMode() => _officerListMode is OfficerListMode.CommandSelection or OfficerListMode.GenericSelection;
    internal bool ViewIsOfficerListShowingNonOfficerContent() => _officerListContentMode is OfficerListContentMode.Cities or OfficerListContentMode.Items or OfficerListContentMode.DiplomacyRelations;
    internal bool ViewIsOfficerListShowingCityContent() => _turnManager?.World != null && _officerListTable != null && _officerListContentMode == OfficerListContentMode.Cities;
    internal TreeItem? ViewGetSelectedOfficerListItem() => _officerListTable?.GetSelected();
    internal OfficerData? ViewGetOfficerById(int officerId) => _turnManager?.World?.GetOfficer(officerId);
    internal void ViewApplyOfficerListSelectionHighlight(TreeItem item) => ApplyViewTableSelectionHighlight(item);
    internal void ViewRefreshOfficerListRowStriping() => RefreshViewTableRowStriping();
    internal void ViewSetOfficerListModeToView() => _officerListMode = OfficerListMode.View;
    internal void ViewResetOfficerListDialogLayoutToSceneDefaults() => ResetOfficerListDialogLayoutToSceneDefaults();
    internal void ViewHideOfficerListAuxRow()
    {
        if (_officerListAuxRow != null)
        {
            _officerListAuxRow.Visible = false;
        }
    }

    internal void ViewSetOfficerListContentToCityOfficers()
    {
        _officerListScope = OfficerListScope.City;
        _officerListContentMode = OfficerListContentMode.Officers;
    }

    internal void ViewSetOfficerListContentToFactionOfficers()
    {
        _officerListScope = OfficerListScope.Faction;
        _officerListContentMode = OfficerListContentMode.Officers;
    }

    internal void ViewSetOfficerListContentToFactionItems()
    {
        _officerListScope = OfficerListScope.Faction;
        _officerListContentMode = OfficerListContentMode.Items;
    }

    internal void ViewSetOfficerListContentToDiplomacyRelations()
    {
        _officerListScope = OfficerListScope.Faction;
        _officerListContentMode = OfficerListContentMode.DiplomacyRelations;
    }

    internal void ViewSetOfficerListContentToCities() => _officerListContentMode = OfficerListContentMode.Cities;

    internal void ViewSetOfficerListConfirmButtonToDefaultText()
    {
        if (_officerListConfirmButton != null)
        {
            _officerListConfirmButton.Text = _localization?.T("ui.confirm_officer_selection") ?? "Confirm Selection";
        }
    }

    internal void ViewPopupOfficerListDialog()
    {
        if (_officerListDialog == null)
        {
            return;
        }

        _officerListDialog.Show();
        _officerListDialog.MoveToFront();
    }
    internal string ViewGetOfficerDetailTitle() => _localization?.T("ui.officer_detail") ?? "Officer Details";
    internal string ViewBuildOfficerDetailText(OfficerData officer) => BuildOfficerDetailText(officer);
    internal bool ViewCanViewOfficerFullInformation(OfficerData officer) => CanViewOfficerFullInformation(officer);
    internal Texture2D? ViewBuildOfficerPortraitTexture(int officerId) => BuildOfficerPortraitTexture(officerId);
    internal string ViewGetOfficerDisplayName(OfficerData officer) => _localization?.GetOfficerName(officer) ?? officer.Name;
    internal string ViewGetPortraitLabel() => _localization?.T("ui.portrait") ?? "Portrait";
    internal LocalizationService? ViewLocalization => _localization;
    internal WorldState? ViewWorld => _turnManager?.World;
    internal CityData? ViewSelectedCity => _selectedCity;
    internal int ViewGetCityListFilterIndex() => _cityListFilterMode switch
    {
        CityListFilterMode.OtherFactions => 1,
        CityListFilterMode.AllCities => 2,
        _ => 0
    };

    internal int ViewGetOfficerSortIndex() => _officerSortMode switch
    {
        OfficerSortMode.Intelligence => 1,
        OfficerSortMode.Status => 2,
        _ => 0
    };

    internal bool ViewCanInspectSelectedFaction() => CanInspectSelectedFaction();
    internal bool ViewIsOfficerListShowingCityOfficers() => _officerListContentMode == OfficerListContentMode.Officers && _officerListScope == OfficerListScope.City;
    internal bool ViewIsOfficerListShowingFactionOfficers() => _officerListContentMode == OfficerListContentMode.Officers && _officerListScope == OfficerListScope.Faction;
    internal bool ViewIsOfficerListShowingItems() => _officerListContentMode == OfficerListContentMode.Items;
    internal bool ViewIsOfficerListShowingDiplomacyRelations() => _officerListContentMode == OfficerListContentMode.DiplomacyRelations;
    internal bool ViewIsOfficerListFactionScope() => _officerListScope == OfficerListScope.Faction;
    internal string ViewGetCityListDialogTitle()
    {
        if (_localization == null)
        {
            return string.Empty;
        }

        return _cityListFilterMode switch
        {
            CityListFilterMode.OtherFactions => _localization.T("ui.view_title.cities_other"),
            CityListFilterMode.AllCities => _localization.T("ui.view_title.cities_all"),
            _ => _localization.T("ui.view_title.cities_self")
        };
    }

    internal void ViewSetOfficerListDialogTitle(string title)
    {
        var titleLabel = _officerListDialog?.GetNodeOrNull<Label>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar/TitleLabel");
        if (titleLabel != null)
        {
            titleLabel.Text = title;
        }
    }

    internal string ViewGetNoCityInScopeMessage() => _localization?.T("ui.no_city_in_scope") ?? "No cities available in this view.";
    internal string ViewGetNoItemInFactionMessage() => _localization?.T("ui.no_item_in_faction") ?? "No items available in this faction.";
    internal string ViewGetNoDiplomacyRelationMessage() => _localization?.T("ui.no_diplomacy_relation_in_faction") ?? "No diplomacy relations available in this faction.";
    internal TreeItem? ViewPrepareOfficerListTableRoot()
    {
        if (_selectedCity == null || _turnManager?.World == null || _officerListTable == null)
        {
            return null;
        }

        _officerListTable.Clear();
        ConfigureViewTableColumns();
        return _officerListTable.CreateItem();
    }

    internal int ViewPopulateCityRows(TreeItem root)
    {
        if (_officerListTable == null)
        {
            return 0;
        }

        var cities = GetFilteredCities();
        for (var index = 0; index < cities.Count; index += 1)
        {
            var row = _officerListTable.CreateItem(root);
            PopulateCityTableRow(row, cities[index]);
            ApplyViewTableRowStriping(row, index, 10);
        }

        return cities.Count;
    }

    internal int ViewPopulateItemRows(TreeItem root)
    {
        if (_officerListTable == null)
        {
            return 0;
        }

        var items = GetSortedFactionInventoryItems();
        for (var index = 0; index < items.Count; index += 1)
        {
            var row = _officerListTable.CreateItem(root);
            PopulateItemTableRow(row, items[index]);
            ApplyViewTableRowStriping(row, index, _officerListTable.Columns);
        }

        return items.Count;
    }

    internal int ViewPopulateDiplomacyRelationRows(TreeItem root)
    {
        if (_officerListTable == null)
        {
            return 0;
        }

        var relations = GetSortedDiplomacyRelations();
        for (var index = 0; index < relations.Count; index += 1)
        {
            var row = _officerListTable.CreateItem(root);
            PopulateDiplomacyRelationTableRow(row, relations[index]);
            ApplyViewTableRowStriping(row, index, _officerListTable.Columns);
        }

        return relations.Count;
    }

    internal (List<OfficerData> Officers, bool IncludeCityName, string EmptyMessage)? ViewGetOfficerListRowsData()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return null;
        }

        var officers = new List<OfficerData>();
        string emptyMessage;
        var includeCityName = false;
        if (_officerListMode == OfficerListMode.View && _officerListScope == OfficerListScope.Faction)
        {
            var faction = _turnManager.World.GetFaction(_selectedCity.OwnerFactionId);
            if (faction != null)
            {
                foreach (var officerId in faction.OfficerIds)
                {
                    var officer = _turnManager.World.GetOfficer(officerId);
                    if (officer != null)
                    {
                        officers.Add(officer);
                    }
                }
            }

            emptyMessage = _localization?.T("ui.no_officer_in_faction") ?? "No officers available in this faction.";
            includeCityName = true;
        }
        else
        {
            if (!CanViewCityFullInformation(_selectedCity))
            {
                return null;
            }

            foreach (var officerId in _selectedCity.OfficerIds)
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer != null)
                {
                    officers.Add(officer);
                }
            }

            foreach (var officer in _turnManager.World.Officers)
            {
                if (officer.CityId == _selectedCity.Id && FreeOfficerMovement.IsVisibleFreeOfficer(_turnManager.World, officer))
                {
                    officers.Add(officer);
                }
            }

            emptyMessage = _localization?.T("ui.no_officer_in_city") ?? "No officers available in this city.";
        }

        return (GetSortedOfficers(officers).ToList(), includeCityName, emptyMessage);
    }

    internal void ViewPopulateOfficerRow(TreeItem row, OfficerData officer, bool includeCityName) => PopulateOfficerTableRow(row, officer, includeCityName);
    internal void ViewApplyOfficerListRowStriping(TreeItem row, int rowIndex, int columnCount) => ApplyViewTableRowStriping(row, rowIndex, columnCount);
    internal bool ViewSelectCityById(int cityId)
    {
        if (_turnManager?.World == null)
        {
            return false;
        }

        var city = _turnManager.World.GetCity(cityId);
        if (city == null)
        {
            return false;
        }

        _selectedCity = city;
        RefreshSelectedCity();
        _mapController?.SelectCityById(city.Id);
        return true;
    }

    internal void ViewRefreshDialogText()
    {
        if (_localization == null)
        {
            return;
        }

        if (_officerListConfirmButton != null)
        {
            _officerListConfirmButton.Text = _localization.T("ui.confirm_officer_selection");
        }

        if (_officerListAuxRow?.Visible == true && _pendingOfficerCommand == CommandType.Recruit)
        {
            ConfigureOfficerListAuxRow(CommandType.Recruit);
        }

        var detailTitleLabel = _officerDetailDialog?.GetNodeOrNull<Label>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar/TitleLabel");
        if (detailTitleLabel != null)
        {
            detailTitleLabel.Text = _localization.T("ui.officer_detail");
        }

        if (_officerPortraitPlaceholderLabel != null && (_officerDetailDialog == null || !_officerDetailDialog.Visible))
        {
            _officerPortraitPlaceholderLabel.Visible = true;
            _officerPortraitPlaceholderLabel.Text = _localization.T("ui.portrait_pending_asset");
        }
    }
}
