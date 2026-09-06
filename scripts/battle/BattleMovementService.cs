using System;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

internal static class BattleMovementService
{
    internal static bool IsBattlePiece(BattleOccupantInfo unit) =>
        unit.Category == CategoryUnit || unit.Category == CategorySiegeEngine;

    internal static bool CanUseCarLadderBridge(BattleOccupantInfo unit) =>
        unit.TeamName.Contains("Attacker") &&
        unit.Category == CategoryUnit &&
        unit.TroopType is TroopInfantry or TroopSpearman or TroopArcher;

    internal static int GetMoveCost(BattleCellData cell) =>
        Math.Max(cell.MovementCost, cell.Terrain switch
        {
            BattleTerrainType.Forest => 2,
            BattleTerrainType.Swamp => 2,
            BattleTerrainType.Hill => 2,
            _ => 1
        });

    internal static int GetAvailableMoveEnergy(BattleOccupantInfo unit) =>
        unit.HasAttackedThisTurn ? 0 : unit.Energy;

    internal static int GetMoveEnergyCost(BattleCellData cell) =>
        cell.Terrain == BattleTerrainType.Road ? 1 : Math.Max(2, GetMoveCost(cell) + 1);
}
