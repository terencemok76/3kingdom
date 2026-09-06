using System;
using static ThreeKingdom.Battle.BattleBalanceSettings;

namespace ThreeKingdom.Battle;

internal sealed class BattleTeamState
{
    internal BattleTeamState(int initialGold, int initialFood)
    {
        StrategyPlans = InitialTeamStrategyPlans;
        Gold = initialGold;
        Food = initialFood;
    }

    internal int TotalTroops { get; set; }
    internal int SiegeUnits { get; set; }
    internal int Generals { get; set; }
    internal int StrategyPlans { get; set; }
    internal int Gold { get; set; }
    internal int Food { get; set; }
    internal int ZeroFoodDays { get; set; }
}

internal static class BattleTeamIdentity
{
    internal const string AttackerName = "Team A / Attacker";
    internal const string DefenderName = "Team B / Defender";

    internal static string GetName(BattleTurnSide side) => side switch
    {
        BattleTurnSide.TeamA => AttackerName,
        BattleTurnSide.TeamB => DefenderName,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown battle side.")
    };

    internal static string GetOpponentName(string teamName) =>
        GetName(GetOpponentSide(ResolveSide(teamName)));

    internal static BattleTurnSide GetOpponentSide(BattleTurnSide side) => side switch
    {
        BattleTurnSide.TeamA => BattleTurnSide.TeamB,
        BattleTurnSide.TeamB => BattleTurnSide.TeamA,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown battle side.")
    };

    internal static BattleTurnSide ResolveSide(string teamName)
    {
        if (TryResolveSide(teamName, out var side))
        {
            return side;
        }

        throw new ArgumentException($"Unknown battle team name: '{teamName}'.", nameof(teamName));
    }

    internal static bool TryResolveSide(string teamName, out BattleTurnSide side)
    {
        if (string.Equals(teamName, AttackerName, StringComparison.OrdinalIgnoreCase))
        {
            side = BattleTurnSide.TeamA;
            return true;
        }

        if (string.Equals(teamName, DefenderName, StringComparison.OrdinalIgnoreCase))
        {
            side = BattleTurnSide.TeamB;
            return true;
        }

        side = default;
        return false;
    }

    internal static bool IsAttacker(string teamName) =>
        TryResolveSide(teamName, out var side) && side == BattleTurnSide.TeamA;

    internal static bool IsDefender(string teamName) =>
        TryResolveSide(teamName, out var side) && side == BattleTurnSide.TeamB;
}
