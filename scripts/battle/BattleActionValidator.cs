namespace ThreeKingdom.Battle;

/// <summary>
/// Validates action-intent structure before scene-specific battle rules run.
/// Terrain, pathfinding, targets, and resources remain owned by the shared
/// battle rules invoked by BattleSceneController.
/// </summary>
internal static class BattleActionValidator
{
    internal static bool RequiresDistinctTarget(BattleActionKind kind)
    {
        return kind is BattleActionKind.Move or
            BattleActionKind.Attack or
            BattleActionKind.Work or
            BattleActionKind.Extinguish or
            BattleActionKind.FireStrategy or
            BattleActionKind.MentalStrategy or
            BattleActionKind.Charge or
            BattleActionKind.Duel or
            BattleActionKind.HireOfficer;
    }

    internal static bool IsStructurallyValid(BattleActionIntent intent)
    {
        if (intent.ReservedEnergy < 0)
        {
            return false;
        }

        var requiresDistinctTarget = RequiresDistinctTarget(intent.Kind);
        if (requiresDistinctTarget == (intent.SourceGrid == intent.TargetGrid))
        {
            return false;
        }

        if (intent.Kind != BattleActionKind.Move &&
            (intent.ReservedEnergy != 0 || !intent.MarkActedAfterMove))
        {
            return false;
        }

        return intent.Kind == BattleActionKind.Work || !intent.UseWoodFenceWork;
    }
}
