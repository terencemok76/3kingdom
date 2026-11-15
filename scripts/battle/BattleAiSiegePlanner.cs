using System.Collections.Generic;

namespace ThreeKingdom.Battle;

// Compares facts collected by the scene without accessing nodes or battle state.
internal static class BattleAiSiegePlanner
{
    internal static bool NeedsAlliedBreach(bool alliedTurn, bool anyOpenOrBrokenGate) =>
        alliedTurn && !anyOpenOrBrokenGate;

    internal static bool CanAttackGate(bool isRam, bool isUnit, bool outsideWall, bool alliedNeedsBreach,
        int turnsToBreach, int damage, int defenderRepair)
    {
        var alliedBreach = alliedNeedsBreach && isUnit && outsideWall;
        var finishingAttack = isUnit && turnsToBreach <= 2;
        var repairStalemate = turnsToBreach > 1 && defenderRepair >= damage;
        return (isRam || finishingAttack || alliedBreach) && (!repairStalemate || alliedBreach);
    }

    internal static bool CanApproachGate(bool isRam, bool isUnit, bool outsideWall,
        bool alliedNeedsBreach, bool canAttack, int damage) =>
        isRam || (alliedNeedsBreach && isUnit && outsideWall && canAttack && damage > 0);

    internal static int AttackScore(int turns, int damage, bool isRam, int threat, int intelligence) =>
        12000 - turns * 1200 + damage * 3 + (isRam ? 4000 : 0) +
        (turns == 1 ? 6000 : 0) - threat * (50 + intelligence) / 300;

    internal static int MoveScore(bool isRam, int support, int defenders, int energy, int steps,
        int threat, int intelligence) =>
        (isRam ? 4000 : 0) + support * 700 - defenders * (140 - intelligence / 2) -
        energy * 300 - steps * 40 - threat * (50 + intelligence) / 200;

    internal static int SelectAttackIndex(IReadOnlyList<(int Score, int Damage, int GateY, int GateX)> candidates)
    {
        var best = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (best < 0 || c.Score > candidates[best].Score ||
                (c.Score == candidates[best].Score && (c.Damage > candidates[best].Damage ||
                (c.Damage == candidates[best].Damage && (c.GateY < candidates[best].GateY ||
                (c.GateY == candidates[best].GateY && c.GateX < candidates[best].GateX))))))
                best = i;
        }
        return best;
    }

    internal static int SelectMoveIndex(IReadOnlyList<(int Score, int Energy, int Steps, int GateY, int GateX)> candidates)
    {
        var best = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (best < 0 || c.Score > candidates[best].Score ||
                (c.Score == candidates[best].Score && (c.Energy < candidates[best].Energy ||
                (c.Energy == candidates[best].Energy && (c.Steps < candidates[best].Steps ||
                (c.Steps == candidates[best].Steps && (c.GateY < candidates[best].GateY ||
                (c.GateY == candidates[best].GateY && c.GateX < candidates[best].GateX))))))))
                best = i;
        }
        return best;
    }
}
