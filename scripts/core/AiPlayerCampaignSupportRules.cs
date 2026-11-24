using System;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class AiPlayerCampaignSupportRules
{
    public static bool RespondToCampaignRequest(WorldState world, ActiveBattleCampaignData campaign, BattleInvitationData invitation,
        PlayerCampaignSupportRequestData response, bool accept)
    {
        var player = world.Factions.FirstOrDefault(faction => faction.IsPlayer);
        if (player == null || campaign.Stage == CampaignStage.Resolved || !campaign.Invitations.Contains(invitation) ||
            invitation.InvitedFactionId != player.Id || invitation.Status != BattleInvitationStatus.Pending || invitation.ResponseBattleDays > 0) return false;
        if (BattleCampaignService.DeclineUnavailableInvitationEnvoy(world, campaign, invitation)) return false;
        if (!accept)
        {
            invitation.Status = BattleInvitationStatus.Declined;
            invitation.DecisionReason = "Player declined support request.";
            BattleCampaignService.ReturnDeclinedEnvoy(world, campaign, invitation);
            return true;
        }
        if (response.RequestedTroops != invitation.RequestedTroops || response.RequestedGold != invitation.RequestedGold ||
            response.RequestedFood != invitation.RequestedFood || response.InviterFactionId != invitation.InviterFactionId || response.Side != invitation.Side ||
            !ValidateResponse(world, new PendingCommandData { TargetCityId = campaign.TargetCityId }, response)) return false;
        try
        {
            var deployments = response.Troops > 0
                ? new[] { new AttackOfficerDeploymentData { OfficerId = response.OfficerId, TroopType = response.TroopType, TroopCount = response.Troops } }
                : Array.Empty<AttackOfficerDeploymentData>();
            var order = BattleCampaignService.DispatchReinforcement(world, campaign, response.SourceCityId, response.Side, deployments,
                response.Gold, response.Food, response.Troops > 0 ? CampaignSupplyOwnership.Expedition : CampaignSupplyOwnership.Gift);
            order.InvitationId = invitation.Id;
            invitation.RequestedSourceCityId = response.SourceCityId;
            invitation.Status = BattleInvitationStatus.Accepted;
            invitation.DecisionReason = "Player accepted support request.";
            campaign.IsPlayerInvolved = true;
            return true;
        }
        catch (ReinforcementDispatchException) { return false; }
    }

    public static bool TryDeferAttackForPlayerSupport(WorldState world, PendingCommandData attack)
    {
        if (attack.IsAwaitingPrebattleSupport || attack.PlayerSupportRequest != null) return false;
        var target = world.GetCity(attack.TargetCityId);
        if (target == null) return false;
        var foodForThreeDays = ((long)attack.TroopsToSend * 5 + 99) / 100 * 3;
        if (attack.TroopsToSend > target.Troops && attack.FoodToSend >= foodForThreeDays) return false;
        var request = CreatePrebattleRequest(world, attack, CampaignBattleSide.Attacker);
        if (request == null) return false;
        attack.PlayerSupportRequest = request;
        attack.IsAwaitingPrebattleSupport = true;
        attack.PrebattleSupportRequestedYear = world.Year;
        attack.PrebattleSupportRequestedMonth = world.Month;
        attack.PrebattleSupportTargetFactionId = target.OwnerFactionId;
        return true;
    }

    public static bool IsDeferredResponseReady(WorldState world, PendingCommandData attack) =>
        (long)world.Year * 12 + world.Month > (long)attack.PrebattleSupportRequestedYear * 12 + attack.PrebattleSupportRequestedMonth;

    public static bool IsDeferredAttackValid(WorldState world, PendingCommandData attack)
    {
        var source = world.GetCity(attack.SourceCityId);
        var target = world.GetCity(attack.TargetCityId);
        var faction = world.GetFaction(attack.ActorFactionId);
        return source?.OwnerFactionId == attack.ActorFactionId && target != null && target.OwnerFactionId > 0 &&
            target.OwnerFactionId == attack.PrebattleSupportTargetFactionId && target.OwnerFactionId != attack.ActorFactionId && source.ConnectedCityIds.Contains(target.Id) && faction != null &&
            attack.OfficerIds.All(id => faction.OfficerIds.Contains(id) && source.OfficerIds.Contains(id) &&
                world.GetOfficer(id) is { CaptiveFactionId: <= 0 } officer && (officer.DeathYear <= 0 || world.Year <= officer.DeathYear));
    }

    public static bool IsDeferredEnvoyAvailable(WorldState world, PendingCommandData attack)
    {
        var id = attack.PlayerSupportRequest?.EnvoyOfficerId ?? 0;
        var officer = world.GetOfficer(id);
        return officer != null && world.GetFaction(attack.ActorFactionId)?.OfficerIds.Contains(id) == true &&
            world.GetCity(officer.CityId)?.OwnerFactionId == attack.ActorFactionId && officer.CaptiveFactionId <= 0 &&
            (officer.DeathYear <= 0 || world.Year <= officer.DeathYear);
    }

    public static void CancelDeferredAttack(WorldState world, PendingCommandData attack)
    {
        if (!attack.IsAwaitingPrebattleSupport || !world.PendingCommands.Remove(attack)) return;
        var city = world.GetCity(attack.SourceCityId);
        if (city?.OwnerFactionId != attack.ActorFactionId)
            city = world.Cities.Where(item => item.OwnerFactionId == attack.ActorFactionId).OrderBy(item => item.Id).FirstOrDefault();
        if (city == null) return;
        city.AddTroopAllocation(attack.TroopAllocation);
        city.AddSiegeEngineAllocation(attack.SiegeEngineAllocation);
        if (attack.BattleSupport.SupplyCart) city.SupplyCartCount++;
        city.Gold += attack.GoldToSend;
        city.Food += attack.FoodToSend;
    }

    public static PlayerCampaignSupportRequestData? CreatePrebattleRequest(WorldState world, PendingCommandData attack, CampaignBattleSide? requestedSide = null)
    {
        if (!world.InteractiveBattlesEnabled || attack.Type != CommandType.Attack) return null;
        var player = world.Factions.FirstOrDefault(faction => faction.IsPlayer);
        var source = world.GetCity(attack.SourceCityId);
        var target = world.GetCity(attack.TargetCityId);
        if (player == null || source == null || target == null || source.OwnerFactionId != attack.ActorFactionId || attack.TroopsToSend <= 0 ||
            !source.ConnectedCityIds.Contains(target.Id) || source.OwnerFactionId == target.OwnerFactionId ||
            source.OwnerFactionId == player.Id || target.OwnerFactionId == player.Id || target.OwnerFactionId <= 0) return null;
        if (world.ActiveBattleCampaigns.Any(campaign => campaign.Stage != CampaignStage.Resolved &&
            (campaign.SourceCityId == source.Id || campaign.TargetCityId == source.Id || campaign.SourceCityId == target.Id || campaign.TargetCityId == target.Id))) return null;

        foreach (var side in new[] { CampaignBattleSide.Defender, CampaignBattleSide.Attacker })
        {
            if (requestedSide.HasValue && side != requestedSide.Value) continue;
            if (side == CampaignBattleSide.Defender && world.PrewarAllianceRequests.Any(item =>
                !item.IsPending && item.InviterFactionId == attack.ActorFactionId && item.AllyFactionId == player.Id && item.TargetCityId == attack.TargetCityId)) continue;
            var factionId = side == CampaignBattleSide.Attacker ? source.OwnerFactionId : target.OwnerFactionId;
            if (world.GetFaction(factionId) is not { IsPlayer: false }) continue;
            var relation = world.GetDiplomacyRelation(factionId, player.Id);
            var allied = relation is { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 };
            if (!allied && (relation?.RelationScore ?? 0) < 45) continue;
            var ownTroops = Math.Max(0, side == CampaignBattleSide.Attacker ? attack.TroopsToSend : target.Troops);
            var enemyTroops = Math.Max(0, side == CampaignBattleSide.Attacker ? target.Troops : attack.TroopsToSend);
            if (ownTroops <= 0) continue;
            var ownFood = Math.Max(0, side == CampaignBattleSide.Attacker ? attack.FoodToSend : target.Food);
            var ownGold = Math.Max(0, side == CampaignBattleSide.Attacker ? attack.GoldToSend : target.Gold);
            var troops = allied && ownTroops < enemyTroops ? Math.Min(3000, Math.Max(500, enemyTroops - ownTroops)) : 0;
            var food = (int)Math.Max(0, Math.Min(10000L, ((long)ownTroops * 5 + 99) / 100 * 5) - ownFood);
            var gold = (int)Math.Max(0, Math.Min(5000L, ((long)ownTroops + 99) / 100 * 5) - ownGold);
            if (troops == 0 && food == 0 && gold == 0) continue;
            var envoy = world.Cities.Where(city => city.OwnerFactionId == factionId &&
                    (side != CampaignBattleSide.Defender || city.Id != target.Id))
                .SelectMany(city => city.OfficerIds).Distinct().Where(id => !attack.OfficerIds.Contains(id) &&
                    !attack.AttackOfficerDeployments.Any(deployment => deployment.OfficerId == id))
                .Select(world.GetOfficer).Where(officer => officer != null &&
                    world.GetFaction(factionId)?.OfficerIds.Contains(officer.Id) == true &&
                    world.GetCity(officer.CityId)?.OwnerFactionId == factionId &&
                    officer.CaptiveFactionId <= 0 && (officer.DeathYear <= 0 || world.Year <= officer.DeathYear) &&
                    !BattleCampaignService.IsOfficerCommitted(world, officer.Id) &&
                    !OfficerAvailabilityRules.IsTravelingWithLogistics(world, officer.Id) &&
                    !(officer.LastAssignedYear == world.Year && officer.LastAssignedMonth == world.Month) &&
                    !world.InternalAffairsSchedules.Any(schedule => schedule.State == InternalAffairsScheduleState.Active && schedule.OfficerId == officer.Id))
                .OrderByDescending(officer => officer!.Intelligence + officer.Charm).ThenBy(officer => officer!.Id).FirstOrDefault();
            if (envoy == null || !world.Cities.Any(city => city.OwnerFactionId == player.Id &&
                    BattleCampaignService.GetFriendlyRouteLinks(world, city.Id, target.Id, player.Id) > 0 &&
                    ((troops > 0 && city.Troops > BattleCampaignService.MinimumCityGarrison && city.OfficerIds.Count > 0) ||
                     (food > 0 && city.Food > 0) || (gold > 0 && city.Gold > 0)))) continue;
            envoy.LastAssignedYear = world.Year; envoy.LastAssignedMonth = world.Month; envoy.LastAssignedCommand = CommandType.Diplomacy;
            return new PlayerCampaignSupportRequestData { Side = side, InviterFactionId = factionId, EnvoyOfficerId = envoy.Id,
                RequestedTroops = troops, RequestedGold = gold, RequestedFood = food };
        }
        return null;
    }

    public static bool ValidateResponse(WorldState world, PendingCommandData attack, PlayerCampaignSupportRequestData request)
    {
        var player = world.Factions.FirstOrDefault(faction => faction.IsPlayer);
        var source = world.GetCity(request.SourceCityId);
        var target = world.GetCity(attack.TargetCityId);
        if (player == null || source?.OwnerFactionId != player.Id || target == null ||
            (request.Side == CampaignBattleSide.Defender && world.PrewarAllianceRequests.Any(item =>
                !item.IsPending && item.InviterFactionId == attack.ActorFactionId && item.AllyFactionId == player.Id && item.TargetCityId == attack.TargetCityId)) ||
            BattleCampaignService.GetFriendlyRouteLinks(world, source.Id, target.Id, player.Id) <= 0 ||
            request.Troops < 0 || request.Troops > request.RequestedTroops || request.Gold < 0 || request.Gold > request.RequestedGold ||
            request.Food < 0 || request.Food > request.RequestedFood || request.Gold > source.Gold || request.Food > source.Food ||
            request.Troops + request.Gold + request.Food <= 0) return false;
        var relation = world.GetDiplomacyRelation(request.InviterFactionId, player.Id);
        if (request.Troops > 0 && (relation is not { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 } ||
            request.TroopType == TroopType.Siege || source.GetTroops(request.TroopType) < request.Troops ||
            source.Troops - request.Troops < BattleCampaignService.MinimumCityGarrison || !source.OfficerIds.Contains(request.OfficerId) ||
            world.GetOfficer(request.OfficerId) is not { } officer || officer.CityId != source.Id || !player.OfficerIds.Contains(officer.Id) ||
            officer.CaptiveFactionId > 0 || (officer.DeathYear > 0 && world.Year > officer.DeathYear) ||
            BattleCampaignService.IsOfficerCommitted(world, request.OfficerId) ||
            OfficerAvailabilityRules.IsTravelingWithLogistics(world, request.OfficerId) ||
            (officer.LastAssignedYear == world.Year && officer.LastAssignedMonth == world.Month) ||
            world.InternalAffairsSchedules.Any(schedule => schedule.State == InternalAffairsScheduleState.Active && schedule.OfficerId == request.OfficerId))) return false;
        return true;
    }
}
