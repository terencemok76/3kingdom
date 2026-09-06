using Godot;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

internal readonly record struct BattleDailySupplyResult(
    int Gold,
    int Food,
    int GoldNeed,
    int FoodNeed,
    int GoldSpent,
    int FoodSpent,
    bool IsFoodShortage,
    bool IsLowFood);

internal static class BattleSupplySystem
{
    internal static BattleDailySupplyResult ResolveDailyUpkeep(int gold, int food, int activeTroops)
    {
        var foodNeed = CalculateScaledResourceNeed(activeTroops, DailyFoodPer100ActiveTroops);
        var goldNeed = CalculateScaledResourceNeed(activeTroops, DailyGoldPer100ActiveTroops);
        var foodSpent = Mathf.Min(food, foodNeed);
        var goldSpent = Mathf.Min(gold, goldNeed);
        var remainingFood = Mathf.Max(0, food - foodNeed);
        var remainingGold = Mathf.Max(0, gold - goldNeed);

        return new BattleDailySupplyResult(
            remainingGold,
            remainingFood,
            goldNeed,
            foodNeed,
            goldSpent,
            foodSpent,
            IsFoodShortage: foodNeed > 0 && food < foodNeed,
            IsLowFood: foodNeed > 0 && food >= foodNeed && remainingFood < foodNeed);
    }

    internal static int CalculateScaledResourceNeed(int activeTroops, int per100Troops) =>
        activeTroops <= 0 || per100Troops <= 0
            ? 0
            : Mathf.CeilToInt(activeTroops / 100.0f * per100Troops);

    internal static bool UsesWeaponAmmo(BattleOccupantInfo unit) => unit.MaxWeaponAmmo.HasValue;

    internal static bool HasWeaponAmmo(BattleOccupantInfo unit) =>
        !UsesWeaponAmmo(unit) || unit.WeaponAmmo.GetValueOrDefault() > 0;

    internal static bool CanUseAmmoDepletedWeakAttack(BattleOccupantInfo unit) =>
        ((unit.Category == CategoryUnit && unit.TroopType is TroopArcher or TroopCrossbow) ||
         (unit.Category == CategorySiegeEngine && unit.TroopType == TroopCatapult)) &&
        unit.MaxWeaponAmmo.HasValue &&
        unit.WeaponAmmo.GetValueOrDefault() <= 0;

    internal static bool TrySpendWeaponAmmo(BattleOccupantInfo unit, out BattleOccupantInfo updatedUnit)
    {
        updatedUnit = unit;
        if (!UsesWeaponAmmo(unit))
        {
            return true;
        }

        var currentAmmo = unit.WeaponAmmo.GetValueOrDefault();
        if (currentAmmo <= 0)
        {
            return false;
        }

        updatedUnit = unit with { WeaponAmmo = currentAmmo - 1 };
        return true;
    }

    internal static bool TryRefillWeaponAmmo(
        BattleOccupantInfo unit,
        out BattleOccupantInfo updatedUnit,
        out int refilledAmmo)
    {
        updatedUnit = unit;
        refilledAmmo = 0;
        if (!unit.WeaponAmmo.HasValue ||
            !unit.MaxWeaponAmmo.HasValue ||
            unit.WeaponAmmo.Value >= unit.MaxWeaponAmmo.Value)
        {
            return false;
        }

        refilledAmmo = unit.MaxWeaponAmmo.Value - unit.WeaponAmmo.Value;
        updatedUnit = unit with { WeaponAmmo = unit.MaxWeaponAmmo.Value };
        return true;
    }

    internal static bool TryRepairSiegeEngine(
        BattleOccupantInfo unit,
        int repairAmount,
        out BattleOccupantInfo updatedUnit,
        out int actualRepair)
    {
        updatedUnit = unit;
        actualRepair = 0;
        if (repairAmount <= 0 || unit.Category != CategorySiegeEngine || unit.HitPoints >= unit.MaxHitPoints)
        {
            return false;
        }

        actualRepair = Mathf.Min(repairAmount, unit.MaxHitPoints - unit.HitPoints);
        updatedUnit = unit with { HitPoints = unit.HitPoints + actualRepair };
        return actualRepair > 0;
    }

    internal static bool TryRecoverWoundedTroops(
        BattleOccupantInfo unit,
        int recoveryAmount,
        out BattleOccupantInfo updatedUnit,
        out int actualRecovery)
    {
        updatedUnit = unit;
        actualRecovery = 0;
        if (recoveryAmount <= 0 || unit.Category != CategoryUnit || unit.WoundedTroops <= 0)
        {
            return false;
        }

        var missingActiveCapacity = Mathf.Max(0, unit.MaxHitPoints - unit.TroopCount);
        actualRecovery = Mathf.Min(recoveryAmount, Mathf.Min(unit.WoundedTroops, missingActiveCapacity));
        if (actualRecovery <= 0)
        {
            return false;
        }

        updatedUnit = unit with
        {
            TroopCount = unit.TroopCount + actualRecovery,
            HitPoints = unit.HitPoints + actualRecovery,
            WoundedTroops = unit.WoundedTroops - actualRecovery
        };
        return true;
    }
}
