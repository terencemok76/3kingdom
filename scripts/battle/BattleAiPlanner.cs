using System;
using System.Collections.Generic;

namespace ThreeKingdom.Battle;

/// <summary>
/// Stateless AI planning decisions. Scene code supplies battle facts; this
/// class compares those facts without selecting nodes or executing commands.
/// </summary>
internal static class BattleAiPlanner
{
    internal static bool TryExecuteFirstAction(params Func<bool>[] stages)
    {
        foreach (var stage in stages)
        {
            if (stage())
                return true;
        }
        return false;
    }

    // Equivalent scores retain the first candidate, matching stable LINQ ordering.
    internal static int SelectOffensiveActionIndex(IReadOnlyList<(int Final, int Base, int Noise)> candidates)
    {
        var best = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (best < 0 || candidate.Final + candidate.Noise > candidates[best].Final + candidates[best].Noise ||
                (candidate.Final + candidate.Noise == candidates[best].Final + candidates[best].Noise &&
                 (candidate.Final > candidates[best].Final ||
                  (candidate.Final == candidates[best].Final &&
                   (candidate.Base > candidates[best].Base ||
                    (candidate.Base == candidates[best].Base && candidate.Noise > candidates[best].Noise))))))
                best = i;
        }
        return best;
    }

    internal readonly record struct SafetyMoveCandidate<TGrid>(
        TGrid Destination,
        int Threat,
        bool HasCover,
        int SupplyDistance,
        int DistanceFromSource);

    internal readonly record struct SafetyMovePlan<TGrid>(TGrid Destination, int Score, string Reason);

    internal static SafetyMovePlan<TGrid>? GetBestSafetyMove<TGrid>(
        IEnumerable<SafetyMoveCandidate<TGrid>> candidates,
        int intelligence,
        int enemyFoodPressure,
        int buildingCoverScore,
        int supplyApproachScore)
    {
        SafetyMovePlan<TGrid>? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var score = intelligence * 10 - candidate.Threat;
            var reason = string.Empty;
            if (candidate.HasCover)
            {
                score += buildingCoverScore + enemyFoodPressure;
                reason = "move to Building/Fortress cover";
            }

            if (candidate.SupplyDistance <= 1)
            {
                score += supplyApproachScore;
                reason = string.IsNullOrEmpty(reason) ? "move next to supply cart" : reason + " and supply cart";
            }

            if (string.IsNullOrEmpty(reason) ||
                best.HasValue && (score < best.Value.Score || score == best.Value.Score && candidate.DistanceFromSource >= bestDistance))
            {
                continue;
            }

            best = new SafetyMovePlan<TGrid>(candidate.Destination, score, reason);
            bestDistance = candidate.DistanceFromSource;
        }

        return best;
    }
}
