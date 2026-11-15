using System;
using System.Collections.Generic;
using Godot;
using static ThreeKingdom.Battle.BattleAiSettings;
using static ThreeKingdom.Battle.BattleBalanceSettings;

namespace ThreeKingdom.Battle;

// Pure decisions over facts supplied by the battle scene; no pathfinding or actions.
internal static class BattleAiSupplyPlanner
{
    internal static bool NeedsSupport(bool sameCart, bool siegeEngine, int hitPoints, int maxHitPoints,
        int wounded, int? morale, int? ammo, int? maxAmmo)
    {
        if (sameCart)
            return siegeEngine && hitPoints < maxHitPoints;

        return wounded > 0 || morale.HasValue && morale.Value < DefaultUnitMorale ||
               siegeEngine && hitPoints < maxHitPoints ||
               ammo.HasValue && maxAmmo.HasValue && ammo.Value < maxAmmo.Value;
    }

    internal static int MoraleScore(int morale, float multiplier) =>
        Mathf.RoundToInt(Mathf.Max(0, DefaultUnitMorale - morale) * 10 * multiplier);

    internal static int RecoveryScore(int wounded, float multiplier) =>
        Mathf.RoundToInt(Mathf.Min(SupplyCartWoundedRecoveryAmount, wounded) * 2 * multiplier);

    internal static int RepairScore(int missingHitPoints, float multiplier) =>
        Mathf.RoundToInt(Mathf.Min(SupplyCartRepairAmount, missingHitPoints) * 2 * multiplier);

    internal static int WeaponScore(int currentAmmo, int maxAmmo, int attackDamage, bool catapult, int intelligence)
    {
        if (maxAmmo <= 0 || currentAmmo >= maxAmmo)
            return 0;

        var score = attackDamage * (maxAmmo - currentAmmo) / maxAmmo;
        if (currentAmmo == 0)
            score += AiAmmoDepletedResupplyBonus;
        if (catapult)
            score += AiCatapultAmmoResupplyBonus + intelligence * 4;
        return score;
    }

    // Index zero is the current position. Remaining positions have already
    // passed the scene's reachability and safety checks.
    internal static List<(int Index, bool Weapon, int Score)> BuildPlans(
        IReadOnlyList<(int Recovery, int Weapon, int Distance)> positions)
    {
        var plans = new List<(int Index, bool Weapon, int Score)>();
        for (var i = 0; i < positions.Count; i++)
        {
            var candidate = positions[i];
            var cost = i == 0 ? 0 : candidate.Distance * 100;
            if (candidate.Recovery > 0)
                plans.Add((i, false, candidate.Recovery - cost));
            if (candidate.Weapon > 0)
                plans.Add((i, true, candidate.Weapon - cost));
        }
        return plans;
    }

    // Scene supplies only paths that A* found. Preserve original stable ties.
    internal static int SelectApproachIndex(IReadOnlyList<(int Score, int Energy, int Steps)> candidates)
    {
        var best = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (best < 0 || c.Score > candidates[best].Score ||
                c.Score == candidates[best].Score && (c.Energy < candidates[best].Energy ||
                c.Energy == candidates[best].Energy && c.Steps < candidates[best].Steps))
                best = i;
        }
        return best;
    }
}
