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
        var effectiveAttack = clampedAttackTroops;
        var effectiveDefense = defender.Troops;

        var attackerWon = effectiveAttack >= effectiveDefense;
        return new CombatResult
        {
            AttackerWon = attackerWon,
            AttackerLosses = attackerWon ? effectiveDefense / 3 : effectiveAttack / 2,
            DefenderLosses = attackerWon ? effectiveDefense : effectiveAttack / 2
        };
    }
}
