using System;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class OfficerRelationshipRules
{
    public static int GetSuccessionRelationshipPriority(OfficerData? first, OfficerData? second)
    {
        return GetRelationshipType(first, second) switch
        {
            "family,blood" => 2,
            "family,non-blood" or "family" => 1,
            _ => 0
        };
    }

    public static bool HasFamilyRelationship(OfficerData? first, OfficerData? second)
    {
        return GetSuccessionRelationshipPriority(first, second) > 0;
    }

    internal static string? GetRelationshipType(OfficerData? first, OfficerData? second)
    {
        return FindRelationshipType(first, second) ?? FindRelationshipType(second, first);
    }

    private static string? FindRelationshipType(OfficerData? source, OfficerData? target)
    {
        if (source?.RelationshipType == null || target == null)
        {
            return null;
        }

        return source.RelationshipType
            .FirstOrDefault(relationship =>
            (relationship.Key.Equals(target.Name, StringComparison.OrdinalIgnoreCase) ||
             relationship.Key.Equals(target.NameZhHant, StringComparison.OrdinalIgnoreCase)))
            .Value?
            .Trim()
            .ToLowerInvariant();
    }
}
