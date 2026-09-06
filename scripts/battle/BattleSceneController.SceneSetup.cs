using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThreeKingdom.Core;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattlePresentationSettings;
using static ThreeKingdom.Battle.BattleResourcePaths;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void CacheSceneNodes()
    {
        _mapRoot ??= GetNodeOrNull<Node2D>("MapRoot");
        _camera ??= GetNodeOrNull<Camera2D>("Camera2D");
        _groundLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/GroundLayer");
        _moatLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/MoatLayer");
        _objectLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/ObjectLayer");
        _workerFenceLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/WorkerFenceLayer");
        _castleLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/CastleLayer");
        _overlayLayer ??= GetNodeOrNull<TileMapLayer>("MapRoot/OverlayLayer");
        _highlightLayer ??= GetNodeOrNull<BattleHighlightRenderer>("MapRoot/HighlightLayer");
        _battleDepthLayer ??= GetNodeOrNull<Node2D>("MapRoot/BattleDepthLayer");
        _occludedUnitSilhouetteLayer ??= GetNodeOrNull<Node2D>("MapRoot/OccludedUnitSilhouetteLayer");
        if (_highlightLayer != null && !Engine.IsEditorHint())
        {
            _highlightLayer.Visible = false;
        }

        if (_battleDepthLayer == null && _mapRoot != null && !Engine.IsEditorHint())
        {
            _battleDepthLayer = new Node2D
            {
                Name = "BattleDepthLayer",
                ZIndex = 20
            };
            _mapRoot.AddChild(_battleDepthLayer);
        }

        if (_occludedUnitSilhouetteLayer == null && _mapRoot != null && !Engine.IsEditorHint())
        {
            _occludedUnitSilhouetteLayer = new Node2D
            {
                Name = "OccludedUnitSilhouetteLayer",
                ZIndex = 28
            };
            _mapRoot.AddChild(_occludedUnitSilhouetteLayer);
        }

        if (_occludedUnitSilhouetteLayer != null)
        {
            _occludedUnitSilhouetteLayer.ZIndex = 28;
        }

        if (_battleDepthLayer != null)
        {
            _battleDepthLayer.Visible = true;
            _battleDepthLayer.ZIndex = 20;
        }

        _commandMenu ??= GetNodeOrNull<Control>("UiLayer/CommandMenu");
        if (_commandMenu != null)
        {
            // Command buttons must stay clickable when the draggable battle log overlaps them.
            _commandMenu.ZIndex = 50;
        }
        _topBar ??= GetNodeOrNull<Control>("UiLayer/TopBar");
        _tileInfoPanel ??= GetNodeOrNull<Control>("UiLayer/SidePanel");
        _battleLogPanel ??= GetNodeOrNull<Control>("UiLayer/BattleLogPanel");
        _battleLogHeaderRow ??= GetNodeOrNull<Control>("UiLayer/BattleLogPanel/Margin/LogContent/HeaderRow");
        _battleLogContent ??= GetNodeOrNull<Control>("UiLayer/BattleLogPanel/Margin/LogContent");
        _battleLogScroll ??= GetNodeOrNull<Control>("UiLayer/BattleLogPanel/Margin/LogContent/LogScroll");
        _battleLogResizeGrip ??= GetNodeOrNull<Control>("UiLayer/BattleLogPanel/ResizeGrip");
        _battleResultOverlay ??= GetNodeOrNull<Control>("UiLayer/BattleResultOverlay");
        _battleResultLabel ??= GetNodeOrNull<Label>("UiLayer/BattleResultOverlay/ResultPanel/Margin/ResultLabel");
        if (_battleResultOverlay != null)
        {
            _battleResultOverlay.ZIndex = 200;
        }
        _retreatNotice ??= GetNodeOrNull<Control>("UiLayer/RetreatNotice");
        _retreatNoticeLabel ??= GetNodeOrNull<Label>("UiLayer/RetreatNotice/Margin/Label");
        _officerCaptureNotice ??= GetNodeOrNull<Control>("UiLayer/OfficerCaptureNotice");
        _officerCaptureNoticeLabel ??= GetNodeOrNull<Label>("UiLayer/OfficerCaptureNotice/Margin/Label");
        _turnBanner ??= GetNodeOrNull<Control>("UiLayer/TurnBanner");
        _turnBannerLabel ??= GetNodeOrNull<Label>("UiLayer/TurnBanner/Margin/Label");
        _officerSpeechOverlay ??= GetNodeOrNull<Control>("UiLayer/OfficerSpeechOverlay");
        _officerSpeechPortrait ??= GetNodeOrNull<TextureRect>("UiLayer/OfficerSpeechOverlay/Margin/Row/Portrait");
        _officerSpeechTeamNameLabel ??= GetNodeOrNull<Label>("UiLayer/OfficerSpeechOverlay/Margin/Row/TextColumn/TeamName");
        _officerSpeechNameLabel ??= GetNodeOrNull<Label>("UiLayer/OfficerSpeechOverlay/Margin/Row/TextColumn/OfficerName");
        _officerSpeechTextLabel ??= GetNodeOrNull<Label>("UiLayer/OfficerSpeechOverlay/Margin/Row/TextColumn/SpeechText");
        _allLogButton ??= GetNodeOrNull<Button>("UiLayer/BattleLogPanel/Margin/LogContent/HeaderRow/AllLogButton");
        _selfLogButton ??= GetNodeOrNull<Button>("UiLayer/BattleLogPanel/Margin/LogContent/HeaderRow/SelfLogButton");
        _minimizeLogButton ??= GetNodeOrNull<Button>("UiLayer/BattleLogPanel/Margin/LogContent/HeaderRow/MinimizeLogButton");
        _battleLogLabel ??= GetNodeOrNull<Label>("UiLayer/BattleLogPanel/Margin/LogContent/LogScroll/LogLabel");
        _battleLogTitleLabel ??= GetNodeOrNull<Label>("UiLayer/BattleLogPanel/Margin/LogContent/HeaderRow/TitleLabel");
        _timeOfDayOverlay ??= GetNodeOrNull<ColorRect>("UiLayer/TimeOfDayOverlay");
        _weatherOverlay ??= GetNodeOrNull<ColorRect>("UiLayer/WeatherOverlay");
        _windowTitleLabel ??= GetNodeOrNull<Label>("UiLayer/CommandMenu/MenuMargin/MenuButtons/WindowTitleLabel");
        _unitMenuInfoLabel ??= GetNodeOrNull<Label>("UiLayer/CommandMenu/MenuMargin/MenuButtons/OfficerInfoRow/UnitMenuInfoLabel");
        _officerPortrait ??= GetNodeOrNull<TextureRect>("UiLayer/CommandMenu/MenuMargin/MenuButtons/OfficerInfoRow/OfficerPortrait");
        _commandScroll ??= GetNodeOrNull<ScrollContainer>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll");
        _endTurnButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/EndTurnButton");
        _enableAiButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/EnableAiButton");
        _disableAiButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/DisableAiButton");
        _startRoundButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/StartRoundButton");
        _nextAiButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/NextAiButton");
        _attackerOneDayFoodButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/AttackerOneDayFoodButton");
        _defenderOneDayFoodButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/DefenderOneDayFoodButton");
        _aiRoundStatusLabel ??= GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/AiRoundStatusLabel");
        _timeButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/TimeButton");
        _weatherButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/WeatherButton");
        _windButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/WindButton");
        _windPowerButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/WindPowerButton");
        _battleOptionButton ??= GetNodeOrNull<Button>("UiLayer/TopBar/Margin/TopBarContent/TopHeaderRow/BattleOptionButton");
        _battleOptionOverlay ??= GetNodeOrNull<Control>("UiLayer/BattleOptionOverlay");
        if (_battleOptionOverlay != null)
        {
            _battleOptionOverlay.ZIndex = 190;
        }
        _battleOptionTitleLabel ??= GetNodeOrNull<Label>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/TitleBar/TitleLabel");
        _battleOptionCloseButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/TitleBar/CloseButton");
        _battleOptionSaveButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SaveLoadRow/SaveButton");
        _battleOptionLoadButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SaveLoadRow/LoadButton");
        _battleOptionLanguageButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/LanguageButton");
        _battleBgmToggleButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/BgmAudioRow/BgmToggleButton");
        _battleBgmVolumeSlider ??= GetNodeOrNull<HSlider>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/BgmAudioRow/BgmVolumeSlider");
        _battleBgmVolumeValueLabel ??= GetNodeOrNull<Label>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/BgmAudioRow/BgmVolumeValueLabel");
        _battleSfxToggleButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SfxAudioRow/SfxToggleButton");
        _battleSfxVolumeSlider ??= GetNodeOrNull<HSlider>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SfxAudioRow/SfxVolumeSlider");
        _battleSfxVolumeValueLabel ??= GetNodeOrNull<Label>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SfxAudioRow/SfxVolumeValueLabel");
        _battleSaveSettingsButton ??= GetNodeOrNull<Button>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SaveSettingsButton");
        _moveButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/MoveButton");
        _attackButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/AttackButton");
        _unionAttackButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/UnionAttackButton");
        _guardButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/GuardButton");
        _chargeButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/ChargeButton");
        _duelButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/DuelButton");
        _retreatButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/RetreatButton");
        _hideButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/HideButton");
        _dropStoneButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/DropStoneButton");
        _pourOilButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/PourOilButton");
        _workButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/WorkButton");
        _installWoodFenceButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/InstallWoodFenceButton");
        _uninstallWoodFenceButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/UninstallWoodFenceButton");
        _supplyButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/SupplyButton");
        _resupplyWeaponButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/ResupplyWeaponButton");
        _captureSupplyCartButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/CaptureSupplyCartButton");
        _hireOfficerButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/HireOfficerButton");
        _strategyButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/StrategyButton");
        _openGateButton ??= GetNodeOrNull<Button>("UiLayer/CommandMenu/MenuMargin/MenuButtons/CommandScroll/ActionButtons/OpenGateButton");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        ScreenshotShortcut.HandleInput(this, @event);
        if (GetViewport().IsInputHandled())
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton mouseButton:
                HandleMouseButton(mouseButton);
                break;
            case InputEventMouseMotion mouseMotion:
                HandleMouseMotion(mouseMotion);
                break;
        }
    }

    public override void _Input(InputEvent @event)
    {
        HandleBattleLogPanelInput(@event);
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
        {
            return;
        }

        UpdateHoverGrid();
        UpdateWeatherVisual(delta);
    }


    private void InitializeMapDataAndLayers()
    {
        if (_groundLayer == null || _objectLayer == null || _castleLayer == null || _overlayLayer == null)
        {
            return;
        }

        var scenarioDefinition = ResolveScenarioDefinition();

        if (ShouldUseEditorAuthoredLayout() && HasEditorAuthoredLayout())
        {
            _mapData = BattleMapData.CreateFromTileMapLayers(_groundLayer, _moatLayer, _objectLayer, _castleLayer, _overlayLayer, scenarioDefinition);
            BattleTileMapBuilder.ConfigureLayer(_groundLayer, _mapData, BattleTileLayerKind.Ground);
            ConfigureOptionalTileMapLayer(_moatLayer, BattleTileLayerKind.Moat);
            BattleTileMapBuilder.ConfigureLayer(_objectLayer, _mapData, BattleTileLayerKind.Object);
            if (_objectLayer != null)
            {
                _objectLayer.Visible = true;
            }
            BattleTileMapBuilder.ConfigureLayer(_overlayLayer, _mapData, BattleTileLayerKind.DeploymentOverlay);
            return;
        }

        _mapData = BattleMapData.Create(scenarioDefinition, _objectLayer);
        ConfigureTileMapLayer("MapRoot/GroundLayer", BattleTileLayerKind.Ground);
        ConfigureTileMapLayer("MapRoot/MoatLayer", BattleTileLayerKind.Moat);
        ConfigureTileMapLayer("MapRoot/ObjectLayer", BattleTileLayerKind.Object);
        ConfigureTileMapLayer("MapRoot/CastleLayer", BattleTileLayerKind.Castle);
        ConfigureTileMapLayer("MapRoot/OverlayLayer", BattleTileLayerKind.DeploymentOverlay);
    }

    private bool HasEditorAuthoredLayout()
    {
        return (_groundLayer?.GetUsedCells().Count ?? 0) > 0 ||
               (_moatLayer?.GetUsedCells().Count ?? 0) > 0 ||
               (_objectLayer?.GetUsedCells().Count ?? 0) > 0 ||
               (_castleLayer?.GetUsedCells().Count ?? 0) > 0 ||
               (_overlayLayer?.GetUsedCells().Count ?? 0) > 0;
    }

    private bool ShouldUseEditorAuthoredLayout()
    {
        return UseEditorAuthoredLayout || IsNorthWestSceneVariant();
    }

    private void BakeBattleLayoutInEditor()
    {
        if (_groundLayer == null || _objectLayer == null || _castleLayer == null || _overlayLayer == null)
        {
            return;
        }

        _mapData = BattleMapData.Create(ResolveScenarioDefinition());
        BattleTileMapBuilder.ConfigureLayer(_groundLayer, _mapData, BattleTileLayerKind.Ground);
        ConfigureOptionalTileMapLayer(_moatLayer, BattleTileLayerKind.Moat);
        BattleTileMapBuilder.ConfigureLayer(_objectLayer, _mapData, BattleTileLayerKind.Object);
        if (_objectLayer != null)
        {
            _objectLayer.Visible = true;
        }
        BattleTileMapBuilder.ConfigureLayer(_castleLayer, _mapData, BattleTileLayerKind.Castle);
        BattleTileMapBuilder.ConfigureLayer(_overlayLayer, _mapData, BattleTileLayerKind.DeploymentOverlay);
    }

    private BattleScenarioDefinition ResolveScenarioDefinition()
    {
        if (TryResolveSceneScenarioDefinition(out var sceneScenarioDefinition))
        {
            return sceneScenarioDefinition;
        }

        if (ScenarioDefinition is BattleScenarioDefinition configuredScenarioDefinition)
        {
            return configuredScenarioDefinition;
        }

        return BattleScenarioDefinition.CreateBuiltIn(ScenarioType);
    }

    private bool TryResolveSceneScenarioDefinition(out BattleScenarioDefinition scenarioDefinition)
    {
        scenarioDefinition = null!;
        var scenarioPath = ResolveSceneScenarioPath();
        if (string.IsNullOrWhiteSpace(scenarioPath) || !ResourceLoader.Exists(scenarioPath))
        {
            return false;
        }

        var loadedScenarioResource = GD.Load<Resource>(scenarioPath);
        if (loadedScenarioResource is BattleScenarioDefinition loadedScenarioDefinition)
        {
            scenarioDefinition = loadedScenarioDefinition;
            return true;
        }

        if (TryCreateKnownSceneScenarioDefinition(scenarioPath, out scenarioDefinition))
        {
            return true;
        }

        return false;
    }

    private static bool TryCreateKnownSceneScenarioDefinition(string scenarioPath, out BattleScenarioDefinition scenarioDefinition)
    {
        scenarioDefinition = null!;
        var isNorthWest = scenarioPath.Equals(NorthWestSiegeScenarioPath, StringComparison.OrdinalIgnoreCase) ||
                          scenarioPath.Equals(NorthWestMoatScenarioPath, StringComparison.OrdinalIgnoreCase);
        var isMoat = scenarioPath.Equals(NorthEastMoatScenarioPath, StringComparison.OrdinalIgnoreCase) ||
                     scenarioPath.Equals(NorthWestMoatScenarioPath, StringComparison.OrdinalIgnoreCase);

        if (!isNorthWest &&
            !scenarioPath.Equals(NorthEastSiegeScenarioPath, StringComparison.OrdinalIgnoreCase) &&
            !scenarioPath.Equals(NorthEastMoatScenarioPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        scenarioDefinition = BattleScenarioDefinition.CreateBuiltIn(isMoat
            ? BattleScenarioType.MoatSiegeBattle
            : BattleScenarioType.SiegeAssault);
        scenarioDefinition.DisplayName = isMoat
            ? $"Moat Siege Battle ({(isNorthWest ? "NW" : "NE")})"
            : $"Siege Battle ({(isNorthWest ? "NW" : "NE")})";
        scenarioDefinition.DefaultStructureFacing = isNorthWest
            ? BattleStructureFacing.NorthWest
            : BattleStructureFacing.NorthEast;
        scenarioDefinition.OpenGateForegroundSide = isNorthWest
            ? BattleGateForegroundSide.Left
            : BattleGateForegroundSide.Right;
        scenarioDefinition.Weather = isMoat ? BattleWeatherType.Cloudy : BattleWeatherType.Sunny;
        scenarioDefinition.WindDirection = isNorthWest
            ? BattleWindDirection.SouthWest
            : BattleWindDirection.SouthEast;
        scenarioDefinition.WindPower = BattleWindPower.Breeze;
        scenarioDefinition.TimeOfDay = BattleTimeOfDay.Morning;
        scenarioDefinition.UnitSpawnGrids = CreateKnownSceneUnitSpawnGrids(isNorthWest);
        return true;
    }

    private static Godot.Collections.Dictionary<string, Vector2I> CreateKnownSceneUnitSpawnGrids(bool isNorthWest)
    {
        if (isNorthWest)
        {
            return new Godot.Collections.Dictionary<string, Vector2I>
            {
                { "AttackerA", new Vector2I(20, 14) },
                { "AttackerB", new Vector2I(18, 12) },
                { "AttackerC", new Vector2I(20, 10) },
                { "AttackerWorker", new Vector2I(18, 14) },
                { "Catapult", new Vector2I(15, 10) },
                { "DefenderA", new Vector2I(7, 14) },
                { "DefenderB", new Vector2I(7, 12) },
                { "DefenderC", new Vector2I(7, 10) },
                { "Ladder", new Vector2I(15, 14) },
                { "Ram", new Vector2I(16, 12) },
                { "Spearman", new Vector2I(18, 16) },
                { "SupplyCart", new Vector2I(18, 15) },
                { "Worker", new Vector2I(5, 9) }
            };
        }

        return new Godot.Collections.Dictionary<string, Vector2I>
        {
            { "AttackerA", new Vector2I(10, 20) },
            { "AttackerB", new Vector2I(12, 18) },
            { "AttackerC", new Vector2I(14, 20) },
            { "AttackerWorker", new Vector2I(16, 20) },
            { "Catapult", new Vector2I(14, 15) },
            { "DefenderA", new Vector2I(10, 7) },
            { "DefenderB", new Vector2I(14, 7) },
            { "DefenderC", new Vector2I(12, 7) },
            { "Ladder", new Vector2I(10, 15) },
            { "Ram", new Vector2I(12, 16) },
            { "Spearman", new Vector2I(8, 18) },
            { "SupplyCart", new Vector2I(16, 19) },
            { "Worker", new Vector2I(16, 5) }
        };
    }

    private string? ResolveSceneScenarioPath()
    {
        if (IsNorthWestSceneVariant())
        {
            return ScenarioType switch
            {
                BattleScenarioType.SiegeAssault => NorthWestSiegeScenarioPath,
                BattleScenarioType.MoatSiegeBattle => NorthWestMoatScenarioPath,
                _ => null
            };
        }

        if (IsNorthEastSceneVariant())
        {
            return ScenarioType switch
            {
                BattleScenarioType.SiegeAssault => NorthEastSiegeScenarioPath,
                BattleScenarioType.MoatSiegeBattle => NorthEastMoatScenarioPath,
                _ => null
            };
        }

        return null;
    }

    private bool IsNorthWestSceneVariant()
    {
        var sceneFilePath = GetCurrentSceneFilePath();
        return !string.IsNullOrWhiteSpace(sceneFilePath) &&
               sceneFilePath.EndsWith(NorthWestScenePath, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsNorthEastSceneVariant()
    {
        var sceneFilePath = GetCurrentSceneFilePath();
        return !string.IsNullOrWhiteSpace(sceneFilePath) &&
               sceneFilePath.EndsWith(NorthEastScenePath, StringComparison.OrdinalIgnoreCase);
    }

    private string GetCurrentSceneFilePath()
    {
        // Tool-mode previews can run before the node is attached to a SceneTree.
        var sceneFilePath = GetTree()?.CurrentScene?.SceneFilePath;
        if (string.IsNullOrWhiteSpace(sceneFilePath))
        {
            sceneFilePath = SceneFilePath;
        }

        return sceneFilePath;
    }

    private void ClearTileLayoutInEditor()
    {
        ClearLayer(_groundLayer, BattleTileLayerKind.Ground);
        ClearLayer(_moatLayer, BattleTileLayerKind.Moat);
        ClearLayer(_objectLayer, BattleTileLayerKind.Object);
        ClearLayer(_castleLayer, BattleTileLayerKind.Castle);
        ClearLayer(_overlayLayer, BattleTileLayerKind.DeploymentOverlay);
    }

    private static void ClearLayer(TileMapLayer? layer, BattleTileLayerKind layerKind)
    {
        if (layer == null)
        {
            return;
        }

        BattleTileMapBuilder.AssignLayerTileSet(layer, layerKind);
        foreach (var coords in layer.GetUsedCells())
        {
            layer.EraseCell(coords);
        }

        layer.UpdateInternals();
    }

    private void ConfigureTileMapLayer(string path, BattleTileLayerKind layerKind)
    {
        var tileMapLayer = GetNodeOrNull<TileMapLayer>(path);
        if (tileMapLayer == null)
        {
            return;
        }

        BattleTileMapBuilder.ConfigureLayer(tileMapLayer, _mapData!, layerKind);
        if (layerKind == BattleTileLayerKind.Moat)
        {
            tileMapLayer.Visible = ScenarioType == BattleScenarioType.MoatSiegeBattle;
        }
    }

    private static void AssignOptionalTileMapLayer(TileMapLayer? layer, BattleTileLayerKind layerKind)
    {
        if (layer == null)
        {
            return;
        }

        BattleTileMapBuilder.AssignLayerTileSet(layer, layerKind);
    }

    private void ConfigureOptionalTileMapLayer(TileMapLayer? layer, BattleTileLayerKind layerKind)
    {
        if (layer == null || _mapData == null)
        {
            return;
        }

        BattleTileMapBuilder.ConfigureLayer(layer, _mapData, layerKind);
        if (layerKind == BattleTileLayerKind.Moat)
        {
            layer.Visible = ScenarioType == BattleScenarioType.MoatSiegeBattle;
        }
    }



}
