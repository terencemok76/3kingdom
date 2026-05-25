using System;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class RecruitRules
{
    private const int CostScale = 5;
    private const int PopulationPerRecruit = 20;

    internal static int GetRecruitGoldCost(TroopType troopType, int troopCount)
    {
        return DivideRoundUp(Math.Max(0, troopCount) * GetGoldCostNumerator(troopType), CostScale);
    }

    internal static int GetRecruitFoodCost(TroopType troopType, int troopCount)
    {
        return DivideRoundUp(Math.Max(0, troopCount) * GetFoodCostNumerator(troopType), CostScale);
    }

    internal static int GetMaxRecruitableCount(CityData city, TroopType troopType)
    {
        if (!CanRecruitTroopType(city, troopType))
        {
            return 0;
        }

        var maxByGold = city.Gold <= 0 ? 0 : city.Gold * CostScale / GetGoldCostNumerator(troopType);
        var maxByFood = city.Food <= 0 ? 0 : city.Food * CostScale / GetFoodCostNumerator(troopType);
        var maxByPopulation = city.Population <= 0 ? 0 : city.Population / PopulationPerRecruit;
        var maxBySpecial = troopType == TroopType.Cavalry ? Math.Max(0, city.Horses) : int.MaxValue;
        return Math.Max(0, Math.Min(Math.Min(maxByGold, maxByFood), Math.Min(maxByPopulation, maxBySpecial)));
    }

    internal static bool CanRecruitTroopType(CityData city, TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Cavalry => city.Horses > 0,
            TroopType.Crossbow => city.BowWorkshopLevel >= 1,
            TroopType.Siege => city.SiegeWorkshopLevel >= 1,
            _ => true
        };
    }

    internal static int CalculateRecruitResult(int requestedCount, int charm, int loyalty, TroopType troopType, Random random)
    {
        if (requestedCount <= 0)
        {
            return 0;
        }

        var charmBonus = Math.Clamp((charm - 50) / 200.0f, -0.10f, 0.25f);
        var loyaltyBonus = Math.Clamp((loyalty - 50) / 250.0f, -0.10f, 0.18f);
        var troopTypeModifier = troopType switch
        {
            TroopType.Infantry => 0.08f,
            TroopType.Spearman => 0.06f,
            TroopType.Archer => 0.04f,
            TroopType.Crossbow => -0.04f,
            TroopType.Cavalry => -0.08f,
            TroopType.Siege => -0.12f,
            _ => 0.0f
        };
        var randomModifier = (float)(random.NextDouble() * 0.18 - 0.09);
        var ratio = Math.Clamp(0.72f + charmBonus + loyaltyBonus + troopTypeModifier + randomModifier, 0.45f, 1.0f);
        return Math.Clamp((int)Math.Round(requestedCount * ratio), 1, requestedCount);
    }

    internal static int GetRecruitLoyaltyPenalty(int recruitedCount)
    {
        return Math.Max(1, DivideRoundUp(Math.Max(0, recruitedCount), 100));
    }

    private static int GetGoldCostNumerator(TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Cavalry => 8,
            TroopType.Crossbow => 7,
            TroopType.Siege => 10,
            _ => 6
        };
    }

    private static int GetFoodCostNumerator(TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Cavalry => 6,
            TroopType.Siege => 7,
            _ => 4
        };
    }

    private static int DivideRoundUp(int value, int divisor)
    {
        return value <= 0 ? 0 : (value + divisor - 1) / divisor;
    }
}
