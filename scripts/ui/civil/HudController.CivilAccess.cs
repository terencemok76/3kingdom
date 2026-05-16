using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    internal TurnManager? CivilTurnManager => _turnManager;
    internal CommandResolver? CivilCommandResolver => _commandResolver;
    internal LocalizationService? CivilLocalization => _localization;
    internal CityData? CivilSelectedCity => _selectedCity;

    internal void CivilPopupDialog(Window? dialog) => PopupDialogUsingSceneSize(dialog);

    internal void CivilPlayUiClickSfx() => PlayUiClickSfx();

    internal void CivilAddLog(string message, bool isPlayerRelated = false) => AddLog(message, isPlayerRelated);

    internal void CivilRefreshSelectedCity() => RefreshSelectedCity();

    internal void CivilRefreshMapVisuals() => _mapController?.RefreshVisuals();

    internal void CivilConfigureMoveSpinBox(SpinBox? spinBox, int maxValue, int value) => ConfigureMoveSpinBox(spinBox, maxValue, value);

    internal void CivilShowOfficerSelectorDialog(
        string title,
        List<int> candidateOfficerIds,
        OfficerSelectorPrimaryStat primaryStat,
        System.Action<int> confirmedAction) =>
        ShowOfficerSelectorDialog(title, candidateOfficerIds, primaryStat, confirmedAction);

    internal List<int> CivilGetAvailableOfficerIdsForOrder() => GetAvailableOfficerIdsForOrder().ToList();

    internal CommandResult CivilExecutePlayerCommand(
        CommandType commandType,
        int? targetCityId = null,
        int troopsToSend = 0,
        List<int>? officerIds = null) =>
        ExecutePlayerCommand(commandType, targetCityId: targetCityId, troopsToSend: troopsToSend, officerIds: officerIds);

    internal string CivilGetCommandName(CommandType commandType) => GetCommandName(commandType);

    internal string CivilGetLocalizedResultMessage(CommandResult result) => GetLocalizedResultMessage(result);
}
