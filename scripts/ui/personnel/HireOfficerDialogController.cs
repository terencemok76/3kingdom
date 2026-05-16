using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class HireOfficerDialogController
{
    private readonly PersonnelUiContext _context;
    private HireOfficerDialog? _dialog;
    private int _selectedOfficerId = -1;

    public HireOfficerDialogController(PersonnelUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        _dialog = GD.Load<PackedScene>("res://scenes/ui/personnel/HireOfficerDialog.tscn").Instantiate<HireOfficerDialog>();
        _dialog.Exclusive = false;
        _dialog.Unresizable = true;
        _context.AddChild(_dialog);
        _dialog.SelectOfficerPressed += OnSelectOfficerPressed;
        _dialog.GoldValueChanged += _ => UpdateSummary();
        _dialog.FoodValueChanged += _ => UpdateSummary();
        _dialog.ItemSelected += _ => UpdateSummary();
        _dialog.ConfirmPressed += OnConfirmPressed;
        _dialog.CloseRequested += () =>
        {
            _context.PlayUiClickSfx();
            _dialog?.Hide();
        };
        _dialog.Hide();
    }

    public void Hide() => _dialog?.Hide();

    public void Show()
    {
        if (_context.SelectedCity == null || _context.TurnManager?.World == null || _dialog == null || _context.Localization == null)
        {
            return;
        }

        RefreshText();
        Populate();
        _context.PopupDialog(_dialog);
    }

    public void RefreshText()
    {
        if (_dialog == null || _context.Localization == null)
        {
            return;
        }

        _dialog.SetDialogText(
            _context.Localization.T("command.personnel.hire_officer"),
            _context.Localization.T("ui.hire_officer_target"),
            _context.Localization.T("ui.select_officer"),
            _context.Localization.T("ui.hire_officer_gold_offer"),
            _context.Localization.T("ui.hire_officer_food_offer"),
            _context.Localization.T("ui.hire_officer_item_offer"),
            _context.Localization.T("ui.confirm_hire_officer"));
        UpdateSelectedOfficerSummary();
    }

    private void Populate()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        if (city == null || world == null || _dialog == null)
        {
            return;
        }

        var playerFactionId = _context.TurnManager!.GetPlayerFactionId();
        var candidates = GetOrderedCandidates(world, playerFactionId);

        _context.ConfigureMoveSpinBox(_dialog.GoldSpinBox, Math.Max(0, city.Gold - HudController.HireOfficerGoldCost), 0);
        _context.ConfigureMoveSpinBox(_dialog.FoodSpinBox, city.Food, 0);
        if (_dialog.GoldSpinBox != null)
        {
            _dialog.GoldSpinBox.Step = 100;
        }
        if (_dialog.FoodSpinBox != null)
        {
            _dialog.FoodSpinBox.Step = 500;
        }

        _context.PopulateFactionInventoryOption(_dialog.ItemOption);
        if (!candidates.Any(officer => officer.Id == _selectedOfficerId))
        {
            _selectedOfficerId = candidates.FirstOrDefault()?.Id ?? -1;
        }

        UpdateSelectedOfficerSummary();
        UpdateSummary();
        if (_dialog.ConfirmButton != null)
        {
            _dialog.ConfirmButton.Disabled = candidates.Count == 0;
        }
    }

    private void UpdateSummary()
    {
        if (_dialog == null || _context.Localization == null)
        {
            return;
        }

        var goldOffer = (int)(_dialog.GoldSpinBox?.Value ?? 0);
        var foodOffer = (int)(_dialog.FoodSpinBox?.Value ?? 0);
        var item = _context.GetSelectedItemFromOption(_dialog.ItemOption);
        var summary = item == null
            ? _context.Localization.Format("fmt.hire_officer_preview", HudController.HireOfficerGoldCost, goldOffer, foodOffer)
            : _context.Localization.Format("fmt.hire_officer_preview_with_item", HudController.HireOfficerGoldCost, goldOffer, foodOffer, _context.Localization.GetItemName(item));
        _dialog.SetSummaryText(summary);
    }

    private void OnSelectOfficerPressed()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || localization == null)
        {
            return;
        }

        var candidateIds = GetOrderedCandidates(world, _context.TurnManager!.GetPlayerFactionId())
            .Select(officer => officer.Id)
            .ToList();
        if (candidateIds.Count == 0)
        {
            _context.AddLog(localization.T("ui.no_hireable_officer"));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("command.personnel.hire_officer"),
            candidateIds,
            HudController.OfficerSelectorPrimaryStat.Charm,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
                UpdateSummary();
            });
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_dialog == null || _context.Localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unassigned");
        _dialog.SetSelectedOfficerSummary($"{_context.Localization.T("ui.hire_officer_target")}: {officerName}");
    }

    private static bool IsCandidate(PersonnelUiContext context, WorldState world, int playerFactionId, OfficerData officer)
    {
        if (context.IsFactionRuler(world, officer))
        {
            return false;
        }

        if (!context.IsOfficerOldEnoughToJoin(world, officer))
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

    private void OnConfirmPressed()
    {
        var city = _context.SelectedCity;
        var turnManager = _context.TurnManager;
        var commandResolver = _context.CommandResolver;
        if (city == null || turnManager == null || commandResolver == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            _context.AddLog(_context.Localization?.T("ui.select_officer_warning") ?? string.Empty);
            _context.PopupDialog(_dialog);
            return;
        }

        var result = commandResolver.ExecuteHireOfficer(
            turnManager.GetPlayerFactionId(),
            city.Id,
            _selectedOfficerId,
            (int)(_dialog?.GoldSpinBox?.Value ?? 0),
            (int)(_dialog?.FoodSpinBox?.Value ?? 0),
            _context.GetSelectedItemFromOption(_dialog?.ItemOption)?.Id ?? 0);
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _dialog?.Hide();
        _context.RefreshSelectedCity();
        _context.RefreshMapVisuals();
    }

    private List<OfficerData> GetOrderedCandidates(WorldState world, int playerFactionId)
    {
        return world.Officers
            .Where(officer => IsCandidate(_context, world, playerFactionId, officer))
            .OrderByDescending(officer => FreeOfficerMovement.IsVisibleFreeOfficer(world, officer))
            .ThenByDescending(officer => officer.Charm)
            .ThenByDescending(officer => officer.Intelligence)
            .ThenBy(officer => _context.Localization?.GetOfficerName(officer) ?? officer.Name)
            .ThenBy(officer => officer.Id)
            .ToList();
    }
}
