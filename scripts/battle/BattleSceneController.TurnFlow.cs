using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void ResolveBattleStatusAtTurnStart(string actingSideName)
    {
        foreach (var (grid, occupants) in _occupantsByGrid.ToList())
        {
            foreach (var unit in occupants.ToList())
            {
                if (unit.TeamName != actingSideName || unit.Category != CategoryUnit)
                {
                    continue;
                }

                var currentUnit = unit;
                if (IsMessed(currentUnit))
                {
                    currentUnit = ApplyMessDesertion(grid, currentUnit);
                }

                if (currentUnit.HitPoints <= 0)
                {
                    continue;
                }

                TryApplyLowMoraleMessAtTurnStart(grid, currentUnit);
            }
        }
    }

    private void ResolveBattleStatusAtTurnEnd(string endingSideName)
    {
        foreach (var (grid, occupants) in _occupantsByGrid.ToList())
        {
            foreach (var unit in occupants.ToList())
            {
                if (unit.TeamName != endingSideName || !IsMessed(unit))
                {
                    continue;
                }

                var updatedUnit = unit with { MessTurns = Math.Max(0, unit.MessTurns - 1) };
                ReplaceOccupantAtGrid(grid, unit, updatedUnit);
                if (_selectedUnit == unit)
                {
                    _selectedUnit = updatedUnit;
                }
            }
        }

        ResolveBuildingRestAtTurnEnd(endingSideName);
    }

    private void ResolveBuildingRestAtTurnEnd(string endingSideName)
    {
        if (GetTeamFood(endingSideName) <= 0)
        {
            return;
        }

        foreach (var (grid, occupants) in _occupantsByGrid.ToList())
        {
            foreach (var unit in occupants.ToList())
            {
                if (!CanUnitRestInBuilding(grid, unit, endingSideName))
                {
                    continue;
                }

                var recoveredTroops = RecoverWoundedTroops(grid, unit, BuildingRestWoundedRecoveryAmount);
                if (recoveredTroops > 0)
                {
                    AppendBattleLog(unit, "Rest", $"{FormatLogUnit(unit)} rests in a defensive position and recovers {recoveredTroops:N0} wounded troop(s).");
                }
            }
        }
    }

    private bool CanUnitRestInBuilding(BattleGridKey grid, BattleOccupantInfo unit, string endingSideName)
    {
        if (unit.TeamName != endingSideName ||
            unit.Category != CategoryUnit ||
            unit.WoundedTroops <= 0 ||
            IsMessed(unit) ||
            !IsBuildingOrOwnedOutpost(grid, unit.TeamName) ||
            HasUnitActed(unit) ||
            unit.HasAttackedThisTurn ||
            unit.IsGuarding ||
            unit.Energy != GetTeamEnergyCap(unit.TeamName) ||
            unit.RemainingMoveRange != GetTeamMoveRangeCap(unit) ||
            HasAdjacentEnemy(grid, unit.TeamName))
        {
            return false;
        }

        return true;
    }

    private bool IsBuildingOrOwnedOutpost(BattleGridKey grid, string teamName)
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

    private bool HasAdjacentEnemy(BattleGridKey grid, string teamName)
    {
        return GetAllBattlePieces().Any(entry =>
            entry.Grid.Level == grid.Level &&
            entry.Occupant.TeamName != teamName &&
            Math.Abs(entry.Grid.X - grid.X) <= 1 &&
            Math.Abs(entry.Grid.Y - grid.Y) <= 1);
    }

    private BattleOccupantInfo ApplyMessDesertion(BattleGridKey grid, BattleOccupantInfo unit)
    {
        var deserters = Math.Max(1, Mathf.FloorToInt(unit.TroopCount * (MessDesertionPercent / 100.0f)));
        deserters = Math.Min(deserters, unit.TroopCount);
        if (deserters <= 0)
        {
            return unit;
        }

        var updatedUnit = unit with
        {
            TroopCount = unit.TroopCount - deserters,
            HitPoints = Math.Max(0, unit.HitPoints - deserters)
        };
        UpdateMarkerStrengthBar(updatedUnit);
        ReplaceOccupantAtGrid(grid, unit, updatedUnit);
        ApplyTeamTroopLoss(unit, deserters);
        ShowDamagePopup(grid, deserters);
        AppendBattleLog(unit, "Status", $"{FormatLogUnit(unit)} is in Mess: {deserters:N0} troop(s) leave battle");
        if (_selectedUnit == unit)
        {
            _selectedUnit = updatedUnit;
        }

        if (updatedUnit.HitPoints <= 0)
        {
            DestroyOccupantAfterDelay(grid, updatedUnit, 0.0);
        }

        return updatedUnit;
    }

    private bool TryApplyLowMoraleMessAtTurnStart(BattleGridKey grid, BattleOccupantInfo unit)
    {
        if (IsMessed(unit) ||
            unit.Morale == null ||
            unit.Morale.Value > MessMoraleThreshold ||
            !TryGetCurrentOccupantAtGrid(grid, unit, out var currentUnit))
        {
            return false;
        }

        unit = currentUnit;
        var morale = unit.Morale.GetValueOrDefault(DefaultUnitMorale);
        if (morale > MessMoraleThreshold)
        {
            return false;
        }

        var chance = Mathf.Clamp(45.0f + (MessMoraleThreshold - morale) * 3.0f, 45.0f, 75.0f);
        if (GD.Randf() * 100.0f > chance)
        {
            return false;
        }

        var updatedUnit = unit with { MessTurns = Math.Max(unit.MessTurns, 1) };
        ReplaceOccupantAtGrid(grid, unit, updatedUnit);
        if (_selectedUnit == unit)
        {
            _selectedUnit = updatedUnit;
        }

        AppendBattleLog(unit, "Status", $"{FormatLogUnit(unit)} morale is too low and falls into Mess ({chance:N0}%)");
        return true;
    }






    private int GetAttackTargetPriority(BattleGridKey targetGrid, string attackerTeamName)
    {
        if (!_occupantsByGrid.TryGetValue(targetGrid, out var occupants))
        {
            return int.MaxValue;
        }

        var target = GetAttackTargetForAttack(occupants, attackerTeamName, targetGrid);
        return target == null ? int.MaxValue : target.Category == CategorySiegeEngine ? target.HitPoints : target.TroopCount;
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetAllBattlePieces()
    {
        foreach (var (grid, occupants) in _occupantsByGrid)
        {
            foreach (var occupant in occupants)
            {
                if (IsBattlePiece(occupant))
                {
                    yield return (grid, occupant);
                }
            }
        }
    }

    private IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetActingBattlePieces()
    {
        return GetAllBattlePieces().Where(entry => entry.Occupant.TeamName == GetCurrentTurnSideName());
    }

    private bool HaveAllActingBattlePiecesActed()
    {
        return GetActingBattlePieces().All(entry => HasUnitActed(entry.Occupant));
    }

    private bool HasUnitActed(BattleOccupantInfo unit)
    {
        return unit.Marker != null && _actedByMarkerThisRound.Contains(unit.Marker);
    }

    private void MarkUnitActed(BattleOccupantInfo unit)
    {
        if (unit.Marker != null)
        {
            _actedByMarkerThisRound.Add(unit.Marker);
        }
    }

    private void MarkUnitActedForAiOnly(BattleOccupantInfo unit)
    {
        if (IsFieldBattleAiTest && IsCurrentTurnAiControlled())
        {
            MarkUnitActed(unit);
        }
    }

    private void RestoreTeamUnitEnergy(string teamName)
    {
        var energyCap = GetTeamEnergyCap(teamName);
        _occupantsByGrid.UpdateAll((_, occupant) =>
        {
            if (!IsBattlePiece(occupant) || occupant.TeamName != teamName)
            {
                return occupant;
            }

            return occupant with
            {
                Energy = energyCap,
                HasAttackedThisTurn = false,
                RemainingMoveRange = GetTeamMoveRangeCap(occupant),
                IsGuarding = false,
                GuardCounterAvailable = false,
                GuardDamageReductionCount = 0
            };
        });

        if (energyCap < DefaultUnitEnergy)
        {
            AppendBattleLog(teamName, "Supply", $"Food is 0: all troops and vehicles begin this turn with Energy {energyCap}/{DefaultUnitEnergy}.");
        }
    }

    private BattleAiControlledSides GetCurrentAiSideFlag()
    {
        return _currentTurnSide == BattleTurnSide.TeamA
            ? BattleAiControlledSides.Attacker
            : BattleAiControlledSides.Defender;
    }

    private bool IsCurrentTurnAiControlled()
    {
        return (_aiControlledSides & GetCurrentAiSideFlag()) != 0;
    }

    private void FocusCameraOnBattleGrid(BattleGridKey grid)
    {
        if (_camera == null || _mapRoot == null)
        {
            return;
        }

        _mapRoot.Position = GetClampedMapPosition(_camera.GlobalPosition - GetMarkerPosition(grid));
    }

    private void FocusCameraOnCurrentTurnTeam()
    {
        if (_camera == null || _mapRoot == null)
        {
            return;
        }

        var teamPositions = GetActingBattlePieces()
            .Select(entry => GetMarkerPosition(entry.Grid))
            .ToList();
        if (teamPositions.Count == 0)
        {
            return;
        }

        var teamCenter = teamPositions.Aggregate(Vector2.Zero, static (sum, position) => sum + position) /
                         teamPositions.Count;
        _mapRoot.Position = GetClampedMapPosition(_camera.GlobalPosition - teamCenter);
    }

    private void OnEndTurnButtonPressed()
    {
        if (_isBattleFinished)
        {
            return;
        }

        if (IsFieldBattleAiTest && !_isFieldAiRoundStarted)
        {
            AppendBattleLog(GetCurrentTurnSideName(), "Round", "Start Round before ending this field-battle test round.");
            RefreshBattleLogPanel();
            return;
        }

        var endingSideName = GetCurrentTurnSideName();
        CancelCommandAction(clearSelection: true);
        ResolveBattleFireAtTurnEnd();
        ResolveBattleStatusAtTurnEnd(endingSideName);
        ConfirmAttackerOutpostVictoryAtTurnEnd();
        RefreshBattleResultState();
        if (_isBattleFinished)
        {
            return;
        }
        _strategyUsedByMarkerThisTurn.Clear();
        _supplyUsedByMarkerThisTurn.Clear();
        _chargeUsedByMarkerThisTurn.Clear();
        _actedByMarkerThisRound.Clear();
        _isFieldAiRoundStarted = false;

        if (_currentTurnSide == BattleTurnSide.TeamA)
        {
            _currentTurnSide = BattleTurnSide.TeamB;
        }
        else
        {
            _currentTurnSide = BattleTurnSide.TeamA;
            ResolveDailyBattleSupply();
            AdvanceBattleDate();
            _turnNumber++;
        }

        var actingSideName = GetCurrentTurnSideName();
        RestoreTeamUnitEnergy(actingSideName);
        ResolveBattleStatusAtTurnStart(actingSideName);
        ShowTurnBanner();
        AppendBattleLog(actingSideName, "Turn", $"Acting side: {actingSideName}");
        ConfigureHud();
        RefreshBattleLogPanel();
        RefreshCoordinateLabel();
        RefreshInfoPanel();
        RefreshHighlights();
    }

    private void AdvanceBattleDate()
    {
        (_battleDateYear, _battleDateMonth, _battleDateDay) =
            BattleTurnResolver.AdvanceDate(_battleDateYear, _battleDateMonth, _battleDateDay);
    }

    private void OnWeatherButtonPressed()
    {
        _currentBattleWeather = GetNextBattleWeather(GetCurrentBattleWeather());
        ConfigureHud();
        ApplyWeatherVisual(animate: true);
        RefreshHighlights();
    }

    private void OnTimeButtonPressed()
    {
        _currentBattleTimeOfDay = GetNextBattleTimeOfDay(GetCurrentBattleTimeOfDay());
        ConfigureHud();
        ApplyTimeOfDayVisual(animate: true);
        RefreshHighlights();
    }

    private void OnWindButtonPressed()
    {
        _currentBattleWindDirection = GetNextBattleWindDirection(GetCurrentBattleWindDirection());
        ConfigureHud();
    }

    private void OnWindPowerButtonPressed()
    {
        _currentBattleWindPower = GetNextBattleWindPower(GetCurrentBattleWindPower());
        ConfigureHud();
    }

    private string FormatCommandMode(BattleCommandMode commandMode)
    {
        return commandMode switch
        {
            BattleCommandMode.MoveSelect => BattleText("ui.battle.command_move_select", "Select Move Target"),
            BattleCommandMode.AttackSelect => BattleText("ui.battle.command_attack_select", "Select Attack Target"),
            BattleCommandMode.WorkSelect => BattleText("ui.battle.command_work_select", "Select Work Target"),
            BattleCommandMode.StrategySelect => BattleText("ui.battle.command_strategy_select", "Strategy Pending"),
            BattleCommandMode.DuelSelect => BattleText("ui.battle.command_duel_select", "Select Duel Target"),
            BattleCommandMode.ChargeSelect => BattleText("ui.battle.command_charge_select", "Select Charge Target"),
            BattleCommandMode.HireOfficerSelect => BattleText("ui.battle.command_hire_officer_select", "Select Hire Target"),
            BattleCommandMode.AwaitingCommand => BattleText("ui.battle.command_awaiting", "Awaiting Command"),
            _ => BattleText("ui.battle.none", "None")
        };
    }

    private string GetCurrentTurnSideName()
    {
        return BattleTeamIdentity.GetName(_currentTurnSide);
    }

    private string FormatTeamName(string teamName)
    {
        if (BattleTeamIdentity.IsAttacker(teamName))
        {
            return BattleText("ui.battle.team_attacker", "Cao Cao");
        }

        if (BattleTeamIdentity.IsDefender(teamName))
        {
            return BattleText("ui.battle.team_defender", "Dong Zhuo");
        }

        return teamName;
    }

    private bool IsCurrentTurnPiece(BattleOccupantInfo occupant)
    {
        return occupant.TeamName == GetCurrentTurnSideName() &&
               !occupant.HasAttackedThisTurn &&
               (!IsFieldBattleAiTest || (_isFieldAiRoundStarted && !HasUnitActed(occupant)));
    }


}
