using System;
using System.Collections.Generic;
using System.Linq;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private BattleActionRuleQueries<BattleOccupantInfo>? _battleActionRuleQueries;
    private BattleActionExecutor<BattleOccupantInfo>? _battleActionExecutor;

    private bool TryExecuteSelectedBattleAction(BattleActionKind kind, bool useWoodFenceWork = false)
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue)
        {
            return false;
        }

        var requiresTarget = BattleActionValidator.RequiresDistinctTarget(kind);
        if (requiresTarget && !_selectedGrid.HasValue)
        {
            return false;
        }

        var targetGrid = requiresTarget
            ? _selectedGridKey ?? GetDefaultGridKey(_selectedGrid!.Value)
            : _selectedUnitGrid.Value;
        return TryExecuteBattleActionIntent(
            new BattleActionIntent(kind, _selectedUnitGrid.Value, targetGrid, UseWoodFenceWork: useWoodFenceWork),
            _selectedUnit);
    }

    private BattleActionRuleQueries<BattleOccupantInfo> ActionRuleQueries =>
        _battleActionRuleQueries ??= new BattleActionRuleQueries<BattleOccupantInfo>(
            new Dictionary<BattleActionKind, Func<BattleActionIntent, BattleOccupantInfo, bool>>
            {
                [BattleActionKind.Move] = (intent, unit) =>
                    unit.Energy - intent.ReservedEnergy > 0 &&
                    CalculateReachableGrids(intent.SourceGrid, unit.Energy - intent.ReservedEnergy, GetAvailableMoveRange(unit)).Contains(intent.TargetGrid),
                [BattleActionKind.Attack] = (intent, unit) => CalculateAttackableGrids(intent.SourceGrid, unit).Contains(intent.TargetGrid),
                [BattleActionKind.Supply] = (_, _) => true,
                [BattleActionKind.ResupplyWeapon] = (intent, unit) =>
                    unit.TroopType == TroopSupplyCart &&
                    unit.Energy >= SupplyActionEnergyCost &&
                    unit.Marker != null &&
                    !_supplyUsedByMarkerThisTurn.Contains(unit.Marker) &&
                    GetWeaponResupplyTargets(intent.SourceGrid, unit).Any(),
                [BattleActionKind.Guard] = (_, unit) => CanUseGuard(unit),
                [BattleActionKind.Hide] = (intent, unit) => CanHideAtGrid(intent.SourceGrid, unit),
                [BattleActionKind.Work] = IsWorkerWorkIntentLegal,
                [BattleActionKind.Retreat] = (_, unit) => IsBattlePiece(unit),
                [BattleActionKind.Extinguish] = (intent, unit) => CalculateExtinguishStrategyTargetGrids(intent.SourceGrid, unit).Contains(intent.TargetGrid),
                [BattleActionKind.FireStrategy] = (intent, unit) => CalculateFireStrategyTargetGrids(intent.SourceGrid, unit).Contains(intent.TargetGrid),
                [BattleActionKind.MentalStrategy] = (intent, unit) => CalculateMentalStrategyTargetGrids(intent.SourceGrid, unit).Contains(intent.TargetGrid),
                [BattleActionKind.Charge] = (intent, unit) => CalculateChargeTargetGrids(intent.SourceGrid, unit).Contains(intent.TargetGrid),
                [BattleActionKind.Duel] = (intent, unit) => CalculateDuelTargetGrids(intent.SourceGrid, unit).Contains(intent.TargetGrid),
                [BattleActionKind.HireOfficer] = (intent, _) => CalculateHireOfficerTargetGrids(intent.SourceGrid).Contains(intent.TargetGrid)
            });

    private BattleActionExecutor<BattleOccupantInfo> ActionExecutor =>
        _battleActionExecutor ??= new BattleActionExecutor<BattleOccupantInfo>(
            new Dictionary<BattleActionKind, Func<BattleActionIntent, BattleOccupantInfo, Action?, bool>>
            {
                [BattleActionKind.Move] = ExecuteMoveActionIntent,
                [BattleActionKind.Attack] = ExecuteAttackActionIntent,
                [BattleActionKind.Supply] = ExecuteSupplyActionIntent,
                [BattleActionKind.ResupplyWeapon] = ExecuteWeaponResupplyActionIntent,
                [BattleActionKind.Guard] = ExecuteGuardActionIntent,
                [BattleActionKind.Hide] = ExecuteHideActionIntent,
                [BattleActionKind.Work] = ExecuteWorkActionIntent,
                [BattleActionKind.Retreat] = ExecuteRetreatActionIntent,
                [BattleActionKind.Extinguish] = ExecuteStrategyActionIntent,
                [BattleActionKind.FireStrategy] = ExecuteStrategyActionIntent,
                [BattleActionKind.MentalStrategy] = ExecuteStrategyActionIntent,
                [BattleActionKind.Charge] = ExecuteChargeActionIntent,
                [BattleActionKind.Duel] = ExecuteDuelActionIntent,
                [BattleActionKind.HireOfficer] = ExecuteHireOfficerActionIntent
            });

    private bool IsWorkerWorkIntentLegal(BattleActionIntent intent, BattleOccupantInfo unit)
    {
        if (_mapData == null || intent.TargetGrid.Level != 0 ||
            GetManhattanDistance(intent.SourceGrid.Grid, intent.TargetGrid.Grid) != 1)
        {
            return false;
        }

        var targetCell = _mapData.GetCell(intent.TargetGrid.X, intent.TargetGrid.Y);
        var workAction = intent.UseWoodFenceWork ? WorkerWorkAction.WoodFence : WorkerWorkAction.General;
        return IsWorkerWorkTargetForAction(intent.TargetGrid.Grid, targetCell, workAction) &&
               unit.Energy >= GetWorkerWorkEnergyCost(targetCell, workAction);
    }

    private bool TryExecuteBattleActionIntent(BattleActionIntent intent, BattleOccupantInfo unit, Action? onMoveAnimationComplete = null)
    {
        if (!BattleActionValidator.IsStructurallyValid(intent))
        {
            return false;
        }

        _selectedUnit = unit;
        _selectedUnitGrid = intent.SourceGrid;
        _selectedGrid = intent.TargetGrid.Grid;
        _selectedGridKey = intent.TargetGrid;
        if (!ActionRuleQueries.IsLegal(intent, unit))
        {
            return false;
        }

        return ActionExecutor.TryExecute(intent, unit, onMoveAnimationComplete);
    }

    private bool ExecuteMoveActionIntent(BattleActionIntent intent, BattleOccupantInfo _, Action? onMoveAnimationComplete)
    {
        _movableGrids.Clear();
        _movableGrids.Add(intent.TargetGrid);
        return TryMoveSelectedUnit(intent.MarkActedAfterMove, onMoveAnimationComplete);
    }

    private bool ExecuteAttackActionIntent(BattleActionIntent intent, BattleOccupantInfo _, Action? __)
    {
        _attackableGrids.Clear();
        _attackableGrids.Add(intent.TargetGrid);
        return TryAttackSelectedTarget();
    }

    private bool ExecuteSupplyActionIntent(BattleActionIntent _, BattleOccupantInfo unit, Action? __)
    {
        ExecuteSelectedSupply();
        return HasUnitActed(unit);
    }

    private bool ExecuteWeaponResupplyActionIntent(BattleActionIntent _, BattleOccupantInfo unit, Action? __)
    {
        ExecuteSelectedWeaponResupply();
        return HasUnitActed(unit);
    }

    private bool ExecuteGuardActionIntent(BattleActionIntent _, BattleOccupantInfo unit, Action? __)
    {
        ExecuteSelectedGuard();
        return HasUnitActed(unit);
    }

    private bool ExecuteHideActionIntent(BattleActionIntent _, BattleOccupantInfo unit, Action? __)
    {
        ExecuteSelectedHide();
        return HasUnitActed(unit);
    }

    private bool ExecuteWorkActionIntent(BattleActionIntent intent, BattleOccupantInfo _, Action? __)
    {
        _workerWorkAction = intent.UseWoodFenceWork ? WorkerWorkAction.WoodFence : WorkerWorkAction.General;
        _workableGrids.Clear();
        _workableGrids.Add(intent.TargetGrid);
        return TryPerformWorkerWork();
    }

    private bool ExecuteRetreatActionIntent(BattleActionIntent intent, BattleOccupantInfo unit, Action? __)
    {
        ExecuteSelectedRetreat();
        return !TryGetCurrentOccupantAtGrid(intent.SourceGrid, unit, out _);
    }

    private bool ExecuteStrategyActionIntent(BattleActionIntent intent, BattleOccupantInfo _, Action? __)
    {
        _selectedStrategyAction = intent.Kind switch
        {
            BattleActionKind.Extinguish => BattleStrategyAction.Extinguish,
            BattleActionKind.FireStrategy => BattleStrategyAction.Fire,
            _ => BattleStrategyAction.Mental
        };
        _strategyTargetGrids.Clear();
        _strategyTargetGrids.Add(intent.TargetGrid);
        return TryExecuteSelectedStrategy();
    }

    private bool ExecuteChargeActionIntent(BattleActionIntent intent, BattleOccupantInfo _, Action? __)
    {
        _chargeTargetGrids.Clear();
        _chargeTargetGrids.Add(intent.TargetGrid);
        return TryExecuteSelectedCharge();
    }

    private bool ExecuteDuelActionIntent(BattleActionIntent intent, BattleOccupantInfo _, Action? __)
    {
        _duelTargetGrids.Clear();
        _duelTargetGrids.Add(intent.TargetGrid);
        return TryExecuteSelectedDuel();
    }

    private bool ExecuteHireOfficerActionIntent(BattleActionIntent intent, BattleOccupantInfo _, Action? __)
    {
        _hireOfficerTargetGrids.Clear();
        _hireOfficerTargetGrids.Add(intent.TargetGrid);
        return TryExecuteSelectedHireOfficer();
    }
}
