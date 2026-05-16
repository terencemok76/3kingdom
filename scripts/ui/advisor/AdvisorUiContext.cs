using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class AdvisorUiContext
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

    public Window CreateWindow(string scenePath, Action<Window> closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Window>();
        dialog.Exclusive = false;
        dialog.Unresizable = true;
        dialog.CloseRequested += () =>
        {
            _owner.AdvisorPlayUiClickSfx();
            closeAction(dialog);
        };
        _owner.AddChild(dialog);
        return dialog;
    }

    public void PopupDialog(Window? dialog) => _owner.AdvisorPopupDialog(dialog);

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
