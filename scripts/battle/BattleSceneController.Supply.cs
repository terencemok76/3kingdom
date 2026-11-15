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
    private void ResolveBattlePeriodSupply()
    {
        ResolveBattlePeriodSupplyForTeam(TeamAInfo.Name, _state.TeamA);
        ResolveBattlePeriodSupplyForTeam(TeamBInfo.Name, _state.TeamB);
    }

    private void ResolveBattlePeriodSupplyForTeam(string teamName, BattleTeamState team)
    {
        if (IsCaravanEscortTransportTeam(teamName))
        {
            // The shipment itself is the transport side's field ration.  Gold is
            // cargo, not a wage pool, so only food is consumed while on the road.
            var escortSupply = BattleSupplySystem.ResolvePeriodUpkeep(
                gold: 0,
                food: _activeCampaign!.CaravanFood,
                activeTroops: team.TotalTroops,
                goldRemainder: 0,
                foodRemainder: team.FoodUpkeepRemainder);
            team.Gold = 0;
            team.Food = escortSupply.Food;
            team.GoldUpkeepRemainder = 0;
            team.FoodUpkeepRemainder = escortSupply.FoodRemainder;
            team.HadFoodShortageThisDay |= escortSupply.IsFoodShortage;
            if (escortSupply.FoodSpent > 0)
            {
                ApplyTeamResourceDelta(teamName, 0, -escortSupply.FoodSpent);
            }

            if (escortSupply.FoodNeed > 0)
            {
                AppendBattleLog(
                    teamName,
                    "Supply",
                    $"{GetCurrentBattleTimeOfDay()} escort rations: food -{escortSupply.FoodSpent:N0}/{escortSupply.FoodNeed:N0}");
            }

            return;
        }

        var result = BattleSupplySystem.ResolvePeriodUpkeep(
            team.Gold,
            team.Food,
            team.TotalTroops,
            team.GoldUpkeepRemainder,
            team.FoodUpkeepRemainder);
        team.Gold = result.Gold;
        team.Food = result.Food;
        team.GoldUpkeepRemainder = result.GoldRemainder;
        team.FoodUpkeepRemainder = result.FoodRemainder;
        team.HadFoodShortageThisDay |= result.IsFoodShortage;
        RecordCampaignResourceChange(teamName, -result.GoldSpent, -result.FoodSpent);
        if (result.FoodNeed <= 0 && result.GoldNeed <= 0)
        {
            return;
        }

        AppendBattleLog(
            teamName,
            "Supply",
            $"{GetCurrentBattleTimeOfDay()} upkeep: food -{result.FoodSpent:N0}/{result.FoodNeed:N0}, gold -{result.GoldSpent:N0}/{result.GoldNeed:N0}");
    }

    private void ResolveEndOfDaySupplyEffects()
    {
        ResolveEndOfDaySupplyEffectsForTeam(TeamAInfo.Name, _state.TeamA);
        ResolveEndOfDaySupplyEffectsForTeam(TeamBInfo.Name, _state.TeamB);
    }

    private void ResolveEndOfDaySupplyEffectsForTeam(string teamName, BattleTeamState team)
    {
        UpdateTeamZeroFoodDays(teamName);
        var dailyFoodNeed = CalculateDailyFoodNeed(team.TotalTroops);
        if (team.HadFoodShortageThisDay)
        {
            ApplyTeamMoralePenalty(teamName, StarvingMoralePenalty, "food shortage");
            ApplyTeamStarvationDesertion(teamName);
        }
        else if (dailyFoodNeed > 0 && team.Food < dailyFoodNeed)
        {
            ApplyTeamMoralePenalty(teamName, LowFoodMoralePenalty, "low food");
        }

        team.HadFoodShortageThisDay = false;
    }

    private void ApplyTeamStarvationDesertion(string teamName)
    {
        var affectedUnits = GetAllBattlePieces()
            .Where(entry => entry.Occupant.TeamName == teamName &&
                            entry.Occupant.Category == CategoryUnit &&
                            entry.Occupant.TroopCount > 0)
            .ToList();
        foreach (var (grid, unit) in affectedUnits)
        {
            if (!TryGetCurrentOccupantAtGrid(grid, unit, out var currentUnit))
            {
                continue;
            }

            var deserters = Math.Max(1, Mathf.FloorToInt(currentUnit.TroopCount * (StarvationDesertionPercent / 100.0f)));
            deserters = Math.Min(deserters, currentUnit.TroopCount);
            var updatedUnit = currentUnit with
            {
                TroopCount = currentUnit.TroopCount - deserters,
                HitPoints = Math.Max(0, currentUnit.HitPoints - deserters)
            };
            UpdateMarkerStrengthBar(updatedUnit);
            ReplaceOccupantAtGrid(grid, currentUnit, updatedUnit);
            ApplyTeamTroopLoss(currentUnit, deserters);
            ShowDamagePopup(grid, deserters);
            AppendBattleLog(updatedUnit, "Supply", $"Food shortage: {deserters:N0} troop(s) leave battle.");
            if (_selectedUnit == currentUnit)
            {
                _selectedUnit = updatedUnit;
            }

            if (updatedUnit.HitPoints <= 0)
            {
                DestroyOccupantAfterDelay(grid, updatedUnit, 0.0);
            }
        }
    }

    private static int CalculateDailyFoodNeed(int activeTroops)
    {
        return CalculateScaledResourceNeed(activeTroops, DailyFoodPer100ActiveTroops);
    }

    private static int CalculateDailyGoldNeed(int activeTroops)
    {
        return CalculateScaledResourceNeed(activeTroops, DailyGoldPer100ActiveTroops);
    }

    private static int CalculateScaledResourceNeed(int activeTroops, int per100Troops)
    {
        return BattleSupplySystem.CalculateScaledResourceNeed(activeTroops, per100Troops);
    }

    private void ApplyTeamMoralePenalty(
        string teamName,
        int penalty,
        string reason,
        double popupDelaySeconds = 0.0,
        ICollection<(BattleGridKey Grid, int MoraleDelta)>? deferredPopups = null)
    {
        if (penalty <= 0)
        {
            return;
        }

        var affectedUnits = new List<BattleOccupantInfo>();
        foreach (var (grid, occupants) in _occupantsByGrid.ToList())
        {
            foreach (var unit in occupants.ToList())
            {
                if (unit.Category != CategoryUnit ||
                    unit.TeamName != teamName ||
                    unit.Morale == null)
                {
                    continue;
                }

                var updatedMorale = Mathf.Clamp(unit.Morale.Value - penalty, 0, 120);
                var actualDelta = updatedMorale - unit.Morale.Value;
                var updatedUnit = unit with { Morale = updatedMorale };
                ReplaceOccupantAtGrid(grid, unit, updatedUnit);
                if (_selectedUnit == unit)
                {
                    _selectedUnit = updatedUnit;
                }

                if (deferredPopups == null)
                {
                    ShowMoralePopup(grid, actualDelta, popupDelaySeconds);
                }
                else
                {
                    deferredPopups.Add((grid, actualDelta));
                }
                affectedUnits.Add(updatedUnit);
            }
        }

        if (affectedUnits.Count > 0)
        {
            AppendBattleLog(teamName, "Supply", $"{reason}: {affectedUnits.Count} battle team(s) morale -{penalty}");
        }
    }

    private void ApplyTeamMoraleBonus(
        string teamName,
        int bonus,
        string reason,
        double popupDelaySeconds = 0.0,
        ICollection<(BattleGridKey Grid, int MoraleDelta)>? deferredPopups = null)
    {
        if (bonus <= 0)
        {
            return;
        }

        var affectedUnits = new List<BattleOccupantInfo>();
        foreach (var (grid, occupants) in _occupantsByGrid.ToList())
        {
            foreach (var unit in occupants.ToList())
            {
                if (unit.Category != CategoryUnit ||
                    unit.TeamName != teamName ||
                    unit.Morale == null)
                {
                    continue;
                }

                var updatedMorale = Mathf.Clamp(unit.Morale.Value + bonus, 0, 120);
                var actualDelta = updatedMorale - unit.Morale.Value;
                var updatedUnit = unit with { Morale = updatedMorale };
                ReplaceOccupantAtGrid(grid, unit, updatedUnit);
                if (_selectedUnit == unit)
                {
                    _selectedUnit = updatedUnit;
                }

                if (deferredPopups == null)
                {
                    ShowMoralePopup(grid, actualDelta, popupDelaySeconds);
                }
                else
                {
                    deferredPopups.Add((grid, actualDelta));
                }
                affectedUnits.Add(updatedUnit);
            }
        }

        if (affectedUnits.Count > 0)
        {
            AppendBattleLog(teamName, "Supply", $"{reason}: {affectedUnits.Count} battle team(s) morale +{bonus}");
        }
    }

    private (int GoldLoss, int FoodLoss) ApplySupplyCartLostResourcePenalty(BattleOccupantInfo supplyCart, string reason)
    {
        var goldLoss = Mathf.CeilToInt(GetTeamGold(supplyCart.TeamName) * (SupplyCartDestroyedResourceLossPercent / 100.0f));
        var foodLoss = Mathf.CeilToInt(GetTeamFood(supplyCart.TeamName) * (SupplyCartDestroyedResourceLossPercent / 100.0f));
        ApplyTeamResourceDelta(supplyCart.TeamName, -goldLoss, -foodLoss);

        AppendBattleLog(
            supplyCart,
            "Supply",
            $"{FormatLogUnit(supplyCart)} {reason}. Team supplies lost {SupplyCartDestroyedResourceLossPercent}%: gold -{goldLoss:N0}, food -{foodLoss:N0}");
        return (goldLoss, foodLoss);
    }

    private int GetTeamGold(string teamName)
    {
        if (IsCaravanEscortTransportTeam(teamName))
        {
            return _activeCampaign!.CaravanGold;
        }

        return GetBattleTeamState(teamName).Gold;
    }

    private int GetTeamFood(string teamName)
    {
        if (IsCaravanEscortTransportTeam(teamName))
        {
            return _activeCampaign!.CaravanFood;
        }

        return GetBattleTeamState(teamName).Food;
    }

    private void UpdateTeamZeroFoodDays(string teamName)
    {
        var hasActiveTroops = GetTeamActiveTroops(teamName) > 0;
        var zeroFoodDays = hasActiveTroops && GetTeamFood(teamName) <= 0
            ? GetTeamZeroFoodDays(teamName) + 1
            : 0;
        SetTeamZeroFoodDays(teamName, zeroFoodDays);
    }

    private int GetTeamZeroFoodDays(string teamName)
    {
        return GetBattleTeamState(teamName).ZeroFoodDays;
    }

    private void SetTeamZeroFoodDays(string teamName, int zeroFoodDays)
    {
        GetBattleTeamState(teamName).ZeroFoodDays = Math.Max(0, zeroFoodDays);
    }

    private int GetTeamEnergyCap(string teamName)
    {
        if (GetTeamFood(teamName) > 0)
        {
            return DefaultUnitEnergy;
        }

        return Math.Max(1, GetTeamZeroFoodDays(teamName)) switch
        {
            1 => FirstZeroFoodEnergyCap,
            2 => SecondZeroFoodEnergyCap,
            _ => SustainedZeroFoodEnergyCap
        };
    }

    private int GetTeamActiveTroops(string teamName)
    {
        return GetBattleTeamState(teamName).TotalTroops;
    }

    private bool IsAiAttackerHiddenEnemyFortressMission(string teamName)
    {
        return BattleTeamIdentity.IsAttacker(teamName) &&
               !GetAllBattlePieces().Any(entry =>
                   entry.Occupant.TeamName != teamName &&
                   !IsHiddenFromSide(entry.Occupant, teamName));
    }

    private int GetAiEnemyFoodPressureScore(string teamName)
    {
        var enemyTeamName = BattleTeamIdentity.GetOpponentName(teamName);
        var enemyDailyFoodNeed = CalculateDailyFoodNeed(GetTeamActiveTroops(enemyTeamName));
        var ownDailyFoodNeed = CalculateDailyFoodNeed(GetTeamActiveTroops(teamName));
        if (enemyDailyFoodNeed <= 0 || ownDailyFoodNeed <= 0)
        {
            return 0;
        }

        var enemyFoodDays = GetTeamFood(enemyTeamName) / (float)enemyDailyFoodNeed;
        var ownFoodDays = GetTeamFood(teamName) / (float)ownDailyFoodNeed;
        if (enemyFoodDays >= 2.0f || ownFoodDays <= enemyFoodDays)
        {
            return 0;
        }

        return enemyFoodDays <= 1.0f
            ? AiCriticalEnemyFoodPressureScore
            : AiLowEnemyFoodPressureScore;
    }

    private void ApplyTeamResourceDelta(string teamName, int goldDelta, int foodDelta)
    {
        var team = GetBattleTeamState(teamName);
        if (IsCaravanEscortTransportTeam(teamName))
        {
            var previousCaravanGold = _activeCampaign!.CaravanGold;
            var previousCaravanFood = _activeCampaign.CaravanFood;
            _activeCampaign.CaravanGold = Math.Max(0, previousCaravanGold + goldDelta);
            _activeCampaign.CaravanFood = Math.Max(0, previousCaravanFood + foodDelta);
            team.Gold = 0;
            team.Food = _activeCampaign.CaravanFood;
            RecordCampaignResourceChange(
                teamName,
                _activeCampaign.CaravanGold - previousCaravanGold,
                _activeCampaign.CaravanFood - previousCaravanFood);
            if (team.Food > 0)
            {
                team.ZeroFoodDays = 0;
            }

            return;
        }

        var previousGold = team.Gold;
        var previousFood = team.Food;
        team.Gold = Math.Max(0, team.Gold + goldDelta);
        team.Food = Math.Max(0, team.Food + foodDelta);
        RecordCampaignResourceChange(teamName, team.Gold - previousGold, team.Food - previousFood);
        if (team.Food > 0)
        {
            team.ZeroFoodDays = 0;
        }
    }

    private void RecordCampaignResourceChange(string teamName, int goldDelta, int foodDelta)
    {
        if (_activeCampaign == null || (goldDelta == 0 && foodDelta == 0))
        {
            return;
        }

        if (teamName == TeamAInfo.Name)
        {
            if (goldDelta < 0) _activeCampaign.AttackerGoldSpent += -goldDelta; else _activeCampaign.AttackerGoldGained += goldDelta;
            if (foodDelta < 0) _activeCampaign.AttackerFoodSpent += -foodDelta; else _activeCampaign.AttackerFoodGained += foodDelta;
            return;
        }

        if (goldDelta < 0) _activeCampaign.DefenderGoldSpent += -goldDelta; else _activeCampaign.DefenderGoldGained += goldDelta;
        if (foodDelta < 0) _activeCampaign.DefenderFoodSpent += -foodDelta; else _activeCampaign.DefenderFoodGained += foodDelta;
    }

    private bool IsCaravanEscortTransportTeam(string teamName) =>
        _activeCampaign?.IsCaravanEscortBattle == true &&
        !BattleTeamIdentity.IsAttacker(teamName);

    private BattleTeamState GetBattleTeamState(string teamName)
    {
        return _state.GetTeam(BattleTeamIdentity.ResolveSide(teamName));
    }

    private bool TryGetBattleTeamState(string teamName, out BattleTeamState team)
    {
        if (BattleTeamIdentity.TryResolveSide(teamName, out var side))
        {
            team = _state.GetTeam(side);
            return true;
        }

        team = null!;
        return false;
    }

    private static bool UsesWeaponAmmo(BattleOccupantInfo unit)
    {
        return BattleSupplySystem.UsesWeaponAmmo(unit);
    }

    private static bool HasWeaponAmmo(BattleOccupantInfo unit)
    {
        return BattleSupplySystem.HasWeaponAmmo(unit);
    }

    private static bool CanUseAmmoDepletedWeakAttack(BattleOccupantInfo unit)
    {
        return BattleSupplySystem.CanUseAmmoDepletedWeakAttack(unit);
    }

    private static bool CanUseNormalAttackWithCurrentAmmo(BattleOccupantInfo unit)
    {
        return HasWeaponAmmo(unit) || CanUseAmmoDepletedWeakAttack(unit);
    }

    private static bool ShouldSpendWeaponAmmoForNormalAttack(BattleOccupantInfo unit)
    {
        return UsesWeaponAmmo(unit) && !CanUseAmmoDepletedWeakAttack(unit);
    }

    private static bool TrySpendWeaponAmmo(BattleOccupantInfo unit, out BattleOccupantInfo updatedUnit)
    {
        return BattleSupplySystem.TrySpendWeaponAmmo(unit, out updatedUnit);
    }

    private static bool TrySpendNormalAttackWeaponAmmo(BattleOccupantInfo unit, out BattleOccupantInfo updatedUnit)
    {
        updatedUnit = unit;
        return !ShouldSpendWeaponAmmoForNormalAttack(unit) || TrySpendWeaponAmmo(unit, out updatedUnit);
    }

    private bool RefillWeaponAmmo(BattleGridKey targetGrid, BattleOccupantInfo target, out int refilledAmmo)
    {
        refilledAmmo = 0;
        if (!TryGetCurrentOccupantAtGrid(targetGrid, target, out var currentTarget))
        {
            return false;
        }

        target = currentTarget;
        if (!BattleSupplySystem.TryRefillWeaponAmmo(target, out var updatedTarget, out refilledAmmo))
        {
            return false;
        }

        ReplaceOccupantAtGrid(targetGrid, target, updatedTarget);
        if (_selectedUnit == target)
        {
            _selectedUnit = updatedTarget;
        }

        return true;
    }

    private void ApplySupplyCartDestroyedMoralePenalty(BattleOccupantInfo supplyCart)
    {
        ApplySupplyCartLostPenalty(supplyCart, "destroyed");
    }

    private (int GoldLoss, int FoodLoss) ApplySupplyCartLostPenalty(BattleOccupantInfo supplyCart, string reason)
    {
        var resourceLoss = ApplySupplyCartLostResourcePenalty(supplyCart, reason);
        var affectedUnits = new List<BattleOccupantInfo>();
        foreach (var (grid, occupants) in _occupantsByGrid.ToList())
        {
            foreach (var unit in occupants.ToList())
            {
                if (unit.Category != CategoryUnit ||
                    unit.TeamName != supplyCart.TeamName ||
                    unit.Morale == null)
                {
                    continue;
                }

                var updatedMorale = Mathf.FloorToInt(unit.Morale.Value * 0.5f);
                var actualDelta = updatedMorale - unit.Morale.Value;
                var updatedUnit = unit with { Morale = updatedMorale };
                ReplaceOccupantAtGrid(grid, unit, updatedUnit);
                if (_selectedUnit == unit)
                {
                    _selectedUnit = updatedUnit;
                }

                ShowMoralePopup(grid, actualDelta);
                affectedUnits.Add(updatedUnit);
            }
        }

        if (affectedUnits.Count > 0)
        {
            AppendBattleLog(
                supplyCart,
                "Supply",
                $"{FormatLogUnit(supplyCart)} {reason}. {affectedUnits.Count} friendly battle team(s) morale -50%");
        }

        return resourceLoss;
    }
}
