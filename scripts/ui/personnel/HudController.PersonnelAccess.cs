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

    internal void PersonnelPopupDialog(Window? dialog) => PopupDialogUsingSceneSize(dialog);

    internal void PersonnelPlayUiClickSfx() => PlayUiClickSfx();

    internal void PersonnelAddLog(string message, bool isPlayerRelated = false) => AddLog(message, isPlayerRelated);

    internal void PersonnelRefreshSelectedCity() => RefreshSelectedCity();

    internal void PersonnelRefreshMapVisuals() => _mapController?.RefreshVisuals();

    internal void PersonnelConfigureMoveSpinBox(SpinBox? spinBox, int maxValue, int value) => ConfigureMoveSpinBox(spinBox, maxValue, value);

    internal void PersonnelShowOfficerSelectorDialog(
        string title,
        List<int> candidateOfficerIds,
        OfficerSelectorPrimaryStat primaryStat,
        Action<int> confirmedAction,
        IEnumerable<OfficerSelectorScopeOption>? scopeOptions = null,
        string? initialScopeKey = null) =>
        ShowOfficerSelectorDialog(title, candidateOfficerIds, primaryStat, confirmedAction, scopeOptions, initialScopeKey);

    internal List<int> PersonnelGetAvailableOfficerIdsForOrder() => GetAvailableOfficerIdsForOrder().ToList();

    internal void PersonnelContinuePendingNonAttackResolution() => ContinuePendingNonAttackResolution();

    internal string PersonnelGetLocalizedResultMessage(CommandResult result) => GetLocalizedResultMessage(result);

    internal bool PersonnelIsFactionRuler(WorldState world, OfficerData officer) => IsFactionRuler(world, officer);

    internal bool PersonnelIsOfficerOldEnoughToJoin(WorldState world, OfficerData officer) => IsOfficerOldEnoughToJoin(world, officer);
}
