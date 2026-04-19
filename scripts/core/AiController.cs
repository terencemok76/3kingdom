using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class AiController
{
    private CommandResolver? _commandResolver;
    private TurnManager? _turnManager;

    public void Initialize(CommandResolver commandResolver, TurnManager turnManager)
    {
        _commandResolver = commandResolver;
        _turnManager = turnManager;
    }

    public CommandResult RunSingleCityDecision(int factionId, int cityId)
    {
        if (_commandResolver == null || _turnManager?.World == null)
        {
            return new CommandResult { Success = false, Message = "AI not initialized." };
        }

        var world = _turnManager.World;
        var city = world.GetCity(cityId);
        if (city == null)
        {
            return new CommandResult { Success = false, Message = "AI city not found." };
        }

        CommandResult? militaryResult = null;
        foreach (var targetId in city.ConnectedCityIds)
        {
            var target = world.GetCity(targetId);
            if (target == null)
            {
                continue;
            }

            if (target.OwnerFactionId == factionId)
            {
                continue;
            }

            if (city.Troops > target.Troops + 300)
            {
                militaryResult = _commandResolver.Execute(new CommandRequest
                {
                    Type = CommandType.Attack,
                    ActorFactionId = factionId,
                    SourceCityId = cityId,
                    TargetCityId = targetId,
                    TroopsToSend = city.Troops / 2
                });
                break;
            }
        }

        if (militaryResult == null)
        {
            foreach (var targetId in city.ConnectedCityIds)
            {
                var target = world.GetCity(targetId);
                if (target == null || target.OwnerFactionId != factionId)
                {
                    continue;
                }

                if (city.Troops > target.Troops + 800)
                {
                    militaryResult = _commandResolver.Execute(new CommandRequest
                    {
                        Type = CommandType.Move,
                        ActorFactionId = factionId,
                        SourceCityId = cityId,
                        TargetCityId = targetId,
                        TroopsToSend = city.Troops / 2,
                        GoldToSend = city.Gold / 3,
                        FoodToSend = city.Food / 3
                    });
                    break;
                }
            }
        }

        CommandResult coreResult;
        if (city.Troops < 2200 && city.Gold >= 120 && city.Food >= 80)
        {
            coreResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Recruit,
                ActorFactionId = factionId,
                SourceCityId = cityId
            });
        }
        else if (city.Gold >= 100)
        {
            coreResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Develop,
                ActorFactionId = factionId,
                SourceCityId = cityId
            });
        }
        else if (city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month)
        {
            coreResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Pass,
                ActorFactionId = factionId,
                SourceCityId = cityId
            });
        }
        else
        {
            coreResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Search,
                ActorFactionId = factionId,
                SourceCityId = cityId
            });
        }

        if (militaryResult == null)
        {
            return coreResult;
        }

        var messages = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(militaryResult.Message))
        {
            messages.Add(militaryResult.Message);
        }

        if (!string.IsNullOrWhiteSpace(coreResult.Message) &&
            !coreResult.Message.Equals("Pass", System.StringComparison.OrdinalIgnoreCase))
        {
            messages.Add(coreResult.Message);
        }

        return new CommandResult
        {
            Success = militaryResult.Success || coreResult.Success,
            Message = messages.Count > 0 ? string.Join(" | ", messages) : "Pass"
        };
    }
}
