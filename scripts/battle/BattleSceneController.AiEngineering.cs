using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static ThreeKingdom.Battle.BattleAiSettings;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private IEnumerable<AiBridgeRepairPlan> GetAiBridgeRepairPlans(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        if (_mapData == null ||
            !BattleBridgeSystem.CanEmergencyRepair(unit) ||
            unit.Energy < BattleBridgeSystem.EmergencyRepairEnergyCost ||
            unit.HasAttackedThisTurn ||
            IsMessed(unit))
        {
            yield break;
        }

        foreach (var target in GetOrthogonalNeighbors(sourceGrid.Grid).Where(IsWithinMap))
        {
            var cell = _mapData.GetCell(target.X, target.Y);
            if (!BattleBridgeSystem.IsEmergencyRepairTarget(cell))
            {
                continue;
            }

            var repairAmount = Math.Min(
                BattleBridgeSystem.EmergencyRepairAmount,
                cell.BridgeMaxHealth - cell.BridgeHealth);
            var nearbyFriendlyUnits = GetAllBattlePieces()
                .Count(entry => entry.Occupant.TeamName == unit.TeamName &&
                                GetManhattanDistance(entry.Grid.Grid, target) <= 4);
            var decision = BattleAiEngineeringPlanner.BridgeRepair(cell.BridgeHealth, cell.BridgeMaxHealth,
                repairAmount, BattleBridgeSystem.IsHeavilyDamaged(cell), nearbyFriendlyUnits);
            yield return new AiBridgeRepairPlan(ToGroundGridKey(target), repairAmount, decision.Score, decision.Reason);
        }
    }

    private bool TryExecuteAiBridgeRepair(
        BattleGridKey sourceGrid,
        BattleOccupantInfo unit,
        AiBridgeRepairPlan plan,
        int noise)
    {
        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(
            unit,
            "AI",
            BattleFormat("log.ai.bridge_repair", "Decision: {0} at {1}; repair {2} HP (score {3}, variance {4}).", GetAiBridgeRepairReasonText(plan.Reason), plan.TargetGrid, plan.RepairAmount, plan.Score, noise));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Work, sourceGrid, plan.TargetGrid),
            unit);
    }

    private IEnumerable<AiGateRepairPlan> GetAiGateRepairPlans(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        if (_mapData == null ||
            !IsDefenderPiece(unit) ||
            !CanUseWorkAction(unit, WorkerWorkAction.General) ||
            unit.HasAttackedThisTurn ||
            IsMessed(unit))
        {
            yield break;
        }

        foreach (var target in GetOrthogonalNeighbors(sourceGrid.Grid).Where(IsWithinMap))
        {
            var cell = _mapData.GetCell(target.X, target.Y);
            if (!CanRepairGate(unit, target, cell) ||
                unit.Energy < GetWorkEnergyCost(unit, cell, WorkerWorkAction.General))
            {
                continue;
            }

            var repairAmount = unit.TroopType == TroopWorker
                ? WorkerGateRepairAmount
                : BattleBridgeSystem.EmergencyGateRepairAmount;
            repairAmount = Math.Min(repairAmount, cell.StructureMaxHealth - cell.StructureHealth);
            var threat = GetAiGateDefensePriority(ToGroundGridKey(target));
            var score = BattleAiEngineeringPlanner.GateRepairScore(cell.StructureHealth,
                cell.StructureMaxHealth, repairAmount, threat, unit.TroopType == TroopWorker);
            yield return new AiGateRepairPlan(ToGroundGridKey(target), repairAmount, score);
        }
    }

    private bool TryExecuteAiGateRepair(
        BattleGridKey sourceGrid,
        BattleOccupantInfo unit,
        AiGateRepairPlan plan,
        int noise)
    {
        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(
            unit,
            "AI",
            BattleFormat("log.ai.gate_repair", "Decision: repair and close gate at {0}; restore {1} HP (score {2}, variance {3}).", plan.TargetGrid, plan.RepairAmount, plan.Score, noise));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Work, sourceGrid, plan.TargetGrid),
            unit);
    }

   private int GetAiOutpostObjectiveScore(BattleOccupantInfo unit, BattleGridKey approachGrid, AiOutpostObjective objective)
   {
       var intelligence = GetOfficerTacticalIntelligence(unit.OfficerName);
       return objective.Score + intelligence * 15 - GetManhattanDistance(approachGrid.Grid, objective.Grid.Grid) * 300;
   }
    private string GetAiBridgeRepairReasonText(string reason)
    {
        return reason switch
        {
            "restore bridge above heavy-damage threshold" => BattleText("log.ai.bridge_repair_threshold", "restore bridge above heavy-damage threshold"),
            "prevent critically damaged bridge from collapsing" => BattleText("log.ai.bridge_repair_critical", "prevent critically damaged bridge from collapsing"),
            _ => BattleText("log.ai.bridge_repair_maintain", "maintain damaged bridge route")
        };
    }

    private bool TryGetAiBridgeEngineeringPlan(BattleGridKey sourceGrid, BattleOccupantInfo worker, out AiBridgeEngineeringPlan plan)
    {
        plan = null!;
        if (_mapData == null ||
            _mapData.ScenarioDefinition.ScenarioType is not (BattleScenarioType.FieldBattle or BattleScenarioType.MoatSiegeBattle) ||
            worker.TroopType != TroopWorker ||
            IsMessed(worker))
        {
            return false;
        }

        var previousUnit = _selectedUnit;
        var previousUnitGrid = _selectedUnitGrid;
        _selectedUnit = worker;
        _selectedUnitGrid = sourceGrid;
        try
        {
            if (worker.Marker != null &&
                _aiBridgePlanByWorker.TryGetValue(worker.Marker, out var activePlan))
            {
                var nextWorkGrid = activePlan.Corridor.Where(IsAiBridgeWorkRequired).ToList();
                if (nextWorkGrid.Count > 0 &&
                    TryGetAiWorkerActionGrid(sourceGrid, worker, nextWorkGrid[0], out var actionGrid, out var canWorkNow))
                {
                    plan = activePlan with
                    {
                        WorkGrid = ToGroundGridKey(nextWorkGrid[0]),
                        ActionGrid = actionGrid,
                        CanWorkNow = canWorkNow,
                        Score = activePlan.Score - GetManhattanDistance(sourceGrid.Grid, actionGrid.Grid) * AiBridgeApproachPenalty
                    };
                    _aiBridgePlanByWorker[worker.Marker] = plan;
                    return true;
                }

                _aiBridgePlanByWorker.Remove(worker.Marker);
            }

            var objectives = GetAllBattlePieces()
                .Where(entry => entry.Occupant.TeamName != worker.TeamName && entry.Grid.Level == 0)
                .Select(entry => entry.Grid)
                .Concat(GetAiOutpostObjectives(worker, []).Select(objective => objective.Grid))
                .Distinct()
                .ToList();
            var friendlyOrigins = GetAllBattlePieces()
                .Where(entry => entry.Occupant.TeamName == worker.TeamName && entry.Occupant.TroopType != TroopWorker && entry.Grid.Level == 0)
                .Select(entry => entry.Grid.Grid)
                .ToList();
            if (objectives.Count == 0 || friendlyOrigins.Count == 0)
            {
                return false;
            }

            var candidates = new List<AiBridgeEngineeringPlan>();
            foreach (var corridor in GetAiBridgeConstructionCorridors())
            {
                var workCells = corridor.Where(IsAiBridgeWorkRequired).ToList();
                if (workCells.Count == 0 || !TryGetAiWorkerActionGrid(sourceGrid, worker, workCells[0], out var actionGrid, out var canWorkNow))
                {
                    continue;
                }

                var virtualBridges = corridor.ToHashSet();
                foreach (var objective in objectives)
                {
                    var beforeLength = friendlyOrigins
                        .Select(origin => GetAiStrategicPathLength(origin, objective.Grid, new HashSet<Vector2I>()))
                        .DefaultIfEmpty(int.MaxValue)
                        .Min();
                    var afterLength = friendlyOrigins
                        .Select(origin => GetAiStrategicPathLength(origin, objective.Grid, virtualBridges))
                        .DefaultIfEmpty(int.MaxValue)
                        .Min();
                    if (afterLength == int.MaxValue)
                    {
                        continue;
                    }
                    var congestionCount = GetAiBridgeCongestionCount(corridor, worker.TeamName);
                    if (!BattleAiEngineeringPlanner.ShouldBuildBridge(beforeLength, afterLength, congestionCount))
                    {
                        continue;
                    }

                    var pathReduction = BattleAiEngineeringPlanner.RouteImprovement(beforeLength, afterLength);
                    var approachDistance = GetManhattanDistance(sourceGrid.Grid, actionGrid.Grid);
                    var score = BattleAiEngineeringPlanner.BridgeConstructionScore(pathReduction, congestionCount,
                        corridor.Count, approachDistance, GetAiCommandIntelligence(sourceGrid, worker),
                        GetAiThreatScore(sourceGrid, worker));
                    candidates.Add(new AiBridgeEngineeringPlan(
                        corridor,
                        ToGroundGridKey(workCells[0]),
                        objective,
                        actionGrid,
                        canWorkNow,
                        pathReduction,
                        score));
                }
            }

            var selectedPlan = candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Corridor.Count)
                .ThenBy(candidate => candidate.WorkGrid.Y)
                .ThenBy(candidate => candidate.WorkGrid.X)
                .FirstOrDefault();
            if (selectedPlan == null)
            {
                return false;
            }

            plan = selectedPlan;
            if (worker.Marker != null)
            {
                _aiBridgePlanByWorker[worker.Marker] = plan;
            }
            return true;
        }
        finally
        {
            _selectedUnit = previousUnit;
            _selectedUnitGrid = previousUnitGrid;
        }
    }

    private IEnumerable<List<Vector2I>> GetAiBridgeConstructionCorridors()
    {
        if (_mapData == null)
        {
            yield break;
        }

        var directions = new[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };
        var emitted = new HashSet<string>();
        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var start = new Vector2I(x, y);
                if (!IsAiBridgeWorkRequired(start))
                {
                    continue;
                }

                foreach (var direction in directions)
                {
                    var corridor = new List<Vector2I>();
                    var current = start;
                    while (IsWithinMap(current) && IsAiBridgeWorkRequired(current) && corridor.Count < 4)
                    {
                        corridor.Add(current);
                        current += direction;
                    }

                    if (corridor.Count == 0 || !IsWithinMap(current) || !IsAiStrategicPassable(current, new HashSet<Vector2I>()))
                    {
                        continue;
                    }

                    var key = string.Join("/", corridor.Select(grid => $"{grid.X},{grid.Y}"));
                    if (emitted.Add(key))
                    {
                        yield return corridor;
                    }
                }
            }
        }
    }

    private int GetAiBridgeCongestionCount(IReadOnlyList<Vector2I> corridor, string teamName)
    {
        if (_mapData == null || corridor.Count == 0 ||
            corridor.Any(grid => _mapData.GetCell(grid.X, grid.Y).HasBridgeHealth))
        {
            return 0;
        }

        var hasParallelCrossing = Enumerable.Range(0, BattleMapData.Height)
            .SelectMany(y => Enumerable.Range(0, BattleMapData.Width).Select(x => new Vector2I(x, y)))
            .Any(grid =>
            {
                var cell = _mapData.GetCell(grid.X, grid.Y);
                return cell.HasBridgeHealth &&
                       !cell.IsBridgeUnderConstruction &&
                       corridor.Any(workGrid => GetManhattanDistance(grid, workGrid) <= 3);
            });
        if (!hasParallelCrossing)
        {
            return 0;
        }

        return GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName == teamName &&
                            entry.Occupant.TroopType != TroopWorker &&
                            entry.Grid.Level == 0 &&
                            corridor.Any(workGrid => GetManhattanDistance(entry.Grid.Grid, workGrid) <= 5))
            .Select(entry => entry.Occupant.Marker)
            .Where(marker => marker != null)
            .Distinct()
            .Count();
    }

    private bool IsAiBridgeWorkRequired(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        var supportsNewBridge = (_mapData.ScenarioDefinition.ScenarioType == BattleScenarioType.FieldBattle &&
                                 cell.Terrain == BattleTerrainType.River) ||
                                (_mapData.ScenarioDefinition.ScenarioType == BattleScenarioType.MoatSiegeBattle &&
                                 cell.Terrain == BattleTerrainType.Moat);
        return supportsNewBridge || (cell.IsWoodenBridge && cell.IsBridgeDamaged);
    }

    private bool TryGetAiWorkerActionGrid(BattleGridKey sourceGrid, BattleOccupantInfo worker, Vector2I workGrid, out BattleGridKey actionGrid, out bool canWorkNow)
    {
        actionGrid = default;
        canWorkNow = GetOrthogonalNeighbors(sourceGrid.Grid).Contains(workGrid);
        if (canWorkNow)
        {
            actionGrid = sourceGrid;
            return true;
        }

        var destinations = CalculateReachableGrids(sourceGrid, GetAvailableMoveEnergy(worker), GetAvailableMoveRange(worker))
            .Where(grid => grid.Level == 0 && IsAiSafeMovementDestination(grid) && GetOrthogonalNeighbors(grid.Grid).Contains(workGrid))
            .OrderBy(grid => GetManhattanDistance(sourceGrid.Grid, grid.Grid))
            .ToList();
        if (destinations.Count == 0)
        {
            return false;
        }

        actionGrid = destinations[0];
        return true;
    }

    private int GetAiStrategicPathLength(Vector2I source, Vector2I objective, IReadOnlySet<Vector2I> virtualBridges, IReadOnlySet<Vector2I>? virtualBlocks = null)
    {
        return BattleAiStrategicPathfinder.FindLength(source, objective,
            grid => IsAiStrategicPassable(grid, virtualBridges, virtualBlocks),
            grid => GetOrthogonalNeighbors(grid).Where(IsWithinMap));
    }

    private bool IsAiStrategicPassable(Vector2I grid, IReadOnlySet<Vector2I> virtualBridges, IReadOnlySet<Vector2I>? virtualBlocks = null)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        if (virtualBridges.Contains(grid))
        {
            return true;
        }

        if (virtualBlocks?.Contains(grid) == true)
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return cell.Terrain is not (BattleTerrainType.River or BattleTerrainType.Moat or BattleTerrainType.Mountain) &&
               !cell.IsBlockingStructure;
    }

    private bool TryExecuteAiBridgeEngineeringAction(BattleGridKey sourceGrid, BattleOccupantInfo worker, AiBridgeEngineeringPlan plan, int noise)
    {
        _selectedUnit = worker;
        _selectedUnitGrid = sourceGrid;
        FocusCameraOnBattleGrid(sourceGrid);
        if (plan.CanWorkNow)
        {
            AppendBattleLog(worker, "AI", BattleFormat(
                "log.ai.bridge_engineering_work",
                "Decision: build bridge at {0} toward {1}; route improves by {2} steps (score {3}, variance {4}).",
                plan.WorkGrid,
                plan.ObjectiveGrid,
                plan.PathReduction,
                plan.Score,
                noise));
            return TryExecuteBattleActionIntent(
                new BattleActionIntent(BattleActionKind.Work, sourceGrid, plan.WorkGrid),
                worker);
        }

        AppendBattleLog(worker, "AI", BattleFormat(
            "log.ai.bridge_engineering_move",
            "Decision: move {0} -> {1}, then build bridge at {2} toward {3}; projected route improves by {4} steps (score {5}, variance {6}).",
            sourceGrid,
            plan.ActionGrid,
            plan.WorkGrid,
            plan.ObjectiveGrid,
            plan.PathReduction,
            plan.Score,
            noise));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Move, sourceGrid, plan.ActionGrid),
            worker);
    }

    private IEnumerable<AiFenceEngineeringPlan> GetAiFenceEngineeringPlans(BattleGridKey sourceGrid, BattleOccupantInfo worker)
    {
        if (_mapData?.ScenarioDefinition.ScenarioType != BattleScenarioType.FieldBattle || worker.TroopType != TroopWorker || IsMessed(worker))
        {
            yield break;
        }

        var previousUnit = _selectedUnit;
        var previousUnitGrid = _selectedUnitGrid;
        _selectedUnit = worker;
        _selectedUnitGrid = sourceGrid;
        try
        {
            for (var y = 0; y < BattleMapData.Height; y++)
            {
                for (var x = 0; x < BattleMapData.Width; x++)
                {
                    var fenceGrid = new Vector2I(x, y);
                    var cell = _mapData.GetCell(x, y);
                    if (cell.Structure == BattleStructureType.WoodenFence)
                    {
                        if (worker.Energy < WorkerRemoveWoodFenceEnergyCost)
                        {
                            continue;
                        }

                        var routeImpact = GetAiFriendlyFenceRouteImpact(worker.TeamName, fenceGrid);
                        if (routeImpact > 0 && TryGetAiWorkerActionGrid(sourceGrid, worker, fenceGrid, out var actionGrid, out var canWorkNow))
                        {
                            var removalScore = BattleAiEngineeringPlanner.FenceRemovalScore(routeImpact,
                                GetManhattanDistance(sourceGrid.Grid, actionGrid.Grid), WorkerRemoveWoodFenceEnergyCost);
                            yield return new AiFenceEngineeringPlan(
                                AiFenceEngineeringAction.Remove,
                                ToGroundGridKey(fenceGrid),
                                actionGrid,
                                canWorkNow,
                                null,
                                routeImpact,
                                removalScore);
                        }

                        continue;
                    }

                    if (worker.Energy < WorkerInstallWoodFenceEnergyCost ||
                        !CanInstallWoodFence(fenceGrid, cell) ||
                        !TryGetAiWorkerActionGrid(sourceGrid, worker, fenceGrid, out var buildActionGrid, out var canBuildNow) ||
                        WouldAiFenceBlockFriendlyRoute(worker.TeamName, fenceGrid))
                    {
                        continue;
                    }

                    var defenseImpact = GetAiFenceDefensePathImpact(worker.TeamName, fenceGrid, out var protectedGrid);
                    if (defenseImpact <= 0)
                    {
                        continue;
                    }

                    var enemyDistance = GetAllBattlePieces()
                        .Where(entry => entry.Occupant.TeamName != worker.TeamName)
                        .Select(entry => GetManhattanDistance(entry.Grid.Grid, fenceGrid))
                        .DefaultIfEmpty(int.MaxValue)
                        .Min();
                    var hasFriendlySupport = GetAllBattlePieces()
                        .Any(entry => entry.Occupant.TeamName == worker.TeamName &&
                                      entry.Occupant.TroopType != TroopWorker &&
                                      GetManhattanDistance(entry.Grid.Grid, fenceGrid) <= 3);
                    if (enemyDistance > 5 || !hasFriendlySupport)
                    {
                        continue;
                    }

                    var constructionScore = BattleAiEngineeringPlanner.FenceConstructionScore(defenseImpact,
                        hasFriendlySupport, enemyDistance, GetAiCommandIntelligence(sourceGrid, worker),
                        GetManhattanDistance(sourceGrid.Grid, buildActionGrid.Grid), WorkerInstallWoodFenceEnergyCost);
                    yield return new AiFenceEngineeringPlan(
                        AiFenceEngineeringAction.Build,
                        ToGroundGridKey(fenceGrid),
                        buildActionGrid,
                        canBuildNow,
                        protectedGrid,
                        defenseImpact,
                        constructionScore);
                }
            }
        }
        finally
        {
            _selectedUnit = previousUnit;
            _selectedUnitGrid = previousUnitGrid;
        }
    }

    private int GetAiFenceDefensePathImpact(string teamName, Vector2I fenceGrid, out BattleGridKey? protectedGrid)
    {
        protectedGrid = null;
        if (_mapData == null)
        {
            return 0;
        }

        var desiredOwner = IsDefenderTeamName(teamName) ? BattleOutpostOwner.Defender : BattleOutpostOwner.Attacker;
        var strongholds = Enumerable.Range(0, BattleMapData.Height)
            .SelectMany(y => Enumerable.Range(0, BattleMapData.Width).Select(x => _mapData.GetCell(x, y)))
            .Where(cell => cell.IsDefenseOutpost && cell.DefenseOutpostOwner == desiredOwner)
            .Select(cell => ToGroundGridKey(cell.Grid))
            .ToList();
        var enemies = GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName != teamName && entry.Grid.Level == 0)
            .Select(entry => entry.Grid.Grid)
            .ToList();
        var virtualBlocks = new HashSet<Vector2I> { fenceGrid };
        var bestImpact = 0;
        foreach (var stronghold in strongholds)
        {
            foreach (var enemy in enemies)
            {
                var before = GetAiStrategicPathLength(enemy, stronghold.Grid, new HashSet<Vector2I>());
                var after = GetAiStrategicPathLength(enemy, stronghold.Grid, new HashSet<Vector2I>(), virtualBlocks);
                var impact = BattleAiEngineeringPlanner.RouteObstruction(before, after);
                if (impact > bestImpact)
                {
                    bestImpact = impact;
                    protectedGrid = stronghold;
                }
            }
        }

        return bestImpact;
    }

    private bool WouldAiFenceBlockFriendlyRoute(string teamName, Vector2I fenceGrid)
    {
        var friendlyOrigins = GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName == teamName && entry.Occupant.TroopType != TroopWorker && entry.Grid.Level == 0)
            .Select(entry => entry.Grid.Grid)
            .ToList();
        var objectives = GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName != teamName && entry.Grid.Level == 0)
            .Select(entry => entry.Grid.Grid)
            .ToList();
        var virtualBlocks = new HashSet<Vector2I> { fenceGrid };
        foreach (var origin in friendlyOrigins)
        {
            foreach (var objective in objectives)
            {
                var before = GetAiStrategicPathLength(origin, objective, new HashSet<Vector2I>());
                if (before == int.MaxValue)
                {
                    continue;
                }

                var after = GetAiStrategicPathLength(origin, objective, new HashSet<Vector2I>(), virtualBlocks);
                if (BattleAiEngineeringPlanner.BlocksFriendlyRoute(before, after))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private int GetAiFriendlyFenceRouteImpact(string teamName, Vector2I fenceGrid)
    {
        var friendlyOrigins = GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName == teamName && entry.Occupant.TroopType != TroopWorker && entry.Grid.Level == 0)
            .Select(entry => entry.Grid.Grid)
            .ToList();
        var objectives = GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName != teamName && entry.Grid.Level == 0)
            .Select(entry => entry.Grid.Grid)
            .ToList();
        var virtualFenceRemoved = new HashSet<Vector2I> { fenceGrid };
        var bestImpact = 0;
        foreach (var origin in friendlyOrigins)
        {
            foreach (var objective in objectives)
            {
                var before = GetAiStrategicPathLength(origin, objective, new HashSet<Vector2I>());
                var after = GetAiStrategicPathLength(origin, objective, virtualFenceRemoved);
                var impact = BattleAiEngineeringPlanner.RouteImprovement(before, after);
                bestImpact = Math.Max(bestImpact, impact);
            }
        }

        return bestImpact;
    }

   private bool TryExecuteAiFenceEngineeringAction(BattleGridKey sourceGrid, BattleOccupantInfo worker, AiFenceEngineeringPlan plan, int noise)
   {
       _selectedUnit = worker;
       _selectedUnitGrid = sourceGrid;
       FocusCameraOnBattleGrid(sourceGrid);
       var actionVerb = plan.Action == AiFenceEngineeringAction.Build ? "build" : "remove";
       var objectiveText = plan.ProtectedGrid.HasValue ? $" protect {plan.ProtectedGrid.Value}" : " reopen own route";
       if (plan.CanWorkNow)
       {
           AppendBattleLog(worker, "AI", $"Engineering plan: {actionVerb} wood fence at {plan.FenceGrid};{objectiveText}, path impact {plan.PathImpact} (score {plan.Score}, variance {noise}).");
           return TryExecuteBattleActionIntent(
               new BattleActionIntent(BattleActionKind.Work, sourceGrid, plan.FenceGrid, UseWoodFenceWork: true),
               worker);
       }

       AppendBattleLog(worker, "AI", $"Engineering plan: move {sourceGrid} -> {plan.ActionGrid}, then {actionVerb} wood fence at {plan.FenceGrid};{objectiveText}, path impact {plan.PathImpact} (score {plan.Score}, variance {noise}).");
       return TryExecuteBattleActionIntent(
           new BattleActionIntent(BattleActionKind.Move, sourceGrid, plan.ActionGrid),
           worker);
   }
}
