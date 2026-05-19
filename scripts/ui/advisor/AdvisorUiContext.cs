using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class AdvisorUiContext : IFloatingOverlayContext
{
    private readonly HudController _owner;

    public AdvisorUiContext(HudController owner)
    {
        _owner = owner;
    }

    public TurnManager? TurnManager => _owner.AdvisorTurnManager;
    public CommandResolver? CommandResolver => _owner.AdvisorCommandResolver;
    public LocalizationService? Localization => _owner.AdvisorLocalization;
    public CityData? SelectedCity => _owner.AdvisorSelectedCity;

    public Control CreateOverlay(string scenePath, Action closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Control>();
        dialog.Visible = false;
        Node parent = _owner;
        if (_owner.AdvisorOverlayParent != null)
        {
            parent = _owner.AdvisorOverlayParent;
        }

        parent.AddChild(dialog);
        return dialog;
    }

    public void PopupDialog(Control? dialog) => _owner.AdvisorPopupDialog(dialog);

    public void CloseOverlay(Action closeAction)
    {
        _owner.AdvisorPlayUiClickSfx();
        closeAction();
    }

    public void BringOverlayToFront(CanvasItem? item) => _owner.AdvisorBringOverlayToFront(item);

    public void AddLog(string message, bool isPlayerRelated = false) => _owner.AdvisorAddLog(message, isPlayerRelated);

    public void RefreshSelectedCity() => _owner.AdvisorRefreshSelectedCity();

    public void ShowOfficerSelectorDialog(
        string title,
        List<int> candidateOfficerIds,
        HudController.OfficerSelectorPrimaryStat primaryStat,
        Action<int> confirmedAction) =>
        _owner.AdvisorShowOfficerSelectorDialog(title, candidateOfficerIds, primaryStat, confirmedAction);

    public bool IsFactionRuler(WorldState world, OfficerData officer) => _owner.AdvisorIsFactionRuler(world, officer);

    public string GetLocalizedResultMessage(CommandResult result) => _owner.AdvisorGetLocalizedResultMessage(result);

    public Texture2D? BuildOfficerPortraitTexture(int officerId) => _owner.AdvisorBuildOfficerPortraitTexture(officerId);

    public string GetPortraitLabel() => _owner.AdvisorGetPortraitLabel();

    public void ApplyCommandButtonTheme(Button button) => _owner.AdvisorApplyCommandButtonTheme(button);

    public List<int> GetNonRulerCityOfficerIds()
    {
        var city = SelectedCity;
        var world = TurnManager?.World;
        if (city == null || world == null)
        {
            return new List<int>();
        }

        return city.OfficerIds
            .Where(officerId =>
            {
                var officer = world.GetOfficer(officerId);
                return officer != null && !IsFactionRuler(world, officer);
            })
            .ToList();
    }
}
