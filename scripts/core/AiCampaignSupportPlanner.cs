using System;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class AiCampaignSupportPlanner
{
    public static BattleInvitationData? TryRequestSupport(WorldState world, ActiveBattleCampaignData campaign, CampaignBattleSide side)
    {
        if (campaign.Stage == CampaignStage.Resolved || campaign.IsCaravanEscortBattle) return null;
        var factionId = side == CampaignBattleSide.Attacker ? campaign.AttackerFactionId : campaign.DefenderFactionId;
        var enemyId = side == CampaignBattleSide.Attacker ? campaign.DefenderFactionId : campaign.AttackerFactionId;
        if (factionId <= 0 || world.GetFaction(factionId) is not { IsPlayer: false }) return null;
        if (campaign.Invitations.Any(item => item.InviterFactionId == factionId && item.Side == side &&
            item.RequestYear == world.Year && item.RequestMonth == world.Month)) return null;

        var friendly = campaign.Teams.Where(team => team.Side == side && team.OfficerId > 0 &&
            team.Location is CampaignTeamLocation.Field or CampaignTeamLocation.InnerCity or CampaignTeamLocation.Reserve)
            .Sum(team => (long)Math.Max(0, team.ActiveTroops));
        var enemy = campaign.Teams.Where(team => team.Side != side && team.OfficerId > 0 &&
            team.Location is CampaignTeamLocation.Field or CampaignTeamLocation.InnerCity or CampaignTeamLocation.Reserve)
            .Sum(team => (long)Math.Max(0, team.ActiveTroops));
        if (friendly <= 0) return null;
        var food = Math.Max(0, side == CampaignBattleSide.Attacker ? campaign.AttackerFood : campaign.DefenderFood);
        var gold = Math.Max(0, side == CampaignBattleSide.Attacker ? campaign.AttackerGold : campaign.DefenderGold);
        var foodTarget = Math.Min(10000L, (friendly * 5 + 99) / 100 * 5);
        var goldTarget = Math.Min(5000L, (friendly + 99) / 100 * 5);
        var troopNeed = friendly < enemy ? (int)Math.Min(3000L, Math.Max(500L, enemy - friendly)) : 0;
        var foodNeed = food < foodTarget ? (int)(foodTarget - food) : 0;
        var goldNeed = gold < goldTarget ? (int)(goldTarget - gold) : 0;
        if (troopNeed == 0 && foodNeed == 0 && goldNeed == 0) return null;

        var envoy = world.Cities.Where(city => city.OwnerFactionId == factionId).SelectMany(city => city.OfficerIds)
            .Distinct().Select(world.GetOfficer).Where(officer => officer != null &&
                !BattleCampaignService.IsOfficerCommitted(world, officer.Id) &&
                !OfficerAvailabilityRules.IsTravelingWithLogistics(world, officer.Id) &&
                !(officer.LastAssignedYear == world.Year && officer.LastAssignedMonth == world.Month) &&
                !world.InternalAffairsSchedules.Any(schedule => schedule.State == InternalAffairsScheduleState.Active && schedule.OfficerId == officer.Id))
            .OrderByDescending(officer => officer!.Intelligence + officer.Charm).ThenBy(officer => officer!.Id).FirstOrDefault();
        if (envoy == null) return null;

        var candidate = world.Cities.Where(city => city.OwnerFactionId > 0 && city.OwnerFactionId != factionId && city.OwnerFactionId != enemyId &&
                world.GetFaction(city.OwnerFactionId) != null &&
                !campaign.Teams.Any(team => team.FactionId == city.OwnerFactionId &&
                    team.Location is not (CampaignTeamLocation.Eliminated or CampaignTeamLocation.Captured or CampaignTeamLocation.NeighborCity)) &&
                !campaign.Reinforcements.Any(order => order.FactionId == city.OwnerFactionId &&
                    order.Status is ReinforcementStatus.Traveling or ReinforcementStatus.Arrived or ReinforcementStatus.Reserve or ReinforcementStatus.Deployed) &&
                !campaign.Invitations.Any(item => item.InvitedFactionId == city.OwnerFactionId &&
                    item.Status is BattleInvitationStatus.Pending or BattleInvitationStatus.Accepted))
            .Select(city => new { City = city, Relation = world.GetDiplomacyRelation(factionId, city.OwnerFactionId),
                Links = BattleCampaignService.GetFriendlyRouteLinks(world, city.Id, campaign.TargetCityId, city.OwnerFactionId) })
            .Where(item => item.Links > 0)
            .Select(item => new { item.City, item.Relation, item.Links,
                CanSendTroops = item.Relation is { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 } &&
                    item.City.Troops > BattleCampaignService.MinimumCityGarrison &&
                    item.City.OfficerIds.Any(id => world.GetOfficer(id) is { } officer &&
                        !BattleCampaignService.IsOfficerCommitted(world, id) &&
                        !OfficerAvailabilityRules.IsTravelingWithLogistics(world, id) &&
                        !(officer.LastAssignedYear == world.Year && officer.LastAssignedMonth == world.Month) &&
                        !world.InternalAffairsSchedules.Any(schedule => schedule.State == InternalAffairsScheduleState.Active && schedule.OfficerId == id)) })
            .Where(item => (troopNeed > 0 && item.CanSendTroops) ||
                           (foodNeed > 0 && item.City.Food >= 3) || (goldNeed > 0 && item.City.Gold >= 3))
            .OrderByDescending(item => troopNeed > 0 && item.CanSendTroops)
            .ThenByDescending(item => item.Relation?.RelationScore ?? 0)
            .ThenBy(item => item.Links).ThenBy(item => item.City.Id).FirstOrDefault();
        if (candidate == null) return null;

        var troops = candidate.CanSendTroops ? troopNeed : 0;
        var requestedFood = candidate.City.Food >= 3 ? foodNeed : 0;
        var requestedGold = candidate.City.Gold >= 3 ? goldNeed : 0;
        if (troops == 0 && requestedFood == 0 && requestedGold == 0) return null;
        var invitation = BattleCampaignService.CreateInvitation(campaign, factionId, candidate.City.OwnerFactionId, side,
            BattleCampaignService.GetRequestedSupportType(troops, requestedGold, requestedFood), envoy.Id,
            troops, requestedGold, requestedFood, candidate.City.Id);
        invitation.RequestYear = world.Year;
        invitation.RequestMonth = world.Month;
        envoy.LastAssignedYear = world.Year;
        envoy.LastAssignedMonth = world.Month;
        envoy.LastAssignedCommand = CommandType.Diplomacy;
        BattleCampaignService.ScheduleInvitationResponse(world, campaign, invitation);
        return invitation;
    }
}
