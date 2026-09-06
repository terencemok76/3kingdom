using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void ShowCommandMenu(Vector2 screenPosition)
    {
        if (_commandMenu == null)
        {
            return;
        }

        var canCommandSelectedUnit = _selectedUnit != null && IsCurrentTurnPiece(_selectedUnit);

        if (_unitMenuInfoLabel != null)
        {
            if (_selectedUnit != null)
            {
                var strengthText = _selectedUnit.Category == CategorySiegeEngine
                    ? BattleFormat("ui.battle.menu_hp", "HP: {0}/{1}", _selectedUnit.HitPoints, _selectedUnit.MaxHitPoints)
                    : BattleFormat("ui.battle.menu_active_wounded", "Troops: {0:N0} ({1})", _selectedUnit.TroopCount, FormatWoundedTroops(_selectedUnit));
                var officerText = FormatOfficerName(_selectedUnit.OfficerName);
                var infoLines = new List<string>();
                if (IsGeneralCountedPiece(_selectedUnit.Category, _selectedUnit.OfficerName))
                {
                    infoLines.Add(BattleFormat("ui.battle.menu_officer", "Officer: {0}", officerText));
                    infoLines.Add(BattleFormat("ui.battle.menu_intelligence", "Intelligence: {0}", GetOfficerTacticalIntelligence(_selectedUnit.OfficerName)));
                    infoLines.Add(BattleFormat("ui.battle.menu_combat", "Combat: {0}", GetOfficerBattleAttribute(_selectedUnit.OfficerName)));
                }

                infoLines.Add(BattleFormat("ui.battle.menu_type", "Type: {0}", FormatTroopType(_selectedUnit.TroopType)));
                infoLines.Add(BattleFormat("ui.battle.menu_status", "Status: {0}", FormatBattleStatus(_selectedUnit)));
                infoLines.Add(BattleFormat("ui.battle.menu_command", "Command: {0}", _selectedUnit.HasAttackedThisTurn
                    ? BattleText("ui.battle.already_used_this_turn", "Already used this turn")
                    : canCommandSelectedUnit
                        ? BattleText("ui.battle.ready", "Ready")
                        : BattleFormat("ui.battle.not_acting_side", "Not Acting Side ({0})", FormatTeamName(GetCurrentTurnSideName()))));
                infoLines.Add(BattleFormat("ui.battle.menu_energy", "Energy: {0}/{1}", _selectedUnit.Energy, GetTeamEnergyCap(_selectedUnit.TeamName)));
                infoLines.Add(BattleFormat("ui.battle.menu_move_range", "Move Range: {0}/{1}", _selectedUnit.RemainingMoveRange, GetTeamMoveRangeCap(_selectedUnit)));
                if (_selectedUnit.Category != CategorySiegeEngine)
                {
                    infoLines.Add(BattleFormat("ui.battle.menu_morale", "Morale: {0}", FormatMorale(_selectedUnit)));
                }
                infoLines.Add(strengthText);
                _unitMenuInfoLabel.Text = string.Join("\n", infoLines);
            }
            else
            {
                _unitMenuInfoLabel.Text = BuildEmptyUnitMenuInfoText();
            }
        }

        RefreshOfficerPortrait();

        if (_openGateButton != null)
        {
            if (canCommandSelectedUnit && TryGetSwitchableGate(out var switchGateGrid) && _mapData != null)
            {
                var switchGateCell = _mapData.GetCell(switchGateGrid.X, switchGateGrid.Y);
                _openGateButton.Text = switchGateCell.IsGateOpen
                    ? BattleText("ui.battle.close_gate", "Close Gate")
                    : BattleText("ui.battle.open_gate", "Open Gate");
                _openGateButton.Visible = true;
            }
            else
            {
                _openGateButton.Visible = false;
            }
        }

        var canUseWallTopAttack = TryGetWallTopAttackGrid(out _);
        if (_dropStoneButton != null)
        {
            var canDropStone = canCommandSelectedUnit &&
                               _selectedUnitGrid?.Level == 2 &&
                               IsWallTopGrid(_selectedUnitGrid.Value.Grid) &&
                               canUseWallTopAttack &&
                               GetWallTopAttackUsesRemaining(isDropStone: true) > 0;
            _dropStoneButton.Visible = canDropStone;
            _dropStoneButton.Text = BattleFormat("ui.battle.drop_stone_count", "Drop Stone ({0})", GetWallTopAttackUsesRemaining(isDropStone: true));
            _dropStoneButton.Disabled = !canDropStone;
        }

        if (_pourOilButton != null)
        {
            var canPourOil = canCommandSelectedUnit &&
                             _selectedUnitGrid?.Level == 2 &&
                             IsWallTopGrid(_selectedUnitGrid.Value.Grid) &&
                             canUseWallTopAttack &&
                             GetWallTopAttackUsesRemaining(isDropStone: false) > 0;
            _pourOilButton.Visible = canPourOil;
            _pourOilButton.Text = BattleFormat("ui.battle.pour_oil_count", "Pour Oil ({0})", GetWallTopAttackUsesRemaining(isDropStone: false));
            _pourOilButton.Disabled = !canPourOil;
        }

        if (_workButton != null)
        {
            var hasBridgeWorkTarget = canCommandSelectedUnit &&
                                      _selectedUnit != null &&
                                      HasWorkTarget(_selectedUnit, WorkerWorkAction.General);
            _workButton.Visible = hasBridgeWorkTarget;
            _workButton.Text = _selectedUnit?.TroopType == TroopWorker
                ? BattleText("ui.battle.bridge", "Bridge")
                : BattleFormat(
                    "ui.battle.repair_bridge",
                    "Repair Bridge (+{0} HP, Energy {1})",
                    BattleBridgeSystem.EmergencyRepairAmount,
                    BattleBridgeSystem.EmergencyRepairEnergyCost);
            _workButton.Disabled = !hasBridgeWorkTarget;
        }

        if (_installWoodFenceButton != null)
        {
            var hasWoodFenceWorkTarget = canCommandSelectedUnit &&
                                         _selectedUnit?.TroopType == TroopWorker &&
                                         HasWorkTarget(_selectedUnit, WorkerWorkAction.WoodFence);
            _installWoodFenceButton.Visible = hasWoodFenceWorkTarget;
            _installWoodFenceButton.Text = $"{BattleText("ui.battle.wood_fence", "Wood Fence")} ({WorkerInstallWoodFenceEnergyCost}/{WorkerRemoveWoodFenceEnergyCost})";
            _installWoodFenceButton.Disabled = !hasWoodFenceWorkTarget;
        }

        if (_uninstallWoodFenceButton != null)
        {
            _uninstallWoodFenceButton.Visible = false;
            _uninstallWoodFenceButton.Disabled = false;
        }

        if (_supplyButton != null)
        {
            var hasSupplyTargets = canCommandSelectedUnit &&
                                   _selectedUnit?.TroopType == TroopSupplyCart &&
                                   HasSupplyTargets();
            _supplyButton.Visible = hasSupplyTargets;
            _supplyButton.Text = BattleFormat("ui.battle.recovery_repair", "Recovery / Repair (+{0} Morale, +{1} HP, Energy {2})", SupplyCartMoraleRestore, SupplyCartRepairAmount, SupplyActionEnergyCost);
            _supplyButton.Disabled = !hasSupplyTargets;
        }

        if (_resupplyWeaponButton != null)
        {
            var hasWeaponResupplyTargets = canCommandSelectedUnit &&
                                           _selectedUnit?.TroopType == TroopSupplyCart &&
                                           HasWeaponResupplyTargets();
            _resupplyWeaponButton.Visible = hasWeaponResupplyTargets;
            _resupplyWeaponButton.Text = BattleFormat("ui.battle.resupply_weapon", "Resupply Weapon (Energy {0})", SupplyActionEnergyCost);
            _resupplyWeaponButton.Disabled = !hasWeaponResupplyTargets;
        }

        if (_captureSupplyCartButton != null)
        {
            var hasCaptureTarget = canCommandSelectedUnit && TryGetCapturableSupplyCartTarget(out _, out _);
            _captureSupplyCartButton.Visible = hasCaptureTarget;
            _captureSupplyCartButton.Text = BattleText("ui.battle.capture_cart", "Capture Cart");
            _captureSupplyCartButton.Disabled = !hasCaptureTarget;
        }

        if (_hireOfficerButton != null)
        {
            var hasHireTarget = canCommandSelectedUnit &&
                                _selectedUnitGrid.HasValue &&
                                CalculateHireOfficerTargetGrids(_selectedUnitGrid.Value).Any();
            var hireCost = _selectedUnit == null ? 0 : GetHireOfficerGoldCost(_selectedUnit);
            var canHireOfficer = hasHireTarget &&
                                 _selectedUnit != null &&
                                 GetTeamGold(_selectedUnit.TeamName) >= hireCost;
            _hireOfficerButton.Visible = canHireOfficer;
            _hireOfficerButton.Text = BattleText("ui.battle.hire_officer", "Hire Officer");
            _hireOfficerButton.Disabled = !canHireOfficer;
        }

        if (_attackButton != null)
        {
            var hasAttackTarget = canCommandSelectedUnit &&
                                  _selectedUnit != null &&
                                  _selectedUnitGrid.HasValue &&
                                  _selectedUnit.Energy >= NormalAttackEnergyCost &&
                                  CanUseAttackCommand(_selectedUnit) &&
                                  CalculateAttackableGrids(_selectedUnitGrid.Value, _selectedUnit).Any();
            _attackButton.Visible = hasAttackTarget;
            _attackButton.Text = _selectedUnit != null && CanUseAmmoDepletedWeakAttack(_selectedUnit)
                ? BattleFormat("ui.battle.attack_weak_close_energy", "Attack (Weak Close, Ammo 0, Energy {0})", NormalAttackEnergyCost)
                : _selectedUnit?.MaxWeaponAmmo.HasValue == true
                ? BattleFormat("ui.battle.attack_ammo_energy", "Attack (Ammo {0}, Energy {1})", FormatWeaponAmmo(_selectedUnit), NormalAttackEnergyCost)
                : BattleFormat("ui.battle.attack_energy", "Attack (Energy {0})", NormalAttackEnergyCost);
            _attackButton.Disabled = !hasAttackTarget;
        }

        if (_strategyButton != null)
        {
            var strategyAction = canCommandSelectedUnit && _selectedUnit != null ? ResolveStrategyAction(_selectedUnit, _selectedUnitGrid) : BattleStrategyAction.None;
            var hasStrategyTarget = strategyAction != BattleStrategyAction.None &&
                                    _selectedUnit != null &&
                                    _selectedUnitGrid.HasValue &&
                                    (strategyAction == BattleStrategyAction.Extinguish
                                        ? CalculateExtinguishStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit).Any()
                                        : strategyAction == BattleStrategyAction.Fire
                                            ? CalculateFireStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit).Any()
                                            : CalculateMentalStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit).Any());
            _strategyButton.Visible = hasStrategyTarget;
            _strategyButton.Text = strategyAction == BattleStrategyAction.Extinguish
                ? BattleFormat("ui.battle.strategy_extinguish_energy", "Strategy (Extinguish, Energy {0})", ExtinguishFireEnergyCost)
                : strategyAction == BattleStrategyAction.Fire && _selectedUnit?.MaxWeaponAmmo.HasValue == true
                ? BattleFormat("ui.battle.strategy_fire_ammo", "Strategy (Fire, Ammo {0})", FormatWeaponAmmo(_selectedUnit))
                : strategyAction == BattleStrategyAction.Fire
                    ? BattleText("ui.battle.strategy_fire", "Strategy (Fire)")
                    : BattleFormat("ui.battle.strategy_mess_calm", "Strategy (Mess / Calm, Range {0})", MessStrategyRange);
            _strategyButton.Disabled = !hasStrategyTarget;
        }

        if (_unionAttackButton != null)
        {
            if (canCommandSelectedUnit && TryGetBestUnionAttackCandidate(out var unionAttackCandidate))
            {
                _unionAttackButton.Visible = true;
                _unionAttackButton.Text = BattleFormat(
                    "ui.battle.union_attack_cost",
                    "Union Attack ({0}: Lead {1}, Support {2})",
                    unionAttackCandidate.Participants.Count,
                    NormalAttackEnergyCost,
                    UnionAttackSupportEnergyCost);
                _unionAttackButton.Disabled = false;
            }
            else
            {
                _unionAttackButton.Visible = false;
                _unionAttackButton.Disabled = false;
                _unionAttackButton.Text = BattleText("ui.battle.union_attack", "Union Attack");
            }
        }

        if (_guardButton != null)
        {
            var canGuard = canCommandSelectedUnit && _selectedUnit != null && CanUseGuard(_selectedUnit);
            _guardButton.Visible = canGuard;
            _guardButton.Disabled = !canGuard;
            _guardButton.Text = BattleFormat("ui.battle.guard_cost", "Guard ({0} Energy)", NormalAttackEnergyCost);
        }

        if (_chargeButton != null)
        {
            var hasChargeTarget = canCommandSelectedUnit &&
                                  _selectedUnit != null &&
                                  _selectedUnitGrid.HasValue &&
                                  CanUseChargeCommand(_selectedUnit) &&
                                  CalculateChargeTargetGrids(_selectedUnitGrid.Value, _selectedUnit).Any();
            _chargeButton.Visible = canCommandSelectedUnit &&
                                    _selectedUnit != null &&
                                    _selectedUnit.TroopType == TroopCavalry &&
                                    hasChargeTarget;
            _chargeButton.Disabled = !hasChargeTarget;
            _chargeButton.Text = BattleText("ui.battle.charge", "Charge");
        }

        if (_duelButton != null)
        {
            var hasDuelTarget = canCommandSelectedUnit &&
                                _selectedUnit != null &&
                                _selectedUnitGrid.HasValue &&
                                CalculateDuelTargetGrids(_selectedUnitGrid.Value, _selectedUnit).Any();
            _duelButton.Visible = hasDuelTarget;
            _duelButton.Disabled = !hasDuelTarget;
            _duelButton.Text = BattleText("ui.battle.duel", "Duel");
        }

        if (_retreatButton != null)
        {
            _retreatButton.Visible = canCommandSelectedUnit && _selectedUnit != null && IsBattlePiece(_selectedUnit);
            _retreatButton.Disabled = _selectedUnit == null || !IsBattlePiece(_selectedUnit);
            _retreatButton.Text = BattleText("ui.battle.retreat", "Retreat");
        }

        if (_hideButton != null)
        {
            var isHideCandidate = canCommandSelectedUnit && _selectedUnit != null && IsBattlePiece(_selectedUnit);
            var canHide = isHideCandidate && CanHideSelectedUnit();
            _hideButton.Visible = canHide;
            _hideButton.Text = _selectedUnit?.IsHidden == true
                ? BattleText("ui.battle.hidden", "Hidden")
                : BattleText("ui.battle.hide", "Hide");
            _hideButton.Disabled = !canHide;
        }

        if (_moveButton != null)
        {
            var hasMoveTarget = canCommandSelectedUnit &&
                                _selectedUnit != null &&
                                _selectedUnitGrid.HasValue &&
                                IsBattlePiece(_selectedUnit) &&
                                !HasUsedChargeThisTurn(_selectedUnit) &&
                                CalculateReachableGrids(_selectedUnitGrid.Value, GetAvailableMoveEnergy(_selectedUnit), GetAvailableMoveRange(_selectedUnit)).Any();
            _moveButton.Visible = hasMoveTarget;
            _moveButton.Disabled = !hasMoveTarget;
            _moveButton.Text = BattleText("ui.battle.move_energy", "Move (Energy by path)");
        }

        UpdateCommandScrollLayout();
        var desiredPosition = screenPosition + new Vector2(12.0f, 12.0f);
        _commandMenu.Position = ClampCommandMenuPosition(desiredPosition);
        _commandMenu.MoveToFront();
        _commandMenu.Visible = true;
        ResizeCommandMenuAfterLayout(desiredPosition);
    }

    private void UpdateCommandScrollLayout()
    {
        if (_commandScroll == null)
        {
            return;
        }

        var visibleCommandCount = GetCommandActionButtons().Count(static button => button.Visible);
        var visibleRows = Math.Min(visibleCommandCount, 4);
        var scrollHeight = visibleRows <= 0
            ? 0.0f
            : (visibleRows * 31.0f) + ((visibleRows - 1) * 8.0f);
        _commandScroll.CustomMinimumSize = new Vector2(0.0f, scrollHeight);
        _commandScroll.ScrollVertical = 0;
    }

    private IEnumerable<Button> GetCommandActionButtons()
    {
        foreach (var button in new[]
        {
            _moveButton,
            _attackButton,
            _unionAttackButton,
            _guardButton,
            _chargeButton,
            _duelButton,
            _retreatButton,
            _hideButton,
            _dropStoneButton,
            _pourOilButton,
            _workButton,
            _installWoodFenceButton,
            _uninstallWoodFenceButton,
            _supplyButton,
            _resupplyWeaponButton,
            _captureSupplyCartButton,
            _hireOfficerButton,
            _strategyButton,
            _openGateButton
        })
        {
            if (button != null)
            {
                yield return button;
            }
        }
    }

    private void HideCommandMenu()
    {
        if (_commandMenu != null)
        {
            _commandMenu.Visible = false;
        }

        _isDraggingCommandMenu = false;

        if (_unitMenuInfoLabel != null)
        {
            _unitMenuInfoLabel.Text = BuildEmptyUnitMenuInfoText();
        }

        if (_officerPortrait != null)
        {
            _officerPortrait.Texture = null;
            _officerPortrait.Visible = false;
        }

        if (_openGateButton != null)
        {
            _openGateButton.Visible = false;
        }

        if (_attackButton != null)
        {
            _attackButton.Visible = true;
            _attackButton.Disabled = false;
            _attackButton.Text = BattleText("ui.battle.attack", "Attack");
        }

        if (_moveButton != null)
        {
            _moveButton.Visible = true;
            _moveButton.Disabled = false;
            _moveButton.Text = BattleText("ui.battle.move", "Move");
        }

        if (_dropStoneButton != null)
        {
            _dropStoneButton.Visible = false;
        }

        if (_pourOilButton != null)
        {
            _pourOilButton.Visible = false;
        }

        if (_workButton != null)
        {
            _workButton.Visible = false;
        }

        if (_installWoodFenceButton != null)
        {
            _installWoodFenceButton.Visible = false;
        }

        if (_uninstallWoodFenceButton != null)
        {
            _uninstallWoodFenceButton.Visible = false;
        }

        if (_supplyButton != null)
        {
            _supplyButton.Visible = false;
            _supplyButton.Disabled = false;
            _supplyButton.Text = BattleText("ui.battle.recovery_repair_short", "Recovery / Repair");
        }

        if (_resupplyWeaponButton != null)
        {
            _resupplyWeaponButton.Visible = false;
            _resupplyWeaponButton.Disabled = false;
            _resupplyWeaponButton.Text = BattleText("ui.battle.resupply_weapon", "Resupply Weapon");
        }

        if (_strategyButton != null)
        {
            _strategyButton.Visible = false;
            _strategyButton.Disabled = false;
            _strategyButton.Text = BattleText("ui.battle.strategy", "Strategy");
        }

        if (_unionAttackButton != null)
        {
            _unionAttackButton.Visible = false;
            _unionAttackButton.Disabled = false;
            _unionAttackButton.Text = BattleText("ui.battle.union_attack", "Union Attack");
        }

        if (_chargeButton != null)
        {
            _chargeButton.Visible = false;
            _chargeButton.Disabled = false;
            _chargeButton.Text = BattleText("ui.battle.charge", "Charge");
        }

        if (_duelButton != null)
        {
            _duelButton.Visible = false;
            _duelButton.Disabled = false;
            _duelButton.Text = BattleText("ui.battle.duel", "Duel");
        }

        if (_retreatButton != null)
        {
            _retreatButton.Visible = false;
            _retreatButton.Disabled = false;
            _retreatButton.Text = BattleText("ui.battle.retreat", "Retreat");
        }

        if (_hideButton != null)
        {
            _hideButton.Visible = false;
            _hideButton.Disabled = false;
            _hideButton.Text = BattleText("ui.battle.hide", "Hide");
        }
    }

    private void CancelCommandAction(bool clearSelection)
    {
        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _chargeTargetGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();

        if (clearSelection)
        {
            _selectedUnit = null;
            _selectedUnitGrid = null;
            _selectedGridKey = null;
        }
    }


}
