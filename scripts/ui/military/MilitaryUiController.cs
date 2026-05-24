using System.Collections.Generic;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public sealed class MilitaryUiController
{
    private readonly AttackDialogController _attackDialogController;
    private readonly MilitaryCommandDialogController _commandDialogController;
    private readonly MoveDialogController _moveDialogController;
    private readonly RecruitTroopDialogController _recruitTroopDialogController;
    private readonly MilitaryUiContext _context;

    public MilitaryUiController(HudController owner)
    {
        _context = new MilitaryUiContext(owner);
        _attackDialogController = new AttackDialogController(_context);
        _moveDialogController = new MoveDialogController(_context);
        _recruitTroopDialogController = new RecruitTroopDialogController(_context);
        _commandDialogController = new MilitaryCommandDialogController(
            _context,
            OpenMoveFlow,
            OpenAttackFlow,
            _recruitTroopDialogController.Show);
    }

    public void Initialize()
    {
        _attackDialogController.Initialize();
        _commandDialogController.Initialize();
        _moveDialogController.Initialize();
        _recruitTroopDialogController.Initialize();
    }

    public void HideDialogs()
    {
        _attackDialogController.Hide();
        _commandDialogController.Hide();
        _moveDialogController.Hide();
        _recruitTroopDialogController.Hide();
    }

    public void RefreshText()
    {
        _attackDialogController.RefreshText();
        _commandDialogController.RefreshText();
        _moveDialogController.RefreshText();
        _recruitTroopDialogController.RefreshText();
    }

    public void ShowMilitaryDialog() => _commandDialogController.Show();

    public void OpenMoveFlow()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        if (city == null || world == null)
        {
            return;
        }

        var candidateIds = new List<int>();
        foreach (var targetId in city.ConnectedCityIds)
        {
            var target = world.GetCity(targetId);
            if (target == null || target.OwnerFactionId != city.OwnerFactionId)
            {
                continue;
            }

            candidateIds.Add(target.Id);
        }

        if (candidateIds.Count == 0)
        {
            _context.AddLog(_context.Localization?.T("ui.no_connected_friendly_city") ?? "No connected friendly city to move troops, resources, or officers.");
            return;
        }

        _moveDialogController.Show(candidateIds);
    }

    public void OpenAttackFlow()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        if (city == null || world == null)
        {
            return;
        }

        var candidateIds = new List<int>();
        foreach (var targetId in city.ConnectedCityIds)
        {
            var target = world.GetCity(targetId);
            if (target == null || target.OwnerFactionId == city.OwnerFactionId)
            {
                continue;
            }

            candidateIds.Add(target.Id);
        }

        if (candidateIds.Count == 0)
        {
            _context.AddLog(_context.Localization?.T("ui.no_connected_enemy_city") ?? "No connected enemy city to attack.");
            return;
        }

        _attackDialogController.ShowAttack(candidateIds);
    }

    public void ShowDefenseAttackDialog(PendingCommandData pendingCommand, CityData defendingCity, CityData attackingCity) =>
        _attackDialogController.ShowDefense(pendingCommand, defendingCity, attackingCity);

    public void ResetAttackDialogState() => _attackDialogController.ResetState();

    public void ProcessDialogs() => _attackDialogController.Process();

    public void CollectVisibleDialogOverlays(List<Control> overlays)
    {
        AddVisibleOverlay(overlays, _attackDialogController);
        AddVisibleOverlay(overlays, _commandDialogController);
        AddVisibleOverlay(overlays, _moveDialogController);
        AddVisibleOverlay(overlays, _recruitTroopDialogController);
    }

    private static void AddVisibleOverlay(List<Control> overlays, FloatingOverlayController controller)
    {
        if (controller.OverlayControl?.Visible == true)
        {
            overlays.Add(controller.OverlayControl);
        }
    }
}
