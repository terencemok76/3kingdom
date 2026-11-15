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
    private void OnStrategyButtonPressed()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue)
        {
            return;
        }

        _selectedStrategyAction = ResolveStrategyAction(_selectedUnit, _selectedUnitGrid);
        _commandMode = BattleCommandMode.StrategySelect;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        if (_selectedStrategyAction == BattleStrategyAction.None)
        {
            _commandMode = BattleCommandMode.AwaitingCommand;
            HideCommandMenu();
            RefreshInfoPanel();
            RefreshHighlights();
            return;
        }

        var strategyTargets = _selectedStrategyAction switch
        {
            BattleStrategyAction.Extinguish => CalculateExtinguishStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit),
            BattleStrategyAction.Fire => CalculateFireStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit),
            _ => CalculateMentalStrategyTargetGrids(_selectedUnitGrid.Value, _selectedUnit)
        };
        foreach (var targetGrid in strategyTargets)
        {
            _strategyTargetGrids.Add(targetGrid);
        }

        if (_strategyTargetGrids.Count == 0)
        {
            _commandMode = BattleCommandMode.AwaitingCommand;
            HideCommandMenu();
            RefreshInfoPanel();
            RefreshHighlights();
            return;
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private IEnumerable<BattleGridKey> CalculateFireStrategyTargetGrids(BattleGridKey sourceGrid, BattleOccupantInfo attacker)
    {
        var strategyAttacker = attacker with { AttackRange = attacker.AttackRange + FireStrategyRangeBonus };
        foreach (var grid in CalculateAttackableGrids(sourceGrid, strategyAttacker, allowHillRangeBonus: false))
        {
            if (grid.Level != 0 || !IsWithinMap(grid.Grid) || _mapData == null)
            {
                continue;
            }

            var cell = _mapData.GetCell(grid.X, grid.Y);
            if (CanCellIgnite(cell))
            {
                yield return grid;
            }
        }
    }

    private bool TryExecuteSelectedStrategy()
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || !_selectedGrid.HasValue || _selectedUnit.Marker == null)
        {
            return false;
        }

        var targetGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        if (!_strategyTargetGrids.Contains(targetGrid))
        {
            return false;
        }

        if (_selectedStrategyAction == BattleStrategyAction.Mental)
        {
            return TryExecuteMentalStrategy(targetGrid);
        }

        if (_selectedStrategyAction == BattleStrategyAction.Extinguish)
        {
            return TryExecuteExtinguishStrategy(targetGrid);
        }

        if (!CanUseFireStrategy(_selectedUnit))
        {
            return false;
        }

        var actingMarker = _selectedUnit.Marker;
        if (actingMarker == null)
        {
            return false;
        }

        var attackDirection = GetInfantryDirection(_selectedUnitGrid.Value.Grid, targetGrid.Grid);
        var actingUnit = _selectedUnit with { FacingDirection = attackDirection };
        if (!TrySpendWeaponAmmo(actingUnit, out actingUnit))
        {
            return false;
        }

        if (!IgniteBattleFire(targetGrid))
        {
            return false;
        }

        if (!TrySpendStrategyPlan(_selectedUnit.TeamName))
        {
            return false;
        }

        ReplaceOccupantAtGrid(_selectedUnitGrid.Value, _selectedUnit, actingUnit);
        _selectedUnit = actingUnit;
        _strategyUsedByMarkerThisTurn.Add(actingMarker);
        var shouldTemporarilyRevealOccludedUnits =
            IsUnitOccludedByCastleVisual(_selectedUnitGrid.Value) ||
            IsUnitOccludedByCastleVisual(targetGrid);
        if (shouldTemporarilyRevealOccludedUnits)
        {
            ClearOccludedUnitSilhouettes();
        }

        var effectDelaySeconds = PlayStrategyActionEffects(_selectedUnitGrid.Value, targetGrid, actingUnit, attackDirection);
        AppendBattleLog(
            _selectedUnit,
            "Strategy",
            BattleFormat(
                "log.strategy_fire",
                "{0} ignites fire at {1}{2}",
                FormatLogUnit(_selectedUnit),
                targetGrid,
                FormatWeaponAmmoLog(_selectedUnit)));
        MarkUnitActed(_selectedUnit);
        if (shouldTemporarilyRevealOccludedUnits)
        {
            RefreshOccludedUnitSilhouettesAfterDelay(effectDelaySeconds);
        }

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
        return true;
    }

    private BattleStrategyAction ResolveStrategyAction(BattleOccupantInfo occupant, BattleGridKey? sourceGrid = null)
    {
        if (sourceGrid.HasValue && CanUseExtinguishStrategy(occupant, sourceGrid.Value))
        {
            return BattleStrategyAction.Extinguish;
        }

        if (CanUseFireStrategy(occupant))
        {
            return BattleStrategyAction.Fire;
        }

        if (CanUseMentalStrategy(occupant))
        {
            return BattleStrategyAction.Mental;
        }

        return BattleStrategyAction.None;
    }

    private IEnumerable<BattleGridKey> CalculateExtinguishStrategyTargetGrids(BattleGridKey sourceGrid, BattleOccupantInfo officer)
    {
        if (!CanUseExtinguishStrategy(officer, sourceGrid))
        {
            yield break;
        }

        foreach (var fireGrid in _activeFireByGrid.Keys)
        {
            if (fireGrid.Level == sourceGrid.Level &&
                GetChebyshevDistance(sourceGrid.Grid, fireGrid.Grid) <= ExtinguishFireRange)
            {
                yield return fireGrid;
            }
        }
    }

    private bool TryExecuteExtinguishStrategy(BattleGridKey targetGrid)
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || _selectedUnit.Marker == null ||
            !CanUseExtinguishStrategy(_selectedUnit, _selectedUnitGrid.Value) ||
            !_activeFireByGrid.ContainsKey(targetGrid) ||
            GetChebyshevDistance(_selectedUnitGrid.Value.Grid, targetGrid.Grid) > ExtinguishFireRange)
        {
            return false;
        }

        var actingUnit = _selectedUnit with { Energy = _selectedUnit.Energy - ExtinguishFireEnergyCost };
        ReplaceOccupantAtGrid(_selectedUnitGrid.Value, _selectedUnit, actingUnit);
        _selectedUnit = actingUnit;
        _strategyUsedByMarkerThisTurn.Add(actingUnit.Marker);

        var successChance = GetExtinguishFireSuccessChance(actingUnit, targetGrid);
        var success = GD.Randf() * 100.0f <= successChance;
        if (success)
        {
            _activeFireByGrid.Remove(targetGrid);
            RemoveFireVisual(targetGrid);
            RefreshBattleDepthLayerOrder();
            ShowBattlePopup(targetGrid, BattleText("ui.battle.extinguish_success_popup", "Fire Extinguished"), new Color(0.25f, 0.78f, 1.0f, 1.0f), new Color(0.01f, 0.10f, 0.16f, 0.95f), new Vector2(-46.0f, -108.0f), 1.6, 22);
            AppendBattleLog(actingUnit, "Strategy", $"{FormatLogUnit(actingUnit)} extinguishes fire at {targetGrid} (success {successChance:N0}%, energy {ExtinguishFireEnergyCost})");
        }
        else
        {
            ShowBattlePopup(targetGrid, BattleText("ui.battle.extinguish_fail_popup", "Extinguish Failed"), new Color(1.0f, 0.72f, 0.20f, 1.0f), new Color(0.16f, 0.06f, 0.01f, 0.95f), new Vector2(-46.0f, -108.0f), 1.6, 22);
            AppendBattleLog(actingUnit, "Strategy", $"{FormatLogUnit(actingUnit)} fails to extinguish fire at {targetGrid} ({successChance:N0}%, energy {ExtinguishFireEnergyCost})");
        }

        MarkUnitActed(actingUnit);
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

    private bool CanUseExtinguishStrategy(BattleOccupantInfo? occupant, BattleGridKey sourceGrid)
    {
        return occupant?.Marker != null &&
               IsGeneralCountedPiece(occupant.Category, occupant.OfficerName) &&
               occupant.Energy >= ExtinguishFireEnergyCost &&
               !_strategyUsedByMarkerThisTurn.Contains(occupant.Marker) &&
               !IsMessed(occupant) &&
               _activeFireByGrid.Keys.Any(fireGrid =>
                   fireGrid.Level == sourceGrid.Level &&
                   GetChebyshevDistance(sourceGrid.Grid, fireGrid.Grid) <= ExtinguishFireRange);
    }

    private float GetExtinguishFireSuccessChance(BattleOccupantInfo officer, BattleGridKey targetGrid)
    {
        var weatherBonus = GetCurrentBattleWeather() switch
        {
            BattleWeatherType.Rain => 18,
            BattleWeatherType.Cloudy => 8,
            _ => 0
        };
        var terrainBonus = _mapData?.GetCell(targetGrid.X, targetGrid.Y).Terrain == BattleTerrainType.Forest ? -8 : 0;
        return Mathf.Clamp(25.0f + (GetOfficerTacticalIntelligence(officer.OfficerName) * 0.6f) + weatherBonus + terrainBonus + GetStrategistSuccessBonus(officer), 35.0f, 90.0f);
    }

    private IEnumerable<BattleGridKey> CalculateMentalStrategyTargetGrids(BattleGridKey sourceGrid, BattleOccupantInfo actor)
    {
        if (!CanUseMentalStrategy(actor))
        {
            yield break;
        }

        foreach (var (targetGrid, target) in GetMentalStrategyCandidates(sourceGrid, actor))
        {
            if (CanApplyMessStrategy(actor, target) || CanApplyCalmStrategy(actor, target))
            {
                yield return targetGrid;
            }
        }
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetMentalStrategyCandidates(BattleGridKey sourceGrid, BattleOccupantInfo actor)
    {
        foreach (var entry in _occupantsByGrid)
        {
            var targetGrid = entry.Key;
            if (targetGrid.Level != sourceGrid.Level ||
                GetChebyshevDistance(sourceGrid.Grid, targetGrid.Grid) > MessStrategyRange)
            {
                continue;
            }

            foreach (var target in entry.Value)
            {
                if (target.Marker != null &&
                    target.Category == CategoryUnit &&
                    !IsHiddenFromSide(target, actor.TeamName))
                {
                    yield return (targetGrid, target);
                }
            }
        }
    }

    private bool TryExecuteMentalStrategy(BattleGridKey targetGrid)
    {
        if (_selectedUnit == null || !_selectedUnitGrid.HasValue || _selectedUnit.Marker == null)
        {
            return false;
        }

        var target = GetMentalStrategyTarget(_selectedUnit, targetGrid);
        if (target == null)
        {
            return false;
        }

        var success = target.TeamName == _selectedUnit.TeamName
            ? TryApplyCalmStrategy(targetGrid, target)
            : TryApplyMessStrategy(targetGrid, target, _selectedUnit);
        if (!success)
        {
            return false;
        }

        if (!TrySpendStrategyPlan(_selectedUnit.TeamName))
        {
            return false;
        }

        _strategyUsedByMarkerThisTurn.Add(_selectedUnit.Marker);
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

    private BattleOccupantInfo? GetMentalStrategyTarget(BattleOccupantInfo actor, BattleGridKey targetGrid)
    {
        return GetOccupantsAtGrid(targetGrid.Grid)
            .Where(entry => entry.Grid == targetGrid)
            .Select(entry => entry.Occupant)
            .FirstOrDefault(occupant =>
                occupant.Marker != null &&
                occupant.Category == CategoryUnit &&
                !IsHiddenFromSide(occupant, actor.TeamName));
    }

    private bool TryApplyMessStrategy(BattleGridKey targetGrid, BattleOccupantInfo target, BattleOccupantInfo actor)
    {
        if (!CanApplyMessStrategy(actor, target) ||
            !TryGetCurrentOccupantAtGrid(targetGrid, target, out var currentTarget))
        {
            return false;
        }

        target = currentTarget;
        var successChance = GetMessStrategySuccessChance(actor, target);
        if (GD.Randf() * 100.0f > successChance)
        {
            AppendBattleLog(actor, "Strategy", $"{FormatLogUnit(actor)} uses Mess against {FormatLogUnit(target)}, but it fails ({successChance:N0}%)");
            return true;
        }

        var updatedMorale = target.Morale.HasValue
            ? Mathf.Clamp(target.Morale.Value - MessStrategyMoraleDamage, 0, 120)
            : target.Morale;
        var updatedTarget = target with
        {
            MessTurns = Math.Max(target.MessTurns, MessStrategyDurationTurns),
            Morale = updatedMorale
        };
        ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedTarget;
        }

        if (target.Morale.HasValue && updatedMorale.HasValue)
        {
            ShowMoralePopup(targetGrid, updatedMorale.Value - target.Morale.Value);
        }

        AppendBattleLog(actor, "Strategy", $"{FormatLogUnit(actor)} uses Mess against {FormatLogUnit(target)}: status Mess {updatedTarget.MessTurns} turns, morale -{MessStrategyMoraleDamage} ({successChance:N0}%)");
        return true;
    }

    private bool TryApplyCalmStrategy(BattleGridKey targetGrid, BattleOccupantInfo target)
    {
        if (_selectedUnit == null ||
            !CanApplyCalmStrategy(_selectedUnit, target) ||
            !TryGetCurrentOccupantAtGrid(targetGrid, target, out var currentTarget))
        {
            return false;
        }

        target = currentTarget;
        var updatedMorale = target.Morale.HasValue
            ? Mathf.Clamp(target.Morale.Value + CalmStrategyMoraleRestore, 0, 120)
            : target.Morale;
        var updatedTarget = target with
        {
            MessTurns = 0,
            Morale = updatedMorale
        };
        ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedTarget;
        }

        if (target.Morale.HasValue && updatedMorale.HasValue)
        {
            ShowMoralePopup(targetGrid, updatedMorale.Value - target.Morale.Value);
        }

        AppendBattleLog(_selectedUnit, "Strategy", $"{FormatLogUnit(_selectedUnit)} uses Calm on {FormatLogUnit(target)}: Mess cleared, morale +{CalmStrategyMoraleRestore}");
        return true;
    }

    private bool CanUseMentalStrategy(BattleOccupantInfo? occupant)
    {
        return occupant?.Marker != null &&
               occupant.Category == CategoryUnit &&
               HasStrategyPlans(occupant.TeamName) &&
               !_strategyUsedByMarkerThisTurn.Contains(occupant.Marker) &&
               !IsMessed(occupant);
    }

    private static bool CanApplyMessStrategy(BattleOccupantInfo actor, BattleOccupantInfo target)
    {
        return actor.TeamName != target.TeamName &&
               target.Category == CategoryUnit &&
               !IsMessed(target);
    }

    private static bool CanApplyCalmStrategy(BattleOccupantInfo actor, BattleOccupantInfo target)
    {
        return actor.TeamName == target.TeamName &&
               target.Category == CategoryUnit &&
               IsMessed(target);
    }

    private float GetMessStrategySuccessChance(BattleOccupantInfo actor, BattleOccupantInfo target)
    {
        var officerDelta = GetOfficerBattleAttribute(actor.OfficerName) - GetOfficerBattleAttribute(target.OfficerName);
        var targetMorale = target.Morale ?? DefaultUnitMorale;
        var lowMoraleBonus = targetMorale <= MessMoraleThreshold ? 20 : 0;
        return Mathf.Clamp(55.0f + (officerDelta * 0.5f) + ((50 - targetMorale) * 0.5f) + lowMoraleBonus + GetStrategistSuccessBonus(actor), 25.0f, 90.0f);
    }

    private int GetStrategistSuccessBonus(BattleOccupantInfo unit)
    {
        if (unit.Category != CategoryUnit || !IsGeneralCountedPiece(unit.Category, unit.OfficerName)) return 0;
        var world = CampaignRuntimeContext.World;
        var officer = world?.GetOfficer(GetBattleOfficerId(unit));
        var faction = officer == null ? null : world?.Factions.FirstOrDefault(candidate => candidate.OfficerIds.Contains(officer.Id));
        var chiefPresent = OfficerAppointmentRules.IsChiefStrategistPresent(faction, unit.TeamName,
            GetAllBattlePieces()
                .Where(entry => entry.Occupant.Category == CategoryUnit && IsGeneralCountedPiece(entry.Occupant.Category, entry.Occupant.OfficerName))
                .Select(entry => (GetBattleOfficerId(entry.Occupant), entry.Occupant.TeamName, entry.Occupant.TroopCount)));
        return OfficerAppointmentRules.GetStrategySuccessBonus(officer, faction, chiefPresent);
    }

    private double PlayStrategyActionEffects(
        BattleGridKey sourceGrid,
        BattleGridKey targetGrid,
        BattleOccupantInfo actingUnit,
        BattleSpriteDirection attackDirection)
    {
        if (actingUnit.Category == CategorySiegeEngine && actingUnit.TroopType == TroopCatapult)
        {
            var attackAnimationDuration = ApplyAttackAnimation(actingUnit, attackDirection);
            var projectileDuration = PlayCatapultProjectileEffect(sourceGrid, targetGrid);
            return Math.Max(attackAnimationDuration, projectileDuration);
        }

        if (IsArrowProjectileAttacker(actingUnit))
        {
            var attackAnimationDuration = ApplyAttackAnimation(actingUnit, attackDirection);
            var projectileDuration = PlayArrowProjectileEffect(sourceGrid, targetGrid);
            return Math.Max(attackAnimationDuration, projectileDuration);
        }

        return 0.0;
    }

    private bool CanUseFireStrategy(BattleOccupantInfo? occupant)
    {
        if (occupant?.Marker == null || !CanUseUnitTypeFireStrategy(occupant))
        {
            return false;
        }

        return !_strategyUsedByMarkerThisTurn.Contains(occupant.Marker) &&
               HasStrategyPlans(occupant.TeamName) &&
               !IsMessed(occupant) &&
               HasWeaponAmmo(occupant);
    }

    private static bool CanUseUnitTypeFireStrategy(BattleOccupantInfo occupant)
    {
        return occupant.TroopType is TroopArcher or TroopCrossbow ||
               (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopCatapult);
    }
}
