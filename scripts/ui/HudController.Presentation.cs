using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;

namespace ThreeKingdom.UI;

public partial class HudController : CanvasLayer
{
    private void OnLanguageChanged()
    {
        RefreshAllText();
    }

    private void ApplyOfficerListDialogTheme()
    {
        if (_officerListDialog == null)
        {
            return;
        }

        var officerListPanel = _officerListDialog.GetNodeOrNull<PanelContainer>("CenterContainer/AdvisorDialogPanel");
        if (officerListPanel == null)
        {
            return;
        }

        var dialogPanel = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.1f, 0.88f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.48f, 0.39f, 0.24f, 0.92f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusBottomLeft = 10
        };
        officerListPanel.AddThemeStyleboxOverride("panel", dialogPanel);

        var okNormal = new StyleBoxFlat
        {
            BgColor = new Color(0.86f, 0.78f, 0.6f, 1.0f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.48f, 0.36f, 0.2f, 1.0f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusBottomLeft = 6
        };
        var okHover = (StyleBoxFlat)okNormal.Duplicate();
        okHover.BgColor = new Color(0.91f, 0.84f, 0.67f, 1.0f);
        var okPressed = (StyleBoxFlat)okNormal.Duplicate();
        okPressed.BgColor = new Color(0.76f, 0.65f, 0.46f, 1.0f);

        if (_officerListConfirmButton != null)
        {
            _officerListConfirmButton.AddThemeStyleboxOverride("normal", okNormal);
            _officerListConfirmButton.AddThemeStyleboxOverride("hover", okHover);
            _officerListConfirmButton.AddThemeStyleboxOverride("pressed", okPressed);
            _officerListConfirmButton.AddThemeColorOverride("font_color", new Color(0.14f, 0.1f, 0.06f, 1.0f));
        }

        foreach (var button in new[] { _viewCityOfficersDialogButton, _viewFactionOfficersDialogButton, _viewFactionItemsDialogButton, _viewDiplomacyRelationsDialogButton, _viewCitiesDialogButton, _viewLogisticsDialogButton })
        {
            if (button == null)
            {
                continue;
            }

            var normal = new StyleBoxFlat
            {
                BgColor = new Color(0.79f, 0.71f, 0.53f, 0.95f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.54f, 0.42f, 0.24f, 1.0f),
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomRight = 6,
                CornerRadiusBottomLeft = 6
            };
            var hover = (StyleBoxFlat)normal.Duplicate();
            hover.BgColor = new Color(0.86f, 0.78f, 0.6f, 1.0f);
            var disabled = (StyleBoxFlat)normal.Duplicate();
            disabled.BgColor = new Color(0.38f, 0.35f, 0.31f, 0.94f);
            disabled.BorderColor = new Color(0.44f, 0.4f, 0.34f, 1.0f);

            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("disabled", disabled);
            button.AddThemeColorOverride("font_color", new Color(0.16f, 0.12f, 0.08f, 1.0f));
            button.AddThemeColorOverride("font_disabled_color", new Color(0.72f, 0.68f, 0.61f, 1.0f));
        }

        if (_officerListTable != null)
        {
            var tablePanel = new StyleBoxFlat
            {
                BgColor = new Color(0.1f, 0.1f, 0.12f, 0.76f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.44f, 0.37f, 0.25f, 0.88f)
            };
            var focusPanel = (StyleBoxFlat)tablePanel.Duplicate();
            focusPanel.BorderColor = new Color(0.65f, 0.49f, 0.25f, 0.98f);
            var selectedPanel = new StyleBoxFlat
            {
                BgColor = new Color(0.47f, 0.38f, 0.24f, 0.92f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.55f, 0.4f, 0.2f, 1.0f)
            };
            var selectedFocusPanel = (StyleBoxFlat)selectedPanel.Duplicate();
            selectedFocusPanel.BgColor = new Color(0.58f, 0.47f, 0.29f, 0.96f);

            var titleNormal = new StyleBoxFlat
            {
                BgColor = new Color(0.18f, 0.16f, 0.14f, 0.98f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.48f, 0.39f, 0.24f, 0.96f)
            };
            var titleHover = (StyleBoxFlat)titleNormal.Duplicate();
            titleHover.BgColor = new Color(0.24f, 0.21f, 0.17f, 1.0f);
            var titlePressed = (StyleBoxFlat)titleNormal.Duplicate();
            titlePressed.BgColor = new Color(0.33f, 0.27f, 0.18f, 1.0f);

            _officerListTable.AddThemeStyleboxOverride("panel", tablePanel);
            _officerListTable.AddThemeStyleboxOverride("focus", focusPanel);
            _officerListTable.AddThemeStyleboxOverride("selected", selectedPanel);
            _officerListTable.AddThemeStyleboxOverride("selected_focus", selectedFocusPanel);
            _officerListTable.AddThemeStyleboxOverride("title_button_normal", titleNormal);
            _officerListTable.AddThemeStyleboxOverride("title_button_hover", titleHover);
            _officerListTable.AddThemeStyleboxOverride("title_button_pressed", titlePressed);
            _officerListTable.AddThemeColorOverride("font_color", new Color(0.92f, 0.89f, 0.82f, 1.0f));
            _officerListTable.AddThemeColorOverride("font_hovered_color", new Color(0.98f, 0.95f, 0.9f, 1.0f));
            _officerListTable.AddThemeColorOverride("font_selected_color", new Color(0.98f, 0.94f, 0.86f, 1.0f));
            _officerListTable.AddThemeColorOverride("font_hovered_selected_color", Colors.White);
            _officerListTable.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.0f));
            _officerListTable.AddThemeColorOverride("custom_button_font_highlight", new Color(0.95f, 0.91f, 0.82f, 1.0f));
            _officerListTable.AddThemeColorOverride("custom_button_font_highlight_pressed", Colors.White);
            _officerListTable.AddThemeColorOverride("title_button_color", new Color(0.91f, 0.87f, 0.77f, 1.0f));
            _officerListTable.AddThemeColorOverride("title_button_hover_color", new Color(0.98f, 0.95f, 0.9f, 1.0f));
            _officerListTable.AddThemeColorOverride("title_button_pressed_color", new Color(0.98f, 0.95f, 0.9f, 1.0f));
            _officerListTable.AddThemeColorOverride("guide_color", new Color(0.44f, 0.37f, 0.25f, 0.58f));
            _officerListTable.AddThemeColorOverride("drop_position_color", new Color(0.75f, 0.55f, 0.22f, 1.0f));
        }
    }

    private void RefreshAllText()
    {
        if (_localization == null)
        {
            return;
        }

        _mainHudUiController?.RefreshText();
        _systemUiController?.RefreshText();
        _viewUiController?.RefreshText();

        _merchantUiController?.RefreshText();
        _diplomacyUiController?.RefreshText();
        _spyUiController?.RefreshText();
        _militaryUiController?.RefreshText();
        _personnelUiController?.RefreshText();
        _advisorUiController?.RefreshText();
        _civilUiController?.RefreshText();
        _internalAffairsUiController?.RefreshText();
        RefreshSelectOfficerDialogText();
    }

    private void RefreshSelectedCity()
    {
        _mainHudUiController?.RefreshSelectedCity();
        _merchantUiController?.RefreshSelectedCity();
        _internalAffairsUiController?.RefreshSelectedCity();
        _viewUiController?.RefreshOfficerListChrome();
        _viewUiController?.RefreshOfficerListContent();
    }

    private string BuildOfficerDetailText(OfficerData officer, bool includeLoyalty = true)
    {
        var canViewOfficer = CanViewOfficerFullInformation(officer);
        var officerName = canViewOfficer ? (_localization?.GetOfficerName(officer) ?? officer.Name) : UnknownInfoText;
        var roleName = canViewOfficer ? GetDisplayedOfficerRole(officer) : UnknownInfoText;
        var appointmentName = canViewOfficer ? BuildOfficerAppointmentsText(officer) : UnknownInfoText;
        var generalTitle = _localization?.GetProgressionTitle(officer.GeneralTitle) ?? officer.GeneralTitle;
        var strategistTitle = _localization?.GetProgressionTitle(officer.StrategistTitle) ?? officer.StrategistTitle;
        var spyTitle = _localization?.GetProgressionTitle(officer.SpyTitle) ?? officer.SpyTitle;
        var diplomacyTitle = _localization?.GetProgressionTitle(officer.DiplomacyTitle) ?? officer.DiplomacyTitle;
        var civilTitle = _localization?.GetProgressionTitle(officer.CivilTitle) ?? officer.CivilTitle;
        var farmTitle = _localization?.GetProgressionTitle(officer.FarmTitle) ?? officer.FarmTitle;
        var commercialTitle = _localization?.GetProgressionTitle(officer.CommercialTitle) ?? officer.CommercialTitle;
        var defendTitle = _localization?.GetProgressionTitle(officer.DefendTitle) ?? officer.DefendTitle;
        var disasterPreventionTitle = _localization?.GetProgressionTitle(officer.DisasterPreventionTitle) ?? officer.DisasterPreventionTitle;
        var constructionTitle = _localization?.GetProgressionTitle(officer.ConstructionTitle) ?? officer.ConstructionTitle;
        var currentYear = _turnManager?.World?.Year ?? 0;
        var officerAge = CalculateOfficerAge(officer, currentYear);
        var itemSummary = BuildOfficerItemSummary(officer);
        var statusValue = _turnManager?.World != null && _localization != null
            ? BuildMaskedOfficerStatus(_turnManager.World, officer)
            : UnknownInfoText;
        var entries = new List<(string Label, string Value)>
        {
            (_localization?.T("ui.role") ?? "Role", roleName),
            (_localization?.T("ui.appointed_titles") ?? "Appointments", appointmentName),
            (_localization?.T("ui.status") ?? "Status", statusValue),
            (_localization?.T("ui.age") ?? "Age", MaskedNumberText(canViewOfficer, officerAge)),
            (_localization?.T("ui.strength") ?? "STR", MaskedNumberText(canViewOfficer, officer.Strength)),
            (_localization?.T("ui.intelligence") ?? "INT", MaskedNumberText(canViewOfficer, officer.Intelligence)),
            (_localization?.T("ui.charm") ?? "CHA", MaskedNumberText(canViewOfficer, officer.Charm)),
            (_localization?.T("ui.leadership") ?? "LEA", MaskedNumberText(canViewOfficer, officer.Leadership)),
            (_localization?.T("ui.politics") ?? "POL", MaskedNumberText(canViewOfficer, officer.Politics)),
            (_localization?.T("ui.combat") ?? "COM", MaskedNumberText(canViewOfficer, officer.Combat))
        };

        if (includeLoyalty)
        {
            entries.Insert(4, (_localization?.T("ui.loyalty_short") ?? "LOY", MaskedNumberText(canViewOfficer, officer.Loyalty)));
        }

        if (canViewOfficer && HasBattleProgression(officer))
        {
            entries.Add((_localization?.T("ui.military_rank") ?? "Military Rank", officer.MilitaryRank.ToString()));
            entries.Add((_localization?.T("ui.general_title") ?? "General Title", generalTitle));
        }

        if (canViewOfficer && HasStrategistProgression(officer))
        {
            entries.Add((_localization?.T("ui.strategist_rank") ?? "Strategist Rank", officer.StrategistRank.ToString()));
            entries.Add((_localization?.T("ui.strategist_title") ?? "Strategist Title", strategistTitle));
        }

        if (canViewOfficer && HasSpyProgression(officer))
        {
            entries.Add((_localization?.T("ui.spy_rank") ?? "Spy Rank", officer.SpyRank.ToString()));
            entries.Add((_localization?.T("ui.spy_title") ?? "Spy Title", spyTitle));
        }

        if (canViewOfficer && HasDiplomacyProgression(officer))
        {
            entries.Add((_localization?.T("ui.diplomacy_rank") ?? "Diplomacy Rank", officer.DiplomacyRank.ToString()));
            entries.Add((_localization?.T("ui.diplomacy_title") ?? "Diplomacy Title", diplomacyTitle));
        }

        if (canViewOfficer && HasCivilProgression(officer))
        {
            entries.Add((_localization?.T("ui.civil_rank") ?? "Civil Rank", officer.CivilRank.ToString()));
            entries.Add((_localization?.T("ui.civil_title") ?? "Civil Title", civilTitle));
        }

        entries.Add((_localization?.T("ui.ambition") ?? "AMB", MaskedNumberText(canViewOfficer, officer.Ambition)));
        var internalAffairsEntries = new List<(string Label, string Value)>
        {
            (_localization?.T("ui.battle_experience") ?? "Battle Experience", MaskedNumberText(canViewOfficer, officer.BattleExperience)),
            (_localization?.T("ui.farm_experience") ?? "Farm Experience", canViewOfficer ? FormatOfficerProgressionValue(officer.FarmExperience, officer.FarmRank, farmTitle) : UnknownInfoText),
            (_localization?.T("ui.commercial_experience") ?? "Commercial Experience", canViewOfficer ? FormatOfficerProgressionValue(officer.CommercialExperience, officer.CommercialRank, commercialTitle) : UnknownInfoText),
            (_localization?.T("ui.defend_experience") ?? "Defend Experience", canViewOfficer ? FormatOfficerProgressionValue(officer.DefendExperience, officer.DefendRank, defendTitle) : UnknownInfoText),
            (_localization?.T("ui.disaster_prevention_experience") ?? "Disaster Prevention Experience", canViewOfficer ? FormatOfficerProgressionValue(officer.DisasterPreventionExperience, officer.DisasterPreventionRank, disasterPreventionTitle) : UnknownInfoText),
            (_localization?.T("ui.construction_experience") ?? "Construction Experience", canViewOfficer ? FormatOfficerProgressionValue(officer.ConstructionExperience, officer.ConstructionRank, constructionTitle) : UnknownInfoText),
            (_localization?.T("ui.spy_experience") ?? "Spy EXP", MaskedNumberText(canViewOfficer, officer.SpyExperience)),
            (_localization?.T("ui.diplomacy_experience") ?? "Diplomacy EXP", MaskedNumberText(canViewOfficer, officer.DiplomacyExperience))
        };

        var bb = new System.Text.StringBuilder();
        bb.Append(officerName);
        bb.Append('\n');
        bb.Append("[table=6]");
        for (var index = 0; index < entries.Count; index += 3)
        {
            for (var offset = 0; offset < 3; offset += 1)
            {
                var entryIndex = index + offset;
                if (entryIndex < entries.Count)
                {
                    var entry = entries[entryIndex];
                    var paddedLabel = offset == 0 ? entry.Label : $"      {entry.Label}";
                    AppendOfficerDetailCells(bb, paddedLabel, entry.Value);
                    continue;
                }

                AppendOfficerDetailCells(bb, string.Empty, string.Empty);
            }
        }

        bb.Append("[/table]");
        bb.Append('\n');
        bb.Append("[table=6]");
        for (var index = 0; index < internalAffairsEntries.Count; index += 3)
        {
            for (var offset = 0; offset < 3; offset += 1)
            {
                var entryIndex = index + offset;
                if (entryIndex < internalAffairsEntries.Count)
                {
                    var entry = internalAffairsEntries[entryIndex];
                    var paddedLabel = offset == 0 ? entry.Label : $"      {entry.Label}";
                    AppendOfficerDetailCells(bb, paddedLabel, entry.Value);
                    continue;
                }

                AppendOfficerDetailCells(bb, string.Empty, string.Empty);
            }
        }

        bb.Append("[/table]");
        bb.Append('\n');
        bb.Append(itemSummary);
        return bb.ToString();
    }

    private string FormatOfficerProgressionValue(int experience, int rank, string title)
    {
        if (rank <= 0 || string.IsNullOrWhiteSpace(title))
        {
            return experience.ToString();
        }

        return _localization?.Format("fmt.experience_title", experience, title)
            ?? $"{experience} ({title})";
    }

    private static int CalculateOfficerAge(OfficerData officer, int currentYear)
    {
        if (officer.BirthYear <= 0 || currentYear <= 0)
        {
            return 0;
        }

        return Math.Max(0, currentYear - officer.BirthYear);
    }

    private static bool HasBattleProgression(OfficerData officer)
    {
        return officer.MilitaryRank > 0;
    }

    private static bool HasStrategistProgression(OfficerData officer)
    {
        return officer.StrategistRank > 0;
    }

    private static bool HasCivilProgression(OfficerData officer)
    {
        return officer.CivilRank > 0;
    }

    private static bool HasSpyProgression(OfficerData officer)
    {
        return officer.SpyRank > 0;
    }

    private static bool HasDiplomacyProgression(OfficerData officer)
    {
        return officer.DiplomacyRank > 0;
    }

    private static bool IsOfficerOldEnoughToJoin(WorldState world, OfficerData officer)
    {
        return officer.BirthYear <= 0 || world.Year - officer.BirthYear >= 14;
    }

    private void EnsureOfficerDetailWidgets()
    {
        if (_officerDetailDialog == null)
        {
            return;
        }

        var existingRoot = _officerDetailDialog.GetNodeOrNull<HBoxContainer>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/OfficerDetailRoot");
        if (existingRoot == null)
        {
            GD.PushError("OfficerDetailRoot not found in OfficerDetailDialog.tscn.");
            return;
        }

        _officerPortraitRect = existingRoot.GetNodeOrNull<TextureRect>("PortraitPanel/PortraitCenter/PortraitStack/PortraitRect");
        _officerPortraitPlaceholderLabel = existingRoot.GetNodeOrNull<Label>("PortraitPanel/PortraitCenter/PortraitStack/PortraitPlaceholder");
        _officerDetailText = existingRoot.GetNodeOrNull<RichTextLabel>("DetailText");
    }

    private string BuildOfficerListRowText(OfficerData officer, bool includeCityName = false)
    {
        var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
        var roleName = GetDisplayedOfficerRole(officer);
        var cityText = string.Empty;
        if (includeCityName && _turnManager?.World != null && _localization != null)
        {
            var city = _turnManager.World.GetCity(officer.CityId);
            if (city != null)
            {
                cityText = $" | {_localization.T("ui.city")} {_localization.GetCityName(city)}";
            }
        }

        return $"{officerName} | {roleName} | {BuildOfficerStatusText(officer)}{cityText} | {_localization?.T("ui.strength") ?? "STR"} {officer.Strength} | {_localization?.T("ui.intelligence") ?? "INT"} {officer.Intelligence}";
    }

    private readonly record struct CityStatRowDefinition(
        string LeftLabel,
        string LeftValue,
        string RightLabel,
        string RightValue,
        string LeftTooltip = "",
        string RightTooltip = "");

    private void PopulateCityStatsPanel(VBoxContainer panel, string ownerName, CityData? city, int freeOfficerCount, CityInfoDataTab tab)
    {
        foreach (var child in panel.GetChildren())
        {
            child.QueueFree();
        }

        var rowIndex = 0;
        foreach (var row in BuildCityStatRows(ownerName, city, freeOfficerCount, tab))
        {
            panel.AddChild(CreateCityStatsRowControl(row, rowIndex));
            rowIndex += 1;
        }
    }

    private string BuildCityHeaderText(CityData? city)
    {
        if (_localization == null)
        {
            return string.Empty;
        }

        if (city == null)
        {
            return _localization.FormatCityHeader("-");
        }

        var cityName = _localization.GetCityName(city);
        var authorizationSuffix = BuildCityHeaderAuthorizationSuffix(city);
        return _localization.FormatCityHeader($"{cityName}{authorizationSuffix}");
    }

    private IReadOnlyList<CityStatRowDefinition> BuildCityStatRows(string ownerName, CityData? city, int freeOfficerCount, CityInfoDataTab tab)
    {
        if (_localization == null)
        {
            return Array.Empty<CityStatRowDefinition>();
        }

        var canViewCity = CanViewCityFullInformation(city);
        var intelDurationText = BuildCityIntelDurationText(city);
        var ownerValue = string.IsNullOrWhiteSpace(intelDurationText)
            ? ownerName
            : $"{ownerName} | {intelDurationText}";
        var showPrefect = city != null && HasAssignedPrefect(city);
        var prefectLabel = showPrefect ? BuildPrefectLabel() : string.Empty;
        var prefectValue = showPrefect && city != null ? BuildPrefectNameText(city) : string.Empty;

        var emptyFacility = _localization.Format(
            "fmt.facility_level_progress",
            0,
            0,
            ConstructionRules.GetRequiredPointsForNextLevel(0));
        string Number(int value) => city == null ? "0" : MaskedNumberText(canViewCity, value);
        string Storage(MarketProductType type) => city == null
            ? "0"
            : canViewCity
                ? _localization.FormatStorageAmount(city, type)
                : UnknownInfoText;
        string Facility(ConstructionProjectType type) => city == null
            ? emptyFacility
            : canViewCity
                ? _localization.FormatFacilityProgress(city, type)
                : UnknownInfoText;
        string FacilityTooltip(ConstructionProjectType type) => city != null && canViewCity
            ? _localization.FormatFacilityTooltip(city, type)
            : string.Empty;

        var ownerRow = new CityStatRowDefinition(
            _localization.T("ui.faction_owner"),
            city == null ? ownerName : ownerValue,
            prefectLabel,
            prefectValue);
        var goldFoodRow = new CityStatRowDefinition(
            _localization.T("ui.gold"),
            Number(city?.Gold ?? 0),
            _localization.T("ui.food"),
            Storage(MarketProductType.Food));
        var populationLoyaltyRow = new CityStatRowDefinition(
            _localization.T("ui.population"),
            Number(city?.Population ?? 0),
            _localization.T("ui.loyalty"),
            Number(city?.Loyalty ?? 0));
        var officersRow = new CityStatRowDefinition(
            _localization.T("ui.officers"),
            Number(city?.OfficerIds.Count ?? 0),
            _localization.T("ui.free_officers"),
            Number(freeOfficerCount));
        var troopsDefenseRow = new CityStatRowDefinition(
            _localization.T("ui.troops"),
            Number(city?.Troops ?? 0),
            _localization.T("ui.defense"),
            Number(city?.Defense ?? 0));
        var farmCommercialRow = new CityStatRowDefinition(
            _localization.T("ui.farm"),
            Number(city?.Farm ?? 0),
            _localization.T("ui.commercial"),
            Number(city?.Commercial ?? 0));

        return tab switch
        {
            CityInfoDataTab.Resources =>
            [
                goldFoodRow,
                new CityStatRowDefinition(_localization.T("ui.horse"), Storage(MarketProductType.Horse), _localization.T("ui.resource_deposits"), city == null ? _localization.T("ui.none") : canViewCity ? BuildResourceDepositSummary(city) : UnknownInfoText),
                new CityStatRowDefinition(_localization.T("ui.wood"), Storage(MarketProductType.Wood), _localization.T("ui.metal"), Storage(MarketProductType.Metal)),
                new CityStatRowDefinition(_localization.T("ui.stone"), Storage(MarketProductType.Stone), string.Empty, string.Empty)
            ],
            CityInfoDataTab.Military =>
            [
                troopsDefenseRow,
                officersRow,
                new CityStatRowDefinition(_localization.T("troop_type.infantry"), Number(city?.InfantryTroops ?? 0), _localization.T("troop_type.spearman"), Number(city?.SpearmanTroops ?? 0)),
                new CityStatRowDefinition(_localization.T("troop_type.cavalry"), Number(city?.CavalryTroops ?? 0), _localization.T("troop_type.archer"), Number(city?.ArcherTroops ?? 0)),
                new CityStatRowDefinition(_localization.T("troop_type.crossbow"), Number(city?.CrossbowTroops ?? 0), _localization.T("troop_type.engineer"), Number(city?.EngineerTroops ?? 0))
            ],
            CityInfoDataTab.Construction =>
            [
                new CityStatRowDefinition(_localization.T("ui.bow_workshop"), Facility(ConstructionProjectType.BowWorkshop), _localization.T("ui.siege_workshop"), Facility(ConstructionProjectType.SiegeWorkshop), FacilityTooltip(ConstructionProjectType.BowWorkshop), city != null && canViewCity ? _localization.FormatSiegeWorkshopTooltip(city) : string.Empty),
                new CityStatRowDefinition(_localization.T("ui.horse_pasture"), Facility(ConstructionProjectType.HorsePasture), _localization.T("ui.horse_stable"), Facility(ConstructionProjectType.HorseStable), FacilityTooltip(ConstructionProjectType.HorsePasture), FacilityTooltip(ConstructionProjectType.HorseStable)),
                new CityStatRowDefinition(_localization.T("ui.granary"), Facility(ConstructionProjectType.Granary), _localization.T("ui.resource_depot"), Facility(ConstructionProjectType.ResourceDepot), FacilityTooltip(ConstructionProjectType.Granary), FacilityTooltip(ConstructionProjectType.ResourceDepot))
            ],
            CityInfoDataTab.Domestic =>
            [
                ownerRow,
                populationLoyaltyRow,
                farmCommercialRow,
                new CityStatRowDefinition(_localization.T("ui.defense"), Number(city?.Defense ?? 0), _localization.T("ui.disaster_prevention"), Number(city?.DisasterPrevention ?? 0))
            ],
            _ =>
            [
                ownerRow,
                goldFoodRow,
                populationLoyaltyRow,
                officersRow,
                troopsDefenseRow,
                farmCommercialRow
            ]
        };
    }

    private Control CreateCityStatsRowControl(CityStatRowDefinition row, int rowIndex)
    {
        var rowPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0.0f, 23.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = string.IsNullOrWhiteSpace(row.LeftTooltip) ? row.RightTooltip : row.LeftTooltip,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        var container = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = string.IsNullOrWhiteSpace(row.LeftTooltip) ? row.RightTooltip : row.LeftTooltip
        };
        container.AddThemeConstantOverride("separation", 6);

        var rowStyle = new StyleBoxFlat
        {
            BgColor = rowIndex % 2 == 0
                ? new Color(0.08f, 0.075f, 0.065f, 0.78f)
                : new Color(0.045f, 0.043f, 0.038f, 0.72f),
            BorderWidthBottom = 1,
            BorderColor = new Color(0.35f, 0.27f, 0.14f, 0.55f),
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
        rowPanel.AddThemeStyleboxOverride("panel", rowStyle);
        rowPanel.AddChild(container);

        container.AddChild(CreateCityStatsLabelCell(row.LeftLabel, 48, HorizontalAlignment.Left, true, row.LeftTooltip));
        container.AddChild(CreateCityStatsValueCell(row.LeftValue, 90, row.LeftTooltip));
        container.AddChild(CreateCityStatsLabelCell(row.RightLabel, 48, HorizontalAlignment.Left, false, row.RightTooltip));
        container.AddChild(CreateCityStatsValueCell(row.RightValue, 0, row.RightTooltip));
        return rowPanel;
    }

    private static Label CreateCityStatsLabelCell(string text, int minimumWidth, HorizontalAlignment alignment, bool visibleWhenEmpty, string tooltip)
    {
        var label = new Label
        {
            Text = string.IsNullOrWhiteSpace(text) ? string.Empty : $"{text}:",
            HorizontalAlignment = alignment,
            CustomMinimumSize = new Vector2(minimumWidth, 0.0f),
            Visible = visibleWhenEmpty || !string.IsNullOrWhiteSpace(text),
            TooltipText = tooltip,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        label.AddThemeColorOverride("font_color", new Color(0.83f, 0.69f, 0.43f, 1.0f));
        return label;
    }

    private static Label CreateCityStatsValueCell(string text, int minimumWidth, string tooltip)
    {
        var label = new Label
        {
            Text = text ?? string.Empty,
            SizeFlagsHorizontal = minimumWidth > 0 ? Control.SizeFlags.Fill : Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.Off,
            TooltipText = tooltip,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        label.AddThemeColorOverride("font_color", new Color(0.94f, 0.91f, 0.82f, 1.0f));
        if (minimumWidth > 0)
        {
            label.CustomMinimumSize = new Vector2(minimumWidth, 0.0f);
        }

        return label;
    }

    private string BuildPrefectLabel()
    {
        if (_localization == null)
        {
            return string.Empty;
        }

        return _localization.GetAppointmentName(OfficerAppointmentRules.Governor);
    }

    private string BuildResourceDepositSummary(CityData city)
    {
        if (_localization == null || city.ResourceDeposits == null || city.ResourceDeposits.Count == 0)
        {
            return _localization?.T("ui.none") ?? "None";
        }

        return string.Join("/", city.ResourceDeposits
            .Select(deposit => deposit.RemainingReserve > 0
                ? $"{GetResourceDisplayName(deposit.Type)} {deposit.RemainingReserve}"
                : $"{GetResourceDisplayName(deposit.Type)} {_localization.T("ui.resource_depleted")}"));
    }

    private string GetResourceDisplayName(StrategicResourceType type) => type switch
    {
        StrategicResourceType.Wood => _localization?.T("ui.wood") ?? "Wood",
        StrategicResourceType.Metal => _localization?.T("ui.metal") ?? "Metal",
        StrategicResourceType.Stone => _localization?.T("ui.stone") ?? "Stone",
        _ => string.Empty
    };

    private string BuildPrefectNameText(CityData city)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return string.Empty;
        }

        if (!CanViewCityFullInformation(city))
        {
            return UnknownInfoText;
        }

        var prefect = city.OfficerIds
            .Select(id => _turnManager.World.GetOfficer(id))
            .FirstOrDefault(officer =>
                officer != null &&
                OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Governor));
        return prefect != null
            ? _localization.GetOfficerName(prefect)
            : _localization.T("ui.unassigned");
    }

    private bool HasAssignedPrefect(CityData city)
    {
        if (_turnManager?.World == null)
        {
            return false;
        }

        return city.OfficerIds
            .Select(id => _turnManager.World.GetOfficer(id))
            .Any(officer =>
                officer != null &&
                OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Governor));
    }

    private string BuildPrefectAuthorizationText(CityData city)
    {
        if (_localization == null)
        {
            return string.Empty;
        }

        if (!CanViewCityFullInformation(city))
        {
            return UnknownInfoText;
        }

        return city.PrefectAuthorizationType switch
        {
            PrefectAuthorizationType.None => _localization.T("ui.prefect_authorization.none"),
            PrefectAuthorizationType.Half => _localization.T("ui.prefect_authorization.half"),
            PrefectAuthorizationType.Full => _localization.T("ui.prefect_authorization.full"),
            _ => city.PrefectAuthorizationType.ToString()
        };
    }

    private string BuildCityHeaderAuthorizationSuffix(CityData city)
    {
        if (!CanViewCityFullInformation(city) || city.PrefectAuthorizationType == PrefectAuthorizationType.None)
        {
            return string.Empty;
        }

        var authorizationText = BuildPrefectAuthorizationText(city);
        return string.IsNullOrWhiteSpace(authorizationText)
            ? string.Empty
            : $" ({authorizationText})";
    }

    private string BuildOfficerAppointmentsText(OfficerData officer)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return string.Empty;
        }

        var appointments = new List<string>();
        foreach (var appointment in officer.Appointments)
        {
            if (appointment.Equals(OfficerAppointmentRules.Lord, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            appointments.Add(_localization.GetAppointmentName(appointment));
        }

        var officerFaction = _turnManager.World.Factions.FirstOrDefault(faction => faction.OfficerIds.Contains(officer.Id));
        if (officerFaction != null)
        {
            if (officerFaction.ChancellorOfficerId == officer.Id)
            {
                appointments.Add(_localization.GetAppointmentName(OfficerAppointmentRules.Chancellor));
            }

            if (officerFaction.ChiefStrategistOfficerId == officer.Id)
            {
                appointments.Add(_localization.GetAppointmentName(OfficerAppointmentRules.ChiefStrategist));
            }
        }

        var distinctAppointments = appointments
            .Where(static appointment => !string.IsNullOrWhiteSpace(appointment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return distinctAppointments.Count > 0
            ? string.Join(" / ", distinctAppointments)
            : _localization.T("ui.none");
    }

    private string BuildAdvisorSummaryText(CityData city)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return string.Empty;
        }

        if (_selectedCity == null || city.OwnerFactionId != _turnManager.GetPlayerFactionId())
        {
            return string.Empty;
        }

        var faction = _turnManager.World.GetFaction(city.OwnerFactionId);
        if (faction == null)
        {
            return string.Empty;
        }

        var chancellor = _turnManager.World.GetOfficer(faction.ChancellorOfficerId);
        var chiefStrategist = _turnManager.World.GetOfficer(faction.ChiefStrategistOfficerId);
        var chancellorName = chancellor != null ? _localization.GetOfficerName(chancellor) : _localization.T("ui.unassigned");
        var chiefStrategistName = chiefStrategist != null ? _localization.GetOfficerName(chiefStrategist) : _localization.T("ui.unassigned");
        var chancellorComment = BuildChancellorComment(city);
        var chiefStrategistComment = BuildChiefStrategistComment(city);

        return
            $"{BuildAdvisorHeading(_localization.T("ui.chancellor"), chancellorName)}\n" +
            $"{BuildAdvisorCommentLine(chancellorComment)}\n" +
            $"{BuildAdvisorHeading(_localization.T("ui.chief_strategist"), chiefStrategistName)}\n" +
            $"{BuildAdvisorCommentLine(chiefStrategistComment)}";
    }

    private string BuildAdvisorHeading(string title, string speakerName)
    {
        var safeTitle = EscapeBbCodeText(title);
        var safeSpeakerName = EscapeBbCodeText(speakerName);
        return $"[color=#8A5A20]{safeTitle}[/color]: {safeSpeakerName}";
    }

    private string BuildAdvisorCommentLine(string comment)
    {
        var prefix = _localization?.T("ui.advisor_comment_prefix") ?? "Comment";
        return $"{EscapeBbCodeText(prefix)}: {EscapeBbCodeText(comment)}";
    }

    private static string EscapeBbCodeText(string value)
    {
        return value
            .Replace("[", "[lb]")
            .Replace("]", "[rb]");
    }

    private string BuildChancellorComment(CityData city)
    {
        if (_localization == null)
        {
            return string.Empty;
        }

        if (_turnManager?.World == null)
        {
            return string.Empty;
        }

        var faction = _turnManager.World.GetFaction(city.OwnerFactionId);
        if (faction == null || faction.ChancellorOfficerId <= 0)
        {
            return _localization.T("ui.advisor_comment_no_chancellor");
        }

        if (city.Loyalty < 70)
        {
            return _localization.T("ui.advisor_comment_chancellor_loyalty");
        }

        if (city.Food < Math.Max(2000, city.Troops * 2))
        {
            return _localization.T("ui.advisor_comment_chancellor_food");
        }

        if (city.Gold < 800)
        {
            return _localization.T("ui.advisor_comment_chancellor_gold");
        }

        if (city.Population < 30000)
        {
            return _localization.T("ui.advisor_comment_chancellor_population");
        }

        return _localization.T("ui.advisor_comment_chancellor_balanced");
    }

    private string BuildChiefStrategistComment(CityData city)
    {
        if (_localization == null || _turnManager?.World == null)
        {
            return string.Empty;
        }

        var faction = _turnManager.World.GetFaction(city.OwnerFactionId);
        if (faction == null || faction.ChiefStrategistOfficerId <= 0)
        {
            return _localization.T("ui.advisor_comment_no_chief_strategist");
        }

        var adjacentEnemyCities = city.ConnectedCityIds
            .Select(id => _turnManager.World.GetCity(id))
            .Where(target => target != null && target.OwnerFactionId > 0 && target.OwnerFactionId != city.OwnerFactionId)
            .ToList();

        if (adjacentEnemyCities.Count > 0)
        {
            var strongestEnemyTroops = adjacentEnemyCities.Max(target => target!.Troops);
            if (city.Troops >= strongestEnemyTroops * 1.2f && city.Food >= 1000)
            {
                return _localization.T("ui.advisor_comment_chief_strategist_attack");
            }

            if (city.Defense < 60)
            {
                return _localization.T("ui.advisor_comment_chief_strategist_defense");
            }

            return _localization.T("ui.advisor_comment_chief_strategist_border");
        }

        if (city.Loyalty < 65)
        {
            return _localization.T("ui.advisor_comment_chief_strategist_loyalty");
        }

        return _localization.T("ui.advisor_comment_chief_strategist_layout");
    }

    private static void AppendOfficerDetailCells(System.Text.StringBuilder bb, string label, string value)
    {
        bb.Append("[cell]");
        if (!string.IsNullOrWhiteSpace(label))
        {
            bb.Append(label);
            bb.Append(":");
        }
        bb.Append("[/cell]");
        bb.Append("[cell]");
        if (!string.IsNullOrWhiteSpace(value))
        {
            bb.Append(value);
        }
        bb.Append("[/cell]");
    }

    private string BuildOfficerItemSummary(OfficerData officer)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return $"{_localization?.T("ui.officer_items") ?? "Equipped Items"}: -";
        }

        if (!CanViewOfficerFullInformation(officer))
        {
            return _localization.Format("fmt.officer_items", _localization.T("ui.officer_items"), UnknownInfoText);
        }

        var items = _turnManager.World.Items
            .Where(item => item.EquippedOfficerId == officer.Id)
            .Select(item => _localization.GetItemName(item))
            .ToList();
        var names = items.Count == 0 ? "-" : string.Join(", ", items);
        return _localization.Format("fmt.officer_items", _localization.T("ui.officer_items"), names);
    }

    private string BuildCityListRowText(CityData city)
    {
        var cityName = _localization?.GetCityName(city) ?? city.NameEn;
        var ownerName = _turnManager?.World != null && _localization != null
            ? _localization.GetFactionName(_turnManager.World, city.OwnerFactionId)
            : city.OwnerFactionId.ToString();
        var canViewCity = CanViewCityFullInformation(city);
        var intelDurationText = BuildCityIntelDurationText(city);
        var ownerText = string.IsNullOrWhiteSpace(intelDurationText)
            ? ownerName
            : $"{ownerName} | {intelDurationText}";
        return $"{cityName} | {_localization?.T("ui.faction_owner") ?? "Owner"} {ownerText} | {_localization?.T("ui.gold") ?? "Gold"} {MaskedNumberText(canViewCity, city.Gold)} | {_localization?.T("ui.food") ?? "Food"} {MaskedNumberText(canViewCity, city.Food)} | {_localization?.T("ui.population") ?? "Population"} {MaskedNumberText(canViewCity, city.Population)} | {_localization?.T("ui.troops") ?? "Troops"} {MaskedNumberText(canViewCity, city.Troops)} | {_localization?.T("ui.officers") ?? "Officers"} {MaskedNumberText(canViewCity, city.OfficerIds.Count)}";
    }

    private string BuildOfficerStatusText(OfficerData officer)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return "Status: Idle";
        }

        return $"{_localization.T("ui.status")}: {BuildMaskedOfficerStatus(_turnManager.World, officer)}";
    }

    private void LoadPortraitData()
    {
        _officerPortraitTextures.Clear();
        foreach (var portraitSource in PortraitSources)
        {
            var portraitSheetTexture = ResourceLoader.Load<Texture2D>(portraitSource.SheetPath);
            if (portraitSheetTexture == null || !FileAccess.FileExists(portraitSource.MappingPath))
            {
                continue;
            }

            using var file = FileAccess.Open(portraitSource.MappingPath, FileAccess.ModeFlags.Read);
            var rawText = file.GetAsText();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                continue;
            }

            foreach (var entry in ParsePortraitMappingEntries(rawText))
            {
                _officerPortraitTextures[entry.CharId] = BuildPortraitAtlasTexture(portraitSheetTexture, entry);
            }
        }
    }

    private IEnumerable<PortraitMappingEntry> ParsePortraitMappingEntries(string rawText)
    {
        try
        {
            var parsedEntries = JsonSerializer.Deserialize<List<PortraitMappingEntry>>(rawText);
            if (parsedEntries != null && parsedEntries.Count > 0)
            {
                return parsedEntries.Where(static entry => entry.CharId > 0 && entry.Width > 0 && entry.Height > 0);
            }
        }
        catch (JsonException)
        {
            // Fallback to tolerant regex parsing for hand-edited mapping files.
        }

        return ParsePortraitMappingEntriesWithRegex(rawText);
    }

    private static IEnumerable<PortraitMappingEntry> ParsePortraitMappingEntriesWithRegex(string rawText)
    {
        var matches = Regex.Matches(
            rawText,
            "\\{\\s*\"charId\"\\s*:\\s*(\\d+)\\s*,\\s*\"x\"\\s*:\\s*(\\d+)\\s*,\\s*\"y\"\\s*:\\s*(\\d+)\\s*,?\\s*\"width\"\\s*:\\s*(\\d+)\\s*,\\s*\"height\"\\s*:\\s*(\\d+)",
            RegexOptions.Singleline);

        var entries = new List<PortraitMappingEntry>(matches.Count);
        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            entries.Add(new PortraitMappingEntry
            {
                CharId = int.Parse(match.Groups[1].Value),
                X = float.Parse(match.Groups[2].Value),
                Y = float.Parse(match.Groups[3].Value),
                Width = float.Parse(match.Groups[4].Value),
                Height = float.Parse(match.Groups[5].Value)
            });
        }

        return entries;
    }

    private static Texture2D BuildPortraitAtlasTexture(Texture2D portraitSheetTexture, PortraitMappingEntry entry)
    {
        return new AtlasTexture
        {
            Atlas = portraitSheetTexture,
            Region = new Rect2(entry.X, entry.Y, entry.Width, entry.Height)
        };
    }

    private Texture2D? BuildOfficerPortraitTexture(int officerId)
    {
        return _officerPortraitTextures.TryGetValue(officerId, out var portraitTexture)
            ? portraitTexture
            : null;
    }

}
