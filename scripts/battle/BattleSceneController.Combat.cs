using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattlePresentationSettings;
using static ThreeKingdom.Battle.BattleResourcePaths;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private bool TryAttackSelectedTarget()
    {
        if (!_selectedGrid.HasValue || !_selectedUnitGrid.HasValue || _selectedUnit == null)
        {
            return false;
        }

        if (!CanUseAttackCommand(_selectedUnit) || _selectedUnit.Energy < NormalAttackEnergyCost)
        {
            return false;
        }

        var targetGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        if (!_attackableGrids.Contains(targetGrid))
        {
            return false;
        }

        if (_selectedUnit.Energy < NormalAttackEnergyCost)
        {
            return false;
        }

        var attackDirection = GetInfantryDirection(_selectedUnitGrid.Value.Grid, targetGrid.Grid);
        BattleOccupantInfo? target = null;
        if (_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            target = GetAttackTargetForAttack(targetOccupants, _selectedUnit.TeamName, targetGrid);
            if (target == null)
            {
                return false;
            }
        }

        var isWeakCloseAttack = CanUseAmmoDepletedWeakAttack(_selectedUnit);
        var isHiddenAmbush = IsHiddenAmbushAttack(_selectedUnitGrid.Value, _selectedUnit);
        var attackDamage = target == null ? GetAttackDamage(_selectedUnit) : GetAttackDamageAgainst(_selectedUnit, target);
        if (isHiddenAmbush)
        {
            attackDamage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * HiddenAmbushDamageMultiplier));
        }
        var attackingUnit = _selectedUnit with
        {
            FacingDirection = attackDirection,
            Energy = _selectedUnit.Energy - NormalAttackEnergyCost,
            HasAttackedThisTurn = true,
            IsHidden = !isHiddenAmbush && _selectedUnit.IsHidden
        };
        if (!TrySpendNormalAttackWeaponAmmo(attackingUnit, out attackingUnit))
        {
            return false;
        }

        ReplaceOccupantAtGrid(_selectedUnitGrid.Value, _selectedUnit, attackingUnit);
        _selectedUnit = attackingUnit;
        if (isHiddenAmbush)
        {
            ShowBattlePopup(
                _selectedUnitGrid.Value,
                BattleText("ui.battle.ambush_popup", "Ambush +25%"),
                new Color(1.0f, 0.88f, 0.22f, 1.0f),
                new Color(0.10f, 0.08f, 0.01f, 0.95f),
                new Vector2(-48.0f, -108.0f),
                1.6,
                22);
            RefreshHiddenUnitVisibility();
        }

        var shouldTemporarilyRevealOccludedUnits =
            IsUnitOccludedByCastleVisual(_selectedUnitGrid.Value) ||
            IsUnitOccludedByCastleVisual(targetGrid);
        if (shouldTemporarilyRevealOccludedUnits)
        {
            ClearOccludedUnitSilhouettes();
        }

        var attackAnimationDuration = ApplyAttackAnimation(attackingUnit, attackDirection);
        var hurtAnimationDuration = GetTargetHurtAnimationDuration(_selectedUnitGrid.Value, targetGrid, attackingUnit);
        var projectileDuration = IsArrowProjectileAttacker(attackingUnit) && !isWeakCloseAttack
            ? ArrowProjectileEffectDurationSeconds
            : attackingUnit.Category == CategorySiegeEngine && attackingUnit.TroopType == TroopCatapult && !isWeakCloseAttack
                ? CatapultProjectileEffectDurationSeconds
                : 0.0;
        var isProjectileAttack = projectileDuration > 0.0;
        var targetHurtDelaySeconds = isProjectileAttack
            ? attackAnimationDuration + projectileDuration
            : attackAnimationDuration;
        if (isProjectileAttack)
        {
            ResolveProjectileAttackImpactAfterDelay(
                targetHurtDelaySeconds,
                _selectedUnitGrid.Value,
                targetGrid,
                attackingUnit,
                attackDamage,
                hurtAnimationDuration);
        }
        else
        {
            PlayTargetHurtAnimationAfterDelay(targetHurtDelaySeconds, _selectedUnitGrid.Value, targetGrid, attackingUnit);
        }
        if (projectileDuration > 0.0)
        {
            PlayAttackProjectileAfterDelay(attackAnimationDuration, _selectedUnitGrid.Value, targetGrid, attackingUnit);
        }

        var effectDelaySeconds = attackAnimationDuration + Math.Max(hurtAnimationDuration, projectileDuration);
        AppendBattleLog(attackingUnit, "Attack", $"{FormatLogUnit(attackingUnit)} attacks {targetGrid}{FormatNormalAttackAmmoLog(attackingUnit, isWeakCloseAttack)}{FormatTroopTypeAdvantageLog(attackingUnit, target)}{FormatAmbushAttackLog(isHiddenAmbush)}");
        if (!isProjectileAttack)
        {
            ApplyAttackDamage(attackingUnit, targetGrid, effectDelaySeconds, attackDamage, _selectedUnitGrid);
        }
        if (shouldTemporarilyRevealOccludedUnits)
        {
            RefreshOccludedUnitSilhouettesAfterDelay(effectDelaySeconds);
        }

        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        MarkUnitActed(attackingUnit);
        return true;
    }


    private void ApplyAttackDamage(
        BattleOccupantInfo attacker,
        BattleGridKey targetGrid,
        double effectDelaySeconds,
        int? damageOverride = null,
        BattleGridKey? attackerGrid = null,
        BattleOfficerSpeechEvent speechEvent = BattleOfficerSpeechEvent.Attack)
    {
        if (IsClosedGateStructureTarget(targetGrid))
        {
            ApplyStructureAttackDamage(attacker, targetGrid);
            return;
        }

        if (!_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            ApplyStructureAttackDamage(attacker, targetGrid);
            return;
        }

        var target = GetAttackTargetForAttack(targetOccupants, attacker.TeamName, targetGrid);
        if (target == null)
        {
            return;
        }

        var damage = damageOverride ?? GetAttackDamageAgainst(attacker, target);
        if (damage <= 0)
        {
            return;
        }

        var casualtyResult = ApplyUnitCasualties(targetGrid, target, damage, NormalDamageKilledRatio, isGuardReducibleAttack: IsGuardReducibleAttack(attacker));
        var updatedTarget = casualtyResult.UpdatedTarget;

        ShowDamagePopup(targetGrid, casualtyResult.ActualDamage);
        AppendBattleLog(
            target,
            "Hurt",
            $"{FormatLogUnit(target)} got {casualtyResult.ActualDamage:N0} hurt by {FormatLogUnit(attacker)} at {targetGrid} ({FormatCasualtyResult(casualtyResult)})");
        TryShowOfficerSpeech(attacker, speechEvent);
        if (updatedTarget.HitPoints * 100 <= updatedTarget.MaxHitPoints * 35)
        {
            TryShowOfficerSpeech(updatedTarget, BattleOfficerSpeechEvent.Critical);
        }
        ConfigureHud();
        RefreshInfoPanel();
        TryExecuteGuardCounterAttack(updatedTarget, targetGrid, attacker, attackerGrid);
        if (updatedTarget.HitPoints <= 0)
        {
            DestroyOccupantAfterDelay(targetGrid, updatedTarget, effectDelaySeconds, attacker);
        }
    }

    private void TryExecuteGuardCounterAttack(BattleOccupantInfo defender, BattleGridKey defenderGrid, BattleOccupantInfo attacker, BattleGridKey? attackerGrid)
    {
        if (attackerGrid == null || !defender.IsGuarding || !defender.GuardCounterAvailable || !IsMeleeAttack(attacker) || defender.HitPoints <= 0)
        {
            return;
        }

        var defenderAfterCounter = defender with { GuardCounterAvailable = false };
        ReplaceOccupantAtGrid(defenderGrid, defender, defenderAfterCounter);
        if (!_occupantsByGrid.TryGetValue(attackerGrid.Value, out var attackers))
        {
            return;
        }

        var currentAttacker = attackers.FirstOrDefault(occupant => occupant.Marker == attacker.Marker);
        if (currentAttacker == null)
        {
            return;
        }

        var counterDamage = Mathf.Max(1, Mathf.RoundToInt(GetAttackDamageAgainst(defenderAfterCounter, currentAttacker) * GuardCounterAttackRatio));
        var counterResult = ApplyUnitCasualties(attackerGrid.Value, currentAttacker, counterDamage, NormalDamageKilledRatio);
        ShowDamagePopup(attackerGrid.Value, counterResult.ActualDamage);
        AppendBattleLog(defenderAfterCounter, "Counter", $"{FormatLogUnit(defenderAfterCounter)} counterattacks {FormatLogUnit(currentAttacker)} for {counterResult.ActualDamage:N0}.{FormatTroopTypeAdvantageLog(defenderAfterCounter, currentAttacker)}");
    }

    private static bool IsMeleeAttack(BattleOccupantInfo attacker)
    {
        return attacker.TroopType is TroopInfantry or TroopSpearman or TroopCavalry or TroopWorker or TroopGuard;
    }

    private static bool IsGuardReducibleAttack(BattleOccupantInfo attacker)
    {
        return attacker.TroopType is TroopInfantry or TroopSpearman or TroopCavalry or TroopWorker or TroopGuard or TroopArcher or TroopCrossbow;
    }

    private static float GetGuardDamageReductionRatio(int reductionCount)
    {
        return GuardDamageReductionBase * Mathf.Pow(GuardDamageReductionDecay, Math.Max(0, reductionCount));
    }

    private void ApplyStructureAttackDamage(BattleOccupantInfo attacker, BattleGridKey targetGrid)
    {
        if (_mapData == null || targetGrid.Level != 0 || !IsWithinMap(targetGrid.Grid))
        {
            return;
        }

        var cell = _mapData.GetCell(targetGrid.X, targetGrid.Y);
        if (cell.HasBridgeHealth)
        {
            var bridgeDamage = GetStructureAttackDamage(attacker);
            if (bridgeDamage <= 0)
            {
                return;
            }

            var actualBridgeDamage = _mapData.ApplyBridgeDamage(targetGrid.Grid, bridgeDamage);
            if (actualBridgeDamage <= 0)
            {
                return;
            }

            if (!cell.HasBridgeHealth)
            {
                RefreshWorkerObjectLayers();
            }

            ShowDamagePopup(targetGrid, actualBridgeDamage);
            RefreshInfoPanel();
            return;
        }

        if (cell.Structure == BattleStructureType.WoodenFence && cell.HasStructureHealth)
        {
            var fenceDamage = GetStructureAttackDamage(attacker);
            if (fenceDamage <= 0)
            {
                return;
            }

            var actualFenceDamage = _mapData.ApplyWoodenFenceDamage(targetGrid.Grid, fenceDamage);
            if (actualFenceDamage <= 0)
            {
                return;
            }

            if (cell.Structure != BattleStructureType.WoodenFence)
            {
                RefreshWorkerObjectLayers();
            }

            ShowDamagePopup(targetGrid, actualFenceDamage);
            RefreshInfoPanel();
            return;
        }

        if (cell.Structure != BattleStructureType.Gate || !cell.HasStructureHealth || cell.IsBroken)
        {
            return;
        }

        var damage = GetStructureAttackDamage(attacker);
        if (damage <= 0)
        {
            return;
        }

        var actualDamage = ApplyGateGroupDamage(targetGrid.Grid, damage);
        ShowDamagePopup(targetGrid, actualDamage);
        RefreshInfoPanel();
    }

    private BattleCasualtyResult ApplyUnitCasualties(
        BattleGridKey targetGrid,
        BattleOccupantInfo target,
        int damage,
        float killedRatio,
        bool ignoresBuildingCover = false,
        bool isGuardReducibleAttack = false)
    {
        if (!ignoresBuildingCover && IsBuildingCoverActive(targetGrid))
        {
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1.0f - BuildingCoverDamageReduction)));
        }

        var guardReductionApplied = false;
        if (isGuardReducibleAttack && target.IsGuarding)
        {
            var damageBeforeGuard = damage;
            var guardReductionRatio = GetGuardDamageReductionRatio(target.GuardDamageReductionCount);
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1.0f - guardReductionRatio)));
            var reducedDamage = damageBeforeGuard - damage;
            guardReductionApplied = true;
            if (reducedDamage > 0)
            {
                AppendBattleLog(
                    target,
                    "Guard",
                    BattleFormat(
                        "ui.battle.log_guard_damage_reduction",
                        "{0} guards: damage {1:N0} -> {2:N0} (-{3:N0}).",
                        FormatLogUnit(target),
                        damageBeforeGuard,
                        damage,
                        reducedDamage,
                        guardReductionRatio * 100.0f));
            }
        }

        var actualDamage = Mathf.Min(target.HitPoints, damage);
        if (actualDamage <= 0)
        {
            return new BattleCasualtyResult(target, 0, 0, 0);
        }

        if (target.Category != CategoryUnit)
        {
            var remainingHp = Mathf.Max(0, target.HitPoints - actualDamage);
            var updatedTarget = target with
            {
                HitPoints = remainingHp,
                GuardDamageReductionCount = target.GuardDamageReductionCount + (guardReductionApplied ? 1 : 0)
            };
            UpdateMarkerStrengthBar(updatedTarget);
            ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
            if (_selectedUnit == target)
            {
                _selectedUnit = updatedTarget;
            }

            return new BattleCasualtyResult(updatedTarget, actualDamage, actualDamage, 0);
        }

        var activeLoss = Mathf.Min(target.TroopCount, actualDamage);
        var killedTroops = Mathf.Clamp(Mathf.RoundToInt(activeLoss * killedRatio), 0, activeLoss);
        var woundedTroops = activeLoss - killedTroops;
        var remainingTroops = Mathf.Max(0, target.TroopCount - activeLoss);
        ApplyTeamTroopLoss(target, activeLoss);
        var updatedUnit = target with
        {
            TroopCount = remainingTroops,
            HitPoints = remainingTroops,
            WoundedTroops = target.WoundedTroops + woundedTroops,
            GuardDamageReductionCount = target.GuardDamageReductionCount + (guardReductionApplied ? 1 : 0)
        };
        UpdateMarkerStrengthBar(updatedUnit);
        ReplaceOccupantAtGrid(targetGrid, target, updatedUnit);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedUnit;
        }

        return new BattleCasualtyResult(updatedUnit, activeLoss, killedTroops, woundedTroops);
    }

    private static string FormatCasualtyResult(BattleCasualtyResult result)
    {
        if (result.WoundedTroops <= 0)
        {
            return $"killed {result.KilledTroops:N0}";
        }

        return $"killed {result.KilledTroops:N0}, wounded {result.WoundedTroops:N0}";
    }

    private bool IsClosedGateStructureTarget(BattleGridKey targetGrid)
    {
        if (_mapData == null || targetGrid.Level != 0 || !IsWithinMap(targetGrid.Grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(targetGrid.X, targetGrid.Y);
        return cell.Structure == BattleStructureType.Gate &&
               cell.HasStructureHealth &&
               !cell.IsGateOpen &&
               !cell.IsBroken;
    }

    private int ApplyGateGroupDamage(Vector2I gateGrid, int damage)
    {
        if (_mapData == null)
        {
            return 0;
        }

        var gateGroup = GetConnectedGateGroup(gateGrid);
        if (gateGroup.Count == 0)
        {
            return 0;
        }

        var groupHealth = gateGroup
            .Select(grid => _mapData.GetCell(grid.X, grid.Y).StructureHealth)
            .Min();
        var actualDamage = Mathf.Min(groupHealth, damage);
        var remainingHealth = Mathf.Max(0, groupHealth - damage);
        foreach (var groupGateGrid in gateGroup)
        {
            var gateCell = _mapData.GetCell(groupGateGrid.X, groupGateGrid.Y);
            gateCell.StructureHealth = remainingHealth;
        }

        if (remainingHealth <= 0)
        {
            OpenGateGroup(gateGroup);
            return actualDamage;
        }

        foreach (var groupGateGrid in gateGroup)
        {
            RefreshCastleDepthVisual(groupGateGrid);
        }

        return actualDamage;
    }


    private static BattleOccupantInfo? GetAttackTarget(IEnumerable<BattleOccupantInfo> occupants)
    {
        return occupants.FirstOrDefault(static occupant => occupant.Marker != null && IsBattlePiece(occupant));
    }

    private static BattleOccupantInfo? GetFireDamageTarget(IEnumerable<BattleOccupantInfo> occupants)
    {
        // Fire is a tile effect: hidden units on the burning tile are still hurt.
        return occupants.FirstOrDefault(static occupant => occupant.Marker != null && IsBattlePiece(occupant));
    }

    private BattleOccupantInfo? GetAttackTarget(IEnumerable<BattleOccupantInfo> occupants, string attackerTeamName)
    {
        return occupants.FirstOrDefault(occupant =>
            occupant.Marker != null &&
            IsBattlePiece(occupant) &&
            occupant.TeamName != attackerTeamName &&
            !IsHiddenFromSide(occupant, attackerTeamName));
    }

    private BattleOccupantInfo? GetAttackTargetForAttack(IEnumerable<BattleOccupantInfo> occupants, string attackerTeamName, BattleGridKey targetGrid)
    {
        var canHitHiddenInForest = IsForestGrid(targetGrid);
        return occupants.FirstOrDefault(occupant =>
            occupant.Marker != null &&
            IsBattlePiece(occupant) &&
            occupant.TeamName != attackerTeamName &&
            (canHitHiddenInForest || !IsHiddenFromSide(occupant, attackerTeamName)));
    }

    private BattleOccupantInfo? GetAiAttackTargetForAttack(IEnumerable<BattleOccupantInfo> occupants, string attackerTeamName, BattleGridKey targetGrid)
    {
        var target = GetAttackTargetForAttack(occupants, attackerTeamName, targetGrid);
        return target != null && !IsHiddenFromSide(target, attackerTeamName) ? target : null;
    }

    private int GetAttackDamage(BattleOccupantInfo attacker)
    {
        var damage = CanUseAmmoDepletedWeakAttack(attacker)
            ? Mathf.Max(1, Mathf.RoundToInt(GetBaseAttackDamage(attacker) * AmmoDepletedWeakAttackDamageRatio))
            : GetBaseAttackDamage(attacker);
        return IsSustainedZeroFood(attacker.TeamName)
            ? Mathf.Max(1, Mathf.RoundToInt(damage * SustainedZeroFoodAttackDamageRatio))
            : damage;
    }

    private bool IsSustainedZeroFood(string teamName)
    {
        return GetTeamFood(teamName) <= 0 && GetTeamZeroFoodDays(teamName) >= 3;
    }

    private int GetAttackDamageAgainst(BattleOccupantInfo attacker, BattleOccupantInfo target)
    {
        var damage = GetAttackDamage(attacker);
        return HasTroopTypeAdvantage(attacker, target)
            ? Mathf.Max(1, Mathf.RoundToInt(damage * TroopTypeAdvantageDamageMultiplier))
            : damage;
    }

    private int GetChargeDamage(BattleOccupantInfo cavalry, BattleOccupantInfo target)
    {
        var damage = target.TroopType == TroopSpearman ? CavalryChargeVsSpearmanDamage : CavalryChargeDamage;
        return IsSustainedZeroFood(cavalry.TeamName)
            ? Mathf.Max(1, Mathf.RoundToInt(damage * SustainedZeroFoodAttackDamageRatio))
            : damage;
    }

    private static bool HasTroopTypeAdvantage(BattleOccupantInfo attacker, BattleOccupantInfo target)
    {
        return (attacker.TroopType, target.TroopType) switch
        {
            (TroopInfantry, TroopSpearman) => true,
            (TroopSpearman, TroopCavalry) => true,
            (TroopCavalry, TroopArcher or TroopCrossbow) => true,
            (TroopArcher or TroopCrossbow, TroopInfantry) => true,
            _ => false
        };
    }

    private static string FormatTroopTypeAdvantageLog(BattleOccupantInfo attacker, BattleOccupantInfo? target)
    {
        return target != null && HasTroopTypeAdvantage(attacker, target)
            ? $"; type advantage {attacker.TroopType} -> {target.TroopType}: +25%"
            : string.Empty;
    }

    private static int GetBaseAttackDamage(BattleOccupantInfo attacker)
    {
        if (attacker.Category == CategorySiegeEngine)
        {
            return attacker.TroopType switch
            {
                TroopRam => RamAttackDamage,
                TroopCatapult => CatapultAttackDamage,
                _ => 0
            };
        }

        return attacker.TroopType switch
        {
            TroopInfantry => InfantryAttackDamage,
            TroopSpearman => SpearmanAttackDamage,
            TroopArcher or TroopCrossbow => ArcherAttackDamage,
            TroopCavalry => CavalryAttackDamage,
            TroopWorker => WorkerAttackDamage,
            _ => 0
        };
    }

    private static int GetStructureAttackDamage(BattleOccupantInfo attacker)
    {
        if (attacker.Category == CategorySiegeEngine)
        {
            return attacker.TroopType switch
            {
                TroopRam => RamStructureDamage,
                TroopCatapult => CatapultStructureDamage,
                _ => 0
            };
        }

        return attacker.TroopType switch
        {
            TroopInfantry => InfantryStructureDamage,
            TroopSpearman => SpearmanStructureDamage,
            TroopArcher or TroopCrossbow => ArcherStructureDamage,
            TroopCavalry => CavalryStructureDamage,
            TroopWorker => WorkerStructureDamage,
            _ => 0
        };
    }


    private void OnAttackButtonPressed()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            _selectedUnit.Energy < NormalAttackEnergyCost ||
            !CanUseAttackCommand(_selectedUnit))
        {
            return;
        }

        _commandMode = BattleCommandMode.AttackSelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _duelTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        foreach (var grid in CalculateAttackableGrids(_selectedUnitGrid.Value, _selectedUnit))
        {
            _attackableGrids.Add(grid);
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private void OnChargeButtonPressed()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !CanUseChargeCommand(_selectedUnit))
        {
            return;
        }

        _commandMode = BattleCommandMode.ChargeSelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _chargeTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        foreach (var grid in CalculateChargeTargetGrids(_selectedUnitGrid.Value, _selectedUnit))
        {
            _chargeTargetGrids.Add(grid);
        }

        if (_chargeTargetGrids.Count == 0)
        {
            _commandMode = BattleCommandMode.AwaitingCommand;
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private IEnumerable<BattleGridKey> CalculateChargeTargetGrids(BattleGridKey cavalryGrid, BattleOccupantInfo cavalry)
    {
        if (!CanUseChargeCommand(cavalry) || cavalryGrid.Level != 0)
        {
            yield break;
        }

        foreach (var targetGrid in GetOrthogonalNeighborGridKeys(cavalryGrid))
        {
            if (!TryGetChargeDestinationGrid(cavalryGrid, targetGrid, out _) ||
                !_occupantsByGrid.TryGetValue(targetGrid, out var occupants))
            {
                continue;
            }

            var target = GetAttackTarget(occupants, cavalry.TeamName);
            if (target != null && IsAttackerPiece(target) != IsAttackerPiece(cavalry))
            {
                yield return targetGrid;
            }
        }
    }

    private bool TryExecuteSelectedCharge()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !_selectedGrid.HasValue ||
            !CanUseChargeCommand(_selectedUnit))
        {
            return false;
        }

        var sourceGrid = _selectedUnitGrid.Value;
        var targetGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        if (!_chargeTargetGrids.Contains(targetGrid) ||
            !TryGetChargeDestinationGrid(sourceGrid, targetGrid, out var destinationGrid) ||
            !_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            return false;
        }

        var target = GetAttackTarget(targetOccupants, _selectedUnit.TeamName);
        if (target == null)
        {
            return false;
        }

        ExecuteCharge(sourceGrid, _selectedUnit, targetGrid, target, destinationGrid);
        MarkUnitActed(_selectedUnit);
        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        _chargeTargetGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        ConfigureHud();
        RefreshHiddenUnitVisibility();
        RefreshInfoPanel();
        RefreshBattleResultState();
        RefreshHighlights();
        RefreshBattleLogPanel();
        return true;
    }

    private void ExecuteCharge(BattleGridKey sourceGrid, BattleOccupantInfo cavalry, BattleGridKey targetGrid, BattleOccupantInfo target, BattleGridKey destinationGrid)
    {
        var direction = GetInfantryDirection(sourceGrid.Grid, targetGrid.Grid);
        var damage = GetChargeDamage(cavalry, target);
        var targetHurtDuration = ApplyTargetHurtAnimation(sourceGrid, targetGrid, cavalry);
        var casualtyResult = ApplyUnitCasualties(targetGrid, target, damage, NormalDamageKilledRatio);
        ShowDamagePopup(targetGrid, casualtyResult.ActualDamage);
        AppendBattleLog(
            cavalry,
            "Charge",
            $"{FormatLogUnit(cavalry)} charges through {FormatLogUnit(target)} {sourceGrid} -> {destinationGrid}: {casualtyResult.ActualDamage:N0} damage ({FormatCasualtyResult(casualtyResult)})");
        TryShowOfficerSpeech(cavalry, BattleOfficerSpeechEvent.Charge);
        if (casualtyResult.UpdatedTarget.HitPoints * 100 <= casualtyResult.UpdatedTarget.MaxHitPoints * 35)
        {
            TryShowOfficerSpeech(casualtyResult.UpdatedTarget, BattleOfficerSpeechEvent.Critical);
        }
        if (casualtyResult.UpdatedTarget.HitPoints <= 0)
        {
            DestroyOccupantAfterDelay(targetGrid, casualtyResult.UpdatedTarget, targetHurtDuration, cavalry);
        }

        var movedCavalry = MoveChargingCavalry(sourceGrid, cavalry, targetGrid, destinationGrid, direction);
        _selectedUnit = movedCavalry;
        _selectedUnitGrid = destinationGrid;
        _selectedGrid = destinationGrid.Grid;
        _selectedGridKey = destinationGrid;
        if (movedCavalry.Marker != null)
        {
            _chargeUsedByMarkerThisTurn.Add(movedCavalry.Marker);
        }

        var moveDuration = GetScaledMoveDuration(
            CavalryMoveAnimationDurationSeconds,
            new[] { GetMarkerPosition(targetGrid), GetMarkerPosition(destinationGrid) });
        if (target.TroopType == TroopSpearman)
        {
            var counterResult = ApplyUnitCasualties(destinationGrid, movedCavalry, CavalryChargeSpearmanCounterDamage, NormalDamageKilledRatio);
            ShowDamagePopup(destinationGrid, counterResult.ActualDamage);
            AppendBattleLog(
                target,
                "Counter",
                $"{FormatLogUnit(target)} counters {FormatLogUnit(movedCavalry)} charge: {counterResult.ActualDamage:N0} damage ({FormatCasualtyResult(counterResult)})");
            if (counterResult.UpdatedTarget.HitPoints <= 0)
            {
                DestroyOccupantAfterDelay(destinationGrid, counterResult.UpdatedTarget, moveDuration, target);
            }
        }

        ConfigureHud();
    }

    private BattleOccupantInfo MoveChargingCavalry(
        BattleGridKey sourceGrid,
        BattleOccupantInfo cavalry,
        BattleGridKey targetGrid,
        BattleGridKey destinationGrid,
        BattleSpriteDirection direction)
    {
        if (!_occupantsByGrid.TryGetValue(sourceGrid, out var sourceOccupants) ||
            !sourceOccupants.Remove(cavalry))
        {
            return cavalry;
        }

        if (sourceOccupants.Count == 0)
        {
            _occupantsByGrid.Remove(sourceGrid);
        }

        if (!_occupantsByGrid.TryGetValue(destinationGrid, out var destinationOccupants))
        {
            destinationOccupants = new List<BattleOccupantInfo>();
            _occupantsByGrid[destinationGrid] = destinationOccupants;
        }

        var remainsHidden = cavalry.IsHidden && IsForestGrid(destinationGrid);
        var movedCavalry = cavalry with
        {
            FacingDirection = direction,
            IsHidden = remainsHidden,
            Energy = Math.Max(0, cavalry.Energy - NormalAttackEnergyCost),
            HasAttackedThisTurn = true,
            RemainingMoveRange = 0
        };
        destinationOccupants.Add(movedCavalry);
        UpdateMarkerStatusIndicator(movedCavalry);
        RegisterBattleDepthEntry(
            movedCavalry.Marker!,
            destinationGrid,
            movedCavalry.Category == CategorySiegeEngine ? BattleDepthRenderKind.SiegeEngine : BattleDepthRenderKind.Unit);

        var movePath = new[] { targetGrid, destinationGrid };
        var pathPositions = movePath.Select(GetMarkerPosition).ToArray();
        var pathDirections = new[] { direction, direction };
        var pathModulates = BuildMovePathModulates(sourceGrid, movePath, movedCavalry);
        ApplyMoveAnimation(
            movedCavalry,
            direction,
            GetMarkerPosition(destinationGrid),
            pathPositions,
            pathDirections,
            pathModulates,
            RefreshOccludedUnitSilhouettes);
        return movedCavalry;
    }

    private IEnumerable<BattleGridKey> GetOrthogonalNeighborGridKeys(BattleGridKey sourceGrid)
    {
        foreach (var neighbor in GetOrthogonalNeighbors(sourceGrid.Grid))
        {
            yield return new BattleGridKey(neighbor.X, neighbor.Y, sourceGrid.Level);
        }
    }

    private bool TryGetChargeDestinationGrid(BattleGridKey sourceGrid, BattleGridKey targetGrid, out BattleGridKey destinationGrid)
    {
        destinationGrid = default;
        if (_mapData == null ||
            _selectedUnit == null ||
            sourceGrid.Level != 0 ||
            targetGrid.Level != 0)
        {
            return false;
        }

        var delta = targetGrid.Grid - sourceGrid.Grid;
        if (Mathf.Abs(delta.X) + Mathf.Abs(delta.Y) != 1)
        {
            return false;
        }

        var destination = targetGrid.Grid + delta;
        if (!IsWithinMap(targetGrid.Grid) || !IsWithinMap(destination))
        {
            return false;
        }

        destinationGrid = new BattleGridKey(destination.X, destination.Y, 0);
        if (HasBlockingOccupant(destinationGrid))
        {
            return false;
        }

        var sourceCell = _mapData.GetCell(sourceGrid.X, sourceGrid.Y);
        var targetCell = _mapData.GetCell(targetGrid.X, targetGrid.Y);
        var destinationCell = _mapData.GetCell(destinationGrid.X, destinationGrid.Y);
        if (sourceCell.Terrain is BattleTerrainType.Forest or BattleTerrainType.Farm ||
            targetCell.Terrain is BattleTerrainType.Forest or BattleTerrainType.Farm ||
            destinationCell.Terrain is BattleTerrainType.Forest or BattleTerrainType.Farm ||
            targetCell.ProvidesBuildingCover ||
            destinationCell.ProvidesBuildingCover)
        {
            return false;
        }

        if (!CanEnterCell(sourceGrid, targetGrid, targetCell) ||
            !CanEnterCell(targetGrid, destinationGrid, destinationCell))
        {
            return false;
        }

        if (IsCellBlockingMovement(destinationCell) &&
            !CanTraverseBlockedCell(destinationGrid, destinationCell))
        {
            return false;
        }

        return true;
    }

    private void OnDuelButtonPressed()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue)
        {
            return;
        }

        _commandMode = BattleCommandMode.DuelSelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        foreach (var grid in CalculateDuelTargetGrids(_selectedUnitGrid.Value, _selectedUnit))
        {
            _duelTargetGrids.Add(grid);
        }

        if (_duelTargetGrids.Count == 0)
        {
            _commandMode = BattleCommandMode.AwaitingCommand;
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private IEnumerable<BattleGridKey> CalculateDuelTargetGrids(BattleGridKey challengerGrid, BattleOccupantInfo challenger)
    {
        if (!CanStartDuel(challenger))
        {
            yield break;
        }

        foreach (var targetGrid in GetDuelCandidateGridKeys(challengerGrid))
        {
            if (!IsWithinMap(targetGrid.Grid) ||
                !_occupantsByGrid.TryGetValue(targetGrid, out var occupants))
            {
                continue;
            }

            var opponent = GetAttackTarget(occupants, challenger.TeamName);
            if (opponent != null &&
                CanStartDuel(opponent) &&
                IsAttackerPiece(opponent) != IsAttackerPiece(challenger))
            {
                yield return targetGrid;
            }
        }
    }

    private IEnumerable<BattleGridKey> GetDuelCandidateGridKeys(BattleGridKey challengerGrid)
    {
        for (var offsetY = -2; offsetY <= 2; offsetY++)
        {
            for (var offsetX = -2; offsetX <= 2; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var range = Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY));
                if (range > 2)
                {
                    continue;
                }

                var grid = new Vector2I(challengerGrid.X + offsetX, challengerGrid.Y + offsetY);
                if (!IsWithinMap(grid))
                {
                    continue;
                }

                yield return new BattleGridKey(grid.X, grid.Y, 0);
                yield return new BattleGridKey(grid.X, grid.Y, 2);
            }
        }
    }

    private bool TryExecuteSelectedDuel()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || !_selectedGrid.HasValue)
        {
            return false;
        }

        var targetGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        if (!_duelTargetGrids.Contains(targetGrid) ||
            !_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            return false;
        }

        var opponent = GetAttackTarget(targetOccupants, _selectedUnit.TeamName);
        if (opponent == null || !CanStartDuel(_selectedUnit) || !CanStartDuel(opponent))
        {
            return false;
        }

        ExecuteDuel(_selectedUnitGrid.Value, _selectedUnit, targetGrid, opponent);
        MarkUnitActed(_selectedUnit);
        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        HideCommandMenu();
        ConfigureHud();
        RefreshInfoPanel();
        RefreshHighlights();
        return true;
    }

    private void ExecuteDuel(BattleGridKey challengerGrid, BattleOccupantInfo challenger, BattleGridKey opponentGrid, BattleOccupantInfo opponent)
    {
        var challengerScore = GetDuelBattleScore(challenger);
        var opponentScore = GetDuelBattleScore(opponent);
        if (!DoesOpponentAcceptDuel(challengerScore, opponentScore))
        {
            UpdateUnitMorale(challengerGrid, challenger, 2, out _);
            UpdateUnitMorale(opponentGrid, opponent, -5, out _);
            AppendBattleLog(
                challenger,
                "Duel",
                $"{FormatLogUnit(challenger)} challenges {FormatLogUnit(opponent)}, but opponent refuses. Morale {FormatLogUnit(challenger)} +2, {FormatLogUnit(opponent)} -5");
            return;
        }

        AppendBattleLog(challenger, "Duel", $"{FormatLogUnit(challenger)} duels {FormatLogUnit(opponent)} ({challengerScore} vs {opponentScore})");
        var scoreDelta = challengerScore - opponentScore;
        if (Mathf.Abs(scoreDelta) <= 5)
        {
            UpdateUnitMorale(challengerGrid, challenger, 3, out _);
            UpdateUnitMorale(opponentGrid, opponent, 3, out _);
            AppendBattleLog(challenger, "Duel", $"Draw: {FormatLogUnit(challenger)} and {FormatLogUnit(opponent)} both keep battle team. Morale +3");
            return;
        }

        var winnerGrid = scoreDelta > 0 ? challengerGrid : opponentGrid;
        var winner = scoreDelta > 0 ? challenger : opponent;
        var loserGrid = scoreDelta > 0 ? opponentGrid : challengerGrid;
        var loser = scoreDelta > 0 ? opponent : challenger;
        UpdateUnitMorale(winnerGrid, winner, 10, out _);
        AppendBattleLog(winner, "Duel", $"{FormatLogUnit(winner)} wins. {FormatLogUnit(loser)} captured; losing team leaves battle. Winner morale +10");
        ApplyRetreatTroopLoss(loser);
        RemoveOccupant(loserGrid, loser);
        ShowOfficerCaptureNotice(loser);
        if (_selectedUnit == loser)
        {
            _selectedUnit = null;
            _selectedUnitGrid = null;
            _selectedGrid = null;
            _selectedGridKey = null;
        }
    }

    private bool UpdateUnitMorale(BattleGridKey grid, BattleOccupantInfo unit, int delta, out BattleOccupantInfo updatedUnit)
    {
        updatedUnit = unit;
        if (unit.Morale == null)
        {
            return false;
        }

        var updatedMorale = Mathf.Clamp(unit.Morale.Value + delta, 0, 120);
        var actualDelta = updatedMorale - unit.Morale.Value;
        updatedUnit = unit with { Morale = updatedMorale };
        ReplaceOccupantAtGrid(grid, unit, updatedUnit);
        if (_selectedUnit == unit)
        {
            _selectedUnit = updatedUnit;
        }

        ShowMoralePopup(grid, actualDelta);
        return true;
    }


    private void OnUnionAttackButtonPressed()
    {
        if (!TryGetBestUnionAttackCandidate(out var candidate) ||
            _selectedUnit == null ||
            !_selectedUnitGrid.HasValue)
        {
            return;
        }

        var shouldTemporarilyRevealOccludedUnits =
            IsUnitOccludedByCastleVisual(candidate.TargetGrid) ||
            candidate.Participants.Any(participant => IsUnitOccludedByCastleVisual(participant.Grid));
        if (shouldTemporarilyRevealOccludedUnits)
        {
            ClearOccludedUnitSilhouettes();
        }

        var maxAttackAnimationDuration = 0.0;
        BattleOccupantInfo? updatedSelectedUnit = null;
        var updatedParticipants = new List<UnionAttackParticipant>();
        var isHiddenAmbush = IsHiddenAmbushAttack(_selectedUnitGrid.Value, _selectedUnit);
        foreach (var participant in candidate.Participants)
        {
            var attackDirection = GetInfantryDirection(participant.Grid.Grid, candidate.TargetGrid.Grid);
            var isInitiator = participant.Grid == _selectedUnitGrid.Value;
            var isInitiatorAmbush = isInitiator && isHiddenAmbush;
            var energyCost = isInitiator ? NormalAttackEnergyCost : UnionAttackSupportEnergyCost;
            var attackingUnit = participant.Occupant with
            {
                FacingDirection = attackDirection,
                Energy = participant.Occupant.Energy - energyCost,
                HasAttackedThisTurn = isInitiator,
                IsHidden = !isInitiatorAmbush && participant.Occupant.IsHidden
            };
            if (!TrySpendNormalAttackWeaponAmmo(attackingUnit, out attackingUnit))
            {
                continue;
            }

            ReplaceOccupantAtGrid(participant.Grid, participant.Occupant, attackingUnit);
            updatedParticipants.Add(new UnionAttackParticipant(participant.Grid, attackingUnit));
            maxAttackAnimationDuration = Math.Max(maxAttackAnimationDuration, ApplyAttackAnimation(attackingUnit, attackDirection));
            if (participant.Grid == _selectedUnitGrid.Value)
            {
                updatedSelectedUnit = attackingUnit;
            }
        }

        if (updatedParticipants.Count < 2)
        {
            return;
        }

        if (updatedSelectedUnit != null)
        {
            _selectedUnit = updatedSelectedUnit;
        }
        if (isHiddenAmbush)
        {
            ShowBattlePopup(
                _selectedUnitGrid.Value,
                BattleText("ui.battle.ambush_popup", "Ambush +25%"),
                new Color(1.0f, 0.88f, 0.22f, 1.0f),
                new Color(0.10f, 0.08f, 0.01f, 0.95f),
                new Vector2(-48.0f, -108.0f),
                1.6,
                22);
            RefreshHiddenUnitVisibility();
        }

        var hurtAnimationDuration = GetTargetHurtAnimationDuration(_selectedUnitGrid.Value, candidate.TargetGrid, _selectedUnit!);
        PlayTargetHurtAnimationAfterDelay(maxAttackAnimationDuration, _selectedUnitGrid.Value, candidate.TargetGrid, _selectedUnit!);
        var effectDelaySeconds = maxAttackAnimationDuration + hurtAnimationDuration;
        var unionTarget = GetAttackTargetForAttack(_occupantsByGrid[candidate.TargetGrid], _selectedUnit!.TeamName, candidate.TargetGrid)!;
        var advantageParticipants = updatedParticipants
            .Where(participant => HasTroopTypeAdvantage(participant.Occupant, unionTarget))
            .Select(participant => participant.Occupant.TroopType)
            .Distinct()
            .ToList();
        var unionAdvantageLog = advantageParticipants.Count == 0
            ? string.Empty
            : $"; type advantage {string.Join("/", advantageParticipants)} -> {unionTarget.TroopType}: +25%";
        AppendBattleLog(
            _selectedUnit!,
            "Attack",
            $"Union x{updatedParticipants.Count}: {string.Join(", ", updatedParticipants.Select(participant => FormatLogUnit(participant.Occupant)))} -> {candidate.TargetGrid}{unionAdvantageLog}{FormatAmbushAttackLog(isHiddenAmbush)}");
        ApplyAttackDamage(
            _selectedUnit!,
            candidate.TargetGrid,
            effectDelaySeconds,
            GetUnionAttackDamage(updatedParticipants, unionTarget, isHiddenAmbush),
            _selectedUnitGrid,
            BattleOfficerSpeechEvent.Union);
        foreach (var participant in updatedParticipants)
        {
            if (participant.Grid == _selectedUnitGrid.Value)
            {
                MarkUnitActed(participant.Occupant);
            }
        }
        if (shouldTemporarilyRevealOccludedUnits)
        {
            RefreshOccludedUnitSilhouettesAfterDelay(effectDelaySeconds);
        }

        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _workerWorkAction = WorkerWorkAction.General;
        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }


    private bool TryGetBestUnionAttackCandidate(out UnionAttackCandidate candidate)
    {
        candidate = new UnionAttackCandidate(default, new List<UnionAttackParticipant>());
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !CanInitiateUnionAttack(_selectedUnit))
        {
            return false;
        }

        var selectedGrid = _selectedUnitGrid.Value;
        var selectedIsAttacker = IsAttackerPiece(_selectedUnit);
        var candidates = new List<UnionAttackCandidate>();
        foreach (var targetEntry in _occupantsByGrid)
        {
            var targetGrid = targetEntry.Key;
            if (targetGrid.Level != selectedGrid.Level || !IsTouchingGrid(selectedGrid, targetGrid))
            {
                continue;
            }

            var target = GetAttackTarget(targetEntry.Value, _selectedUnit.TeamName);
            if (target == null ||
                target.Marker == null ||
                IsAttackerPiece(target) == selectedIsAttacker)
            {
                continue;
            }

            var participants = CollectUnionAttackParticipants(targetGrid, selectedGrid, selectedIsAttacker);
            if (participants.Count >= 2)
            {
                candidates.Add(new UnionAttackCandidate(targetGrid, participants));
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        candidate = candidates
            .OrderByDescending(current => current.Participants.Count)
            .ThenBy(current => GetManhattanDistance(selectedGrid.Grid, current.TargetGrid.Grid))
            .First();
        return true;
    }

    private List<UnionAttackParticipant> CollectUnionAttackParticipants(
        BattleGridKey targetGrid,
        BattleGridKey selectedGrid,
        bool selectedIsAttacker)
    {
        var participants = new List<UnionAttackParticipant>();
        if (_selectedUnit != null)
        {
            participants.Add(new UnionAttackParticipant(selectedGrid, _selectedUnit));
        }

        var supportParticipants = new List<UnionAttackParticipant>();
        foreach (var sourceEntry in _occupantsByGrid)
        {
            var sourceGrid = sourceEntry.Key;
            if (sourceGrid == selectedGrid ||
                sourceGrid.Level != targetGrid.Level ||
                !IsTouchingGrid(sourceGrid, targetGrid))
            {
                continue;
            }

            var supportUnit = sourceEntry.Value.FirstOrDefault(occupant =>
                CanJoinUnionAttack(occupant) &&
                IsAttackerPiece(occupant) == selectedIsAttacker);
            if (supportUnit != null)
            {
                supportParticipants.Add(new UnionAttackParticipant(sourceGrid, supportUnit));
            }
        }

        participants.AddRange(
            supportParticipants
                .OrderBy(participant => GetManhattanDistance(participant.Grid.Grid, selectedGrid.Grid))
                .Take(3));
        return participants;
    }

    private bool CanInitiateUnionAttack(BattleOccupantInfo occupant)
    {
        return CanParticipateInUnionAttack(occupant, NormalAttackEnergyCost) &&
               IsCurrentTurnPiece(occupant);
    }

    private bool CanJoinUnionAttack(BattleOccupantInfo occupant)
    {
        return CanParticipateInUnionAttack(occupant, UnionAttackSupportEnergyCost) &&
               IsCurrentTurnPiece(occupant);
    }

    private bool CanParticipateInUnionAttack(BattleOccupantInfo occupant, int energyCost)
    {
        return occupant.Marker != null &&
               occupant.Category == CategoryUnit &&
               !IsMessed(occupant) &&
               !occupant.HasAttackedThisTurn &&
               !HasUsedChargeThisTurn(occupant) &&
               occupant.Energy >= energyCost &&
               IsUnionAttackTroopType(occupant.TroopType) &&
               GetAttackDamage(occupant) > 0 &&
               CanUseNormalAttackWithCurrentAmmo(occupant);
    }

    private bool CanUseAttackCommand(BattleOccupantInfo occupant)
    {
        return occupant.TroopType != TroopSupplyCart &&
               IsBattlePiece(occupant) &&
               !IsMessed(occupant) &&
               !occupant.HasAttackedThisTurn &&
               !HasUsedChargeThisTurn(occupant) &&
               GetEffectiveAttackRange(occupant) > 0 &&
               GetAttackDamage(occupant) > 0 &&
               CanUseNormalAttackWithCurrentAmmo(occupant);
    }

    private bool CanUseGuard(BattleOccupantInfo occupant)
    {
        return IsCurrentTurnPiece(occupant) &&
               occupant.Category == CategoryUnit &&
               occupant.TroopType != TroopWorker &&
               occupant.Energy >= NormalAttackEnergyCost;
    }

    private bool CanUseChargeCommand(BattleOccupantInfo occupant)
    {
        return occupant.Marker != null &&
               occupant.Category == CategoryUnit &&
               occupant.TroopType == TroopCavalry &&
               !IsMessed(occupant) &&
               !HasUsedChargeThisTurn(occupant) &&
               occupant.Energy >= NormalAttackEnergyCost &&
               IsCurrentTurnPiece(occupant);
    }

    private bool HasUsedChargeThisTurn(BattleOccupantInfo occupant)
    {
        return occupant.Marker != null && _chargeUsedByMarkerThisTurn.Contains(occupant.Marker);
    }

    private static bool IsUnionAttackTroopType(string troopType)
    {
        return troopType is TroopInfantry or TroopSpearman or TroopCavalry or TroopArcher or TroopCrossbow or TroopWorker;
    }

    private int GetUnionAttackDamage(IReadOnlyList<UnionAttackParticipant> participants, BattleOccupantInfo target, bool ambushInitiator = false)
    {
        var damage = 0;
        for (var index = 0; index < participants.Count; index++)
        {
            var participantDamage = GetAttackDamageAgainst(participants[index].Occupant, target);
            damage += index == 0
                ? ambushInitiator
                    ? Mathf.Max(1, Mathf.RoundToInt(participantDamage * HiddenAmbushDamageMultiplier))
                    : participantDamage
                : Mathf.Max(1, participantDamage / 2);
        }

        return damage;
    }

    private static bool IsTouchingGrid(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (sourceGrid.Level != targetGrid.Level)
        {
            return false;
        }

        return GetManhattanDistance(sourceGrid.Grid, targetGrid.Grid) == 1;
    }

    private static int GetManhattanDistance(Vector2I a, Vector2I b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }

    private static int GetChebyshevDistance(Vector2I a, Vector2I b)
    {
        return Math.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
    }


    private IEnumerable<BattleGridKey> CalculateAttackableGrids(BattleGridKey startGrid, BattleOccupantInfo attacker, bool allowHillRangeBonus = true)
    {
        if (!CanUseNormalAttackWithCurrentAmmo(attacker))
        {
            yield break;
        }

        var attackRange = GetEffectiveAttackRange(attacker, startGrid, allowHillRangeBonus);
        if (attackRange <= 0)
        {
            yield break;
        }

        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var grid = new Vector2I(x, y);
                foreach (var gridKey in GetAttackCandidateGridKeys(startGrid, attacker, grid))
                {
                    var distance = Mathf.Abs(gridKey.X - startGrid.X) + Mathf.Abs(gridKey.Y - startGrid.Y);
                    if (distance > 0 &&
                        distance <= attackRange &&
                        IsAttackLevelCompatible(startGrid, attacker, gridKey) &&
                        !IsClosedGateExteriorAttackBlocked(startGrid, gridKey))
                    {
                        yield return gridKey;
                    }
                }
            }
        }
    }

    private int GetEffectiveAttackRange(BattleOccupantInfo attacker, BattleGridKey? sourceGrid = null, bool allowHillRangeBonus = true)
    {
        if (CanUseAmmoDepletedWeakAttack(attacker))
        {
            return 1;
        }

        var attackRange = attacker.AttackRange;
        if (GetCurrentBattleTimeOfDay() == BattleTimeOfDay.Night && IsRangedBattleAttacker(attacker))
        {
            attackRange = Mathf.Max(1, attackRange - 1);
        }

        if (allowHillRangeBonus &&
            sourceGrid.HasValue &&
            _mapData != null &&
            sourceGrid.Value.Level == 0 &&
            attacker.Category == CategoryUnit &&
            attacker.TroopType is TroopArcher or TroopCrossbow &&
            _mapData.GetCell(sourceGrid.Value.X, sourceGrid.Value.Y).Terrain == BattleTerrainType.Hill)
        {
            attackRange++;
        }

        return attackRange;
    }

    private static bool IsRangedBattleAttacker(BattleOccupantInfo attacker)
    {
        if (attacker.Category == CategoryUnit)
        {
            return attacker.TroopType is TroopArcher or TroopCrossbow;
        }

        return attacker.Category == CategorySiegeEngine && attacker.TroopType == TroopCatapult;
    }

    private bool IsClosedGateExteriorAttackBlocked(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (_mapData == null || sourceGrid.Level != 0 || !IsWithinMap(sourceGrid.Grid))
        {
            return false;
        }

        var sourceCell = _mapData.GetCell(sourceGrid.X, sourceGrid.Y);
        if (sourceCell.Structure != BattleStructureType.Gate || sourceCell.IsGateOpen || sourceCell.IsBroken)
        {
            return false;
        }

        return targetGrid.Level == 0 && !IsInsideCityGroundGrid(targetGrid.Grid);
    }

    private IEnumerable<BattleGridKey> GetAttackCandidateGridKeys(BattleGridKey sourceGrid, BattleOccupantInfo attacker, Vector2I targetGrid)
    {
        if (_mapData == null || !IsWithinMap(targetGrid))
        {
            yield break;
        }

        if (IsCrossLevelRangedAttacker(attacker, sourceGrid))
        {
            if (IsWallTopGrid(targetGrid))
            {
                yield return ToWallWalkGridKey(targetGrid);
                if (IsAttackableStructureGroundGrid(targetGrid) || IsOpenGateGroundGrid(targetGrid))
                {
                    yield return ToGroundGridKey(targetGrid);
                }
            }
            else
            {
                yield return ToGroundGridKey(targetGrid);
            }

            yield break;
        }

        if (sourceGrid.Level == 2)
        {
            if (IsWallTopGrid(targetGrid))
            {
                yield return ToWallWalkGridKey(targetGrid);
                if (IsAttackableStructureGroundGrid(targetGrid) || IsOpenGateGroundGrid(targetGrid))
                {
                    yield return ToGroundGridKey(targetGrid);
                }
            }

            yield break;
        }

        yield return ToGroundGridKey(targetGrid);
    }

    private bool IsAttackLevelCompatible(BattleGridKey sourceGrid, BattleOccupantInfo attacker, BattleGridKey targetGrid)
    {
        if (_mapData == null || !IsWithinMap(targetGrid.Grid))
        {
            return false;
        }

        if (sourceGrid.Level == targetGrid.Level)
        {
            return targetGrid.Level != 2 || IsWallTopGrid(targetGrid.Grid);
        }

        if (!IsCrossLevelRangedAttacker(attacker, sourceGrid))
        {
            return false;
        }

        return (sourceGrid.Level, targetGrid.Level) is (0, 2) or (2, 0) &&
               (targetGrid.Level != 2 || IsWallTopGrid(targetGrid.Grid)) &&
               (targetGrid.Level != 0 ||
                !IsWallTopGrid(targetGrid.Grid) ||
                IsAttackableStructureGroundGrid(targetGrid.Grid) ||
                IsOpenGateGroundGrid(targetGrid.Grid));
    }

    private bool IsAttackableStructureGroundGrid(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return ((cell.Structure == BattleStructureType.Gate || cell.Structure == BattleStructureType.WoodenFence) &&
                cell.HasStructureHealth &&
                !cell.IsBroken) ||
               cell.HasBridgeHealth;
    }

    private bool IsOpenGateGroundGrid(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return cell.Structure == BattleStructureType.Gate && (cell.IsGateOpen || cell.IsBroken);
    }

    private static bool IsCrossLevelRangedAttacker(BattleOccupantInfo attacker, BattleGridKey sourceGrid)
    {
        if (attacker.Category == CategoryUnit)
        {
            return attacker.TroopType is TroopArcher or TroopCrossbow;
        }

        return attacker.Category == CategorySiegeEngine &&
               attacker.TroopType == TroopCatapult &&
               sourceGrid.Level == 0;
    }

    private bool IsBuildingCoverActive(BattleGridKey? gridKey)
    {
        if (!gridKey.HasValue ||
            _mapData == null ||
            gridKey.Value.Level != 0 ||
            !IsWithinMap(gridKey.Value.Grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(gridKey.Value.X, gridKey.Value.Y);
        return cell.ProvidesBuildingCover &&
               !_activeFireByGrid.ContainsKey(ToGroundGridKey(gridKey.Value.Grid));
    }


}
