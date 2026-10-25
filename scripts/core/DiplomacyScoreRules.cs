using System;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class DiplomacyScoreRules
{
    public static int GetTrustScore(DiplomacyRelationData? relation, int observerFactionId, int otherFactionId)
    {
        if (relation == null || observerFactionId <= 0 || otherFactionId <= 0)
        {
            return 50;
        }

        return relation.FactionAId == observerFactionId
            ? relation.TrustFromAToB
            : relation.TrustFromBToA;
    }

    public static int GetThreatScore(DiplomacyRelationData? relation, int observerFactionId, int otherFactionId)
    {
        if (relation == null || observerFactionId <= 0 || otherFactionId <= 0)
        {
            return 0;
        }

        return relation.FactionAId == observerFactionId
            ? relation.ThreatFromAToB
            : relation.ThreatFromBToA;
    }

    public static void AdjustTrust(DiplomacyRelationData relation, int observerFactionId, int amount)
    {
        if (relation.FactionAId == observerFactionId)
        {
            relation.TrustFromAToB = Math.Clamp(relation.TrustFromAToB + amount, 0, 100);
        }
        else if (relation.FactionBId == observerFactionId)
        {
            relation.TrustFromBToA = Math.Clamp(relation.TrustFromBToA + amount, 0, 100);
        }
    }

    public static void RefreshThreatScores(WorldState world)
    {
        var activeFactionIds = world.Factions
            .Where(faction => world.Cities.Any(city => city.OwnerFactionId == faction.Id))
            .Select(faction => faction.Id)
            .OrderBy(id => id)
            .ToList();

        for (var left = 0; left < activeFactionIds.Count; left += 1)
        {
            for (var right = left + 1; right < activeFactionIds.Count; right += 1)
            {
                var relation = GetOrCreate(world, activeFactionIds[left], activeFactionIds[right]);
                relation.ThreatFromAToB = CalculateThreatScore(world, relation.FactionAId, relation.FactionBId, relation);
                relation.ThreatFromBToA = CalculateThreatScore(world, relation.FactionBId, relation.FactionAId, relation);
            }
        }
    }

    public static int CalculateThreatScore(WorldState world, int observerFactionId, int otherFactionId, DiplomacyRelationData? relation = null)
    {
        var observerCities = world.Cities.Where(city => city.OwnerFactionId == observerFactionId).ToList();
        var otherCities = world.Cities.Where(city => city.OwnerFactionId == otherFactionId).ToList();
        if (observerCities.Count == 0 || otherCities.Count == 0)
        {
            return 0;
        }

        var observerTroops = observerCities.Sum(city => Math.Max(0, city.Troops));
        var otherTroops = otherCities.Sum(city => Math.Max(0, city.Troops));
        var borderLinks = 0;
        var frontierPressure = 0;
        foreach (var city in observerCities)
        {
            foreach (var connectedId in city.ConnectedCityIds)
            {
                var neighbor = world.GetCity(connectedId);
                if (neighbor?.OwnerFactionId != otherFactionId)
                {
                    continue;
                }

                borderLinks += 1;
                frontierPressure += Math.Max(0, neighbor.Troops - city.Troops) / 120;
            }
        }

        var score = 12 + (otherTroops - observerTroops) / 120 + borderLinks * 7 + Math.Min(32, frontierPressure);
        if (borderLinks == 0)
        {
            score -= 10;
        }

        if (relation is { RemainingMonths: > 0, Status: DiplomacyStatusType.Alliance })
        {
            score -= 15;
        }
        else if (relation is { RemainingMonths: > 0, Status: DiplomacyStatusType.Truce })
        {
            score -= 8;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static DiplomacyRelationData GetOrCreate(WorldState world, int factionAId, int factionBId)
    {
        var low = Math.Min(factionAId, factionBId);
        var high = Math.Max(factionAId, factionBId);
        var relation = world.DiplomacyRelations.FirstOrDefault(item => item.FactionAId == low && item.FactionBId == high);
        if (relation != null)
        {
            return relation;
        }

        relation = new DiplomacyRelationData { FactionAId = low, FactionBId = high };
        world.DiplomacyRelations.Add(relation);
        return relation;
    }
}
