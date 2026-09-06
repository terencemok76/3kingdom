namespace ThreeKingdom.Battle;

/// <summary>
/// Pure scoring helpers for field-battle AI.  This class deliberately has no
/// Godot node, scene, or mutable battle-state dependency so its arithmetic can
/// be exercised independently from animation and command execution.
/// </summary>
internal static class BattleAiScoring
{
    internal const int DecisiveActionScore = 5000;

    internal readonly record struct FinalOffensiveActionInput(
        int BaseScore,
        int Intelligence,
        int Combat,
        int EnemyFoodPressure,
        bool IsTacticalObjective,
        bool IsDirectAttack,
        bool GetsFoodPressureBonus,
        bool IsHiddenUnit,
        bool IsHideAction,
        bool TargetsSupplyCart,
        bool IsDecisiveAction,
        int HiddenAmbushAttackBonus);

    internal static int GetOffensiveActionScore(
        int damage,
        int targetHitPoints,
        bool targetsOfficerUnit,
        int supportCount,
        int supportEnergyCost,
        int defenseOutpostBonus)
    {
        var score = damage;
        if (damage >= targetHitPoints)
        {
            score += DecisiveActionScore;
        }

        if (targetsOfficerUnit)
        {
            score += 200;
        }

        return score - supportCount * supportEnergyCost * 20 + defenseOutpostBonus;
    }

    internal static int GetFinalOffensiveActionScore(FinalOffensiveActionInput input)
    {
        var ambushBonus = input.IsHiddenUnit && !input.IsTacticalObjective && !input.IsHideAction
            ? input.HiddenAmbushAttackBonus
            : 0;
        var foodPressureBonus = input.GetsFoodPressureBonus
            ? input.EnemyFoodPressure
            : 0;
        if (input.TargetsSupplyCart)
        {
            foodPressureBonus += input.EnemyFoodPressure * 2;
        }

        var foodPressurePenalty = input.EnemyFoodPressure > 0 &&
                                  !input.IsTacticalObjective &&
                                  !input.IsHideAction &&
                                  !input.IsDirectAttack &&
                                  !input.IsDecisiveAction &&
                                  foodPressureBonus == 0
            ? input.EnemyFoodPressure
            : 0;
        var officerWeight = input.IsTacticalObjective
            ? input.Intelligence * 6 + input.Combat * 2
            : input.Intelligence * 2 + input.Combat * 6;
        return input.BaseScore + ambushBonus + foodPressureBonus - foodPressurePenalty + officerWeight;
    }

    internal static int GetDecisionNoise(int turnNumber, BattleGridKey sourceGrid, BattleGridKey targetGrid, int participantCount)
    {
        var hash = 17;
        hash = hash * 31 + turnNumber;
        hash = hash * 31 + sourceGrid.X;
        hash = hash * 31 + sourceGrid.Y;
        hash = hash * 31 + sourceGrid.Level;
        hash = hash * 31 + targetGrid.X;
        hash = hash * 31 + targetGrid.Y;
        hash = hash * 31 + participantCount;
        return (hash & int.MaxValue) % 200;
    }
}
