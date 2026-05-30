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
    internal Control? MilitaryOverlayParent => GetNodeOrNull<Control>("Root");

    internal void MilitaryPopupDialog(Window? dialog) => PopupDialogUsingSceneSize(dialog);
    internal void MilitaryPopupDialog(Control? dialog) => ShowOverlay(dialog);
    internal void MilitaryBringOverlayToFront(CanvasItem? item) => item?.MoveToFront();

    internal void MilitaryPlayUiClickSfx() => PlayUiClickSfx();

    internal void MilitaryAddLog(string message, bool isPlayerRelated = false) => AddLog(message, isPlayerRelated);

    internal void MilitaryShowOfficerSelectorDialog(
        string title,
        List<int> candidateOfficerIds,
        OfficerSelectorPrimaryStat primaryStat,
        System.Action<int> confirmedAction,
        System.Func<string>? titleFactory = null) =>
        ShowOfficerSelectorDialog(title, candidateOfficerIds, primaryStat, confirmedAction, titleFactory: titleFactory);

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
    internal void MilitaryConfigureCompactOfficerTableColumns(Tree tree, bool includeCheck) =>
        ConfigureCompactOfficerTableColumns(tree, includeCheck: includeCheck);

    internal void MilitaryPopulateCompactOfficerTableRow(TreeItem row, OfficerData officer, int rowIndex, bool includeCheck) =>
        PopulateCompactOfficerTableRow(row, officer, rowIndex, includeCheck: includeCheck);
    internal List<int> MilitaryGetCheckedTreeMetadataIds(Tree? tree) => GetCheckedTreeMetadataIds(tree).ToList();
    internal CommandResult MilitaryExecuteMoveCommand(int targetCityId, int troops, int gold, int food, int horses, SiegeEngineAllocationData siegeEngineAllocation, List<int> officerIds) =>
        ExecutePlayerCommand(CommandType.Move, targetCityId: targetCityId, troopsToSend: troops, goldToSend: gold, foodToSend: food, horsesToSend: horses, siegeEngineAllocation: siegeEngineAllocation, officerIds: officerIds);
    internal CommandResult MilitaryExecuteAttackCommand(int targetCityId, int troops, int gold, int food, List<AttackOfficerDeploymentData> deployments, List<int> officerIds) =>
        ExecutePlayerCommand(CommandType.Attack, targetCityId: targetCityId, troopsToSend: troops, goldToSend: gold, foodToSend: food, attackOfficerDeployments: deployments, officerIds: officerIds);
    internal string MilitaryGetLocalizedResultMessage(CommandResult result) => GetLocalizedResultMessage(result);
    internal void MilitaryContinuePendingAttackResolution() => ContinuePendingAttackResolution();

    internal void MilitaryApplyCommandButtonTheme(Button button)
    {
        if (MainHudViewButton != null)
        {
            CopyButtonTheme(MainHudViewButton, button);
        }
    }
}
