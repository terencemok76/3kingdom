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
