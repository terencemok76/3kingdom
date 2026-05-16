using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class MilitaryUiContext
{
    private readonly HudController _owner;

    public MilitaryUiContext(HudController owner)
    {
        _owner = owner;
    }

    public TurnManager? TurnManager => _owner.MilitaryTurnManager;
    public CommandResolver? CommandResolver => _owner.MilitaryCommandResolver;
    public LocalizationService? Localization => _owner.MilitaryLocalization;
    public CityData? SelectedCity => _owner.MilitarySelectedCity;

    public Window CreateWindow(string scenePath, Action<Window> closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Window>();
        dialog.Exclusive = false;
        dialog.Unresizable = true;
        dialog.CloseRequested += () =>
        {
            _owner.MilitaryPlayUiClickSfx();
            closeAction(dialog);
        };
        _owner.AddChild(dialog);
        return dialog;
    }

    public void PopupDialog(Window? dialog) => _owner.MilitaryPopupDialog(dialog);

    public void ReopenDialog(Window? dialog)
    {
        Callable.From(() => _owner.MilitaryPopupDialog(dialog)).CallDeferred();
    }

    public void AddLog(string message, bool isPlayerRelated = false) => _owner.MilitaryAddLog(message, isPlayerRelated);
    public void ShowOfficerSelectorDialog(string title, List<int> ids, HudController.OfficerSelectorPrimaryStat stat, Action<int> confirmedAction) => _owner.MilitaryShowOfficerSelectorDialog(title, ids, stat, confirmedAction);
    public List<int> GetAvailableOfficerIdsForOrder() => _owner.MilitaryGetAvailableOfficerIdsForOrder();
    public CommandResult ExecutePlayerCommand(CommandType commandType, int? targetCityId = null, int troopsToSend = 0, List<int>? officerIds = null, TroopType recruitTroopType = TroopType.Infantry) => _owner.MilitaryExecutePlayerCommand(commandType, targetCityId, troopsToSend, officerIds, recruitTroopType);
    public string GetCommandName(CommandType commandType) => _owner.MilitaryGetCommandName(commandType);
    public string GetTroopTypeDisplayName(TroopType troopType) => _owner.MilitaryGetTroopTypeDisplayName(troopType);
    public void OpenAttackFlow() => _owner.MilitaryOpenAttackFlow();
    public void OpenMoveFlow() => _owner.MilitaryOpenMoveFlow();

    public List<int> GetAvailableCityOfficerIds()
    {
        var city = SelectedCity;
        if (city == null)
        {
            return new List<int>();
        }

        var availableOfficerIds = GetAvailableOfficerIdsForOrder();
        return city.OfficerIds.Where(availableOfficerIds.Contains).ToList();
    }
}
