using Godot;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

internal static class BattleBridgeSystem
{
    internal const int EmergencyRepairAmount = 150;
    internal const int EmergencyRepairEnergyCost = 5;
    // BattleMovementService adds one energy to non-road terrain movement cost,
    // so a base cost of 2 produces the intended 3-energy bridge crossing.
    internal const int HeavilyDamagedTerrainMoveCost = 2;
    internal const int HeavilyDamagedMoveRangeCost = 3;

    internal static bool CanEmergencyRepair(BattleOccupantInfo unit) =>
        unit.Category == CategoryUnit &&
        unit.TroopType is TroopInfantry or TroopSpearman or TroopGuard;

    internal static bool IsEmergencyRepairTarget(BattleCellData cell) =>
        cell.IsBridgeDamaged && !cell.IsBridgeUnderConstruction;

    internal static bool IsHeavilyDamaged(BattleCellData cell) =>
        cell.HasBridgeHealth &&
        !cell.IsBridgeUnderConstruction &&
        cell.BridgeHealth > 0 &&
        cell.BridgeHealth * 2 <= cell.BridgeMaxHealth;

    internal static bool CanUnitTraverse(BattleOccupantInfo unit, BattleCellData cell)
    {
        if (!cell.HasBridgeHealth)
        {
            return true;
        }

        if (cell.IsBridgeUnderConstruction)
        {
            return false;
        }

        if (unit.Category != CategorySiegeEngine)
        {
            return true;
        }

        return !cell.IsWoodenBridge && !IsHeavilyDamaged(cell);
    }

    internal static int GetMoveCost(BattleCellData cell) =>
        IsHeavilyDamaged(cell) ? HeavilyDamagedTerrainMoveCost : 1;

    internal static int GetMoveRangeCost(BattleCellData cell) =>
        IsHeavilyDamaged(cell) ? HeavilyDamagedMoveRangeCost : 1;

    internal static int ApplyDamage(BattleCellData cell, int damage)
    {
        if (!cell.HasBridgeHealth || damage <= 0)
        {
            return 0;
        }

        var actualDamage = Mathf.Min(cell.BridgeHealth, damage);
        cell.BridgeHealth -= actualDamage;
        if (cell.BridgeHealth > 0)
        {
            cell.BlocksMovement = cell.IsBridgeUnderConstruction;
            return actualDamage;
        }

        cell.Terrain = cell.BridgeRestoresToRiver ? BattleTerrainType.River : BattleTerrainType.Moat;
        cell.HasBridgeVisual = false;
        cell.BridgeFlipHorizontally = false;
        cell.BridgeAtlasSourceId = -1;
        cell.BridgeAtlasCoords = new Vector2I(-1, -1);
        cell.BridgeMaxHealth = 0;
        cell.IsWoodenBridge = false;
        cell.IsBridgeUnderConstruction = false;
        cell.BridgeRestoresToRiver = false;
        cell.BlocksMovement = true;
        return actualDamage;
    }

    internal static int ApplyRepair(BattleCellData cell, int repairAmount)
    {
        if (!cell.IsBridgeDamaged || repairAmount <= 0)
        {
            return 0;
        }

        var actualRepair = Mathf.Min(repairAmount, cell.BridgeMaxHealth - cell.BridgeHealth);
        cell.BridgeHealth += actualRepair;
        if (cell.BridgeHealth >= cell.BridgeMaxHealth)
        {
            cell.IsBridgeUnderConstruction = false;
        }

        cell.BlocksMovement = cell.IsBridgeUnderConstruction;
        return actualRepair;
    }
}
