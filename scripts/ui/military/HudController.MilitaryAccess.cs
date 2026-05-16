using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    internal TurnManager? MilitaryTurnManager => _turnManager;
    internal CommandResolver? MilitaryCommandResolver => _commandResolver;
    internal LocalizationService? MilitaryLocalization => _localization;
    internal CityData? MilitarySelectedCity => _selectedCity;

    internal void MilitaryPopupDialog(Window? dialog) => PopupDialogUsingSceneSize(dialog);

    internal void MilitaryPlayUiClickSfx() => PlayUiClickSfx();

    internal void MilitaryAddLog(string message, bool isPlayerRelated = false) => AddLog(message, isPlayerRelated);

    internal void MilitaryShowOfficerSelectorDialog(
        string title,
        List<int> candidateOfficerIds,
        OfficerSelectorPrimaryStat primaryStat,
        System.Action<int> confirmedAction) =>
        ShowOfficerSelectorDialog(title, candidateOfficerIds, primaryStat, confirmedAction);

    internal List<int> MilitaryGetAvailableOfficerIdsForOrder() => GetAvailableOfficerIdsForOrder().ToList();

    internal CommandResult MilitaryExecutePlayerCommand(
        CommandType commandType,
        int? targetCityId = null,
        int troopsToSend = 0,
        List<int>? officerIds = null,
        TroopType recruitTroopType = TroopType.Infantry) =>
        ExecutePlayerCommand(commandType, targetCityId: targetCityId, troopsToSend: troopsToSend, officerIds: officerIds, recruitTroopType: recruitTroopType);

    internal string MilitaryGetCommandName(CommandType commandType) => GetCommandName(commandType);

    internal string MilitaryGetTroopTypeDisplayName(TroopType troopType) => GetTroopTypeDisplayName(troopType);

    internal void MilitaryOpenAttackFlow() => OpenAttackFlow();

    internal void MilitaryOpenMoveFlow() => OpenMoveFlow();
}
