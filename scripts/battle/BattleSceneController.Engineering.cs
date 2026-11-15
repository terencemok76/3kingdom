using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using static ThreeKingdom.Battle.BattleAiSettings;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattlePresentationSettings;
using static ThreeKingdom.Battle.BattleResourcePaths;
using static ThreeKingdom.Battle.BattleUnitTypes;
using static ThreeKingdom.Battle.BattleUnitVisualCatalog;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private bool TryPerformWork()
    {
        if (_mapData == null ||
            _selectedUnit == null ||
            IsMessed(_selectedUnit) ||
            !_selectedUnitGrid.HasValue ||
            !_selectedGrid.HasValue)
        {
            return false;
        }

        var targetGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        if (targetGrid.Level != 0 || !_workableGrids.Contains(targetGrid))
        {
            return false;
        }

        var sourceGrid = _selectedUnitGrid.Value;
        var targetCell = _mapData.GetCell(targetGrid.X, targetGrid.Y);
        var workEnergyCost = GetWorkEnergyCost(_selectedUnit, targetCell, _workerWorkAction);
        if (_selectedUnit.Energy < workEnergyCost)
        {
            return false;
        }

        var isWorker = _selectedUnit.TroopType == TroopWorker;
        var engineerWorkSfxPaths = GetEngineerWorkSfxPaths(_selectedUnit, targetGrid.Grid, targetCell);
        if (!ApplyWork(_selectedUnit, targetGrid.Grid, targetCell, out var removedWoodFence))
        {
            return false;
        }

        var workDirection = GetInfantryDirection(sourceGrid.Grid, targetGrid.Grid);
        var workingUnit = _selectedUnit with
        {
            FacingDirection = workDirection,
            Energy = _selectedUnit.Energy - workEnergyCost,
            HasAttackedThisTurn = _selectedUnit.HasAttackedThisTurn || !isWorker
        };
        ReplaceOccupantAtGrid(sourceGrid, _selectedUnit, workingUnit);
        _selectedUnit = workingUnit;
        if (isWorker)
        {
            if (engineerWorkSfxPaths != null)
            {
                GameAudioController.Instance?.PlayGameplaySfx(engineerWorkSfxPaths);
            }

            workingUnit.Marker?.PlayAction(
                GetWorkerWorkScene(workDirection),
                GetWorkerIdleScene(workDirection),
                WorkerWorkAnimationDurationSeconds);
        }

        var workDescription = isWorker
            ? FormatWorkerWorkAction(_workerWorkAction, targetCell, removedWoodFence)
            : targetCell.Structure == BattleStructureType.Gate
                ? BattleText("log.work.repair_gate", "repairs gate")
                : BattleText("log.work.bridge", "works on bridge");
        AppendBattleLog(workingUnit, "Action", $"{FormatLogUnit(workingUnit)} {workDescription} at {targetGrid} (energy {workEnergyCost})");

        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        MarkUnitActed(workingUnit);
        return true;
    }

    private string[]? GetEngineerWorkSfxPaths(BattleOccupantInfo unit, Vector2I targetGrid, BattleCellData targetCell)
    {
        if (unit.TroopType != TroopWorker)
        {
            return null;
        }

        if (_workerWorkAction == WorkerWorkAction.WoodFence)
        {
            return targetCell.Structure == BattleStructureType.WoodenFence
                ? EngineerDestroySfxPaths
                : EngineerBuildSfxPaths;
        }

        if ((targetCell.Terrain == BattleTerrainType.Moat &&
             _mapData?.ScenarioDefinition.ScenarioType == BattleScenarioType.MoatSiegeBattle) ||
            (targetCell.Terrain == BattleTerrainType.River &&
             _mapData?.ScenarioDefinition.ScenarioType == BattleScenarioType.FieldBattle))
        {
            return EngineerBuildSfxPaths;
        }

        if (targetCell.IsBridgeDamaged || CanRepairGate(unit, targetGrid, targetCell))
        {
            return EngineerRepairSfxPaths;
        }

        return targetCell.Structure == BattleStructureType.Trap
            ? EngineerDestroySfxPaths
            : null;
    }

    private static int GetWorkerWorkEnergyCost(BattleCellData targetCell, WorkerWorkAction workAction)
    {
        if (workAction != WorkerWorkAction.WoodFence)
        {
            return 0;
        }

        return targetCell.Structure == BattleStructureType.WoodenFence
            ? WorkerRemoveWoodFenceEnergyCost
            : WorkerInstallWoodFenceEnergyCost;
    }

    private static int GetWorkEnergyCost(BattleOccupantInfo unit, BattleCellData targetCell, WorkerWorkAction workAction)
    {
        return unit.TroopType == TroopWorker
            ? GetWorkerWorkEnergyCost(targetCell, workAction)
            : BattleBridgeSystem.EmergencyRepairEnergyCost;
    }

    private bool ApplyWork(BattleOccupantInfo unit, Vector2I targetGrid, BattleCellData targetCell, out bool removedWoodFence)
    {
        if (unit.TroopType == TroopWorker)
        {
            return ApplyWorkerWork(unit, targetGrid, targetCell, out removedWoodFence);
        }

        removedWoodFence = false;
        if (!BattleBridgeSystem.CanEmergencyRepair(unit))
        {
            return false;
        }

        if (CanRepairGate(unit, targetGrid, targetCell))
        {
            return RepairGate(targetGrid, BattleBridgeSystem.EmergencyGateRepairAmount) > 0;
        }

        if (!BattleBridgeSystem.IsEmergencyRepairTarget(targetCell))
        {
            return false;
        }

        var repairedHp = BattleBridgeSystem.ApplyRepair(targetCell, BattleBridgeSystem.EmergencyRepairAmount);
        if (repairedHp <= 0)
        {
            return false;
        }

        ShowRepairPopup(GetDefaultGridKey(targetGrid), repairedHp);
        return true;
    }

    private bool ApplyWorkerWork(BattleOccupantInfo unit, Vector2I targetGrid, BattleCellData targetCell, out bool removedWoodFence)
    {
        removedWoodFence = false;
        if (_mapData == null)
        {
            return false;
        }

        if (_workerWorkAction == WorkerWorkAction.WoodFence)
        {
            if (targetCell.Structure == BattleStructureType.WoodenFence)
            {
                targetCell.Structure = BattleStructureType.None;
                targetCell.BlocksMovement = false;
                targetCell.WoodenFenceFlipHorizontally = false;
                removedWoodFence = true;
                RefreshWorkerObjectLayers();
                return true;
            }

            if (!CanInstallWoodFence(targetGrid, targetCell))
            {
                return false;
            }

            targetCell.Structure = BattleStructureType.WoodenFence;
            targetCell.StructureMaxHealth = BattleCellData.WoodenFenceMaxHealth;
            targetCell.StructureHealth = targetCell.StructureMaxHealth;
            targetCell.BlocksMovement = true;
            targetCell.WoodenFenceFlipHorizontally = GD.Randf() >= 0.5f;
            RefreshWorkerObjectLayers();
            return true;
        }

        if ((targetCell.Terrain == BattleTerrainType.Moat &&
             _mapData.ScenarioDefinition.ScenarioType == BattleScenarioType.MoatSiegeBattle) ||
            (targetCell.Terrain == BattleTerrainType.River &&
             _mapData.ScenarioDefinition.ScenarioType == BattleScenarioType.FieldBattle))
        {
            var isWoodenBridge = targetCell.Terrain == BattleTerrainType.River;
            targetCell.Terrain = BattleTerrainType.Bridge;
            targetCell.HasBridgeVisual = true;
            targetCell.BridgeFlipHorizontally = _mapData.ScenarioDefinition.DefaultStructureFacing == BattleStructureFacing.NorthWest;
            targetCell.BridgeAtlasSourceId = 8;
            targetCell.BridgeAtlasCoords = new Vector2I(0, 0);
            targetCell.IsWoodenBridge = isWoodenBridge;
            targetCell.IsBridgeUnderConstruction = true;
            targetCell.BridgeRestoresToRiver = isWoodenBridge;
            targetCell.BridgeMaxHealth = isWoodenBridge ? BattleCellData.WoodenBridgeMaxDurability : BattleCellData.BridgeMaxDurability;
            targetCell.BridgeHealth = isWoodenBridge ? BattleCellData.WoodenBridgeConstructionStep : BattleCellData.BridgeConstructionStep;
            targetCell.BlocksMovement = true;
            RefreshWorkerObjectLayers();
            return true;
        }

        if (targetCell.IsBridgeDamaged)
        {
            var repairedHp = BattleBridgeSystem.ApplyRepair(targetCell, WorkerBridgeRepairAmount);
            if (repairedHp > 0)
            {
                ShowRepairPopup(GetDefaultGridKey(targetGrid), repairedHp);
                return true;
            }
        }

        if (CanRepairGate(unit, targetGrid, targetCell))
        {
            return RepairGate(targetGrid, WorkerGateRepairAmount) > 0;
        }

        if (targetCell.Structure == BattleStructureType.Trap)
        {
            targetCell.Structure = BattleStructureType.None;
            targetCell.BlocksMovement = false;
            targetCell.StructureMaxHealth = 0;
            targetCell.StructureHealth = 0;
            RefreshWorkerObjectLayers();
            return true;
        }

        return false;
    }

    private bool CanRepairGate(BattleOccupantInfo unit, Vector2I targetGrid, BattleCellData targetCell)
    {
        return IsDefenderPiece(unit) &&
               targetCell.Structure == BattleStructureType.Gate &&
               targetCell.HasStructureHealth &&
               !targetCell.IsBroken &&
               targetCell.StructureHealth < targetCell.StructureMaxHealth &&
               (!targetCell.IsGateOpen || !HasFriendlyUnitOutsideCity(unit.TeamName));
    }

    private bool HasFriendlyUnitOutsideCity(string teamName)
    {
        return GetAllBattlePieces().Any(entry =>
            entry.Occupant.TeamName == teamName &&
            entry.Grid.Level == 0 &&
            !IsInsideCityGroundGrid(entry.Grid.Grid));
    }

    private int RepairGate(Vector2I targetGrid, int repairAmount)
    {
        if (_mapData == null || repairAmount <= 0)
        {
            return 0;
        }

        var repairedHp = 0;
        foreach (var gateGrid in GetConnectedGateGroup(targetGrid))
        {
            var gateCell = _mapData.GetCell(gateGrid.X, gateGrid.Y);
            var actualRepair = Math.Min(repairAmount, gateCell.StructureMaxHealth - gateCell.StructureHealth);
            if (actualRepair <= 0)
            {
                continue;
            }

            gateCell.StructureHealth += actualRepair;
            gateCell.IsGateOpen = false;
            gateCell.BlocksMovement = true;
            repairedHp = Math.Max(repairedHp, actualRepair);
            if (_castleLayer != null)
            {
                BattleTileMapBuilder.SetCastleGateVisual(_castleLayer, gateGrid, isOpen: false);
            }

            RefreshCastleDepthVisual(gateGrid);
        }

        if (repairedHp <= 0)
        {
            return 0;
        }

        ShowRepairPopup(GetDefaultGridKey(targetGrid), repairedHp);
        RefreshBattleDepthLayerOrder();
        RefreshOccludedUnitSilhouettes();
        return repairedHp;
    }

    private void RefreshWorkerObjectLayers()
    {
        if (_mapData == null)
        {
            return;
        }

        ConfigureOptionalTileMapLayer(_moatLayer, BattleTileLayerKind.Moat);
        if (_objectLayer != null)
        {
            BattleTileMapBuilder.ConfigureLayer(_objectLayer, _mapData, BattleTileLayerKind.Object);
            _objectLayer.Visible = true;
        }
        RefreshWorkerFenceLayer();
    }

    private void RefreshWorkerFenceLayer()
    {
        if (_workerFenceLayer != null && _mapData != null)
        {
            BattleTileMapBuilder.ConfigureWorkerFenceLayer(_workerFenceLayer, _mapData);
            _workerFenceLayer.Visible = true;
        }
    }


    private void OnWorkButtonPressed()
    {
        BeginWorkSelection(WorkerWorkAction.General);
    }

    private void OnInstallWoodFenceButtonPressed()
    {
        BeginWorkSelection(WorkerWorkAction.WoodFence);
    }

    private void OnUninstallWoodFenceButtonPressed()
    {
        BeginWorkSelection(WorkerWorkAction.WoodFence);
    }

    private void BeginWorkSelection(WorkerWorkAction workAction)
    {
        if (_selectedUnit == null ||
            IsMessed(_selectedUnit) ||
            !_selectedUnitGrid.HasValue ||
            !CanUseWorkAction(_selectedUnit, workAction))
        {
            return;
        }

        _workerWorkAction = workAction;
        _commandMode = BattleCommandMode.WorkSelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        foreach (var targetGrid in GetOrthogonalNeighbors(_selectedUnitGrid.Value.Grid))
        {
            if (!IsWithinMap(targetGrid))
            {
                continue;
            }

            var targetCell = _mapData?.GetCell(targetGrid.X, targetGrid.Y);
            if (targetCell != null &&
                IsWorkTargetForAction(_selectedUnit, targetGrid, targetCell, workAction) &&
                _selectedUnit.Energy >= GetWorkEnergyCost(_selectedUnit, targetCell, workAction))
            {
                _workableGrids.Add(new BattleGridKey(targetGrid.X, targetGrid.Y, 0));
            }
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private static bool CanUseWorkAction(BattleOccupantInfo unit, WorkerWorkAction workAction)
    {
        return unit.TroopType == TroopWorker ||
               (workAction == WorkerWorkAction.General && BattleBridgeSystem.CanEmergencyRepair(unit));
    }

    private bool IsWorkTargetForAction(
        BattleOccupantInfo unit,
        Vector2I targetGrid,
        BattleCellData cell,
        WorkerWorkAction workAction)
    {
        if (unit.TroopType != TroopWorker)
        {
            return workAction == WorkerWorkAction.General &&
                   BattleBridgeSystem.CanEmergencyRepair(unit) &&
                   (BattleBridgeSystem.IsEmergencyRepairTarget(cell) || CanRepairGate(unit, targetGrid, cell));
        }

        if (workAction == WorkerWorkAction.WoodFence)
        {
            return cell.Structure == BattleStructureType.WoodenFence || CanInstallWoodFence(targetGrid, cell);
        }

        return (cell.Terrain == BattleTerrainType.Moat && _mapData?.ScenarioDefinition.ScenarioType == BattleScenarioType.MoatSiegeBattle) ||
               (cell.Terrain == BattleTerrainType.River && _mapData?.ScenarioDefinition.ScenarioType == BattleScenarioType.FieldBattle) ||
               cell.IsBridgeDamaged ||
               CanRepairGate(unit, targetGrid, cell) ||
               cell.Structure == BattleStructureType.Trap;
    }

    private bool CanInstallWoodFence(Vector2I targetGrid, BattleCellData cell)
    {
        return cell.Structure == BattleStructureType.None &&
               cell.Terrain is not (BattleTerrainType.Moat or BattleTerrainType.Bridge or BattleTerrainType.WallWalk) &&
               !HasBlockingOccupant(new BattleGridKey(targetGrid.X, targetGrid.Y, 0));
    }

    private bool HasWorkTarget(BattleOccupantInfo unit, WorkerWorkAction workAction)
    {
        return _selectedUnitGrid.HasValue &&
               _mapData != null &&
               !IsMessed(unit) &&
               CanUseWorkAction(unit, workAction) &&
               GetOrthogonalNeighbors(_selectedUnitGrid.Value.Grid)
                   .Where(IsWithinMap)
                   .Any(grid =>
                   {
                       var cell = _mapData.GetCell(grid.X, grid.Y);
                       return IsWorkTargetForAction(unit, grid, cell, workAction) &&
                              unit.Energy >= GetWorkEnergyCost(unit, cell, workAction);
                   });
    }

    private void OnOpenGateButtonPressed()
    {
        TryExecuteSelectedBattleAction(BattleActionKind.ToggleGate);
    }

    private void OpenGateGroup(IEnumerable<Vector2I> gateGroup)
    {
        if (_mapData == null)
        {
            return;
        }

        var gates = gateGroup.ToList();
        var openedAnyGate = gates.Any(grid => !_mapData.GetCell(grid.X, grid.Y).IsGateOpen);
        foreach (var groupGateGrid in gates)
        {
            var gateCell = _mapData.GetCell(groupGateGrid.X, groupGateGrid.Y);
            gateCell.IsGateOpen = true;
            if (_castleLayer != null)
            {
                BattleTileMapBuilder.SetCastleGateVisual(_castleLayer, groupGateGrid, isOpen: true);
            }

            RefreshCastleDepthVisual(groupGateGrid);
        }

        if (openedAnyGate)
        {
            GameAudioController.Instance?.PlayGameplaySfx(GateOpenSfxPaths);
        }
        RefreshOccludedUnitSilhouettes();
    }

    private void ToggleGateGroup(IEnumerable<Vector2I> gateGroup)
    {
        if (_mapData == null)
        {
            return;
        }

        var gates = gateGroup.ToList();
        if (gates.Count == 0)
        {
            return;
        }

        var shouldOpen = gates.Any(grid => !_mapData.GetCell(grid.X, grid.Y).IsGateOpen);
        foreach (var groupGateGrid in gates)
        {
            var gateCell = _mapData.GetCell(groupGateGrid.X, groupGateGrid.Y);
            if (gateCell.IsBroken)
            {
                gateCell.IsGateOpen = true;
            }
            else
            {
                gateCell.IsGateOpen = shouldOpen;
            }

            if (_castleLayer != null)
            {
                BattleTileMapBuilder.SetCastleGateVisual(_castleLayer, groupGateGrid, isOpen: gateCell.IsGateOpen);
            }

            RefreshCastleDepthVisual(groupGateGrid);
        }

        GameAudioController.Instance?.PlayGameplaySfx(shouldOpen ? GateOpenSfxPaths : [GateCloseSfxPath]);
        RefreshOccludedUnitSilhouettes();
    }

    private bool TryGetSwitchableGate(out Vector2I gateGrid)
    {
        gateGrid = default;
        if (_mapData == null ||
            _selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            _selectedUnit.Category != CategoryUnit)
        {
            return false;
        }

        var unitGrid = _selectedUnitGrid.Value;
        if (!IsWithinMap(unitGrid.Grid))
        {
            return false;
        }

        if (CanToggleGateAtGrid(unitGrid, _selectedUnit))
        {
            gateGrid = unitGrid.Grid;
            return true;
        }

        return false;
    }

    private bool CanToggleGateAtGrid(BattleGridKey grid, BattleOccupantInfo unit)
    {
        if (_mapData == null ||
            unit.Category != CategoryUnit ||
            grid.Level != 0 ||
            !IsWithinMap(grid.Grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return cell.Structure == BattleStructureType.Gate && !cell.IsBroken;
    }

    private List<Vector2I> GetConnectedGateGroup(Vector2I startGateGrid)
    {
        var group = new List<Vector2I>();
        if (_mapData == null || !IsWithinMap(startGateGrid))
        {
            return group;
        }

        var startCell = _mapData.GetCell(startGateGrid.X, startGateGrid.Y);
        if (startCell.Structure != BattleStructureType.Gate)
        {
            return group;
        }

        var visited = new HashSet<Vector2I> { startGateGrid };
        var frontier = new Queue<Vector2I>();
        frontier.Enqueue(startGateGrid);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            group.Add(current);

            foreach (var neighbor in GetOrthogonalNeighbors(current))
            {
                if (!IsWithinMap(neighbor) || !visited.Add(neighbor))
                {
                    continue;
                }

                var neighborCell = _mapData.GetCell(neighbor.X, neighbor.Y);
                if (neighborCell.Structure != BattleStructureType.Gate)
                {
                    continue;
                }

                frontier.Enqueue(neighbor);
            }
        }

        return group;
    }
}
