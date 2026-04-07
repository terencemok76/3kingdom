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

        var request = new CommandRequest
        {
            Type = CommandType.Pass,
            ActorFactionId = factionId,
            SourceCityId = cityId
        };

        return _commandResolver.Execute(request);
    }
}
