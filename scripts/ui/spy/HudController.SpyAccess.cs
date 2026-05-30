using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    internal TurnManager? SpyTurnManager => _turnManager;
    internal CommandResolver? SpyCommandResolver => _commandResolver;
    internal LocalizationService? SpyLocalization => _localization;
    internal CityData? SpySelectedCity => _selectedCity;
    internal Control? SpyOverlayParent => GetNodeOrNull<Control>("Root");

    internal void SpyPopupDialog(Control? dialog) => ShowOverlay(dialog);
    internal void SpyBringOverlayToFront(CanvasItem? item) => item?.MoveToFront();

    internal void SpyPlayUiClickSfx() => PlayUiClickSfx();

    internal void SpyAddLog(string message, bool isPlayerRelated = false) => AddLog(message, isPlayerRelated);

    internal void SpyRefreshSelectedCity() => RefreshSelectedCity();

    internal void SpyShowOfficerSelectorDialog(
        string title,
        System.Collections.Generic.List<int> candidateOfficerIds,
        OfficerSelectorPrimaryStat primaryStat,
        System.Action<int> confirmedAction,
        System.Func<string>? titleFactory = null) =>
        ShowOfficerSelectorDialog(title, candidateOfficerIds, primaryStat, confirmedAction, titleFactory: titleFactory);

    internal bool SpyHasActiveInternalAffairsSchedule(int officerId) => HasActiveInternalAffairsSchedule(officerId);

    internal string SpyGetLocalizedResultMessage(CommandResult result) => GetLocalizedResultMessage(result);

    internal void SpyApplyCommandButtonTheme(Button button)
    {
        if (MainHudViewButton != null)
        {
            CopyButtonTheme(MainHudViewButton, button);
        }
    }
}
