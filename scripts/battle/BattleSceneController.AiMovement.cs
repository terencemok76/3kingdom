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
                .Any(targetGrid => TryGetAiOrdinaryAttackTarget(targetGrid, unit.TeamName, out _)));

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
