using Godot;
using ThreeKingdom.Data;
using ThreeKingdom.Map;
using ThreeKingdom.UI;

namespace ThreeKingdom.Core;

public partial class GameBootstrap : Node
{
    private readonly WorldRepository _worldRepository = new();
    private readonly TurnManager _turnManager = new();
    private readonly CommandResolver _commandResolver = new();
    private readonly CombatResolver _combatResolver = new();
    private readonly AiController _aiController = new();
    private readonly LocalizationService _localization = new();

    public override void _Ready()
    {
        _localization.Load();

        var world = _worldRepository.LoadScenario("res://data/scenarios/phase1_scenario.json");
        if (world == null)
        {
            GD.PushError("Failed to load phase1 scenario.");
            return;
        }

        _turnManager.Initialize(world);
        _commandResolver.Initialize(_turnManager, _combatResolver, _localization);
        _aiController.Initialize(_commandResolver, _turnManager, _localization);

        var mapController = GetNodeOrNull<MapController>("MapScene");
        mapController?.BindWorld(world, _localization);

        var hudController = GetNodeOrNull<HudController>("HUD");
        hudController?.Initialize(_turnManager, _commandResolver, _aiController, _localization, mapController);

        if (mapController != null && hudController != null)
        {
            mapController.CitySelected += hudController.OnCitySelected;
        }

        GD.Print("Phase 1 bootstrap complete.");
    }
}
