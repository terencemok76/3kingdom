using System;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class CommandDiplomacyRules
{
    internal static DiplomacyRelationData GetOrCreateDiplomacyRelation(WorldState world, int factionAId, int factionBId)
    {
        var low = Math.Min(factionAId, factionBId);
        var high = Math.Max(factionAId, factionBId);
        var existing = world.DiplomacyRelations.FirstOrDefault(relation =>
            relation.FactionAId == low &&
            relation.FactionBId == high);
        if (existing != null)
        {
            return existing;
        }

        var relation = new DiplomacyRelationData
        {
            FactionAId = low,
            FactionBId = high
        };
        world.DiplomacyRelations.Add(relation);
        return relation;
    }

    internal static DiplomacyRelationData? FindDiplomacyRelation(WorldState world, int factionAId, int factionBId)
    {
        var low = Math.Min(factionAId, factionBId);
        var high = Math.Max(factionAId, factionBId);
        return world.DiplomacyRelations.FirstOrDefault(relation =>
            relation.FactionAId == low &&
            relation.FactionBId == high);
    }

    internal static bool HasActiveDiplomacyBlock(WorldState world, int factionAId, int factionBId)
    {
        var relation = FindDiplomacyRelation(world, factionAId, factionBId);
        return relation != null &&
               relation.Status is DiplomacyStatusType.Truce or DiplomacyStatusType.Alliance &&
               relation.RemainingMonths > 0;
    }

    internal static bool TryBreakDiplomacyBlockForAttack(WorldState world, int attackerFactionId, int defenderFactionId)
    {
        var relation = FindDiplomacyRelation(world, attackerFactionId, defenderFactionId);
        if (relation == null ||
            relation.Status is not (DiplomacyStatusType.Truce or DiplomacyStatusType.Alliance) ||
            relation.RemainingMonths <= 0)
        {
            return false;
        }

        relation.Status = DiplomacyStatusType.Neutral;
        relation.RemainingMonths = 0;
        relation.RelationScore = Math.Clamp(relation.RelationScore - 18, -100, 100);
        DiplomacyScoreRules.AdjustTrust(relation, defenderFactionId, -35);
        relation.LastUpdatedYear = world.Year;
        relation.LastUpdatedMonth = world.Month;
        return true;
    }

}
