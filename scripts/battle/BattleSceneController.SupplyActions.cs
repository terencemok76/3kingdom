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
    private void OnSupplyActionRequested()
    {
        TryExecuteSelectedBattleAction(BattleActionKind.Supply);
    }

    private void ExecuteSelectedSupply()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            _selectedUnit.Marker == null ||
            _selectedUnit.TroopType != TroopSupplyCart ||
            _selectedUnit.Energy < SupplyActionEnergyCost ||
            _supplyUsedByMarkerThisTurn.Contains(_selectedUnit.Marker))
        {
            return;
        }

        var suppliedUnits = GetSupplyMoraleTargets(_selectedUnitGrid.Value, _selectedUnit).ToList();
        var recoveryTargets = GetWoundedRecoveryTargets(_selectedUnitGrid.Value, _selectedUnit).ToList();
        var repairTargets = GetSupplyRepairTargets(_selectedUnitGrid.Value, _selectedUnit).ToList();
        var supportMultiplier = GetSupplyCartSupportMultiplier(_selectedUnit);
        if (suppliedUnits.Count == 0 && recoveryTargets.Count == 0 && repairTargets.Count == 0)
        {
            return;
        }

        var suppliedTargetCount = 0;
        foreach (var (targetGrid, target) in suppliedUnits)
        {
            if (UpdateUnitMorale(targetGrid, target, Mathf.Max(1, Mathf.RoundToInt(SupplyCartMoraleRestore * supportMultiplier)), out _))
            {
                suppliedTargetCount++;
            }
        }

        var recoveredTroops = 0;
        var recoveredTargetCount = 0;
        foreach (var (targetGrid, target) in recoveryTargets)
        {
            var targetRecoveredTroops = RecoverWoundedTroops(targetGrid, target, Mathf.Max(1, Mathf.RoundToInt(SupplyCartWoundedRecoveryAmount * supportMultiplier)));
            if (targetRecoveredTroops > 0)
            {
                recoveredTroops += targetRecoveredTroops;
                recoveredTargetCount++;
            }
        }

        var repairedHp = 0;
        var repairedTargetCount = 0;
        foreach (var (targetGrid, target) in repairTargets)
        {
            var targetRepairedHp = RepairSiegeEngine(targetGrid, target, Mathf.Max(1, Mathf.RoundToInt(SupplyCartRepairAmount * supportMultiplier)));
            if (targetRepairedHp > 0)
            {
                repairedHp += targetRepairedHp;
                repairedTargetCount++;
            }
        }

        var logParts = new List<string>();
        if (suppliedTargetCount > 0)
        {
            logParts.Add($"{suppliedTargetCount} unit(s) morale +{SupplyCartMoraleRestore}");
        }

        if (recoveredTargetCount > 0)
        {
            logParts.Add($"{recoveredTargetCount} unit(s) recovered {recoveredTroops:N0} wounded");
        }

        if (repairedTargetCount > 0)
        {
            logParts.Add($"{repairedTargetCount} car(s) repaired +{repairedHp:N0} HP");
        }

        if (logParts.Count == 0)
        {
            return;
        }

        if (!TrySpendSupplyActionEnergy())
        {
            return;
        }

        _supplyUsedByMarkerThisTurn.Add(_selectedUnit.Marker!);
        MarkUnitActed(_selectedUnit);

        AppendBattleLog(
            _selectedUnit,
            "Recovery",
            $"{FormatLogUnit(_selectedUnit)} recovery / repair: {string.Join(", ", logParts)}");

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        ConfigureHud();
        RefreshInfoPanel();
        RefreshHighlights();
        RefreshBattleLogPanel();
    }

    private void OnResupplyWeaponButtonPressed()
    {
        TryExecuteSelectedBattleAction(BattleActionKind.ResupplyWeapon);
    }

    private void ExecuteSelectedWeaponResupply()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            _selectedUnit.Marker == null ||
            _selectedUnit.TroopType != TroopSupplyCart ||
            _selectedUnit.Energy < SupplyActionEnergyCost ||
            _supplyUsedByMarkerThisTurn.Contains(_selectedUnit.Marker))
        {
            return;
        }

        var resupplyTargets = GetWeaponResupplyTargets(_selectedUnitGrid.Value, _selectedUnit).ToList();
        if (resupplyTargets.Count == 0)
        {
            return;
        }

        var refilledAmmo = 0;
        foreach (var (targetGrid, target) in resupplyTargets)
        {
            if (RefillWeaponAmmo(targetGrid, target, out var targetRefilledAmmo))
            {
                refilledAmmo += targetRefilledAmmo;
            }
        }

        if (!TrySpendSupplyActionEnergy())
        {
            return;
        }

        _supplyUsedByMarkerThisTurn.Add(_selectedUnit.Marker!);
        MarkUnitActed(_selectedUnit);
        AppendBattleLog(
            _selectedUnit,
            "Supply",
            $"{FormatLogUnit(_selectedUnit)} resupplies {resupplyTargets.Count} weapon unit(s). Ammo +{refilledAmmo:N0}");

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
        RefreshBattleLogPanel();
    }

    private void OnCaptureSupplyCartButtonPressed()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !TryGetCapturableSupplyCartTarget(out var targetGrid, out var supplyCart) ||
            supplyCart == null)
        {
            return;
        }

        var captor = _selectedUnit;
        var previousTeamName = supplyCart.TeamName;
        var capturedResources = ApplySupplyCartLostPenalty(supplyCart, "captured");
        ApplyTeamResourceDelta(captor.TeamName, capturedResources.GoldLoss, capturedResources.FoodLoss);
        ApplyTeamMoraleBonus(captor.TeamName, SupplyCartCaptureMoraleBonus, "captured supplies");
        ConvertBattlePieceToSide(targetGrid, supplyCart, captor.TeamName);
        MarkUnitActed(captor);
        TryShowOfficerSpeech(captor, BattleOfficerSpeechEvent.Capture);
        AppendBattleLog(
            captor,
            "Supply",
            $"{FormatLogUnit(captor)} captures {FormatLogUnit(supplyCart)} at {targetGrid}: gold +{capturedResources.GoldLoss:N0}, food +{capturedResources.FoodLoss:N0}. Supply Cart changes side: {previousTeamName} -> {captor.TeamName}");

        _commandMode = BattleCommandMode.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        HideCommandMenu();
        ConfigureHud();
        RefreshInfoPanel();
        RefreshHighlights();
        RefreshBattleLogPanel();
        RefreshBattleDepthLayerOrder();
        RefreshOccludedUnitSilhouettes();
    }

    private bool TryGetCapturableSupplyCartTarget(out BattleGridKey targetGrid, out BattleOccupantInfo? supplyCart)
    {
        targetGrid = default;
        supplyCart = default;
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || _selectedUnit.Category != CategoryUnit)
        {
            return false;
        }

        var selectedGrid = _selectedUnitGrid.Value;
        var selectedIsAttacker = IsAttackerPiece(_selectedUnit);
        foreach (var (grid, occupants) in GetAdjacentOccupantEntries(selectedGrid))
        {
            var cart = occupants.FirstOrDefault(occupant =>
                occupant.Category == CategorySiegeEngine &&
                occupant.TroopType == TroopSupplyCart &&
                IsAttackerPiece(occupant) != selectedIsAttacker);
            if (cart != null)
            {
                targetGrid = grid;
                supplyCart = cart;
                return true;
            }
        }

        return false;
    }

    private bool HasSupplyTargets()
    {
        return _selectedUnit != null &&
               _selectedUnitGrid.HasValue &&
               _selectedUnit.TroopType == TroopSupplyCart &&
               _selectedUnit.Marker != null &&
               _selectedUnit.Energy >= SupplyActionEnergyCost &&
               !_supplyUsedByMarkerThisTurn.Contains(_selectedUnit.Marker) &&
               (GetSupplyMoraleTargets(_selectedUnitGrid.Value, _selectedUnit).Any() ||
                GetWoundedRecoveryTargets(_selectedUnitGrid.Value, _selectedUnit).Any() ||
                GetSupplyRepairTargets(_selectedUnitGrid.Value, _selectedUnit).Any());
    }

    private bool HasWeaponResupplyTargets()
    {
        return _selectedUnit != null &&
               _selectedUnitGrid.HasValue &&
               _selectedUnit.TroopType == TroopSupplyCart &&
               _selectedUnit.Marker != null &&
               _selectedUnit.Energy >= SupplyActionEnergyCost &&
               !_supplyUsedByMarkerThisTurn.Contains(_selectedUnit.Marker) &&
               GetWeaponResupplyTargets(_selectedUnitGrid.Value, _selectedUnit).Any();
    }

    private bool TrySpendSupplyActionEnergy()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || _selectedUnit.Energy < SupplyActionEnergyCost)
        {
            return false;
        }

        var updatedSupplyCart = _selectedUnit with { Energy = _selectedUnit.Energy - SupplyActionEnergyCost };
        ReplaceOccupantAtGrid(_selectedUnitGrid.Value, _selectedUnit, updatedSupplyCart);
        _selectedUnit = updatedSupplyCart;
        return true;
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetSupplyMoraleTargets(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart)
    {
        foreach (var neighborGrid in GetAdjacentEightNeighbors(supplyGrid.Grid))
        {
            if (!IsWithinMap(neighborGrid))
            {
                continue;
            }

            foreach (var (targetGrid, target) in GetOccupantsAtGrid(neighborGrid))
            {
                if (targetGrid.Level != supplyGrid.Level ||
                    target.Morale == null ||
                    target.Morale.Value >= 120 ||
                    target.TeamName != supplyCart.TeamName ||
                    target.TroopType == TroopSupplyCart)
                {
                    continue;
                }

                yield return (targetGrid, target);
            }
        }
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetSupplyRepairTargets(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart)
    {
        foreach (var occupant in GetOccupantsAtGrid(supplyGrid.Grid))
        {
            if (IsSupplyRepairTarget(supplyGrid, supplyCart, occupant.Grid, occupant.Occupant, allowSelf: true))
            {
                yield return occupant;
            }
        }

        foreach (var neighborGrid in GetAdjacentEightNeighbors(supplyGrid.Grid))
        {
            if (!IsWithinMap(neighborGrid))
            {
                continue;
            }

            foreach (var occupant in GetOccupantsAtGrid(neighborGrid))
            {
                if (IsSupplyRepairTarget(supplyGrid, supplyCart, occupant.Grid, occupant.Occupant, allowSelf: false))
                {
                    yield return occupant;
                }
            }
        }
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetWoundedRecoveryTargets(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart)
    {
        foreach (var neighborGrid in GetAdjacentEightNeighbors(supplyGrid.Grid))
        {
            if (!IsWithinMap(neighborGrid))
            {
                continue;
            }

            foreach (var (targetGrid, target) in GetOccupantsAtGrid(neighborGrid))
            {
                if (targetGrid.Level != supplyGrid.Level ||
                    target.TeamName != supplyCart.TeamName ||
                    target.Category != CategoryUnit ||
                    target.WoundedTroops <= 0)
                {
                    continue;
                }

                yield return (targetGrid, target);
            }
        }
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetWeaponResupplyTargets(BattleGridKey supplyGrid, BattleOccupantInfo supplyCart)
    {
        foreach (var neighborGrid in GetAdjacentEightNeighbors(supplyGrid.Grid))
        {
            if (!IsWithinMap(neighborGrid))
            {
                continue;
            }

            foreach (var occupant in GetOccupantsAtGrid(neighborGrid))
            {
                if (IsWeaponResupplyTarget(supplyGrid, supplyCart, occupant.Grid, occupant.Occupant))
                {
                    yield return occupant;
                }
            }
        }
    }

    private static bool IsSupplyRepairTarget(
        BattleGridKey supplyGrid,
        BattleOccupantInfo supplyCart,
        BattleGridKey targetGrid,
        BattleOccupantInfo target,
        bool allowSelf)
    {
        var isSelf = target.Marker != null && target.Marker == supplyCart.Marker;
        return targetGrid.Level == supplyGrid.Level &&
               target.TeamName == supplyCart.TeamName &&
               target.Category == CategorySiegeEngine &&
               target.HitPoints < target.MaxHitPoints &&
               (allowSelf || !isSelf);
    }

    private static bool IsWeaponResupplyTarget(
        BattleGridKey supplyGrid,
        BattleOccupantInfo supplyCart,
        BattleGridKey targetGrid,
        BattleOccupantInfo target)
    {
        return targetGrid.Level == supplyGrid.Level &&
               target.TeamName == supplyCart.TeamName &&
               target.WeaponAmmo.HasValue &&
               target.MaxWeaponAmmo.HasValue &&
               target.WeaponAmmo.Value < target.MaxWeaponAmmo.Value;
    }

    private int RepairSiegeEngine(BattleGridKey targetGrid, BattleOccupantInfo target, int repairAmount)
    {
        if (!TryGetCurrentOccupantAtGrid(targetGrid, target, out var currentTarget))
        {
            return 0;
        }

        target = currentTarget;
        if (!BattleSupplySystem.TryRepairSiegeEngine(target, repairAmount, out var updatedTarget, out var actualRepair))
        {
            return 0;
        }

        UpdateMarkerStrengthBar(updatedTarget);
        ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedTarget;
        }

        ShowRepairPopup(targetGrid, actualRepair);
        return actualRepair;
    }

    private static float GetSupplyCartSupportMultiplier(BattleOccupantInfo supplyCart)
    {
        if (supplyCart.MaxHitPoints <= 0)
        {
            return 1.0f;
        }

        return supplyCart.HitPoints / (float)supplyCart.MaxHitPoints <= SiegeEngineCriticalHpRatio
            ? CriticalSupplyCartSupportRatio
            : 1.0f;
    }

    private int RecoverWoundedTroops(BattleGridKey targetGrid, BattleOccupantInfo target, int recoveryAmount)
    {
        if (!TryGetCurrentOccupantAtGrid(targetGrid, target, out var currentTarget))
        {
            return 0;
        }

        target = currentTarget;
        if (!BattleSupplySystem.TryRecoverWoundedTroops(target, recoveryAmount, out var updatedTarget, out var actualRecovery))
        {
            return 0;
        }

        UpdateMarkerStrengthBar(updatedTarget);
        ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
        ApplyTeamTroopDelta(target.Category, target.TeamName, actualRecovery);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedTarget;
        }

        ShowRepairPopup(targetGrid, actualRecovery);
        return actualRecovery;
    }
}
