using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private void ShowHireOfficerDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _hireOfficerDialog == null || _localization == null)
        {
            return;
        }

        UpdateHireOfficerDialogText();
        PopulateHireOfficerDialog();
        ShowHireOfficerDialogAtComputedSize();
    }

    private void PopulateHireOfficerDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _localization == null || _hireOfficerDialog == null)
        {
            return;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        var candidates = GetOrderedHireOfficerCandidates(_turnManager.World, playerFactionId);

        ConfigureMoveSpinBox(_hireOfficerDialog.GoldSpinBox, Math.Max(0, _selectedCity.Gold - HireOfficerGoldCost), 0);
        ConfigureMoveSpinBox(_hireOfficerDialog.FoodSpinBox, _selectedCity.Food, 0);
        if (_hireOfficerDialog.GoldSpinBox != null)
        {
            _hireOfficerDialog.GoldSpinBox.Step = 100;
        }
        if (_hireOfficerDialog.FoodSpinBox != null)
        {
            _hireOfficerDialog.FoodSpinBox.Step = 500;
        }

        PopulateFactionInventoryOption(_hireOfficerDialog.ItemOption);
        if (!candidates.Any(officer => officer.Id == _hireOfficerSelectedOfficerId))
        {
            _hireOfficerSelectedOfficerId = candidates.FirstOrDefault()?.Id ?? -1;
        }

        UpdateHireOfficerSelectedOfficerSummary();
        UpdateHireOfficerSummary();
        if (_hireOfficerDialog.ConfirmButton != null)
        {
            _hireOfficerDialog.ConfirmButton.Disabled = candidates.Count == 0;
        }
    }

    private void UpdateHireOfficerDialogText()
    {
        if (_hireOfficerDialog == null || _localization == null)
        {
            return;
        }

        _hireOfficerDialog.SetDialogText(
            _localization.T("command.personnel.hire_officer"),
            _localization.T("ui.hire_officer_target"),
            _localization.T("ui.select_officer"),
            _localization.T("ui.hire_officer_gold_offer"),
            _localization.T("ui.hire_officer_food_offer"),
            _localization.T("ui.hire_officer_item_offer"),
            _localization.T("ui.confirm_hire_officer"));
        UpdateHireOfficerSelectedOfficerSummary();
    }

    private void UpdateHireOfficerSummary()
    {
        if (_hireOfficerDialog == null || _localization == null)
        {
            return;
        }

        var goldOffer = (int)(_hireOfficerDialog.GoldSpinBox?.Value ?? 0);
        var foodOffer = (int)(_hireOfficerDialog.FoodSpinBox?.Value ?? 0);
        var item = GetSelectedItemFromOption(_hireOfficerDialog.ItemOption);
        var summary = item == null
            ? _localization.Format(
                "fmt.hire_officer_preview",
                HireOfficerGoldCost,
                goldOffer,
                foodOffer)
            : _localization.Format(
                "fmt.hire_officer_preview_with_item",
                HireOfficerGoldCost,
                goldOffer,
                foodOffer,
                _localization.GetItemName(item));
        _hireOfficerDialog.SetSummaryText(summary);
    }

    private void OnHireOfficerSelectOfficerPressed()
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return;
        }

        var candidateIds = GetOrderedHireOfficerCandidates(_turnManager.World, _turnManager.GetPlayerFactionId())
            .Select(officer => officer.Id)
            .ToList();
        if (candidateIds.Count == 0)
        {
            AddLog(_localization.T("ui.no_hireable_officer"));
            return;
        }

        ShowOfficerSelectorDialog(
            _localization.T("command.personnel.hire_officer"),
            candidateIds,
            OfficerSelectorPrimaryStat.Charm,
            SelectHireOfficerById);
    }

    private void SelectHireOfficerById(int officerId)
    {
        _hireOfficerSelectedOfficerId = officerId;
        UpdateHireOfficerSelectedOfficerSummary();
        UpdateHireOfficerSummary();
    }

    private void UpdateHireOfficerSelectedOfficerSummary()
    {
        if (_hireOfficerDialog == null || _localization == null)
        {
            return;
        }

        var officer = _hireOfficerSelectedOfficerId > 0 ? _turnManager?.World?.GetOfficer(_hireOfficerSelectedOfficerId) : null;
        var officerName = officer != null ? _localization.GetOfficerName(officer) : _localization.T("ui.unassigned");
        _hireOfficerDialog.SetSelectedOfficerSummary($"{_localization.T("ui.hire_officer_target")}: {officerName}");
    }

    private static bool IsHireOfficerCandidate(WorldState world, int playerFactionId, OfficerData officer)
    {
        if (IsFactionRuler(world, officer))
        {
            return false;
        }

        if (!IsOfficerOldEnoughToJoin(world, officer))
        {
            return false;
        }

        if (FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
        {
            return true;
        }

        var sourceCity = officer.CityId > 0 ? world.GetCity(officer.CityId) : null;
        return sourceCity == null || sourceCity.OwnerFactionId != playerFactionId;
    }

    private void OnHireOfficerDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var selectedOfficerId = _hireOfficerSelectedOfficerId;
        if (selectedOfficerId <= 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenHireOfficerDialog();
            return;
        }

        var result = _commandResolver.ExecuteHireOfficer(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            selectedOfficerId,
            (int)(_hireOfficerDialog?.GoldSpinBox?.Value ?? 0),
            (int)(_hireOfficerDialog?.FoodSpinBox?.Value ?? 0),
            GetSelectedItemFromOption(_hireOfficerDialog?.ItemOption)?.Id ?? 0);
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        _hireOfficerDialog?.Hide();
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }

    private void ReopenHireOfficerDialog()
    {
        CallDeferred(nameof(ReopenHireOfficerDialogDeferred));
    }

    private void ReopenHireOfficerDialogDeferred()
    {
        ShowHireOfficerDialogAtComputedSize();
    }

    private void ShowHireOfficerDialogAtComputedSize()
    {
        if (_hireOfficerDialog == null)
        {
            return;
        }

        PopupDialogUsingSceneSize(_hireOfficerDialog);
    }

    private void OnHireOfficerOfferChanged(double _)
    {
        UpdateHireOfficerSummary();
    }

    private void OnHireOfficerItemSelected(long _)
    {
        UpdateHireOfficerSummary();
    }

    private List<OfficerData> GetOrderedHireOfficerCandidates(WorldState world, int playerFactionId)
    {
        return world.Officers
            .Where(officer => IsHireOfficerCandidate(world, playerFactionId, officer))
            .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
            .ThenByDescending(officer => officer.Charm)
            .ThenByDescending(officer => officer.Intelligence)
            .ThenBy(officer => _localization?.GetOfficerName(officer) ?? officer.Name)
            .ThenBy(officer => officer.Id)
            .ToList();
    }

    private void PopulateFactionInventoryOption(OptionButton? option)
    {
        if (option == null || _selectedCity == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        option.Clear();
        option.AddItem(_localization.T("ui.no_item"));
        option.SetItemMetadata(0, 0);

        foreach (var item in _turnManager.World.Items
                     .Where(item => item.OwnerFactionId == _selectedCity.OwnerFactionId && item.EquippedOfficerId <= 0)
                     .OrderBy(item => _localization.GetItemName(item)))
        {
            var row = _localization.Format(
                "fmt.item_option",
                _localization.GetItemName(item),
                _localization.GetItemType(item),
                _localization.GetItemRarity(item));
            option.AddItem(row);
            option.SetItemMetadata(option.ItemCount - 1, item.Id);
        }

        option.Select(0);
    }

    private ItemData? GetSelectedItemFromOption(OptionButton? option)
    {
        if (option == null || _turnManager?.World == null)
        {
            return null;
        }

        var selectedIndex = option.Selected;
        if (selectedIndex < 0)
        {
            return null;
        }

        var metadata = option.GetItemMetadata(selectedIndex);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return null;
        }

        var itemId = metadata.AsInt32();
        return itemId > 0 ? _turnManager.World.GetItem(itemId) : null;
    }
}
