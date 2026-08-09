using Godot;
using ThreeKingdom.Battle;
using ThreeKingdom.Data;
using ThreeKingdom.Map;
using ThreeKingdom.UI;

namespace ThreeKingdom.Core;

public partial class GameBootstrap : Node
{
    private const string DefaultScenarioPath = "res://data/scenarios/story1_scenario.json";
    private const string BattleNorthEastScenePath = "res://scenes/battle/BattleScene.tscn";
    private const string BattleNorthWestScenePath = "res://scenes/battle/BattleSceneNorthWest.tscn";
    private const string FieldBattleLuoyangScenePath = "res://scenes/battle/field/FieldBattleLuoyang.tscn";

    private readonly WorldRepository _worldRepository = new();
    private readonly TurnManager _turnManager = new();
    private readonly CommandResolver _commandResolver = new();
    private readonly CombatResolver _combatResolver = new();
    private readonly AiController _aiController = new();
    private readonly LocalizationService _localization = new();
    private GameAudioController? _audioController;
    private bool _mapHudSignalsConnected;

    public override void _UnhandledInput(InputEvent @event)
    {
        ScreenshotShortcut.HandleInput(this, @event);
    }

    public override void _Ready()
    {
        _localization.Load();
        var optionSettings = OptionSettingsStore.LoadOrDefault();
        _localization.SetLanguage(optionSettings.Language);
        _audioController = new GameAudioController();
        AddChild(_audioController);
        _audioController.SetBgmEnabled(optionSettings.BgmEnabled);
        _audioController.SetSfxEnabled(optionSettings.SfxEnabled);
        _audioController.SetBgmVolume(optionSettings.BgmVolume);
        _audioController.SetSfxVolume(optionSettings.SfxVolume);

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
        startMenuController.BattleRequested += OnBattleRequested;
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

    private void OnBattleRequested(string variant)
    {
        var (scenePath, scenarioType, useEditorAuthoredLayout) = variant.ToUpperInvariant() switch
        {
            "FIELD" => (FieldBattleLuoyangScenePath, BattleScenarioType.FieldBattle, true),
            "NE_SIEGE" => (BattleNorthEastScenePath, BattleScenarioType.SiegeAssault, true),
            "NE_MOAT" => (BattleNorthEastScenePath, BattleScenarioType.MoatSiegeBattle, true),
            "NW_SIEGE" => (BattleNorthWestScenePath, BattleScenarioType.SiegeAssault, true),
            "NW_MOAT" => (BattleNorthWestScenePath, BattleScenarioType.MoatSiegeBattle, true),
            _ => (BattleNorthEastScenePath, BattleScenarioType.SiegeAssault, true)
        };

        BattleSceneController.PendingLaunchOptions =
            new BattleSceneController.LaunchOptions(scenarioType, useEditorAuthoredLayout);
        GetTree().ChangeSceneToFile(scenePath);
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
