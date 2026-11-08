using System;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

/// <summary>Shared data validation and troop arithmetic for covert officers.</summary>
public static class CovertOfficerRules
{
    public static bool IsValid(WorldState world, WorldState.CovertOfficerData agent)
    {
        var officer = world.GetOfficer(agent.OfficerId);
        var original = world.GetFaction(agent.OriginalFactionId);
        return agent.SponsorFactionId > 0 && agent.OriginalFactionId > 0 &&
            agent.SponsorFactionId != agent.OriginalFactionId && world.GetFaction(agent.SponsorFactionId) != null &&
            original != null && original.RulerOfficerId != agent.OfficerId && original.OfficerIds.Contains(agent.OfficerId) &&
            officer != null && officer.CaptiveFactionId <= 0 && (officer.DeathYear <= 0 || world.Year <= officer.DeathYear);
    }

    public const int AnnualMaintenance = 100;

    // Use the same deterministic payment order and treasury selection for every faction.
    public static System.Collections.Generic.List<(int SponsorId, int OfficerId, string Kind)> ProcessMaintenance(WorldState world, bool annualDue)
    {
        var events = new System.Collections.Generic.List<(int, int, string)>();
        foreach (var agent in world.CovertOfficerRecords.Where(item => IsValid(world, item))
                     .OrderBy(item => item.SponsorFactionId).ThenBy(item => item.OfficerId).ToList())
        {
            if (!annualDue && !agent.MaintenanceUnpaid) continue;
            if (annualDue && agent.MaintenanceUnpaid && agent.MaintenanceUnpaidSinceYear < world.Year)
            {
                world.CovertOfficerRecords.Remove(agent);
                events.Add((agent.SponsorFactionId, agent.OfficerId, "left"));
                continue;
            }
            var treasury = MainCityRules.FindMainCity(world, agent.SponsorFactionId);
            if (treasury != null && treasury.Gold >= AnnualMaintenance)
            {
                treasury.Gold -= AnnualMaintenance;
                var recovered = agent.MaintenanceUnpaid;
                agent.MaintenanceUnpaid = false;
                agent.MaintenanceUnpaidSinceYear = 0;
                if (recovered) events.Add((agent.SponsorFactionId, agent.OfficerId, "recovered"));
            }
            else if (!agent.MaintenanceUnpaid)
            {
                agent.MaintenanceUnpaid = true;
                agent.MaintenanceUnpaidSinceYear = world.Year;
                events.Add((agent.SponsorFactionId, agent.OfficerId, "unpaid"));
            }
        }
        return events;
    }

    public static bool CanScheduleDiscord(WorldState world, WorldState.CovertOfficerData agent, int targetId)
    {
        if (!IsValid(world, agent) || agent.MaintenanceUnpaid || agent.DiscordTargetOfficerId != 0 || agent.CitySurrenderScheduledMonth > 0 || targetId == agent.OfficerId) return false;
        var officer = world.GetOfficer(agent.OfficerId);
        var city = officer == null ? null : world.GetCity(officer.CityId);
        var target = world.GetOfficer(targetId);
        return city != null && city.OwnerFactionId == agent.OriginalFactionId && city.OfficerIds.Contains(agent.OfficerId) &&
            target != null && target.CityId == city.Id && target.CaptiveFactionId <= 0 && city.OfficerIds.Contains(targetId) &&
            world.GetFaction(agent.OriginalFactionId)?.OfficerIds.Contains(targetId) == true &&
            world.GetFaction(agent.OriginalFactionId)?.RulerOfficerId != targetId &&
            !BattleCampaignService.IsOfficerCommitted(world, agent.OfficerId) && !BattleCampaignService.IsOfficerCommitted(world, targetId);
    }

    public static bool ScheduleDiscord(WorldState world, WorldState.CovertOfficerData agent, int targetId)
    {
        if (!CanScheduleDiscord(world, agent, targetId)) return false;
        agent.DiscordTargetOfficerId = targetId;
        agent.DiscordScheduledMonth = world.Year * 12 + world.Month;
        agent.AwaitingDefection = false;
        return true;
    }

    public static bool CanScheduleCitySurrender(WorldState world, WorldState.CovertOfficerData agent)
    {
        if (!IsValid(world, agent) || agent.MaintenanceUnpaid || agent.AwaitingDefection ||
            agent.DiscordTargetOfficerId > 0 || agent.CitySurrenderScheduledMonth > 0)
        {
            return false;
        }

        var officer = world.GetOfficer(agent.OfficerId);
        var city = officer == null ? null : world.GetCity(officer.CityId);
        var original = world.GetFaction(agent.OriginalFactionId);
        return officer != null && city != null && original != null &&
               city.OwnerFactionId == agent.OriginalFactionId && city.OfficerIds.Contains(officer.Id) &&
               original.RulerOfficerId != officer.Id &&
               OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Governor) &&
               !world.ActiveBattleCampaigns.Any(campaign => campaign.Stage != CampaignStage.Resolved && campaign.TargetCityId == city.Id);
    }

    public static bool ScheduleCitySurrender(WorldState world, WorldState.CovertOfficerData agent)
    {
        if (!CanScheduleCitySurrender(world, agent)) return false;
        agent.CitySurrenderScheduledMonth = world.Year * 12 + world.Month;
        agent.CitySurrenderCityId = world.GetOfficer(agent.OfficerId)!.CityId;
        return true;
    }

    public static System.Collections.Generic.List<(int SponsorId, int OfficerId, int CityId, string Kind, System.Collections.Generic.List<int> JoinedOfficerIds)> ProcessCitySurrenders(WorldState world)
    {
        var results = new System.Collections.Generic.List<(int, int, int, string, System.Collections.Generic.List<int>)>();
        foreach (var agent in world.CovertOfficerRecords
                     .Where(item => item.CitySurrenderScheduledMonth > 0 && item.CitySurrenderScheduledMonth < world.Year * 12 + world.Month)
                     .ToList())
        {
            if (!world.CovertOfficerRecords.Contains(agent)) continue;
            agent.CitySurrenderScheduledMonth = 0;
            var officer = world.GetOfficer(agent.OfficerId);
            var cityId = agent.CitySurrenderCityId > 0 ? agent.CitySurrenderCityId : officer?.CityId ?? 0;
            agent.CitySurrenderCityId = 0;
            if (officer?.CityId != cityId || !CanScheduleCitySurrender(world, agent))
            {
                results.Add((agent.SponsorFactionId, agent.OfficerId, cityId, "cancelled", new()));
                continue;
            }

            var city = world.GetCity(cityId)!;
            var affinity = Math.Clamp(world.GetOfficerAffection(agent.SponsorFactionId, officer!.Id)?.Score ?? 0, 0, 100);
            var chance = Math.Clamp(45 + (60 - officer.Loyalty) + affinity / 4 +
                                    officer.Intelligence / 10 - city.Defense / 12, 10, 80);
            var random = new Random(HashCode.Combine(world.RandomSeed, world.Year, world.Month, officer.Id, city.Id));
            var forceSuccess = world.ForceNextCitySurrenderSuccessForDebug && world.GetFaction(agent.SponsorFactionId)?.IsPlayer == true;
            if (!forceSuccess && random.Next(100) >= chance)
            {
                var exposed = random.Next(100) < 15;
                if (exposed) world.CovertOfficerRecords.Remove(agent);
                results.Add((agent.SponsorFactionId, officer.Id, city.Id, exposed ? "exposed" : "failed", new()));
                continue;
            }

            var joinedOfficerIds = TransferCityToSponsor(world, agent, city, officer);
            if (forceSuccess) world.ForceNextCitySurrenderSuccessForDebug = false;
            results.Add((agent.SponsorFactionId, officer.Id, city.Id, "success", joinedOfficerIds));
            world.CovertOfficerRecords.Remove(agent);
        }
        return results;
    }

    private static System.Collections.Generic.List<int> TransferCityToSponsor(WorldState world, WorldState.CovertOfficerData agent, CityData city, OfficerData governor)
    {
        var original = world.GetFaction(agent.OriginalFactionId)!;
        var sponsor = world.GetFaction(agent.SponsorFactionId)!;
        var joinedOfficerIds = new System.Collections.Generic.List<int>();
        var refuge = world.Cities.Where(item => item.OwnerFactionId == original.Id && item.Id != city.Id)
            .OrderBy(item => item.Id).FirstOrDefault();

        foreach (var officerId in city.OfficerIds.Where(id => id != governor.Id).ToList())
        {
            var officer = world.GetOfficer(officerId);
            if (officer != null && officer.CityId == city.Id && original.OfficerIds.Contains(officer.Id) &&
                original.RulerOfficerId != officer.Id && officer.CaptiveFactionId <= 0 &&
                (officer.DeathYear <= 0 || world.Year <= officer.DeathYear) &&
                !BattleCampaignService.IsOfficerCommitted(world, officer.Id) &&
                !OfficerAvailabilityRules.IsTravelingWithLogistics(world, officer.Id) &&
                WillFollowGovernor(world, officer, governor, city.Id))
            {
                original.OfficerIds.Remove(officer.Id);
                if (original.ChancellorOfficerId == officer.Id) original.ChancellorOfficerId = 0;
                if (original.ChiefStrategistOfficerId == officer.Id) original.ChiefStrategistOfficerId = 0;
                officer.Appointments.Clear();
                officer.Belongs = sponsor.Id.ToString();
                officer.Loyalty = 60;
                if (!sponsor.OfficerIds.Contains(officer.Id)) sponsor.OfficerIds.Add(officer.Id);
                world.InternalAffairsSchedules.RemoveAll(schedule => schedule.OfficerId == officer.Id);
                world.CovertOfficerRecords.RemoveAll(record => record.OfficerId == officer.Id);
                foreach (var item in world.Items.Where(item => item.EquippedOfficerId == officer.Id)) item.OwnerFactionId = sponsor.Id;
                joinedOfficerIds.Add(officer.Id);
                continue;
            }
            city.OfficerIds.Remove(officerId);
            if (officer == null) continue;
            OfficerAppointmentRules.RemoveAppointment(officer, OfficerAppointmentRules.Governor);
            if (refuge != null)
            {
                refuge.OfficerIds.Add(officer.Id);
                officer.CityId = refuge.Id;
            }
            else
            {
                officer.CityId = 0;
                officer.FreeOfficerStayMonths = Math.Max(officer.FreeOfficerStayMonths, 2);
                original.OfficerIds.Remove(officer.Id);
                if (original.ChancellorOfficerId == officer.Id) original.ChancellorOfficerId = 0;
                if (original.ChiefStrategistOfficerId == officer.Id) original.ChiefStrategistOfficerId = 0;
                officer.Appointments.Clear();
                officer.Belongs = "0";
            }
        }

        world.InternalAffairsSchedules.RemoveAll(schedule => schedule.CityId == city.Id);
        city.PrefectAuthorizationType = PrefectAuthorizationType.None;
        city.PrefectPlanJobType = InternalAffairsJobType.Farm;
        city.PrefectPlanConstructionProjectType = ConstructionProjectType.None;
        city.PrefectPlanTotalMonths = 0;
        city.PrefectPlanRemainingMonths = 0;
        city.PrefectPlanInvestedGold = 0;
        city.PrefectPlanIsPlayerDirected = false;
        city.OwnerFactionId = sponsor.Id;
        city.Loyalty = Math.Max(city.Loyalty, 55);

        original.OfficerIds.Remove(governor.Id);
        OfficerAppointmentRules.ClearMilitaryAppointments(governor);
        if (original.ChancellorOfficerId == governor.Id) original.ChancellorOfficerId = 0;
        if (original.ChiefStrategistOfficerId == governor.Id) original.ChiefStrategistOfficerId = 0;
        governor.Belongs = sponsor.Id.ToString();
        governor.Loyalty = 60;
        governor.CityId = city.Id;
        if (!sponsor.OfficerIds.Contains(governor.Id)) sponsor.OfficerIds.Add(governor.Id);
        foreach (var item in world.Items.Where(item => item.EquippedOfficerId == governor.Id)) item.OwnerFactionId = sponsor.Id;
        return joinedOfficerIds;
    }

    private static bool WillFollowGovernor(WorldState world, OfficerData officer, OfficerData governor, int cityId)
    {
        if (officer.Loyalty <= 40) return true;
        if (officer.Loyalty > 70) return false;

        var chance = OfficerRelationshipRules.GetRelationshipType(officer, governor) switch
        {
            "family,blood" => 70,
            "family,non-blood" => 50,
            "family" => 40,
            _ => 0
        };
        if (chance == 0) return false;
        var random = new Random(HashCode.Combine(world.RandomSeed, world.Year, world.Month, cityId, governor.Id, officer.Id));
        return random.Next(100) < chance;
    }

    public static System.Collections.Generic.List<(int SponsorId, int OfficerId, int TargetId, string Kind)> ProcessDiscord(WorldState world)
    {
        var results = new System.Collections.Generic.List<(int, int, int, string)>();
        foreach (var agent in world.CovertOfficerRecords.Where(a => a.DiscordTargetOfficerId > 0 && a.DiscordScheduledMonth < world.Year * 12 + world.Month).ToList())
        {
            var targetId = agent.DiscordTargetOfficerId;
            agent.DiscordTargetOfficerId = 0;
            agent.DiscordScheduledMonth = 0;
            if (!CanScheduleDiscord(world, agent, targetId))
            {
                results.Add((agent.SponsorFactionId, agent.OfficerId, targetId, "cancelled"));
                continue;
            }
            var random = new Random(HashCode.Combine(world.RandomSeed, world.Year, world.Month, agent.OfficerId, targetId));
            var target = world.GetOfficer(targetId)!;
            var actor = world.GetOfficer(agent.OfficerId)!;
            var chance = Math.Clamp(45 + (actor.Intelligence - target.Intelligence) / 3, 20, 75);
            var success = random.Next(100) < chance;
            var exposed = random.Next(100) < 15;
            if (success) target.Loyalty = Math.Max(0, target.Loyalty - 8);
            if (exposed) world.CovertOfficerRecords.Remove(agent);
            results.Add((agent.SponsorFactionId, agent.OfficerId, targetId, exposed ? "exposed" : success ? "success" : "failed"));
        }
        return results;
    }

    public static (int Joining, int Withdrawing) SplitTroops(int troops)
    {
        var total = Math.Max(0, troops);
        var joining = (int)((long)total * 60 / 100);
        return (joining, total - joining);
    }

    /// <summary>Atomically changes a deployed team and its strategic ownership. The caller must first sync live battle casualties.</summary>
    public static bool TryDefect(WorldState world, ActiveBattleCampaignData campaign, CampaignBattleTeamData team)
    {
        if (campaign.IsCaravanEscortBattle || campaign.Stage == CampaignStage.Resolved || campaign.TotalBattleDays < 1 ||
            !(team.Location == CampaignTeamLocation.Field || campaign.Stage == CampaignStage.CityBattle && team.Location == CampaignTeamLocation.InnerCity) || team.OfficerId <= 0 || team.EquipmentType != BattleEquipmentType.None ||
            team.SiegeEngineType != SiegeEngineType.None || team.ActiveTroops < 2 || team.RetreatDestinationCityId > 0)
            return false;
        var agent = world.CovertOfficerRecords.FirstOrDefault(item => item.OfficerId == team.OfficerId && item.AwaitingDefection && !item.MaintenanceUnpaid && IsValid(world, item));
        if (agent == null || team.FactionId != agent.OriginalFactionId) return false;
        var newSide = team.Side == CampaignBattleSide.Attacker ? CampaignBattleSide.Defender : CampaignBattleSide.Attacker;
        var expectedSponsor = newSide == CampaignBattleSide.Attacker ? campaign.AttackerFactionId : campaign.DefenderFactionId;
        if (agent.SponsorFactionId != expectedSponsor) return false;
        var origin = world.GetCity(team.OriginCityId);
        if (origin?.OwnerFactionId != agent.OriginalFactionId)
            origin = world.Cities.Where(city => city.OwnerFactionId == agent.OriginalFactionId).OrderBy(city => city.Id).FirstOrDefault();
        var sponsorCity = world.Cities.Where(city => city.OwnerFactionId == agent.SponsorFactionId).OrderBy(city => city.Id).FirstOrDefault();
        var sponsor = world.GetFaction(agent.SponsorFactionId);
        var original = world.GetFaction(agent.OriginalFactionId);
        var officer = world.GetOfficer(team.OfficerId);
        if (origin == null || sponsorCity == null || sponsor == null || original == null || officer == null) return false;

        var (joining, withdrawing) = SplitTroops(team.ActiveTroops);
        var returned = withdrawing + Math.Max(0, team.WoundedTroops);
        origin.AddTroops(team.TroopType, returned);
        var previousMaximum = Math.Max(team.MaximumTroops, team.ActiveTroops + team.WoundedTroops);
        campaign.Teams.Add(new CampaignBattleTeamData
        {
            Id = campaign.Teams.Count == 0 ? 1 : campaign.Teams.Max(item => item.Id) + 1,
            FactionId = agent.OriginalFactionId, Side = team.Side, ControllerType = team.ControllerType,
            TroopType = team.TroopType, OriginCityId = origin.Id, Location = CampaignTeamLocation.NeighborCity,
            RetreatDestinationCityId = origin.Id, MaximumTroops = previousMaximum - joining,
            ReturnedTroops = returned
        });
        foreach (var city in world.Cities) city.OfficerIds.Remove(officer.Id);
        world.InternalAffairsSchedules.RemoveAll(schedule => schedule.OfficerId == officer.Id);
        original.OfficerIds.Remove(officer.Id);
        if (original.ChancellorOfficerId == officer.Id) original.ChancellorOfficerId = 0;
        if (original.ChiefStrategistOfficerId == officer.Id) original.ChiefStrategistOfficerId = 0;
        officer.Appointments.Clear();
        officer.CityId = 0;
        officer.Loyalty = Math.Min(officer.Loyalty, 60);
        if (!sponsor.OfficerIds.Contains(officer.Id)) sponsor.OfficerIds.Add(officer.Id);
        foreach (var item in world.Items.Where(item => item.EquippedOfficerId == officer.Id)) item.OwnerFactionId = sponsor.Id;
        team.Side = newSide;
        team.FactionId = sponsor.Id;
        team.ControllerType = sponsor.IsPlayer ? CampaignControllerType.Player : CampaignControllerType.Ai;
        team.ActiveTroops = joining;
        team.WoundedTroops = 0;
        team.MaximumTroops = joining;
        team.ReturnedTroops = 0;
        team.OriginCityId = sponsorCity.Id;
        team.OfficerReturnCityId = 0;
        team.ReinforcementOrderId = 0;
        team.CooperationObjective = string.Empty;
        world.CovertOfficerRecords.Remove(agent);
        return true;
    }
}

