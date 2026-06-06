using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private sealed class DiplomacyRelationViewRow
    {
        public int TargetFactionId { get; init; }
        public string TargetFactionName { get; init; } = string.Empty;
        public DiplomacyStatusType Status { get; init; } = DiplomacyStatusType.Neutral;
        public int RemainingMonths { get; init; }
        public int RelationScore { get; init; }
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
            ViewTableSortField.BowWorkshop => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? ConstructionRules.GetLevel(city, ConstructionProjectType.BowWorkshop) * 100000 + ConstructionRules.GetProgress(city, ConstructionProjectType.BowWorkshop) : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? ConstructionRules.GetLevel(city, ConstructionProjectType.BowWorkshop) * 100000 + ConstructionRules.GetProgress(city, ConstructionProjectType.BowWorkshop) : int.MinValue),
            ViewTableSortField.SiegeWorkshop => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? ConstructionRules.GetLevel(city, ConstructionProjectType.SiegeWorkshop) * 100000 + ConstructionRules.GetProgress(city, ConstructionProjectType.SiegeWorkshop) : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? ConstructionRules.GetLevel(city, ConstructionProjectType.SiegeWorkshop) * 100000 + ConstructionRules.GetProgress(city, ConstructionProjectType.SiegeWorkshop) : int.MinValue),
            ViewTableSortField.HorsePasture => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? ConstructionRules.GetLevel(city, ConstructionProjectType.HorsePasture) * 100000 + ConstructionRules.GetProgress(city, ConstructionProjectType.HorsePasture) : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? ConstructionRules.GetLevel(city, ConstructionProjectType.HorsePasture) * 100000 + ConstructionRules.GetProgress(city, ConstructionProjectType.HorsePasture) : int.MinValue),
            ViewTableSortField.Ram => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.RamCount * 100000 + city.RamProgress : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.RamCount * 100000 + city.RamProgress : int.MinValue),
            ViewTableSortField.Catapult => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.CatapultCount * 100000 + city.CatapultProgress : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.CatapultCount * 100000 + city.CatapultProgress : int.MinValue),
            ViewTableSortField.Ladder => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.LadderCount * 100000 + city.LadderProgress : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.LadderCount * 100000 + city.LadderProgress : int.MinValue),
            ViewTableSortField.Loyalty => _viewTableSortAscending
                ? result.OrderBy(city => CanViewCityFullInformation(city) ? city.Loyalty : int.MinValue)
                : result.OrderByDescending(city => CanViewCityFullInformation(city) ? city.Loyalty : int.MinValue),
            _ => _viewTableSortAscending
                ? result.OrderBy(city => _localization?.GetCityName(city) ?? city.NameEn)
                : result.OrderByDescending(city => _localization?.GetCityName(city) ?? city.NameEn)
        };

        return ordered.ThenBy(city => city.Id).ToList();
    }

    private void ConfigureViewTableColumns()
    {
        if (_officerListTable == null || _localization == null)
        {
            return;
        }

        if (_officerListContentMode == OfficerListContentMode.Cities)
        {
            _officerListTable.Columns = 17;
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
            SetViewTableColumn(10, _localization.T("ui.bow_workshop"), 140, ViewTableSortField.BowWorkshop);
            SetViewTableColumn(11, _localization.T("ui.siege_workshop"), 140, ViewTableSortField.SiegeWorkshop);
            SetViewTableColumn(12, _localization.T("ui.horse_pasture"), 140, ViewTableSortField.HorsePasture);
            SetViewTableColumn(13, _localization.T("siege_engine.ram"), 120, ViewTableSortField.Ram);
            SetViewTableColumn(14, _localization.T("siege_engine.catapult"), 120, ViewTableSortField.Catapult);
            SetViewTableColumn(15, _localization.T("siege_engine.ladder"), 120, ViewTableSortField.Ladder);
            SetViewTableColumn(16, _localization.T("ui.loyalty"), 90, ViewTableSortField.Loyalty);
            StretchTrailingViewColumns(17, 10, 130 + 140 + 90 + 90 + 110 + 90 + 90 + 90 + 110 + 90);
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
            StretchTrailingViewColumns(11, 4, 170 + 130 + 110 + 90);
            return;
        }

        if (_officerListContentMode == OfficerListContentMode.DiplomacyRelations)
        {
            _officerListTable.Columns = 4;
            SetViewTableColumn(0, _localization.T("ui.target_faction"), 160, ViewTableSortField.Name);
            SetViewTableColumn(1, _localization.T("ui.relation_status"), 120, ViewTableSortField.RelationStatus);
            SetViewTableColumn(2, _localization.T("ui.remaining_months"), 120, ViewTableSortField.RemainingMonths);
            SetViewTableColumn(3, _localization.T("ui.relation_score"), 110, ViewTableSortField.RelationScore);
            StretchTrailingViewColumns(4, 1, 160);
            return;
        }

        var includeCityName = _officerListScope == OfficerListScope.Faction;
        _officerListTable.Columns = includeCityName ? 13 : 12;
        SetViewTableColumn(0, _localization.T("ui.officers"), 170, ViewTableSortField.Name);
        SetViewTableColumn(1, _localization.T("ui.role"), 120, ViewTableSortField.Role);
        SetViewTableColumn(2, _localization.T("ui.appointed_titles"), 170, ViewTableSortField.Appointment);
        SetViewTableColumn(3, _localization.T("ui.status"), 100, ViewTableSortField.Status);
        if (includeCityName)
        {
            SetViewTableColumn(4, _localization.T("ui.city"), 140, ViewTableSortField.City);
            SetViewTableColumn(5, _localization.T("ui.age"), 70, ViewTableSortField.Age);
            SetViewTableColumn(6, _localization.T("ui.loyalty"), 90, ViewTableSortField.OfficerLoyalty);
            SetViewTableColumn(7, _localization.T("ui.strength"), 90, ViewTableSortField.Strength);
            SetViewTableColumn(8, _localization.T("ui.intelligence"), 90, ViewTableSortField.Intelligence);
            SetViewTableColumn(9, _localization.T("ui.charm"), 90, ViewTableSortField.Charm);
            SetViewTableColumn(10, _localization.T("ui.leadership"), 90, ViewTableSortField.Leadership);
            SetViewTableColumn(11, _localization.T("ui.politics"), 90, ViewTableSortField.Politics);
            SetViewTableColumn(12, _localization.T("ui.combat"), 90, ViewTableSortField.Combat);
            StretchTrailingViewColumns(13, 6, 170 + 120 + 170 + 100 + 140 + 70);
        }
        else
        {
            SetViewTableColumn(4, _localization.T("ui.age"), 70, ViewTableSortField.Age);
            SetViewTableColumn(5, _localization.T("ui.loyalty"), 90, ViewTableSortField.OfficerLoyalty);
            SetViewTableColumn(6, _localization.T("ui.strength"), 90, ViewTableSortField.Strength);
            SetViewTableColumn(7, _localization.T("ui.intelligence"), 90, ViewTableSortField.Intelligence);
            SetViewTableColumn(8, _localization.T("ui.charm"), 90, ViewTableSortField.Charm);
            SetViewTableColumn(9, _localization.T("ui.leadership"), 90, ViewTableSortField.Leadership);
            SetViewTableColumn(10, _localization.T("ui.politics"), 90, ViewTableSortField.Politics);
            SetViewTableColumn(11, _localization.T("ui.combat"), 90, ViewTableSortField.Combat);
            StretchTrailingViewColumns(12, 5, 170 + 120 + 170 + 100 + 70);
        }
    }

    private void SetViewTableColumn(int column, string title, int minWidth, ViewTableSortField field)
    {
        _officerListTable?.SetColumnTitle(column, BuildSortableColumnTitle(title, field));
        _officerListTable?.SetColumnCustomMinimumWidth(column, minWidth);
        _officerListTable?.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
    }

    private void StretchTrailingViewColumns(int columnCount, int trailingColumnStart, int fixedWidth)
    {
        if (_officerListTable == null)
        {
            return;
        }

        var totalWidth = (int)Mathf.Round(_officerListTable.Size.X);
        if (totalWidth <= 0)
        {
            totalWidth = (int)Mathf.Round(_officerListTable.CustomMinimumSize.X);
        }

        var trailingCount = columnCount - trailingColumnStart;
        if (trailingCount <= 0)
        {
            return;
        }

        var availableWidth = Mathf.Max(0, totalWidth - fixedWidth);
        var trailingWidth = Mathf.Max(90, availableWidth / trailingCount);
        for (var column = 0; column < columnCount; column += 1)
        {
            var isTrailing = column >= trailingColumnStart;
            _officerListTable.SetColumnExpand(column, false);
            if (isTrailing)
            {
                _officerListTable.SetColumnCustomMinimumWidth(column, trailingWidth);
            }
        }
    }

    private string BuildSortableColumnTitle(string title, ViewTableSortField field)
    {
        if (_viewTableSortField != field)
        {
            return title;
        }

        return _viewTableSortAscending ? $"{title} ^" : $"{title} v";
    }

    private static void ApplyViewTableRowStriping(TreeItem row, int rowIndex, int columnCount)
    {
        var background = rowIndex % 2 == 0
            ? new Color(0.12f, 0.12f, 0.14f, 0.84f)
            : new Color(0.16f, 0.16f, 0.18f, 0.8f);
        var textColor = new Color(0.92f, 0.89f, 0.82f, 1.0f);

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
        var background = new Color(0.55f, 0.45f, 0.28f, 0.92f);
        var textColor = new Color(0.98f, 0.95f, 0.9f, 1.0f);

        for (var column = 0; column < columnCount; column += 1)
        {
            row.SetCustomBgColor(column, background, false);
            row.SetCustomColor(column, textColor);
        }
    }

    private void RefreshViewTableRowStriping()
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

        var selectedRow = _officerListTable.GetSelected();
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
        row.SetText(2, BuildMaskedOfficerAppointments(officer));
        var world = _turnManager!.World!;
        row.SetText(3, BuildMaskedOfficerStatus(world, officer));
        var officerAge = CalculateOfficerAge(officer, world.Year);
        var loyaltyText = BuildMaskedOfficerLoyalty(world, officer);
        if (includeCityName)
        {
            var city = _turnManager?.World?.GetCity(officer.CaptiveFactionId > 0 ? officer.JailedCityId : officer.CityId);
            row.SetText(4, canViewOfficer && city != null ? _localization.GetCityName(city) : UnknownInfoText);
            row.SetText(5, MaskedNumberText(canViewOfficer, officerAge));
            row.SetText(6, loyaltyText);
            row.SetText(7, MaskedNumberText(canViewOfficer, officer.Strength));
            row.SetText(8, MaskedNumberText(canViewOfficer, officer.Intelligence));
            row.SetText(9, MaskedNumberText(canViewOfficer, officer.Charm));
            row.SetText(10, MaskedNumberText(canViewOfficer, officer.Leadership));
            row.SetText(11, MaskedNumberText(canViewOfficer, officer.Politics));
            row.SetText(12, MaskedNumberText(canViewOfficer, officer.Combat));
        }
        else
        {
            row.SetText(4, MaskedNumberText(canViewOfficer, officerAge));
            row.SetText(5, loyaltyText);
            row.SetText(6, MaskedNumberText(canViewOfficer, officer.Strength));
            row.SetText(7, MaskedNumberText(canViewOfficer, officer.Intelligence));
            row.SetText(8, MaskedNumberText(canViewOfficer, officer.Charm));
            row.SetText(9, MaskedNumberText(canViewOfficer, officer.Leadership));
            row.SetText(10, MaskedNumberText(canViewOfficer, officer.Politics));
            row.SetText(11, MaskedNumberText(canViewOfficer, officer.Combat));
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
        row.SetText(10, canViewCity ? _localization.FormatFacilityProgress(city, ConstructionProjectType.BowWorkshop) : UnknownInfoText);
        row.SetText(11, canViewCity ? _localization.FormatFacilityProgress(city, ConstructionProjectType.SiegeWorkshop) : UnknownInfoText);
        row.SetText(12, canViewCity ? _localization.FormatFacilityProgress(city, ConstructionProjectType.HorsePasture) : UnknownInfoText);
        row.SetText(13, canViewCity ? _localization.FormatSiegeEngineProgress(city, SiegeEngineType.Ram) : UnknownInfoText);
        row.SetText(14, canViewCity ? _localization.FormatSiegeEngineProgress(city, SiegeEngineType.Catapult) : UnknownInfoText);
        row.SetText(15, canViewCity ? _localization.FormatSiegeEngineProgress(city, SiegeEngineType.Ladder) : UnknownInfoText);
        row.SetText(16, MaskedNumberText(canViewCity, city.Loyalty));
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
                10 => ViewTableSortField.BowWorkshop,
                11 => ViewTableSortField.SiegeWorkshop,
                12 => ViewTableSortField.HorsePasture,
                13 => ViewTableSortField.Ram,
                14 => ViewTableSortField.Catapult,
                15 => ViewTableSortField.Ladder,
                16 => ViewTableSortField.Loyalty,
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
                2 => ViewTableSortField.Appointment,
                3 => ViewTableSortField.Status,
                4 => ViewTableSortField.City,
                5 => ViewTableSortField.Age,
                6 => ViewTableSortField.OfficerLoyalty,
                7 => ViewTableSortField.Strength,
                8 => ViewTableSortField.Intelligence,
                9 => ViewTableSortField.Charm,
                10 => ViewTableSortField.Leadership,
                11 => ViewTableSortField.Politics,
                12 => ViewTableSortField.Combat,
                _ => ViewTableSortField.Name
            };
        }

        return column switch
        {
            1 => ViewTableSortField.Role,
            2 => ViewTableSortField.Appointment,
            3 => ViewTableSortField.Status,
            4 => ViewTableSortField.Age,
            5 => ViewTableSortField.OfficerLoyalty,
            6 => ViewTableSortField.Strength,
            7 => ViewTableSortField.Intelligence,
            8 => ViewTableSortField.Charm,
            9 => ViewTableSortField.Leadership,
            10 => ViewTableSortField.Politics,
            11 => ViewTableSortField.Combat,
            _ => ViewTableSortField.Name
        };
    }

    private static bool IsAscendingDefaultSortField(ViewTableSortField field)
    {
        return field is ViewTableSortField.Name or ViewTableSortField.Role or ViewTableSortField.Appointment or ViewTableSortField.Status or ViewTableSortField.City or ViewTableSortField.Owner or ViewTableSortField.Holder or ViewTableSortField.ItemType or ViewTableSortField.Rarity or ViewTableSortField.RelationStatus;
    }

    private IEnumerable<OfficerData> GetSortedOfficers(List<OfficerData> officers)
    {
        return _viewTableSortField switch
        {
            ViewTableSortField.Role => _viewTableSortAscending
                ? officers.OrderBy(GetDisplayedOfficerRole)
                : officers.OrderByDescending(GetDisplayedOfficerRole),
            ViewTableSortField.Appointment => _viewTableSortAscending
                ? officers.OrderBy(BuildOfficerAppointmentsForSort)
                : officers.OrderByDescending(BuildOfficerAppointmentsForSort),
            ViewTableSortField.Status => _viewTableSortAscending
                ? officers.OrderBy(GetOfficerStatusSortKey)
                : officers.OrderByDescending(GetOfficerStatusSortKey),
            ViewTableSortField.City => _viewTableSortAscending
                ? officers.OrderBy(GetOfficerCityNameForSort)
                : officers.OrderByDescending(GetOfficerCityNameForSort),
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
            ViewTableSortField.Charm => _viewTableSortAscending
                ? officers.OrderBy(officer => CanViewOfficerFullInformation(officer) ? officer.Charm : int.MinValue)
                : officers.OrderByDescending(officer => CanViewOfficerFullInformation(officer) ? officer.Charm : int.MinValue),
            ViewTableSortField.Leadership => _viewTableSortAscending
                ? officers.OrderBy(officer => CanViewOfficerFullInformation(officer) ? officer.Leadership : int.MinValue)
                : officers.OrderByDescending(officer => CanViewOfficerFullInformation(officer) ? officer.Leadership : int.MinValue),
            ViewTableSortField.Politics => _viewTableSortAscending
                ? officers.OrderBy(officer => CanViewOfficerFullInformation(officer) ? officer.Politics : int.MinValue)
                : officers.OrderByDescending(officer => CanViewOfficerFullInformation(officer) ? officer.Politics : int.MinValue),
            ViewTableSortField.Combat => _viewTableSortAscending
                ? officers.OrderBy(officer => CanViewOfficerFullInformation(officer) ? officer.Combat : int.MinValue)
                : officers.OrderByDescending(officer => CanViewOfficerFullInformation(officer) ? officer.Combat : int.MinValue),
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

    private string BuildOfficerAppointmentsForSort(OfficerData officer)
    {
        if (!CanViewOfficerFullInformation(officer))
        {
            return UnknownInfoText;
        }

        return BuildOfficerAppointmentsText(officer);
    }

    private List<ItemData> GetSortedFactionInventoryItems()
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return new List<ItemData>();
        }

        var items = _turnManager.World.Items.Where(item => item.OwnerFactionId == _selectedCity.OwnerFactionId).ToList();
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

        return ordered.ThenBy(item => item.Id).ToList();
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

        return ordered.ThenBy(item => item.TargetFactionId).ToList();
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

        var city = _turnManager.World.GetCity(officer.CaptiveFactionId > 0 ? officer.JailedCityId : officer.CityId);
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
}
