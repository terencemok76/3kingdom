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
