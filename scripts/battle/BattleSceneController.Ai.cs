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

        foreach (var candidate in candidates.Where(candidate => candidate.Occupant.TroopType != TroopSupplyCart))
        {
            if (TryExecuteAiMoveAndAttack(candidate.Grid, candidate.Occupant))
            {
                return;
            }
        }

        foreach (var movingCandidate in candidates)
        {
            if (TryExecuteAiMove(movingCandidate.Grid, movingCandidate.Occupant))
            {
                return;
            }
        }

        var waitingCandidate = candidates[0];
        FocusCameraOnBattleGrid(waitingCandidate.Grid);
        AppendBattleLog(waitingCandidate.Occupant, "AI", $"Decision: wait at {waitingCandidate.Grid}; no legal move or attack.");
        MarkUnitActed(waitingCandidate.Occupant);
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
            $"Decision: supply own team (wounded {recoveryCount}, morale {moraleCount}, repair {repairCount}).");
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Supply, sourceGrid, sourceGrid),
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
            yield return new AiSupplyPlan(sourceGrid, MoveBeforeSupply: false, directScore, "supply adjacent team");
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
                    actionScore - GetManhattanDistance(sourceGrid.Grid, destination.Grid) * 100,
                    "move and supply team");
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

                var score = GetAiSupplyActionScore(actionGrid, supplyCart);
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
            AppendBattleLog(supplyCart, "AI", $"Decision: {plan.Reason} (score {plan.Score}, variance {noise}).");
            return TryExecuteAiSupply(sourceGrid, supplyCart);
        }

        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(supplyCart, "AI", $"Decision: move {sourceGrid} -> {plan.ActionGrid}, reserve {SupplyActionEnergyCost} energy, then {plan.Reason} (score {plan.Score}, variance {noise}).");
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
                    TryExecuteAiSupply(_selectedUnitGrid.Value, _selectedUnit);
                }
            });
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
        AppendBattleLog(supplyCart, "AI", $"Decision: move {sourceGrid} -> {destination}; A* supply route to {supplyActionGrid} (support score {supplyScore}, full path energy {fullPathEnergyCost}, steps {fullPathSteps}).");
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
               candidate.Category == CategorySiegeEngine && candidate.HitPoints < candidate.MaxHitPoints;
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
        var outpostSummary = "eliminate enemy";
        if (_mapData?.ScenarioDefinition.ScenarioType == BattleScenarioType.FieldBattle)
        {
            var owner = _currentTurnSide == BattleTurnSide.TeamB ? BattleOutpostOwner.Defender : BattleOutpostOwner.Attacker;
            var unresolvedOutposts = Enumerable.Range(0, BattleMapData.Height)
                .SelectMany(y => Enumerable.Range(0, BattleMapData.Width).Select(x => _mapData.GetCell(x, y)))
                .Count(cell => cell.IsDefenseOutpost && cell.DefenseOutpostOwner != owner);
            outpostSummary = unresolvedOutposts > 0
                ? $"eliminate enemy or contest {unresolvedOutposts} fortress(es)"
                : "hold occupied fortresses and eliminate enemy";
        }

        var bridgeSummary = "no bridge route is worthwhile";
        foreach (var (grid, worker) in actingUnits.Where(entry => entry.Occupant.TroopType == TroopWorker))
        {
            if (TryGetAiBridgeEngineeringPlan(grid, worker, out var bridgePlan))
            {
                bridgeSummary = $"bridge at {bridgePlan.WorkGrid} toward {bridgePlan.ObjectiveGrid}, route -{bridgePlan.PathReduction}";
                break;
            }
        }

        return $"Opening plan: {outpostSummary}; {bridgeSummary}; vulnerable teams {vulnerableCount}/{actingUnits.Count}. Intelligence favors cover, supply, and tactical objectives; Combat favors decisive attacks.";
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
                var retreatAction = new AiSurvivalAction(
                    sourceGrid,
                    unit,
                    null,
                    AiSurvivalActionKind.Retreat,
                    AiRetreatSurvivalScore + threat + intelligence * 12,
                    "critical strength and projected enemy threat");
                if (retreatAction.Score > immediateAttackScore)
                {
                    actions.Add(retreatAction);
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

    private bool ExecuteAiSurvivalAction(AiSurvivalAction action)
    {
        var intelligence = GetOfficerTacticalIntelligence(action.Unit.OfficerName);
        var combat = GetOfficerBattleAttribute(action.Unit.OfficerName);
        _selectedUnit = action.Unit;
        _selectedUnitGrid = action.SourceGrid;
        FocusCameraOnBattleGrid(action.SourceGrid);
        AppendBattleLog(action.Unit, "AI", $"Survival decision: {action.Kind}; {action.Reason} (score {action.Score}, intelligence {intelligence}, combat {combat}).");
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
            default:
                return false;
        }
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

            if (unit.HasAttackedThisTurn)
            {
                continue;
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
            AppendBattleLog(chosenAction.Unit, "AI", $"Decision: {guardReason} (score {chosenAction.Score}, variance {chosenAction.Noise}).");
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
            $"Decision: extinguish fire at {plan.TargetGrid}; protect {plan.ProtectedUnits} unit(s), projected fire damage {plan.ProjectedDamage}, score {plan.Score}, variance {noise}.");
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
        AppendBattleLog(unit, "AI", $"Decision: hide in forest and prepare ambush (score {score}, variance {noise}).");
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
            $"Decision: fire strategy at {plan.TargetGrid}; enemy damage {plan.EnemyDamage}, friendly risk {plan.FriendlyDamage}, enemy targets {plan.EnemyTargets}, spread targets {plan.SpreadTargets}, score {plan.Score}, variance {noise}.");
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.FireStrategy, sourceGrid, plan.TargetGrid),
            unit);
    }

   private int GetAiOutpostObjectiveScore(BattleOccupantInfo unit, BattleGridKey approachGrid, AiOutpostObjective objective)
   {
       var intelligence = GetOfficerTacticalIntelligence(unit.OfficerName);
       return objective.Score + intelligence * 15 - GetManhattanDistance(approachGrid.Grid, objective.Grid.Grid) * 300;
   }
    private bool TryGetAiBridgeEngineeringPlan(BattleGridKey sourceGrid, BattleOccupantInfo worker, out AiBridgeEngineeringPlan plan)
    {
        plan = null!;
        if (_mapData?.ScenarioDefinition.ScenarioType != BattleScenarioType.FieldBattle ||
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
        return cell.Terrain == BattleTerrainType.River || (cell.IsWoodenBridge && cell.IsBridgeDamaged);
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
            AppendBattleLog(worker, "AI", $"Engineering plan: build bridge at {plan.WorkGrid} toward {plan.ObjectiveGrid}; route improves by {plan.PathReduction} steps (score {plan.Score}, variance {noise}).");
            return TryExecuteBattleActionIntent(
                new BattleActionIntent(BattleActionKind.Work, sourceGrid, plan.WorkGrid),
                worker);
        }

        AppendBattleLog(worker, "AI", $"Engineering plan: move {sourceGrid} -> {plan.ActionGrid}, then build bridge at {plan.WorkGrid} toward {plan.ObjectiveGrid}; projected route improves by {plan.PathReduction} steps (score {plan.Score}, variance {noise}).");
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
        AppendBattleLog(unit, "AI", $"Decision: move {sourceGrid} -> {destinationGrid}; fortress plan target {objective.Grid}: {objective.Reason} (score {score}, intelligence {GetOfficerTacticalIntelligence(unit.OfficerName)}, variance {noise}).");
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
        var isTacticalObjective = action.OutpostObjective.HasValue || action.BridgePlan != null || action.FencePlan != null || action.SupplyPlan.HasValue || action.ExtinguishPlan.HasValue || action.FirePlan.HasValue || action.IsGuardAction;
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
        AppendBattleLog(unit, "AI", $"Decision: union attack {candidate.TargetGrid} with {candidate.Participants.Count} battle teams (score {score}, variance {noise}).");
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
        AppendBattleLog(unit, "AI", $"Decision: attack {targetGrid}; score {score}, variance {noise}.");
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

        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(unit, "AI", $"Decision: move {sourceGrid} -> {destinationGrid}, reserve {NormalAttackEnergyCost} energy, then attack {plannedTarget} (score {plannedScore}, variance {plannedNoise}).");
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
        var destination = CalculateReachableGrids(sourceGrid, GetAvailableMoveEnergy(unit), GetAvailableMoveRange(unit))
            .Where(IsAiSafeMovementDestination)
            .OrderBy(grid => enemyGrids.Min(enemy => GetManhattanDistance(grid.Grid, enemy.Grid)))
            .ThenBy(grid => GetManhattanDistance(sourceGrid.Grid, grid.Grid))
            .FirstOrDefault();
        if (destination == default)
        {
            return false;
        }

        if (!TryBuildMovePath(sourceGrid, destination, unit.Energy, GetAvailableMoveRange(unit), out var movePath))
        {
            return false;
        }

        var moveEnergyCost = GetMovePathEnergyCost(movePath);
        var remainingEnergy = unit.Energy - moveEnergyCost;
        var remainingMoveRange = unit.RemainingMoveRange - movePath.Count;
        var moveOnlyReason = GetAiMoveOnlyReason(sourceGrid, unit);

        AppendBattleLog(unit, "AI", $"Decision: move {sourceGrid} -> {destination}; shortest legal approach to an enemy. Energy {unit.Energy} - {moveEnergyCost} = {remainingEnergy}, move range {remainingMoveRange}/{unit.MoveRange}; move+attack unavailable: {moveOnlyReason}.");
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Move, sourceGrid, destination),
            unit);
    }

    private string GetAiMoveOnlyReason(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        if (unit.Energy < NormalAttackEnergyCost)
        {
            return $"energy {unit.Energy} is below attack cost {NormalAttackEnergyCost}";
        }

        if (!CanUseAttackCommand(unit))
        {
            return "unit attack command is unavailable";
        }

        var hasMoveAndAttackPlan = CalculateReachableGrids(
                sourceGrid,
                unit.Energy - NormalAttackEnergyCost,
                GetAvailableMoveRange(unit))
            .Where(IsAiSafeMovementDestination)
            .Any(grid => CalculateAttackableGrids(grid, unit)
                .Any(targetGrid => _occupantsByGrid.TryGetValue(targetGrid, out var occupants) &&
                                  GetAiAttackTargetForAttack(occupants, unit.TeamName, targetGrid) != null));

        return hasMoveAndAttackPlan
            ? "a move+attack plan existed but was not selected"
            : $"no legal attack position reachable while reserving {NormalAttackEnergyCost} energy";
    }
}
