using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class CivilUiContext
{
    private readonly HudController _owner;

    public CivilUiContext(HudController owner)
    {
        _owner = owner;
    }

    public TurnManager? TurnManager => _owner.CivilTurnManager;
    public CommandResolver? CommandResolver => _owner.CivilCommandResolver;
    public LocalizationService? Localization => _owner.CivilLocalization;
    public CityData? SelectedCity => _owner.CivilSelectedCity;

    public Window CreateWindow(string scenePath, Action<Window> closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Window>();
        dialog.Exclusive = false;
        dialog.Unresizable = true;
        dialog.CloseRequested += () =>
        {
            _owner.CivilPlayUiClickSfx();
            closeAction(dialog);
        };
        _owner.AddChild(dialog);
        return dialog;
    }

    public void PopupDialog(Window? dialog) => _owner.CivilPopupDialog(dialog);
    public void AddLog(string message, bool isPlayerRelated = false) => _owner.CivilAddLog(message, isPlayerRelated);
    public void RefreshSelectedCity() => _owner.CivilRefreshSelectedCity();
    public void RefreshMapVisuals() => _owner.CivilRefreshMapVisuals();
    public void ConfigureMoveSpinBox(SpinBox? spinBox, int maxValue, int value) => _owner.CivilConfigureMoveSpinBox(spinBox, maxValue, value);
    public void ShowOfficerSelectorDialog(string title, List<int> ids, HudController.OfficerSelectorPrimaryStat stat, Action<int> confirmedAction) => _owner.CivilShowOfficerSelectorDialog(title, ids, stat, confirmedAction);
    public List<int> GetAvailableOfficerIdsForOrder() => _owner.CivilGetAvailableOfficerIdsForOrder();
    public CommandResult ExecutePlayerCommand(CommandType commandType, int? targetCityId = null, int troopsToSend = 0, List<int>? officerIds = null) => _owner.CivilExecutePlayerCommand(commandType, targetCityId, troopsToSend, officerIds);
    public string GetCommandName(CommandType commandType) => _owner.CivilGetCommandName(commandType);
    public string GetLocalizedResultMessage(CommandResult result) => _owner.CivilGetLocalizedResultMessage(result);

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
