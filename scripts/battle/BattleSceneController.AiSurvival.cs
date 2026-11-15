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
}
