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
    private void ExecuteOneAiAction()
    {
        var candidates = GetActingBattlePieces()
            .Where(entry => !HasUnitActed(entry.Occupant))
            .OrderBy(entry => entry.Occupant.TroopType)
            .ThenBy(entry => entry.Grid.Y)
            .ThenBy(entry => entry.Grid.X)
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        foreach (var candidate in candidates.Where(candidate => candidate.Occupant.TroopType == TroopLadder))
        {
            if (TryExecuteAiLadderDeployment(candidate.Grid, candidate.Occupant))
            {
                return;
            }
        }

        if (TryExecuteAiGateControl(candidates))
        {
            return;
        }

        if (TryExecuteAiSiegeBreachAction(candidates))
        {
            return;
        }

        foreach (var candidate in candidates.Where(candidate => IsAiPostGateBreachSiegeEngine(candidate.Occupant)))
        {
            if (TryExecuteAiPostGateBreachSiegeEngineAction(candidate.Grid, candidate.Occupant))
            {
                return;
            }
        }

        foreach (var candidate in candidates.Where(candidate => candidate.Occupant.TroopType == TroopSupplyCart))
        {
            if (TryExecuteAiPrioritySupply(candidate.Grid, candidate.Occupant))
            {
                return;
            }
        }

        if (TryExecuteBestAiSurvivalAction(candidates))
        {
            return;
        }

        if (TryExecuteBestAiOffensiveAction(candidates))
        {
            return;
        }

        if (TryExecuteAiFortressMissionAdvance(candidates))
        {
            return;
        }

        foreach (var candidate in candidates.Where(candidate =>
                     candidate.Occupant.TroopType != TroopSupplyCart &&
                     !IsAiAmmoDepletedCatapult(candidate.Occupant)))
        {
            if (TryExecuteAiMoveAndAttack(candidate.Grid, candidate.Occupant))
            {
                return;
            }
        }

        if (TryExecuteAiRoadYield(candidates))
        {
            return;
        }

        foreach (var movingCandidate in candidates.OrderBy(candidate => GetAiFallbackMovementPriority(candidate.Occupant)))
        {
            if (TryExecuteAiMove(movingCandidate.Grid, movingCandidate.Occupant))
            {
                return;
            }
        }

        var waitingCandidate = candidates[0];
        FocusCameraOnBattleGrid(waitingCandidate.Grid);
        AppendBattleLog(waitingCandidate.Occupant, "AI", BattleFormat("log.ai.wait", "Decision: wait at {0}; no legal move or attack.", waitingCandidate.Grid));
        MarkUnitActed(waitingCandidate.Occupant);
    }

    private bool TryExecuteAiRoadYield(IReadOnlyList<(BattleGridKey Grid, BattleOccupantInfo Occupant)> candidates)
    {
        if (_mapData == null)
        {
            return false;
        }

        foreach (var blocker in candidates
                     .Where(candidate => candidate.Occupant.Category == CategoryUnit &&
                                         !string.IsNullOrWhiteSpace(candidate.Occupant.OfficerName) &&
                                         GetOfficerTacticalIntelligence(candidate.Occupant.OfficerName) >= AiRoadYieldIntelligenceThreshold)
                     .OrderByDescending(candidate => GetOfficerTacticalIntelligence(candidate.Occupant.OfficerName))
                     .ThenBy(candidate => candidate.Grid.Y)
                     .ThenBy(candidate => candidate.Grid.X))
        {
            if (_mapData.GetCell(blocker.Grid.X, blocker.Grid.Y).Terrain != BattleTerrainType.Road ||
                !TryGetAiRoadYieldDestination(blocker.Grid, blocker.Occupant, out var destination) ||
                !TryGetAiTeammateBlockedByRoad(blocker.Grid, blocker.Occupant, candidates, out var delayedTeammate, out var enemyGrid))
            {
                continue;
            }

            _selectedUnit = blocker.Occupant;
            _selectedUnitGrid = blocker.Grid;
            FocusCameraOnBattleGrid(blocker.Grid);
            AppendBattleLog(
                blocker.Occupant,
                "AI",
                BattleFormat(
                    "log.ai.road_yield",
                    "Decision: yield road at {0}; move to {1} so {2} can advance toward {3}.",
                    blocker.Grid,
                    destination,
                    FormatLogUnit(delayedTeammate),
                    enemyGrid));
            return TryExecuteBattleActionIntent(
                new BattleActionIntent(BattleActionKind.Move, blocker.Grid, destination),
                blocker.Occupant);
        }

        return false;
    }

    private bool TryGetAiRoadYieldDestination(BattleGridKey sourceGrid, BattleOccupantInfo blocker, out BattleGridKey destination)
    {
        destination = default;
        var currentThreat = GetAiThreatScore(sourceGrid, blocker);
        _selectedUnit = blocker;
        _selectedUnitGrid = sourceGrid;
        destination = CalculateReachableGrids(sourceGrid, GetAvailableMoveEnergy(blocker), GetAvailableMoveRange(blocker))
            .Where(grid => grid.Level == sourceGrid.Level && GetManhattanDistance(sourceGrid.Grid, grid.Grid) == 1)
            .Where(grid => _mapData!.GetCell(grid.X, grid.Y).Terrain != BattleTerrainType.Road)
            .Where(IsAiSafeMovementDestination)
            .Where(grid => GetAiThreatScore(grid, blocker) <= currentThreat)
            .OrderBy(grid => GetAiThreatScore(grid, blocker))
            .ThenBy(grid => grid.Y)
            .ThenBy(grid => grid.X)
            .FirstOrDefault();
        return destination != default;
    }

    private bool TryGetAiTeammateBlockedByRoad(
        BattleGridKey blockerGrid,
        BattleOccupantInfo blocker,
        IReadOnlyList<(BattleGridKey Grid, BattleOccupantInfo Occupant)> candidates,
        out BattleOccupantInfo delayedTeammate,
        out BattleGridKey enemyGrid)
    {
        delayedTeammate = null!;
        enemyGrid = default;
        var previousUnit = _selectedUnit;
        var previousUnitGrid = _selectedUnitGrid;
        try
        {
            foreach (var teammate in candidates.Where(candidate =>
                         candidate.Occupant.Marker != blocker.Marker &&
                         candidate.Occupant.TeamName == blocker.TeamName &&
                         candidate.Occupant.Category == CategoryUnit))
            {
                _selectedUnit = teammate.Occupant;
                _selectedUnitGrid = teammate.Grid;
                foreach (var enemy in GetAllBattlePieces().Where(entry =>
                             IsAttackerPiece(entry.Occupant) != IsAttackerPiece(teammate.Occupant) &&
                             !IsHiddenFromSide(entry.Occupant, teammate.Occupant.TeamName)))
                {
                    foreach (var approachGrid in GetMovementNeighbors(enemy.Grid)
                                 .Select(step => step.Grid)
                                 .Where(grid => IsWithinMap(grid.Grid))
                                 .Distinct())
                    {
                        var fullPathBudget = BattleMapData.Width * BattleMapData.Height * 2;
                        if (TryBuildMovePath(teammate.Grid, approachGrid, fullPathBudget, fullPathBudget, out _) ||
                            !TryBuildMovePath(teammate.Grid, approachGrid, fullPathBudget, fullPathBudget, out var yieldPath, blockerGrid) ||
                            !yieldPath.Contains(blockerGrid))
                        {
                            continue;
                        }

                        delayedTeammate = teammate.Occupant;
                        enemyGrid = enemy.Grid;
                        return true;
                    }
                }
            }

            return false;
        }
        finally
        {
            _selectedUnit = previousUnit;
            _selectedUnitGrid = previousUnitGrid;
        }
    }

    private bool IsAiPostGateBreachSiegeEngine(BattleOccupantInfo unit)
    {
        return IsAttackerPiece(unit) &&
               unit.Category == CategorySiegeEngine &&
               unit.TroopType is TroopRam or TroopLadder &&
               HasBreachedCityGate();
    }

    private bool HasBreachedCityGate()
    {
        if (_mapData == null)
        {
            return false;
        }

        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var cell = _mapData.GetCell(x, y);
                if (cell.Structure == BattleStructureType.Gate && (cell.IsGateOpen || cell.IsBroken))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryExecuteAiLadderDeployment(BattleGridKey sourceGrid, BattleOccupantInfo ladder)
    {
        if (_mapData == null ||
            ladder.TroopType != TroopLadder ||
            !IsAttackerPiece(ladder) ||
            _mapData.ScenarioDefinition.ScenarioType == BattleScenarioType.FieldBattle)
        {
            return false;
        }

        _selectedUnit = ladder;
        _selectedUnitGrid = sourceGrid;
        FocusCameraOnBattleGrid(sourceGrid);

        if (IsAiLadderDeploymentGrid(sourceGrid))
        {
            var wallTopCount = GetCarLadderWallTopEndpoints(sourceGrid.Grid).Count();
            AppendBattleLog(ladder, "AI", BattleFormat(
                "log.ai.ladder_hold",
                "Decision: hold ladder at {0}; it supports {1} wall-top access point(s) for friendly climbing teams.",
                sourceGrid,
                wallTopCount));
            MarkUnitActed(ladder);
            return true;
        }

        var plans = new List<(BattleGridKey Goal, BattleGridKey Destination, int Score, int PathEnergy, int PathSteps, int OpenWallTopCount, int NearbyClimberCount)>();
        foreach (var goal in GetAiLadderDeploymentGrids())
        {
            if (!TryGetAiPathEndpointToward(sourceGrid, ladder, goal, out var destination, out var pathEnergy, out var pathSteps))
            {
                continue;
            }

            var wallTopEndpoints = GetCarLadderWallTopEndpoints(goal.Grid)
                .Select(ToWallWalkGridKey)
                .ToList();
            var openWallTopCount = wallTopEndpoints.Count(grid => !HasBlockingOccupant(grid));
            var groundEndpoints = GetCarLadderGroundEndpoints(goal.Grid)
                .Select(ToGroundGridKey)
                .ToList();
            var nearbyClimberCount = GetAllBattlePieces()
                .Count(entry =>
                    entry.Occupant.TeamName == ladder.TeamName &&
                    entry.Grid.Level != 2 &&
                    CanUseCarLadderBridge(entry.Occupant) &&
                    groundEndpoints.Any(endpoint => GetManhattanDistance(entry.Grid.Grid, endpoint.Grid) <= 6));
            var enemyWallDefenderCount = wallTopEndpoints.Count(grid =>
                _occupantsByGrid.TryGetValue(grid, out var occupants) &&
                occupants.Any(occupant => IsAttackerPiece(occupant) != IsAttackerPiece(ladder)));
            var score = openWallTopCount * 5000 +
                        nearbyClimberCount * 1000 +
                        enemyWallDefenderCount * 350 -
                        pathEnergy * 300 -
                        pathSteps * 40 -
                        GetAiThreatScore(goal, ladder) / 2;
            plans.Add((goal, destination, score, pathEnergy, pathSteps, openWallTopCount, nearbyClimberCount));
        }

        var plan = plans
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.OpenWallTopCount)
            .ThenByDescending(candidate => candidate.NearbyClimberCount)
            .ThenBy(candidate => candidate.PathEnergy)
            .ThenBy(candidate => candidate.PathSteps)
            .ThenBy(candidate => candidate.Goal.Y)
            .ThenBy(candidate => candidate.Goal.X)
            .FirstOrDefault();
        if (plan == default)
        {
            return false;
        }

        var moveEnergyCost = 0;
        var moveRangeCost = 0;
        if (plan.Destination != sourceGrid &&
            TryBuildMovePath(sourceGrid, plan.Destination, ladder.Energy, GetAvailableMoveRange(ladder), out var movePath))
        {
            moveEnergyCost = GetMovePathEnergyCost(movePath);
            moveRangeCost = GetMovePathRangeCost(movePath);
        }

        AppendBattleLog(ladder, "AI", BattleFormat(
            "log.ai.ladder_deploy",
            "Decision: deploy ladder toward {0}; move {1} -> {2}. Open wall-top entries {3}, nearby climbers {4}, A* energy {5}, steps {6}, this move energy {7}, range {8}, score {9}.",
            plan.Goal, sourceGrid, plan.Destination, plan.OpenWallTopCount, plan.NearbyClimberCount,
            plan.PathEnergy, plan.PathSteps, moveEnergyCost, moveRangeCost, plan.Score));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Move, sourceGrid, plan.Destination),
            ladder);
    }

    private IEnumerable<BattleGridKey> GetAiLadderDeploymentGrids()
    {
        if (_mapData == null)
        {
            yield break;
        }

        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var grid = new Vector2I(x, y);
                var deploymentGrid = ToGroundGridKey(grid);
                if (IsGateGrid(grid) || !IsAiLadderDeploymentGrid(deploymentGrid))
                {
                    continue;
                }

                yield return deploymentGrid;
            }
        }
    }

    private bool IsAiLadderDeploymentGrid(BattleGridKey grid)
    {
        if (_mapData == null ||
            grid.Level != 0 ||
            !IsWithinMap(grid.Grid) ||
            IsGateGrid(grid.Grid))
        {
            return false;
        }

        return GetCarLadderGroundEndpoints(grid.Grid).Any() &&
               GetCarLadderWallTopEndpoints(grid.Grid).Any();
    }

    private bool TryExecuteAiGateControl(IReadOnlyList<(BattleGridKey Grid, BattleOccupantInfo Occupant)> candidates)
    {
        if (_mapData == null || _mapData.ScenarioDefinition.ScenarioType == BattleScenarioType.FieldBattle)
        {
            return false;
        }

        var gateGrids = GetAiGateGrids().ToList();
        if (gateGrids.Count == 0)
        {
            return false;
        }

        var actingAttackers = candidates.Where(candidate => IsAttackerPiece(candidate.Occupant)).ToList();
        if (actingAttackers.Count > 0)
        {
            foreach (var attacker in actingAttackers)
            {
                var attackerGateCell = _mapData.GetCell(attacker.Grid.X, attacker.Grid.Y);
                if (!CanToggleGateAtGrid(attacker.Grid, attacker.Occupant) || attackerGateCell.IsGateOpen)
                {
                    continue;
                }

                FocusCameraOnBattleGrid(attacker.Grid);
                AppendBattleLog(attacker.Occupant, "AI", BattleFormat("log.ai.gate_capture", "Gate capture: open controlled gate at {0} for the ground assault.", attacker.Grid));
                return TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.ToggleGate, attacker.Grid, attacker.Grid),
                    attacker.Occupant);
            }

            return false;
        }

        if (TryExecuteAiDefenderSortiePlan(candidates))
        {
            return true;
        }

        var defenderCandidates = candidates
            .Where(candidate => IsDefenderPiece(candidate.Occupant) && candidate.Occupant.Category == CategoryUnit)
            .ToList();
        if (defenderCandidates.Count == 0)
        {
            return false;
        }

        var capturedGate = gateGrids.FirstOrDefault(gateGrid => HasAttackerGateController(gateGrid));
        if (capturedGate != default)
        {
            foreach (var defender in defenderCandidates)
            {
                _selectedUnit = defender.Occupant;
                _selectedUnitGrid = defender.Grid;
                if (CalculateAttackableGrids(defender.Grid, defender.Occupant).Contains(capturedGate))
                {
                    var score = 20000 + GetAttackDamageAgainst(defender.Occupant, GetAiAttackTargetForAttack(_occupantsByGrid[capturedGate], defender.Occupant.TeamName, capturedGate)!);
                    return TryExecuteAiAttack(defender.Grid, defender.Occupant, capturedGate, score, GetAiDecisionNoise(defender.Grid, capturedGate, participantCount: 1));
                }
            }

            var recoveryMoves = new List<(BattleGridKey Source, BattleOccupantInfo Unit, BattleGridKey Destination, int PathEnergy, int PathSteps)>();
            foreach (var defender in defenderCandidates)
            {
                _selectedUnit = defender.Occupant;
                _selectedUnitGrid = defender.Grid;
                if (TryGetAiPathEndpointTowardEnemy(defender.Grid, defender.Occupant, capturedGate, out var destination, out var pathEnergy, out var pathSteps))
                {
                    recoveryMoves.Add((defender.Grid, defender.Occupant, destination, pathEnergy, pathSteps));
                }
            }

            var recoveryMove = recoveryMoves
                .OrderBy(move => move.PathEnergy)
                .ThenBy(move => move.PathSteps)
                .ThenByDescending(move => move.Unit.TroopCount)
                .FirstOrDefault();
            if (recoveryMove != default)
            {
                FocusCameraOnBattleGrid(recoveryMove.Source);
                AppendBattleLog(recoveryMove.Unit, "AI", BattleFormat("log.ai.gate_recover_move", "Gate defense: {0} moves {1} -> {2} to recover attacker-controlled gate {3}; A* energy {4}, steps {5}.", FormatLogUnit(recoveryMove.Unit), recoveryMove.Source, recoveryMove.Destination, capturedGate, recoveryMove.PathEnergy, recoveryMove.PathSteps));
                return TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.Move, recoveryMove.Source, recoveryMove.Destination),
                    recoveryMove.Unit);
            }
        }

        foreach (var defender in defenderCandidates.Where(candidate => candidate.Grid.Level == 2))
        {
            var wallTopThreat = gateGrids
                .Where(gateGrid => GetManhattanDistance(defender.Grid.Grid, gateGrid.Grid) <= 1)
                .Select(GetAiGateDefensePriority)
                .DefaultIfEmpty(0)
                .Max();
            if (wallTopThreat > 0 &&
                !HasAiDirectAttackOpportunity(defender.Grid, defender.Occupant) &&
                CanUseGuard(defender.Occupant))
            {
                FocusCameraOnBattleGrid(defender.Grid);
                AppendBattleLog(defender.Occupant, "AI", BattleFormat("log.ai.gate_wall_guard", "Gate defense: {0} holds wall top at {1} against gate threat {2}.", FormatLogUnit(defender.Occupant), defender.Grid, wallTopThreat));
                return TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.Guard, defender.Grid, defender.Grid),
                    defender.Occupant);
            }
        }

        foreach (var defender in defenderCandidates)
        {
            if (!CanToggleGateAtGrid(defender.Grid, defender.Occupant))
            {
                continue;
            }

            var gateCell = _mapData.GetCell(defender.Grid.X, defender.Grid.Y);
            if (!gateCell.IsGateOpen &&
                defender.Occupant.Marker != null &&
                TryGetAiDefenderSortieTarget(defender.Grid, out var sortieTarget, out var sortieExit))
            {
                var sortiePlan = new AiGateSortiePlan(
                    defender.Grid,
                    sortieTarget,
                    sortieExit,
                    defender.Occupant.Marker,
                    defender.Occupant.TroopCount,
                    _turnNumber + 1,
                    AiGateSortiePhase.Exit);
                FocusCameraOnBattleGrid(defender.Grid);
                if (!TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.ToggleGate, defender.Grid, defender.Grid),
                    defender.Occupant))
                {
                    return false;
                }

                // Do not leave a pending sortie plan behind when opening the
                // gate was rejected by the shared action rules.
                _aiGateSortiePlan = sortiePlan;
                AppendBattleLog(defender.Occupant, "AI", BattleFormat("log.ai.gate_sortie_open", "Gate defense: {0} opens controlled gate at {1}; sortie exit {2}, target {3}, scheduled for defender turn {4}.", FormatLogUnit(defender.Occupant), defender.Grid, sortieExit, sortieTarget, _turnNumber + 1));
                return true;
            }

            if (gateCell.IsGateOpen)
            {
                if (IsAiDefenderSortiePlanHoldingGate(defender.Grid))
                {
                    continue;
                }

                FocusCameraOnBattleGrid(defender.Grid);
                AppendBattleLog(defender.Occupant, "AI", BattleFormat("log.ai.gate_close", "Gate defense: {0} closes controlled gate at {1} before attackers can exploit the passage.", FormatLogUnit(defender.Occupant), defender.Grid));
                return TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.ToggleGate, defender.Grid, defender.Grid),
                    defender.Occupant);
            }

            if (GetAiGateDefensePriority(defender.Grid) > 0 && CanUseGuard(defender.Occupant))
            {
                FocusCameraOnBattleGrid(defender.Grid);
                AppendBattleLog(defender.Occupant, "AI", BattleFormat("log.ai.gate_guard", "Gate defense: {0} guards threatened closed gate at {1}.", FormatLogUnit(defender.Occupant), defender.Grid));
                return TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.Guard, defender.Grid, defender.Grid),
                    defender.Occupant);
            }
        }

        var threatenedGates = gateGrids.Where(gateGrid => GetAiGateDefensePriority(gateGrid) > 0).ToList();
        var protectionMoves = new List<(BattleGridKey Source, BattleOccupantInfo Unit, BattleGridKey Destination, BattleGridKey Gate, int Threat, int PathEnergy, int PathSteps)>();
        foreach (var defender in defenderCandidates)
        {
            _selectedUnit = defender.Occupant;
            _selectedUnitGrid = defender.Grid;
            foreach (var gateGrid in threatenedGates)
            {
                if (defender.Grid == gateGrid ||
                    !TryGetAiPathEndpointToward(defender.Grid, defender.Occupant, gateGrid, out var destination, out var pathEnergy, out var pathSteps))
                {
                    continue;
                }

                protectionMoves.Add((defender.Grid, defender.Occupant, destination, gateGrid, GetAiGateDefensePriority(gateGrid), pathEnergy, pathSteps));
            }
        }

        var protectionMove = protectionMoves
            .OrderByDescending(move => move.Threat)
            .ThenBy(move => move.PathEnergy)
            .ThenBy(move => move.PathSteps)
            .ThenByDescending(move => move.Unit.TroopCount)
            .FirstOrDefault();
        if (protectionMove == default)
        {
            return false;
        }

        FocusCameraOnBattleGrid(protectionMove.Source);
        AppendBattleLog(protectionMove.Unit, "AI", BattleFormat("log.ai.gate_protect_move", "Gate defense: {0} moves {1} -> {2} to protect threatened gate {3}; threat {4}, A* energy {5}, steps {6}.", FormatLogUnit(protectionMove.Unit), protectionMove.Source, protectionMove.Destination, protectionMove.Gate, protectionMove.Threat, protectionMove.PathEnergy, protectionMove.PathSteps));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Move, protectionMove.Source, protectionMove.Destination),
            protectionMove.Unit);
    }

    private IEnumerable<BattleGridKey> GetAiGateGrids()
    {
        if (_mapData == null)
        {
            yield break;
        }

        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var grid = new Vector2I(x, y);
                if (_mapData.GetCell(x, y).Structure == BattleStructureType.Gate)
                {
                    yield return ToGroundGridKey(grid);
                }
            }
        }
    }

    private bool TryExecuteAiDefenderSortiePlan(IReadOnlyList<(BattleGridKey Grid, BattleOccupantInfo Occupant)> candidates)
    {
        var plan = _aiGateSortiePlan;
        if (plan == null || _currentTurnSide != BattleTurnSide.TeamB)
        {
            return false;
        }

        if (_turnNumber > plan.ExecuteTurn + 4 || _mapData == null)
        {
            _aiGateSortiePlan = null;
            return false;
        }

        var gateCell = _mapData.GetCell(plan.GateGrid.X, plan.GateGrid.Y);
        if (!gateCell.IsGateOpen)
        {
            _aiGateSortiePlan = null;
            return false;
        }

        var sortieUnit = GetAllBattlePieces().FirstOrDefault(entry => entry.Occupant.Marker == plan.SortieMarker);
        if (sortieUnit == default || !IsDefenderPiece(sortieUnit.Occupant))
        {
            _aiGateSortiePlan = null;
            return false;
        }

        if (plan.Phase == AiGateSortiePhase.Exit)
        {
            if (_turnNumber < plan.ExecuteTurn || sortieUnit.Grid != plan.GateGrid ||
                !candidates.Any(candidate => candidate.Occupant.Marker == plan.SortieMarker))
            {
                return false;
            }

            _aiGateSortiePlan = plan with { Phase = AiGateSortiePhase.Engage };
            FocusCameraOnBattleGrid(sortieUnit.Grid);
            AppendBattleLog(sortieUnit.Occupant, "AI", BattleFormat("log.ai.sortie_exit", "Gate sortie: exit {0} -> {1}; target {2} remains isolated outside the city.", plan.GateGrid, plan.ExitGrid, plan.TargetGrid));
            return TryExecuteBattleActionIntent(
                new BattleActionIntent(BattleActionKind.Move, sortieUnit.Grid, plan.ExitGrid),
                sortieUnit.Occupant);
        }

        if (plan.Phase == AiGateSortiePhase.Return)
        {
            var innerCityGoal = GetOrthogonalNeighbors(plan.GateGrid.Grid)
                .Where(IsWithinMap)
                .Where(IsInsideCityGroundGrid)
                .Select(ToGroundGridKey)
                .FirstOrDefault();
            if (innerCityGoal == default ||
                !candidates.Any(candidate => candidate.Occupant.Marker == plan.SortieMarker) ||
                !TryGetAiPathEndpointToward(sortieUnit.Grid, sortieUnit.Occupant, innerCityGoal, out var returnDestination, out var returnEnergy, out var returnSteps))
            {
                return false;
            }

            _aiGateSortiePlan = plan with { Phase = AiGateSortiePhase.Close };
            FocusCameraOnBattleGrid(sortieUnit.Grid);
            AppendBattleLog(sortieUnit.Occupant, "AI", BattleFormat("log.ai.sortie_retreat", "Gate sortie: retreat {0} -> {1} through opened gate {2}; A* energy {3}, steps {4}.", sortieUnit.Grid, returnDestination, plan.GateGrid, returnEnergy, returnSteps));
            return TryExecuteBattleActionIntent(
                new BattleActionIntent(BattleActionKind.Move, sortieUnit.Grid, returnDestination),
                sortieUnit.Occupant);
        }

        if (plan.Phase == AiGateSortiePhase.Close)
        {
            var closer = candidates.FirstOrDefault(candidate =>
                candidate.Grid == plan.GateGrid &&
                IsDefenderPiece(candidate.Occupant) &&
                candidate.Occupant.Category == CategoryUnit &&
                CanToggleGateAtGrid(candidate.Grid, candidate.Occupant));
            if (closer != default)
            {
                _aiGateSortiePlan = null;
                FocusCameraOnBattleGrid(closer.Grid);
                AppendBattleLog(closer.Occupant, "AI", BattleFormat("log.ai.sortie_close", "Gate sortie: close gate {0} after the sortie unit returned to the inner city.", plan.GateGrid));
                return TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.ToggleGate, closer.Grid, closer.Grid),
                    closer.Occupant);
            }

            var closerMove = candidates
                .Where(candidate => candidate.Occupant.Category == CategoryUnit && candidate.Occupant.Marker != plan.SortieMarker)
                .Select(candidate =>
                {
                    var canReach = TryGetAiPathEndpointToward(candidate.Grid, candidate.Occupant, plan.GateGrid, out var destination, out var pathEnergy, out var pathSteps);
                    return (Candidate: candidate, CanReach: canReach, Destination: destination, PathEnergy: pathEnergy, PathSteps: pathSteps);
                })
                .Where(move => move.CanReach)
                .OrderBy(move => move.PathEnergy)
                .ThenBy(move => move.PathSteps)
                .FirstOrDefault();
            if (closerMove != default)
            {
                FocusCameraOnBattleGrid(closerMove.Candidate.Grid);
                AppendBattleLog(closerMove.Candidate.Occupant, "AI", BattleFormat("log.ai.sortie_closer_move", "Gate sortie: move {0} -> {1} to close returned-sortie gate {2}; A* energy {3}, steps {4}.", closerMove.Candidate.Grid, closerMove.Destination, plan.GateGrid, closerMove.PathEnergy, closerMove.PathSteps));
                return TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.Move, closerMove.Candidate.Grid, closerMove.Destination),
                    closerMove.Candidate.Occupant);
            }

            return false;
        }

        if (plan.Phase != AiGateSortiePhase.Engage ||
            !candidates.Any(candidate => candidate.Occupant.Marker == plan.SortieMarker))
        {
            return false;
        }

        if (IsAiSortieRetreatRequired(sortieUnit.Grid, sortieUnit.Occupant, plan) ||
            !_occupantsByGrid.TryGetValue(plan.TargetGrid, out var targetOccupants) ||
            !targetOccupants.Any(IsAttackerPiece))
        {
            _aiGateSortiePlan = plan with { Phase = AiGateSortiePhase.Return };
            return TryExecuteAiDefenderSortiePlan(candidates);
        }

        _selectedUnit = sortieUnit.Occupant;
        _selectedUnitGrid = sortieUnit.Grid;
        if (CalculateAttackableGrids(sortieUnit.Grid, sortieUnit.Occupant).Contains(plan.TargetGrid))
        {
            var target = GetAiAttackTargetForAttack(targetOccupants, sortieUnit.Occupant.TeamName, plan.TargetGrid);
            if (target != null)
            {
                var score = 16000 + GetAttackDamageAgainst(sortieUnit.Occupant, target);
                FocusCameraOnBattleGrid(sortieUnit.Grid);
                AppendBattleLog(sortieUnit.Occupant, "AI", BattleFormat("log.ai.sortie_attack", "Gate sortie: attack isolated attacker at {0} from exterior grid {1}.", plan.TargetGrid, sortieUnit.Grid));
                return TryExecuteAiAttack(sortieUnit.Grid, sortieUnit.Occupant, plan.TargetGrid, score, GetAiDecisionNoise(sortieUnit.Grid, plan.TargetGrid, participantCount: 1));
            }
        }

        return false;
    }

    private bool IsAiSortieRetreatRequired(BattleGridKey sortieGrid, BattleOccupantInfo sortieUnit, AiGateSortiePlan plan)
    {
        if (sortieUnit.TroopCount * 100 <= plan.StartingTroopCount * 35 ||
            sortieUnit.Morale.HasValue && sortieUnit.Morale.Value <= LowMoraleMovePenaltyThreshold)
        {
            return true;
        }

        var adjacentAttackers = GetOrthogonalNeighbors(sortieGrid.Grid)
            .Select(ToGroundGridKey)
            .Count(grid => _occupantsByGrid.TryGetValue(grid, out var occupants) && occupants.Any(IsAttackerPiece));
        if (adjacentAttackers >= 2)
        {
            return true;
        }

        return TryGetAiGateBreachEstimate(plan.GateGrid, out var arrivalTurns, out var breachTurns, out _) &&
               arrivalTurns <= 1 && breachTurns <= 2;
    }

    private bool IsAiDefenderSortiePlanHoldingGate(BattleGridKey gateGrid)
    {
        return _aiGateSortiePlan is { } plan &&
               gateGrid.Level == 0 &&
               GetConnectedGateGroup(plan.GateGrid.Grid).Contains(gateGrid.Grid) &&
               plan.Phase == AiGateSortiePhase.Exit &&
               _turnNumber <= plan.ExecuteTurn;
    }

    private bool TryExecuteAiSiegeBreachAction(IReadOnlyList<(BattleGridKey Grid, BattleOccupantInfo Occupant)> candidates)
    {
        if (_mapData == null ||
            _mapData.ScenarioDefinition.ScenarioType == BattleScenarioType.FieldBattle ||
            !candidates.Any(candidate => IsAttackerPiece(candidate.Occupant)))
        {
            return false;
        }

        var intactGates = GetAiGateGrids()
            .Where(gateGrid =>
            {
                var cell = _mapData.GetCell(gateGrid.X, gateGrid.Y);
                return !cell.IsGateOpen && !cell.IsBroken && cell.HasStructureHealth;
            })
            .ToList();
        if (intactGates.Count == 0)
        {
            return false;
        }

        var breachActions = new List<(BattleGridKey Source, BattleOccupantInfo Unit, BattleGridKey Gate, int Damage, int Score)>();
        foreach (var attacker in candidates.Where(candidate => IsAttackerPiece(candidate.Occupant)))
        {
            if (attacker.Occupant.Energy < NormalAttackEnergyCost || !CanUseAttackCommand(attacker.Occupant))
            {
                continue;
            }

            _selectedUnit = attacker.Occupant;
            _selectedUnitGrid = attacker.Grid;
            foreach (var gateGrid in intactGates.Where(gateGrid => CalculateAttackableGrids(attacker.Grid, attacker.Occupant).Contains(gateGrid)))
            {
                var damage = GetStructureAttackDamage(attacker.Occupant);
                if (damage <= 0)
                {
                    continue;
                }

                var gateHealth = _mapData.GetCell(gateGrid.X, gateGrid.Y).StructureHealth;
                var turnsToBreach = Mathf.CeilToInt((float)gateHealth / damage);
                var isRam = attacker.Occupant.TroopType == TroopRam;
                var isFinishingTeamAttack = attacker.Occupant.Category == CategoryUnit && turnsToBreach <= 2;
                if (!isRam && !isFinishingTeamAttack)
                {
                    continue;
                }

                var score = 12000 - turnsToBreach * 1200 + damage * 3 +
                            (isRam ? 4000 : 0) +
                            (turnsToBreach == 1 ? 6000 : 0) -
                            GetAiThreatScore(attacker.Grid, attacker.Occupant) / 3;
                breachActions.Add((attacker.Grid, attacker.Occupant, gateGrid, damage, score));
            }
        }

        var breachAction = breachActions
            .OrderByDescending(action => action.Score)
            .ThenByDescending(action => action.Damage)
            .ThenBy(action => action.Gate.Y)
            .ThenBy(action => action.Gate.X)
            .FirstOrDefault();
        if (breachAction != default)
        {
            FocusCameraOnBattleGrid(breachAction.Source);
            AppendBattleLog(breachAction.Unit, "AI", BattleFormat("log.ai.breach_attack", "Siege breach: attack gate {0} with {1}; structure damage {2}, score {3}.", breachAction.Gate, FormatTroopType(breachAction.Unit.TroopType), breachAction.Damage, breachAction.Score));
            return TryExecuteBattleActionIntent(
                new BattleActionIntent(BattleActionKind.Attack, breachAction.Source, breachAction.Gate),
                breachAction.Unit);
        }

        var ramMoves = new List<(BattleGridKey Source, BattleOccupantInfo Ram, BattleGridKey Destination, BattleGridKey Gate, int PathEnergy, int PathSteps)>();
        foreach (var ram in candidates.Where(candidate => IsAttackerPiece(candidate.Occupant) && candidate.Occupant.TroopType == TroopRam))
        {
            _selectedUnit = ram.Occupant;
            _selectedUnitGrid = ram.Grid;
            foreach (var gateGrid in intactGates)
            {
                foreach (var approachGrid in GetOrthogonalNeighbors(gateGrid.Grid).Select(ToGroundGridKey))
                {
                    if (!IsWithinMap(approachGrid.Grid) ||
                        !TryGetAiPathEndpointToward(ram.Grid, ram.Occupant, approachGrid, out var destination, out var pathEnergy, out var pathSteps))
                    {
                        continue;
                    }

                    ramMoves.Add((ram.Grid, ram.Occupant, destination, gateGrid, pathEnergy, pathSteps));
                }
            }
        }

        var ramMove = ramMoves
            .OrderBy(move => move.PathEnergy)
            .ThenBy(move => move.PathSteps)
            .ThenBy(move => move.Gate.Y)
            .ThenBy(move => move.Gate.X)
            .FirstOrDefault();
        if (ramMove == default)
        {
            return false;
        }

        FocusCameraOnBattleGrid(ramMove.Source);
        AppendBattleLog(ramMove.Ram, "AI", BattleFormat("log.ai.breach_move_ram", "Siege breach: move ram {0} -> {1} toward gate {2}; A* energy {3}, steps {4}.", ramMove.Source, ramMove.Destination, ramMove.Gate, ramMove.PathEnergy, ramMove.PathSteps));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Move, ramMove.Source, ramMove.Destination),
            ramMove.Ram);
    }

    private bool HasAttackerGateController(BattleGridKey gateGrid)
    {
        return _occupantsByGrid.TryGetValue(gateGrid, out var occupants) &&
               occupants.Any(occupant => IsAttackerPiece(occupant) && occupant.Category == CategoryUnit);
    }

    private int GetAiGateThreatLevel(BattleGridKey gateGrid)
    {
        if (_mapData == null || !IsWithinMap(gateGrid.Grid))
        {
            return 0;
        }

        if (HasAttackerGateController(gateGrid))
        {
            return 3;
        }

        if (_occupantsByGrid.TryGetValue(ToWallWalkGridKey(gateGrid.Grid), out var wallTopOccupants) &&
            wallTopOccupants.Any(IsAttackerPiece))
        {
            return 2;
        }

        if (GetOrthogonalNeighbors(gateGrid.Grid)
            .Select(ToGroundGridKey)
            .Any(grid => _occupantsByGrid.TryGetValue(grid, out var occupants) && occupants.Any(IsAttackerPiece)))
        {
            return 2;
        }

        return GetUsableCarLadderGrids()
            .Any(ladderGrid => GetCarLadderWallTopEndpoints(ladderGrid.Grid)
                .Any(wallTopGrid => GetManhattanDistance(wallTopGrid, gateGrid.Grid) <= 1))
            ? 1
            : 0;
    }

    private int GetAiGateDefensePriority(BattleGridKey gateGrid)
    {
        var immediateThreat = GetAiGateThreatLevel(gateGrid);
        if (immediateThreat <= 0 && !TryGetAiGateBreachEstimate(gateGrid, out _, out _, out _))
        {
            return 0;
        }

        var priority = immediateThreat * 5000;
        if (!TryGetAiGateBreachEstimate(gateGrid, out var arrivalTurns, out var breachTurns, out var damage))
        {
            return priority;
        }

        var totalTurns = arrivalTurns + breachTurns;
        return priority + Math.Max(0, 12000 - totalTurns * 2000) + damage * 25;
    }

    private bool TryGetAiGateBreachEstimate(BattleGridKey gateGrid, out int arrivalTurns, out int breachTurns, out int damage)
    {
        arrivalTurns = 0;
        breachTurns = 0;
        damage = 0;
        if (_mapData == null || !IsWithinMap(gateGrid.Grid))
        {
            return false;
        }

        var gateCell = _mapData.GetCell(gateGrid.X, gateGrid.Y);
        if (gateCell.IsGateOpen || gateCell.IsBroken || !gateCell.HasStructureHealth)
        {
            return false;
        }

        var estimates = new List<(int ArrivalTurns, int BreachTurns, int Damage)>();
        foreach (var attacker in GetAllBattlePieces().Where(entry => IsAttackerPiece(entry.Occupant)))
        {
            var structureDamage = GetStructureAttackDamage(attacker.Occupant);
            if (structureDamage <= 0)
            {
                continue;
            }

            var requiredAttacks = Mathf.CeilToInt((float)gateCell.StructureHealth / structureDamage);
            var isRam = attacker.Occupant.TroopType == TroopRam;
            if (!isRam && requiredAttacks > 2)
            {
                continue;
            }

            var pathEnergy = 0;
            var canAttackNow = CalculateAttackableGrids(attacker.Grid, attacker.Occupant).Contains(gateGrid);
            if (!canAttackNow)
            {
                var approachCosts = new List<int>();
                foreach (var approachGrid in GetOrthogonalNeighbors(gateGrid.Grid).Select(ToGroundGridKey))
                {
                    if (!IsWithinMap(approachGrid.Grid) ||
                        !TryGetAiPathEndpointToward(attacker.Grid, attacker.Occupant, approachGrid, out _, out var fullPathEnergy, out _))
                    {
                        continue;
                    }

                    approachCosts.Add(fullPathEnergy);
                }

                if (approachCosts.Count == 0)
                {
                    continue;
                }

                pathEnergy = approachCosts.Min();
            }

            var energyPerTurn = Math.Max(1, GetTeamEnergyCap(attacker.Occupant.TeamName));
            estimates.Add((Mathf.CeilToInt((float)pathEnergy / energyPerTurn), requiredAttacks, structureDamage));
        }

        var best = estimates
            .OrderBy(estimate => estimate.ArrivalTurns + estimate.BreachTurns)
            .ThenBy(estimate => estimate.ArrivalTurns)
            .ThenByDescending(estimate => estimate.Damage)
            .FirstOrDefault();
        if (best == default)
        {
            return false;
        }

        arrivalTurns = best.ArrivalTurns;
        breachTurns = best.BreachTurns;
        damage = best.Damage;
        return true;
    }

    private bool TryGetAiDefenderSortieTarget(BattleGridKey gateGrid, out BattleGridKey targetGrid, out BattleGridKey exitGrid)
    {
        targetGrid = default;
        exitGrid = default;
        if (_mapData == null ||
            gateGrid.Level != 0 ||
            !IsWithinMap(gateGrid.Grid) ||
            _mapData.GetCell(gateGrid.X, gateGrid.Y).IsGateOpen ||
            HasAttackerGateController(gateGrid))
        {
            return false;
        }

        if (_occupantsByGrid.TryGetValue(ToWallWalkGridKey(gateGrid.Grid), out var wallTopOccupants) &&
            wallTopOccupants.Any(IsAttackerPiece))
        {
            return false;
        }

        var exteriorAttackers = GetAllBattlePieces()
            .Where(entry => IsAttackerPiece(entry.Occupant) &&
                            entry.Grid.Level == 0 &&
                            !IsInsideCityGroundGrid(entry.Grid.Grid) &&
                            GetManhattanDistance(entry.Grid.Grid, gateGrid.Grid) is >= 2 and <= 3)
            .Select(entry => entry.Grid)
            .ToList();
        if (exteriorAttackers.Count != 1)
        {
            return false;
        }

        var exitGrids = GetOrthogonalNeighbors(gateGrid.Grid)
            .Where(IsWithinMap)
            .Where(grid => !IsInsideCityGroundGrid(grid))
            .Select(ToGroundGridKey)
            .Where(grid => !HasBlockingOccupant(grid))
            .Where(grid => GetManhattanDistance(grid.Grid, exteriorAttackers[0].Grid) < GetManhattanDistance(gateGrid.Grid, exteriorAttackers[0].Grid))
            .ToList();
        if (exitGrids.Count == 0)
        {
            return false;
        }

        if (TryGetAiGateBreachEstimate(gateGrid, out var arrivalTurns, out var breachTurns, out _) &&
            arrivalTurns <= 1 && breachTurns <= 2)
        {
            return false;
        }

        var defenderSupport = GetAllBattlePieces()
            .Count(entry => IsDefenderPiece(entry.Occupant) &&
                            entry.Occupant.Category == CategoryUnit &&
                            GetManhattanDistance(entry.Grid.Grid, gateGrid.Grid) <= 2);
        if (defenderSupport < 2)
        {
            return false;
        }

        var selectedTarget = exteriorAttackers[0];
        targetGrid = selectedTarget;
        exitGrid = exitGrids
            .OrderBy(grid => GetManhattanDistance(grid.Grid, selectedTarget.Grid))
            .First();
        return true;
    }

    private bool TryExecuteAiPostGateBreachSiegeEngineAction(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        _selectedUnit = unit;
        _selectedUnitGrid = sourceGrid;
        FocusCameraOnBattleGrid(sourceGrid);

        // A ladder remains on the exterior wall line to support climbing troops. It
        // only moves away if it is physically occupying the opened gate corridor.
        if (unit.TroopType == TroopLadder && !IsGateGrid(sourceGrid.Grid))
        {
            AppendBattleLog(unit, "AI", BattleFormat(
                "log.ai.post_breach_ladder_hold",
                "Gate breached: ladder holds at {0} outside the wall and will not enter the inner city.",
                sourceGrid));
            MarkUnitActed(unit);
            return true;
        }

        var rearDestination = GetAiRetreatExitAdvanceGrid(sourceGrid, unit);
        if (rearDestination.HasValue)
        {
            AppendBattleLog(unit, "AI", BattleFormat(
                "log.ai.post_breach_withdraw",
                "Gate breached: {0} withdraws {1} -> {2} to clear the gate corridor.",
                FormatTroopType(unit.TroopType),
                sourceGrid,
                rearDestination.Value));
            return TryExecuteBattleActionIntent(
                new BattleActionIntent(BattleActionKind.Move, sourceGrid, rearDestination.Value),
                unit);
        }

        AppendBattleLog(unit, "AI", BattleFormat(
            "log.ai.post_breach_hold",
            "Gate breached: {0} holds at {1}; no clear rear route is currently available.",
            FormatTroopType(unit.TroopType),
            sourceGrid));
        MarkUnitActed(unit);
        return true;
    }

    private bool TryExecuteAiFortressMissionAdvance(IReadOnlyList<(BattleGridKey Grid, BattleOccupantInfo Occupant)> candidates)
    {
        if (candidates.Count == 0 || IsDefenderTeam(candidates[0].Occupant))
        {
            return false;
        }

        var teamName = candidates[0].Occupant.TeamName;
        if (!IsAiAttackerHiddenEnemyFortressMission(teamName))
        {
            return false;
        }

        var advances = new List<(BattleGridKey SourceGrid, BattleOccupantInfo Unit, BattleGridKey DestinationGrid, AiOutpostObjective Objective, int PathEnergy, int PathSteps, bool UsesFullAStar)>();
        foreach (var candidate in candidates.Where(candidate =>
                     candidate.Occupant.Category == CategoryUnit &&
                     candidate.Occupant.TroopType != TroopWorker &&
                     !candidate.Occupant.HasAttackedThisTurn))
        {
            // Path helpers read the selected piece for category-specific movement rules.
            // Bind them to this candidate, never to the last unit clicked or evaluated.
            var previousSelectedUnit = _selectedUnit;
            var previousSelectedUnitGrid = _selectedUnitGrid;
            _selectedUnit = candidate.Occupant;
            _selectedUnitGrid = candidate.Grid;
            foreach (var objective in GetAiOutpostObjectives(candidate.Occupant, []))
            {
                // A hidden defender may already occupy the fortress. In that case, advance to a legal adjacent grid
                // until the defender is discovered instead of treating the occupied fortress as an unreachable mission.
                var missionGoals = new[] { objective.Grid }
                    .Concat(GetMovementNeighbors(objective.Grid).Select(step => step.Grid))
                    .Distinct();
                foreach (var missionGoal in missionGoals)
                {
                    if (TryGetAiPathEndpointToward(
                            candidate.Grid,
                            candidate.Occupant,
                            missionGoal,
                            out var destinationGrid,
                            out var fullPathEnergy,
                            out var fullPathSteps))
                    {
                        advances.Add((candidate.Grid, candidate.Occupant, destinationGrid, objective, fullPathEnergy, fullPathSteps, UsesFullAStar: true));
                        break;
                    }

                    // Friendly units can temporarily block a bridge or narrow road. Keep the mission moving by
                    // selecting this turn's legal progress cell instead of treating that congestion as a permanent dead end.
                    if (TryGetAiLocalFortressAdvance(
                            candidate.Grid,
                            candidate.Occupant,
                            missionGoal,
                            out destinationGrid,
                            out var localPathEnergy,
                            out var localPathSteps))
                    {
                        advances.Add((candidate.Grid, candidate.Occupant, destinationGrid, objective, localPathEnergy, localPathSteps, UsesFullAStar: false));
                        break;
                    }
                }
            }

            _selectedUnit = previousSelectedUnit;
            _selectedUnitGrid = previousSelectedUnitGrid;
        }

        if (advances.Count == 0)
        {
            return false;
        }

        var chosenAdvance = advances
            .OrderByDescending(advance => advance.Objective.Score)
            .ThenBy(advance => advance.PathEnergy)
            .ThenBy(advance => advance.PathSteps)
            .ThenByDescending(advance => GetAiOfficerDecisionTieBreakScore(advance.Unit))
            .First();
        var routedObjective = chosenAdvance.Objective with
        {
            Reason = chosenAdvance.UsesFullAStar
                ? $"mission advance while enemy is hidden; A* energy {chosenAdvance.PathEnergy}, steps {chosenAdvance.PathSteps}"
                : $"mission advance while enemy is hidden; local progress around current blockage, energy {chosenAdvance.PathEnergy}, steps {chosenAdvance.PathSteps}"
        };
        var score = routedObjective.Score + GetOfficerTacticalIntelligence(chosenAdvance.Unit.OfficerName) * 15;
        return TryExecuteAiOutpostMove(
            chosenAdvance.SourceGrid,
            chosenAdvance.Unit,
            chosenAdvance.DestinationGrid,
            routedObjective,
            score,
            GetAiDecisionNoise(chosenAdvance.SourceGrid, chosenAdvance.DestinationGrid, participantCount: 1));
    }

    private bool TryExecuteAiSupply(BattleGridKey sourceGrid, BattleOccupantInfo supplyCart)
    {
        if (supplyCart.TroopType != TroopSupplyCart || supplyCart.Energy < SupplyActionEnergyCost)
        {
            return false;
        }

        var recoveryCount = GetWoundedRecoveryTargets(sourceGrid, supplyCart).Count();
        var moraleCount = GetSupplyMoraleTargets(sourceGrid, supplyCart)
            .Count(target => target.Occupant.Morale.GetValueOrDefault() < DefaultUnitMorale);
        var repairCount = GetSupplyRepairTargets(sourceGrid, supplyCart).Count();
        if (recoveryCount == 0 && moraleCount == 0 && repairCount == 0)
        {
            return false;
        }

        _selectedUnit = supplyCart;
        _selectedUnitGrid = sourceGrid;
        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(
            supplyCart,
            "AI",
            BattleFormat("log.ai.supply", "Decision: supply own team (wounded {0}, morale {1}, repair {2}).", recoveryCount, moraleCount, repairCount));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Supply, sourceGrid, sourceGrid),
            supplyCart);
    }

    private bool TryExecuteAiWeaponResupply(BattleGridKey sourceGrid, BattleOccupantInfo supplyCart)
    {
        var targets = GetWeaponResupplyTargets(sourceGrid, supplyCart).ToList();
        if (targets.Count == 0)
        {
            return false;
        }

        var missingAmmo = targets.Sum(target =>
            target.Occupant.MaxWeaponAmmo.GetValueOrDefault() - target.Occupant.WeaponAmmo.GetValueOrDefault());
        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(
            supplyCart,
            "AI",
            BattleFormat("log.ai.weapon_resupply", "Decision: resupply {0} weapon unit(s), missing ammo {1}.", targets.Count, missingAmmo));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.ResupplyWeapon, sourceGrid, sourceGrid),
            supplyCart);
    }

    private bool TryExecuteAiPrioritySupply(BattleGridKey sourceGrid, BattleOccupantInfo supplyCart)
    {
        if (!HasAiPrioritySupplyTarget(sourceGrid, supplyCart))
        {
            return false;
        }

        AppendBattleLog(supplyCart, "AI", "Priority supply: critical wounded unit, low morale, or damaged key siege engine is adjacent.");
        return TryExecuteAiSupply(sourceGrid, supplyCart);
    }

    private bool HasAiPrioritySupplyTarget(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart)
    {
        return GetWoundedRecoveryTargets(supplyGrid, supplyCart)
                   .Any(target => IsAiVulnerable(target.Occupant)) ||
               GetSupplyMoraleTargets(supplyGrid, supplyCart)
                   .Any(target => target.Occupant.Morale.GetValueOrDefault(DefaultUnitMorale) <= LowMoraleMovePenaltyThreshold) ||
               GetSupplyRepairTargets(supplyGrid, supplyCart)
                   .Any(target => target.Occupant.TroopType == TroopCatapult && target.Occupant.HitPoints < target.Occupant.MaxHitPoints);
    }

    private IEnumerable<AiSupplyPlan> GetAiSupplyPlans(BattleGridKey sourceGrid, BattleOccupantInfo supplyCart)
    {
        if (supplyCart.Energy < SupplyActionEnergyCost)
        {
            yield break;
        }

        var directScore = GetAiSupplyActionScore(sourceGrid, supplyCart);
        if (directScore > 0)
        {
            yield return new AiSupplyPlan(sourceGrid, MoveBeforeSupply: false, AiSupplyActionKind.RecoveryRepair, directScore, "supply adjacent team");
        }

        var directWeaponScore = GetAiWeaponResupplyActionScore(sourceGrid, supplyCart);
        if (directWeaponScore > 0)
        {
            yield return new AiSupplyPlan(sourceGrid, MoveBeforeSupply: false, AiSupplyActionKind.WeaponResupply, directWeaponScore, "resupply adjacent weapon units");
        }

        foreach (var destination in CalculateReachableGrids(sourceGrid, supplyCart.Energy - SupplyActionEnergyCost, GetAvailableMoveRange(supplyCart))
                     .Where(grid => grid != sourceGrid && IsAiSafeMovementDestination(grid)))
        {
            var actionScore = GetAiSupplyActionScore(destination, supplyCart);
            if (actionScore > 0)
            {
                yield return new AiSupplyPlan(
                    destination,
                    MoveBeforeSupply: true,
                    AiSupplyActionKind.RecoveryRepair,
                    actionScore - GetManhattanDistance(sourceGrid.Grid, destination.Grid) * 100,
                    "move and supply team");
            }


            var weaponScore = GetAiWeaponResupplyActionScore(destination, supplyCart);
            if (weaponScore > 0)
            {
                yield return new AiSupplyPlan(
                    destination,
                    MoveBeforeSupply: true,
                    AiSupplyActionKind.WeaponResupply,
                    weaponScore - GetManhattanDistance(sourceGrid.Grid, destination.Grid) * 100,
                    "move and resupply weapon units");
            }
        }
    }

    private int GetAiSupplyActionScore(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart)
    {
        var moraleScore = GetSupplyMoraleTargets(supplyGrid, supplyCart)
            .Sum(target => Mathf.Max(0, DefaultUnitMorale - target.Occupant.Morale.GetValueOrDefault(DefaultUnitMorale)) * 10);
        var recoveryScore = GetWoundedRecoveryTargets(supplyGrid, supplyCart)
            .Sum(target => Mathf.Min(SupplyCartWoundedRecoveryAmount, target.Occupant.WoundedTroops) * 2);
        var repairScore = GetSupplyRepairTargets(supplyGrid, supplyCart)
            .Where(target => target.Occupant.TroopType != TroopSupplyCart)
            .Sum(target => Mathf.Min(SupplyCartRepairAmount, target.Occupant.MaxHitPoints - target.Occupant.HitPoints) * 2);
        return moraleScore + recoveryScore + repairScore;
    }

    private int GetAiWeaponResupplyActionScore(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart)
    {
        return GetWeaponResupplyTargets(supplyGrid, supplyCart).Sum(target =>
        {
            var currentAmmo = target.Occupant.WeaponAmmo.GetValueOrDefault();
            var maxAmmo = target.Occupant.MaxWeaponAmmo.GetValueOrDefault();
            if (maxAmmo <= 0 || currentAmmo >= maxAmmo)
            {
                return 0;
            }

            var missingAmmo = maxAmmo - currentAmmo;
            var score = GetBaseAttackDamage(target.Occupant) * missingAmmo / maxAmmo;
            if (currentAmmo == 0)
            {
                score += AiAmmoDepletedResupplyBonus;
            }

            if (target.Occupant.TroopType == TroopCatapult)
            {
                score += AiCatapultAmmoResupplyBonus;
            }

            return score;
        });
    }

    private bool TryGetAiSupplyApproach(
        BattleGridKey sourceGrid,
        BattleOccupantInfo supplyCart,
        out BattleGridKey destinationGrid,
        out BattleGridKey supplyActionGrid,
        out int supplyScore,
        out int fullPathEnergyCost,
        out int fullPathSteps)
    {
        destinationGrid = default;
        supplyActionGrid = default;
        supplyScore = 0;
        fullPathEnergyCost = 0;
        fullPathSteps = 0;
        if (_mapData == null)
        {
            return false;
        }

        var candidates = new List<(BattleGridKey ActionGrid, BattleGridKey DestinationGrid, int Score, int FullPathEnergy, int FullPathSteps)>();
        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var actionGrid = ToGroundGridKey(new Vector2I(x, y));
                if (actionGrid == sourceGrid || HasBlockingOccupant(actionGrid))
                {
                    continue;
                }

                var score = Math.Max(
                    GetAiSupplyActionScore(actionGrid, supplyCart),
                    GetAiWeaponResupplyActionScore(actionGrid, supplyCart));
                if (score <= 0 ||
                    !TryGetAiPathEndpointToward(sourceGrid, supplyCart, actionGrid, out var nextDestination, out var pathEnergy, out var pathSteps))
                {
                    continue;
                }

                candidates.Add((actionGrid, nextDestination, score, pathEnergy, pathSteps));
            }
        }

        var best = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.FullPathEnergy)
            .ThenBy(candidate => candidate.FullPathSteps)
            .FirstOrDefault();
        if (best == default)
        {
            return false;
        }

        destinationGrid = best.DestinationGrid;
        supplyActionGrid = best.ActionGrid;
        supplyScore = best.Score;
        fullPathEnergyCost = best.FullPathEnergy;
        fullPathSteps = best.FullPathSteps;
        return true;
    }

    private bool TryExecuteAiSupplyPlan(BattleGridKey sourceGrid, BattleOccupantInfo supplyCart, AiSupplyPlan plan, int noise)
    {
        if (!plan.MoveBeforeSupply)
        {
            AppendBattleLog(supplyCart, "AI", BattleFormat("log.ai.supply_plan", "Decision: {0} (score {1}, variance {2}).", GetAiSupplyPlanReasonText(plan), plan.Score, noise));
            return plan.Kind == AiSupplyActionKind.WeaponResupply
                ? TryExecuteAiWeaponResupply(sourceGrid, supplyCart)
                : TryExecuteAiSupply(sourceGrid, supplyCart);
        }

        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(supplyCart, "AI", BattleFormat("log.ai.supply_move_plan", "Decision: move {0} -> {1}, reserve {2} energy, then {3} (score {4}, variance {5}).", sourceGrid, plan.ActionGrid, SupplyActionEnergyCost, GetAiSupplyPlanReasonText(plan), plan.Score, noise));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(
                BattleActionKind.Move,
                sourceGrid,
                plan.ActionGrid,
                ReservedEnergy: SupplyActionEnergyCost,
                MarkActedAfterMove: false),
            supplyCart,
            () =>
            {
                if (_selectedUnit != null && _selectedUnitGrid.HasValue)
                {
                    if (plan.Kind == AiSupplyActionKind.WeaponResupply)
                    {
                        TryExecuteAiWeaponResupply(_selectedUnitGrid.Value, _selectedUnit);
                    }
                    else
                    {
                        TryExecuteAiSupply(_selectedUnitGrid.Value, _selectedUnit);
                    }
                }
            });
    }

    private string GetAiSupplyPlanReasonText(AiSupplyPlan plan)
    {
        return plan.Kind == AiSupplyActionKind.WeaponResupply
            ? BattleText(
                plan.MoveBeforeSupply ? "log.ai.supply_reason_move_weapon" : "log.ai.supply_reason_weapon",
                plan.MoveBeforeSupply ? "move and resupply weapon units" : "resupply adjacent weapon units")
            : BattleText(
                plan.MoveBeforeSupply ? "log.ai.supply_reason_move_team" : "log.ai.supply_reason_team",
                plan.MoveBeforeSupply ? "move and supply team" : "supply adjacent team");
    }

    private bool TryExecuteAiSupplyMove(BattleGridKey sourceGrid, BattleOccupantInfo supplyCart)
    {
        var supportGrids = GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName == supplyCart.TeamName && NeedsAiSupplySupport(entry.Occupant, supplyCart))
            .Select(entry => entry.Grid)
            .ToList();
        if (supportGrids.Count == 0)
        {
            return false;
        }

        // A* movement helpers still read the active unit while planning the route.
        _selectedUnit = supplyCart;
        _selectedUnitGrid = sourceGrid;
        if (!TryGetAiSupplyApproach(
                sourceGrid,
                supplyCart,
                out var destination,
                out var supplyActionGrid,
                out var supplyScore,
                out var fullPathEnergyCost,
                out var fullPathSteps))
        {
            return false;
        }

        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(supplyCart, "AI", BattleFormat("log.ai.supply_route", "Decision: move {0} -> {1}; A* supply route to {2} (support score {3}, full path energy {4}, steps {5}).", sourceGrid, destination, supplyActionGrid, supplyScore, fullPathEnergyCost, fullPathSteps));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Move, sourceGrid, destination),
            supplyCart);
    }

    private static bool NeedsAiSupplySupport(BattleOccupantInfo candidate, BattleOccupantInfo supplyCart)
    {
        if (candidate.Marker == supplyCart.Marker)
        {
            return candidate.Category == CategorySiegeEngine && candidate.HitPoints < candidate.MaxHitPoints;
        }

        return candidate.WoundedTroops > 0 ||
               candidate.Morale.HasValue && candidate.Morale.Value < DefaultUnitMorale ||
               candidate.Category == CategorySiegeEngine && candidate.HitPoints < candidate.MaxHitPoints ||
               candidate.WeaponAmmo.HasValue &&
               candidate.MaxWeaponAmmo.HasValue &&
               candidate.WeaponAmmo.Value < candidate.MaxWeaponAmmo.Value;
    }

    private static bool IsAiAmmoDepletedCatapult(BattleOccupantInfo unit)
    {
        return unit.Category == CategorySiegeEngine &&
               unit.TroopType == TroopCatapult &&
               unit.MaxWeaponAmmo.HasValue &&
               unit.WeaponAmmo.GetValueOrDefault() <= 0;
    }

    private static int GetAiFallbackMovementPriority(BattleOccupantInfo unit)
    {
        if (unit.TroopType == TroopSupplyCart)
        {
            return 3;
        }

        if (unit.Category == CategorySiegeEngine)
        {
            return 2;
        }

        return unit.TroopType == TroopWorker ? 1 : 0;
    }

   private int GetSupplyActionTargetCount(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart)
   {
       return GetSupplyMoraleTargets(supplyGrid, supplyCart).Count() +
              GetWoundedRecoveryTargets(supplyGrid, supplyCart).Count() +
              GetSupplyRepairTargets(supplyGrid, supplyCart).Count();
   }
    private string BuildAiOpeningPlanLog()
    {
        var actingUnits = GetActingBattlePieces().ToList();
        var vulnerableCount = actingUnits.Count(entry => IsAiVulnerable(entry.Occupant));
        var outpostSummary = BattleText("log.ai.opening_eliminate", "eliminate enemy");
        if (_mapData?.ScenarioDefinition.ScenarioType == BattleScenarioType.FieldBattle)
        {
            var owner = _currentTurnSide == BattleTurnSide.TeamB ? BattleOutpostOwner.Defender : BattleOutpostOwner.Attacker;
            var unresolvedOutposts = Enumerable.Range(0, BattleMapData.Height)
                .SelectMany(y => Enumerable.Range(0, BattleMapData.Width).Select(x => _mapData.GetCell(x, y)))
                .Count(cell => cell.IsDefenseOutpost && cell.DefenseOutpostOwner != owner);
            outpostSummary = unresolvedOutposts > 0
                ? BattleFormat("log.ai.opening_contest_fortress", "eliminate enemy or contest {0} fortress(es)", unresolvedOutposts)
                : BattleText("log.ai.opening_hold_fortress", "hold occupied fortresses and eliminate enemy");
        }

        var bridgeSummary = BattleText("log.ai.opening_no_bridge", "no bridge route is worthwhile");
        foreach (var (grid, worker) in actingUnits.Where(entry => entry.Occupant.TroopType == TroopWorker))
        {
            if (TryGetAiBridgeEngineeringPlan(grid, worker, out var bridgePlan))
            {
                bridgeSummary = BattleFormat("log.ai.opening_bridge", "bridge at {0} toward {1}, route -{2}", bridgePlan.WorkGrid, bridgePlan.ObjectiveGrid, bridgePlan.PathReduction);
                break;
            }
        }

        return BattleFormat("log.ai.opening_plan", "Opening plan: {0}; {1}; vulnerable teams {2}/{3}. Intelligence favors cover, supply, and tactical objectives; Combat favors decisive attacks.", outpostSummary, bridgeSummary, vulnerableCount, actingUnits.Count);
    }

    private bool TryExecuteBestAiSurvivalAction(IReadOnlyList<(BattleGridKey Grid, BattleOccupantInfo Occupant)> candidates)
    {
        var actions = new List<AiSurvivalAction>();
        foreach (var (sourceGrid, unit) in candidates)
        {
            if (unit.TroopType == TroopSupplyCart || !IsAiVulnerable(unit))
            {
                continue;
            }

            var intelligence = GetOfficerTacticalIntelligence(unit.OfficerName);
            var threat = GetAiThreatScore(sourceGrid, unit);
            var immediateAttackScore = GetAiBestOffensiveActionScore(sourceGrid, unit);
            var enemyFoodPressure = GetAiEnemyFoodPressureScore(unit.TeamName);
            var safetyAction = GetAiBestSafetyMove(sourceGrid, unit, intelligence, enemyFoodPressure);
            if (safetyAction.HasValue && safetyAction.Value.Score > immediateAttackScore)
            {
                actions.Add(safetyAction.Value);
            }

            if (!IsAiAttackerHiddenEnemyFortressMission(unit.TeamName) &&
                CanUseGuard(unit) &&
                IsAiDefensivePosition(sourceGrid, unit.TeamName))
            {
                var guardAction = new AiSurvivalAction(
                    sourceGrid,
                    unit,
                    null,
                    AiSurvivalActionKind.Guard,
                    AiGuardSurvivalScore + threat + intelligence * 8 + enemyFoodPressure,
                    "guard favorable Building/Fortress position");
                if (guardAction.Score > immediateAttackScore)
                {
                    actions.Add(guardAction);
                }
            }

            if (unit.HitPoints <= unit.MaxHitPoints * AiCriticalHealthRatio && threat > immediateAttackScore)
            {
                var retreatDestination = GetAiRetreatExitAdvanceGrid(sourceGrid, unit);
                var retreatAction = new AiSurvivalAction(
                    sourceGrid,
                    unit,
                    retreatDestination,
                    CanRetreatFromGrid(sourceGrid, unit)
                        ? AiSurvivalActionKind.Retreat
                        : AiSurvivalActionKind.AdvanceToRetreatExit,
                    AiRetreatSurvivalScore + threat + intelligence * 12,
                    CanRetreatFromGrid(sourceGrid, unit)
                        ? "critical strength and projected enemy threat; reached exit"
                        : "critical strength and projected enemy threat; advancing to exit");
                if (retreatAction.Score > immediateAttackScore)
                {
                    if (retreatAction.Kind == AiSurvivalActionKind.Retreat || retreatDestination.HasValue)
                    {
                        actions.Add(retreatAction);
                    }
                }
            }

            if (!IsAiAttackerHiddenEnemyFortressMission(unit.TeamName) &&
                IsAiDefensivePosition(sourceGrid, unit.TeamName) &&
                threat > immediateAttackScore &&
                unit.Energy < NormalAttackEnergyCost)
            {
                var stayAction = new AiSurvivalAction(
                    sourceGrid,
                    unit,
                    null,
                    AiSurvivalActionKind.Stay,
                    AiBuildingCoverSurvivalScore + threat + intelligence * 6 + enemyFoodPressure,
                    "remain in favorable defensive position");
                if (stayAction.Score > immediateAttackScore)
                {
                    actions.Add(stayAction);
                }
            }
        }

        if (actions.Count == 0)
        {
            return false;
        }

        var chosenAction = actions
            .OrderByDescending(action => action.Score)
            .ThenByDescending(action => GetAiOfficerDecisionTieBreakScore(action.Unit))
            .First();
        return ExecuteAiSurvivalAction(chosenAction);
    }

    private AiSurvivalAction? GetAiBestSafetyMove(BattleGridKey sourceGrid, BattleOccupantInfo unit, int intelligence, int enemyFoodPressure)
    {
        var supplyGrids = GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName == unit.TeamName && entry.Occupant.TroopType == TroopSupplyCart)
            .Select(entry => entry.Grid)
            .ToList();
        var candidates = CalculateReachableGrids(sourceGrid, GetAvailableMoveEnergy(unit), GetAvailableMoveRange(unit))
            .Where(IsAiSafeMovementDestination)
            .Select(destinationGrid => new BattleAiPlanner.SafetyMoveCandidate<BattleGridKey>(
                destinationGrid,
                GetAiThreatScore(destinationGrid, unit),
                IsAiDefensivePosition(destinationGrid, unit.TeamName),
                supplyGrids.Count == 0
                    ? int.MaxValue
                    : supplyGrids.Min(supplyGrid => GetManhattanDistance(destinationGrid.Grid, supplyGrid.Grid)),
                GetManhattanDistance(sourceGrid.Grid, destinationGrid.Grid)));
        var plan = BattleAiPlanner.GetBestSafetyMove(
            candidates,
            intelligence,
            enemyFoodPressure,
            AiBuildingCoverSurvivalScore,
            AiSupplyApproachSurvivalScore);
        return plan.HasValue
            ? new AiSurvivalAction(sourceGrid, unit, plan.Value.Destination, AiSurvivalActionKind.MoveToSafety, plan.Value.Score, plan.Value.Reason)
            : null;
    }

    private BattleGridKey? GetAiRetreatExitAdvanceGrid(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        var exitGrids = GetRetreatExitGrids(unit)
            .Where(IsWithinMap)
            .Select(GetDefaultGridKey)
            .ToList();
        if (exitGrids.Count == 0)
        {
            return null;
        }

        var currentDistance = exitGrids.Min(exitGrid => GetManhattanDistance(sourceGrid.Grid, exitGrid.Grid));
        var candidates = CalculateReachableGrids(sourceGrid, GetAvailableMoveEnergy(unit), GetAvailableMoveRange(unit))
            .Select(grid => new
            {
                Grid = grid,
                Distance = exitGrids.Min(exitGrid => GetManhattanDistance(grid.Grid, exitGrid.Grid)),
                Threat = GetAiThreatScore(grid, unit)
            })
            .Where(candidate => candidate.Distance < currentDistance)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Threat)
            .ThenBy(candidate => candidate.Grid.Y)
            .ThenBy(candidate => candidate.Grid.X)
            .ToList();
        return candidates.Count == 0 ? null : candidates[0].Grid;
    }

    private bool ExecuteAiSurvivalAction(AiSurvivalAction action)
    {
        var intelligence = GetOfficerTacticalIntelligence(action.Unit.OfficerName);
        var combat = GetOfficerBattleAttribute(action.Unit.OfficerName);
        _selectedUnit = action.Unit;
        _selectedUnitGrid = action.SourceGrid;
        FocusCameraOnBattleGrid(action.SourceGrid);
        AppendBattleLog(action.Unit, "AI", BattleFormat(
            "log.ai.survival_decision",
            "Survival decision: {0}; {1} (score {2}, intelligence {3}, combat {4}).",
            FormatAiSurvivalActionKind(action.Kind),
            FormatAiSurvivalReason(action.Reason),
            action.Score,
            intelligence,
            combat));
        switch (action.Kind)
        {
            case AiSurvivalActionKind.MoveToSafety when action.DestinationGrid.HasValue:
                return TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.Move, action.SourceGrid, action.DestinationGrid.Value),
                    action.Unit);
            case AiSurvivalActionKind.Guard:
                return TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.Guard, action.SourceGrid, action.SourceGrid),
                    action.Unit);
            case AiSurvivalActionKind.Stay:
                MarkUnitActed(action.Unit);
                return true;
            case AiSurvivalActionKind.Retreat:
                return TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.Retreat, action.SourceGrid, action.SourceGrid),
                    action.Unit);
            case AiSurvivalActionKind.AdvanceToRetreatExit when action.DestinationGrid.HasValue:
                return TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.Move, action.SourceGrid, action.DestinationGrid.Value),
                    action.Unit);
            default:
                return false;
        }
    }

    private string FormatAiSurvivalActionKind(AiSurvivalActionKind kind)
    {
        return kind switch
        {
            AiSurvivalActionKind.MoveToSafety => BattleText("log.ai.survival_move_safety", "Move to safety"),
            AiSurvivalActionKind.Guard => BattleText("log.ai.survival_guard", "Guard"),
            AiSurvivalActionKind.Stay => BattleText("log.ai.survival_stay", "Hold position"),
            AiSurvivalActionKind.Retreat => BattleText("log.ai.survival_retreat", "Retreat"),
            AiSurvivalActionKind.AdvanceToRetreatExit => BattleText("log.ai.survival_advance_exit", "Advance to retreat exit"),
            _ => kind.ToString()
        };
    }

    private string FormatAiSurvivalReason(string reason)
    {
        return reason switch
        {
            "critical strength and projected enemy threat; reached exit" => BattleText("log.ai.survival_reason_critical_exit", "critical strength and projected enemy threat; reached exit"),
            "critical strength and projected enemy threat; advancing to exit" => BattleText("log.ai.survival_reason_critical_advance", "critical strength and projected enemy threat; advancing to exit"),
            "guard favorable Building/Fortress position" => BattleText("log.ai.survival_reason_guard_cover", "guard favorable Building/Fortress position"),
            "remain in favorable defensive position" => BattleText("log.ai.survival_reason_hold_cover", "remain in favorable defensive position"),
            "move to Building/Fortress cover" => BattleText("log.ai.survival_reason_move_cover", "move to Building/Fortress cover"),
            "move next to supply cart" => BattleText("log.ai.survival_reason_move_supply", "move next to supply cart"),
            "move to Building/Fortress cover and supply cart" => BattleText("log.ai.survival_reason_move_cover_supply", "move to Building/Fortress cover and supply cart"),
            _ => reason
        };
    }

    private int GetAiBestOffensiveActionScore(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        var fireScores = GetAiFirePlans(sourceGrid, unit).Select(plan => plan.Score).ToList();
        fireScores.AddRange(GetAiExtinguishPlans(sourceGrid, unit).Select(plan => plan.Score));
        if (unit.Energy < NormalAttackEnergyCost || !CanUseAttackCommand(unit))
        {
            return fireScores.Count == 0
                ? 0
                : fireScores.Max() + GetAiCombatDecisionScore(unit);
        }

        var scores = CalculateAttackableGrids(sourceGrid, unit)
            .Where(grid => _occupantsByGrid.TryGetValue(grid, out var occupants) && GetAiAttackTargetForAttack(occupants, unit.TeamName, grid) != null)
            .Select(grid =>
            {
                var target = GetAiAttackTargetForAttack(_occupantsByGrid[grid], unit.TeamName, grid)!;
                return GetAiOffensiveActionScore(grid, unit.TeamName, target, GetAttackDamageAgainst(unit, target), supportCount: 0);
            })
            .ToList();

        scores.AddRange(
            CalculateReachableGrids(sourceGrid, unit.Energy - NormalAttackEnergyCost, GetAvailableMoveRange(unit))
                .Where(grid => grid != sourceGrid && IsAiSafeMovementDestination(grid))
                .SelectMany(grid => CalculateAttackableGrids(grid, unit)
                    .Where(targetGrid => _occupantsByGrid.TryGetValue(targetGrid, out var occupants) &&
                                         GetAiAttackTargetForAttack(occupants, unit.TeamName, targetGrid) != null)
                    .Select(targetGrid =>
                    {
                        var target = GetAiAttackTargetForAttack(_occupantsByGrid[targetGrid], unit.TeamName, targetGrid)!;
                        return GetAiOffensiveActionScore(targetGrid, unit.TeamName, target, GetAttackDamageAgainst(unit, target), supportCount: 0);
                    })));

        scores.AddRange(fireScores);

        var previousUnit = _selectedUnit;
        var previousUnitGrid = _selectedUnitGrid;
        _selectedUnit = unit;
        _selectedUnitGrid = sourceGrid;
        try
        {
            if (TryGetBestUnionAttackCandidate(out var unionCandidate) &&
                _occupantsByGrid.TryGetValue(unionCandidate.TargetGrid, out var occupants))
            {
                var target = GetAiAttackTargetForAttack(occupants, unit.TeamName, unionCandidate.TargetGrid);
                if (target != null)
                {
                    scores.Add(GetAiOffensiveActionScore(
                        unionCandidate.TargetGrid,
                        unit.TeamName,
                        target,
                        GetUnionAttackDamage(unionCandidate.Participants, target),
                        unionCandidate.Participants.Count - 1));
                }
            }
        }
        finally
        {
            _selectedUnit = previousUnit;
            _selectedUnitGrid = previousUnitGrid;
        }

        return scores.Count == 0
            ? 0
            : scores.Max() + GetAiCombatDecisionScore(unit);
    }

    private IEnumerable<AiExtinguishPlan> GetAiExtinguishPlans(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        // CanUseExtinguishStrategy requires a general/officer battle team, so siege and logistics vehicles cannot use this.
        if (!CanUseExtinguishStrategy(unit, sourceGrid))
        {
            yield break;
        }

        var projectedDamagePerTurn = GetFireDamagePerTurn(GetCurrentBattleWeather());
        foreach (var targetGrid in CalculateExtinguishStrategyTargetGrids(sourceGrid, unit))
        {
            if (!_activeFireByGrid.TryGetValue(targetGrid, out var fireState))
            {
                continue;
            }

            var protectedUnits = 0;
            var projectedDamage = 0;
            if (_occupantsByGrid.TryGetValue(targetGrid, out var occupants))
            {
                foreach (var occupant in occupants.Where(occupant => occupant.TeamName == unit.TeamName && IsBattlePiece(occupant)))
                {
                    protectedUnits++;
                    projectedDamage += Math.Min(occupant.HitPoints, projectedDamagePerTurn * fireState.RemainingTurns);
                }
            }

            var score = projectedDamage * 4;
            if (IsAiOwnedOutpost(targetGrid, unit.TeamName))
            {
                score += 1800;
            }

            if (_occupantsByGrid.TryGetValue(targetGrid, out var threatenedOccupants) &&
                threatenedOccupants.Any(occupant => occupant.TeamName == unit.TeamName && IsGeneralCountedPiece(occupant.Category, occupant.OfficerName)))
            {
                score += 2200;
            }

            if (score <= 0)
            {
                continue;
            }

            score += GetOfficerTacticalIntelligence(unit.OfficerName) * 6;
            yield return new AiExtinguishPlan(targetGrid, score, protectedUnits, projectedDamage);
        }
    }

    private IEnumerable<AiFirePlan> GetAiFirePlans(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        if (!CanUseFireStrategy(unit) || _mapData == null)
        {
            yield break;
        }

        var weather = GetCurrentBattleWeather();
        var fireDamagePerTurn = GetFireDamagePerTurn(weather);
        foreach (var targetGrid in CalculateFireStrategyTargetGrids(sourceGrid, unit))
        {
            var targetCell = _mapData.GetCell(targetGrid.X, targetGrid.Y);
            var duration = GetInitialFireDuration(weather, targetCell);
            var enemyDamage = 0;
            var friendlyDamage = 0;
            var enemyTargets = 0;
            var score = 0;

            ScoreAiFireGrid(
                targetGrid,
                unit.TeamName,
                fireDamagePerTurn,
                duration,
                weightPercent: 100,
                ref score,
                ref enemyDamage,
                ref friendlyDamage,
                ref enemyTargets);

            var spreadTargets = 0;
            if (weather != BattleWeatherType.Rain && duration > 1)
            {
                foreach (var spreadGrid in GetFireSpreadTargets(targetGrid))
                {
                    spreadTargets++;
                    ScoreAiFireGrid(
                        spreadGrid,
                        unit.TeamName,
                        fireDamagePerTurn,
                        Math.Max(1, duration - 1),
                        weightPercent: 50,
                        ref score,
                        ref enemyDamage,
                        ref friendlyDamage,
                        ref enemyTargets);
                }
            }

            // Fire is indiscriminate. Do not burn a friendly position even when the enemy value looks tempting.
            if (friendlyDamage > 0 || enemyTargets == 0)
            {
                continue;
            }

            var intelligenceBonus = GetOfficerTacticalIntelligence(unit.OfficerName) * 8;
            var windBonus = weather == BattleWeatherType.Sunny && GetCurrentBattleWindPower() == BattleWindPower.Strong
                ? 180
                : 0;
            yield return new AiFirePlan(
                targetGrid,
                score + intelligenceBonus + windBonus,
                enemyDamage,
                friendlyDamage,
                enemyTargets,
                spreadTargets);
        }
    }

    private void ScoreAiFireGrid(
        BattleGridKey grid,
        string actingTeamName,
        int damagePerTurn,
        int turns,
        int weightPercent,
        ref int score,
        ref int enemyDamage,
        ref int friendlyDamage,
        ref int enemyTargets)
    {
        if (!_occupantsByGrid.TryGetValue(grid, out var occupants))
        {
            return;
        }

        foreach (var occupant in occupants)
        {
            if (!IsBattlePiece(occupant))
            {
                continue;
            }

            var projectedDamage = Math.Min(occupant.HitPoints, damagePerTurn * turns);
            var weightedDamage = projectedDamage * weightPercent / 100;
            if (occupant.TeamName == actingTeamName)
            {
                friendlyDamage += weightedDamage;
                continue;
            }

            if (IsHiddenFromSide(occupant, actingTeamName))
            {
                continue;
            }

            enemyTargets++;
            enemyDamage += weightedDamage;
            score += weightedDamage;
            if (projectedDamage >= occupant.HitPoints)
            {
                score += 5000;
            }

            if (occupant.Category == CategoryUnit && !string.IsNullOrWhiteSpace(occupant.OfficerName))
            {
                score += 200;
            }
        }
    }

    private int GetAiThreatScore(BattleGridKey grid, BattleOccupantInfo threatenedUnit)
    {
        var threat = 0;
        foreach (var (enemyGrid, enemy) in GetAllBattlePieces().Where(entry => entry.Occupant.TeamName != threatenedUnit.TeamName))
        {
            if (CanAiUnitThreatenGrid(enemyGrid, enemy, grid, threatenedUnit))
            {
                threat += GetAttackDamageAgainst(enemy, threatenedUnit);
            }
        }

        return threat;
    }

    private bool CanAiUnitThreatenGrid(BattleGridKey enemyGrid, BattleOccupantInfo enemy, BattleGridKey targetGrid, BattleOccupantInfo threatenedUnit)
    {
        if (IsHiddenFromSide(threatenedUnit, enemy.TeamName))
        {
            return false;
        }

        var projectedEnemy = enemy with
        {
            Energy = GetTeamEnergyCap(enemy.TeamName),
            HasAttackedThisTurn = false,
            RemainingMoveRange = GetTeamMoveRangeCap(enemy)
        };
        if (!CanUseAttackCommand(projectedEnemy))
        {
            return false;
        }

        if (CalculateAttackableGrids(enemyGrid, projectedEnemy).Contains(targetGrid))
        {
            return true;
        }

        return CalculateReachableGrids(
                enemyGrid,
                projectedEnemy.Energy - NormalAttackEnergyCost,
                GetAvailableMoveRange(projectedEnemy))
            .Any(attackGrid => CalculateAttackableGrids(attackGrid, projectedEnemy).Contains(targetGrid));
    }

    private bool IsAiDefensivePosition(BattleGridKey grid, string teamName)
    {
        if (_mapData == null || grid.Level != 0 || !IsWithinMap(grid.Grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        var ownsOutpost = cell.IsDefenseOutpost &&
                          cell.DefenseOutpostOwner == (IsDefenderTeamName(teamName) ? BattleOutpostOwner.Defender : BattleOutpostOwner.Attacker);
        return IsBuildingCoverActive(grid) || ownsOutpost;
    }

    private bool HasAiDirectAttackOpportunity(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        return unit.Energy >= NormalAttackEnergyCost &&
               CanUseAttackCommand(unit) &&
               CalculateAttackableGrids(sourceGrid, unit).Any(targetGrid =>
                   _occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants) &&
                   GetAiAttackTargetForAttack(targetOccupants, unit.TeamName, targetGrid) != null);
    }

    private bool IsAiOwnedOutpost(BattleGridKey grid, string teamName)
    {
        if (_mapData == null || grid.Level != 0 || !IsWithinMap(grid.Grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return cell.IsDefenseOutpost &&
               cell.DefenseOutpostOwner == (IsDefenderTeamName(teamName) ? BattleOutpostOwner.Defender : BattleOutpostOwner.Attacker);
    }

   private static bool IsAiVulnerable(BattleOccupantInfo unit)
   {
       return unit.MaxHitPoints > 0 && unit.HitPoints <= unit.MaxHitPoints * AiVulnerableHealthRatio;
   }
    private bool TryExecuteBestAiOffensiveAction(IReadOnlyList<(BattleGridKey Grid, BattleOccupantInfo Occupant)> candidates)
    {
        var actions = new List<AiOffensiveAction>();
        foreach (var (sourceGrid, unit) in candidates)
        {
            foreach (var extinguishPlan in GetAiExtinguishPlans(sourceGrid, unit))
            {
                actions.Add(new AiOffensiveAction(
                    sourceGrid,
                    unit,
                    extinguishPlan.TargetGrid,
                    null,
                    null,
                    null,
                    null,
                    extinguishPlan.Score,
                    GetAiDecisionNoise(sourceGrid, extinguishPlan.TargetGrid, participantCount: 1))
                {
                    ExtinguishPlan = extinguishPlan
                });
            }

            foreach (var firePlan in GetAiFirePlans(sourceGrid, unit))
            {
                actions.Add(new AiOffensiveAction(
                    sourceGrid,
                    unit,
                    firePlan.TargetGrid,
                    null,
                    null,
                    null,
                    null,
                    firePlan.Score,
                    GetAiDecisionNoise(sourceGrid, firePlan.TargetGrid, participantCount: 1))
                {
                    FirePlan = firePlan
                });
            }

            if (unit.Energy >= NormalAttackEnergyCost && CanUseAttackCommand(unit))
            {
                foreach (var targetGrid in CalculateAttackableGrids(sourceGrid, unit))
                {
                    if (!_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
                    {
                        continue;
                    }

                    var target = GetAiAttackTargetForAttack(targetOccupants, unit.TeamName, targetGrid);
                    if (target != null)
                    {
                        actions.Add(new AiOffensiveAction(
                            sourceGrid,
                            unit,
                            targetGrid,
                            null,
                            null,
                            null,
                            null,
                            GetAiOffensiveActionScore(targetGrid, unit.TeamName, target, GetAttackDamageAgainst(unit, target), supportCount: 0),
                            GetAiDecisionNoise(sourceGrid, targetGrid, participantCount: 1)));
                    }
                }
            }

            _selectedUnit = unit;
            _selectedUnitGrid = sourceGrid;
            if (TryGetBestUnionAttackCandidate(out var unionCandidate) &&
                _occupantsByGrid.TryGetValue(unionCandidate.TargetGrid, out var unionTargetOccupants))
            {
                var unionTarget = GetAiAttackTargetForAttack(unionTargetOccupants, unit.TeamName, unionCandidate.TargetGrid);
                if (unionTarget != null)
                {
                    actions.Add(new AiOffensiveAction(
                        sourceGrid,
                        unit,
                        unionCandidate.TargetGrid,
                        unionCandidate,
                        null,
                        null,
                        null,
                        GetAiOffensiveActionScore(unionCandidate.TargetGrid, unit.TeamName, unionTarget, GetUnionAttackDamage(unionCandidate.Participants, unionTarget), unionCandidate.Participants.Count - 1),
                        GetAiDecisionNoise(sourceGrid, unionCandidate.TargetGrid, unionCandidate.Participants.Count)));
                }
            }

            if (TryGetAiHideAmbushScore(sourceGrid, unit, out var hideScore))
            {
                actions.Add(new AiOffensiveAction(
                    sourceGrid,
                    unit,
                    sourceGrid,
                    null,
                    null,
                    null,
                    null,
                    hideScore,
                    GetAiDecisionNoise(sourceGrid, sourceGrid, participantCount: 1))
                {
                    IsHideAction = true
                });
            }

            var enemyFoodPressure = GetAiEnemyFoodPressureScore(unit.TeamName);
            var hasDirectAttack = HasAiDirectAttackOpportunity(sourceGrid, unit);
            if (!IsAiAttackerHiddenEnemyFortressMission(unit.TeamName) &&
                !hasDirectAttack &&
                CanUseGuard(unit) &&
                IsAiDefensivePosition(sourceGrid, unit.TeamName) &&
                (enemyFoodPressure > 0 || IsAiOwnedOutpost(sourceGrid, unit.TeamName)))
            {
                actions.Add(new AiOffensiveAction(
                    sourceGrid,
                    unit,
                    sourceGrid,
                    null,
                    null,
                    null,
                    null,
                    AiGuardSurvivalScore + enemyFoodPressure + (IsAiOwnedOutpost(sourceGrid, unit.TeamName) ? AiOutpostThreatObjectiveScore : 0) + GetOfficerTacticalIntelligence(unit.OfficerName) * 8,
                    GetAiDecisionNoise(sourceGrid, sourceGrid, participantCount: 1))
                {
                    IsGuardAction = true
                });
            }

            if (unit.TroopType == TroopSupplyCart)
            {
                foreach (var supplyPlan in GetAiSupplyPlans(sourceGrid, unit))
                {
                    actions.Add(new AiOffensiveAction(
                        sourceGrid,
                        unit,
                        supplyPlan.ActionGrid,
                        null,
                        null,
                        null,
                        null,
                        supplyPlan.Score,
                        GetAiDecisionNoise(sourceGrid, supplyPlan.ActionGrid, participantCount: 1))
                    {
                        SupplyPlan = supplyPlan
                    });
                }

                continue;
            }

            // An empty catapult should recover its ranged role instead of joining
            // fortress or generic close-approach movement at a bridge bottleneck.
            if (IsAiAmmoDepletedCatapult(unit))
            {
                continue;
            }

            if (unit.HasAttackedThisTurn)
            {
                continue;
            }

            foreach (var repairPlan in GetAiBridgeRepairPlans(sourceGrid, unit))
            {
                actions.Add(new AiOffensiveAction(
                    sourceGrid,
                    unit,
                    repairPlan.TargetGrid,
                    null,
                    null,
                    null,
                    null,
                    repairPlan.Score,
                    GetAiDecisionNoise(sourceGrid, repairPlan.TargetGrid, participantCount: 1))
                {
                    BridgeRepairPlan = repairPlan
                });
            }

            if (unit.Energy >= NormalAttackEnergyCost && CanUseAttackCommand(unit))
            {
                foreach (var moveAttackPlan in CalculateReachableGrids(sourceGrid, unit.Energy - NormalAttackEnergyCost, GetAvailableMoveRange(unit))
                             .Where(grid => grid != sourceGrid && IsAiSafeMovementDestination(grid))
                             .SelectMany(grid => CalculateAttackableGrids(grid, unit)
                                 .Where(targetGrid => _occupantsByGrid.TryGetValue(targetGrid, out var occupants) &&
                                                      GetAiAttackTargetForAttack(occupants, unit.TeamName, targetGrid) != null)
                                 .Select(targetGrid => (Destination: grid, Target: targetGrid))))
                {
                    var target = GetAiAttackTargetForAttack(_occupantsByGrid[moveAttackPlan.Target], unit.TeamName, moveAttackPlan.Target)!;
                    actions.Add(new AiOffensiveAction(
                        sourceGrid,
                        unit,
                        moveAttackPlan.Target,
                        null,
                        null,
                        null,
                        null,
                        GetAiOffensiveActionScore(moveAttackPlan.Target, unit.TeamName, target, GetAttackDamageAgainst(unit, target), supportCount: 0),
                        GetAiDecisionNoise(sourceGrid, moveAttackPlan.Target, participantCount: 1))
                    {
                        MoveAttackDestination = moveAttackPlan.Destination
                    });
                }
            }

            if (unit.TroopType == TroopWorker && TryGetAiBridgeEngineeringPlan(sourceGrid, unit, out var bridgePlan))
            {
                actions.Add(new AiOffensiveAction(
                    sourceGrid,
                    unit,
                    bridgePlan.ActionGrid,
                    null,
                    null,
                    bridgePlan,
                    null,
                    bridgePlan.Score,
                    GetAiDecisionNoise(sourceGrid, bridgePlan.WorkGrid, participantCount: bridgePlan.Corridor.Count)));
            }

            if (unit.TroopType == TroopWorker)
            {
                foreach (var fencePlan in GetAiFenceEngineeringPlans(sourceGrid, unit))
                {
                    actions.Add(new AiOffensiveAction(
                        sourceGrid,
                        unit,
                        fencePlan.ActionGrid,
                        null,
                        null,
                        null,
                        fencePlan,
                        fencePlan.Score,
                        GetAiDecisionNoise(sourceGrid, fencePlan.FenceGrid, participantCount: 1)));
                }
            }

            var enemyGrids = GetAllBattlePieces()
                .Where(entry => IsAttackerPiece(entry.Occupant) != IsAttackerPiece(unit) && !IsHiddenFromSide(entry.Occupant, unit.TeamName))
                .Select(entry => entry.Grid)
                .ToList();
            foreach (var objective in GetAiOutpostObjectives(unit, enemyGrids))
            {
                if (sourceGrid == objective.Grid)
                {
                    continue;
                }

                if (!TryGetAiPathEndpointToward(
                        sourceGrid,
                        unit,
                        objective.Grid,
                        out var approachGrid,
                        out var fullPathEnergyCost,
                        out var fullPathSteps))
                {
                    continue;
                }

                var score = objective.Score +
                            GetOfficerTacticalIntelligence(unit.OfficerName) * 15 -
                            fullPathEnergyCost * 300;
                if (score <= 0)
                {
                    continue;
                }
                var routedObjective = objective with
                {
                    Reason = $"{objective.Reason}; A* energy {fullPathEnergyCost}, steps {fullPathSteps}"
                };
                actions.Add(new AiOffensiveAction(
                    sourceGrid,
                    unit,
                    approachGrid,
                    null,
                    routedObjective,
                    null,
                    null,
                    score,
                    GetAiDecisionNoise(sourceGrid, approachGrid, participantCount: 1)));
            }
        }

        if (actions.Count == 0)
        {
            return false;
        }

        var chosenAction = actions
            .OrderByDescending(action => GetAiFinalOffensiveActionScore(action) + action.Noise)
            .ThenByDescending(GetAiFinalOffensiveActionScore)
            .ThenByDescending(action => action.Score)
            .ThenByDescending(action => action.Noise)
            .First();
        if (chosenAction.OutpostObjective.HasValue)
        {
            return TryExecuteAiOutpostMove(chosenAction.SourceGrid, chosenAction.Unit, chosenAction.TargetGrid, chosenAction.OutpostObjective.Value, chosenAction.Score, chosenAction.Noise);
        }

        if (chosenAction.BridgePlan != null)
        {
            return TryExecuteAiBridgeEngineeringAction(chosenAction.SourceGrid, chosenAction.Unit, chosenAction.BridgePlan, chosenAction.Noise);
        }

        if (chosenAction.BridgeRepairPlan.HasValue)
        {
            return TryExecuteAiBridgeRepair(chosenAction.SourceGrid, chosenAction.Unit, chosenAction.BridgeRepairPlan.Value, chosenAction.Noise);
        }

        if (chosenAction.FencePlan != null)
        {
            return TryExecuteAiFenceEngineeringAction(chosenAction.SourceGrid, chosenAction.Unit, chosenAction.FencePlan, chosenAction.Noise);
        }

        if (chosenAction.SupplyPlan.HasValue)
        {
            return TryExecuteAiSupplyPlan(chosenAction.SourceGrid, chosenAction.Unit, chosenAction.SupplyPlan.Value, chosenAction.Noise);
        }

        if (chosenAction.ExtinguishPlan.HasValue)
        {
            return TryExecuteAiExtinguishStrategy(chosenAction.SourceGrid, chosenAction.Unit, chosenAction.ExtinguishPlan.Value, chosenAction.Noise);
        }

        if (chosenAction.FirePlan.HasValue)
        {
            return TryExecuteAiFireStrategy(chosenAction.SourceGrid, chosenAction.Unit, chosenAction.FirePlan.Value, chosenAction.Noise);
        }

        if (chosenAction.IsHideAction)
        {
            return TryExecuteAiHide(chosenAction.SourceGrid, chosenAction.Unit, chosenAction.Score, chosenAction.Noise);
        }

        if (chosenAction.IsGuardAction)
        {
            _selectedUnit = chosenAction.Unit;
            _selectedUnitGrid = chosenAction.SourceGrid;
            FocusCameraOnBattleGrid(chosenAction.SourceGrid);
            var guardReason = IsAiOwnedOutpost(chosenAction.SourceGrid, chosenAction.Unit.TeamName)
                ? GetAiEnemyFoodPressureScore(chosenAction.Unit.TeamName) > 0
                    ? "guard occupied fortress while enemy food is low"
                    : "guard occupied fortress"
                : "guard defensive position while enemy food is low";
            var guardReasonText = guardReason switch
            {
                "guard occupied fortress while enemy food is low" => BattleText("log.ai.guard_fortress_low_food", "guard occupied fortress while enemy food is low"),
                "guard occupied fortress" => BattleText("log.ai.guard_fortress", "guard occupied fortress"),
                _ => BattleText("log.ai.guard_low_food", "guard defensive position while enemy food is low")
            };
            AppendBattleLog(chosenAction.Unit, "AI", BattleFormat("log.ai.guard_decision", "Decision: {0} (score {1}, variance {2}).", guardReasonText, chosenAction.Score, chosenAction.Noise));
            return TryExecuteBattleActionIntent(
                new BattleActionIntent(BattleActionKind.Guard, chosenAction.SourceGrid, chosenAction.SourceGrid),
                chosenAction.Unit);
        }

        if (chosenAction.MoveAttackDestination.HasValue)
        {
            return TryExecuteAiMoveAndAttack(
                chosenAction.SourceGrid,
                chosenAction.Unit,
                chosenAction.MoveAttackDestination.Value,
                chosenAction.TargetGrid,
                chosenAction.Score,
                chosenAction.Noise);
        }

        return chosenAction.UnionCandidate == null
            ? TryExecuteAiAttack(chosenAction.SourceGrid, chosenAction.Unit, chosenAction.TargetGrid, chosenAction.Score, chosenAction.Noise)
            : TryExecuteAiUnionAttack(chosenAction.SourceGrid, chosenAction.Unit, chosenAction.UnionCandidate, chosenAction.Score, chosenAction.Noise);
    }

    private bool TryExecuteAiExtinguishStrategy(BattleGridKey sourceGrid, BattleOccupantInfo unit, AiExtinguishPlan plan, int noise)
    {
        if (!CanUseExtinguishStrategy(unit, sourceGrid) ||
            !CalculateExtinguishStrategyTargetGrids(sourceGrid, unit).Contains(plan.TargetGrid))
        {
            AppendBattleLog(unit, "AI", $"Extinguish plan cancelled: fire target {plan.TargetGrid} is no longer legal.");
            return false;
        }

        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(
            unit,
            "AI",
            BattleFormat("log.ai.extinguish", "Decision: extinguish fire at {0}; protect {1} unit(s), projected fire damage {2}, score {3}, variance {4}.", plan.TargetGrid, plan.ProtectedUnits, plan.ProjectedDamage, plan.Score, noise));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Extinguish, sourceGrid, plan.TargetGrid),
            unit);
    }

    private bool TryGetAiHideAmbushScore(BattleGridKey sourceGrid, BattleOccupantInfo unit, out int score)
    {
        score = 0;
        if (unit.Category != CategoryUnit ||
            unit.HasAttackedThisTurn ||
            !CanHideAtGrid(sourceGrid, unit) ||
            GetAiThreatScore(sourceGrid, unit) > 0)
        {
            return false;
        }

        if (CalculateAttackableGrids(sourceGrid, unit)
            .Any(grid => _occupantsByGrid.TryGetValue(grid, out var occupants) &&
                         GetAiAttackTargetForAttack(occupants, unit.TeamName, grid) != null))
        {
            return false;
        }

        var nearestVisibleEnemyDistance = GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName != unit.TeamName && !IsHiddenFromSide(entry.Occupant, unit.TeamName))
            .Select(entry => GetManhattanDistance(sourceGrid.Grid, entry.Grid.Grid))
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        if (nearestVisibleEnemyDistance > AiHideAmbushRange)
        {
            return false;
        }

        score = AiHideAmbushBaseScore +
                (AiHideAmbushRange - nearestVisibleEnemyDistance) * 120 +
                GetOfficerTacticalIntelligence(unit.OfficerName) * 5 +
                GetAiEnemyFoodPressureScore(unit.TeamName);
        return true;
    }

    private bool TryExecuteAiHide(BattleGridKey sourceGrid, BattleOccupantInfo unit, int score, int noise)
    {
        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(unit, "AI", BattleFormat("log.ai.hide", "Decision: hide in forest and prepare ambush (score {0}, variance {1}).", score, noise));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Hide, sourceGrid, sourceGrid),
            unit);
    }

    private bool TryExecuteAiFireStrategy(BattleGridKey sourceGrid, BattleOccupantInfo unit, AiFirePlan plan, int noise)
    {
        if (!CanUseFireStrategy(unit) || !CalculateFireStrategyTargetGrids(sourceGrid, unit).Contains(plan.TargetGrid))
        {
            AppendBattleLog(unit, "AI", $"Fire plan cancelled: target {plan.TargetGrid} is no longer legal.");
            return false;
        }

        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(
            unit,
            "AI",
            BattleFormat(
                "log.ai.fire_strategy",
                "Decision: fire strategy at {0}; enemy damage {1}, friendly risk {2}, enemy targets {3}, spread targets {4}, score {5}, variance {6}.",
                plan.TargetGrid,
                plan.EnemyDamage,
                plan.FriendlyDamage,
                plan.EnemyTargets,
                plan.SpreadTargets,
                plan.Score,
                noise));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.FireStrategy, sourceGrid, plan.TargetGrid),
            unit);
    }

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
            var crossesHeavyDamageThreshold = BattleBridgeSystem.IsHeavilyDamaged(cell) &&
                                              (cell.BridgeHealth + repairAmount) * 2 > cell.BridgeMaxHealth;
            var nearbyFriendlyUnits = GetAllBattlePieces()
                .Count(entry => entry.Occupant.TeamName == unit.TeamName &&
                                GetManhattanDistance(entry.Grid.Grid, target) <= 4);
            var criticalBonus = cell.BridgeHealth * 4 <= cell.BridgeMaxHealth
                ? AiBridgeRepairCriticalScore
                : 0;
            var thresholdBonus = crossesHeavyDamageThreshold
                ? AiBridgeRepairThresholdScore
                : 0;
            var score = AiBridgeRepairBaseScore +
                        repairAmount * 2 +
                        criticalBonus +
                        thresholdBonus +
                        Math.Min(3, nearbyFriendlyUnits) * AiBridgeRepairNearbyFriendlyScore;
            var reason = crossesHeavyDamageThreshold
                ? "restore bridge above heavy-damage threshold"
                : cell.BridgeHealth * 4 <= cell.BridgeMaxHealth
                    ? "prevent critically damaged bridge from collapsing"
                    : "maintain damaged bridge route";
            yield return new AiBridgeRepairPlan(ToGroundGridKey(target), repairAmount, score, reason);
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

                    var pathReduction = beforeLength == int.MaxValue
                        ? AiBridgeMinimumPathReduction + 3
                        : beforeLength - afterLength;
                    if (pathReduction < AiBridgeMinimumPathReduction)
                    {
                        continue;
                    }

                    var approachDistance = GetManhattanDistance(sourceGrid.Grid, actionGrid.Grid);
                    var score = AiBridgeConstructionBaseScore +
                                pathReduction * AiBridgePathReductionScore -
                                (corridor.Count - 1) * AiBridgeSegmentPenalty -
                                approachDistance * AiBridgeApproachPenalty +
                                GetOfficerTacticalIntelligence(worker.OfficerName) * 8 -
                                GetAiThreatScore(sourceGrid, worker) / 2;
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
        if (!IsAiStrategicPassable(source, virtualBridges, virtualBlocks) || !IsAiStrategicPassable(objective, virtualBridges, virtualBlocks))
        {
            return int.MaxValue;
        }

        var distances = new Dictionary<Vector2I, int> { [source] = 0 };
        var frontier = new Queue<Vector2I>();
        frontier.Enqueue(source);
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == objective)
            {
                return distances[current];
            }

            foreach (var neighbor in GetOrthogonalNeighbors(current).Where(IsWithinMap))
            {
                if (!distances.ContainsKey(neighbor) && IsAiStrategicPassable(neighbor, virtualBridges, virtualBlocks))
                {
                    distances[neighbor] = distances[current] + 1;
                    frontier.Enqueue(neighbor);
                }
            }
        }

        return int.MaxValue;
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
                            var removalScore = AiFenceRemovalBaseScore + routeImpact * AiFencePathImpactScore -
                                               GetManhattanDistance(sourceGrid.Grid, actionGrid.Grid) * AiBridgeApproachPenalty -
                                               WorkerRemoveWoodFenceEnergyCost * 20;
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

                    var constructionScore = AiFenceConstructionBaseScore +
                                            defenseImpact * AiFencePathImpactScore +
                                            (hasFriendlySupport ? AiFenceSupportScore : 0) +
                                            (5 - enemyDistance) * 120 +
                                            GetOfficerTacticalIntelligence(worker.OfficerName) * 5 -
                                            GetManhattanDistance(sourceGrid.Grid, buildActionGrid.Grid) * AiBridgeApproachPenalty -
                                            WorkerInstallWoodFenceEnergyCost * 20;
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
                var impact = before != int.MaxValue && after == int.MaxValue
                    ? AiBridgeMinimumPathReduction + 3
                    : before == int.MaxValue || after == int.MaxValue ? 0 : after - before;
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
                if (after == int.MaxValue || after > before + 2)
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
                var impact = before == int.MaxValue && after != int.MaxValue
                    ? AiBridgeMinimumPathReduction + 3
                    : before == int.MaxValue || after == int.MaxValue ? 0 : before - after;
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
    private bool TryExecuteAiOutpostMove(BattleGridKey sourceGrid, BattleOccupantInfo unit, BattleGridKey destinationGrid, AiOutpostObjective objective, int score, int noise)
    {
        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(unit, "AI", BattleFormat("log.ai.outpost_move", "Decision: move {0} -> {1}; fortress plan target {2}: {3} (score {4}, intelligence {5}, variance {6}).", sourceGrid, destinationGrid, objective.Grid, GetAiOutpostReasonText(objective.Reason), score, GetOfficerTacticalIntelligence(unit.OfficerName), noise));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Move, sourceGrid, destinationGrid),
            unit);
    }

    private List<AiOutpostObjective> GetAiOutpostObjectives(BattleOccupantInfo unit, IReadOnlyList<BattleGridKey> enemyGrids)
    {
        var objectives = new List<AiOutpostObjective>();
        if (_mapData?.ScenarioDefinition.ScenarioType != BattleScenarioType.FieldBattle)
        {
            return objectives;
        }

        var outpostGrids = new List<BattleGridKey>();
        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var cell = _mapData.GetCell(x, y);
                if (cell.IsDefenseOutpost)
                {
                    outpostGrids.Add(ToGroundGridKey(cell.Grid));
                }
            }
        }

        var isDefender = IsDefenderTeam(unit);
        var desiredOwner = isDefender ? BattleOutpostOwner.Defender : BattleOutpostOwner.Attacker;
        var lostOutposts = outpostGrids
            .Where(grid => _mapData.GetCell(grid.X, grid.Y).DefenseOutpostOwner != desiredOwner)
            .ToList();
        foreach (var outpostGrid in lostOutposts)
        {
            var score = isDefender ? AiOutpostRecaptureObjectiveScore : AiOutpostCaptureObjectiveScore;
            if (!isDefender && lostOutposts.Count == 1)
            {
                score += AiOutpostLastCaptureObjectiveBonus;
            }

            objectives.Add(new AiOutpostObjective(
                outpostGrid,
                score,
                isDefender ? "recapture lost fortress" : lostOutposts.Count == 1 ? "capture final fortress for victory" : "capture unoccupied fortress"));
        }

        if (!isDefender || enemyGrids.Count == 0)
        {
            return objectives;
        }

        foreach (var outpostGrid in outpostGrids.Except(lostOutposts))
        {
            var enemyDistance = enemyGrids.Min(enemyGrid => GetManhattanDistance(outpostGrid.Grid, enemyGrid.Grid));
            if (enemyDistance <= AiOutpostThreatRange)
            {
                objectives.Add(new AiOutpostObjective(
                    outpostGrid,
                    AiOutpostThreatObjectiveScore + (AiOutpostThreatRange - enemyDistance) * 250,
                    $"protect fortress from enemy at distance {enemyDistance}"));
            }
        }

        return objectives;
    }

    private string GetAiOutpostReasonText(string reason)
    {
        return reason switch
        {
            "recapture lost fortress" => BattleText("log.ai.outpost_recapture", "recapture lost fortress"),
            "capture final fortress for victory" => BattleText("log.ai.outpost_final_capture", "capture final fortress for victory"),
            "capture unoccupied fortress" => BattleText("log.ai.outpost_capture", "capture unoccupied fortress"),
            _ when reason.StartsWith("protect fortress from enemy at distance ") => BattleFormat("log.ai.outpost_protect", "protect fortress from enemy at distance {0}", reason["protect fortress from enemy at distance ".Length..]),
            _ => reason
        };
    }

    private static int GetOfficerTacticalIntelligence(string officerName)
    {
        return BattleOfficerAiProfiles.GetTacticalIntelligence(officerName);
    }

    private static int GetAiCombatDecisionScore(BattleOccupantInfo unit)
    {
        return GetOfficerBattleAttribute(unit.OfficerName) * 6;
    }

    private static int GetAiOfficerDecisionTieBreakScore(BattleOccupantInfo unit)
    {
        return GetOfficerTacticalIntelligence(unit.OfficerName) * 4 + GetOfficerBattleAttribute(unit.OfficerName) * 2;
    }

    private int GetAiFinalOffensiveActionScore(AiOffensiveAction action)
    {
        var intelligence = GetOfficerTacticalIntelligence(action.Unit.OfficerName);
        var combat = GetOfficerBattleAttribute(action.Unit.OfficerName);
        var isTacticalObjective = action.OutpostObjective.HasValue || action.BridgePlan != null || action.BridgeRepairPlan.HasValue || action.FencePlan != null || action.SupplyPlan.HasValue || action.ExtinguishPlan.HasValue || action.FirePlan.HasValue || action.IsGuardAction;
        var isDirectAttack = action.UnionCandidate == null &&
                             !action.MoveAttackDestination.HasValue &&
                             !action.ExtinguishPlan.HasValue &&
                             !action.FirePlan.HasValue &&
                             !action.IsHideAction &&
                             !action.IsGuardAction &&
                             _occupantsByGrid.TryGetValue(action.TargetGrid, out var directTargetOccupants) &&
                             GetAiAttackTargetForAttack(directTargetOccupants, action.Unit.TeamName, action.TargetGrid) != null;
        var enemyFoodPressure = GetAiEnemyFoodPressureScore(action.Unit.TeamName);
        var isDefensiveFoodAction = action.IsGuardAction ||
                                    action.OutpostObjective?.Reason.StartsWith("protect", StringComparison.Ordinal) == true;
        var isDecisiveAction = action.Score >= BattleAiScoring.DecisiveActionScore ||
                               action.OutpostObjective?.Reason.Contains("final", StringComparison.OrdinalIgnoreCase) == true;
        var targetsSupplyCart = _occupantsByGrid.TryGetValue(action.TargetGrid, out var targetOccupants) &&
                                GetAiAttackTargetForAttack(targetOccupants, action.Unit.TeamName, action.TargetGrid) is { TroopType: TroopSupplyCart };
        return BattleAiScoring.GetFinalOffensiveActionScore(new BattleAiScoring.FinalOffensiveActionInput(
            action.Score,
            intelligence,
            combat,
            enemyFoodPressure,
            isTacticalObjective,
            isDirectAttack,
            isDirectAttack || action.IsHideAction || isDefensiveFoodAction,
            action.Unit.IsHidden,
            action.IsHideAction,
            targetsSupplyCart,
            isDecisiveAction,
            AiHiddenAmbushAttackScoreBonus));
    }

    private int GetAiOffensiveActionScore(BattleGridKey targetGrid, string attackerTeamName, BattleOccupantInfo target, int damage, int supportCount)
    {
        return BattleAiScoring.GetOffensiveActionScore(
            damage,
            target.HitPoints,
            target.Category == CategoryUnit && !string.IsNullOrWhiteSpace(target.OfficerName),
            supportCount,
            UnionAttackSupportEnergyCost,
            GetAiDefenseOutpostAttackScoreBonus(targetGrid, attackerTeamName));
    }

    private int GetAiDefenseOutpostAttackScoreBonus(BattleGridKey targetGrid, string attackerTeamName)
    {
        if (_mapData?.ScenarioDefinition.ScenarioType != BattleScenarioType.FieldBattle || targetGrid.Level != 0)
        {
            return 0;
        }

        var cell = _mapData.GetCell(targetGrid.X, targetGrid.Y);
        if (!cell.IsDefenseOutpost)
        {
            return 0;
        }

        var desiredOwner = IsDefenderTeamName(attackerTeamName)
            ? BattleOutpostOwner.Defender
            : BattleOutpostOwner.Attacker;
        return cell.DefenseOutpostOwner == desiredOwner ? 0 : AiOutpostAttackScoreBonus;
    }

    private int GetAiDecisionNoise(BattleGridKey sourceGrid, BattleGridKey targetGrid, int participantCount)
    {
        return BattleAiScoring.GetDecisionNoise(_turnNumber, sourceGrid, targetGrid, participantCount);
    }

    private bool TryExecuteAiUnionAttack(BattleGridKey sourceGrid, BattleOccupantInfo unit, UnionAttackCandidate candidate, int score, int noise)
    {
        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(unit, "AI", BattleFormat("log.ai.union_attack", "Decision: union attack {0} with {1} battle teams (score {2}, variance {3}).", candidate.TargetGrid, candidate.Participants.Count, score, noise));
        OnUnionAttackButtonPressed();
        return HasUnitActed(unit);
    }

    private bool TryExecuteAiAttack(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        if (unit.Energy < NormalAttackEnergyCost)
        {
            return false;
        }

        var targetGrid = CalculateAttackableGrids(sourceGrid, unit)
            .Where(grid => _occupantsByGrid.TryGetValue(grid, out var occupants) && GetAiAttackTargetForAttack(occupants, unit.TeamName, grid) != null)
            .OrderByDescending(grid =>
            {
                var target = GetAiAttackTargetForAttack(_occupantsByGrid[grid], unit.TeamName, grid)!;
                return GetAiOffensiveActionScore(grid, unit.TeamName, target, GetAttackDamageAgainst(unit, target), supportCount: 0);
            })
            .ThenBy(grid => GetManhattanDistance(sourceGrid.Grid, grid.Grid))
            .FirstOrDefault();
        if (targetGrid == default)
        {
            return false;
        }

        var target = GetAiAttackTargetForAttack(_occupantsByGrid[targetGrid], unit.TeamName, targetGrid)!;
        return TryExecuteAiAttack(sourceGrid, unit, targetGrid, GetAiOffensiveActionScore(targetGrid, unit.TeamName, target, GetAttackDamageAgainst(unit, target), supportCount: 0), GetAiDecisionNoise(sourceGrid, targetGrid, participantCount: 1));
    }

    private bool TryExecuteAiAttack(BattleGridKey sourceGrid, BattleOccupantInfo unit, BattleGridKey targetGrid, int score, int noise)
    {
        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(unit, "AI", BattleFormat("log.ai.attack", "Decision: attack {0}; score {1}, variance {2}.", targetGrid, score, noise));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Attack, sourceGrid, targetGrid),
            unit);
    }

    private bool TryExecuteAiMoveAndAttack(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        if (unit.Energy < NormalAttackEnergyCost || !CanUseAttackCommand(unit))
        {
            return false;
        }

        var destination = CalculateReachableGrids(sourceGrid, unit.Energy - NormalAttackEnergyCost, GetAvailableMoveRange(unit))
            .Where(IsAiSafeMovementDestination)
            .Where(grid => !IsAiDefenderWorkerLeavingInnerCity(sourceGrid, grid, unit))
            .SelectMany(grid => CalculateAttackableGrids(grid, unit)
                .Where(targetGrid => _occupantsByGrid.TryGetValue(targetGrid, out var occupants) &&
                                     GetAiAttackTargetForAttack(occupants, unit.TeamName, targetGrid) != null)
                .Select(targetGrid => (Grid: grid, Target: targetGrid)))
            .OrderByDescending(plan =>
            {
                var target = GetAiAttackTargetForAttack(_occupantsByGrid[plan.Target], unit.TeamName, plan.Target)!;
                return GetAiOffensiveActionScore(plan.Target, unit.TeamName, target, GetAttackDamageAgainst(unit, target), supportCount: 0);
            })
            .ThenBy(plan => GetManhattanDistance(plan.Grid.Grid, plan.Target.Grid))
            .ThenByDescending(plan => GetManhattanDistance(sourceGrid.Grid, plan.Grid.Grid))
            .FirstOrDefault();
        if (destination == default)
        {
            return false;
        }

        var plannedTarget = destination.Target;
        var plannedTargetOccupants = _occupantsByGrid[plannedTarget];
        var plannedTargetUnit = GetAiAttackTargetForAttack(plannedTargetOccupants, unit.TeamName, plannedTarget)!;
        var plannedScore = GetAiOffensiveActionScore(plannedTarget, unit.TeamName, plannedTargetUnit, GetAttackDamageAgainst(unit, plannedTargetUnit), supportCount: 0);
        var plannedNoise = GetAiDecisionNoise(sourceGrid, plannedTarget, participantCount: 1);

        return TryExecuteAiMoveAndAttack(sourceGrid, unit, destination.Grid, plannedTarget, plannedScore, plannedNoise);
    }

    private bool TryExecuteAiMoveAndAttack(
        BattleGridKey sourceGrid,
        BattleOccupantInfo unit,
        BattleGridKey destinationGrid,
        BattleGridKey plannedTarget,
        int plannedScore,
        int plannedNoise)
    {

        if (!IsAiSafeMovementDestination(destinationGrid))
        {
            AppendBattleLog(unit, "AI", $"Move+attack cancelled: destination {destinationGrid} is burning.");
            return false;
        }

        if (IsAiDefenderWorkerLeavingInnerCity(sourceGrid, destinationGrid, unit))
        {
            return false;
        }

        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(unit, "AI", BattleFormat("log.ai.move_attack", "Decision: move {0} -> {1}, reserve {2} energy, then attack {3} (score {4}, variance {5}).", sourceGrid, destinationGrid, NormalAttackEnergyCost, plannedTarget, plannedScore, plannedNoise));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(
                BattleActionKind.Move,
                sourceGrid,
                destinationGrid,
                ReservedEnergy: NormalAttackEnergyCost,
                MarkActedAfterMove: false),
            unit,
            () => TryExecuteAiPlannedMoveAttack(destinationGrid, unit, plannedTarget, plannedScore, plannedNoise));
    }

    private bool TryExecuteAiPlannedMoveAttack(BattleGridKey sourceGrid, BattleOccupantInfo plannedUnit, BattleGridKey plannedTarget, int score, int noise)
    {
        if (!TryGetCurrentOccupantAtGrid(sourceGrid, plannedUnit, out var currentUnit))
        {
            AppendBattleLog(plannedUnit, "AI", $"Move+attack failed: unit is no longer at {sourceGrid}; planned target {plannedTarget}.");
            return false;
        }

        if (currentUnit.Energy < NormalAttackEnergyCost || !CanUseAttackCommand(currentUnit))
        {
            AppendBattleLog(currentUnit, "AI", $"Move+attack failed: attack unavailable at {sourceGrid}; energy {currentUnit.Energy}, planned target {plannedTarget}.");
            return false;
        }

        if (!_occupantsByGrid.TryGetValue(plannedTarget, out var targetOccupants) ||
            GetAiAttackTargetForAttack(targetOccupants, currentUnit.TeamName, plannedTarget) == null)
        {
            AppendBattleLog(currentUnit, "AI", $"Move+attack failed: no valid target at planned grid {plannedTarget}.");
            return false;
        }

        if (!CalculateAttackableGrids(sourceGrid, currentUnit).Contains(plannedTarget))
        {
            AppendBattleLog(currentUnit, "AI", $"Move+attack failed: planned target {plannedTarget} is outside the legal attack grids from {sourceGrid}.");
            return false;
        }

        return TryExecuteAiAttack(sourceGrid, currentUnit, plannedTarget, score, noise);
    }

    private bool TryExecuteAiMove(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        if (unit.TroopType == TroopSupplyCart)
        {
            return TryExecuteAiSupplyMove(sourceGrid, unit);
        }

        if (IsAiAmmoDepletedCatapult(unit))
        {
            return TryExecuteAiAmmoDepletedCatapultReposition(sourceGrid, unit);
        }

        var enemyGrids = GetAllBattlePieces()
            .Where(entry => IsAttackerPiece(entry.Occupant) != IsAttackerPiece(unit) && !IsHiddenFromSide(entry.Occupant, unit.TeamName))
            .Select(entry => entry.Grid)
            .ToList();
        if (enemyGrids.Count == 0)
        {
            return false;
        }

        _selectedUnit = unit;
        _selectedUnitGrid = sourceGrid;
        FocusCameraOnBattleGrid(sourceGrid);
        var routedPursuits = new List<(BattleGridKey Destination, BattleGridKey Enemy, int EnergyCost, int Steps)>();
        foreach (var enemyGrid in enemyGrids)
        {
            if (TryGetAiPathEndpointTowardEnemy(
                    sourceGrid,
                    unit,
                    enemyGrid,
                    out var endpoint,
                    out var energyCost,
                    out var steps))
            {
                if (!IsAiDefenderWorkerLeavingInnerCity(sourceGrid, endpoint, unit))
                {
                    routedPursuits.Add((endpoint, enemyGrid, energyCost, steps));
                }
            }
        }

        var usesFullRoute = routedPursuits.Count > 0;
        var destination = usesFullRoute
            ? routedPursuits
                .OrderBy(route => route.EnergyCost)
                .ThenBy(route => route.Steps)
                .ThenBy(route => route.Enemy.Y)
                .ThenBy(route => route.Enemy.X)
                .First()
                .Destination
            : CalculateReachableGrids(sourceGrid, GetAvailableMoveEnergy(unit), GetAvailableMoveRange(unit))
                .Where(IsAiSafeMovementDestination)
                .Where(grid => !IsAiDefenderWorkerLeavingInnerCity(sourceGrid, grid, unit))
                .OrderBy(grid => enemyGrids.Min(enemy => GetManhattanDistance(grid.Grid, enemy.Grid)))
                .ThenBy(grid => GetManhattanDistance(sourceGrid.Grid, grid.Grid))
                .FirstOrDefault();
        if (destination == default)
        {
            return false;
        }

        var currentEnemyDistance = enemyGrids.Min(enemy => GetManhattanDistance(sourceGrid.Grid, enemy.Grid));
        var destinationEnemyDistance = enemyGrids.Min(enemy => GetManhattanDistance(destination.Grid, enemy.Grid));
        if (unit.TroopType == TroopCatapult && destinationEnemyDistance >= currentEnemyDistance)
        {
            AppendBattleLog(unit, "AI", BattleFormat("log.ai.catapult_hold_forward", "Decision: hold at {0}; no forward catapult position is currently available.", sourceGrid));
            MarkUnitActed(unit);
            return true;
        }

        if (!TryBuildMovePath(sourceGrid, destination, unit.Energy, GetAvailableMoveRange(unit), out var movePath))
        {
            return false;
        }

        var moveEnergyCost = GetMovePathEnergyCost(movePath);
        var remainingEnergy = unit.Energy - moveEnergyCost;
        var remainingMoveRange = unit.RemainingMoveRange - GetMovePathRangeCost(movePath);
        var moveOnlyReason = GetAiMoveOnlyReason(sourceGrid, unit);

        var routeDescription = usesFullRoute
            ? BattleText("log.ai.route_astar", "A* legal approach to an enemy")
            : BattleText("log.ai.route_local", "local legal approach to an enemy (no complete route currently available)");
        AppendBattleLog(unit, "AI", BattleFormat(
            "log.ai.move_decision",
            "Decision: move {0} -> {1}; {2}. Energy {3} - {4} = {5}, move range {6}/{7}; move+attack unavailable: {8}.",
            sourceGrid,
            destination,
            routeDescription,
            unit.Energy,
            moveEnergyCost,
            remainingEnergy,
            remainingMoveRange,
            unit.MoveRange,
            moveOnlyReason));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Move, sourceGrid, destination),
            unit);
    }

    private bool TryExecuteAiAmmoDepletedCatapultReposition(BattleGridKey sourceGrid, BattleOccupantInfo catapult)
    {
        var supplyGrids = GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName == catapult.TeamName && entry.Occupant.TroopType == TroopSupplyCart)
            .Select(entry => entry.Grid)
            .ToList();
        if (supplyGrids.Count == 0)
        {
            AppendBattleLog(catapult, "AI", BattleFormat("log.ai.catapult_no_ammo_no_supply", "Decision: hold at {0}; ammo 0 and no friendly Supply Cart remains.", sourceGrid));
            MarkUnitActed(catapult);
            return true;
        }

        var currentSupplyDistance = supplyGrids.Min(grid => GetManhattanDistance(sourceGrid.Grid, grid.Grid));
        if (currentSupplyDistance <= 1)
        {
            AppendBattleLog(catapult, "AI", BattleFormat("log.ai.catapult_no_ammo_wait", "Decision: hold at {0}; ammo 0, waiting beside Supply Cart for weapon resupply.", sourceGrid));
            MarkUnitActed(catapult);
            return true;
        }

        _selectedUnit = catapult;
        _selectedUnitGrid = sourceGrid;
        var destination = CalculateReachableGrids(sourceGrid, GetAvailableMoveEnergy(catapult), GetAvailableMoveRange(catapult))
            .Where(grid => grid != sourceGrid && IsAiSafeMovementDestination(grid))
            .Where(grid => supplyGrids.Min(supplyGrid => GetManhattanDistance(grid.Grid, supplyGrid.Grid)) < currentSupplyDistance)
            .OrderBy(grid => supplyGrids.Min(supplyGrid => GetManhattanDistance(grid.Grid, supplyGrid.Grid)))
            .ThenBy(grid => GetAiThreatScore(grid, catapult))
            .ThenByDescending(grid => GetManhattanDistance(sourceGrid.Grid, grid.Grid))
            .FirstOrDefault();
        if (destination == default)
        {
            AppendBattleLog(catapult, "AI", BattleFormat("log.ai.catapult_no_ammo_blocked", "Decision: hold at {0}; ammo 0 and route toward Supply Cart is blocked.", sourceGrid));
            MarkUnitActed(catapult);
            return true;
        }

        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(catapult, "AI", BattleFormat("log.ai.catapult_no_ammo_move", "Decision: move {0} -> {1}; ammo 0, withdraw toward Supply Cart.", sourceGrid, destination));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Move, sourceGrid, destination),
            catapult);
    }

    private string GetAiMoveOnlyReason(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        if (unit.Energy < NormalAttackEnergyCost)
        {
            return BattleFormat("log.ai.move_only_low_energy", "energy {0} is below attack cost {1}", unit.Energy, NormalAttackEnergyCost);
        }

        if (!CanUseAttackCommand(unit))
        {
            return BattleText("log.ai.move_only_no_attack", "unit attack command is unavailable");
        }

        var hasMoveAndAttackPlan = CalculateReachableGrids(
                sourceGrid,
                unit.Energy - NormalAttackEnergyCost,
                GetAvailableMoveRange(unit))
            .Where(IsAiSafeMovementDestination)
            .Where(grid => !IsAiDefenderWorkerLeavingInnerCity(sourceGrid, grid, unit))
            .Any(grid => CalculateAttackableGrids(grid, unit)
                .Any(targetGrid => _occupantsByGrid.TryGetValue(targetGrid, out var occupants) &&
                                  GetAiAttackTargetForAttack(occupants, unit.TeamName, targetGrid) != null));

        return hasMoveAndAttackPlan
            ? BattleText("log.ai.move_only_not_selected", "a move+attack plan existed but was not selected")
            : BattleFormat("log.ai.move_only_no_position", "no legal attack position reachable while reserving {0} energy", NormalAttackEnergyCost);
    }

    private bool IsAiDefenderWorkerLeavingInnerCity(BattleGridKey sourceGrid, BattleGridKey destinationGrid, BattleOccupantInfo unit)
    {
        // A gate opening is a controlled sortie route, not a general-purpose pursuit route
        // for the garrison's engineering team. Explicit engineering/sortie actions bypass
        // this generic move selector when they are introduced.
        return IsDefenderPiece(unit) &&
               unit.TroopType == TroopWorker &&
               sourceGrid.Level == 0 &&
               destinationGrid.Level == 0 &&
               IsInsideCityGroundGrid(sourceGrid.Grid) &&
               !IsInsideCityGroundGrid(destinationGrid.Grid);
    }
}
