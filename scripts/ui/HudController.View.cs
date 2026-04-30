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
    private void OnOfficerListItemSelected(long index)
    {
        if (_turnManager?.World == null || _officerListView == null)
        {
            return;
        }

        var metadata = _officerListView.GetItemMetadata((int)index);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        if (_officerListMode == OfficerListMode.View && _officerListContentMode == OfficerListContentMode.Cities)
        {
            var city = _turnManager.World.GetCity(metadata.AsInt32());
            if (city == null)
            {
                return;
            }

            _selectedCity = city;
            RefreshSelectedCity();
            _mapController?.SelectCityById(city.Id);
            _officerListDialog?.Hide();
            return;
        }

        var officer = _turnManager.World.GetOfficer(metadata.AsInt32());
        if (officer == null)
        {
            return;
        }

        if (_officerListMode == OfficerListMode.CommandSelection)
        {
            return;
        }

        if (_officerDetailDialog == null)
        {
            return;
        }

        _officerDetailDialog.Title = _localization?.T("ui.officer_detail") ?? "Officer Details";
        if (_officerDetailText != null)
        {
            _officerDetailText.Text = BuildOfficerDetailText(officer);
        }

        if (_officerPortraitRect != null)
        {
            _officerPortraitRect.Texture = BuildOfficerPortraitTexture(officer.Id);
        }

        if (_officerPortraitPlaceholderLabel != null)
        {
            var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
            var hasPortrait = _officerPortraitRect?.Texture != null;
            _officerPortraitPlaceholderLabel.Visible = !hasPortrait;
            _officerPortraitPlaceholderLabel.Text = $"{(_localization?.T("ui.portrait") ?? "Portrait")}\n{officerName}";
        }

        _officerDetailDialog.DialogText = string.Empty;
        if (_officerDetailDialog.Visible)
        {
            _officerDetailDialog.Show();
        }
        else
        {
            _officerDetailDialog.PopupCentered(new Vector2I(520, 340));
        }
    }

    private void OnOfficerListClosePressed()
    {
        _officerListDialog?.Hide();
    }

    private void OnOfficerListHeaderGuiInput(InputEvent inputEvent)
    {
        if (_officerListDialog == null)
        {
            return;
        }

        if (inputEvent is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            _isDraggingOfficerListDialog = mouseButton.Pressed;
            if (_isDraggingOfficerListDialog)
            {
                _officerListDialogDragOffset = DisplayServer.MouseGetPosition() - _officerListDialog.Position;
            }

            return;
        }

        if (_isDraggingOfficerListDialog && inputEvent is InputEventMouseMotion)
        {
            _officerListDialog.Position = DisplayServer.MouseGetPosition() - _officerListDialogDragOffset;
        }
    }

    private void OnOfficerListTableSelected()
    {
        if (_turnManager?.World == null || _officerListTable == null)
        {
            return;
        }

        var selectedItem = _officerListTable.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        ApplyViewTableSelectionHighlight(selectedItem);

        var metadata = selectedItem.GetMetadata(0);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        if (_officerListContentMode == OfficerListContentMode.Cities)
        {
            return;
        }

        var officer = _turnManager.World.GetOfficer(metadata.AsInt32());
        if (officer == null)
        {
            return;
        }

        if (_officerDetailDialog == null)
        {
            return;
        }

        _officerDetailDialog.Title = _localization?.T("ui.officer_detail") ?? "Officer Details";
        if (_officerDetailText != null)
        {
            _officerDetailText.Text = BuildOfficerDetailText(officer);
        }

        if (_officerPortraitRect != null)
        {
            _officerPortraitRect.Texture = BuildOfficerPortraitTexture(officer.Id);
        }

        if (_officerPortraitPlaceholderLabel != null)
        {
            var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
            var hasPortrait = _officerPortraitRect?.Texture != null;
            _officerPortraitPlaceholderLabel.Visible = !hasPortrait;
            _officerPortraitPlaceholderLabel.Text = $"{(_localization?.T("ui.portrait") ?? "Portrait")}\n{officerName}";
        }

        _officerDetailDialog.DialogText = string.Empty;
        if (_officerDetailDialog.Visible)
        {
            _officerDetailDialog.Show();
        }
        else
        {
            _officerDetailDialog.PopupCentered(new Vector2I(520, 340));
        }
    }

    private void OnOfficerListTableActivated()
    {
        if (_turnManager?.World == null || _officerListTable == null || _officerListContentMode != OfficerListContentMode.Cities)
        {
            return;
        }

        var selectedItem = _officerListTable.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        var metadata = selectedItem.GetMetadata(0);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        var city = _turnManager.World.GetCity(metadata.AsInt32());
        if (city == null)
        {
            return;
        }

        _selectedCity = city;
        RefreshSelectedCity();
        _mapController?.SelectCityById(city.Id);
        _officerListDialog?.Hide();
    }

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

        PopulateOfficerListDialog();
    }

    private void OnOfficerListDialogConfirmed()
    {
        if (_officerListMode != OfficerListMode.CommandSelection || _officerListView == null)
        {
            _officerListDialog?.Hide();
            return;
        }

        var selectedItems = _officerListView.GetSelectedItems();
        if (selectedItems.Length == 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? "Select one officer first.");
            return;
        }

        var metadata = _officerListView.GetItemMetadata(selectedItems[0]);
        if (metadata.VariantType != Variant.Type.Int)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? "Select one officer first.");
            return;
        }

        var result = ExecutePlayerCommand(_pendingOfficerCommand, officerIds: new List<int> { metadata.AsInt32() });
        if (result.Success)
        {
            _officerListDialog?.Hide();
            return;
        }
    }

    private void PopulateOfficerListDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _officerListView == null || _officerListTable == null)
        {
            return;
        }

        var isViewTable = _officerListMode == OfficerListMode.View;
        _officerListTable.Visible = isViewTable;
        _officerListView.Visible = !isViewTable;

        if (!isViewTable)
        {
            _officerListView.Clear();
            foreach (var officerId in _selectedCity.OfficerIds)
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null)
                {
                    continue;
                }

                var itemIndex = _officerListView.AddItem(BuildOfficerListRowText(officer));
                _officerListView.SetItemMetadata(itemIndex, officer.Id);
            }

            return;
        }

        _officerListTable.Clear();
        ConfigureViewTableColumns();
        var root = _officerListTable.CreateItem();

        if (_officerListMode == OfficerListMode.View && _officerListContentMode == OfficerListContentMode.Cities)
        {
            var cities = GetFilteredCities();
            for (var index = 0; index < cities.Count; index += 1)
            {
                var row = _officerListTable.CreateItem(root);
                PopulateCityTableRow(row, cities[index]);
                ApplyViewTableRowStriping(row, index, 10);
            }

            if (cities.Count == 0)
            {
                AddLog(_localization?.T("ui.no_city_in_scope") ?? "No cities available in this view.");
            }
        }
        else
        {
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
                foreach (var officerId in _selectedCity.OfficerIds)
                {
                    var officer = _turnManager.World.GetOfficer(officerId);
                    if (officer != null)
                    {
                        officers.Add(officer);
                    }
                }

                emptyMessage = _localization?.T("ui.no_officer_in_city") ?? "No officers available in this city.";
            }

            var sortedOfficers = GetSortedOfficers(officers).ToList();
            var columnCount = includeCityName ? 8 : 7;
            for (var index = 0; index < sortedOfficers.Count; index += 1)
            {
                var row = _officerListTable.CreateItem(root);
                PopulateOfficerTableRow(row, sortedOfficers[index], includeCityName);
                ApplyViewTableRowStriping(row, index, columnCount);
            }

            if (officers.Count == 0)
            {
                AddLog(emptyMessage);
            }
        }

        UpdateOfficerListDialogTitle();
    }

    private List<CityData> GetFilteredCities()
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return new List<CityData>();
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        var result = new List<CityData>();
        foreach (var city in _turnManager.World.Cities)
        {
            var include = _cityListFilterMode switch
            {
                CityListFilterMode.OtherFactions => city.OwnerFactionId > 0 && city.OwnerFactionId != playerFactionId,
                CityListFilterMode.AllCities => true,
                _ => city.OwnerFactionId == playerFactionId
            };

            if (include)
            {
                result.Add(city);
            }
        }

        IOrderedEnumerable<CityData> ordered = _viewTableSortField switch
        {
            ViewTableSortField.Owner => _viewTableSortAscending
                ? result.OrderBy(city => _localization?.GetFactionName(_turnManager.World, city.OwnerFactionId) ?? city.OwnerFactionId.ToString())
                : result.OrderByDescending(city => _localization?.GetFactionName(_turnManager.World, city.OwnerFactionId) ?? city.OwnerFactionId.ToString()),
            ViewTableSortField.Gold => _viewTableSortAscending
                ? result.OrderBy(city => city.Gold)
                : result.OrderByDescending(city => city.Gold),
            ViewTableSortField.Food => _viewTableSortAscending
                ? result.OrderBy(city => city.Food)
                : result.OrderByDescending(city => city.Food),
            ViewTableSortField.Troops => _viewTableSortAscending
                ? result.OrderBy(city => city.Troops)
                : result.OrderByDescending(city => city.Troops),
            ViewTableSortField.OfficerCount => _viewTableSortAscending
                ? result.OrderBy(city => city.OfficerIds.Count)
                : result.OrderByDescending(city => city.OfficerIds.Count),
            ViewTableSortField.Farm => _viewTableSortAscending
                ? result.OrderBy(city => city.Farm)
                : result.OrderByDescending(city => city.Farm),
            ViewTableSortField.Commercial => _viewTableSortAscending
                ? result.OrderBy(city => city.Commercial)
                : result.OrderByDescending(city => city.Commercial),
            ViewTableSortField.Defense => _viewTableSortAscending
                ? result.OrderBy(city => city.Defense)
                : result.OrderByDescending(city => city.Defense),
            ViewTableSortField.Loyalty => _viewTableSortAscending
                ? result.OrderBy(city => city.Loyalty)
                : result.OrderByDescending(city => city.Loyalty),
            _ => _viewTableSortAscending
                ? result.OrderBy(city => _localization?.GetCityName(city) ?? city.NameEn)
                : result.OrderByDescending(city => _localization?.GetCityName(city) ?? city.NameEn)
        };

        return ordered
            .ThenBy(city => city.Id)
            .ToList();
    }

    private void ConfigureViewTableColumns()
    {
        if (_officerListTable == null || _localization == null)
        {
            return;
        }

        if (_officerListContentMode == OfficerListContentMode.Cities)
        {
            _officerListTable.Columns = 10;
            SetViewTableColumn(0, _localization.T("ui.city"), 130, ViewTableSortField.Name);
            SetViewTableColumn(1, _localization.T("ui.owner"), 140, ViewTableSortField.Owner);
            SetViewTableColumn(2, _localization.T("ui.gold"), 90, ViewTableSortField.Gold);
            SetViewTableColumn(3, _localization.T("ui.food"), 90, ViewTableSortField.Food);
            SetViewTableColumn(4, _localization.T("ui.troops"), 90, ViewTableSortField.Troops);
            SetViewTableColumn(5, _localization.T("ui.officers"), 90, ViewTableSortField.OfficerCount);
            SetViewTableColumn(6, _localization.T("ui.farm"), 90, ViewTableSortField.Farm);
            SetViewTableColumn(7, _localization.T("ui.commercial"), 110, ViewTableSortField.Commercial);
            SetViewTableColumn(8, _localization.T("ui.defense"), 90, ViewTableSortField.Defense);
            SetViewTableColumn(9, _localization.T("ui.loyalty"), 90, ViewTableSortField.Loyalty);
            return;
        }

        var includeCityName = _officerListScope == OfficerListScope.Faction;
        _officerListTable.Columns = includeCityName ? 8 : 7;
        SetViewTableColumn(0, _localization.T("ui.officers"), 170, ViewTableSortField.Name);
        SetViewTableColumn(1, _localization.T("ui.role"), 120, ViewTableSortField.Role);
        SetViewTableColumn(2, _localization.T("ui.status"), 100, ViewTableSortField.Status);
        if (includeCityName)
        {
            SetViewTableColumn(3, _localization.T("ui.city"), 140, ViewTableSortField.City);
            SetViewTableColumn(4, _localization.T("ui.age"), 70, ViewTableSortField.Age);
            SetViewTableColumn(5, _localization.T("ui.loyalty"), 90, ViewTableSortField.OfficerLoyalty);
            SetViewTableColumn(6, _localization.T("ui.strength"), 90, ViewTableSortField.Strength);
            SetViewTableColumn(7, _localization.T("ui.intelligence"), 90, ViewTableSortField.Intelligence);
        }
        else
        {
            SetViewTableColumn(3, _localization.T("ui.age"), 70, ViewTableSortField.Age);
            SetViewTableColumn(4, _localization.T("ui.loyalty"), 90, ViewTableSortField.OfficerLoyalty);
            SetViewTableColumn(5, _localization.T("ui.strength"), 90, ViewTableSortField.Strength);
            SetViewTableColumn(6, _localization.T("ui.intelligence"), 90, ViewTableSortField.Intelligence);
        }
    }

    private void SetViewTableColumn(int column, string title, int minWidth, ViewTableSortField field)
    {
        _officerListTable?.SetColumnTitle(column, BuildSortableColumnTitle(title, field));
        _officerListTable?.SetColumnCustomMinimumWidth(column, minWidth);
        _officerListTable?.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
    }

    private string BuildSortableColumnTitle(string title, ViewTableSortField field)
    {
        if (_viewTableSortField != field)
        {
            return title;
        }

        return _viewTableSortAscending ? $"{title} ▲" : $"{title} ▼";
    }

    private static void ApplyViewTableRowStriping(TreeItem row, int rowIndex, int columnCount)
    {
        var background = rowIndex % 2 == 0
            ? new Color(0.98f, 0.95f, 0.89f, 0.92f)
            : new Color(0.93f, 0.88f, 0.78f, 0.9f);
        var textColor = new Color(0.13f, 0.09f, 0.05f, 1.0f);

        for (var column = 0; column < columnCount; column += 1)
        {
            row.SetCustomBgColor(column, background, false);
            row.SetCustomColor(column, textColor);
        }
    }

    private void ApplyViewTableSelectionHighlight(TreeItem selectedRow)
    {
        if (_officerListTable == null)
        {
            return;
        }

        var root = _officerListTable.GetRoot();
        if (root == null)
        {
            return;
        }

        var columnCount = _officerListTable.Columns;
        var row = root.GetFirstChild();
        var rowIndex = 0;
        while (row != null)
        {
            if (row == selectedRow)
            {
                ApplyViewTableSelectedRowStyle(row, columnCount);
            }
            else
            {
                ApplyViewTableRowStriping(row, rowIndex, columnCount);
            }

            row = row.GetNext();
            rowIndex += 1;
        }
    }

    private static void ApplyViewTableSelectedRowStyle(TreeItem row, int columnCount)
    {
        var background = new Color(0.82f, 0.68f, 0.38f, 1.0f);
        var textColor = new Color(0.22f, 0.05f, 0.02f, 1.0f);

        for (var column = 0; column < columnCount; column += 1)
        {
            row.SetCustomBgColor(column, background, false);
            row.SetCustomColor(column, textColor);
        }
    }

    private void PopulateOfficerTableRow(TreeItem row, OfficerData officer, bool includeCityName)
    {
        if (_localization == null)
        {
            return;
        }

        row.SetMetadata(0, officer.Id);
        row.SetText(0, _localization.GetOfficerName(officer));
        row.SetText(1, _localization.GetOfficerRole(officer));
        var world = _turnManager!.World!;
        row.SetText(2, _localization.GetOfficerStatus(world, officer));
        var officerAge = CalculateOfficerAge(officer, world.Year);
        var loyaltyText = BuildOfficerLoyaltyTableText(world, officer);
        if (includeCityName)
        {
            var city = _turnManager?.World?.GetCity(officer.CityId);
            row.SetText(3, city != null ? _localization.GetCityName(city) : "-");
            row.SetText(4, officerAge.ToString());
            row.SetText(5, loyaltyText);
            row.SetText(6, officer.Strength.ToString());
            row.SetText(7, officer.Intelligence.ToString());
        }
        else
        {
            row.SetText(3, officerAge.ToString());
            row.SetText(4, loyaltyText);
            row.SetText(5, officer.Strength.ToString());
            row.SetText(6, officer.Intelligence.ToString());
        }
    }

    private static string BuildOfficerLoyaltyTableText(WorldState world, OfficerData officer)
    {
        return IsFactionRuler(world, officer) ? "-" : officer.Loyalty.ToString();
    }

    private static bool IsFactionRuler(WorldState world, OfficerData officer)
    {
        return world.Factions.Any(faction => faction.RulerOfficerId == officer.Id);
    }

    private void PopulateCityTableRow(TreeItem row, CityData city)
    {
        if (_localization == null || _turnManager?.World == null)
        {
            return;
        }

        row.SetMetadata(0, city.Id);
        row.SetText(0, _localization.GetCityName(city));
        row.SetText(1, _localization.GetFactionName(_turnManager.World, city.OwnerFactionId));
        row.SetText(2, city.Gold.ToString());
        row.SetText(3, city.Food.ToString());
        row.SetText(4, city.Troops.ToString());
        row.SetText(5, city.OfficerIds.Count.ToString());
        row.SetText(6, city.Farm.ToString());
        row.SetText(7, city.Commercial.ToString());
        row.SetText(8, city.Defense.ToString());
        row.SetText(9, city.Loyalty.ToString());
    }

    private ViewTableSortField GetViewTableSortFieldForColumn(int column)
    {
        if (_officerListContentMode == OfficerListContentMode.Cities)
        {
            return column switch
            {
                1 => ViewTableSortField.Owner,
                2 => ViewTableSortField.Gold,
                3 => ViewTableSortField.Food,
                4 => ViewTableSortField.Troops,
                5 => ViewTableSortField.OfficerCount,
                6 => ViewTableSortField.Farm,
                7 => ViewTableSortField.Commercial,
                8 => ViewTableSortField.Defense,
                9 => ViewTableSortField.Loyalty,
                _ => ViewTableSortField.Name
            };
        }

        if (_officerListScope == OfficerListScope.Faction)
        {
            return column switch
            {
                1 => ViewTableSortField.Role,
                2 => ViewTableSortField.Status,
                3 => ViewTableSortField.City,
                4 => ViewTableSortField.Age,
                5 => ViewTableSortField.OfficerLoyalty,
                6 => ViewTableSortField.Strength,
                7 => ViewTableSortField.Intelligence,
                _ => ViewTableSortField.Name
            };
        }

        return column switch
        {
            1 => ViewTableSortField.Role,
            2 => ViewTableSortField.Status,
            3 => ViewTableSortField.Age,
            4 => ViewTableSortField.OfficerLoyalty,
            5 => ViewTableSortField.Strength,
            6 => ViewTableSortField.Intelligence,
            _ => ViewTableSortField.Name
        };
    }

    private static bool IsAscendingDefaultSortField(ViewTableSortField field)
    {
        return field is ViewTableSortField.Name or ViewTableSortField.Role or ViewTableSortField.Status or ViewTableSortField.City or ViewTableSortField.Owner;
    }

    private void UpdateOfficerListToolbar()
    {
        if (_officerListToolbar == null || _viewCityOfficersDialogButton == null || _viewFactionOfficersDialogButton == null || _viewCitiesDialogButton == null || _cityListFilterOption == null || _officerSortOption == null || _selectedCity == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        var isViewMode = _officerListMode == OfficerListMode.View;
        _officerListToolbar.Visible = isViewMode;
        if (!isViewMode)
        {
            return;
        }

        _viewCityOfficersDialogButton.Text = _localization.T("ui.view_city_officers");
        _viewFactionOfficersDialogButton.Text = _localization.T("ui.view_faction_officers");
        _viewCitiesDialogButton.Text = _localization.T("ui.view_cities");
        if (_cityListFilterOption.ItemCount == 0)
        {
            _cityListFilterOption.AddItem(_localization.T("ui.city_filter_self"));
            _cityListFilterOption.AddItem(_localization.T("ui.city_filter_other"));
            _cityListFilterOption.AddItem(_localization.T("ui.city_filter_all"));
        }
        else
        {
            _cityListFilterOption.SetItemText(0, _localization.T("ui.city_filter_self"));
            _cityListFilterOption.SetItemText(1, _localization.T("ui.city_filter_other"));
            _cityListFilterOption.SetItemText(2, _localization.T("ui.city_filter_all"));
        }

        _cityListFilterOption.Select(_cityListFilterMode switch
        {
            CityListFilterMode.OtherFactions => 1,
            CityListFilterMode.AllCities => 2,
            _ => 0
        });

        if (_officerSortOption.ItemCount == 0)
        {
            _officerSortOption.AddItem(_localization.T("ui.sort_strength"));
            _officerSortOption.AddItem(_localization.T("ui.sort_intelligence"));
            _officerSortOption.AddItem(_localization.T("ui.sort_status"));
        }
        else
        {
            _officerSortOption.SetItemText(0, _localization.T("ui.sort_strength"));
            _officerSortOption.SetItemText(1, _localization.T("ui.sort_intelligence"));
            _officerSortOption.SetItemText(2, _localization.T("ui.sort_status"));
        }

        _officerSortOption.Select(_officerSortMode switch
        {
            OfficerSortMode.Intelligence => 1,
            OfficerSortMode.Status => 2,
            _ => 0
        });

        var hasFaction = _selectedCity.OwnerFactionId > 0 && _turnManager.World.GetFaction(_selectedCity.OwnerFactionId) != null;
        _viewFactionOfficersDialogButton.Visible = hasFaction;
        _viewCityOfficersDialogButton.Disabled = _officerListContentMode == OfficerListContentMode.Officers && _officerListScope == OfficerListScope.City;
        _viewFactionOfficersDialogButton.Disabled = !hasFaction || (_officerListContentMode == OfficerListContentMode.Officers && _officerListScope == OfficerListScope.Faction);
        _viewCitiesDialogButton.Disabled = _officerListContentMode == OfficerListContentMode.Cities;
        _cityListFilterOption.Visible = _officerListContentMode == OfficerListContentMode.Cities;
        _officerSortOption.Visible = false;
    }

    private void UpdateOfficerListDialogTitle()
    {
        if (_localization == null)
        {
            return;
        }

        if (_officerListMode != OfficerListMode.View)
        {
            return;
        }

        if (_officerListContentMode == OfficerListContentMode.Cities)
        {
            SetOfficerListDialogTitle(_cityListFilterMode switch
            {
                CityListFilterMode.OtherFactions => _localization.T("ui.view_dialog_title_other_cities"),
                CityListFilterMode.AllCities => _localization.T("ui.view_dialog_title_all_cities"),
                _ => _localization.T("ui.view_dialog_title_self_cities")
            });
            return;
        }

        SetOfficerListDialogTitle(_officerListScope == OfficerListScope.Faction
            ? _localization.T("ui.view_dialog_title_faction")
            : _localization.Format("fmt.view_dialog_title_city_name", _selectedCity != null ? _localization.GetCityName(_selectedCity) : _localization.T("ui.view_dialog_title_city")));
    }

    private void SetOfficerListDialogTitle(string title)
    {
        if (_officerListDialog != null)
        {
            // Keep the built-in titlebar visually quiet and show the real title in our themed header row.
            _officerListDialog.Title = " ";
        }

        if (_officerListHeaderLabel != null)
        {
            _officerListHeaderLabel.Text = title;
        }
    }

    private IEnumerable<OfficerData> GetSortedOfficers(List<OfficerData> officers)
    {
        return _viewTableSortField switch
        {
            ViewTableSortField.Role => _viewTableSortAscending
                ? officers.OrderBy(officer => _localization?.GetOfficerRole(officer) ?? officer.Role)
                : officers.OrderByDescending(officer => _localization?.GetOfficerRole(officer) ?? officer.Role),
            ViewTableSortField.Status => _viewTableSortAscending
                ? officers.OrderBy(officer => GetOfficerStatusSortKey(officer))
                : officers.OrderByDescending(officer => GetOfficerStatusSortKey(officer)),
            ViewTableSortField.City => _viewTableSortAscending
                ? officers.OrderBy(officer => GetOfficerCityNameForSort(officer))
                : officers.OrderByDescending(officer => GetOfficerCityNameForSort(officer)),
            ViewTableSortField.Age => _viewTableSortAscending
                ? officers.OrderBy(officer => CalculateOfficerAge(officer, _turnManager?.World?.Year ?? 0))
                : officers.OrderByDescending(officer => CalculateOfficerAge(officer, _turnManager?.World?.Year ?? 0)),
            ViewTableSortField.OfficerLoyalty => _viewTableSortAscending
                ? officers.OrderBy(officer => officer.Loyalty)
                : officers.OrderByDescending(officer => officer.Loyalty),
            ViewTableSortField.Strength => _viewTableSortAscending
                ? officers.OrderBy(officer => officer.Strength)
                : officers.OrderByDescending(officer => officer.Strength),
            ViewTableSortField.Intelligence => _viewTableSortAscending
                ? officers.OrderBy(officer => officer.Intelligence)
                : officers.OrderByDescending(officer => officer.Intelligence),
            _ => _viewTableSortAscending
                ? officers.OrderBy(officer => _localization?.GetOfficerName(officer) ?? officer.Name)
                : officers.OrderByDescending(officer => _localization?.GetOfficerName(officer) ?? officer.Name)
        };
    }

    private string GetOfficerCityNameForSort(OfficerData officer)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return string.Empty;
        }

        var city = _turnManager.World.GetCity(officer.CityId);
        return city != null ? _localization.GetCityName(city) : string.Empty;
    }

    private int GetOfficerStatusSortKey(OfficerData officer)
    {
        if (_turnManager?.World == null)
        {
            return 0;
        }

        if (officer.LastAssignedYear != _turnManager.World.Year || officer.LastAssignedMonth != _turnManager.World.Month)
        {
            return 0;
        }

        return officer.LastAssignedCommand switch
        {
            CommandType.InternalAffairs => 1,
            CommandType.Develop => 1,
            CommandType.Recruit => 2,
            CommandType.Search => 3,
            CommandType.Move => 4,
            CommandType.Attack => 5,
            _ => 0
        };
    }

    private void ReopenOfficerListDialog()
    {
        if (_officerListDialog == null)
        {
            return;
        }

        CallDeferred(nameof(ReopenOfficerListDialogDeferred));
    }

    private void ReopenOfficerListDialogDeferred()
    {
        _officerListDialog?.PopupCentered(new Vector2I(420, 320));
    }


}
