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
    public CombatResult Resolve(WorldState world, CityData attacker, CityData defender, int attackingTroops, System.Collections.Generic.List<int>? attackingOfficerIds = null)
    {
        var clampedAttackTroops = attackingTroops < 0 ? 0 : attackingTroops;
        var attackerStrength = GetAverageOfficerStat(world, attacker, officer => officer.Strength, attackingOfficerIds);
        var attackerCombat = GetAverageOfficerStat(world, attacker, officer => officer.Combat, attackingOfficerIds);
        var defenderCombat = GetAverageOfficerStat(world, defender, officer => officer.Combat);

        var attackStat = attackerStrength * 0.6f + attackerCombat * 0.4f;
        var attackMultiplier = 1.0f + attackStat / 200.0f;
        var defenseMultiplier = 1.0f + (defender.Defense * 0.006f) + (defenderCombat / 500.0f);

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

    private static int GetAverageOfficerStat(
        WorldState world,
        CityData city,
        System.Func<OfficerData, int> selector,
        System.Collections.Generic.List<int>? officerIdsOverride = null)
    {
        var total = 0;
        var count = 0;

        var officerIds = officerIdsOverride != null && officerIdsOverride.Count > 0
            ? officerIdsOverride
            : city.OfficerIds;

        foreach (var officerId in officerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            total += selector(officer);
            count += 1;
        }

        return count == 0 ? 50 : total / count;
    }
}
