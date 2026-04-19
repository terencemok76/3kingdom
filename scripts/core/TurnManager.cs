using System.Collections.Generic;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class TurnManager
{
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

    public void ApplyMonthlyEconomy()
    {
        if (World == null)
        {
            return;
        }

        foreach (var city in World.Cities)
        {
            var loyaltyFactor = 0.8f + city.Loyalty / 200.0f;
            var goldIncome = (int)((30 + city.Commercial * 2.0f) * loyaltyFactor);
            var foodIncome = (int)((40 + city.Farm * 3.0f) * loyaltyFactor);

            city.Gold += goldIncome;
            city.Food += foodIncome;

            var upkeep = city.Troops / 20;
            city.Food -= upkeep;

            if (city.Food < 0)
            {
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
