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

        // 1) Try attack adjacent non-friendly city when troops are enough.
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
                return _commandResolver.Execute(new CommandRequest
                {
                    Type = CommandType.Attack,
                    ActorFactionId = factionId,
                    SourceCityId = cityId,
                    TargetCityId = targetId,
                    TroopsToSend = city.Troops / 2
                });
            }
        }

        // 2) Recruit when low on troops.
        if (city.Troops < 2200 && city.Gold >= 120 && city.Food >= 80)
        {
            return _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Recruit,
                ActorFactionId = factionId,
                SourceCityId = cityId
            });
        }

        // 3) Develop when enough gold.
        if (city.Gold >= 100)
        {
            return _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Develop,
                ActorFactionId = factionId,
                SourceCityId = cityId
            });
        }

        // 4) fallback search. If already searched this month, pass.
        if (city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month)
        {
            return _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Pass,
                ActorFactionId = factionId,
                SourceCityId = cityId
            });
        }

        return _commandResolver.Execute(new CommandRequest
        {
            Type = CommandType.Search,
            ActorFactionId = factionId,
            SourceCityId = cityId
        });
    }
}
