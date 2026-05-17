using System.Collections.Generic;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public sealed class ViewUiContext
{
    private readonly HudController _hud;

    public ViewUiContext(HudController hud)
    {
        _hud = hud;
    }

    internal Window? OfficerListDialog
    {
        get => _hud.ViewOfficerListDialog;
        set => _hud.ViewOfficerListDialog = value;
    }

    internal HBoxContainer? OfficerListToolbar
    {
        get => _hud.ViewOfficerListToolbar;
        set => _hud.ViewOfficerListToolbar = value;
    }

    internal HBoxContainer? OfficerListAuxRow
    {
        get => _hud.ViewOfficerListAuxRow;
        set => _hud.ViewOfficerListAuxRow = value;
    }

    internal Label? OfficerListAuxLabel
    {
        get => _hud.ViewOfficerListAuxLabel;
        set => _hud.ViewOfficerListAuxLabel = value;
    }

    internal OptionButton? OfficerListAuxOption
    {
        get => _hud.ViewOfficerListAuxOption;
        set => _hud.ViewOfficerListAuxOption = value;
    }

    internal Button? ViewCityOfficersButton
    {
        get => _hud.ViewCityOfficersButton;
        set => _hud.ViewCityOfficersButton = value;
    }

    internal Button? ViewFactionOfficersButton
    {
        get => _hud.ViewFactionOfficersButton;
        set => _hud.ViewFactionOfficersButton = value;
    }

    internal Button? ViewFactionItemsButton
    {
        get => _hud.ViewFactionItemsButton;
        set => _hud.ViewFactionItemsButton = value;
    }

    internal Button? ViewDiplomacyRelationsButton
    {
        get => _hud.ViewDiplomacyRelationsButton;
        set => _hud.ViewDiplomacyRelationsButton = value;
    }

    internal Button? ViewCitiesButton
    {
        get => _hud.ViewCitiesButton;
        set => _hud.ViewCitiesButton = value;
    }

    internal Button? OfficerListConfirmButton
    {
        get => _hud.ViewOfficerListConfirmButton;
        set => _hud.ViewOfficerListConfirmButton = value;
    }

    internal OptionButton? CityListFilterOption
    {
        get => _hud.ViewCityListFilterOption;
        set => _hud.ViewCityListFilterOption = value;
    }

    internal OptionButton? OfficerSortOption
    {
        get => _hud.ViewOfficerSortOption;
        set => _hud.ViewOfficerSortOption = value;
    }

    internal Tree? OfficerListTable
    {
        get => _hud.ViewOfficerListTable;
        set => _hud.ViewOfficerListTable = value;
    }

    internal Window? OfficerDetailDialog
    {
        get => _hud.ViewOfficerDetailDialog;
        set => _hud.ViewOfficerDetailDialog = value;
    }

    internal TextureRect? OfficerPortraitRect
    {
        get => _hud.ViewOfficerPortraitRect;
        set => _hud.ViewOfficerPortraitRect = value;
    }

    internal Label? OfficerPortraitPlaceholderLabel
    {
        get => _hud.ViewOfficerPortraitPlaceholderLabel;
        set => _hud.ViewOfficerPortraitPlaceholderLabel = value;
    }

    internal RichTextLabel? OfficerDetailText
    {
        get => _hud.ViewOfficerDetailText;
        set => _hud.ViewOfficerDetailText = value;
    }

    internal void AddChild(Node node) => _hud.AddChild(node);
    internal void PlayUiClickSfx() => _hud.ViewPlayUiClickSfx();
    internal void ApplyOfficerListTheme() => _hud.ViewApplyOfficerListTheme();
    internal void EnsureOfficerDetailWidgets() => _hud.ViewEnsureOfficerDetailWidgets();
    internal void RefreshDialogsText() => _hud.ViewRefreshDialogText();
    internal void HideOfficerListDialog() => _hud.ViewCloseOfficerListDialog();
    internal void HandleOfficerListAuxSelected(long index) => _hud.ViewHandleOfficerListAuxSelected(index);
    internal void HandleCityListFilterSelected(long index) => _hud.ViewHandleCityListFilterSelected(index);
    internal void HandleOfficerSortSelected(long index) => _hud.ViewHandleOfficerSortSelected(index);
    internal void HandleOfficerListColumnTitleClicked(long column, long mouseButtonIndex) => _hud.ViewHandleOfficerListColumnTitleClicked(column, mouseButtonIndex);
    internal void HandleOfficerListConfirmPressed() => _hud.ViewHandleOfficerListConfirmPressed();

    internal string UnknownInfoText => _hud.ViewUnknownInfoText;
    internal bool CanOpenMainViewDialog() => _hud.ViewCanOpenMainDialog();
    internal bool IsOfficerListInViewMode() => _hud.ViewIsOfficerListInViewMode();
    internal bool IsOfficerListSelectionOnlyMode() => _hud.ViewIsOfficerListSelectionOnlyMode();
    internal bool IsOfficerListShowingNonOfficerContent() => _hud.ViewIsOfficerListShowingNonOfficerContent();
    internal bool IsOfficerListShowingCityContent() => _hud.ViewIsOfficerListShowingCityContent();
    internal TreeItem? GetSelectedOfficerListItem() => _hud.ViewGetSelectedOfficerListItem();
    internal OfficerData? GetOfficerById(int officerId) => _hud.ViewGetOfficerById(officerId);
    internal void ApplyOfficerListSelectionHighlight(TreeItem item) => _hud.ViewApplyOfficerListSelectionHighlight(item);
    internal void SetOfficerListModeToView() => _hud.ViewSetOfficerListModeToView();
    internal void ResetOfficerListDialogLayoutToSceneDefaults() => _hud.ViewResetOfficerListDialogLayoutToSceneDefaults();
    internal void HideOfficerListAuxRow() => _hud.ViewHideOfficerListAuxRow();
    internal void SetOfficerListContentToCityOfficers() => _hud.ViewSetOfficerListContentToCityOfficers();
    internal void SetOfficerListContentToFactionOfficers() => _hud.ViewSetOfficerListContentToFactionOfficers();
    internal void SetOfficerListContentToFactionItems() => _hud.ViewSetOfficerListContentToFactionItems();
    internal void SetOfficerListContentToDiplomacyRelations() => _hud.ViewSetOfficerListContentToDiplomacyRelations();
    internal void SetOfficerListContentToCities() => _hud.ViewSetOfficerListContentToCities();
    internal void SetOfficerListConfirmButtonToDefaultText() => _hud.ViewSetOfficerListConfirmButtonToDefaultText();
    internal void PopupOfficerListDialog() => _hud.ViewPopupOfficerListDialog();
    internal bool SelectCityById(int cityId) => _hud.ViewSelectCityById(cityId);
    internal string GetOfficerDetailTitle() => _hud.ViewGetOfficerDetailTitle();
    internal string BuildOfficerDetailText(OfficerData officer) => _hud.ViewBuildOfficerDetailText(officer);
    internal bool CanViewOfficerFullInformation(OfficerData officer) => _hud.ViewCanViewOfficerFullInformation(officer);
    internal Texture2D? BuildOfficerPortraitTexture(int officerId) => _hud.ViewBuildOfficerPortraitTexture(officerId);
    internal string GetOfficerDisplayName(OfficerData officer) => _hud.ViewGetOfficerDisplayName(officer);
    internal string GetPortraitLabel() => _hud.ViewGetPortraitLabel();
    internal LocalizationService? Localization => _hud.ViewLocalization;
    internal WorldState? World => _hud.ViewWorld;
    internal CityData? SelectedCity => _hud.ViewSelectedCity;
    internal int GetCityListFilterIndex() => _hud.ViewGetCityListFilterIndex();
    internal int GetOfficerSortIndex() => _hud.ViewGetOfficerSortIndex();
    internal bool CanInspectSelectedFaction() => _hud.ViewCanInspectSelectedFaction();
    internal bool IsOfficerListShowingCityOfficers() => _hud.ViewIsOfficerListShowingCityOfficers();
    internal bool IsOfficerListShowingFactionOfficers() => _hud.ViewIsOfficerListShowingFactionOfficers();
    internal bool IsOfficerListShowingItems() => _hud.ViewIsOfficerListShowingItems();
    internal bool IsOfficerListShowingDiplomacyRelations() => _hud.ViewIsOfficerListShowingDiplomacyRelations();
    internal bool IsOfficerListFactionScope() => _hud.ViewIsOfficerListFactionScope();
    internal string GetCityListDialogTitle() => _hud.ViewGetCityListDialogTitle();
    internal void SetOfficerListDialogTitle(string title) => _hud.ViewSetOfficerListDialogTitle(title);
    internal string CityEmptyMessage() => _hud.ViewGetNoCityInScopeMessage();
    internal string ItemEmptyMessage() => _hud.ViewGetNoItemInFactionMessage();
    internal string DiplomacyEmptyMessage() => _hud.ViewGetNoDiplomacyRelationMessage();
    internal TreeItem? PrepareOfficerListTableRoot() => _hud.ViewPrepareOfficerListTableRoot();
    internal int PopulateCityRows(TreeItem root) => _hud.ViewPopulateCityRows(root);
    internal int PopulateItemRows(TreeItem root) => _hud.ViewPopulateItemRows(root);
    internal int PopulateDiplomacyRelationRows(TreeItem root) => _hud.ViewPopulateDiplomacyRelationRows(root);
    internal (List<OfficerData> Officers, bool IncludeCityName, string EmptyMessage)? GetOfficerListRowsData() => _hud.ViewGetOfficerListRowsData();
    internal void PopulateOfficerRow(TreeItem row, OfficerData officer, bool includeCityName) => _hud.ViewPopulateOfficerRow(row, officer, includeCityName);
    internal void ApplyOfficerListRowStriping(TreeItem row, int rowIndex, int columnCount) => _hud.ViewApplyOfficerListRowStriping(row, rowIndex, columnCount);
    internal void AddLog(string message) => _hud.AddLog(message);
}
