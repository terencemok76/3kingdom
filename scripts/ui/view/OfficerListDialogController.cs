using Godot;

namespace ThreeKingdom.UI;

internal sealed class OfficerListDialogController : FloatingOverlayController
{
    private readonly ViewUiContext _context;
    private readonly OfficerDetailDialogController _officerDetailDialogController;
    protected override Vector2 MinimumOverlaySize => new(960.0f, 380.0f);

    public OfficerListDialogController(ViewUiContext context, OfficerDetailDialogController officerDetailDialogController)
        : base(context, "res://scenes/ui/view/OfficerListDialog.tscn")
    {
        _context = context;
        _officerDetailDialogController = officerDetailDialogController;
    }

    public void Initialize()
    {
        InitializeOverlay();
        _context.OfficerListDialog = OverlayRoot;
        _context.ApplyOfficerListTheme();
    }

    public void Hide()
    {
        HideOverlay();
    }

    public void RefreshText()
    {
        _context.RefreshDialogsText();
        RefreshChrome();
    }

    public void ShowMainDialog()
    {
        if (!_context.CanOpenMainViewDialog())
        {
            return;
        }

        _context.SetOfficerListModeToView();
        _context.ResetOfficerListDialogLayoutToSceneDefaults();
        _context.HideOfficerListAuxRow();
        _context.SetOfficerListContentToCityOfficers();
        _context.SetOfficerListConfirmButtonToDefaultText();
        RefreshChrome();
        PopulateDialog();
        ShowOverlay();
    }

    public void RefreshChrome()
    {
        UpdateToolbar();
        UpdateDialogTitle();
    }

    public void PopulateDialog()
    {
        if (_context.SelectedCity == null || _context.World == null)
        {
            return;
        }

        var root = _context.PrepareOfficerListTableRoot();
        if (root == null)
        {
            return;
        }

        if (_context.IsOfficerListInViewMode() && _context.IsOfficerListShowingCityContent())
        {
            var cityCount = _context.PopulateCityRows(root);
            if (cityCount == 0)
            {
                _context.AddLog(_context.CityEmptyMessage());
            }

            UpdateDialogTitle();
            return;
        }

        if (_context.IsOfficerListInViewMode() && _context.IsOfficerListShowingItems())
        {
            var itemCount = _context.PopulateItemRows(root);
            if (itemCount == 0)
            {
                _context.AddLog(_context.ItemEmptyMessage());
            }

            UpdateDialogTitle();
            return;
        }

        if (_context.IsOfficerListInViewMode() && _context.IsOfficerListShowingDiplomacyRelations())
        {
            var relationCount = _context.PopulateDiplomacyRelationRows(root);
            if (relationCount == 0)
            {
                _context.AddLog(_context.DiplomacyEmptyMessage());
            }

            UpdateDialogTitle();
            return;
        }

        var officerData = _context.GetOfficerListRowsData();
        if (officerData == null)
        {
            UpdateDialogTitle();
            return;
        }

        var (officers, includeCityName, emptyMessage) = officerData.Value;
        var columnCount = includeCityName ? 8 : 7;
        for (var index = 0; index < officers.Count; index += 1)
        {
            var row = _context.OfficerListTable?.CreateItem(root);
            if (row == null)
            {
                continue;
            }

            _context.PopulateOfficerRow(row, officers[index], includeCityName);
            _context.ApplyOfficerListRowStriping(row, index, columnCount);
        }

        if (officers.Count == 0)
        {
            _context.AddLog(emptyMessage);
        }

        UpdateDialogTitle();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _context.OfficerListDialog = OverlayRoot;
        _context.OfficerListToolbar = root.GetNodeOrNull<HBoxContainer>("OfficerListContentMargin/OfficerListContent/OfficerListToolbar");
        _context.OfficerListAuxRow = root.GetNodeOrNull<HBoxContainer>("OfficerListContentMargin/OfficerListContent/OfficerListAuxRow");
        _context.OfficerListAuxLabel = root.GetNodeOrNull<Label>("OfficerListContentMargin/OfficerListContent/OfficerListAuxRow/OfficerListAuxLabel");
        _context.OfficerListAuxOption = root.GetNodeOrNull<OptionButton>("OfficerListContentMargin/OfficerListContent/OfficerListAuxRow/OfficerListAuxOption");
        _context.ViewCityOfficersButton = root.GetNodeOrNull<Button>("OfficerListContentMargin/OfficerListContent/OfficerListToolbar/ViewCityOfficersButton");
        _context.ViewFactionOfficersButton = root.GetNodeOrNull<Button>("OfficerListContentMargin/OfficerListContent/OfficerListToolbar/ViewFactionOfficersButton");
        _context.ViewFactionItemsButton = root.GetNodeOrNull<Button>("OfficerListContentMargin/OfficerListContent/OfficerListToolbar/ViewFactionItemsButton");
        _context.ViewDiplomacyRelationsButton = root.GetNodeOrNull<Button>("OfficerListContentMargin/OfficerListContent/OfficerListToolbar/ViewDiplomacyRelationsButton");
        _context.ViewCitiesButton = root.GetNodeOrNull<Button>("OfficerListContentMargin/OfficerListContent/OfficerListToolbar/ViewCitiesButton");
        _context.CityListFilterOption = root.GetNodeOrNull<OptionButton>("OfficerListContentMargin/OfficerListContent/OfficerListToolbar/CityListFilterOption");
        _context.OfficerSortOption = root.GetNodeOrNull<OptionButton>("OfficerListContentMargin/OfficerListContent/OfficerListToolbar/OfficerSortOption");
        _context.OfficerListTable = root.GetNodeOrNull<Tree>("OfficerListContentMargin/OfficerListContent/OfficerListTable");
        _context.OfficerListConfirmButton = root.GetNodeOrNull<Button>("OfficerListContentMargin/OfficerListContent/OfficerListConfirmRow/OfficerListConfirmButton");

        if (_context.OfficerListAuxOption != null)
        {
            _context.OfficerListAuxOption.ItemSelected += OnAuxOptionSelected;
        }

        if (_context.ViewCityOfficersButton != null)
        {
            _context.ViewCityOfficersButton.Pressed += OnViewCityOfficersPressed;
        }

        if (_context.ViewFactionOfficersButton != null)
        {
            _context.ViewFactionOfficersButton.Pressed += OnViewFactionOfficersPressed;
        }

        if (_context.ViewFactionItemsButton != null)
        {
            _context.ViewFactionItemsButton.Pressed += OnViewFactionItemsPressed;
        }

        if (_context.ViewDiplomacyRelationsButton != null)
        {
            _context.ViewDiplomacyRelationsButton.Pressed += OnViewDiplomacyRelationsPressed;
        }

        if (_context.ViewCitiesButton != null)
        {
            _context.ViewCitiesButton.Pressed += OnViewCitiesPressed;
        }

        if (_context.CityListFilterOption != null)
        {
            _context.CityListFilterOption.ItemSelected += OnCityListFilterSelected;
        }

        if (_context.OfficerSortOption != null)
        {
            _context.OfficerSortOption.ItemSelected += OnOfficerSortSelected;
        }

        if (_context.OfficerListTable != null)
        {
            _context.OfficerListTable.ItemSelected += OnOfficerListSelected;
            _context.OfficerListTable.ItemActivated += OnOfficerListActivated;
            _context.OfficerListTable.ColumnTitleClicked += OnOfficerListColumnTitleClicked;
        }

        if (_context.OfficerListConfirmButton != null)
        {
            _context.OfficerListConfirmButton.Pressed += OnOfficerListConfirmPressed;
        }
    }

    private void OnAuxOptionSelected(long index) => _context.HandleOfficerListAuxSelected(index);
    private void OnViewCityOfficersPressed()
    {
        if (!_context.IsOfficerListInViewMode())
        {
            return;
        }

        _context.SetOfficerListContentToCityOfficers();
        RefreshChrome();
        PopulateDialog();
    }

    private void OnViewFactionOfficersPressed()
    {
        if (!_context.IsOfficerListInViewMode())
        {
            return;
        }

        _context.SetOfficerListContentToFactionOfficers();
        RefreshChrome();
        PopulateDialog();
    }

    private void OnViewFactionItemsPressed()
    {
        if (!_context.IsOfficerListInViewMode())
        {
            return;
        }

        _context.SetOfficerListContentToFactionItems();
        RefreshChrome();
        PopulateDialog();
    }

    private void OnViewDiplomacyRelationsPressed()
    {
        if (!_context.IsOfficerListInViewMode())
        {
            return;
        }

        _context.SetOfficerListContentToDiplomacyRelations();
        RefreshChrome();
        PopulateDialog();
    }

    private void OnViewCitiesPressed()
    {
        if (!_context.IsOfficerListInViewMode())
        {
            return;
        }

        _context.SetOfficerListContentToCities();
        RefreshChrome();
        PopulateDialog();
    }

    private void OnCityListFilterSelected(long index) => _context.HandleCityListFilterSelected(index);
    private void OnOfficerSortSelected(long index) => _context.HandleOfficerSortSelected(index);

    private void OnOfficerListSelected()
    {
        var selectedItem = _context.GetSelectedOfficerListItem();
        if (selectedItem == null)
        {
            return;
        }

        _context.ApplyOfficerListSelectionHighlight(selectedItem);
        if (_context.IsOfficerListShowingNonOfficerContent() || _context.IsOfficerListSelectionOnlyMode())
        {
            return;
        }

        var metadata = selectedItem.GetMetadata(0);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        var officer = _context.GetOfficerById(metadata.AsInt32());
        if (officer == null)
        {
            return;
        }

        _officerDetailDialogController.ShowOfficer(officer);
    }

    private void OnOfficerListActivated()
    {
        if (!_context.IsOfficerListShowingCityContent())
        {
            return;
        }

        var selectedItem = _context.GetSelectedOfficerListItem();
        if (selectedItem == null)
        {
            return;
        }

        var metadata = selectedItem.GetMetadata(0);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        if (!_context.SelectCityById(metadata.AsInt32()))
        {
            return;
        }

        _context.HideOfficerListDialog();
    }

    private void OnOfficerListColumnTitleClicked(long column, long mouseButtonIndex) => _context.HandleOfficerListColumnTitleClicked(column, mouseButtonIndex);
    private void OnOfficerListConfirmPressed() => _context.HandleOfficerListConfirmPressed();

    private void UpdateToolbar()
    {
        var toolbar = _context.OfficerListToolbar;
        var cityButton = _context.ViewCityOfficersButton;
        var factionButton = _context.ViewFactionOfficersButton;
        var itemButton = _context.ViewFactionItemsButton;
        var diplomacyButton = _context.ViewDiplomacyRelationsButton;
        var citiesButton = _context.ViewCitiesButton;
        var cityFilter = _context.CityListFilterOption;
        var sortOption = _context.OfficerSortOption;
        var selectedCity = _context.SelectedCity;
        var world = _context.World;
        var localization = _context.Localization;
        if (toolbar == null || cityButton == null || factionButton == null || itemButton == null || diplomacyButton == null || citiesButton == null || cityFilter == null || sortOption == null || selectedCity == null || world == null || localization == null)
        {
            return;
        }

        var isViewMode = _context.IsOfficerListInViewMode();
        toolbar.Visible = isViewMode;
        if (!isViewMode)
        {
            return;
        }

        cityButton.Text = localization.T("ui.view_city_officers");
        factionButton.Text = localization.T("ui.view_faction_officers");
        itemButton.Text = localization.T("ui.view_faction_items");
        diplomacyButton.Text = localization.T("ui.view_diplomacy_relations");
        citiesButton.Text = localization.T("ui.view_cities");

        if (cityFilter.ItemCount == 0)
        {
            cityFilter.AddItem(localization.T("ui.city_filter_self"));
            cityFilter.AddItem(localization.T("ui.city_filter_other"));
            cityFilter.AddItem(localization.T("ui.city_filter_all"));
        }
        else
        {
            cityFilter.SetItemText(0, localization.T("ui.city_filter_self"));
            cityFilter.SetItemText(1, localization.T("ui.city_filter_other"));
            cityFilter.SetItemText(2, localization.T("ui.city_filter_all"));
        }

        cityFilter.Select(_context.GetCityListFilterIndex());

        if (sortOption.ItemCount == 0)
        {
            sortOption.AddItem(localization.T("ui.sort_strength"));
            sortOption.AddItem(localization.T("ui.sort_intelligence"));
            sortOption.AddItem(localization.T("ui.sort_status"));
        }
        else
        {
            sortOption.SetItemText(0, localization.T("ui.sort_strength"));
            sortOption.SetItemText(1, localization.T("ui.sort_intelligence"));
            sortOption.SetItemText(2, localization.T("ui.sort_status"));
        }

        sortOption.Select(_context.GetOfficerSortIndex());

        var hasFaction = selectedCity.OwnerFactionId > 0 && world.GetFaction(selectedCity.OwnerFactionId) != null;
        var canInspectFaction = hasFaction && _context.CanInspectSelectedFaction();
        factionButton.Visible = canInspectFaction;
        itemButton.Visible = canInspectFaction;
        diplomacyButton.Visible = canInspectFaction;
        cityButton.Disabled = _context.IsOfficerListShowingCityOfficers();
        factionButton.Disabled = !canInspectFaction || _context.IsOfficerListShowingFactionOfficers();
        itemButton.Disabled = !canInspectFaction || _context.IsOfficerListShowingItems();
        diplomacyButton.Disabled = !canInspectFaction || _context.IsOfficerListShowingDiplomacyRelations();
        citiesButton.Disabled = _context.IsOfficerListShowingCityContent();
        cityFilter.Visible = _context.IsOfficerListShowingCityContent();
        sortOption.Visible = false;
    }

    private void UpdateDialogTitle()
    {
        var localization = _context.Localization;
        if (localization == null || !_context.IsOfficerListInViewMode())
        {
            return;
        }

        if (_context.IsOfficerListShowingCityContent())
        {
            _context.SetOfficerListDialogTitle(_context.GetCityListDialogTitle());
            return;
        }

        if (_context.IsOfficerListShowingItems())
        {
            _context.SetOfficerListDialogTitle(localization.T("ui.view_title.items_faction"));
            return;
        }

        if (_context.IsOfficerListShowingDiplomacyRelations())
        {
            _context.SetOfficerListDialogTitle(localization.T("ui.view_title.diplomacy_faction"));
            return;
        }

        _context.SetOfficerListDialogTitle(_context.IsOfficerListFactionScope()
            ? localization.T("ui.view_title.officers_faction")
            : localization.Format("fmt.view_title.officers_city_name", _context.SelectedCity != null ? localization.GetCityName(_context.SelectedCity) : localization.T("ui.view_title.officers_city")));
    }
}
