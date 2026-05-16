using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    internal TurnManager? InternalAffairsTurnManager => _turnManager;
    internal CommandResolver? InternalAffairsCommandResolver => _commandResolver;
    internal LocalizationService? InternalAffairsLocalization => _localization;
    internal CityData? InternalAffairsSelectedCity => _selectedCity;

    internal void InternalAffairsPopupDialog(Window? dialog) => PopupDialogUsingSceneSize(dialog);

    internal void InternalAffairsPlayUiClickSfx() => PlayUiClickSfx();

    internal void InternalAffairsAddLog(string message, bool isPlayerRelated = false) => AddLog(message, isPlayerRelated);

    internal void InternalAffairsRefreshSelectedCity() => RefreshSelectedCity();

    internal void InternalAffairsRefreshMapVisuals() => _mapController?.RefreshVisuals();

    internal void InternalAffairsShowOfficerSelectorDialog(
        string title,
        List<int> candidateOfficerIds,
        OfficerSelectorPrimaryStat primaryStat,
        System.Action<int> confirmedAction) =>
        ShowOfficerSelectorDialog(title, candidateOfficerIds, primaryStat, confirmedAction);

    internal List<int> InternalAffairsGetAvailableOfficerIdsForOrder() => GetAvailableOfficerIdsForOrder().ToList();

    internal string InternalAffairsGetLocalizedResultMessage(CommandResult result) => GetLocalizedResultMessage(result);
}
