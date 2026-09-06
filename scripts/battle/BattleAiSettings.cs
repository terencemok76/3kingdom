namespace ThreeKingdom.Battle;

internal static class BattleAiSettings
{
    internal const int AiOutpostAttackScoreBonus = 600;
    internal const int AiOutpostCaptureObjectiveScore = 900;
    internal const int AiOutpostLastCaptureObjectiveBonus = 4000;
    internal const int AiOutpostRecaptureObjectiveScore = 3500;
    internal const int AiOutpostThreatObjectiveScore = 800;
    internal const int AiOutpostThreatRange = 4;
    internal const float AiVulnerableHealthRatio = 0.45f;
    internal const float AiCriticalHealthRatio = 0.22f;
    internal const int AiBuildingCoverSurvivalScore = 1200;
    internal const int AiSupplyApproachSurvivalScore = 1000;
    internal const int AiAmmoDepletedResupplyBonus = 1200;
    internal const int AiCatapultAmmoResupplyBonus = 800;
    internal const int AiGuardSurvivalScore = 900;
    internal const int AiRetreatSurvivalScore = 1800;
    internal const int AiBridgeMinimumPathReduction = 3;
    internal const int AiBridgePathReductionScore = 550;
    internal const int AiBridgeConstructionBaseScore = 450;
    internal const int AiBridgeSegmentPenalty = 220;
    internal const int AiBridgeApproachPenalty = 100;
    internal const int AiBridgeRepairBaseScore = 400;
    internal const int AiBridgeRepairCriticalScore = 600;
    internal const int AiBridgeRepairThresholdScore = 500;
    internal const int AiBridgeRepairNearbyFriendlyScore = 100;
    internal const int AiFenceConstructionBaseScore = 700;
    internal const int AiFenceRemovalBaseScore = 900;
    internal const int AiFencePathImpactScore = 500;
    internal const int AiFenceSupportScore = 250;
    internal const int AiHideAmbushBaseScore = 700;
    internal const int AiHideAmbushRange = 4;
    internal const int AiHiddenAmbushAttackScoreBonus = 900;
    internal const int AiLowEnemyFoodPressureScore = 600;
    internal const int AiCriticalEnemyFoodPressureScore = 1200;
    // A wall-top unit must render after the NE wall segments that overlap it from the right.
}
