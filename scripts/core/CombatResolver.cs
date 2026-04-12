using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class CombatResult
{
    public bool AttackerWon { get; set; }
    public int AttackerLosses { get; set; }
    public int DefenderLosses { get; set; }
}

public class CombatResolver
{
    public CombatResult Resolve(CityData attacker, CityData defender, int attackingTroops)
    {
        var clampedAttackTroops = attackingTroops < 0 ? 0 : attackingTroops;

        // Defense now contributes directly to defender effective strength.
        var attackMultiplier = 1.0f + (attacker.Defense * 0.002f);
        var defenseMultiplier = 1.0f + (defender.Defense * 0.006f);

        var effectiveAttack = clampedAttackTroops * attackMultiplier;
        var effectiveDefense = defender.Troops * defenseMultiplier;

        var attackerWon = effectiveAttack >= effectiveDefense;
        return new CombatResult
        {
            AttackerWon = attackerWon,
            AttackerLosses = attackerWon ? defender.Troops / 3 : clampedAttackTroops / 2,
            DefenderLosses = attackerWon ? defender.Troops : clampedAttackTroops / 2
        };
    }
}
