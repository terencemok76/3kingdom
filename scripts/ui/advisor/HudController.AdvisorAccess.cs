using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    internal TurnManager? AdvisorTurnManager => _turnManager;
    internal CommandResolver? AdvisorCommandResolver => _commandResolver;
    internal LocalizationService? AdvisorLocalization => _localization;
    internal CityData? AdvisorSelectedCity => _selectedCity;

    internal void AdvisorPopupDialog(Window? dialog) => PopupDialogUsingSceneSize(dialog);

    internal void AdvisorPlayUiClickSfx() => PlayUiClickSfx();

    internal void AdvisorAddLog(string message, bool isPlayerRelated = false) => AddLog(message, isPlayerRelated);

    internal void AdvisorRefreshSelectedCity() => RefreshSelectedCity();

    internal void AdvisorShowOfficerSelectorDialog(
        string title,
        List<int> candidateOfficerIds,
        OfficerSelectorPrimaryStat primaryStat,
        System.Action<int> confirmedAction) =>
        ShowOfficerSelectorDialog(title, candidateOfficerIds, primaryStat, confirmedAction);

    internal bool AdvisorIsFactionRuler(WorldState world, OfficerData officer) => IsFactionRuler(world, officer);

    internal string AdvisorGetLocalizedResultMessage(CommandResult result) => GetLocalizedResultMessage(result);
}
