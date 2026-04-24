using System.Collections.Generic;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class MonthlyEconomyResult
{
    // Keep both world totals and player-city breakdown so HUD can choose the right summary.
    public int AnnualGoldCollected { get; set; }
    public int AnnualFoodCollected { get; set; }
    public List<(int CityId, int Amount)> PlayerCityGoldIncome { get; } = new();
    public List<(int CityId, int Amount)> PlayerCityFoodIncome { get; } = new();
}

public class TurnManager
{
    private const int MonthlyUpkeepDivisor = 40;

    public WorldState? World { get; private set; }
    public int ActiveFactionId { get; private set; }

    public void Initialize(WorldState world)
    {
        World = world;
        ActiveFactionId = GetPlayerFactionId();
    }

    public int GetPlayerFactionId()
    {
        if (World == null)
        {
            return -1;
        }

        foreach (var faction in World.Factions)
        {
            if (faction.IsPlayer)
            {
                return faction.Id;
            }
        }

        return -1;
    }

    public List<CommandResult> ResolvePendingCommands(CommandResolver resolver)
    {
        var results = new List<CommandResult>();
        if (World == null)
        {
            return results;
        }

        ResolvePendingCommandsOfType(resolver, CommandType.Develop, results);
        ResolvePendingCommandsOfType(resolver, CommandType.Recruit, results);
        ResolvePendingCommandsOfType(resolver, CommandType.Move, results);
        ResolvePendingCommandsOfType(resolver, CommandType.Attack, results);
        World.PendingCommands.Clear();
        return results;
    }

    public MonthlyEconomyResult ApplyMonthlyEconomy()
    {
        var result = new MonthlyEconomyResult();
        if (World == null)
        {
            return result;
        }

        var playerFactionId = GetPlayerFactionId();

        foreach (var city in World.Cities)
        {
            var loyaltyFactor = 0.8f + city.Loyalty / 200.0f;
            var goldIncome = (int)((30 + city.Commercial * 2.0f) * loyaltyFactor);
            var foodIncome = (int)((40 + city.Farm * 3.0f) * loyaltyFactor);

            // Seasonal income is paid when the game enters April/August, not after those months end.
            if (World.Month == 4)
            {
                var annualGold = goldIncome * 12;
                city.Gold += annualGold;
                result.AnnualGoldCollected += annualGold;
                if (city.OwnerFactionId == playerFactionId)
                {
                    result.PlayerCityGoldIncome.Add((city.Id, annualGold));
                }
            }

            if (World.Month == 8)
            {
                var annualFood = foodIncome * 12;
                city.Food += annualFood;
                result.AnnualFoodCollected += annualFood;
                if (city.OwnerFactionId == playerFactionId)
                {
                    result.PlayerCityFoodIncome.Add((city.Id, annualFood));
                }
            }

            var upkeep = city.Troops / MonthlyUpkeepDivisor;
            city.Food -= upkeep;

            if (city.Food < 0)
            {
                // Resolve shortage immediately here so later systems do not see negative food carry-over.
                var shortage = -city.Food;
                var deserters = shortage * 2;
                if (deserters > city.Troops)
                {
                    deserters = city.Troops;
                }

                city.Troops -= deserters;
                city.Food = 0;
                city.Loyalty = city.Loyalty > 2 ? city.Loyalty - 2 : 0;
            }
        }

        return result;
    }

    public void AdvanceMonth()
    {
        if (World == null)
        {
            return;
        }

        World.Month += 1;
        if (World.Month > 12)
        {
            World.Month = 1;
            World.Year += 1;
        }
    }

    private void ResolvePendingCommandsOfType(
        CommandResolver resolver,
        CommandType commandType,
        List<CommandResult> results)
    {
        if (World == null)
        {
            return;
        }

        foreach (var pendingCommand in World.PendingCommands)
        {
            if (pendingCommand.Type != commandType)
            {
                continue;
            }

            results.Add(resolver.ResolvePendingCommand(pendingCommand));
        }
    }
}
