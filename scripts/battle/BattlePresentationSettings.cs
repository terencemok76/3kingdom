using Godot;

namespace ThreeKingdom.Battle;

internal static class BattlePresentationSettings
{
    internal const float MapPaddingLeft = 220.0f;
    internal const float MapPaddingTop = 220.0f;
    internal const float MapPaddingRight = 220.0f;
    internal const float MapPaddingBottom = 320.0f;
    internal const double OfficerSpeechDurationSeconds = 4.0;
    internal const double TurnBannerDurationSeconds = 2.5;
    internal const ulong OfficerSpeechCooldownMilliseconds = 20000;
    internal const float MinimumBattleCameraZoom = 0.75f;
    internal const float MaximumBattleCameraZoom = 1.35f;
    internal const float BattleCameraZoomStep = 0.10f;
    internal const float BattleLogMinimumWidth = 240.0f;
    internal const float BattleLogMinimumHeight = 150.0f;
    internal const float DefaultUnitVisualLift = -16.0f;
    internal const float WallWalkUnitVisualLift = -58.0f;
    internal const float WallWalkHighlightVisualLift = -42.0f;
    internal static readonly Vector2 NorthEastWallTopHighlightOffset = new(60.0f, -86.0f);
    internal static readonly Vector2 NorthWestWallTopHighlightOffset = new(-60.0f, -86.0f);
    internal const int WallTopLevelDepthOffset = 3;
    internal const double InfantryMoveAnimationDurationSeconds = 0.4;
    internal const double SpearmanMoveAnimationDurationSeconds = 0.4;
    internal const double ArcherMoveAnimationDurationSeconds = 0.4;
    internal const double CavalryMoveAnimationDurationSeconds = 0.4;
    internal const double InfantryAttackAnimationDurationSeconds = 0.62;
    internal const double SpearmanAttackAnimationDurationSeconds = 0.72;
    internal const double ArcherAttackAnimationDurationSeconds = 0.62;
    internal const double CavalryAttackAnimationDurationSeconds = 0.5;
    internal const double WorkerAttackAnimationDurationSeconds = 0.75;
    internal const double WorkerWorkAnimationDurationSeconds = 0.8;
    internal const double CatapultAttackAnimationDurationSeconds = 0.72;
    internal const double InfantryHurtAnimationDurationSeconds = 0.5;
    internal const double SpearmanHurtAnimationDurationSeconds = 0.65;
    internal const double ArcherHurtAnimationDurationSeconds = 0.5;
    internal const double CavalryHurtAnimationDurationSeconds = 0.5;
    internal const double WorkerHurtAnimationDurationSeconds = 0.75;
    internal const double CarMoveAnimationDurationSeconds = 0.4;
    internal const double CatapultMoveAnimationDurationSeconds = 0.4;
    internal const double DropStoneEffectDurationSeconds = 0.48;
    internal const double PourOilEffectDurationSeconds = 0.58;
    internal const double ArrowProjectileEffectDurationSeconds = 0.42;
    internal const double CatapultProjectileEffectDurationSeconds = 0.7;
    internal const double HireOfficerEffectDurationSeconds = 1.35;
    internal const double HireOfficerPopupDelaySeconds = 0.18;
    internal const double HireOfficerMoralePopupDelaySeconds = 0.58;
    internal const double DamagePopupDurationSeconds = 4.0;
    internal const double MoralePopupDurationSeconds = 4.0;
}
