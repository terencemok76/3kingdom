using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class SpyUiContext
{
    private readonly HudController _owner;

    public SpyUiContext(HudController owner)
    {
        _owner = owner;
    }

    public TurnManager? TurnManager => _owner.SpyTurnManager;
    public CommandResolver? CommandResolver => _owner.SpyCommandResolver;
    public LocalizationService? Localization => _owner.SpyLocalization;
    public CityData? SelectedCity => _owner.SpySelectedCity;

    public Window CreateWindow(string scenePath, Action<Window> closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Window>();
        dialog.Exclusive = false;
        dialog.Unresizable = true;
        dialog.CloseRequested += () =>
        {
            _owner.SpyPlayUiClickSfx();
            closeAction(dialog);
        };
        _owner.AddChild(dialog);
        return dialog;
    }

    public void PopupDialog(Window? dialog) => _owner.SpyPopupDialog(dialog);
    public void AddLog(string message, bool isPlayerRelated = false) => _owner.SpyAddLog(message, isPlayerRelated);
    public void RefreshSelectedCity() => _owner.SpyRefreshSelectedCity();
    public void ShowOfficerSelectorDialog(string title, List<int> ids, HudController.OfficerSelectorPrimaryStat stat, Action<int> confirmedAction) => _owner.SpyShowOfficerSelectorDialog(title, ids, stat, confirmedAction);
    public bool HasActiveInternalAffairsSchedule(int officerId) => _owner.SpyHasActiveInternalAffairsSchedule(officerId);
    public string GetLocalizedResultMessage(CommandResult result) => _owner.SpyGetLocalizedResultMessage(result);

    public CommandResult ExecuteSpyCommand(int targetCityId, int selectedOfficerId, SpyActionType actionType, int targetOfficerId)
    {
        var city = SelectedCity;
        var turnManager = TurnManager;
        var commandResolver = CommandResolver;
        if (city == null || turnManager == null || commandResolver == null)
        {
            return new CommandResult { Success = false, Message = "ui.command_unavailable" };
        }

        return commandResolver.Execute(new CommandRequest
        {
            Type = CommandType.Spy,
            ActorFactionId = turnManager.GetPlayerFactionId(),
            SourceCityId = city.Id,
            TargetCityId = targetCityId,
            TargetOfficerId = actionType == SpyActionType.Assassination ? targetOfficerId : null,
            OfficerIds = new List<int> { selectedOfficerId },
            SpyActionType = actionType
        });
    }

    public List<int> GetAvailableSpyOfficerIds()
    {
        var city = SelectedCity;
        var world = TurnManager?.World;
        if (city == null || world == null)
        {
            return new List<int>();
        }

        return city.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer =>
                officer != null &&
                !(officer.LastAssignedYear == world.Year &&
                  officer.LastAssignedMonth == world.Month) &&
                !HasActiveInternalAffairsSchedule(officer.Id))
            .Select(officer => officer!.Id)
            .ToList();
    }
}
