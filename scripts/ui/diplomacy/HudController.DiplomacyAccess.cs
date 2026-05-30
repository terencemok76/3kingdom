using System.Collections.Generic;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    internal TurnManager? DiplomacyTurnManager => _turnManager;
    internal CommandResolver? DiplomacyCommandResolver => _commandResolver;
    internal LocalizationService? DiplomacyLocalization => _localization;
    internal CityData? DiplomacySelectedCity => _selectedCity;
    internal Control? DiplomacyOverlayParent => GetNodeOrNull<Control>("Root");

    internal void DiplomacyPopupDialog(Control? dialog) => ShowOverlay(dialog);
    internal void DiplomacyBringOverlayToFront(CanvasItem? item) => item?.MoveToFront();

    internal void DiplomacyPlayUiClickSfx() => PlayUiClickSfx();

    internal void DiplomacyAddLog(string message, bool isPlayerRelated = false) => AddLog(message, isPlayerRelated);

    internal void DiplomacyRefreshSelectedCity() => RefreshSelectedCity();

    internal void DiplomacyShowOfficerSelectorDialog(
        string title,
        List<int> candidateOfficerIds,
        OfficerSelectorPrimaryStat primaryStat,
        System.Action<int> confirmedAction,
        System.Func<string>? titleFactory = null) =>
        ShowOfficerSelectorDialog(title, candidateOfficerIds, primaryStat, confirmedAction, titleFactory: titleFactory);

    internal void DiplomacyCheckFactionEliminations() => CheckFactionEliminations();

    internal void DiplomacyContinuePendingNonAttackResolution() => ContinuePendingNonAttackResolution();

    internal string DiplomacyGetLocalizedResultMessage(CommandResult result) => GetLocalizedResultMessage(result);

    internal string DiplomacyGetStatusText(DiplomacyStatusType status) => GetDiplomacyStatusText(status);

    internal void DiplomacyApplyCommandButtonTheme(Button button)
    {
        if (MainHudViewButton != null)
        {
            CopyButtonTheme(MainHudViewButton, button);
        }
    }
}
