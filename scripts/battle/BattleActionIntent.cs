namespace ThreeKingdom.Battle;

internal enum BattleActionKind
{
    Move,
    Attack,
    Supply,
    ResupplyWeapon,
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
    bool MarkActedAfterMove = true,
    bool UseWoodFenceWork = false);
