using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class MilitaryUiContext : IFloatingOverlayContext
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

    public Control CreateOverlay(string scenePath, Action closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Control>();
        dialog.Visible = false;
        Node parent = _owner;
        if (_owner.MilitaryOverlayParent != null)
        {
            parent = _owner.MilitaryOverlayParent;
        }

        parent.AddChild(dialog);
        return dialog;
    }

    public void PopupDialog(Window? dialog) => _owner.MilitaryPopupDialog(dialog);
    public void PopupDialog(Control? dialog) => _owner.MilitaryPopupDialog(dialog);
    public void CloseOverlay(Action closeAction)
    {
        _owner.MilitaryPlayUiClickSfx();
        closeAction();
    }
    public void BringOverlayToFront(CanvasItem? item) => _owner.MilitaryBringOverlayToFront(item);

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
    public void ConfigureCompactOfficerTableColumns(Tree tree, bool includeCheck) => _owner.MilitaryConfigureCompactOfficerTableColumns(tree, includeCheck);
    public void PopulateCompactOfficerTableRow(TreeItem row, OfficerData officer, int rowIndex, bool includeCheck) => _owner.MilitaryPopulateCompactOfficerTableRow(row, officer, rowIndex, includeCheck);
    public List<int> GetCheckedTreeMetadataIds(Tree? tree) => _owner.MilitaryGetCheckedTreeMetadataIds(tree);
    public CommandResult ExecuteMoveCommand(int targetCityId, int troops, int gold, int food, int horses, List<int> officerIds) => _owner.MilitaryExecuteMoveCommand(targetCityId, troops, gold, food, horses, officerIds);
    public CommandResult ExecuteAttackCommand(int targetCityId, int troops, int gold, int food, List<AttackOfficerDeploymentData> deployments, List<int> officerIds) => _owner.MilitaryExecuteAttackCommand(targetCityId, troops, gold, food, deployments, officerIds);
    public string GetLocalizedResultMessage(CommandResult result) => _owner.MilitaryGetLocalizedResultMessage(result);
    public void ContinuePendingAttackResolution() => _owner.MilitaryContinuePendingAttackResolution();
    public void ApplyCommandButtonTheme(Button button) => _owner.MilitaryApplyCommandButtonTheme(button);

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
