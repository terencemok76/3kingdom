using Godot;
using ThreeKingdom.Data;
using ThreeKingdom.Map;
using ThreeKingdom.UI;

namespace ThreeKingdom.Core;

public partial class GameBootstrap : Node
{
    private const string DefaultScenarioPath = "res://data/scenarios/phase1_scenario.json";

    private readonly WorldRepository _worldRepository = new();
    private readonly TurnManager _turnManager = new();
    private readonly CommandResolver _commandResolver = new();
    private readonly CombatResolver _combatResolver = new();
    private readonly AiController _aiController = new();
    private readonly LocalizationService _localization = new();
    private GameAudioController? _audioController;
    private bool _mapHudSignalsConnected;

    public override void _Ready()
    {
        _localization.Load();
        _audioController = new GameAudioController();
        AddChild(_audioController);

        var mapController = GetNodeOrNull<MapController>("MapScene");
        var hudController = GetNodeOrNull<HudController>("HUD");
        var startMenuController = GetNodeOrNull<GameStartMenuController>("GameStartMenu");
        if (mapController == null || hudController == null || startMenuController == null)
        {
            GD.PushError("Main scene bootstrap nodes are missing.");
            return;
        }

        mapController.Visible = false;
        hudController.Visible = false;

        startMenuController.Initialize(
            _localization,
            _worldRepository,
            new[]
            {
                new GameStartMenuController.ScenarioEntry(DefaultScenarioPath)
            });
        startMenuController.StartGameConfirmed += OnStartGameConfirmed;
        startMenuController.LoadGameConfirmed += OnLoadGameConfirmed;
        startMenuController.ShowMainMenu();
    }

    private void OnStartGameConfirmed(string scenarioPath, int factionId)
    {
        var world = _worldRepository.LoadScenario(scenarioPath);
        if (world == null)
        {
            GD.PushError($"Failed to load scenario: {scenarioPath}");
            return;
        }

        SetPlayerFaction(world, factionId);
        EnterGameplay(world);
    }

    private void OnLoadGameConfirmed(int slotNumber)
    {
        var world = _worldRepository.LoadSavedGame(BuildSaveSlotPath(slotNumber));
        if (world == null)
        {
            GD.PushError($"Failed to load save slot {slotNumber}.");
            return;
        }

        EnterGameplay(world);
    }

    private void EnterGameplay(WorldState world)
    {
        var mapController = GetNodeOrNull<MapController>("MapScene");
        var hudController = GetNodeOrNull<HudController>("HUD");
        var startMenuController = GetNodeOrNull<GameStartMenuController>("GameStartMenu");
        if (mapController == null || hudController == null || startMenuController == null)
        {
            GD.PushError("Gameplay nodes are missing.");
            return;
        }

        _turnManager.Initialize(world);
        _commandResolver.Initialize(_turnManager, _combatResolver, _localization);
        _aiController.Initialize(_commandResolver, _turnManager, _localization);
        hudController.Initialize(_turnManager, _commandResolver, _aiController, _localization, _worldRepository, mapController);
        hudController.ReapplyOptionSettings();

        if (!_mapHudSignalsConnected)
        {
            mapController.CitySelected += hudController.OnCitySelected;
            _mapHudSignalsConnected = true;
        }

        mapController.Visible = true;
        hudController.Visible = true;
        hudController.ApplyLoadedWorld(world);
        startMenuController.HideMenu();
        GD.Print("Game flow bootstrap complete.");
    }

    private static void SetPlayerFaction(WorldState world, int factionId)
    {
        foreach (var faction in world.Factions)
        {
            faction.IsPlayer = faction.Id == factionId;
        }
    }

    private static string BuildSaveSlotPath(int slotNumber)
    {
        return $"user://saves/slot{slotNumber:00}.json";
    }
}
