using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class CampaignRetreatService
{
    /// <summary>
    /// Returns direct, strategic destinations for an officer who has reached its
    /// own battle exit. In a Field Battle, a defender may first fall back into
    /// the defended target city for a subsequent city battle. A team's valid
    /// origin city and adjacent friendly or neutral cities follow.
    /// </summary>
    public static IReadOnlyList<CityData> GetRetreatDestinations(
        WorldState world,
        ActiveBattleCampaignData campaign,
        CampaignBattleTeamData team)
    {
        var anchorCityId = team.Side == CampaignBattleSide.Defender
            ? campaign.TargetCityId
            : campaign.SourceCityId;
        var anchor = world.GetCity(anchorCityId);
        if (anchor == null)
        {
            return Array.Empty<CityData>();
        }

        var retreatFactionId = GetRetreatFactionId(world, team);
        var destinations = new List<CityData>();
        var origin = world.GetCity(GetRetreatOriginCityId(campaign, team));
        var canFallBackToDefendedCity = team.Side == CampaignBattleSide.Defender &&
                                        campaign.Stage == CampaignStage.FieldBattle &&
                                        anchor.OwnerFactionId == retreatFactionId;
        if (canFallBackToDefendedCity)
        {
            destinations.Add(anchor);
        }

        if (origin != null &&
            origin.OwnerFactionId == retreatFactionId &&
            !(team.Side == CampaignBattleSide.Defender && origin.Id == anchorCityId) &&
            destinations.All(existing => existing.Id != origin.Id))
        {
            destinations.Add(origin);
        }

        destinations.AddRange(anchor.ConnectedCityIds
            .Distinct()
            .Select(world.GetCity)
            .Where(city => city != null &&
                           (city.OwnerFactionId == retreatFactionId || city.OwnerFactionId == 0))
            .Select(city => city!)
            .OrderBy(city => city.OwnerFactionId == retreatFactionId ? 0 : 1)
            .ThenBy(city => city.Id)
            .Where(city => destinations.All(existing => existing.Id != city.Id)));

        return destinations;
    }

    /// <summary>
    /// Resolves a team's source city, including campaign snapshots created before
    /// OriginCityId was serialized. This keeps older campaign saves retreatable.
    /// </summary>
    public static int GetRetreatOriginCityId(ActiveBattleCampaignData campaign, CampaignBattleTeamData team)
    {
        if (IsAlliedEnvoyLoanTeam(team) && team.OfficerReturnCityId > 0)
        {
            return team.OfficerReturnCityId;
        }

        if (team.OriginCityId > 0)
        {
            return team.OriginCityId;
        }

        var reinforcement = campaign.Reinforcements.FirstOrDefault(order => order.Id == team.ReinforcementOrderId);
        if (reinforcement != null && reinforcement.SourceCityId > 0)
        {
            return reinforcement.SourceCityId;
        }

        return team.Side == CampaignBattleSide.Defender
            ? campaign.TargetCityId
            : campaign.SourceCityId;
    }

    public static bool TryRetreatTeamToCity(
        WorldState world,
        ActiveBattleCampaignData campaign,
        CampaignBattleTeamData team,
        int destinationCityId)
    {
        var destination = GetRetreatDestinations(world, campaign, team)
            .FirstOrDefault(city => city.Id == destinationCityId);
        if (destination == null)
        {
            return false;
        }

        if (destination.OwnerFactionId == 0)
        {
            destination.OwnerFactionId = GetRetreatFactionId(world, team);
        }

        if (team.Side == CampaignBattleSide.Defender &&
            campaign.Stage == CampaignStage.FieldBattle &&
            destination.Id == campaign.TargetCityId)
        {
            team.Location = CampaignTeamLocation.InnerCity;
            team.RetreatDestinationCityId = destination.Id;
            return true;
        }

        var troopDestination = destination;
        if (IsAlliedEnvoyLoanTeam(team))
        {
            var origin = world.GetCity(team.OriginCityId);
            troopDestination = origin?.OwnerFactionId == team.FactionId ? origin : world.Cities
                .Where(city => city.OwnerFactionId == team.FactionId).OrderBy(city => city.Id).FirstOrDefault();
        }
        if (troopDestination != null)
            ReturnTeamToCity(world, troopDestination, team, destination);
        else
        {
            team.DisbandedTroops += Math.Max(0, team.ActiveTroops) + Math.Max(0, team.WoundedTroops);
            if (team.OfficerId > 0 && world.GetOfficer(team.OfficerId) is { } officer)
            {
                RemoveOfficerFromAllCities(world, officer.Id);
                if (!destination.OfficerIds.Contains(officer.Id)) destination.OfficerIds.Add(officer.Id);
                officer.CityId = destination.Id;
            }
        }
        CampaignTroopOriginRules.ApplyCounts(team, 0, 0);
        team.Location = CampaignTeamLocation.NeighborCity;
        team.RetreatDestinationCityId = destination.Id;
        return true;
    }

    public static bool TryDeliverCaravanEscortTeam(
        WorldState world,
        ActiveBattleCampaignData campaign,
        CampaignBattleTeamData team)
    {
        if (!campaign.IsCaravanEscortBattle || team.Side != CampaignBattleSide.Defender)
        {
            return false;
        }

        var destination = world.GetCity(campaign.TargetCityId);
        if (destination == null || destination.OwnerFactionId != team.FactionId)
        {
            return false;
        }

        ReturnTeamToCity(world, destination, team, destination);
        CampaignTroopOriginRules.ApplyCounts(team, 0, 0);
        team.Location = CampaignTeamLocation.NeighborCity;
        team.RetreatDestinationCityId = destination.Id;
        return true;
    }

    public static bool TryReturnCaravanEscortTeamToSource(
        WorldState world,
        ActiveBattleCampaignData campaign,
        CampaignBattleTeamData team)
    {
        if (!campaign.IsCaravanEscortBattle || team.Side != CampaignBattleSide.Defender)
        {
            return false;
        }

        var source = world.GetCity(campaign.SourceCityId);
        if (source == null || source.OwnerFactionId != team.FactionId)
        {
            return false;
        }

        ReturnTeamToCity(world, source, team, source);
        CampaignTroopOriginRules.ApplyCounts(team, 0, 0);
        team.Location = CampaignTeamLocation.NeighborCity;
        team.RetreatDestinationCityId = source.Id;
        return true;
    }

    public static bool TryReturnSupportTeamToOrigin(
        WorldState world,
        CampaignBattleTeamData team)
    {
        var destination = world.GetCity(team.OriginCityId);
        if (destination == null || destination.OwnerFactionId != team.FactionId)
        {
            return false;
        }

        ReturnTeamToCity(world, destination, team);
        CampaignTroopOriginRules.ApplyCounts(team, 0, 0);
        team.Location = CampaignTeamLocation.NeighborCity;
        team.RetreatDestinationCityId = destination.Id;
        return true;
    }

    internal static void ReturnTeamToCity(
        WorldState world,
        CityData destination,
        CampaignBattleTeamData team,
        CityData? officerDestination = null)
    {
        var returnedTroops = Math.Max(0, team.ActiveTroops + team.WoundedTroops);
        if (team.EquipmentType != BattleEquipmentType.None)
        {
            // Equipment teams are represented by one battle marker. Their crew
            // was reserved separately, so return both only if the marker survives.
            if (returnedTroops > 0)
            {
                var crewCount = BattleSupportRules.GetRequiredEngineerCount(team.EquipmentType);
                destination.AddTroops(TroopType.Engineer, crewCount);
                AddBattleEquipment(destination, team.EquipmentType);
                team.ReturnedTroops += crewCount;
            }
        }
        else
        {
            destination.AddTroops(team.TroopType, returnedTroops);
            team.ReturnedTroops += returnedTroops;
        }
        if (team.TroopType == TroopType.Siege && team.SiegeEngineType != SiegeEngineType.None)
        {
            destination.AddSiegeEngine(team.SiegeEngineType, 1);
        }

        if (team.OfficerId > 0)
        {
            officerDestination ??= destination;
            RemoveOfficerFromAllCities(world, team.OfficerId);
            if (!officerDestination.OfficerIds.Contains(team.OfficerId))
            {
                officerDestination.OfficerIds.Add(team.OfficerId);
            }

            if (world.GetOfficer(team.OfficerId) is { } officer)
            {
                officer.CityId = officerDestination.Id;
            }
        }
    }

    // Automatic campaign withdrawal returns mixed-origin troops to their own
    // friendly origin when possible. Explicit player retreat still uses the
    // selected destination for the whole team.
    internal static void ReturnMixedTeamToOrigins(WorldState world, CityData fallback, CampaignBattleTeamData team, CityData officerDestination, int excludedCityId = 0)
    {
        CampaignTroopOriginRules.EnsureInitialized(team);
        var total = 0;
        foreach (var origin in team.TroopOrigins)
        {
            var city = world.GetCity(origin.CityId);
            if (city == null || city.OwnerFactionId != team.FactionId || city.Id == excludedCityId) city = fallback;
            var troops = Math.Max(0, origin.ActiveTroops) + Math.Max(0, origin.WoundedTroops);
            city.AddTroops(team.TroopType, troops);
            total += troops;
        }
        team.ReturnedTroops += total;
        if (team.OfficerId <= 0) return;
        RemoveOfficerFromAllCities(world, team.OfficerId);
        if (!officerDestination.OfficerIds.Contains(team.OfficerId)) officerDestination.OfficerIds.Add(team.OfficerId);
        if (world.GetOfficer(team.OfficerId) is { } officer) officer.CityId = officerDestination.Id;
    }

    private static void AddBattleEquipment(CityData city, BattleEquipmentType equipmentType)
    {
        switch (equipmentType)
        {
            case BattleEquipmentType.SupplyCart:
                city.SupplyCartCount += 1;
                break;
            case BattleEquipmentType.Ram:
                city.RamCount += 1;
                break;
            case BattleEquipmentType.Ladder:
                city.LadderCount += 1;
                break;
            case BattleEquipmentType.Catapult:
                city.CatapultCount += 1;
                break;
        }
    }

    private static bool IsAlliedEnvoyLoanTeam(CampaignBattleTeamData team) =>
        string.Equals(team.CooperationObjective, "allied-envoy-loan", StringComparison.Ordinal);

    public static int GetRetreatFactionId(WorldState world, CampaignBattleTeamData team)
    {
        if (!IsAlliedEnvoyLoanTeam(team) || team.OfficerId <= 0)
        {
            return team.FactionId;
        }

        return world.Factions
            .FirstOrDefault(faction => faction.OfficerIds.Contains(team.OfficerId) || faction.RulerOfficerId == team.OfficerId)
            ?.Id ?? team.FactionId;
    }

    internal static CityData? ResolveReturnCity(
        WorldState world,
        ActiveBattleCampaignData campaign,
        CampaignBattleTeamData team,
        CampaignBattleSide winner,
        bool attackerWonCity)
    {
        var reinforcement = campaign.Reinforcements.FirstOrDefault(order => order.Id == team.ReinforcementOrderId);
        if (reinforcement != null && reinforcement.FactionId != campaign.AttackerFactionId && reinforcement.FactionId != campaign.DefenderFactionId)
        {
            var source = world.GetCity(reinforcement.SourceCityId);
            return source?.OwnerFactionId == team.FactionId ? source : world.Cities
                .Where(city => city.OwnerFactionId == team.FactionId).OrderBy(city => city.Id).FirstOrDefault();
        }

        if (team.Side == CampaignBattleSide.Attacker)
        {
            if (attackerWonCity) return world.GetCity(campaign.TargetCityId);
            var source = world.GetCity(campaign.SourceCityId);
            return source?.OwnerFactionId == team.FactionId ? source : world.Cities
                .Where(city => city.OwnerFactionId == team.FactionId).OrderBy(city => city.Id).FirstOrDefault();
        }

        return winner == CampaignBattleSide.Defender || !attackerWonCity
            ? world.GetCity(campaign.TargetCityId)
            : null;
    }

    internal static void RemoveOfficerFromAllCities(WorldState world, int officerId)
    {
        foreach (var city in world.Cities)
        {
            city.OfficerIds.Remove(officerId);
        }
    }

}
