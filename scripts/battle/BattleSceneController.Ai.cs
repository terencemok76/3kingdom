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
    private bool TryExecuteAiCooperationAttackObjective(BattleGridKey sourceGrid, BattleOccupantInfo unit, BattleGridKey targetGrid)
    {
        if (!IsWithinMap(targetGrid.Grid))
        {
            return false;
        }

        if (CalculateAttackableGrids(sourceGrid, unit).Contains(targetGrid) &&
            TryGetAiOrdinaryAttackTarget(targetGrid, unit.TeamName, out _))
        {
            AppendBattleLog(unit, "Cooperation", $"Ally attacks the requested area at {targetGrid}.");
            return TryExecuteAiAttack(sourceGrid, unit, targetGrid, 0, 0);
        }

        if (!TryGetAiPathEndpointToward(sourceGrid, unit, targetGrid, out var destination, out _, out _) || destination == sourceGrid)
        {
            return false;
        }

        AppendBattleLog(unit, "Cooperation", $"Ally advances toward the requested area at {targetGrid}.");
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Move, sourceGrid, destination),
            unit);
    }

    private bool TryExecuteAiCooperationProtectObjective(BattleGridKey sourceGrid, BattleOccupantInfo unit, int protectedTeamId)
    {
        var protectedEntry = GetAllBattlePieces().FirstOrDefault(entry =>
            entry.Occupant.CampaignTeamId == protectedTeamId &&
            entry.Occupant.TeamName == unit.TeamName);
        if (protectedEntry.Occupant == null)
        {
            return false;
        }

        var distance = GetManhattanDistance(sourceGrid.Grid, protectedEntry.Grid.Grid);
        if (distance <= 1)
        {
            if (CanUseGuard(unit))
            {
                AppendBattleLog(unit, "Cooperation", "Ally guards the requested friendly team.");
                return TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.Guard, sourceGrid, sourceGrid),
                    unit);
            }

            MarkUnitActed(unit);
            AppendBattleLog(unit, "Cooperation", "Ally holds beside the requested friendly team.");
            return true;
        }

        if (!TryGetAiPathEndpointToward(sourceGrid, unit, protectedEntry.Grid, out var destination, out _, out _) || destination == sourceGrid)
        {
            return false;
        }

        AppendBattleLog(unit, "Cooperation", "Ally advances to protect the requested friendly team.");
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(BattleActionKind.Move, sourceGrid, destination),
            unit);
    }

    private void ExecuteOneAiAction()
    {
        if (IsTurnBannerInputLocked())
        {
            return;
        }

        var candidates = GetAiActingBattlePieces()
            .Where(entry => !HasUnitActed(entry.Occupant))
            .OrderBy(entry => entry.Occupant.TroopType)
            .ThenBy(entry => entry.Grid.Y)
            .ThenBy(entry => entry.Grid.X)
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        if (BattleAiPlanner.TryExecuteFirstAction(
            () => TryExecuteAiCooperationStage(candidates),
            () => TryExecuteAiSiegeStage(candidates),
            () => TryExecuteAiPrioritySupplyStage(candidates),
            () => TryExecuteBestAiSurvivalAction(candidates),
            () => TryExecuteAiRoadYield(candidates),
            () => TryExecuteBestAiOffensiveAction(candidates),
            () => TryExecuteAiFortressMissionAdvance(candidates),
            () => TryExecuteAiFallbackStage(candidates)))
        {
            return;
        }

        var waitingCandidate = candidates[0];
        FocusCameraOnBattleGrid(waitingCandidate.Grid);
        AppendBattleLog(waitingCandidate.Occupant, "AI", BattleFormat("log.ai.wait", "Decision: wait at {0}; no legal move or attack.", waitingCandidate.Grid));
        MarkUnitActed(waitingCandidate.Occupant);
    }

    private bool TryExecuteAiCooperationStage(IReadOnlyList<(BattleGridKey Grid, BattleOccupantInfo Occupant)> candidates)
    {
        foreach (var candidate in candidates.Where(candidate => GetCampaignTeam(candidate.Occupant)?.CooperationObjective == CooperationObjectiveSuggestRetreat))
        {
            if (TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.Retreat, candidate.Grid, candidate.Grid),
                    candidate.Occupant))
            {
                AppendBattleLog(candidate.Occupant, "Cooperation", "Ally follows the retreat recommendation.");
                return true;
            }
        }

        foreach (var candidate in candidates.Where(candidate => GetCampaignTeam(candidate.Occupant)?.CooperationObjective == CooperationObjectiveHoldPosition))
        {
            if (CanUseGuard(candidate.Occupant))
            {
                AppendBattleLog(candidate.Occupant, "Cooperation", "Ally holds the requested position.");
                TryExecuteBattleActionIntent(
                    new BattleActionIntent(BattleActionKind.Guard, candidate.Grid, candidate.Grid),
                    candidate.Occupant);
                return true;
            }

            MarkUnitActed(candidate.Occupant);
            AppendBattleLog(candidate.Occupant, "Cooperation", "Ally holds the requested position.");
            return true;
        }

        foreach (var candidate in candidates)
        {
            if (IsAttackCooperationObjective(GetCampaignTeam(candidate.Occupant)?.CooperationObjective, out var targetGrid) &&
                TryExecuteAiCooperationAttackObjective(candidate.Grid, candidate.Occupant, targetGrid))
            {
                return true;
            }
        }

        foreach (var candidate in candidates)
        {
            if (IsProtectCooperationObjective(GetCampaignTeam(candidate.Occupant)?.CooperationObjective, out var protectedTeamId) &&
                TryExecuteAiCooperationProtectObjective(candidate.Grid, candidate.Occupant, protectedTeamId))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryExecuteAiSiegeStage(IReadOnlyList<(BattleGridKey Grid, BattleOccupantInfo Occupant)> candidates)
    {
        foreach (var candidate in candidates.Where(candidate => candidate.Occupant.TroopType == TroopLadder))
        {
            if (TryExecuteAiLadderDeployment(candidate.Grid, candidate.Occupant))
            {
                return true;
            }
        }

        if (TryExecuteAiGateControl(candidates))
        {
            return true;
        }

        if (TryExecuteAiSiegeBreachAction(candidates))
        {
            return true;
        }

        foreach (var candidate in candidates.Where(candidate => IsAiPostGateBreachSiegeEngine(candidate.Occupant)))
        {
            if (TryExecuteAiPostGateBreachSiegeEngineAction(candidate.Grid, candidate.Occupant))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryExecuteAiPrioritySupplyStage(IReadOnlyList<(BattleGridKey Grid, BattleOccupantInfo Occupant)> candidates)
    {
        foreach (var candidate in candidates.Where(candidate => candidate.Occupant.TroopType == TroopSupplyCart))
        {
            if (TryExecuteAiPrioritySupply(candidate.Grid, candidate.Occupant))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryExecuteAiFallbackStage(IReadOnlyList<(BattleGridKey Grid, BattleOccupantInfo Occupant)> candidates)
    {
        foreach (var candidate in candidates.Where(candidate =>
                     candidate.Occupant.TroopType != TroopSupplyCart &&
                     !IsAiAmmoDepletedCatapult(candidate.Occupant)))
        {
            if (TryExecuteAiMoveAndAttack(candidate.Grid, candidate.Occupant))
            {
                return true;
            }
        }

        foreach (var movingCandidate in candidates.OrderBy(candidate => GetAiFallbackMovementPriority(candidate.Occupant)))
        {
            if (TryExecuteAiMove(movingCandidate.Grid, movingCandidate.Occupant))
            {
                return true;
            }
        }

        return false;
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
                !TryGetAiTeammateBlockedByRoad(blocker.Grid, destination, blocker.Occupant, candidates, out var delayedTeammate, out var enemyGrid))
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
            // Letting a unit "step aside" onto a bridge just replaces one road
            // blockage with another, often at the only crossing.
            .Where(grid => !_mapData!.GetCell(grid.X, grid.Y).HasBridgeHealth)
            .Where(grid => !IsAiSiegeApproachReservedGrid(grid, blocker.TeamName))
            .Where(IsAiSafeMovementDestination)
            .Where(grid => GetAiThreatScore(grid, blocker) <= currentThreat)
            .OrderBy(grid => GetAiThreatScore(grid, blocker))
            .ThenBy(grid => grid.Y)
            .ThenBy(grid => grid.X)
            .FirstOrDefault();
        return destination != default;
    }

    private bool IsAiSiegeApproachReservedGrid(BattleGridKey grid, string teamName)
    {
        if (_mapData == null || grid.Level != 0 || !IsWithinMap(grid.Grid))
        {
            return false;
        }

        var previousUnit = _selectedUnit;
        var previousUnitGrid = _selectedUnitGrid;
        try
        {
            foreach (var siegePiece in GetAllBattlePieces().Where(entry =>
                         entry.Occupant.TeamName == teamName &&
                         entry.Occupant.TroopType is TroopRam or TroopLadder))
            {
                if (siegePiece.Occupant.TroopType == TroopLadder && IsAiLadderDeploymentGrid(siegePiece.Grid))
                {
                    if (GetCarLadderGroundEndpoints(siegePiece.Grid.Grid).Any(endpoint => grid.Grid == endpoint))
                    {
                        return true;
                    }

                    continue;
                }

                if (siegePiece.Occupant.TroopType != TroopRam)
                {
                    continue;
                }

                _selectedUnit = siegePiece.Occupant;
                _selectedUnitGrid = siegePiece.Grid;
                foreach (var gate in GetAiGateGrids().Where(gateGrid =>
                             !_mapData.GetCell(gateGrid.X, gateGrid.Y).IsGateOpen &&
                             !_mapData.GetCell(gateGrid.X, gateGrid.Y).IsBroken))
                {
                    foreach (var approach in GetOrthogonalNeighbors(gate.Grid).Select(ToGroundGridKey))
                    {
                        var fullBudget = BattleMapData.Width * BattleMapData.Height * 2;
                        if (IsWithinMap(approach.Grid) &&
                            TryBuildMovePath(siegePiece.Grid, approach, fullBudget, fullBudget, out var path, grid) &&
                            path.Contains(grid))
                        {
                            return true;
                        }
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

    private bool TryGetAiTeammateBlockedByRoad(
        BattleGridKey blockerGrid,
        BattleGridKey yieldDestination,
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
                            !yieldPath.Contains(blockerGrid) ||
                            !TryBuildMovePath(
                                teammate.Grid,
                                approachGrid,
                                fullPathBudget,
                                fullPathBudget,
                                out _,
                                blockerGrid,
                                new HashSet<BattleGridKey> { yieldDestination }))
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

    private int GetAiBestOffensiveActionScore(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        var fireScores = GetAiFirePlans(sourceGrid, unit).Select(plan => plan.Score).ToList();
        fireScores.AddRange(GetAiExtinguishPlans(sourceGrid, unit).Select(plan => plan.Score));
        fireScores.AddRange(GetAiConfusionPlans(sourceGrid, unit).Select(plan => plan.Score));
        if (unit.Energy < NormalAttackEnergyCost || !CanUseAttackCommand(unit))
        {
            return fireScores.Count == 0
                ? 0
                : fireScores.Max() + GetAiCombatDecisionScore(unit);
        }

        var scores = CalculateAttackableGrids(sourceGrid, unit)
            .Where(grid => TryGetAiOrdinaryAttackTarget(grid, unit.TeamName, out _))
            .Select(grid =>
            {
                TryGetAiOrdinaryAttackTarget(grid, unit.TeamName, out var target);
                return GetAiOffensiveActionScore(grid, unit.TeamName, target, GetAttackDamageAgainst(unit, target), supportCount: 0);
            })
            .ToList();

        scores.AddRange(
            CalculateReachableGrids(sourceGrid, unit.Energy - NormalAttackEnergyCost, GetAvailableMoveRange(unit))
                .Where(grid => grid != sourceGrid && IsAiSafeMovementDestination(grid))
                .SelectMany(grid => CalculateAttackableGrids(grid, unit)
                    .Where(targetGrid => TryGetAiOrdinaryAttackTarget(targetGrid, unit.TeamName, out _))
                    .Select(targetGrid =>
                    {
                        TryGetAiOrdinaryAttackTarget(targetGrid, unit.TeamName, out var target);
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

            score = BattleAiScoring.GetExpectedStrategyScore(score, GetExtinguishFireSuccessChance(unit, targetGrid));
            score += GetOfficerTacticalIntelligence(unit.OfficerName) * 6;
            yield return new AiExtinguishPlan(targetGrid, score, protectedUnits, projectedDamage);
        }
    }

    private IEnumerable<AiConfusionPlan> GetAiConfusionPlans(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        if (!CanUseMentalStrategy(unit)) yield break;
        foreach (var targetGrid in CalculateMentalStrategyTargetGrids(sourceGrid, unit).Distinct())
        {
            if (targetGrid == sourceGrid) continue;
            var target = GetMentalStrategyTarget(unit, targetGrid);
            if (target == null || target.TroopCount <= 0 || !CanApplyMessStrategy(unit, target)) continue;
            var chance = GetMessStrategySuccessChance(unit, target);
            var value = BattleAiScoring.GetConfusionValue(target.TroopCount, IsGeneralCountedPiece(target.Category, target.OfficerName));
            yield return new AiConfusionPlan(targetGrid, GetBattleOfficerId(target), BattleAiScoring.GetExpectedStrategyScore(value, chance), chance);
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

            if (IsClosedGateGroundFireProtected(grid, occupant))
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
                   TryGetAiOrdinaryAttackTarget(targetGrid, unit.TeamName, out _));
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
            AddAiOffensiveCandidates(actions, sourceGrid, unit);
        }

        if (actions.Count == 0)
        {
            return false;
        }

        var scores = actions.Select(action => (Final: GetAiFinalOffensiveActionScore(action), Base: action.Score, action.Noise)).ToArray();
        var selectedIndex = BattleAiPlanner.SelectOffensiveActionIndex(scores);
        return ExecuteAiOffensiveAction(actions[selectedIndex]);
    }

    private void AddAiOffensiveCandidates(List<AiOffensiveAction> actions, BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        AddAiStrategyCandidates(actions, sourceGrid, unit);
        AddAiImmediateCombatCandidates(actions, sourceGrid, unit);
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

            return;
        }

        // An empty catapult should recover its ranged role instead of joining
        // fortress or generic close-approach movement at a bridge bottleneck.
        if (IsAiAmmoDepletedCatapult(unit))
        {
            return;
        }

        if (unit.HasAttackedThisTurn)
        {
            return;
        }
        AddAiEngineeringAndMovementCandidates(actions, sourceGrid, unit);
    }

    private void AddAiStrategyCandidates(List<AiOffensiveAction> actions, BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        foreach (var confusionPlan in GetAiConfusionPlans(sourceGrid, unit))
        {
            actions.Add(new AiOffensiveAction(sourceGrid, unit, confusionPlan.TargetGrid, null, null, null, null,
                confusionPlan.Score, GetAiDecisionNoise(sourceGrid, confusionPlan.TargetGrid, participantCount: 1))
            { ConfusionPlan = confusionPlan });
        }
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
    }

    private void AddAiImmediateCombatCandidates(List<AiOffensiveAction> actions, BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        if (unit.Energy >= NormalAttackEnergyCost && CanUseAttackCommand(unit))
        {
            foreach (var targetGrid in CalculateAttackableGrids(sourceGrid, unit))
            {
                if (TryGetAiOrdinaryAttackTarget(targetGrid, unit.TeamName, out var target))
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
    }

    private void AddAiEngineeringAndMovementCandidates(List<AiOffensiveAction> actions, BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
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

        foreach (var repairPlan in GetAiGateRepairPlans(sourceGrid, unit))
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
                GateRepairPlan = repairPlan
            });
        }

        if (unit.Energy >= NormalAttackEnergyCost && CanUseAttackCommand(unit))
        {
            foreach (var moveAttackPlan in CalculateReachableGrids(sourceGrid, unit.Energy - NormalAttackEnergyCost, GetAvailableMoveRange(unit))
                         .Where(grid => grid != sourceGrid && IsAiSafeMovementDestination(grid))
                         .SelectMany(grid => CalculateAttackableGrids(grid, unit)
                             .Where(targetGrid => TryGetAiOrdinaryAttackTarget(targetGrid, unit.TeamName, out _))
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
                        GetAiCommandIntelligence(sourceGrid, unit) * 15 -
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

    private bool ExecuteAiOffensiveAction(AiOffensiveAction chosenAction)
    {
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

        if (chosenAction.GateRepairPlan.HasValue)
        {
            return TryExecuteAiGateRepair(chosenAction.SourceGrid, chosenAction.Unit, chosenAction.GateRepairPlan.Value, chosenAction.Noise);
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

        if (chosenAction.ConfusionPlan.HasValue)
        {
            return TryExecuteAiConfusionStrategy(chosenAction.SourceGrid, chosenAction.Unit, chosenAction.ConfusionPlan.Value, chosenAction.Noise);
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

    private bool TryExecuteAiConfusionStrategy(BattleGridKey sourceGrid, BattleOccupantInfo unit, AiConfusionPlan plan, int noise)
    {
        var target = GetMentalStrategyTarget(unit, plan.TargetGrid);
        if (target == null || target.TroopCount <= 0 || !CanApplyMessStrategy(unit, target) ||
            GetBattleOfficerId(target) != plan.TargetOfficerId ||
            !CalculateMentalStrategyTargetGrids(sourceGrid, unit).Contains(plan.TargetGrid)) return false;
        FocusCameraOnBattleGrid(sourceGrid);
        AppendBattleLog(unit, "AI", BattleFormat("log.ai.confusion", "Decision: confuse {0}; success {1:N0}%, score {2}, variance {3}.",
            FormatLogUnit(target), plan.SuccessChance, plan.Score, noise));
        return TryExecuteBattleActionIntent(new BattleActionIntent(BattleActionKind.MentalStrategy, sourceGrid, plan.TargetGrid), unit);
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
            .Any(grid => TryGetAiOrdinaryAttackTarget(grid, unit.TeamName, out _)))
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

    private int GetOfficerTacticalIntelligence(string officerName)
    {
        return GetBattleOfficerData(officerName)?.Intelligence ?? BattleOfficerAiProfiles.GetTacticalIntelligence(officerName);
    }

    private int GetAiCommandIntelligence(BattleGridKey sourceGrid, BattleOccupantInfo unit)
    {
        if (unit.TroopType is not (TroopWorker or TroopSupplyCart or TroopCatapult or TroopLadder or TroopRam))
        {
            return GetOfficerTacticalIntelligence(unit.OfficerName);
        }

        var commander = GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName == unit.TeamName &&
                            IsGeneralCountedPiece(entry.Occupant.Category, entry.Occupant.OfficerName) &&
                            !IsMessed(entry.Occupant))
            .Select(entry => new
            {
                Intelligence = GetOfficerTacticalIntelligence(entry.Occupant.OfficerName),
                Distance = GetManhattanDistance(sourceGrid.Grid, entry.Grid.Grid)
            })
            .Where(entry => entry.Distance <= AiCommandRadius)
            .OrderByDescending(entry => entry.Intelligence)
            .ThenBy(entry => entry.Distance)
            .FirstOrDefault();
        return commander?.Intelligence ?? AiUncommandedSupportIntelligence;
    }

    private int GetAiCombatDecisionScore(BattleOccupantInfo unit)
    {
        return GetOfficerBattleAttribute(unit.OfficerName) * 6;
    }

    private int GetAiOfficerDecisionTieBreakScore(BattleOccupantInfo unit)
    {
        return GetOfficerTacticalIntelligence(unit.OfficerName) * 4 + GetOfficerBattleAttribute(unit.OfficerName) * 2;
    }

    private int GetAiFinalOffensiveActionScore(AiOffensiveAction action)
    {
        var intelligence = GetAiCommandIntelligence(action.SourceGrid, action.Unit);
        var combat = GetOfficerBattleAttribute(action.Unit.OfficerName);
        var isTacticalObjective = action.OutpostObjective.HasValue || action.BridgePlan != null || action.BridgeRepairPlan.HasValue || action.GateRepairPlan.HasValue || action.FencePlan != null || action.SupplyPlan.HasValue || action.ExtinguishPlan.HasValue || action.ConfusionPlan.HasValue || action.FirePlan.HasValue || action.IsGuardAction;
        var isDirectAttack = action.UnionCandidate == null &&
                             !action.MoveAttackDestination.HasValue &&
                             !action.ExtinguishPlan.HasValue &&
                             !action.ConfusionPlan.HasValue &&
                             !action.FirePlan.HasValue &&
                             !action.IsHideAction &&
                             !action.IsGuardAction &&
                             TryGetAiOrdinaryAttackTarget(action.TargetGrid, action.Unit.TeamName, out _);
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
            .Where(grid => TryGetAiOrdinaryAttackTarget(grid, unit.TeamName, out _))
            .OrderByDescending(grid =>
            {
                TryGetAiOrdinaryAttackTarget(grid, unit.TeamName, out var target);
                return GetAiOffensiveActionScore(grid, unit.TeamName, target, GetAttackDamageAgainst(unit, target), supportCount: 0);
            })
            .ThenBy(grid => GetManhattanDistance(sourceGrid.Grid, grid.Grid))
            .FirstOrDefault();
        if (targetGrid == default)
        {
            return false;
        }

        TryGetAiOrdinaryAttackTarget(targetGrid, unit.TeamName, out var target);
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
                .Where(targetGrid => TryGetAiOrdinaryAttackTarget(targetGrid, unit.TeamName, out _))
                .Select(targetGrid => (Grid: grid, Target: targetGrid)))
            .OrderByDescending(plan =>
            {
                TryGetAiOrdinaryAttackTarget(plan.Target, unit.TeamName, out var target);
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
        TryGetAiOrdinaryAttackTarget(plannedTarget, unit.TeamName, out var plannedTargetUnit);
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

        if (!TryGetAiOrdinaryAttackTarget(plannedTarget, currentUnit.TeamName, out _))
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


}
