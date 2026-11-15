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

        var commandIntelligence = GetAiCommandIntelligence(sourceGrid, supplyCart);
        var positions = new List<BattleGridKey> { sourceGrid };
        positions.AddRange(CalculateReachableGrids(sourceGrid, supplyCart.Energy - SupplyActionEnergyCost, GetAvailableMoveRange(supplyCart))
            .Where(grid => grid != sourceGrid && IsAiSafeMovementDestination(grid)));
        var facts = positions.Select(grid => (
            Recovery: GetAiSupplyActionScore(grid, supplyCart, commandIntelligence),
            Weapon: GetAiWeaponResupplyActionScore(grid, supplyCart, commandIntelligence),
            Distance: GetManhattanDistance(sourceGrid.Grid, grid.Grid))).ToArray();
        foreach (var plan in BattleAiSupplyPlanner.BuildPlans(facts))
        {
            var move = plan.Index != 0;
            var kind = plan.Weapon ? AiSupplyActionKind.WeaponResupply : AiSupplyActionKind.RecoveryRepair;
            var reason = plan.Weapon
                ? (move ? "move and resupply weapon units" : "resupply adjacent weapon units")
                : (move ? "move and supply team" : "supply adjacent team");
            yield return new AiSupplyPlan(positions[plan.Index], move, kind, plan.Score, reason);
        }
    }

    private int GetAiSupplyActionScore(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart, int commandIntelligence)
    {
        var supportMultiplier = GetSupplyCartSupportMultiplier(supplyCart);
        var moraleScore = GetSupplyMoraleTargets(supplyGrid, supplyCart)
            .Sum(target => BattleAiSupplyPlanner.MoraleScore(target.Occupant.Morale.GetValueOrDefault(DefaultUnitMorale), supportMultiplier));
        var recoveryScore = GetWoundedRecoveryTargets(supplyGrid, supplyCart)
            .Sum(target => BattleAiSupplyPlanner.RecoveryScore(target.Occupant.WoundedTroops, supportMultiplier));
        var repairScore = GetSupplyRepairTargets(supplyGrid, supplyCart)
            .Where(target => target.Occupant.TroopType != TroopSupplyCart)
            .Sum(target => BattleAiSupplyPlanner.RepairScore(target.Occupant.MaxHitPoints - target.Occupant.HitPoints, supportMultiplier));
        var criticalSupportScore = GetWoundedRecoveryTargets(supplyGrid, supplyCart)
            .Concat(GetSupplyMoraleTargets(supplyGrid, supplyCart))
            .Concat(GetSupplyRepairTargets(supplyGrid, supplyCart))
            .Where(target => target.Occupant.TroopType is TroopCatapult or TroopRam or TroopLadder ||
                             IsGeneralCountedPiece(target.Occupant.Category, target.Occupant.OfficerName))
            .DistinctBy(target => target.Grid)
            .Count() * commandIntelligence * 4;
        return moraleScore + recoveryScore + repairScore + criticalSupportScore;
    }

    private int GetAiWeaponResupplyActionScore(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart, int commandIntelligence)
    {
        return GetWeaponResupplyTargets(supplyGrid, supplyCart).Sum(target =>
        {
            return BattleAiSupplyPlanner.WeaponScore(target.Occupant.WeaponAmmo.GetValueOrDefault(),
                target.Occupant.MaxWeaponAmmo.GetValueOrDefault(), GetBaseAttackDamage(target.Occupant),
                target.Occupant.TroopType == TroopCatapult, commandIntelligence);
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

        var commandIntelligence = GetAiCommandIntelligence(sourceGrid, supplyCart);
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
                    GetAiSupplyActionScore(actionGrid, supplyCart, commandIntelligence),
                    GetAiWeaponResupplyActionScore(actionGrid, supplyCart, commandIntelligence));
                if (score <= 0 ||
                    !TryGetAiPathEndpointToward(sourceGrid, supplyCart, actionGrid, out var nextDestination, out var pathEnergy, out var pathSteps))
                {
                    continue;
                }

                candidates.Add((actionGrid, nextDestination, score, pathEnergy, pathSteps));
            }
        }

        var bestIndex = BattleAiSupplyPlanner.SelectApproachIndex(candidates
            .Select(candidate => (candidate.Score, candidate.FullPathEnergy, candidate.FullPathSteps)).ToArray());
        if (bestIndex < 0)
        {
            return false;
        }

        var best = candidates[bestIndex];
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
        return BattleAiSupplyPlanner.NeedsSupport(candidate.Marker == supplyCart.Marker,
            candidate.Category == CategorySiegeEngine, candidate.HitPoints, candidate.MaxHitPoints,
            candidate.WoundedTroops, candidate.Morale, candidate.WeaponAmmo, candidate.MaxWeaponAmmo);
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
}
