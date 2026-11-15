using System;
using static ThreeKingdom.Battle.BattleAiSettings;

namespace ThreeKingdom.Battle;

// Scores engineering decisions from scene-provided facts without accessing nodes.
internal static class BattleAiEngineeringPlanner
{
    internal static (int Score, string Reason) BridgeRepair(int health, int maxHealth, int repair,
        bool heavilyDamaged, int nearbyFriendlyUnits)
    {
        var crossesThreshold = heavilyDamaged && (health + repair) * 2 > maxHealth;
        var critical = health * 4 <= maxHealth;
        var score = AiBridgeRepairBaseScore + repair * 2 +
                    (critical ? AiBridgeRepairCriticalScore : 0) +
                    (crossesThreshold ? AiBridgeRepairThresholdScore : 0) +
                    Math.Min(3, nearbyFriendlyUnits) * AiBridgeRepairNearbyFriendlyScore;
        var reason = crossesThreshold ? "restore bridge above heavy-damage threshold" :
            critical ? "prevent critically damaged bridge from collapsing" : "maintain damaged bridge route";
        return (score, reason);
    }

    internal static int GateRepairScore(int health, int maxHealth, int repair, int threat, bool worker) =>
        AiGateRepairBaseScore + repair * 2 + threat +
        (health * 2 <= maxHealth ? 800 : 0) + (worker ? AiGateRepairWorkerBonus : 0);

    internal static int RouteImprovement(int before, int after) =>
        before == int.MaxValue && after != int.MaxValue ? AiBridgeMinimumPathReduction + 3 :
        before == int.MaxValue || after == int.MaxValue ? 0 : before - after;

    internal static int RouteObstruction(int before, int after) =>
        before != int.MaxValue && after == int.MaxValue ? AiBridgeMinimumPathReduction + 3 :
        before == int.MaxValue || after == int.MaxValue ? 0 : after - before;

    internal static bool BlocksFriendlyRoute(int before, int after) =>
        before != int.MaxValue && (after == int.MaxValue || after > before + 2);

    internal static bool ShouldBuildBridge(int before, int after, int congestion)
    {
        if (after == int.MaxValue)
            return false;
        var relievesCongestion = congestion >= AiBridgeCongestionThreshold &&
                                 before != int.MaxValue && after <= before;
        return RouteImprovement(before, after) >= AiBridgeMinimumPathReduction || relievesCongestion;
    }

    internal static int BridgeConstructionScore(int reduction, int congestion, int segments, int distance,
        int intelligence, int threat) =>
        AiBridgeConstructionBaseScore + reduction * AiBridgePathReductionScore - congestion * AiBridgeCongestionScore -
        (segments - 1) * AiBridgeSegmentPenalty - distance * AiBridgeApproachPenalty + intelligence * 8 - threat / 2;

    internal static int FenceRemovalScore(int routeImpact, int distance, int energyCost) =>
        AiFenceRemovalBaseScore + routeImpact * AiFencePathImpactScore - distance * AiBridgeApproachPenalty - energyCost * 20;

    internal static int FenceConstructionScore(int defenseImpact, bool support, int enemyDistance,
        int intelligence, int distance, int energyCost) =>
        AiFenceConstructionBaseScore + defenseImpact * AiFencePathImpactScore + (support ? AiFenceSupportScore : 0) +
        (5 - enemyDistance) * 120 + intelligence * 5 - distance * AiBridgeApproachPenalty - energyCost * 20;
}
