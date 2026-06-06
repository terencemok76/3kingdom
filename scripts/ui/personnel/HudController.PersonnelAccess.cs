using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    internal TurnManager? PersonnelTurnManager => _turnManager;
    internal CommandResolver? PersonnelCommandResolver => _commandResolver;
    internal LocalizationService? PersonnelLocalization => _localization;
    internal CityData? PersonnelSelectedCity => _selectedCity;
    internal Control? PersonnelOverlayParent => GetNodeOrNull<Control>("Root");

    internal void PersonnelPopupDialog(Window? dialog) => PopupDialogUsingSceneSize(dialog);
    internal void PersonnelPopupDialog(Control? dialog) => ShowOverlay(dialog);
    internal void PersonnelBringOverlayToFront(CanvasItem? item) => item?.MoveToFront();

    internal void PersonnelPlayUiClickSfx() => PlayUiClickSfx();

    internal void PersonnelAddLog(string message, bool isPlayerRelated = false) => AddLog(message, isPlayerRelated);

    internal void PersonnelRefreshSelectedCity() => RefreshSelectedCity();
    internal void PersonnelRefreshMapVisuals() => _mapController?.RefreshVisuals();
    internal UiEventHub PersonnelUiEventHub => _uiEventHub;

    internal void PersonnelConfigureMoveSpinBox(SpinBox? spinBox, int maxValue, int value) => ConfigureMoveSpinBox(spinBox, maxValue, value);

    internal void PersonnelShowOfficerSelectorDialog(
        string title,
        List<int> candidateOfficerIds,
        OfficerSelectorPrimaryStat primaryStat,
        Action<int> confirmedAction,
        IEnumerable<OfficerSelectorScopeOption>? scopeOptions = null,
        string? initialScopeKey = null,
        Func<string>? titleFactory = null,
        Func<IEnumerable<OfficerSelectorScopeOption>?>? scopeOptionsFactory = null,
        OfficerSelectorDisplayConfig? displayConfig = null,
        Func<OfficerSelectorDisplayConfig?>? displayConfigFactory = null) =>
        ShowOfficerSelectorDialog(title, candidateOfficerIds, primaryStat, confirmedAction, scopeOptions, initialScopeKey, displayConfig, titleFactory, scopeOptionsFactory, displayConfigFactory);

    internal void PersonnelShowAssignRoleOfficerSelectorDialog(
        string title,
        List<int> candidateOfficerIds,
        Action<int> confirmedAction,
        IEnumerable<OfficerSelectorScopeOption>? scopeOptions = null,
        string? initialScopeKey = null,
        Func<string>? titleFactory = null,
        Func<IEnumerable<OfficerSelectorScopeOption>?>? scopeOptionsFactory = null,
        Func<OfficerSelectorDisplayConfig?>? displayConfigFactory = null) =>
        ShowOfficerSelectorDialog(
            title,
            candidateOfficerIds,
            OfficerSelectorPrimaryStat.Politics,
            confirmedAction,
            scopeOptions,
            initialScopeKey,
            displayConfigFactory?.Invoke() ?? BuildAssignRoleOfficerSelectorDisplayConfig(),
            titleFactory,
            scopeOptionsFactory,
            displayConfigFactory);

    internal List<int> PersonnelGetAvailableOfficerIdsForOrder() => GetAvailableOfficerIdsForOrder().ToList();

    internal void PersonnelContinuePendingNonAttackResolution() => ContinuePendingNonAttackResolution();

    internal string PersonnelGetLocalizedResultMessage(CommandResult result) => GetLocalizedResultMessage(result);

    internal bool PersonnelIsFactionRuler(WorldState world, OfficerData officer) => IsFactionRuler(world, officer);

    internal bool PersonnelIsOfficerOldEnoughToJoin(WorldState world, OfficerData officer) => IsOfficerOldEnoughToJoin(world, officer);
    internal Texture2D? PersonnelBuildOfficerPortraitTexture(int officerId) => BuildOfficerPortraitTexture(officerId);
    internal string PersonnelBuildOfficerDetailText(OfficerData officer) => BuildOfficerDetailText(officer);
    internal string PersonnelGetPortraitLabel() => _localization?.T("ui.portrait") ?? "Portrait";

    internal void PersonnelApplyCommandButtonTheme(Button button)
    {
        if (MainHudViewButton != null)
        {
            CopyButtonTheme(MainHudViewButton, button);
        }
    }

    internal OfficerSelectorDisplayConfig PersonnelBuildAssignRoleOfficerSelectorDisplayConfig()
        => BuildAssignRoleOfficerSelectorDisplayConfig();

    internal OfficerSelectorDisplayConfig PersonnelBuildPrisonerOfficerSelectorDisplayConfig()
        => BuildPrisonerOfficerSelectorDisplayConfig();

    private OfficerSelectorDisplayConfig BuildAssignRoleOfficerSelectorDisplayConfig()
    {
        return new OfficerSelectorDisplayConfig
        {
            Columns =
            [
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.officers") ?? "Officer", MinWidth = 140 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.role") ?? "Role", MinWidth = 90 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.appointed_titles") ?? "Appointments", MinWidth = 180 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.city") ?? "City", MinWidth = 100 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.status") ?? "Status", MinWidth = 90 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.age") ?? "Age", MinWidth = 60 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.loyalty") ?? "Loyalty", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.strength") ?? "Strength", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.intelligence") ?? "Intelligence", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.charm") ?? "Charm", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.leadership") ?? "Leadership", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.politics") ?? "Politics", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.combat") ?? "Combat", MinWidth = 70 }
            ],
            BuildRowTexts = BuildAssignRoleOfficerSelectorRowTexts,
            PanelSize = new Vector2(1460.0f, 360.0f)
        };
    }

    private IReadOnlyList<string> BuildAssignRoleOfficerSelectorRowTexts(OfficerData officer)
    {
        var age = CalculateOfficerAge(officer, _turnManager?.World?.Year ?? 0);
        var cityName = _turnManager?.World?.GetCity(officer.CityId) is { } city && _localization != null
            ? _localization.GetCityName(city)
            : "-";
        var statusName = _turnManager?.World != null && _localization != null
            ? _localization.GetOfficerStatus(_turnManager.World, officer)
            : string.Empty;
        var appointedTitles = GetOfficerAppointmentSummary(officer);

        return
        [
            _localization?.GetOfficerName(officer) ?? officer.Name,
            _localization?.GetOfficerRole(officer) ?? officer.Role,
            appointedTitles,
            cityName,
            statusName,
            age.ToString(),
            officer.Loyalty.ToString(),
            officer.Strength.ToString(),
            officer.Intelligence.ToString(),
            officer.Charm.ToString(),
            officer.Leadership.ToString(),
            officer.Politics.ToString(),
            officer.Combat.ToString()
        ];
    }

    private OfficerSelectorDisplayConfig BuildPrisonerOfficerSelectorDisplayConfig()
    {
        return new OfficerSelectorDisplayConfig
        {
            Columns =
            [
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.officers") ?? "Officer", MinWidth = 150 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.role") ?? "Role", MinWidth = 90 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.status") ?? "Status", MinWidth = 90 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.age") ?? "Age", MinWidth = 60 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.loyalty") ?? "Loyalty", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.ambition") ?? "Ambition", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.strength") ?? "Strength", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.intelligence") ?? "Intelligence", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.charm") ?? "Charm", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.leadership") ?? "Leadership", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.politics") ?? "Politics", MinWidth = 70 },
                new OfficerSelectorColumnDefinition { Title = _localization?.T("ui.combat") ?? "Combat", MinWidth = 70 }
            ],
            BuildRowTexts = BuildPrisonerOfficerSelectorRowTexts,
            PanelSize = new Vector2(1180.0f, 360.0f)
        };
    }

    private IReadOnlyList<string> BuildPrisonerOfficerSelectorRowTexts(OfficerData officer)
    {
        var age = CalculateOfficerAge(officer, _turnManager?.World?.Year ?? 0);
        var statusName = _turnManager?.World != null && _localization != null
            ? BuildMaskedOfficerStatus(_turnManager.World, officer)
            : string.Empty;
        var loyaltyText = _turnManager?.World != null
            ? BuildOfficerLoyaltyTableText(_turnManager.World, officer)
            : officer.Loyalty.ToString();

        return
        [
            _localization?.GetOfficerName(officer) ?? officer.Name,
            GetDisplayedOfficerRole(officer),
            statusName,
            age.ToString(),
            loyaltyText,
            officer.Ambition.ToString(),
            officer.Strength.ToString(),
            officer.Intelligence.ToString(),
            officer.Charm.ToString(),
            officer.Leadership.ToString(),
            officer.Politics.ToString(),
            officer.Combat.ToString()
        ];
    }

    private string GetOfficerAppointmentSummary(OfficerData officer)
    {
        var world = _turnManager?.World;
        var localization = _localization;
        if (world == null || localization == null)
        {
            return string.Empty;
        }

        var titles = officer.Appointments
            .Where(static appointment => !string.IsNullOrWhiteSpace(appointment))
            .Select(localization.GetAppointmentName)
            .ToList();

        var faction = world.Factions.FirstOrDefault(item => item.OfficerIds.Contains(officer.Id));
        if (faction != null)
        {
            if (faction.ChancellorOfficerId == officer.Id)
            {
                titles.Add(localization.GetAppointmentName(OfficerAppointmentRules.Chancellor));
            }

            if (faction.ChiefStrategistOfficerId == officer.Id)
            {
                titles.Add(localization.GetAppointmentName(OfficerAppointmentRules.ChiefStrategist));
            }
        }

        var distinctTitles = titles
            .Where(static title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return distinctTitles.Count > 0 ? string.Join(" / ", distinctTitles) : localization.T("ui.none");
    }
}
