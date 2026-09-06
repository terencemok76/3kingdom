using System;
using System.Collections.Generic;

namespace ThreeKingdom.Battle;

/// <summary>
/// Dispatches a validated battle action to its scene adapter. Player input and
/// AI planning share this dispatcher, while scene mutations remain in the
/// BattleSceneController action partial.
/// </summary>
internal sealed class BattleActionExecutor<TUnit>
{
    private readonly IReadOnlyDictionary<BattleActionKind, Func<BattleActionIntent, TUnit, Action?, bool>> _handlers;

    internal BattleActionExecutor(
        IReadOnlyDictionary<BattleActionKind, Func<BattleActionIntent, TUnit, Action?, bool>> handlers)
    {
        _handlers = handlers;
    }

    internal bool TryExecute(BattleActionIntent intent, TUnit unit, Action? onMoveAnimationComplete = null)
    {
        return _handlers.TryGetValue(intent.Kind, out var handler) &&
               handler(intent, unit, onMoveAnimationComplete);
    }
}
