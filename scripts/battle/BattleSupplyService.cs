using Godot;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

internal static class BattleSupplyService
{
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
}
