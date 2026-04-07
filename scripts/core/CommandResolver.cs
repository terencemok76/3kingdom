using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class CommandResolver
{
    private TurnManager? _turnManager;
    private CombatResolver? _combatResolver;

    public void Initialize(TurnManager turnManager, CombatResolver combatResolver)
    {
        _turnManager = turnManager;
        _combatResolver = combatResolver;
    }

    public CommandResult Execute(CommandRequest request)
    {
        if (_turnManager?.World == null)
        {
            return new CommandResult { Success = false, Message = "World not initialized." };
        }

        return new CommandResult
        {
            Success = true,
            Message = $"Command queued: {request.Type} (Phase 1 skeleton)."
        };
    }
}
