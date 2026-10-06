using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

internal static class BattleCombatResolver
{
    internal static bool HasTroopTypeAdvantage(BattleOccupantInfo attacker, BattleOccupantInfo target) =>
        (attacker.TroopType, target.TroopType) switch
        {
            (TroopInfantry, TroopSpearman) => true,
            (TroopSpearman, TroopCavalry) => true,
            (TroopCavalry, TroopArcher or TroopCrossbow) => true,
            (TroopArcher or TroopCrossbow, TroopInfantry) => true,
            _ => false
        };

    internal static int GetBaseAttackDamage(BattleOccupantInfo attacker)
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

        var casualtyRate = attacker.TroopType switch
        {
            TroopInfantry => InfantryAttackCasualtyRate,
            TroopSpearman => SpearmanAttackCasualtyRate,
            TroopArcher or TroopCrossbow => ArcherAttackCasualtyRate,
            TroopCavalry => CavalryAttackCasualtyRate,
            TroopWorker => WorkerAttackCasualtyRate,
            _ => 0.0f
        };
        return casualtyRate <= 0.0f
            ? 0
            : System.Math.Max(1, Godot.Mathf.RoundToInt(attacker.TroopCount * casualtyRate));
    }

    internal static int GetStructureAttackDamage(BattleOccupantInfo attacker)
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
}
