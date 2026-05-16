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
    private sealed class DiplomacyRelationViewRow
    {
        public int TargetFactionId { get; init; }
        public string TargetFactionName { get; init; } = string.Empty;
        public DiplomacyStatusType Status { get; init; } = DiplomacyStatusType.Neutral;
        public int RemainingMonths { get; init; }
        public int RelationScore { get; init; }
    }

    private void OnOfficerListClosePressed()
    {
        _genericOfficerSelectorConfirmedAction = null;
        _genericOfficerSelectorCandidateIds.Clear();
        _officerListDialog?.Hide();
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

        if (_officerListContentMode is OfficerListContentMode.Cities or OfficerListContentMode.Items or OfficerListContentMode.DiplomacyRelations)
        {
            return;
        }

        if (_officerListMode is OfficerListMode.CommandSelection or OfficerListMode.GenericSelection)
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
            _officerPortraitRect.Texture = CanViewOfficerFullInformation(officer)
                ? BuildOfficerPortraitTexture(officer.Id)
                : null;
        }

        if (_officerPortraitPlaceholderLabel != null)
        {
            var officerName = CanViewOfficerFullInformation(officer)
                ? (_localization?.GetOfficerName(officer) ?? officer.Name)
                : UnknownInfoText;
            var hasPortrait = _officerPortraitRect?.Texture != null;
            _officerPortraitPlaceholderLabel.Visible = !hasPortrait;
            _officerPortraitPlaceholderLabel.Text = $"{(_localization?.T("ui.portrait") ?? "Portrait")}\n{officerName}";
        }

        if (_officerDetailDialog.Visible)
        {
            _officerDetailDialog.Show();
        }
        else
        {
            _officerDetailDialog.PopupCentered(new Vector2I(700, 360));
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
        if (_officerListMode != OfficerListMode.CommandSelection)
        {
            if (_officerListMode == OfficerListMode.GenericSelection)
            {
                var selectedItemForSelector = _officerListTable?.GetSelected();
                if (selectedItemForSelector == null)
                {
                    AddLog(_localization?.T("ui.select_officer_warning") ?? "Select one officer first.");
                    return;
                }

                var selectorMetadata = selectedItemForSelector.GetMetadata(0);
                if (selectorMetadata.VariantType != Variant.Type.Int)
                {
                    AddLog(_localization?.T("ui.select_officer_warning") ?? "Select one officer first.");
                    return;
                }

                _genericOfficerSelectorConfirmedAction?.Invoke(selectorMetadata.AsInt32());
                _genericOfficerSelectorConfirmedAction = null;
                _genericOfficerSelectorCandidateIds.Clear();
                _officerListDialog?.Hide();
                return;
            }

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
            return;
        }
    }

    private void PopulateOfficerListDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _officerListTable == null)
        {
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
        else if (_officerListMode == OfficerListMode.View && _officerListContentMode == OfficerListContentMode.Items)
        {
            var items = GetSortedFactionInventoryItems();
            for (var index = 0; index < items.Count; index += 1)
            {
                var row = _officerListTable.CreateItem(root);
                PopulateItemTableRow(row, items[index]);
                ApplyViewTableRowStriping(row, index, _officerListTable.Columns);
            }

            if (items.Count == 0)
            {
                AddLog(_localization?.T("ui.no_item_in_faction") ?? "No items available in this faction.");
            }
        }
        else if (_officerListMode == OfficerListMode.View && _officerListContentMode == OfficerListContentMode.DiplomacyRelations)
        {
            var relations = GetSortedDiplomacyRelations();
            for (var index = 0; index < relations.Count; index += 1)
            {
                var row = _officerListTable.CreateItem(root);
                PopulateDiplomacyRelationTableRow(row, relations[index]);
                ApplyViewTableRowStriping(row, index, _officerListTable.Columns);
            }

            if (relations.Count == 0)
            {
                AddLog(_localization?.T("ui.no_diplomacy_relation_in_faction") ?? "No diplomacy relations available in this faction.");
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
                if (!CanViewCityFullInformation(_selectedCity))
                {
                    UpdateOfficerListDialogTitle();
                    return;
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
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.Gold : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.Gold : int.MinValue),
            ViewTableSortField.Food => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.Food : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.Food : int.MinValue),
            ViewTableSortField.Population => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.Population : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.Population : int.MinValue),
            ViewTableSortField.Troops => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.Troops : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.Troops : int.MinValue),
            ViewTableSortField.OfficerCount => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.OfficerIds.Count : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.OfficerIds.Count : int.MinValue),
            ViewTableSortField.Farm => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.Farm : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.Farm : int.MinValue),
            ViewTableSortField.Commercial => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.Commercial : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.Commercial : int.MinValue),
            ViewTableSortField.Defense => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.Defense : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.Defense : int.MinValue),
            ViewTableSortField.Loyalty => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.Loyalty : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.Loyalty : int.MinValue),
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
            _officerListTable.Columns = 11;
            SetViewTableColumn(0, _localization.T("ui.city"), 130, ViewTableSortField.Name);
            SetViewTableColumn(1, _localization.T("ui.faction_owner"), 140, ViewTableSortField.Owner);
            SetViewTableColumn(2, _localization.T("ui.gold"), 90, ViewTableSortField.Gold);
            SetViewTableColumn(3, _localization.T("ui.food"), 90, ViewTableSortField.Food);
            SetViewTableColumn(4, _localization.T("ui.population"), 110, ViewTableSortField.Population);
            SetViewTableColumn(5, _localization.T("ui.troops"), 90, ViewTableSortField.Troops);
            SetViewTableColumn(6, _localization.T("ui.officers"), 90, ViewTableSortField.OfficerCount);
            SetViewTableColumn(7, _localization.T("ui.farm"), 90, ViewTableSortField.Farm);
            SetViewTableColumn(8, _localization.T("ui.commercial"), 110, ViewTableSortField.Commercial);
            SetViewTableColumn(9, _localization.T("ui.defense"), 90, ViewTableSortField.Defense);
            SetViewTableColumn(10, _localization.T("ui.loyalty"), 90, ViewTableSortField.Loyalty);
            return;
        }

        if (_officerListContentMode == OfficerListContentMode.Items)
        {
            _officerListTable.Columns = 11;
            SetViewTableColumn(0, _localization.T("ui.items"), 170, ViewTableSortField.Name);
            SetViewTableColumn(1, _localization.T("ui.holder"), 130, ViewTableSortField.Holder);
            SetViewTableColumn(2, _localization.T("ui.item_type"), 110, ViewTableSortField.ItemType);
            SetViewTableColumn(3, _localization.T("ui.rarity"), 90, ViewTableSortField.Rarity);
            SetViewTableColumn(4, _localization.T("ui.strength"), 70, ViewTableSortField.Strength);
            SetViewTableColumn(5, _localization.T("ui.intelligence"), 70, ViewTableSortField.Intelligence);
            SetViewTableColumn(6, _localization.T("ui.charm"), 70, ViewTableSortField.Charm);
            SetViewTableColumn(7, _localization.T("ui.leadership"), 70, ViewTableSortField.Leadership);
            SetViewTableColumn(8, _localization.T("ui.politics"), 70, ViewTableSortField.Politics);
            SetViewTableColumn(9, _localization.T("ui.combat"), 70, ViewTableSortField.Combat);
            SetViewTableColumn(10, _localization.T("ui.loyalty"), 70, ViewTableSortField.OfficerLoyalty);
            return;
        }

        if (_officerListContentMode == OfficerListContentMode.DiplomacyRelations)
        {
            _officerListTable.Columns = 4;
            SetViewTableColumn(0, _localization.T("ui.target_faction"), 160, ViewTableSortField.Name);
            SetViewTableColumn(1, _localization.T("ui.relation_status"), 120, ViewTableSortField.RelationStatus);
            SetViewTableColumn(2, _localization.T("ui.remaining_months"), 120, ViewTableSortField.RemainingMonths);
            SetViewTableColumn(3, _localization.T("ui.relation_score"), 110, ViewTableSortField.RelationScore);
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

        var canViewOfficer = CanViewOfficerFullInformation(officer);
        row.SetMetadata(0, officer.Id);
        row.SetText(0, BuildMaskedOfficerName(officer));
        row.SetText(1, BuildMaskedOfficerRole(officer));
        var world = _turnManager!.World!;
        row.SetText(2, BuildMaskedOfficerStatus(world, officer));
        var officerAge = CalculateOfficerAge(officer, world.Year);
        var loyaltyText = BuildMaskedOfficerLoyalty(world, officer);
        if (includeCityName)
        {
            var city = _turnManager?.World?.GetCity(officer.CityId);
            row.SetText(3, canViewOfficer && city != null ? _localization.GetCityName(city) : UnknownInfoText);
            row.SetText(4, MaskedNumberText(canViewOfficer, officerAge));
            row.SetText(5, loyaltyText);
            row.SetText(6, MaskedNumberText(canViewOfficer, officer.Strength));
            row.SetText(7, MaskedNumberText(canViewOfficer, officer.Intelligence));
        }
        else
        {
            row.SetText(3, MaskedNumberText(canViewOfficer, officerAge));
            row.SetText(4, loyaltyText);
            row.SetText(5, MaskedNumberText(canViewOfficer, officer.Strength));
            row.SetText(6, MaskedNumberText(canViewOfficer, officer.Intelligence));
        }
    }

    private static string BuildOfficerLoyaltyTableText(WorldState world, OfficerData officer)
    {
        return IsFactionRuler(world, officer) || FreeOfficerMovement.IsFreeOfficer(world, officer) ? "-" : officer.Loyalty.ToString();
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

        var canViewCity = CanViewCityFullInformation(city);
        row.SetMetadata(0, city.Id);
        row.SetText(0, _localization.GetCityName(city));
        var ownerName = _localization.GetFactionName(_turnManager.World, city.OwnerFactionId);
        var intelDurationText = BuildCityIntelDurationText(city);
        row.SetText(1, string.IsNullOrWhiteSpace(intelDurationText)
            ? ownerName
            : $"{ownerName} | {intelDurationText}");
        row.SetText(2, MaskedNumberText(canViewCity, city.Gold));
        row.SetText(3, MaskedNumberText(canViewCity, city.Food));
        row.SetText(4, MaskedNumberText(canViewCity, city.Population));
        row.SetText(5, MaskedNumberText(canViewCity, city.Troops));
        row.SetText(6, MaskedNumberText(canViewCity, city.OfficerIds.Count));
        row.SetText(7, MaskedNumberText(canViewCity, city.Farm));
        row.SetText(8, MaskedNumberText(canViewCity, city.Commercial));
        row.SetText(9, MaskedNumberText(canViewCity, city.Defense));
        row.SetText(10, MaskedNumberText(canViewCity, city.Loyalty));
    }

    private void PopulateItemTableRow(TreeItem row, ItemData item)
    {
        if (_localization == null)
        {
            return;
        }

        row.SetMetadata(0, item.Id);
        row.SetText(0, _localization.GetItemName(item));
        row.SetText(1, BuildItemHolderText(item));
        row.SetText(2, _localization.GetItemType(item));
        row.SetText(3, _localization.GetItemRarity(item));
        row.SetText(4, item.StrengthBonus.ToString());
        row.SetText(5, item.IntelligenceBonus.ToString());
        row.SetText(6, item.CharmBonus.ToString());
        row.SetText(7, item.LeadershipBonus.ToString());
        row.SetText(8, item.PoliticsBonus.ToString());
        row.SetText(9, item.CombatBonus.ToString());
        row.SetText(10, item.LoyaltyBonus.ToString());
    }

    private void PopulateDiplomacyRelationTableRow(TreeItem row, DiplomacyRelationViewRow relation)
    {
        if (_localization == null)
        {
            return;
        }

        row.SetMetadata(0, relation.TargetFactionId);
        row.SetText(0, relation.TargetFactionName);
        row.SetText(1, GetDiplomacyStatusText(relation.Status));
        row.SetText(2, relation.Status == DiplomacyStatusType.Neutral ? "-" : relation.RemainingMonths.ToString());
        row.SetText(3, relation.RelationScore.ToString());
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
                4 => ViewTableSortField.Population,
                5 => ViewTableSortField.Troops,
                6 => ViewTableSortField.OfficerCount,
                7 => ViewTableSortField.Farm,
                8 => ViewTableSortField.Commercial,
                9 => ViewTableSortField.Defense,
                10 => ViewTableSortField.Loyalty,
                _ => ViewTableSortField.Name
            };
        }

        if (_officerListContentMode == OfficerListContentMode.Items)
        {
            return column switch
            {
                1 => ViewTableSortField.Holder,
                2 => ViewTableSortField.ItemType,
                3 => ViewTableSortField.Rarity,
                4 => ViewTableSortField.Strength,
                5 => ViewTableSortField.Intelligence,
                6 => ViewTableSortField.Charm,
                7 => ViewTableSortField.Leadership,
                8 => ViewTableSortField.Politics,
                9 => ViewTableSortField.Combat,
                10 => ViewTableSortField.OfficerLoyalty,
                _ => ViewTableSortField.Name
            };
        }

        if (_officerListContentMode == OfficerListContentMode.DiplomacyRelations)
        {
            return column switch
            {
                1 => ViewTableSortField.RelationStatus,
                2 => ViewTableSortField.RemainingMonths,
                3 => ViewTableSortField.RelationScore,
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
        return field is ViewTableSortField.Name or ViewTableSortField.Role or ViewTableSortField.Status or ViewTableSortField.City or ViewTableSortField.Owner or ViewTableSortField.Holder or ViewTableSortField.ItemType or ViewTableSortField.Rarity or ViewTableSortField.RelationStatus;
    }

    private void UpdateOfficerListToolbar()
    {
        if (_officerListToolbar == null || _viewCityOfficersDialogButton == null || _viewFactionOfficersDialogButton == null || _viewFactionItemsDialogButton == null || _viewDiplomacyRelationsDialogButton == null || _viewCitiesDialogButton == null || _cityListFilterOption == null || _officerSortOption == null || _selectedCity == null || _turnManager?.World == null || _localization == null)
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
        _viewFactionItemsDialogButton.Text = _localization.T("ui.view_faction_items");
        _viewDiplomacyRelationsDialogButton.Text = _localization.T("ui.view_diplomacy_relations");
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
        var canInspectFaction = hasFaction && CanInspectSelectedFaction();
        _viewFactionOfficersDialogButton.Visible = canInspectFaction;
        _viewFactionItemsDialogButton.Visible = canInspectFaction;
        _viewDiplomacyRelationsDialogButton.Visible = canInspectFaction;
        _viewCityOfficersDialogButton.Disabled = _officerListContentMode == OfficerListContentMode.Officers && _officerListScope == OfficerListScope.City;
        _viewFactionOfficersDialogButton.Disabled = !canInspectFaction || (_officerListContentMode == OfficerListContentMode.Officers && _officerListScope == OfficerListScope.Faction);
        _viewFactionItemsDialogButton.Disabled = !canInspectFaction || _officerListContentMode == OfficerListContentMode.Items;
        _viewDiplomacyRelationsDialogButton.Disabled = !canInspectFaction || _officerListContentMode == OfficerListContentMode.DiplomacyRelations;
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
                CityListFilterMode.OtherFactions => _localization.T("ui.view_title.cities_other"),
                CityListFilterMode.AllCities => _localization.T("ui.view_title.cities_all"),
                _ => _localization.T("ui.view_title.cities_self")
            });
            return;
        }

        if (_officerListContentMode == OfficerListContentMode.Items)
        {
            SetOfficerListDialogTitle(_localization.T("ui.view_title.items_faction"));
            return;
        }

        if (_officerListContentMode == OfficerListContentMode.DiplomacyRelations)
        {
            SetOfficerListDialogTitle(_localization.T("ui.view_title.diplomacy_faction"));
            return;
        }

        SetOfficerListDialogTitle(_officerListScope == OfficerListScope.Faction
            ? _localization.T("ui.view_title.officers_faction")
            : _localization.Format("fmt.view_title.officers_city_name", _selectedCity != null ? _localization.GetCityName(_selectedCity) : _localization.T("ui.view_title.officers_city")));
    }

    private void OnViewFactionItemsDialogPressed()
    {
        if (_officerListMode != OfficerListMode.View)
        {
            return;
        }

        _officerListScope = OfficerListScope.Faction;
        _officerListContentMode = OfficerListContentMode.Items;
        UpdateOfficerListToolbar();
        PopulateOfficerListDialog();
    }

    private void OnViewDiplomacyRelationsDialogPressed()
    {
        if (_officerListMode != OfficerListMode.View)
        {
            return;
        }

        _officerListScope = OfficerListScope.Faction;
        _officerListContentMode = OfficerListContentMode.DiplomacyRelations;
        UpdateOfficerListToolbar();
        PopulateOfficerListDialog();
    }

    private void SetOfficerListDialogTitle(string title)
    {
        if (_officerListDialog != null)
        {
            _officerListDialog.Title = title;
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
                ? officers.OrderBy(officer => CanViewOfficerFullInformation(officer) ? CalculateOfficerAge(officer, _turnManager?.World?.Year ?? 0) : int.MinValue)
                : officers.OrderByDescending(officer => CanViewOfficerFullInformation(officer) ? CalculateOfficerAge(officer, _turnManager?.World?.Year ?? 0) : int.MinValue),
            ViewTableSortField.OfficerLoyalty => _viewTableSortAscending
                ? officers.OrderBy(officer => CanViewOfficerFullInformation(officer) ? officer.Loyalty : int.MinValue)
                : officers.OrderByDescending(officer => CanViewOfficerFullInformation(officer) ? officer.Loyalty : int.MinValue),
            ViewTableSortField.Strength => _viewTableSortAscending
                ? officers.OrderBy(officer => CanViewOfficerFullInformation(officer) ? officer.Strength : int.MinValue)
                : officers.OrderByDescending(officer => CanViewOfficerFullInformation(officer) ? officer.Strength : int.MinValue),
            ViewTableSortField.Intelligence => _viewTableSortAscending
                ? officers.OrderBy(officer => CanViewOfficerFullInformation(officer) ? officer.Intelligence : int.MinValue)
                : officers.OrderByDescending(officer => CanViewOfficerFullInformation(officer) ? officer.Intelligence : int.MinValue),
            ViewTableSortField.SpyExperience => _viewTableSortAscending
                ? officers.OrderBy(officer => CanViewOfficerFullInformation(officer) ? officer.SpyExperience : int.MinValue)
                : officers.OrderByDescending(officer => CanViewOfficerFullInformation(officer) ? officer.SpyExperience : int.MinValue),
            ViewTableSortField.DiplomacyExperience => _viewTableSortAscending
                ? officers.OrderBy(officer => CanViewOfficerFullInformation(officer) ? officer.DiplomacyExperience : int.MinValue)
                : officers.OrderByDescending(officer => CanViewOfficerFullInformation(officer) ? officer.DiplomacyExperience : int.MinValue),
            _ => _viewTableSortAscending
                ? officers.OrderBy(BuildMaskedOfficerName)
                : officers.OrderByDescending(BuildMaskedOfficerName)
        };
    }

    private List<ItemData> GetSortedFactionInventoryItems()
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return new List<ItemData>();
        }

        var items = _turnManager.World.Items
            .Where(item => item.OwnerFactionId == _selectedCity.OwnerFactionId)
            .ToList();

        IOrderedEnumerable<ItemData> ordered = _viewTableSortField switch
        {
            ViewTableSortField.Holder => _viewTableSortAscending
                ? items.OrderBy(BuildItemHolderSortText)
                : items.OrderByDescending(BuildItemHolderSortText),
            ViewTableSortField.ItemType => _viewTableSortAscending
                ? items.OrderBy(item => _localization?.GetItemType(item) ?? item.ItemType.ToString())
                : items.OrderByDescending(item => _localization?.GetItemType(item) ?? item.ItemType.ToString()),
            ViewTableSortField.Rarity => _viewTableSortAscending
                ? items.OrderBy(item => GetItemRaritySortKey(item)).ThenBy(item => _localization?.GetItemRarity(item) ?? item.Rarity)
                : items.OrderByDescending(item => GetItemRaritySortKey(item)).ThenByDescending(item => _localization?.GetItemRarity(item) ?? item.Rarity),
            ViewTableSortField.Strength => _viewTableSortAscending
                ? items.OrderBy(item => item.StrengthBonus)
                : items.OrderByDescending(item => item.StrengthBonus),
            ViewTableSortField.Intelligence => _viewTableSortAscending
                ? items.OrderBy(item => item.IntelligenceBonus)
                : items.OrderByDescending(item => item.IntelligenceBonus),
            ViewTableSortField.Charm => _viewTableSortAscending
                ? items.OrderBy(item => item.CharmBonus)
                : items.OrderByDescending(item => item.CharmBonus),
            ViewTableSortField.Leadership => _viewTableSortAscending
                ? items.OrderBy(item => item.LeadershipBonus)
                : items.OrderByDescending(item => item.LeadershipBonus),
            ViewTableSortField.Politics => _viewTableSortAscending
                ? items.OrderBy(item => item.PoliticsBonus)
                : items.OrderByDescending(item => item.PoliticsBonus),
            ViewTableSortField.Combat => _viewTableSortAscending
                ? items.OrderBy(item => item.CombatBonus)
                : items.OrderByDescending(item => item.CombatBonus),
            ViewTableSortField.OfficerLoyalty => _viewTableSortAscending
                ? items.OrderBy(item => item.LoyaltyBonus)
                : items.OrderByDescending(item => item.LoyaltyBonus),
            _ => _viewTableSortAscending
                ? items.OrderBy(item => _localization?.GetItemName(item) ?? item.NameEn)
                : items.OrderByDescending(item => _localization?.GetItemName(item) ?? item.NameEn)
        };

        return ordered
            .ThenBy(item => item.Id)
            .ToList();
    }

    private List<DiplomacyRelationViewRow> GetSortedDiplomacyRelations()
    {
        if (_turnManager?.World == null || _selectedCity == null || _localization == null)
        {
            return new List<DiplomacyRelationViewRow>();
        }

        var factionId = _selectedCity.OwnerFactionId;
        if (factionId <= 0)
        {
            return new List<DiplomacyRelationViewRow>();
        }

        var relations = _turnManager.World.Factions
            .Where(faction => faction.Id != factionId)
            .Select(faction =>
            {
                var relation = _turnManager.World.GetDiplomacyRelation(factionId, faction.Id);
                return new DiplomacyRelationViewRow
                {
                    TargetFactionId = faction.Id,
                    TargetFactionName = _localization.GetFactionName(_turnManager.World, faction.Id),
                    Status = relation?.Status ?? DiplomacyStatusType.Neutral,
                    RemainingMonths = relation?.RemainingMonths ?? 0,
                    RelationScore = relation?.RelationScore ?? 0
                };
            })
            .ToList();

        IOrderedEnumerable<DiplomacyRelationViewRow> ordered = _viewTableSortField switch
        {
            ViewTableSortField.RelationStatus => _viewTableSortAscending
                ? relations.OrderBy(item => GetDiplomacyStatusSortKey(item.Status))
                : relations.OrderByDescending(item => GetDiplomacyStatusSortKey(item.Status)),
            ViewTableSortField.RemainingMonths => _viewTableSortAscending
                ? relations.OrderBy(item => item.RemainingMonths)
                : relations.OrderByDescending(item => item.RemainingMonths),
            ViewTableSortField.RelationScore => _viewTableSortAscending
                ? relations.OrderBy(item => item.RelationScore)
                : relations.OrderByDescending(item => item.RelationScore),
            _ => _viewTableSortAscending
                ? relations.OrderBy(item => item.TargetFactionName)
                : relations.OrderByDescending(item => item.TargetFactionName)
        };

        return ordered
            .ThenBy(item => item.TargetFactionId)
            .ToList();
    }

    private string BuildItemHolderText(ItemData item)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return "-";
        }

        if (item.EquippedOfficerId > 0)
        {
            var officer = _turnManager.World.GetOfficer(item.EquippedOfficerId);
            if (officer != null)
            {
                return _localization.GetOfficerName(officer);
            }
        }

        return _localization.T("ui.none");
    }

    private string BuildItemHolderSortText(ItemData item)
    {
        var holder = BuildItemHolderText(item);
        return holder == (_localization?.T("ui.none") ?? "None") ? "zzzz" : holder;
    }

    private string GetDiplomacyStatusText(DiplomacyStatusType status)
    {
        if (_localization == null)
        {
            return status.ToString();
        }

        var key = status switch
        {
            DiplomacyStatusType.Alliance => "ui.diplomacy_status_alliance",
            DiplomacyStatusType.Truce => "ui.diplomacy_status_truce",
            _ => "ui.diplomacy_status_neutral"
        };

        return _localization.T(key);
    }

    private static int GetDiplomacyStatusSortKey(DiplomacyStatusType status)
    {
        return status switch
        {
            DiplomacyStatusType.Alliance => 2,
            DiplomacyStatusType.Truce => 1,
            _ => 0
        };
    }

    private static int GetItemRaritySortKey(ItemData item)
    {
        return item.Rarity.ToLowerInvariant() switch
        {
            "common" => 0,
            "rare" => 1,
            "epic" => 2,
            _ => 99
        };
    }

    private string GetOfficerCityNameForSort(OfficerData officer)
    {
        if (_turnManager?.World == null || _localization == null || !CanViewOfficerFullInformation(officer))
        {
            return UnknownInfoText;
        }

        var city = _turnManager.World.GetCity(officer.CityId);
        return city != null ? _localization.GetCityName(city) : string.Empty;
    }

    private int GetOfficerStatusSortKey(OfficerData officer)
    {
        if (_turnManager?.World == null || !CanViewOfficerFullInformation(officer))
        {
            return 0;
        }

        if (FreeOfficerMovement.IsFreeOfficer(_turnManager.World, officer))
        {
            return -1;
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
        PopupDialogUsingSceneSize(_officerListDialog);
    }


}
