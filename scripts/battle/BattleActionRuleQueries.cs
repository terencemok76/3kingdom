using System;
using System.Collections.Generic;

namespace ThreeKingdom.Battle;

/// <summary>
/// Routes each action kind to the shared battle-rule query supplied by the
/// scene. This keeps rule selection separate from action execution.
/// </summary>
internal sealed class BattleActionRuleQueries<TUnit>
{
    private readonly IReadOnlyDictionary<BattleActionKind, Func<BattleActionIntent, TUnit, bool>> _rules;

    internal BattleActionRuleQueries(IReadOnlyDictionary<BattleActionKind, Func<BattleActionIntent, TUnit, bool>> rules)
    {
        _rules = rules;
    }

    internal bool IsLegal(BattleActionIntent intent, TUnit unit)
    {
        return _rules.TryGetValue(intent.Kind, out var rule) && rule(intent, unit);
    }
}
