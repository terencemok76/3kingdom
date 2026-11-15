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

        var commandIntelligence = GetAiCommandIntelligence(sourceGrid, ladder);
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
                        GetAiThreatScore(goal, ladder) * (50 + commandIntelligence) / 200 +
                        (openWallTopCount > 0 ? commandIntelligence * 8 : 0);
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

        // Allied infantry must create an entrance when the city is sealed;
        // otherwise the generic enemy pursuit can lead them along the wall.
        var alliedNeedsBreach = BattleAiSiegePlanner.NeedsAlliedBreach(_isResolvingAlliedAiTurn,
            _isResolvingAlliedAiTurn && HasBreachedCityGate());

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
                var defenderRepair = GetAiDefenderGateRepairPotential(gateGrid);
                var isUnit = attacker.Occupant.Category == CategoryUnit;
                if (!BattleAiSiegePlanner.CanAttackGate(isRam, isUnit,
                    alliedNeedsBreach && isUnit && IsOutsideCityWall(attacker.Grid.Grid),
                    alliedNeedsBreach, turnsToBreach, damage, defenderRepair))
                {
                    continue;
                }

                var commandIntelligence = GetAiCommandIntelligence(attacker.Grid, attacker.Occupant);
                var score = BattleAiSiegePlanner.AttackScore(turnsToBreach, damage, isRam,
                    GetAiThreatScore(attacker.Grid, attacker.Occupant), commandIntelligence);
                breachActions.Add((attacker.Grid, attacker.Occupant, gateGrid, damage, score));
            }
        }

        var breachIndex = BattleAiSiegePlanner.SelectAttackIndex(breachActions
            .Select(action => (action.Score, action.Damage, action.Gate.Y, action.Gate.X)).ToArray());
        if (breachIndex >= 0)
        {
            var breachAction = breachActions[breachIndex];
            FocusCameraOnBattleGrid(breachAction.Source);
            AppendBattleLog(breachAction.Unit, "AI", BattleFormat("log.ai.breach_attack", "Siege breach: attack gate {0} with {1}; structure damage {2}, score {3}.", breachAction.Gate, FormatTroopType(breachAction.Unit.TroopType), breachAction.Damage, breachAction.Score));
            return TryExecuteBattleActionIntent(
                new BattleActionIntent(BattleActionKind.Attack, breachAction.Source, breachAction.Gate),
                breachAction.Unit);
        }

        var ramMoves = new List<(BattleGridKey Source, BattleOccupantInfo Ram, BattleGridKey Destination, BattleGridKey Gate, int PathEnergy, int PathSteps, int Score)>();
        foreach (var ram in candidates.Where(candidate => IsAttackerPiece(candidate.Occupant) &&
                     (candidate.Occupant.TroopType == TroopRam || (alliedNeedsBreach &&
                      BattleAiSiegePlanner.CanApproachGate(false,
                         candidate.Occupant.Category == CategoryUnit, IsOutsideCityWall(candidate.Grid.Grid),
                         true, CanUseAttackCommand(candidate.Occupant), GetStructureAttackDamage(candidate.Occupant))))))
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

                    var commandIntelligence = GetAiCommandIntelligence(ram.Grid, ram.Occupant);
                    var nearbyFriendlySupport = GetAllBattlePieces().Count(entry =>
                        entry.Occupant.TeamName == ram.Occupant.TeamName &&
                        entry.Occupant.Category == CategoryUnit &&
                        GetManhattanDistance(entry.Grid.Grid, gateGrid.Grid) <= 4);
                    var nearbyEnemyDefenders = GetAllBattlePieces().Count(entry =>
                        entry.Occupant.TeamName != ram.Occupant.TeamName &&
                        GetManhattanDistance(entry.Grid.Grid, gateGrid.Grid) <= 3);
                    var score = BattleAiSiegePlanner.MoveScore(ram.Occupant.TroopType == TroopRam,
                        nearbyFriendlySupport, nearbyEnemyDefenders, pathEnergy, pathSteps,
                        GetAiThreatScore(destination, ram.Occupant), commandIntelligence);
                    ramMoves.Add((ram.Grid, ram.Occupant, destination, gateGrid, pathEnergy, pathSteps, score));
                }
            }
        }

        var moveIndex = BattleAiSiegePlanner.SelectMoveIndex(ramMoves
            .Select(move => (move.Score, move.PathEnergy, move.PathSteps, move.Gate.Y, move.Gate.X)).ToArray());
        if (moveIndex < 0)
        {
            return false;
        }

        var ramMove = ramMoves[moveIndex];
        FocusCameraOnBattleGrid(ramMove.Source);
        AppendBattleLog(ramMove.Ram, "AI", BattleFormat("log.ai.breach_move", "Siege breach: move {0} -> {1} toward gate {2}; A* energy {3}, steps {4}.", ramMove.Source, ramMove.Destination, ramMove.Gate, ramMove.PathEnergy, ramMove.PathSteps));
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Move, ramMove.Source, ramMove.Destination),
            ramMove.Ram);
    }

    private int GetAiDefenderGateRepairPotential(BattleGridKey gateGrid)
    {
        if (_mapData == null || !IsWithinMap(gateGrid.Grid))
        {
            return 0;
        }

        var gateCell = _mapData.GetCell(gateGrid.X, gateGrid.Y);
        var missingHealth = gateCell.StructureMaxHealth - gateCell.StructureHealth;
        if (missingHealth <= 0)
        {
            return 0;
        }

        var repairPotential = 0;
        foreach (var defender in GetAllBattlePieces().Where(entry =>
                     IsDefenderPiece(entry.Occupant) &&
                     !HasUnitActed(entry.Occupant) &&
                     GetManhattanDistance(entry.Grid.Grid, gateGrid.Grid) == 1))
        {
            if (!CanRepairGate(defender.Occupant, gateGrid.Grid, gateCell) ||
                defender.Occupant.Energy < GetWorkEnergyCost(defender.Occupant, gateCell, WorkerWorkAction.General))
            {
                continue;
            }

            repairPotential += defender.Occupant.TroopType == TroopWorker
                ? WorkerGateRepairAmount
                : BattleBridgeSystem.EmergencyGateRepairAmount;
        }

        return Math.Min(missingHealth, repairPotential);
    }

    private bool TryGetAiOrdinaryAttackTarget(BattleGridKey targetGrid, string attackerTeamName, out BattleOccupantInfo target)
    {
        target = null!;
        // Closed gates own their target square. Their concealed defender must
        // never leak into normal attack, move+attack, or union scoring.
        if (IsClosedGateStructureTarget(targetGrid) ||
            !_occupantsByGrid.TryGetValue(targetGrid, out var occupants))
        {
            return false;
        }

        target = GetAiAttackTargetForAttack(occupants, attackerTeamName, targetGrid)!;
        return target != null;
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
}
