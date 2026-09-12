namespace ThreeKingdom.Battle;

internal enum BattleActionKind
{
    Move,
    Attack,
    Supply,
    ResupplyWeapon,
    ToggleGate,
    Guard,
    Hide,
    Work,
    Retreat,
    Extinguish,
    FireStrategy,
    MentalStrategy,
    Charge,
    Duel,
    HireOfficer
}

/// <summary>
/// A controller-neutral request to perform one battle action. Both player input
/// and AI planning use this shape before the scene executes an action.
/// </summary>
internal readonly record struct BattleActionIntent(
    BattleActionKind Kind,
    BattleGridKey SourceGrid,
    BattleGridKey TargetGrid,
    int ReservedEnergy = 0,
    // Movement spends only its path energy and cumulative MoveRange.  It does not
    // complete the team's turn unless a caller explicitly requests that behavior.
    bool MarkActedAfterMove = false,
    bool UseWoodFenceWork = false);
