using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;
using static ThreeKingdom.Core.BattleCampaignService;
using static ThreeKingdom.Core.CampaignRetreatService;

namespace ThreeKingdom.Core;

internal static class CampaignCaptiveService
{
    internal static void ReleaseDefeatedCaravanOfficers(
        WorldState world,
        ActiveBattleCampaignData campaign,
        CityData sourceCity)
    {
        foreach (var team in campaign.Teams.Where(team =>
                     team.Side == CampaignBattleSide.Defender &&
                     team.OfficerId > 0 &&
                     team.Location is CampaignTeamLocation.Eliminated or CampaignTeamLocation.Captured))
        {
            var officer = world.GetOfficer(team.OfficerId);
            if (officer == null)
            {
                continue;
            }

            // A bandit ambush has no prison or captor faction. A defeated
            // transport officer therefore becomes a visible free officer at
            // their source city rather than entering the captive workflow.
            officer.HomeCityId = sourceCity.Id;
            RemoveCapturedOfficerFromFaction(world, officer);
            RemoveOfficerFromAllCities(world, officer.Id);
            officer.CityId = sourceCity.Id;
            officer.CaptiveFactionId = 0;
            officer.JailedCityId = 0;
            officer.FreeOfficerStayMonths = 2;
            team.Location = CampaignTeamLocation.Eliminated;
        }
    }


    internal static void CaptureDefeatedCampaignTeams(
        WorldState world,
        ActiveBattleCampaignData campaign,
        CampaignBattleSide winner,
        int jailCityId,
        ICollection<int> capturedOfficerIds,
        IDictionary<int, int>? deferredRulerSuccessions = null)
    {
        var winnerFactionId = winner == CampaignBattleSide.Attacker
            ? campaign.AttackerFactionId
            : campaign.DefenderFactionId;
        foreach (var team in campaign.Teams.Where(team =>
                     team.Side != winner &&
                     team.OfficerId > 0 &&
                     team.Location is not (CampaignTeamLocation.NeighborCity or CampaignTeamLocation.Captured)))
        {
            var officer = world.GetOfficer(team.OfficerId);
            if (officer == null || (officer.DeathYear > 0 && world.Year > officer.DeathYear))
            {
                continue;
            }

            officer.HomeCityId = team.OfficerReturnCityId > 0
                ? team.OfficerReturnCityId
                : (team.OriginCityId > 0 ? team.OriginCityId : officer.CityId);
            RemoveCapturedOfficerFromFaction(world, officer, deferredRulerSuccessions);
            RemoveOfficerFromAllCities(world, officer.Id);
            officer.CityId = 0;
            officer.CaptiveFactionId = winnerFactionId;
            officer.JailedCityId = jailCityId;
            officer.FreeOfficerStayMonths = 0;
            team.ActiveTroops = 0;
            team.WoundedTroops = 0;
            team.Location = CampaignTeamLocation.Captured;
            capturedOfficerIds.Add(officer.Id);
        }
    }

    internal static void RemoveCapturedOfficerFromFaction(
        WorldState world,
        OfficerData officer,
        IDictionary<int, int>? deferredRulerSuccessions = null)
    {
        var faction = world.Factions.FirstOrDefault(candidate => candidate.OfficerIds.Contains(officer.Id) || candidate.RulerOfficerId == officer.Id);
        if (faction == null)
        {
            return;
        }

        var wasRuler = faction.RulerOfficerId == officer.Id;
        faction.OfficerIds.Remove(officer.Id);
        OfficerAppointmentRules.ClearMilitaryAppointments(officer);
        OfficerAppointmentRules.RemoveAppointment(officer, OfficerAppointmentRules.Strategist);
        if (faction.ChiefStrategistOfficerId == officer.Id) faction.ChiefStrategistOfficerId = 0;
        OfficerAppointmentRules.RemoveAppointment(officer, OfficerAppointmentRules.ChiefStrategist);
        OfficerAppointmentRules.RemoveAppointment(officer, OfficerAppointmentRules.Lord);
        if (!wasRuler)
        {
            return;
        }

        officer.DisplacedRulerFactionId = faction.Id;
        faction.RulerOfficerId = 0;
        if (deferredRulerSuccessions != null)
        {
            deferredRulerSuccessions.TryAdd(faction.Id, officer.Id);
            return;
        }

        ResolveCapturedRulerSuccession(world, faction, officer);
    }

    internal static void ResolveDeferredCapturedRulerSuccessions(WorldState world, IReadOnlyDictionary<int, int> deferredRulerSuccessions)
    {
        foreach (var (factionId, previousRulerOfficerId) in deferredRulerSuccessions)
        {
            var faction = world.GetFaction(factionId);
            var previousRuler = world.GetOfficer(previousRulerOfficerId);
            if (faction != null && previousRuler != null)
            {
                ResolveCapturedRulerSuccession(world, faction, previousRuler);
            }
        }
    }

    private static void ResolveCapturedRulerSuccession(WorldState world, FactionData faction, OfficerData previousRuler)
    {
        var candidates = faction.OfficerIds
            .Select(world.GetOfficer)
            .Where(candidate => candidate != null &&
                                candidate.CaptiveFactionId <= 0 &&
                                (candidate.DeathYear <= 0 || world.Year <= candidate.DeathYear) &&
                                !IsOfficerCommitted(world, candidate.Id))
            // Succession follows the ruling house whenever a qualified blood
            // relative remains; ability breaks ties within that group.
            .OrderByDescending(candidate => OfficerRelationshipRules.GetSuccessionRelationshipPriority(previousRuler, candidate))
            .ThenByDescending(candidate => candidate!.Leadership + candidate.Intelligence + candidate.Politics + candidate.Charm)
            .ThenByDescending(candidate => candidate!.Loyalty)
            .ToList();
        if (faction.IsPlayer)
        {
            world.PendingSuccessionRecords.RemoveAll(record => record.FactionId == faction.Id);
            world.PendingSuccessionRecords.Add(new WorldState.PendingSuccessionData
            {
                FactionId = faction.Id,
                PreviousRulerOfficerId = previousRuler.Id,
                TriggeredByCapture = true,
                CandidateOfficerIds = candidates.Select(candidate => candidate!.Id).ToList()
            });
            return;
        }

        var successor = candidates.FirstOrDefault();
        if (successor == null)
        {
            return;
        }

        faction.RulerOfficerId = successor.Id;
        faction.ChancellorOfficerId = faction.ChancellorOfficerId == successor.Id ? 0 : faction.ChancellorOfficerId;
        faction.ChiefStrategistOfficerId = faction.ChiefStrategistOfficerId == successor.Id ? 0 : faction.ChiefStrategistOfficerId;
        OfficerAppointmentRules.ClearMilitaryAppointments(successor);
        OfficerAppointmentRules.RemoveAppointment(successor, OfficerAppointmentRules.Strategist);
        OfficerAppointmentRules.RemoveAppointment(successor, OfficerAppointmentRules.ChiefStrategist);
        OfficerAppointmentRules.AddAppointment(successor, OfficerAppointmentRules.Lord);
        successor.Belongs = faction.Id.ToString();
        faction.NameZhHant = string.IsNullOrWhiteSpace(successor.NameZhHant) ? faction.NameZhHant : successor.NameZhHant;
        faction.NameEn = string.IsNullOrWhiteSpace(successor.Name) ? faction.NameEn : successor.Name;
    }

}
