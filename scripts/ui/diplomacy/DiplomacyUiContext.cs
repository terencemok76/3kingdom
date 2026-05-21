using System;
using System.Collections.Generic;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class DiplomacyUiContext : IFloatingOverlayContext
{
    private readonly HudController _owner;

    public DiplomacyUiContext(HudController owner)
    {
        _owner = owner;
    }

    public TurnManager? TurnManager => _owner.DiplomacyTurnManager;
    public CommandResolver? CommandResolver => _owner.DiplomacyCommandResolver;
    public LocalizationService? Localization => _owner.DiplomacyLocalization;
    public CityData? SelectedCity => _owner.DiplomacySelectedCity;

    public Control CreateOverlay(string scenePath, Action closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Control>();
        dialog.Visible = false;
        Node parent = _owner;
        if (_owner.DiplomacyOverlayParent != null)
        {
            parent = _owner.DiplomacyOverlayParent;
        }

        parent.AddChild(dialog);
        return dialog;
    }

    public Window CreateCodeWindow(Action closeAction)
    {
        var dialog = new Window
        {
            Exclusive = false,
            Unresizable = true
        };
        dialog.CloseRequested += () =>
        {
            _owner.DiplomacyPlayUiClickSfx();
            closeAction();
        };
        _owner.AddChild(dialog);
        return dialog;
    }

    public void PopupDialog(Control? dialog) => _owner.DiplomacyPopupDialog(dialog);
    public void CloseOverlay(Action closeAction)
    {
        _owner.DiplomacyPlayUiClickSfx();
        closeAction();
    }
    public void BringOverlayToFront(CanvasItem? item) => _owner.DiplomacyBringOverlayToFront(item);
    public void AddLog(string message, bool isPlayerRelated = false) => _owner.DiplomacyAddLog(message, isPlayerRelated);
    public void RefreshSelectedCity() => _owner.DiplomacyRefreshSelectedCity();
    public void ShowOfficerSelectorDialog(string title, List<int> ids, HudController.OfficerSelectorPrimaryStat stat, Action<int> confirmedAction) => _owner.DiplomacyShowOfficerSelectorDialog(title, ids, stat, confirmedAction);
    public void CheckFactionEliminations() => _owner.DiplomacyCheckFactionEliminations();
    public void ContinuePendingNonAttackResolution() => _owner.DiplomacyContinuePendingNonAttackResolution();
    public string GetLocalizedResultMessage(CommandResult result) => _owner.DiplomacyGetLocalizedResultMessage(result);
    public string GetStatusText(DiplomacyStatusType status) => _owner.DiplomacyGetStatusText(status);
    public void ApplyCommandButtonTheme(Button button) => _owner.DiplomacyApplyCommandButtonTheme(button);
}
