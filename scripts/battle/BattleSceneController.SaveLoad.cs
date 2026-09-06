using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattlePresentationSettings;
using static ThreeKingdom.Battle.BattleResourcePaths;
using static ThreeKingdom.Battle.BattleUnitTypes;
using static ThreeKingdom.Battle.BattleUnitVisualCatalog;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private static readonly JsonSerializerOptions BattleSaveJsonOptions = new()
    {
        WriteIndented = true
    };

    private bool TrySaveBattleQuickSave(out string errorMessage)
    {
        errorMessage = string.Empty;
        if (_mapData == null)
        {
            errorMessage = "battle map is not ready";
            return false;
        }

        try
        {
            var saveData = CreateBattleSaveData();
            Directory.CreateDirectory(ProjectSettings.GlobalizePath("user://saves"));
            File.WriteAllText(
                ProjectSettings.GlobalizePath(BattleQuickSavePath),
                JsonSerializer.Serialize(saveData, BattleSaveJsonOptions),
                Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            GD.PushError($"Battle quick save failed: {ex}");
            return false;
        }
    }

    private bool TryLoadBattleQuickSave(out string errorMessage)
    {
        errorMessage = string.Empty;
        var resolvedPath = ProjectSettings.GlobalizePath(BattleQuickSavePath);
        if (!File.Exists(resolvedPath))
        {
            errorMessage = "battle quick save file not found";
            return false;
        }

        try
        {
            var json = File.ReadAllText(resolvedPath, Encoding.UTF8);
            var saveData = JsonSerializer.Deserialize<BattleSaveData>(json, BattleSaveJsonOptions);
            if (saveData == null || saveData.Version != 1)
            {
                errorMessage = "unsupported battle save format";
                return false;
            }

            ApplyBattleSaveData(saveData);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            GD.PushError($"Battle quick load failed: {ex}");
            return false;
        }
    }

    private BattleSaveData CreateBattleSaveData()
    {
        var saveData = new BattleSaveData
        {
            ScenarioType = ScenarioType,
            UseEditorAuthoredLayout = UseEditorAuthoredLayout,
            TurnNumber = _turnNumber,
            CurrentTurnSide = _currentTurnSide,
            BattleDateYear = _battleDateYear,
            BattleDateMonth = _battleDateMonth,
            BattleDateDay = _battleDateDay,
            CurrentBattleTimeOfDay = GetCurrentBattleTimeOfDay(),
            CurrentBattleWeather = GetCurrentBattleWeather(),
            CurrentBattleWindDirection = GetCurrentBattleWindDirection(),
            CurrentBattleWindPower = GetCurrentBattleWindPower(),
            TeamAStrategyPlans = _teamAStrategyPlans,
            TeamBStrategyPlans = _teamBStrategyPlans,
            TeamAGold = _teamAGold,
            TeamAFood = _teamAFood,
            TeamAZeroFoodDays = _teamAZeroFoodDays,
            TeamBGold = _teamBGold,
            TeamBFood = _teamBFood,
            TeamBZeroFoodDays = _teamBZeroFoodDays,
            ShowSelfTeamLogOnly = _showSelfTeamLogOnly,
            BattleLogExpandedWidth = _battleLogExpandedSize.X,
            BattleLogExpandedHeight = _battleLogExpandedSize.Y,
            IsBattleLogMinimized = _isBattleLogMinimized,
            AttackerOutpostVictorySecured = _attackerOutpostVictorySecured
        };

        if (_mapData != null)
        {
            for (var y = 0; y < BattleMapData.Height; y++)
            {
                for (var x = 0; x < BattleMapData.Width; x++)
                {
                    saveData.Cells.Add(BattleCellSaveData.FromCell(_mapData.GetCell(x, y)));
                }
            }
        }

        foreach (var (grid, occupants) in _occupantsByGrid.OrderBy(entry => entry.Key.Y).ThenBy(entry => entry.Key.X).ThenBy(entry => entry.Key.Level))
        {
            foreach (var occupant in occupants)
            {
                if (!IsBattlePiece(occupant))
                {
                    continue;
                }

                var occupantSave = BattleOccupantSaveData.FromOccupant(grid, occupant);
                saveData.Occupants.Add(occupantSave);
                if (occupant.Marker == null)
                {
                    continue;
                }

                if (_strategyUsedByMarkerThisTurn.Contains(occupant.Marker))
                {
                    saveData.StrategyUsedUnitIds.Add(occupantSave.UnitId);
                }

                if (_supplyUsedByMarkerThisTurn.Contains(occupant.Marker))
                {
                    saveData.SupplyUsedUnitIds.Add(occupantSave.UnitId);
                }

                if (_chargeUsedByMarkerThisTurn.Contains(occupant.Marker))
                {
                    saveData.ChargeUsedUnitIds.Add(occupantSave.UnitId);
                }

                if (_wallTopAttackAmmoByMarker.TryGetValue(occupant.Marker, out var ammo))
                {
                    saveData.WallTopAmmo.Add(new BattleWallTopAmmoSaveData
                    {
                        UnitId = occupantSave.UnitId,
                        DropStoneUses = ammo.DropStoneUses,
                        PourOilUses = ammo.PourOilUses
                    });
                }
            }
        }

        foreach (var (grid, fireState) in _activeFireByGrid.OrderBy(entry => entry.Key.Y).ThenBy(entry => entry.Key.X).ThenBy(entry => entry.Key.Level))
        {
            saveData.ActiveFires.Add(new BattleFireSaveData
            {
                X = grid.X,
                Y = grid.Y,
                Level = grid.Level,
                RemainingTurns = fireState.RemainingTurns,
                BurnTurns = fireState.BurnTurns
            });
        }

        foreach (var log in _battleLogs)
        {
            saveData.Logs.Add(new BattleLogSaveData
            {
                Turn = log.Turn,
                TeamName = log.TeamName,
                Category = log.Category,
                Message = log.Message
            });
        }

        return saveData;
    }

    private void ApplyBattleSaveData(BattleSaveData saveData)
    {
        var reusableMarkersByStableId = CollectReusableBattleMarkersByStableId();
        ResetBattleRuntimeStateForLoad();

        ScenarioType = saveData.ScenarioType;
        UseEditorAuthoredLayout = saveData.UseEditorAuthoredLayout;
        _turnNumber = Math.Max(1, saveData.TurnNumber);
        _currentTurnSide = saveData.CurrentTurnSide;
        _battleDateYear = saveData.BattleDateYear > 0 ? saveData.BattleDateYear : BattleDateYear;
        _battleDateMonth = Mathf.Clamp(saveData.BattleDateMonth, 1, 12);
        _battleDateDay = Mathf.Clamp(saveData.BattleDateDay, 1, DateTime.DaysInMonth(_battleDateYear, _battleDateMonth));
        if (saveData.BattleDateMonth <= 0 || saveData.BattleDateDay <= 0)
        {
            _battleDateMonth = BattleDateMonth;
            _battleDateDay = BattleDateDay;
        }
        _currentBattleTimeOfDay = saveData.CurrentBattleTimeOfDay;
        _currentBattleWeather = saveData.CurrentBattleWeather;
        _currentBattleWindDirection = saveData.CurrentBattleWindDirection;
        _currentBattleWindPower = saveData.CurrentBattleWindPower;
        _teamAStrategyPlans = Mathf.Clamp(saveData.TeamAStrategyPlans, 0, InitialTeamStrategyPlans);
        _teamBStrategyPlans = Mathf.Clamp(saveData.TeamBStrategyPlans, 0, InitialTeamStrategyPlans);
        _teamAGold = Math.Max(0, saveData.TeamAGold);
        _teamAFood = Math.Max(0, saveData.TeamAFood);
        _teamAZeroFoodDays = Mathf.Max(0, saveData.TeamAZeroFoodDays);
        _teamBGold = Math.Max(0, saveData.TeamBGold);
        _teamBFood = Math.Max(0, saveData.TeamBFood);
        _teamBZeroFoodDays = Mathf.Max(0, saveData.TeamBZeroFoodDays);
        _showSelfTeamLogOnly = saveData.ShowSelfTeamLogOnly;
        _attackerOutpostVictorySecured = saveData.AttackerOutpostVictorySecured;
        _isBattleLogMinimized = saveData.IsBattleLogMinimized;
        _battleLogExpandedSize = new Vector2(
            Mathf.Max(BattleLogMinimumWidth, saveData.BattleLogExpandedWidth),
            Mathf.Max(BattleLogMinimumHeight, saveData.BattleLogExpandedHeight));

        InitializeMapDataAndLayers();
        ApplySavedCells(saveData.Cells);
        RefreshBattleMapVisualsAfterLoad();

        var markersByUnitId = new Dictionary<string, BattlePieceMarker>();
        foreach (var occupantSave in saveData.Occupants ?? new List<BattleOccupantSaveData>())
        {
            var marker = CreateSavedBattleMarker(occupantSave, reusableMarkersByStableId);
            if (marker != null)
            {
                markersByUnitId[occupantSave.UnitId] = marker;
            }
        }

        RecalculateBattleHudTotals();
        RestoreBattleTurnUsage(saveData, markersByUnitId);
        RestoreBattleLogs(saveData.Logs);
        RestoreBattleFires(saveData.ActiveFires);

        ConfigureHud();
        ApplyTimeOfDayVisual(animate: false);
        BuildWeatherVisuals();
        ApplyWeatherVisual(animate: false);
        RefreshBattleLogPanel();
        RefreshInfoPanel();
        RefreshHighlights();
        RefreshOccludedUnitSilhouettes();
        RefreshBattleDepthLayerOrder();
    }

    private void ResetBattleRuntimeStateForLoad()
    {
        HideCommandMenu();
        _isBattleFinished = false;
        _attackerOutpostVictorySecured = false;
        if (_battleResultOverlay != null)
        {
            _battleResultOverlay.Visible = false;
        }
        ClearHighlightDepthVisuals();
        ClearOccludedUnitSilhouettes();
        ClearFireVisuals();
        PrepareBattlePieceMarkersForLoad();
        ClearCastleDepthVisuals();
        ClearBuildingDepthVisuals();

        _battleDepthEntries.Clear();
        _occupantsByGrid.Clear();
        _wallTopAttackAmmoByMarker.Clear();
        _activeFireByGrid.Clear();
        _strategyUsedByMarkerThisTurn.Clear();
        _supplyUsedByMarkerThisTurn.Clear();
        _chargeUsedByMarkerThisTurn.Clear();
        _aiBridgePlanByWorker.Clear();
        _battleLogs.Clear();
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _chargeTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _workerWorkAction = WorkerWorkAction.General;
        _selectedUnit = null;
        _selectedUnitGrid = null;
        _selectedGrid = null;
        _selectedGridKey = null;
    }

    private void ClearFireVisuals()
    {
        foreach (var fireRoot in _fireVisualsByGrid.Values)
        {
            _battleDepthEntries.Remove(fireRoot);
            fireRoot.QueueFree();
        }

        _fireVisualsByGrid.Clear();
    }

    private Dictionary<string, Queue<BattlePieceMarker>> CollectReusableBattleMarkersByStableId()
    {
        var markersByStableId = new Dictionary<string, Queue<BattlePieceMarker>>();
        foreach (var occupant in _occupantsByGrid.Values.SelectMany(static occupants => occupants))
        {
            if (occupant.Marker == null || !IsBattlePiece(occupant))
            {
                continue;
            }

            var stableId = BuildBattleOccupantStableId(occupant);
            if (!markersByStableId.TryGetValue(stableId, out var markerQueue))
            {
                markerQueue = new Queue<BattlePieceMarker>();
                markersByStableId[stableId] = markerQueue;
            }

            markerQueue.Enqueue(occupant.Marker);
        }

        return markersByStableId;
    }

    private void PrepareBattlePieceMarkersForLoad()
    {
        foreach (var marker in GetBattlePieceMarkersInUnitLayer().ToList())
        {
            _battleDepthEntries.Remove(marker);
            marker.Visible = false;
        }
    }

    private IEnumerable<BattlePieceMarker> GetBattlePieceMarkersInUnitLayer()
    {
        var unitLayer = GetNodeOrNull<Node2D>("MapRoot/UnitLayer");
        return unitLayer == null
            ? Enumerable.Empty<BattlePieceMarker>()
            : EnumerateBattlePieceMarkers(unitLayer);
    }

    private static IEnumerable<BattlePieceMarker> EnumerateBattlePieceMarkers(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is BattlePieceMarker marker)
            {
                yield return marker;
            }

            foreach (var nestedMarker in EnumerateBattlePieceMarkers(child))
            {
                yield return nestedMarker;
            }
        }
    }

    private void ApplySavedCells(List<BattleCellSaveData>? cells)
    {
        if (_mapData == null || cells == null)
        {
            return;
        }

        foreach (var savedCell in cells)
        {
            if (!IsWithinMap(new Vector2I(savedCell.X, savedCell.Y)))
            {
                continue;
            }

            savedCell.ApplyTo(_mapData.GetCell(savedCell.X, savedCell.Y));
        }
    }

    private void RefreshBattleMapVisualsAfterLoad()
    {
        if (_mapData == null)
        {
            return;
        }

        if (_groundLayer != null)
        {
            BattleTileMapBuilder.ConfigureLayer(_groundLayer, _mapData, BattleTileLayerKind.Ground);
        }

        ConfigureOptionalTileMapLayer(_moatLayer, BattleTileLayerKind.Moat);
        if (_objectLayer != null)
        {
            BattleTileMapBuilder.ConfigureLayer(_objectLayer, _mapData, BattleTileLayerKind.Object);
            _objectLayer.Visible = true;
        }
        RefreshWorkerFenceLayer();

        if (_castleLayer != null)
        {
            BattleTileMapBuilder.ConfigureLayer(_castleLayer, _mapData, BattleTileLayerKind.Castle);
        }

        if (_overlayLayer != null)
        {
            BattleTileMapBuilder.ConfigureLayer(_overlayLayer, _mapData, BattleTileLayerKind.DeploymentOverlay);
        }

        BuildCastleDepthVisuals();
        BuildBuildingDepthVisuals();
    }

    private BattlePieceMarker? CreateSavedBattleMarker(BattleOccupantSaveData saveData, Dictionary<string, Queue<BattlePieceMarker>> reusableMarkersByStableId)
    {
        var unitLayer = GetNodeOrNull<Node2D>("MapRoot/UnitLayer");
        if (unitLayer == null)
        {
            return null;
        }

        var grid = new BattleGridKey(saveData.X, saveData.Y, saveData.Level);
        var marker = TryTakeReusableBattleMarker(saveData, reusableMarkersByStableId);
        if (marker == null)
        {
            marker = new BattlePieceMarker
            {
                Name = $"Saved_{saveData.ShortLabel}_{saveData.X}_{saveData.Y}_L{saveData.Level}"
            };
            unitLayer.AddChild(marker);
        }

        marker.Position = GetMarkerPosition(grid);
        marker.Visible = true;

        var occupant = saveData.ToOccupant(marker);
        marker.Setup(
            saveData.ShortLabel,
            GetSavedMarkerFillColor(occupant),
            GetSavedMarkerBorderColor(occupant),
            saveData.Category == CategorySiegeEngine ? 21.0f : 19.0f);
        marker.SetupNamePlate(FormatMarkerName(saveData.OfficerName, saveData.DisplayName, saveData.TroopType));
        marker.SetupTeamArrow(GetTeamArrowColor(saveData.TeamName));
        marker.SetupSpriteAnimationScene(GetIdleSceneForOccupant(occupant));
        UpdateMarkerStrengthBar(occupant);
        UpdateMarkerStatusIndicator(occupant);

        _occupantsByGrid.Add(grid, occupant);
        RegisterBattleDepthEntry(marker, grid, saveData.Category == CategorySiegeEngine ? BattleDepthRenderKind.SiegeEngine : BattleDepthRenderKind.Unit);
        return marker;
    }

    private static BattlePieceMarker? TryTakeReusableBattleMarker(BattleOccupantSaveData saveData, Dictionary<string, Queue<BattlePieceMarker>> reusableMarkersByStableId)
    {
        var stableId = BuildBattleOccupantStableId(saveData);
        if (!reusableMarkersByStableId.TryGetValue(stableId, out var markerQueue) || markerQueue.Count == 0)
        {
            return null;
        }

        return markerQueue.Dequeue();
    }

    private static Color GetSavedMarkerFillColor(BattleOccupantInfo occupant)
    {
        if (BattleTeamIdentity.IsDefender(occupant.TeamName))
        {
            return occupant.Category == CategorySiegeEngine ? new Color("31576f") : new Color("2d668d");
        }

        return occupant.Category == CategorySiegeEngine ? new Color("725137") : new Color("9d4d32");
    }

    private static Color GetSavedMarkerBorderColor(BattleOccupantInfo occupant)
    {
        return BattleTeamIdentity.IsDefender(occupant.TeamName)
            ? new Color("e0f0ff")
            : new Color("f0d6a8");
    }

    private static string GetIdleSceneForOccupant(BattleOccupantInfo occupant)
    {
        return occupant.TroopType switch
        {
            TroopInfantry => GetInfantryIdleScene(occupant.FacingDirection),
            TroopSpearman => GetSpearmanIdleScene(occupant.FacingDirection),
            TroopArcher or TroopCrossbow => GetArcherIdleScene(occupant.FacingDirection),
            TroopCavalry => GetCavalryIdleScene(occupant.FacingDirection),
            TroopWorker => GetWorkerIdleScene(occupant.FacingDirection),
            TroopRam => GetCarIdleScene(occupant.FacingDirection),
            TroopLadder => GetCarLadderIdleScene(occupant.FacingDirection),
            TroopCatapult => GetCatapultIdleScene(occupant.FacingDirection),
            TroopSupplyCart => GetSupplyCarIdleScene(occupant.FacingDirection),
            _ => GetInfantryIdleScene(occupant.FacingDirection)
        };
    }

    private void RecalculateBattleHudTotals()
    {
        _teamATotalTroops = 0;
        _teamBTotalTroops = 0;
        _teamASiegeUnits = 0;
        _teamBSiegeUnits = 0;
        _teamAGenerals = 0;
        _teamBGenerals = 0;

        foreach (var occupant in _occupantsByGrid.Values.SelectMany(static occupants => occupants))
        {
            ApplyTeamTroopDelta(occupant.Category, occupant.TeamName, occupant.TroopCount);
            ApplyTeamSiegeUnitDelta(occupant.Category, occupant.TeamName, 1);
            ApplyTeamGeneralDelta(occupant.Category, occupant.TeamName, occupant.OfficerName, 1);
        }
    }

    private void RestoreBattleTurnUsage(BattleSaveData saveData, Dictionary<string, BattlePieceMarker> markersByUnitId)
    {
        foreach (var unitId in saveData.StrategyUsedUnitIds ?? new List<string>())
        {
            if (markersByUnitId.TryGetValue(unitId, out var marker))
            {
                _strategyUsedByMarkerThisTurn.Add(marker);
            }
        }

        foreach (var unitId in saveData.SupplyUsedUnitIds ?? new List<string>())
        {
            if (markersByUnitId.TryGetValue(unitId, out var marker))
            {
                _supplyUsedByMarkerThisTurn.Add(marker);
            }
        }

        foreach (var unitId in saveData.ChargeUsedUnitIds ?? new List<string>())
        {
            if (markersByUnitId.TryGetValue(unitId, out var marker))
            {
                _chargeUsedByMarkerThisTurn.Add(marker);
            }
        }

        foreach (var ammo in saveData.WallTopAmmo ?? new List<BattleWallTopAmmoSaveData>())
        {
            if (markersByUnitId.TryGetValue(ammo.UnitId, out var marker))
            {
                _wallTopAttackAmmoByMarker[marker] = new WallTopAttackAmmo(ammo.DropStoneUses, ammo.PourOilUses);
            }
        }
    }

    private void RestoreBattleLogs(List<BattleLogSaveData>? logs)
    {
        _battleLogs.Clear();
        if (logs == null)
        {
            return;
        }

        foreach (var log in logs)
        {
            _battleLogs.Add(new BattleLogEntry(log.Turn, log.TeamName, log.Category, log.Message));
        }
    }

    private void RestoreBattleFires(List<BattleFireSaveData>? fires)
    {
        _activeFireByGrid.Clear();
        if (fires == null)
        {
            return;
        }

        foreach (var fire in fires)
        {
            var grid = new BattleGridKey(fire.X, fire.Y, fire.Level);
            if (!IsWithinMap(grid.Grid))
            {
                continue;
            }

            _activeFireByGrid[grid] = new BattleFireState(fire.RemainingTurns, fire.BurnTurns);
            RefreshFireVisual(grid);
        }
    }

    private sealed class BattleSaveData
    {
        public int Version { get; set; } = 1;
        public BattleScenarioType ScenarioType { get; set; }
        public bool UseEditorAuthoredLayout { get; set; }
        public int TurnNumber { get; set; }
        public BattleTurnSide CurrentTurnSide { get; set; }
        public int BattleDateYear { get; set; }
        public int BattleDateMonth { get; set; }
        public int BattleDateDay { get; set; }
        public BattleTimeOfDay CurrentBattleTimeOfDay { get; set; }
        public BattleWeatherType CurrentBattleWeather { get; set; }
        public BattleWindDirection CurrentBattleWindDirection { get; set; }
        public BattleWindPower CurrentBattleWindPower { get; set; }
        public int TeamAStrategyPlans { get; set; }
        public int TeamBStrategyPlans { get; set; }
        public int TeamAGold { get; set; } = InitialTeamAGold;
        public int TeamAFood { get; set; } = InitialTeamAFood;
        public int TeamAZeroFoodDays { get; set; }
        public int TeamBGold { get; set; } = InitialTeamBGold;
        public int TeamBFood { get; set; } = InitialTeamBFood;
        public int TeamBZeroFoodDays { get; set; }
        public bool ShowSelfTeamLogOnly { get; set; }
        public float BattleLogExpandedWidth { get; set; }
        public float BattleLogExpandedHeight { get; set; }
        public bool IsBattleLogMinimized { get; set; }
        public bool AttackerOutpostVictorySecured { get; set; }
        public List<BattleCellSaveData> Cells { get; set; } = new();
        public List<BattleOccupantSaveData> Occupants { get; set; } = new();
        public List<string> StrategyUsedUnitIds { get; set; } = new();
        public List<string> SupplyUsedUnitIds { get; set; } = new();
        public List<string> ChargeUsedUnitIds { get; set; } = new();
        public List<BattleWallTopAmmoSaveData> WallTopAmmo { get; set; } = new();
        public List<BattleFireSaveData> ActiveFires { get; set; } = new();
        public List<BattleLogSaveData> Logs { get; set; } = new();
    }

    private sealed class BattleCellSaveData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public BattleTerrainType Terrain { get; set; }
        public bool HasGroundAtlasVisual { get; set; }
        public int GroundAtlasX { get; set; }
        public int GroundAtlasY { get; set; }
        public BattleStructureType Structure { get; set; }
        public BattleDeploymentZone DeploymentZone { get; set; }
        public bool BlocksMovement { get; set; }
        public bool IsGateOpen { get; set; }
        public int HeightLevel { get; set; }
        public int StructureMaxHealth { get; set; }
        public int StructureHealth { get; set; }
        public BattleStructureFacing StructureFacing { get; set; }
        public BattleGateSegment GateSegment { get; set; }
        public int CastleSourceId { get; set; }
        public int CastleAtlasX { get; set; }
        public int CastleAtlasY { get; set; }
        public int CastleAlternativeTile { get; set; }
        public bool HideGroundOccupantWithForeground { get; set; }
        public bool HideGroundOccupantWhenGateOpen { get; set; }
        public bool HasBridgeVisual { get; set; }
        public bool BridgeFlipHorizontally { get; set; }
        public int BridgeAtlasSourceId { get; set; }
        public int BridgeAtlasX { get; set; }
        public int BridgeAtlasY { get; set; }
        public bool WoodenFenceFlipHorizontally { get; set; }
        public int BuildingAtlasX { get; set; }
        public int BuildingAtlasY { get; set; }
        public bool IsDefenseOutpost { get; set; }
        public int DefenseOutpostAtlasIndex { get; set; }
        public BattleOutpostOwner DefenseOutpostOwner { get; set; }
        public bool HasForestObjectVisual { get; set; }
        public int ForestAtlasSourceId { get; set; }
        public int ForestAtlasX { get; set; }
        public int ForestAtlasY { get; set; }
        public bool HasSwampObjectVisual { get; set; }
        public int SwampAtlasX { get; set; }
        public int SwampAtlasY { get; set; }
        public bool HasHillObjectVisual { get; set; }
        public int HillAtlasX { get; set; }
        public int HillAtlasY { get; set; }
        public bool HasMountainObjectVisual { get; set; }
        public int MountainAtlasX { get; set; }
        public int MountainAtlasY { get; set; }
        public bool HasFarmObjectVisual { get; set; }
        public int FarmAtlasX { get; set; }
        public int FarmAtlasY { get; set; }
        public int BridgeMaxHealth { get; set; }
        public int BridgeHealth { get; set; }
        public bool IsWoodenBridge { get; set; }
        public bool IsBridgeUnderConstruction { get; set; }
        public bool BridgeRestoresToRiver { get; set; }

        public static BattleCellSaveData FromCell(BattleCellData cell)
        {
            return new BattleCellSaveData
            {
                X = cell.Grid.X,
                Y = cell.Grid.Y,
                Terrain = cell.Terrain,
                HasGroundAtlasVisual = cell.GroundAtlasCoords.X >= 0,
                GroundAtlasX = cell.GroundAtlasCoords.X,
                GroundAtlasY = cell.GroundAtlasCoords.Y,
                Structure = cell.Structure,
                DeploymentZone = cell.DeploymentZone,
                BlocksMovement = cell.BlocksMovement,
                IsGateOpen = cell.IsGateOpen,
                HeightLevel = cell.HeightLevel,
                StructureMaxHealth = cell.StructureMaxHealth,
                StructureHealth = cell.StructureHealth,
                StructureFacing = cell.StructureFacing,
                GateSegment = cell.GateSegment,
                CastleSourceId = cell.CastleSourceId,
                CastleAtlasX = cell.CastleAtlasCoords.X,
                CastleAtlasY = cell.CastleAtlasCoords.Y,
                CastleAlternativeTile = cell.CastleAlternativeTile,
                HideGroundOccupantWithForeground = cell.HideGroundOccupantWithForeground,
                HideGroundOccupantWhenGateOpen = cell.HideGroundOccupantWhenGateOpen,
                HasBridgeVisual = cell.HasBridgeVisual,
                BridgeFlipHorizontally = cell.BridgeFlipHorizontally,
                BridgeAtlasSourceId = cell.BridgeAtlasSourceId,
                BridgeAtlasX = cell.BridgeAtlasCoords.X,
                BridgeAtlasY = cell.BridgeAtlasCoords.Y,
                WoodenFenceFlipHorizontally = cell.WoodenFenceFlipHorizontally,
                BuildingAtlasX = cell.BuildingAtlasCoords.X,
                BuildingAtlasY = cell.BuildingAtlasCoords.Y,
                IsDefenseOutpost = cell.IsDefenseOutpost,
                DefenseOutpostAtlasIndex = cell.DefenseOutpostAtlasIndex,
                DefenseOutpostOwner = cell.DefenseOutpostOwner,
                HasForestObjectVisual = cell.ForestAtlasCoords.X >= 0,
                ForestAtlasSourceId = cell.ForestAtlasSourceId,
                ForestAtlasX = cell.ForestAtlasCoords.X,
                ForestAtlasY = cell.ForestAtlasCoords.Y,
                HasSwampObjectVisual = cell.SwampAtlasCoords.X >= 0,
                SwampAtlasX = cell.SwampAtlasCoords.X,
                SwampAtlasY = cell.SwampAtlasCoords.Y,
                HasHillObjectVisual = cell.HillAtlasCoords.X >= 0,
                HillAtlasX = cell.HillAtlasCoords.X,
                HillAtlasY = cell.HillAtlasCoords.Y,
                HasMountainObjectVisual = cell.MountainAtlasCoords.X >= 0,
                MountainAtlasX = cell.MountainAtlasCoords.X,
                MountainAtlasY = cell.MountainAtlasCoords.Y,
                HasFarmObjectVisual = cell.FarmAtlasCoords.X >= 0,
                FarmAtlasX = cell.FarmAtlasCoords.X,
                FarmAtlasY = cell.FarmAtlasCoords.Y,
                BridgeMaxHealth = cell.BridgeMaxHealth,
                BridgeHealth = cell.BridgeHealth,
                IsWoodenBridge = cell.IsWoodenBridge,
                IsBridgeUnderConstruction = cell.IsBridgeUnderConstruction,
                BridgeRestoresToRiver = cell.BridgeRestoresToRiver
            };
        }

        public void ApplyTo(BattleCellData cell)
        {
            cell.Terrain = Terrain;
            cell.GroundAtlasCoords = HasGroundAtlasVisual
                ? new Vector2I(GroundAtlasX, GroundAtlasY)
                : new Vector2I(-1, -1);
            cell.Structure = Structure;
            cell.DeploymentZone = DeploymentZone;
            cell.BlocksMovement = BlocksMovement;
            cell.IsGateOpen = IsGateOpen;
            cell.HeightLevel = HeightLevel;
            cell.StructureMaxHealth = StructureMaxHealth;
            cell.StructureHealth = StructureHealth;
            cell.StructureFacing = StructureFacing;
            cell.GateSegment = GateSegment;
            cell.CastleSourceId = CastleSourceId;
            cell.CastleAtlasCoords = new Vector2I(CastleAtlasX, CastleAtlasY);
            cell.CastleAlternativeTile = CastleAlternativeTile;
            cell.HideGroundOccupantWithForeground = HideGroundOccupantWithForeground;
            cell.HideGroundOccupantWhenGateOpen = HideGroundOccupantWhenGateOpen;
            cell.HasBridgeVisual = HasBridgeVisual;
            cell.BridgeFlipHorizontally = BridgeFlipHorizontally;
            cell.BridgeAtlasSourceId = BridgeAtlasSourceId;
            cell.BridgeAtlasCoords = new Vector2I(BridgeAtlasX, BridgeAtlasY);
            cell.WoodenFenceFlipHorizontally = WoodenFenceFlipHorizontally;
            cell.BuildingAtlasCoords = new Vector2I(BuildingAtlasX, BuildingAtlasY);
            cell.IsDefenseOutpost = IsDefenseOutpost;
            cell.DefenseOutpostAtlasIndex = DefenseOutpostAtlasIndex;
            cell.DefenseOutpostOwner = DefenseOutpostOwner;
            cell.ForestAtlasCoords = HasForestObjectVisual
                ? new Vector2I(ForestAtlasX, ForestAtlasY)
                : new Vector2I(-1, -1);
            cell.ForestAtlasSourceId = HasForestObjectVisual
                ? ForestAtlasSourceId == 6 ? 6 : 2
                : -1;
            cell.SwampAtlasCoords = HasSwampObjectVisual
                ? new Vector2I(SwampAtlasX, SwampAtlasY)
                : new Vector2I(-1, -1);
            cell.HillAtlasCoords = HasHillObjectVisual
                ? new Vector2I(HillAtlasX, HillAtlasY)
                : new Vector2I(-1, -1);
            cell.MountainAtlasCoords = HasMountainObjectVisual
                ? new Vector2I(MountainAtlasX, MountainAtlasY)
                : new Vector2I(-1, -1);
            cell.FarmAtlasCoords = HasFarmObjectVisual
                ? new Vector2I(FarmAtlasX, FarmAtlasY)
                : new Vector2I(-1, -1);
            cell.BridgeMaxHealth = BridgeMaxHealth;
            cell.BridgeHealth = BridgeHealth;
            cell.IsWoodenBridge = IsWoodenBridge;
            cell.IsBridgeUnderConstruction = IsBridgeUnderConstruction;
            cell.BridgeRestoresToRiver = BridgeRestoresToRiver;
            if (cell.HasBridgeHealth)
            {
                cell.BlocksMovement = cell.IsBridgeUnderConstruction;
            }
        }
    }

    private sealed class BattleOccupantSaveData
    {
        public string UnitId { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Level { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ShortLabel { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string OfficerName { get; set; } = string.Empty;
        public string TroopType { get; set; } = string.Empty;
        public int TroopCount { get; set; }
        public int HitPoints { get; set; }
        public int MaxHitPoints { get; set; }
        public int WoundedTroops { get; set; }
        public int MessTurns { get; set; }
        public bool IsHidden { get; set; }
        public int? Morale { get; set; }
        public int? WeaponAmmo { get; set; }
        public int? MaxWeaponAmmo { get; set; }
        public int MoveRange { get; set; }
        public int AttackRange { get; set; }
        public int? Energy { get; set; }
        public bool HasAttackedThisTurn { get; set; }
        public int? RemainingMoveRange { get; set; }
        public bool IsGuarding { get; set; }
        public bool GuardCounterAvailable { get; set; }
        public int GuardDamageReductionCount { get; set; }
        public BattleSpriteDirection FacingDirection { get; set; }

        public static BattleOccupantSaveData FromOccupant(BattleGridKey grid, BattleOccupantInfo occupant)
        {
            var saveData = new BattleOccupantSaveData
            {
                X = grid.X,
                Y = grid.Y,
                Level = grid.Level,
                DisplayName = occupant.DisplayName,
                Category = occupant.Category,
                ShortLabel = occupant.ShortLabel,
                TeamName = occupant.TeamName,
                OfficerName = occupant.OfficerName,
                TroopType = occupant.TroopType,
                TroopCount = occupant.TroopCount,
                HitPoints = occupant.HitPoints,
                MaxHitPoints = occupant.MaxHitPoints,
                WoundedTroops = occupant.WoundedTroops,
                MessTurns = occupant.MessTurns,
                IsHidden = occupant.IsHidden,
                Morale = occupant.Morale,
                WeaponAmmo = occupant.WeaponAmmo,
                MaxWeaponAmmo = occupant.MaxWeaponAmmo,
                MoveRange = occupant.MoveRange,
                AttackRange = occupant.AttackRange,
                Energy = occupant.Energy,
                HasAttackedThisTurn = occupant.HasAttackedThisTurn,
                RemainingMoveRange = occupant.RemainingMoveRange,
                IsGuarding = occupant.IsGuarding,
                GuardCounterAvailable = occupant.GuardCounterAvailable,
                GuardDamageReductionCount = occupant.GuardDamageReductionCount,
                FacingDirection = occupant.FacingDirection
            };
            saveData.UnitId = BuildUnitId(saveData);
            return saveData;
        }

        public BattleOccupantInfo ToOccupant(BattlePieceMarker marker)
        {
            UnitId = string.IsNullOrWhiteSpace(UnitId) ? BuildUnitId(this) : UnitId;
            return new BattleOccupantInfo(
                DisplayName,
                Category,
                ShortLabel,
                TeamName,
                OfficerName,
                TroopType,
                TroopCount,
                HitPoints,
                MaxHitPoints,
                WoundedTroops,
                MessTurns,
                IsHidden,
                Morale,
                WeaponAmmo,
                MaxWeaponAmmo,
                MoveRange,
                AttackRange,
                marker,
                FacingDirection,
                Energy ?? DefaultUnitEnergy,
                HasAttackedThisTurn,
                RemainingMoveRange ?? MoveRange,
                IsGuarding,
                GuardCounterAvailable,
                GuardDamageReductionCount);
        }

        private static string BuildUnitId(BattleOccupantSaveData saveData)
        {
            return string.Join("|", saveData.TeamName, saveData.DisplayName, saveData.OfficerName, saveData.TroopType, saveData.ShortLabel, saveData.Category);
        }
    }

    private sealed class BattleWallTopAmmoSaveData
    {
        public string UnitId { get; set; } = string.Empty;
        public int DropStoneUses { get; set; }
        public int PourOilUses { get; set; }
    }

    private sealed class BattleFireSaveData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Level { get; set; }
        public int RemainingTurns { get; set; }
        public int BurnTurns { get; set; }
    }

    private sealed class BattleLogSaveData
    {
        public int Turn { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
