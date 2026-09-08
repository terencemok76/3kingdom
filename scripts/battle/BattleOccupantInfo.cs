using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Battle;

internal sealed record BattleOccupantInfo(
    string DisplayName,
    string Category,
    string ShortLabel,
    string TeamName,
    string OfficerName,
    string TroopType,
    int TroopCount,
    int HitPoints,
    int MaxHitPoints,
    int WoundedTroops,
    int MessTurns,
    bool IsHidden,
    int? Morale,
    int? WeaponAmmo,
    int? MaxWeaponAmmo,
    int MoveRange,
    int AttackRange,
    BattlePieceMarker? Marker,
    BattleSpriteDirection FacingDirection,
    int Energy,
    bool HasAttackedThisTurn,
    int RemainingMoveRange,
    bool IsGuarding,
    bool GuardCounterAvailable,
    int GuardDamageReductionCount,
    int CampaignTeamId = 0,
    int FactionId = 0,
    CampaignControllerType ControllerType = CampaignControllerType.Ai);
