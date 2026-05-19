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
    internal Control? AdvisorOverlayParent => GetNodeOrNull<Control>("Root");

    internal void AdvisorPopupDialog(Control? dialog) => ShowOverlay(dialog);
    internal void AdvisorBringOverlayToFront(CanvasItem? item) => item?.MoveToFront();

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

    internal Texture2D? AdvisorBuildOfficerPortraitTexture(int officerId) => BuildOfficerPortraitTexture(officerId);

    internal string AdvisorGetPortraitLabel() => _localization?.T("ui.portrait") ?? "Portrait";

    internal void AdvisorApplyCommandButtonTheme(Button button)
    {
        if (MainHudViewButton != null)
        {
            CopyButtonTheme(MainHudViewButton, button);
        }
    }
}
