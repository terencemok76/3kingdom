using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class InternalAffairsUiContext
    : IFloatingOverlayContext
{
    private readonly HudController _owner;

    public InternalAffairsUiContext(HudController owner)
    {
        _owner = owner;
    }

    public TurnManager? TurnManager => _owner.InternalAffairsTurnManager;
    public CommandResolver? CommandResolver => _owner.InternalAffairsCommandResolver;
    public LocalizationService? Localization => _owner.InternalAffairsLocalization;
    public CityData? SelectedCity => _owner.InternalAffairsSelectedCity;

    public Control CreateOverlay(string scenePath, Action closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Control>();
        dialog.Visible = false;
        Node parent = _owner;
        if (_owner.InternalAffairsOverlayParent != null)
        {
            parent = _owner.InternalAffairsOverlayParent;
        }

        parent.AddChild(dialog);
        return dialog;
    }

    public void PopupDialog(Control? dialog) => _owner.InternalAffairsPopupDialog(dialog);

    public void ReopenDialog(Control? dialog)
    {
        Callable.From(() => _owner.InternalAffairsPopupDialog(dialog)).CallDeferred();
    }

    public void CloseOverlay(Action closeAction)
    {
        _owner.InternalAffairsPlayUiClickSfx();
        closeAction();
    }

    public void BringOverlayToFront(CanvasItem? item) => _owner.InternalAffairsBringOverlayToFront(item);

    public void AddLog(string message, bool isPlayerRelated = false) => _owner.InternalAffairsAddLog(message, isPlayerRelated);
    public UiEventHub UiEventHub => _owner.UiEventHub;
    public void RefreshSelectedCity() => _owner.InternalAffairsRefreshSelectedCity();
    public void RefreshMapVisuals() => _owner.InternalAffairsRefreshMapVisuals();
    public void ApplyCommandButtonTheme(Button button) => _owner.InternalAffairsApplyCommandButtonTheme(button);
    public void ShowOfficerSelectorDialog(string title, List<int> ids, HudController.OfficerSelectorPrimaryStat stat, Action<int> confirmedAction) => _owner.InternalAffairsShowOfficerSelectorDialog(title, ids, stat, confirmedAction);
    public List<int> GetAvailableOfficerIdsForOrder() => _owner.InternalAffairsGetAvailableOfficerIdsForOrder();
    public string GetLocalizedResultMessage(CommandResult result) => _owner.InternalAffairsGetLocalizedResultMessage(result);
    public int GetRecommendedInternalAffairsOfficerId(int cityId, InternalAffairsJobType jobType) =>
        CommandResolver?.GetRecommendedInternalAffairsOfficerId(TurnManager?.GetPlayerFactionId() ?? 0, cityId, jobType) ?? 0;

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

    public List<int> GetSelectedItemMetadataIds(ItemList? itemList)
    {
        if (itemList == null)
        {
            return new List<int>();
        }

        var selectedIndices = itemList.GetSelectedItems();
        var results = new List<int>(selectedIndices.Length);
        foreach (var index in selectedIndices)
        {
            var metadata = itemList.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int)
            {
                results.Add(metadata.AsInt32());
            }
        }

        return results;
    }
}
