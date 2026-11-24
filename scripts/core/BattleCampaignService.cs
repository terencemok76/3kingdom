using static ThreeKingdom.Core.CampaignRetreatService;
using static ThreeKingdom.Core.BattleCampaignCreationService;
using static ThreeKingdom.Core.CampaignDeploymentRules;
using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public enum ReinforcementDispatchFailure
{
    InvalidSource,
    AlreadyDispatchedThisMonth,
    NoFriendlyRoute,
    MinimumGarrison,
    InvalidDeployment,
    InsufficientResources,
    InsufficientSiegeEngines
}

public sealed class ReinforcementDispatchException : InvalidOperationException
{
    public ReinforcementDispatchException(ReinforcementDispatchFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    public ReinforcementDispatchFailure Failure { get; }
}

public static class BattleCampaignService
{
    public const int MaximumBattleDaysPerMonth = 10;
    public const int MaximumActivePiecesPerSide = 12;
    public const int MaximumSupplyCartsPerSide = 1;
    public const int MaximumSiegeEnginesPerSide = 3;
    public const int CaravanEscortTeamMaximumTroops = 1000;
    public const int CaravanBanditRationDays = 5;
    public const int MinimumCityGarrison = 1000;
    public const int DefenderCityWoundedRecoveryPercent = 100;
    public const int AttackerCampWoundedRecoveryPercent = 50;
    public const int PrewarAllianceValidityMonths = 6;

    // A promise is only an incentive, not money already reserved. Trust limits
    // its impact and settlement applies consequences if it is not fulfilled.
    private static int GetPromisedRewardBonus(int gold, int food, int trust)
    {
        var value = Math.Max(0L, gold) + Math.Max(0L, food) / 2;
        var bonus = Math.Min(10L, value / 100);
        return (int)(bonus * Math.Clamp(trust, 0, 100) / 100);
    }

    private static int GetAiAidRiskPenalty(WorldState world, int factionId, int opponentThreat, int routeLinks)
    {
        var distancePenalty = Math.Clamp(routeLinks - 1, 0, 4) * 2;
        var faction = world.GetFaction(factionId);
        var decisionMakers = new[] { faction?.RulerOfficerId ?? 0, faction?.ChiefStrategistOfficerId ?? 0 }
            .Where(id => id > 0 && faction?.OfficerIds.Contains(id) == true)
            .Select(world.GetOfficer)
            .Where(officer => officer != null && officer.CaptiveFactionId <= 0 &&
                (officer.DeathYear <= 0 || world.Year <= officer.DeathYear))
            .Select(officer => Math.Clamp(officer!.Intelligence, 0, 100))
            .DefaultIfEmpty(0).Max();
        var cautionPenalty = Math.Max(0, opponentThreat - 50) * decisionMakers / 1000;
        return distancePenalty + cautionPenalty;
    }

    // AI retains a defensive reserve against the largest unprotected neighbour.
    // This affects voluntary foreign aid only, not the player's legal minimum.
    internal static int GetAiAidGarrison(WorldState world, CityData city)
    {
        var strongest = city.ConnectedCityIds.Select(world.GetCity)
            .Where(neighbor => neighbor != null && neighbor.OwnerFactionId > 0 && neighbor.OwnerFactionId != city.OwnerFactionId)
            .Where(neighbor => world.GetDiplomacyRelation(city.OwnerFactionId, neighbor!.OwnerFactionId) is not
                { RemainingMonths: > 0, Status: DiplomacyStatusType.Alliance or DiplomacyStatusType.Truce })
            .Select(neighbor => Math.Max(0, neighbor!.Troops)).DefaultIfEmpty(0).Max();
        return (int)Math.Min(int.MaxValue, Math.Max((long)MinimumCityGarrison, ((long)strongest + 1) / 2));
    }

    internal static int GetAiAidFoodSurplus(CityData city)
    {
        var fiveDayReserve = ((long)Math.Max(0, city.Troops) * 5 + 99) / 100 * 5;
        return (int)Math.Max(0L, (long)city.Food - fiveDayReserve);
    }

    public static DefenderBattlePlan ChooseDefenderBattlePlan(
        WorldState world,
        CityData targetCity,
        PendingCommandData attack)
    {
        var defenderOfficers = targetCity.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null)
            .Select(officer => officer!)
            .ToList();
        var intelligence = defenderOfficers.Count == 0 ? 0 : (int)defenderOfficers.Average(officer => officer.Intelligence);
        var combat = defenderOfficers.Count == 0 ? 0 : (int)defenderOfficers.Average(officer => officer.Combat);
        var fieldScore = combat + Math.Min(25, targetCity.CavalryTroops / 300) + Math.Min(20, targetCity.Troops / 1000);
        var cityScore = intelligence + targetCity.Defense / 2 + Math.Min(20, targetCity.Troops / 1500);
        if (attack.SiegeEngineAllocation.Ram + attack.SiegeEngineAllocation.Ladder > 0)
        {
            fieldScore += 12;
        }

        return fieldScore > cityScore + 8
            ? DefenderBattlePlan.FieldIntercept
            : DefenderBattlePlan.CityDefense;
    }

    public static ActiveBattleCampaignData CreateCampaign(WorldState world, PendingCommandData attack, DefenderBattlePlan defenderPlan, IReadOnlyList<PendingCommandData>? jointAttacks = null) =>
        BattleCampaignCreationService.CreateCampaign(world, attack, defenderPlan, jointAttacks);

    public static ActiveBattleCampaignData CreateCaravanEscortCampaign(WorldState world, CityData sourceCity, CityData targetCity, TroopAllocationData escort, IReadOnlyCollection<int> officerIds, int gold, int food, int horses, int wood, int metal, int stone, SiegeEngineAllocationData equipment) =>
        BattleCampaignCreationService.CreateCaravanEscortCampaign(world, sourceCity, targetCity, escort, officerIds, gold, food, horses, wood, metal, stone, equipment);
    public static bool IsOfficerCommitted(WorldState world, int officerId) =>
        world.PrewarAllianceRequests.Any(request => request.EnvoyOfficerId == officerId) ||
        world.PendingCommands.Any(command => command.IsAwaitingPrebattleSupport &&
            (command.OfficerIds.Contains(officerId) || command.PlayerSupportRequest?.EnvoyOfficerId == officerId)) ||
        world.ActiveBattleCampaigns.Any(campaign =>
            campaign.Stage != CampaignStage.Resolved &&
            (campaign.Teams.Any(team => team.OfficerId == officerId &&
                                        team.Location is not (CampaignTeamLocation.Eliminated or CampaignTeamLocation.Captured or CampaignTeamLocation.NeighborCity)) ||
             campaign.Reinforcements.Any(order =>
                 (order.Status is ReinforcementStatus.Traveling or ReinforcementStatus.Arrived or ReinforcementStatus.Reserve or ReinforcementStatus.Deployed) &&
                 order.Teams.Any(team => team.OfficerId == officerId)) ||
             campaign.Invitations.Any(invitation =>
                 invitation.Status == BattleInvitationStatus.Pending &&
                 invitation.EnvoyOfficerId == officerId)));

    public static bool IsOfficerWithActiveCampaign(WorldState world, int officerId) =>
        world.ActiveBattleCampaigns.Any(campaign => campaign.Stage != CampaignStage.Resolved &&
            (campaign.Teams.Any(team => team.OfficerId == officerId &&
                team.Location is not (CampaignTeamLocation.Eliminated or CampaignTeamLocation.Captured or CampaignTeamLocation.NeighborCity)) ||
             campaign.Reinforcements.Any(order => order.Status is ReinforcementStatus.Traveling or ReinforcementStatus.Arrived or ReinforcementStatus.Reserve or ReinforcementStatus.Deployed &&
                 order.Teams.Any(team => team.OfficerId == officerId && team.Location is not (CampaignTeamLocation.Eliminated or CampaignTeamLocation.Captured or CampaignTeamLocation.NeighborCity)))));

    public static bool HasFactionRulerInActiveCampaign(WorldState world, int factionId)
    {
        var faction = world.GetFaction(factionId);
        var ruler = faction == null ? null : world.GetOfficer(faction.RulerOfficerId);
        return ruler != null && ruler.CaptiveFactionId <= 0 && (ruler.DeathYear <= 0 || world.Year <= ruler.DeathYear) &&
            faction!.OfficerIds.Contains(ruler.Id) && IsOfficerWithActiveCampaign(world, ruler.Id);
    }

    private static bool IsFactionCommittedToOpposingSide(ActiveBattleCampaignData campaign, int factionId, CampaignBattleSide side) =>
        factionId == (side == CampaignBattleSide.Attacker ? campaign.DefenderFactionId : campaign.AttackerFactionId) ||
        campaign.Invitations.Any(item => item.InvitedFactionId == factionId && item.Side != side &&
            item.Status is BattleInvitationStatus.Pending or BattleInvitationStatus.Accepted) ||
        campaign.Reinforcements.Any(item => item.FactionId == factionId && item.Side != side &&
            item.Status is ReinforcementStatus.Traveling or ReinforcementStatus.Arrived or ReinforcementStatus.Reserve or ReinforcementStatus.Deployed) ||
        campaign.Teams.Any(team => team.FactionId == factionId && team.Side != side &&
            team.Location is not (CampaignTeamLocation.Eliminated or CampaignTeamLocation.Captured or CampaignTeamLocation.NeighborCity));

    public static int GetFriendlyRouteLinks(WorldState world, int sourceCityId, int targetCityId, int factionId) =>
        FindFriendlyRouteLinks(world, sourceCityId, targetCityId, factionId);

    public static bool TrySchedulePrewarAlliance(WorldState world, PrewarAllianceRequestData request)
    {
        var target = world.GetCity(request.TargetCityId);
        var relation = world.GetDiplomacyRelation(request.InviterFactionId, request.AllyFactionId);
        var envoy = world.GetOfficer(request.EnvoyOfficerId);
        var inviter = world.GetFaction(request.InviterFactionId);
        if (envoy == null || inviter == null || !inviter.OfficerIds.Contains(envoy.Id) ||
            world.GetCity(envoy.CityId) is not { } envoyCity || envoyCity.OwnerFactionId != inviter.Id || !envoyCity.OfficerIds.Contains(envoy.Id) ||
            envoy.CaptiveFactionId > 0 || (envoy.DeathYear > 0 && world.Year > envoy.DeathYear) ||
            IsOfficerCommitted(world, envoy.Id) || OfficerAvailabilityRules.IsTravelingWithLogistics(world, envoy.Id) ||
            (envoy.LastAssignedYear == world.Year && envoy.LastAssignedMonth == world.Month) ||
            world.InternalAffairsSchedules.Any(schedule => schedule.State == InternalAffairsScheduleState.Active && schedule.OfficerId == envoy.Id) ||
            target == null || target.OwnerFactionId <= 0 ||
            target.OwnerFactionId == request.InviterFactionId || target.OwnerFactionId == request.AllyFactionId ||
            relation is not { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 } ||
            (request.RequestedTroops <= 0 && request.RequestedGold <= 0 && request.RequestedFood <= 0) ||
            (request.SourceCityId > 0 && (world.GetCity(request.SourceCityId)?.OwnerFactionId != request.AllyFactionId ||
                FindFriendlyRouteLinks(world, request.SourceCityId, request.TargetCityId, request.AllyFactionId) <= 0)) ||
            (request.SourceCityId <= 0 && !world.Cities.Any(city => city.OwnerFactionId == request.AllyFactionId &&
                FindFriendlyRouteLinks(world, city.Id, request.TargetCityId, request.AllyFactionId) > 0)) ||
            world.PrewarAllianceRequests.Any(item => item.InviterFactionId == request.InviterFactionId && item.AllyFactionId == request.AllyFactionId && item.TargetCityId == request.TargetCityId)) return false;
        request.TargetFactionId = target.OwnerFactionId;
        request.IsPending = true;
        world.PrewarAllianceRequests.Add(request);
        return true;
    }

    private static bool ResolvePrewarAlliance(WorldState world, PrewarAllianceRequestData request)
    {
        var target = world.GetCity(request.TargetCityId);
        var relation = world.GetDiplomacyRelation(request.InviterFactionId, request.AllyFactionId);
        var envoy = world.GetOfficer(request.EnvoyOfficerId);
        var inviter = world.GetFaction(request.InviterFactionId);
        if (target == null || target.OwnerFactionId != request.TargetFactionId ||
            relation is not { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 } ||
            envoy == null || inviter == null || !inviter.OfficerIds.Contains(envoy.Id) || envoy.CaptiveFactionId > 0 ||
            (envoy.DeathYear > 0 && world.Year > envoy.DeathYear) ||
            world.GetCity(envoy.CityId) is not { } envoyCity || envoyCity.OwnerFactionId != inviter.Id || !envoyCity.OfficerIds.Contains(envoy.Id) ||
            (request.SourceCityId > 0 && (world.GetCity(request.SourceCityId)?.OwnerFactionId != request.AllyFactionId ||
                FindFriendlyRouteLinks(world, request.SourceCityId, request.TargetCityId, request.AllyFactionId) <= 0)) ||
            (request.SourceCityId <= 0 && !world.Cities.Any(city => city.OwnerFactionId == request.AllyFactionId &&
                FindFriendlyRouteLinks(world, city.Id, request.TargetCityId, request.AllyFactionId) > 0))) return false;
        var trust = DiplomacyScoreRules.GetTrustScore(relation, request.AllyFactionId, request.InviterFactionId);
        var enemyRelation = world.GetDiplomacyRelation(request.AllyFactionId, target.OwnerFactionId);
        var threat = DiplomacyScoreRules.CalculateThreatScore(world, request.AllyFactionId, target.OwnerFactionId, enemyRelation);
        var safeTroops = world.Cities.Where(c => c.OwnerFactionId == request.AllyFactionId && (request.SourceCityId <= 0 || c.Id == request.SourceCityId))
            .Where(c => FindFriendlyRouteLinks(world, c.Id, request.TargetCityId, request.AllyFactionId) > 0)
            .Select(c => Math.Max(0, c.Troops - MinimumCityGarrison)).DefaultIfEmpty(0).Max();
        var score = relation.RelationScore + (trust - 50) / 2 + 35 + Math.Min(25, safeTroops / 400) +
                    Math.Min(20, (envoy.Intelligence + envoy.Charm) / 10) - threat / 5 +
                    GetPromisedRewardBonus(request.PromisedGold, request.PromisedFood, trust) -
                    GetAiAidRiskPenalty(world, request.AllyFactionId, threat, request.SourceCityId > 0
                        ? FindFriendlyRouteLinks(world, request.SourceCityId, request.TargetCityId, request.AllyFactionId) : 1);
        if (score < 45) return false;
        request.IsPending = false;
        request.AcceptedYear = world.Year;
        request.AcceptedMonth = world.Month;
        return true;
    }

    public static void ResolvePendingPrewarAlliances(WorldState world)
    {
        foreach (var request in world.PrewarAllianceRequests.Where(item => item.IsPending).ToList())
            if (!ResolvePrewarAlliance(world, request)) world.PrewarAllianceRequests.Remove(request);
    }

    public static int GetPrewarAllianceMonthsRemaining(WorldState world, PrewarAllianceRequestData request)
    {
        if (request.AcceptedMonth is < 1 or > 12) return PrewarAllianceValidityMonths;
        var elapsed = ((long)world.Year - request.AcceptedYear) * 12 + world.Month - request.AcceptedMonth;
        return (int)Math.Clamp(PrewarAllianceValidityMonths - elapsed, 0L, PrewarAllianceValidityMonths);
    }

    public static void ExpirePrewarAlliances(WorldState world)
    {
        foreach (var request in world.PrewarAllianceRequests.Where(item => !item.IsPending && item.AcceptedMonth is < 1 or > 12))
        {
            request.AcceptedYear = world.Year;
            request.AcceptedMonth = world.Month;
        }
        world.PrewarAllianceRequests.RemoveAll(request => !request.IsPending && GetPrewarAllianceMonthsRemaining(world, request) == 0);
    }

    public static void DispatchPrewarAllies(WorldState world, ActiveBattleCampaignData campaign)
    {
        var requests = world.PrewarAllianceRequests.Where(item => !item.IsPending &&
            item.InviterFactionId == campaign.AttackerFactionId && item.TargetCityId == campaign.TargetCityId &&
            (item.AcceptedYear < campaign.StartedYear || item.AcceptedYear == campaign.StartedYear && item.AcceptedMonth <= campaign.StartedMonth)).ToList();
        foreach (var request in requests)
        {
            world.PrewarAllianceRequests.Remove(request);
            var relation = world.GetDiplomacyRelation(request.InviterFactionId, request.AllyFactionId);
            if (IsFactionCommittedToOpposingSide(campaign, request.AllyFactionId, CampaignBattleSide.Attacker))
            {
                campaign.Invitations.Add(new BattleInvitationData
                {
                    Id = campaign.Invitations.Count == 0 ? 1 : campaign.Invitations.Max(item => item.Id) + 1,
                    CampaignId = campaign.Id, InviterFactionId = request.InviterFactionId,
                    InvitedFactionId = request.AllyFactionId, RequestedSourceCityId = request.SourceCityId,
                    Side = CampaignBattleSide.Attacker, EnvoyOfficerId = request.EnvoyOfficerId,
                    SupportType = GetRequestedSupportType(request.RequestedTroops, request.RequestedGold, request.RequestedFood),
                    Status = BattleInvitationStatus.Declined, DecisionReason = "Circumstances changed."
                });
                continue;
            }
            var invitation = CreateInvitation(campaign, request.InviterFactionId, request.AllyFactionId,
                CampaignBattleSide.Attacker, GetRequestedSupportType(request.RequestedTroops, request.RequestedGold, request.RequestedFood), request.EnvoyOfficerId,
                request.RequestedTroops, request.RequestedGold, request.RequestedFood, request.SourceCityId, request.PromisedGold, request.PromisedFood);
            if (world.GetCity(request.TargetCityId)?.OwnerFactionId != request.TargetFactionId ||
                relation is not { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 })
            {
                invitation.Status = BattleInvitationStatus.Declined;
                invitation.DecisionReason = "Circumstances changed.";
            }
            else ScheduleInvitationResponse(world, campaign, invitation);
        }
    }

    public static BattleInvitationSupportType GetRequestedSupportType(int troops, int gold, int food) =>
        troops <= 0 ? BattleInvitationSupportType.Resources :
        gold > 0 || food > 0 ? BattleInvitationSupportType.FullSupport : BattleInvitationSupportType.Troops;

    public static ReinforcementOrderData? GetInvitationReinforcement(ActiveBattleCampaignData campaign, BattleInvitationData invitation) =>
        campaign.Reinforcements.FirstOrDefault(order => order.InvitationId == invitation.Id && invitation.Id > 0 &&
            order.FactionId == invitation.InvitedFactionId && order.Side == invitation.Side);

    public static void ScheduleInvitationResponse(WorldState world, ActiveBattleCampaignData campaign, BattleInvitationData invitation)
    {
        if (invitation.Status != BattleInvitationStatus.Pending) return;
        invitation.ResponseBattleDays = GetInvitationResponseDays(world, campaign.TargetCityId, invitation.InvitedFactionId,
            invitation.RequestedSourceCityId, invitation.EnvoyOfficerId);
        invitation.DecisionReason = "Awaiting envoy response.";
    }

    public static int GetInvitationResponseDays(WorldState world, int targetCityId, int invitedFactionId, int sourceCityId, int envoyOfficerId)
    {
        var envoy = world.GetOfficer(envoyOfficerId);
        var skill = Math.Clamp((envoy?.Intelligence ?? 50) + (envoy?.Charm ?? 50), 0, 200);
        var baseDays = skill >= 160 ? 1 : skill >= 100 ? 2 : 3;
        var links = sourceCityId > 0
            ? FindFriendlyRouteLinks(world, sourceCityId, targetCityId, invitedFactionId)
            : 1;
        return baseDays + Math.Clamp(links - 1, 0, 2);
    }

    public static bool DeclineUnavailableInvitationEnvoy(WorldState world, ActiveBattleCampaignData campaign, BattleInvitationData invitation)
    {
        if (campaign.Stage == CampaignStage.Resolved || invitation.Status != BattleInvitationStatus.Pending || invitation.EnvoyOfficerId <= 0)
            return false;
        var envoy = world.GetOfficer(invitation.EnvoyOfficerId);
        var faction = world.GetFaction(invitation.InviterFactionId);
        if (envoy != null && faction?.OfficerIds.Contains(envoy.Id) == true && envoy.CaptiveFactionId <= 0 &&
            (envoy.DeathYear <= 0 || world.Year <= envoy.DeathYear)) return false;

        invitation.Status = BattleInvitationStatus.Declined;
        invitation.DecisionReason = "Envoy is no longer available.";
        ReturnDeclinedEnvoy(world, campaign, invitation);
        return true;
    }

    public static void AdvanceInvitationResponses(WorldState world, ActiveBattleCampaignData campaign, int elapsedDays = 1)
    {
        if (campaign.Stage == CampaignStage.Resolved || elapsedDays <= 0) return;
        foreach (var invitation in campaign.Invitations.Where(item => item.Status == BattleInvitationStatus.Pending).ToList())
        {
            var remainingBefore = invitation.ResponseBattleDays;
            invitation.ResponseBattleDays = Math.Max(0, invitation.ResponseBattleDays - elapsedDays);
            if (invitation.ResponseBattleDays == 0 && DeclineUnavailableInvitationEnvoy(world, campaign, invitation)) continue;
            if (invitation.ResponseBattleDays == 0 && world.GetFaction(invitation.InvitedFactionId)?.IsPlayer != true)
            {
                var previousCount = campaign.Reinforcements.Count;
                if (ResolveAiInvitation(world, campaign, invitation) && elapsedDays > remainingBefore)
                {
                    // A siege preparation interval can contain travel after the
                    // reply; only newly dispatched orders receive those days.
                    foreach (var order in campaign.Reinforcements.Skip(previousCount))
                    {
                        order.RemainingBattleDays = Math.Max(0, order.RemainingBattleDays - (elapsedDays - remainingBefore));
                        if (order.RemainingBattleDays != 0) continue;
                        order.Status = ReinforcementStatus.Arrived;
                        foreach (var team in order.Teams) team.Location = CampaignTeamLocation.Reserve;
                    }
                }
                if (invitation.Status == BattleInvitationStatus.Declined)
                    ReturnDeclinedEnvoy(world, campaign, invitation);
            }
        }
        FillReturningEnvoys(campaign);
    }

    public static void ReturnDeclinedEnvoy(WorldState world, ActiveBattleCampaignData campaign, BattleInvitationData invitation)
    {
        var officer = world.GetOfficer(invitation.EnvoyOfficerId);
        var faction = world.GetFaction(invitation.InviterFactionId);
        if (campaign.Stage == CampaignStage.Resolved || invitation.Status is not (BattleInvitationStatus.Declined or BattleInvitationStatus.Accepted) ||
            officer == null || faction == null || !faction.OfficerIds.Contains(officer.Id) || officer.CaptiveFactionId > 0 ||
            (officer.DeathYear > 0 && world.Year > officer.DeathYear) ||
            campaign.Teams.Concat(campaign.Reinforcements.SelectMany(order => order.Teams)).Any(team => team.OfficerId == officer.Id)) return;

        campaign.Teams.Add(new CampaignBattleTeamData
        {
            Id = campaign.Teams.Count == 0 ? 1 : campaign.Teams.Max(team => team.Id) + 1,
            FactionId = faction.Id, Side = invitation.Side,
            ControllerType = faction.IsPlayer ? CampaignControllerType.Player : CampaignControllerType.Ai,
            OfficerId = officer.Id, OriginCityId = officer.CityId,
            ReorganizationCapacity = CampaignReorganizationRules.GetOfficerCapacity(officer),
            Location = CampaignTeamLocation.Reserve, AwaitingEnvoyReserveTroops = true
        });
        FillReturningEnvoys(campaign);
    }

    private static void FillReturningEnvoys(ActiveBattleCampaignData campaign)
    {
        foreach (var envoy in campaign.Teams.Where(team => team.AwaitingEnvoyReserveTroops && team.Location == CampaignTeamLocation.Reserve).ToList())
        {
            if (envoy.ActiveTroops > 0) { envoy.AwaitingEnvoyReserveTroops = false; continue; }
            var donors = CampaignReorganizationRules.GetEligibleTeams(campaign, envoy.FactionId, envoy.ControllerType)
                .Where(team => team != envoy && team.Side == envoy.Side && team.Location == CampaignTeamLocation.Reserve && team.ActiveTroops > 0)
                .OrderByDescending(team => team.ActiveTroops).ThenBy(team => team.Id).ToList();
            if (donors.Count == 0) continue;
            envoy.TroopType = donors[0].TroopType;
            var remaining = Math.Min(1000, CampaignReorganizationRules.GetCapacity(envoy));
            foreach (var donor in donors.Where(team => team.TroopType == envoy.TroopType))
            {
                var amount = Math.Min(remaining, Math.Min(donor.ActiveTroops, donor.MaximumTroops));
                if (amount > 0 && CampaignReorganizationRules.TryTransfer(campaign, envoy.FactionId, donor, envoy, amount, 0, envoy.ControllerType, out _))
                    remaining -= amount;
                if (remaining <= 0) break;
            }
            if (envoy.ActiveTroops > 0) envoy.AwaitingEnvoyReserveTroops = false;
        }
    }

    public static ReinforcementOrderData DispatchReinforcement(
        WorldState world,
        ActiveBattleCampaignData campaign,
        int sourceCityId,
        CampaignBattleSide side,
        IReadOnlyList<AttackOfficerDeploymentData> deployments,
        int gold,
        int food,
        CampaignSupplyOwnership supplyOwnership = CampaignSupplyOwnership.MainFaction)
    {
        if (campaign.IsCaravanEscortBattle)
        {
            throw new ReinforcementDispatchException(ReinforcementDispatchFailure.InvalidDeployment,
                "Caravan escort battles do not accept reinforcements.");
        }
        var sourceCity = world.GetCity(sourceCityId) ??
            throw new ReinforcementDispatchException(ReinforcementDispatchFailure.InvalidSource, "Reinforcement source city is missing.");
        if (world.ActiveBattleCampaigns.Any(other => other.Id != campaign.Id && other.Stage != CampaignStage.Resolved &&
            (other.SourceCityId == sourceCityId || other.TargetCityId == sourceCityId)))
            throw new ReinforcementDispatchException(ReinforcementDispatchFailure.InvalidSource, "The source city is participating in another campaign.");
        if (IsFactionCommittedToOpposingSide(campaign, sourceCity.OwnerFactionId, side))
            throw new ReinforcementDispatchException(ReinforcementDispatchFailure.InvalidSource, "The faction is committed to the opposing side.");
        var expectedFactionId = side == CampaignBattleSide.Attacker
            ? campaign.AttackerFactionId
            : campaign.DefenderFactionId;
        if (supplyOwnership == CampaignSupplyOwnership.MainFaction && sourceCity.OwnerFactionId != expectedFactionId)
        {
            throw new ReinforcementDispatchException(ReinforcementDispatchFailure.InvalidSource, "Domestic reinforcements must come from the main faction.");
        }

        if (campaign.Reinforcements.Any(order =>
                order.SourceCityId == sourceCityId &&
                order.DispatchYear == world.Year &&
                order.DispatchMonth == world.Month &&
                order.Status != ReinforcementStatus.Cancelled))
        {
            throw new ReinforcementDispatchException(ReinforcementDispatchFailure.AlreadyDispatchedThisMonth, "This city has already reinforced the campaign this month.");
        }

        var routeLinks = FindFriendlyRouteLinks(world, sourceCity.Id, campaign.TargetCityId, sourceCity.OwnerFactionId);
        if (routeLinks <= 0)
        {
            throw new ReinforcementDispatchException(ReinforcementDispatchFailure.NoFriendlyRoute, "No valid reinforcement route reaches the battlefield.");
        }

        var normalizedDeployments = deployments
            .Where(item => item.TroopCount > 0 && sourceCity.OfficerIds.Contains(item.OfficerId))
            .GroupBy(item => item.OfficerId)
            .Select(group => group.First())
            .Select(CloneDeployment)
            .ToList();
        var allocation = BuildTroopAllocation(normalizedDeployments);
        if (allocation.Total > 0 && sourceCity.Troops - allocation.Total < MinimumCityGarrison)
        {
            throw new ReinforcementDispatchException(ReinforcementDispatchFailure.MinimumGarrison, "The source city must retain its minimum garrison.");
        }

        ValidateCityCanSupply(sourceCity, normalizedDeployments, allocation, gold, food);
        var orderId = campaign.Reinforcements.Count == 0
            ? 1
            : campaign.Reinforcements.Max(item => item.Id) + 1;
        var playerFactionId = world.Factions.FirstOrDefault(faction => faction.IsPlayer)?.Id ?? -1;
        var order = new ReinforcementOrderData
        {
            Id = orderId,
            CampaignId = campaign.Id,
            FactionId = sourceCity.OwnerFactionId,
            Side = side,
            SourceCityId = sourceCity.Id,
            TargetCityId = campaign.TargetCityId,
            DispatchYear = world.Year,
            DispatchMonth = world.Month,
            RouteLinks = routeLinks,
            RemainingBattleDays = routeLinks * 2,
            Gold = Math.Clamp(gold, 0, sourceCity.Gold),
            Food = Math.Clamp(food, 0, sourceCity.Food),
            SupplyOwnership = supplyOwnership,
            Status = ReinforcementStatus.Traveling
        };
        order.Teams.AddRange(CreateTeams(
            normalizedDeployments,
            sourceCity.OwnerFactionId,
            side,
            playerFactionId,
            CampaignTeamLocation.Traveling,
            orderId,
            sourceCity.Id));

        sourceCity.RemoveTroopAllocation(allocation);
        sourceCity.RemoveSiegeEngineAllocation(BuildSiegeEngineAllocation(normalizedDeployments));
        sourceCity.Gold -= order.Gold;
        sourceCity.Food -= order.Food;
        foreach (var officerId in normalizedDeployments.Select(item => item.OfficerId))
        {
            if (world.GetOfficer(officerId) is { } officer)
            {
                officer.LastAssignedYear = world.Year;
                officer.LastAssignedMonth = world.Month;
                officer.LastAssignedCommand = CommandType.Attack;
            }
        }

        campaign.Reinforcements.Add(order);
        UpsertParticipant(campaign, sourceCity.OwnerFactionId, side, playerFactionId, order);
        if (allocation.Total > 0 && sourceCity.OwnerFactionId != expectedFactionId)
        {
            var opposingFactionId = side == CampaignBattleSide.Attacker ? campaign.DefenderFactionId : campaign.AttackerFactionId;
            var participant = campaign.Participants.First(item => item.FactionId == sourceCity.OwnerFactionId && item.Side == side);
            if (opposingFactionId > 0 && opposingFactionId != sourceCity.OwnerFactionId && !participant.ForeignHostilityApplied)
            {
                if (!CommandDiplomacyRules.TryBreakDiplomacyBlockForAttack(world, sourceCity.OwnerFactionId, opposingFactionId))
                {
                    var relation = CommandDiplomacyRules.GetOrCreateDiplomacyRelation(world, sourceCity.OwnerFactionId, opposingFactionId);
                    relation.RelationScore = Math.Clamp(relation.RelationScore - 18, -100, 100);
                    DiplomacyScoreRules.AdjustTrust(relation, opposingFactionId, -35);
                    relation.LastUpdatedYear = world.Year;
                    relation.LastUpdatedMonth = world.Month;
                }
                participant.ForeignHostilityApplied = true;
            }
        }
        return order;
    }

    public static BattleInvitationData CreateInvitation(
        ActiveBattleCampaignData campaign,
        int inviterFactionId,
        int invitedFactionId,
        CampaignBattleSide side,
        BattleInvitationSupportType supportType,
        int envoyOfficerId,
        int requestedTroops,
        int requestedGold,
        int requestedFood,
        int requestedSourceCityId = 0,
        int promisedGold = 0,
        int promisedFood = 0)
    {
        if (IsFactionCommittedToOpposingSide(campaign, invitedFactionId, side))
        {
            throw new ReinforcementDispatchException(ReinforcementDispatchFailure.InvalidSource, "The invited faction is already committed to the opposing side.");
        }

        var invitation = new BattleInvitationData
        {
            Id = campaign.Invitations.Count == 0 ? 1 : campaign.Invitations.Max(item => item.Id) + 1,
            CampaignId = campaign.Id,
            InviterFactionId = inviterFactionId,
            InvitedFactionId = invitedFactionId,
            RequestedSourceCityId = requestedSourceCityId,
            Side = side,
            SupportType = supportType,
            EnvoyOfficerId = envoyOfficerId,
            RequestedTroops = Math.Max(0, requestedTroops),
            RequestedGold = Math.Max(0, requestedGold),
            RequestedFood = Math.Max(0, requestedFood),
            PromisedGold = Math.Max(0, promisedGold),
            PromisedFood = Math.Max(0, promisedFood),
            ResponseBattleDays = 1
        };
        campaign.Invitations.Add(invitation);
        return invitation;
    }

    public static bool ResolveAiInvitation(WorldState world, ActiveBattleCampaignData campaign, BattleInvitationData invitation)
    {
        if (campaign.Stage == CampaignStage.Resolved || invitation.Status != BattleInvitationStatus.Pending ||
            world.GetFaction(invitation.InvitedFactionId)?.IsPlayer == true) return false;
        if (DeclineUnavailableInvitationEnvoy(world, campaign, invitation)) return false;
        var relation = world.GetDiplomacyRelation(invitation.InviterFactionId, invitation.InvitedFactionId);
        var isAlliance = relation is { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 };
        var resourcesOnly = invitation.SupportType == BattleInvitationSupportType.Resources;
        if (!isAlliance && !resourcesOnly)
        {
            invitation.Status = BattleInvitationStatus.Declined;
            invitation.DecisionReason = "No active alliance permits troop support.";
            return false;
        }

        var candidateCities = world.Cities
            .Where(city => city.OwnerFactionId == invitation.InvitedFactionId)
            .Where(city => invitation.RequestedSourceCityId <= 0 || city.Id == invitation.RequestedSourceCityId)
            .Where(city => !world.ActiveBattleCampaigns.Any(other => other.Id != campaign.Id && other.Stage != CampaignStage.Resolved &&
                (other.SourceCityId == city.Id || other.TargetCityId == city.Id)))
            .Where(city => !campaign.Reinforcements.Any(order => order.SourceCityId == city.Id && order.DispatchYear == world.Year &&
                order.DispatchMonth == world.Month && order.Status != ReinforcementStatus.Cancelled))
            .Select(city => (City: city, Links: FindFriendlyRouteLinks(world, city.Id, campaign.TargetCityId, invitation.InvitedFactionId)))
            .Where(item => item.Links > 0)
            .OrderByDescending(item => resourcesOnly || (item.City.Troops > GetAiAidGarrison(world, item.City) && CreateAiAllianceReinforcementDeployments(world, item.City, invitation.RequestedTroops).Count > 0))
            .ThenBy(item => item.City.ConnectedCityIds.Count(id => world.GetCity(id) is { OwnerFactionId: > 0 } neighbor && neighbor.OwnerFactionId != invitation.InvitedFactionId && neighbor.OwnerFactionId != invitation.InviterFactionId))
            .ThenBy(item => item.Links)
            .ThenByDescending(item => item.City.Troops)
            .ToList();
        var source = candidateCities.FirstOrDefault();
        var relationScore = relation?.RelationScore ?? 0;
        var trustScore = DiplomacyScoreRules.GetTrustScore(relation, invitation.InvitedFactionId, invitation.InviterFactionId);
        var opposingFactionId = invitation.Side == CampaignBattleSide.Attacker ? campaign.DefenderFactionId : campaign.AttackerFactionId;
        var opposingRelation = world.GetDiplomacyRelation(invitation.InvitedFactionId, opposingFactionId);
        var threatScore = DiplomacyScoreRules.CalculateThreatScore(world, invitation.InvitedFactionId, opposingFactionId, opposingRelation);
        var envoy = world.GetOfficer(invitation.EnvoyOfficerId);
        var envoyBonus = envoy == null ? 0 : Math.Min(20, (envoy.Intelligence + envoy.Charm) / 10);
        var safeTroops = source.City == null ? 0 : Math.Max(0, source.City.Troops - GetAiAidGarrison(world, source.City));
        var acceptanceScore = relationScore + (trustScore - 50) / 2 + (isAlliance ? 35 : 0) + Math.Min(25, safeTroops / 400) + envoyBonus - threatScore / 5 +
                              GetPromisedRewardBonus(invitation.PromisedGold, invitation.PromisedFood, trustScore) -
                              GetAiAidRiskPenalty(world, invitation.InvitedFactionId, threatScore, source.City == null ? 1 : source.Links);
        var troopDeployments = source.City == null || resourcesOnly
            ? new List<AttackOfficerDeploymentData>()
            : CreateAiAllianceReinforcementDeployments(world, source.City, invitation.RequestedTroops);
        var canSendRequestedTroops = resourcesOnly ||
            (safeTroops > 0 && troopDeployments.Count > 0 && acceptanceScore >= 45 && GetAiAidFoodSurplus(source.City!) > 0 &&
             !CommandDiplomacyRules.HasActiveDiplomacyBlock(world, invitation.InvitedFactionId, opposingFactionId));
        if (source.City == null || acceptanceScore < 45)
        {
            invitation.Status = BattleInvitationStatus.Declined;
            invitation.DecisionReason = source.City == null
                ? "No eligible source city or friendly route is available."
                : "The faction cannot safely spare the requested support.";
            return false;
        }

        // An ally that cannot safely retain its garrison sends a deliberately
        // modest gift rather than treating supplies as a full expedition.
        if (!canSendRequestedTroops)
        {
            var fallbackGold = invitation.RequestedGold > 0
                ? Math.Min(invitation.RequestedGold, Math.Max(0, source.City.Gold / 6))
                : Math.Min(100, Math.Max(0, source.City.Gold / 10));
            var fallbackFood = invitation.RequestedFood > 0
                ? Math.Min(invitation.RequestedFood, Math.Min(GetAiAidFoodSurplus(source.City), Math.Max(0, source.City.Food / 6)))
                : Math.Min(300, Math.Min(GetAiAidFoodSurplus(source.City), Math.Max(0, source.City.Food / 10)));
            if (fallbackGold <= 0 && fallbackFood <= 0)
            {
                invitation.Status = BattleInvitationStatus.Declined;
                invitation.DecisionReason = "The faction cannot safely spare troops or supplies.";
                return false;
            }

            try
            {
                var fallbackOrder = DispatchReinforcement(world, campaign, source.City.Id, invitation.Side,
                    Array.Empty<AttackOfficerDeploymentData>(), fallbackGold, fallbackFood,
                    CampaignSupplyOwnership.Gift);
                fallbackOrder.InvitationId = invitation.Id;
                invitation.Status = BattleInvitationStatus.Accepted;
                invitation.DecisionReason = "Troop support unavailable; limited supplies sent.";
                ReturnDeclinedEnvoy(world, campaign, invitation);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                invitation.Status = BattleInvitationStatus.Declined;
                invitation.DecisionReason = ex.Message;
                return false;
            }
        }

        invitation.Status = BattleInvitationStatus.Accepted;
        invitation.DecisionReason = "Support accepted.";
        var deployments = invitation.SupportType == BattleInvitationSupportType.Resources
            ? new List<AttackOfficerDeploymentData>()
            : troopDeployments;
        var envoyLoanedInfantry = DetermineAiEnvoyLoanedInfantry(world, campaign, invitation, source.City);
        if (invitation.RequestedTroops > 0)
        {
            // Leave at least one soldier for an allied officer so dispatch
            // validation can reserve the order before adding the envoy's loan.
            envoyLoanedInfantry = Math.Min(envoyLoanedInfantry, Math.Max(0, safeTroops - 1));
            LimitDeploymentTroops(deployments, Math.Min(safeTroops - envoyLoanedInfantry,
                Math.Max(1, invitation.RequestedTroops - envoyLoanedInfantry)));
        }
        var allocation = BuildTroopAllocation(deployments);
        envoyLoanedInfantry = Math.Min(envoyLoanedInfantry, Math.Max(0, source.City.InfantryTroops - allocation.Infantry));
        envoyLoanedInfantry = Math.Min(envoyLoanedInfantry, Math.Max(0, source.City.Troops - allocation.Total - MinimumCityGarrison));

        var gold = Math.Min(invitation.RequestedGold, Math.Max(0, source.City.Gold / 3));
        var food = Math.Min(invitation.RequestedFood, Math.Min(GetAiAidFoodSurplus(source.City), Math.Max(0, source.City.Food / 3)));
        try
        {
            var order = DispatchReinforcement(
                world,
                campaign,
                source.City.Id,
                invitation.Side,
                deployments,
                gold,
                food,
                invitation.SupportType == BattleInvitationSupportType.Resources
                    ? CampaignSupplyOwnership.Gift
                    : CampaignSupplyOwnership.Expedition);
            order.InvitationId = invitation.Id;
            AttachAiEnvoyLoanedInfantry(world, campaign, invitation, source.City, order, envoyLoanedInfantry);
        }
        catch (InvalidOperationException ex)
        {
            invitation.Status = BattleInvitationStatus.Declined;
            invitation.DecisionReason = ex.Message;
            return false;
        }
        return true;
    }

    private static int DetermineAiEnvoyLoanedInfantry(WorldState world, ActiveBattleCampaignData campaign, BattleInvitationData invitation, CityData sourceCity)
    {
        var envoy = world.GetOfficer(invitation.EnvoyOfficerId);
        var defenderCity = world.GetCity(campaign.TargetCityId);
        if (envoy == null || defenderCity == null || !defenderCity.OfficerIds.Contains(envoy.Id) || invitation.RequestedTroops <= 1)
        {
            return 0;
        }

        // The allied ruler decides the loan without an extra player input.
        var desired = Math.Min(invitation.RequestedTroops - 1,
            Math.Max(1, invitation.RequestedTroops / 4) + Math.Max(0, envoy.Intelligence + envoy.Charm - 100) * 2);
        var safeInfantry = Math.Max(0, Math.Min(sourceCity.InfantryTroops, sourceCity.Troops - MinimumCityGarrison));
        return Math.Min(desired, safeInfantry);
    }

    private static List<AttackOfficerDeploymentData> CreateAiAllianceReinforcementDeployments(WorldState world, CityData sourceCity, int requestedTroops)
    {
        var teamLimit = requestedTroops <= 400 ? 1 : requestedTroops <= 1000 ? 2 : 3;
        var selectedOfficers = sourceCity.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null && officer.CityId == sourceCity.Id && officer.CaptiveFactionId <= 0 &&
                (officer.DeathYear <= 0 || world.Year <= officer.DeathYear) &&
                world.GetFaction(sourceCity.OwnerFactionId)?.OfficerIds.Contains(officer.Id) == true &&
                !IsOfficerCommitted(world, officer.Id) &&
                !OfficerAvailabilityRules.IsTravelingWithLogistics(world, officer.Id) &&
                !(officer.LastAssignedYear == world.Year && officer.LastAssignedMonth == world.Month) &&
                !world.InternalAffairsSchedules.Any(schedule => schedule.State == InternalAffairsScheduleState.Active && schedule.OfficerId == officer.Id))
            .Cast<OfficerData>()
            .OrderByDescending(officer => officer.Leadership + officer.Combat)
            .ThenByDescending(officer => officer.Intelligence)
            .Take(teamLimit)
            .Select(officer => officer.Id)
            .ToHashSet();
        var excludedOfficers = sourceCity.OfficerIds.Where(id => !selectedOfficers.Contains(id)).ToHashSet();
        return CreateDefaultDefenderDeployments(sourceCity, 25, excludedOfficerIds: excludedOfficers);
    }

    private static void AttachAiEnvoyLoanedInfantry(WorldState world, ActiveBattleCampaignData campaign, BattleInvitationData invitation, CityData sourceCity, ReinforcementOrderData order, int troopCount)
    {
        if (troopCount <= 0 || invitation.EnvoyOfficerId <= 0)
        {
            return;
        }

        sourceCity.InfantryTroops -= troopCount;
        order.Teams.Add(new CampaignBattleTeamData
        {
            Id = order.Teams.Count + 1,
            FactionId = sourceCity.OwnerFactionId,
            Side = invitation.Side,
            ControllerType = world.Factions.Any(faction => faction.IsPlayer && faction.OfficerIds.Contains(invitation.EnvoyOfficerId))
                ? CampaignControllerType.Player : CampaignControllerType.Ai,
            OfficerId = invitation.EnvoyOfficerId,
            TroopType = TroopType.Infantry,
            ActiveTroops = troopCount,
            MaximumTroops = troopCount,
            ReinforcementOrderId = order.Id,
            OriginCityId = sourceCity.Id,
            OfficerReturnCityId = campaign.TargetCityId,
            Location = CampaignTeamLocation.Traveling,
            CooperationObjective = "allied-envoy-loan"
        });
        var participant = campaign.Participants.FirstOrDefault(item => item.FactionId == sourceCity.OwnerFactionId && item.Side == invitation.Side);
        if (participant != null)
        {
            participant.TroopsCommitted += troopCount;
        }
        if (world.GetOfficer(invitation.EnvoyOfficerId) is { } envoy)
        {
            envoy.LastAssignedYear = world.Year;
            envoy.LastAssignedMonth = world.Month;
            envoy.LastAssignedCommand = CommandType.Attack;
        }
    }

    public static void DispatchDefenseReinforcementRequests(
        WorldState world,
        ActiveBattleCampaignData campaign,
        PendingCommandData attack)
    {
        void RejectForeignRequest(DefenseReinforcementRequestData request, int factionId, string reason)
        {
            var rejected = new BattleInvitationData
            {
                Id = campaign.Invitations.Count == 0 ? 1 : campaign.Invitations.Max(item => item.Id) + 1,
                CampaignId = campaign.Id, InviterFactionId = campaign.DefenderFactionId,
                InvitedFactionId = factionId, RequestedSourceCityId = request.SourceCityId,
                Side = CampaignBattleSide.Defender, EnvoyOfficerId = request.EnvoyOfficerId,
                RequestedTroops = request.RequestedTroops, RequestedGold = request.RequestedGold, RequestedFood = request.RequestedFood,
                PromisedGold = request.PromisedGold, PromisedFood = request.PromisedFood,
                SupportType = GetRequestedSupportType(request.RequestedTroops, request.RequestedGold, request.RequestedFood),
                Status = BattleInvitationStatus.Declined, DecisionReason = reason
            };
            campaign.Invitations.Add(rejected);
            ReturnDeclinedEnvoy(world, campaign, rejected);
        }

        foreach (var request in attack.DefenseReinforcementRequests)
        {
            var source = world.GetCity(request.SourceCityId);
            var requestedFactionId = request.RequestedFactionId > 0 ? request.RequestedFactionId : source?.OwnerFactionId ?? 0;
            if (request.IsAllianceRequest && (source == null || source.Id == campaign.TargetCityId ||
                requestedFactionId <= 0 || source.OwnerFactionId != requestedFactionId ||
                requestedFactionId == campaign.DefenderFactionId || requestedFactionId == campaign.AttackerFactionId ||
                (request.RequestedTroops <= 0 && request.RequestedGold <= 0 && request.RequestedFood <= 0) ||
                FindFriendlyRouteLinks(world, source.Id, campaign.TargetCityId, requestedFactionId) <= 0 ||
                world.ActiveBattleCampaigns.Any(other => other.Id != campaign.Id && other.Stage != CampaignStage.Resolved &&
                    (other.SourceCityId == source.Id || other.TargetCityId == source.Id))))
            {
                RejectForeignRequest(request, requestedFactionId, "The requested source is no longer available.");
                continue;
            }
            try
            {
                if (source == null || source.Id == campaign.TargetCityId)
                {
                    continue;
                }

                if (!request.IsAllianceRequest && source.OwnerFactionId == campaign.DefenderFactionId)
                {
                    DispatchReinforcement(world, campaign, source.Id, CampaignBattleSide.Defender,
                        request.Deployments, request.RequestedGold, request.RequestedFood);
                    continue;
                }

                if (!request.IsAllianceRequest || source.OwnerFactionId == campaign.DefenderFactionId || source.OwnerFactionId == campaign.AttackerFactionId ||
                    (request.RequestedTroops <= 0 && request.RequestedGold <= 0 && request.RequestedFood <= 0))
                {
                    continue;
                }

                var invitation = CreateInvitation(campaign, campaign.DefenderFactionId,
                    requestedFactionId, CampaignBattleSide.Defender,
                    GetRequestedSupportType(request.RequestedTroops, request.RequestedGold, request.RequestedFood), request.EnvoyOfficerId, request.RequestedTroops, request.RequestedGold, request.RequestedFood, source.Id,
                    request.PromisedGold, request.PromisedFood);
                ScheduleInvitationResponse(world, campaign, invitation);
            }
            catch (ReinforcementDispatchException)
            {
                if (request.IsAllianceRequest)
                    RejectForeignRequest(request, requestedFactionId, "The faction is committed to the opposing side.");
                // A source can become unavailable while another pending command
                // resolves.  Preserve all other defense requests.
            }
        }
    }

    public static void AdvanceCompletedBattleDay(ActiveBattleCampaignData campaign, WorldState? world = null)
    {
        campaign.BattleDaysThisMonth++;
        campaign.TotalBattleDays++;
        foreach (var participant in campaign.Participants)
        {
            var hasBattlefieldTeam = campaign.Teams.Any(team =>
                team.FactionId == participant.FactionId &&
                team.Side == participant.Side &&
                team.ActiveTroops > 0 &&
                team.Location is CampaignTeamLocation.Field or CampaignTeamLocation.InnerCity);
            if (hasBattlefieldTeam)
            {
                participant.BattleDaysParticipated++;
            }
        }

        foreach (var order in campaign.Reinforcements.Where(item => item.Status == ReinforcementStatus.Traveling))
        {
            order.RemainingBattleDays = Math.Max(0, order.RemainingBattleDays - 1);
            if (order.RemainingBattleDays > 0)
            {
                continue;
            }

            order.Status = ReinforcementStatus.Arrived;
            foreach (var team in order.Teams)
            {
                team.Location = CampaignTeamLocation.Reserve;
            }
        }

        // Resolve after advancing existing orders: newly dispatched support starts
        // its travel clock on the following battle day.
        if (world != null) AdvanceInvitationResponses(world, campaign);

        PromoteReserves(campaign, CampaignBattleSide.Attacker);
        PromoteReserves(campaign, CampaignBattleSide.Defender);
    }

    public static bool HasReachedMonthlyBattleLimit(
        ActiveBattleCampaignData campaign,
        int maximumBattleDays = MaximumBattleDaysPerMonth) =>
        campaign.BattleDaysThisMonth >= Math.Max(1, maximumBattleDays);

    public static void BeginNextCampaignMonth(ActiveBattleCampaignData campaign, int year, int month)
    {
        CampaignReorganizationRules.SynchronizeReinforcementTeamReferences(campaign);
        campaign.CurrentYear = year;
        campaign.CurrentMonth = month;
        campaign.BattleDaysThisMonth = 0;
        var defenderFood = campaign.DefenderFood;
        RecoverWounded(campaign, CampaignBattleSide.Defender, ref defenderFood);
        campaign.DefenderFood = defenderFood;
        var attackerFood = campaign.AttackerFood;
        RecoverWounded(campaign, CampaignBattleSide.Attacker, ref attackerFood);
        campaign.AttackerFood = attackerFood;
        if (campaign.Stage == CampaignStage.SiegePreparation)
        {
            campaign.Stage = CampaignStage.CityBattle;
            MoveInnerCityDefendersToField(campaign);
            ReleaseCityBattleSiegeEquipment(campaign);
            campaign.BattleSnapshotJson = string.Empty;
        }
    }

    public static void CompleteCampaign(WorldState world, ActiveBattleCampaignData campaign, CampaignBattleSide winner) =>
        CampaignSettlementService.CompleteCampaign(world, campaign, winner);

    public static void CompleteCaravanEscortCampaign(WorldState world, ActiveBattleCampaignData campaign, CaravanEscortOutcome outcome) =>
        CampaignSettlementService.CompleteCaravanEscortCampaign(world, campaign, outcome);
    public static void ApplyPostFieldDecision(ActiveBattleCampaignData campaign, PostFieldBattleDecision decision)
    {
        if (!campaign.AttackerWonFieldBattle)
        {
            throw new InvalidOperationException("A post-field decision requires an attacker field victory.");
        }

        campaign.PostFieldDecision = decision;
        campaign.IsAwaitingPlayerDecision = false;
        switch (decision)
        {
            case PostFieldBattleDecision.ImmediateAssault:
                campaign.Stage = CampaignStage.CityBattle;
                MoveInnerCityDefendersToField(campaign);
                ReleaseCityBattleSiegeEquipment(campaign);
                break;
            case PostFieldBattleDecision.PrepareNextMonthSiege:
                campaign.Stage = CampaignStage.SiegePreparation;
                break;
            case PostFieldBattleDecision.Withdraw:
                campaign.Stage = CampaignStage.Resolved;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown post-field decision.");
        }
    }

    public static bool HasEffectiveInnerCityDefender(ActiveBattleCampaignData campaign) =>
        campaign.Teams.Any(team =>
            team.Side == CampaignBattleSide.Defender &&
            team.Location == CampaignTeamLocation.InnerCity &&
            team.OfficerId > 0 &&
            team.ActiveTroops > 0);

    public static bool TryReorganizeFactionTeams(
        ActiveBattleCampaignData campaign,
        int factionId,
        IReadOnlyList<CampaignBattleTeamData> proposedTeams,
        out string error)
    {
        error = string.Empty;
        var existing = campaign.Teams
            .Where(team => team.FactionId == factionId &&
                           team.Location is CampaignTeamLocation.Field or CampaignTeamLocation.InnerCity or CampaignTeamLocation.Reserve)
            .ToList();
        var proposed = proposedTeams.Where(team => team.FactionId == factionId).ToList();
        if (!HaveEqualTroopTotals(existing, proposed, team => team.ActiveTroops) ||
            !HaveEqualTroopTotals(existing, proposed, team => team.WoundedTroops))
        {
            error = "Reorganization must preserve active and wounded troop totals for every troop type.";
            return false;
        }

        if (proposed.Any(team =>
                team.ActiveTroops < 0 || team.WoundedTroops < 0 ||
                team.ActiveTroops + team.WoundedTroops > team.MaximumTroops ||
                team.OfficerId <= 0))
        {
            error = "A reorganized team exceeds its capacity or has no officer.";
            return false;
        }

        campaign.Teams.RemoveAll(team => existing.Contains(team));
        campaign.Teams.AddRange(proposed.Select(CloneTeam));
        return true;
    }

    public static void PromoteReserves(ActiveBattleCampaignData campaign, CampaignBattleSide side)
    {
        CampaignReorganizationRules.SynchronizeReinforcementTeamReferences(campaign);
        var activeCount = campaign.Teams.Count(team => team.Side == side && team.ActiveTroops > 0 && IsActiveBattleLocation(campaign, team));
        var activeSiegeCount = campaign.Teams.Count(team => team.Side == side && team.ActiveTroops > 0 && IsActiveBattleLocation(campaign, team) && team.TroopType == TroopType.Siege);
        foreach (var team in campaign.Teams
                     .Where(team => team.Side == side && team.Location == CampaignTeamLocation.Reserve && team.ActiveTroops > 0 &&
                         (team.ReinforcementOrderId == 0 || campaign.Reinforcements.Any(order =>
                             order.Id == team.ReinforcementOrderId && order.Status == ReinforcementStatus.Deployed)))
                     .OrderBy(team => team.Id))
        {
            if (campaign.Stage == CampaignStage.FieldBattle && team.SiegeEngineType is SiegeEngineType.Ram or SiegeEngineType.Ladder)
            {
                continue;
            }
            if (activeCount >= MaximumActivePiecesPerSide)
            {
                break;
            }
            if (team.TroopType == TroopType.Siege && activeSiegeCount >= MaximumSiegeEnginesPerSide)
            {
                continue;
            }

            team.Location = campaign.Stage == CampaignStage.CityBattle && side == CampaignBattleSide.Defender && team.ReinforcementOrderId == 0
                ? CampaignTeamLocation.InnerCity
                : CampaignTeamLocation.Field;
            activeCount++;
            if (team.TroopType == TroopType.Siege)
            {
                activeSiegeCount++;
            }
        }

        foreach (var order in campaign.Reinforcements
                     .Where(item => item.Side == side && item.Status is ReinforcementStatus.Arrived or ReinforcementStatus.Reserve)
                     .OrderBy(item => item.DispatchYear)
                     .ThenBy(item => item.DispatchMonth)
                     .ThenBy(item => item.Id))
        {
            order.SuppliesDelivered ??= order.Status == ReinforcementStatus.Deployed;
            foreach (var team in order.Teams.Where(team => team.Location == CampaignTeamLocation.Reserve && team.ActiveTroops > 0))
            {
                if (campaign.Stage == CampaignStage.FieldBattle && team.SiegeEngineType is SiegeEngineType.Ram or SiegeEngineType.Ladder)
                {
                    order.Status = ReinforcementStatus.Reserve;
                    continue;
                }
                if (activeCount >= MaximumActivePiecesPerSide)
                {
                    order.Status = ReinforcementStatus.Reserve;
                    return;
                }

                if (team.TroopType == TroopType.Siege && activeSiegeCount >= MaximumSiegeEnginesPerSide)
                {
                    order.Status = ReinforcementStatus.Reserve;
                    continue;
                }

                // A reinforcement is an external force even during a city battle.
                // Only the city's original defenders may begin inside the walls.
                team.Location = CampaignTeamLocation.Field;
                if (!campaign.Teams.Contains(team))
                {
                    team.Id = campaign.Teams.Count == 0 ? 1 : campaign.Teams.Max(item => item.Id) + 1;
                    campaign.Teams.Add(team);
                }
                activeCount++;
                if (team.TroopType == TroopType.Siege)
                {
                    activeSiegeCount++;
                }
            }

            order.Status = order.Teams.All(team => team.Location != CampaignTeamLocation.Reserve)
                ? ReinforcementStatus.Deployed
                : ReinforcementStatus.Reserve;
            if (order.Status == ReinforcementStatus.Deployed && order.SuppliesDelivered != true)
            {
                if (side == CampaignBattleSide.Attacker)
                {
                    campaign.AttackerGold += order.Gold;
                    campaign.AttackerFood += order.Food;
                    campaign.AttackerGoldInvested += order.Gold;
                    campaign.AttackerFoodInvested += order.Food;
                }
                else
                {
                    campaign.DefenderGold += order.Gold;
                    campaign.DefenderFood += order.Food;
                    campaign.DefenderGoldInvested += order.Gold;
                    campaign.DefenderFoodInvested += order.Food;
                }
                order.SuppliesDelivered = true;
                order.SuppliesPendingPlacement = true;
            }
        }
    }

    public static void MarkCampaignTeamSpawnBlocked(ActiveBattleCampaignData campaign, CampaignBattleTeamData team)
    {
        team.Location = CampaignTeamLocation.Reserve;
        var order = campaign.Reinforcements.FirstOrDefault(item => item.Id == team.ReinforcementOrderId && team.ReinforcementOrderId > 0);
        if (order == null) return;
        order.SuppliesDelivered ??= order.Status == ReinforcementStatus.Deployed;
        // A newly credited order is rolled back before battle resumes. Previously
        // delivered supplies may already have been spent and must never be revoked.
        if (order.SuppliesPendingPlacement && order.SuppliesDelivered == true)
        {
            if (order.Side == CampaignBattleSide.Attacker && campaign.AttackerGold >= order.Gold && campaign.AttackerFood >= order.Food)
            {
                campaign.AttackerGold -= order.Gold; campaign.AttackerFood -= order.Food;
                campaign.AttackerGoldInvested -= order.Gold; campaign.AttackerFoodInvested -= order.Food;
                order.SuppliesDelivered = false;
            }
            else if (order.Side == CampaignBattleSide.Defender && campaign.DefenderGold >= order.Gold && campaign.DefenderFood >= order.Food)
            {
                campaign.DefenderGold -= order.Gold; campaign.DefenderFood -= order.Food;
                campaign.DefenderGoldInvested -= order.Gold; campaign.DefenderFoodInvested -= order.Food;
                order.SuppliesDelivered = false;
            }
        }
        order.SuppliesPendingPlacement = false;
        order.Status = ReinforcementStatus.Reserve;
    }

    public static void CompleteReinforcementPlacement(ActiveBattleCampaignData campaign)
    {
        foreach (var order in campaign.Reinforcements.Where(order => order.Status == ReinforcementStatus.Deployed))
            order.SuppliesPendingPlacement = false;
    }

    public static void MarkReinforcementTeamPlaced(ActiveBattleCampaignData campaign, CampaignBattleTeamData team)
    {
        if (team.ReinforcementOrderId > 0 && campaign.Teams.Contains(team) &&
            campaign.Reinforcements.Any(order => order.Id == team.ReinforcementOrderId && order.Teams.Contains(team)) &&
            team.Location is CampaignTeamLocation.Field or CampaignTeamLocation.InnerCity)
            team.HasEnteredBattle = true;
    }

    private static void RecoverWounded(
        ActiveBattleCampaignData campaign,
        CampaignBattleSide side,
        ref int food)
    {
        var arrivedReserves = campaign.Reinforcements
            .Where(order => order.Status is ReinforcementStatus.Arrived or ReinforcementStatus.Reserve)
            .SelectMany(order => order.Teams).Where(team => team.Location == CampaignTeamLocation.Reserve);
        foreach (var team in campaign.Teams.Concat(arrivedReserves).Distinct().Where(team =>
                     team.Side == side &&
                     team.WoundedTroops > 0 &&
                     team.Location is CampaignTeamLocation.Field or CampaignTeamLocation.InnerCity or CampaignTeamLocation.Reserve))
        {
            var recoveryPercent = team.Location == CampaignTeamLocation.Reserve ||
                                  (side == CampaignBattleSide.Defender && team.Location == CampaignTeamLocation.InnerCity)
                ? DefenderCityWoundedRecoveryPercent
                : AttackerCampWoundedRecoveryPercent;
            var capacity = Math.Max(0, team.MaximumTroops - team.ActiveTroops);
            var recoverable = Math.Min(capacity, team.WoundedTroops * recoveryPercent / 100);
            var foodLimitedRecovery = Math.Min(recoverable, Math.Max(0, food) * 20);
            var foodCost = (foodLimitedRecovery + 19) / 20;
            CampaignTroopOriginRules.ApplyCounts(team, team.ActiveTroops + foodLimitedRecovery, team.WoundedTroops - foodLimitedRecovery);
            food = Math.Max(0, food - foodCost);
        }
    }

    private static void MoveInnerCityDefendersToField(ActiveBattleCampaignData campaign)
    {
        foreach (var team in campaign.Teams.Where(team =>
                     team.Side == CampaignBattleSide.Defender && team.Location == CampaignTeamLocation.InnerCity))
        {
            team.Location = CampaignTeamLocation.Field;
            // Falling back into the defended city is not a strategic return.
            // Clear its temporary marker when the same team enters the siege.
            if (team.RetreatDestinationCityId == campaign.TargetCityId)
                team.RetreatDestinationCityId = 0;
        }
    }

    private static void ReleaseCityBattleSiegeEquipment(ActiveBattleCampaignData campaign)
    {
        PromoteReserves(campaign, CampaignBattleSide.Attacker);
        PromoteReserves(campaign, CampaignBattleSide.Defender);
    }

    private static void UpsertParticipant(
        ActiveBattleCampaignData campaign,
        int factionId,
        CampaignBattleSide side,
        int playerFactionId,
        ReinforcementOrderData order)
    {
        var participant = campaign.Participants.FirstOrDefault(item => item.FactionId == factionId && item.Side == side);
        if (participant == null)
        {
            participant = new BattleParticipantData
            {
                FactionId = factionId,
                Side = side,
                ControllerType = factionId == playerFactionId ? CampaignControllerType.Player : CampaignControllerType.Ai
            };
            campaign.Participants.Add(participant);
        }

        participant.GoldContribution += order.Gold;
        participant.FoodContribution += order.Food;
        participant.TroopsCommitted += order.Teams.Sum(team => team.ActiveTroops);
    }

    public static IReadOnlyList<CityData> GetRetreatDestinations(WorldState world, ActiveBattleCampaignData campaign, CampaignBattleTeamData team) =>
        CampaignRetreatService.GetRetreatDestinations(world, campaign, team);

    public static int GetRetreatOriginCityId(ActiveBattleCampaignData campaign, CampaignBattleTeamData team) =>
        CampaignRetreatService.GetRetreatOriginCityId(campaign, team);

    public static bool TryRetreatTeamToCity(WorldState world, ActiveBattleCampaignData campaign, CampaignBattleTeamData team, int destinationCityId) =>
        CampaignRetreatService.TryRetreatTeamToCity(world, campaign, team, destinationCityId);

    public static bool TryDeliverCaravanEscortTeam(WorldState world, ActiveBattleCampaignData campaign, CampaignBattleTeamData team) =>
        CampaignRetreatService.TryDeliverCaravanEscortTeam(world, campaign, team);

    public static bool TryReturnCaravanEscortTeamToSource(WorldState world, ActiveBattleCampaignData campaign, CampaignBattleTeamData team) =>
        CampaignRetreatService.TryReturnCaravanEscortTeamToSource(world, campaign, team);

    public static bool TryReturnSupportTeamToOrigin(WorldState world, CampaignBattleTeamData team) =>
        CampaignRetreatService.TryReturnSupportTeamToOrigin(world, team);

    public static int GetRetreatFactionId(WorldState world, CampaignBattleTeamData team) =>
        CampaignRetreatService.GetRetreatFactionId(world, team);
    private static int FindFriendlyRouteLinks(WorldState world, int sourceCityId, int targetCityId, int factionId)
    {
        if (sourceCityId == targetCityId)
        {
            return 0;
        }

        // The battlefield itself is a valid endpoint, but another active
        // battlefield cannot be used as a source or a friendly transit city.
        if (world.ActiveBattleCampaigns.Any(campaign => campaign.Stage != CampaignStage.Resolved && campaign.TargetCityId == sourceCityId))
            return -1;

        var visited = new HashSet<int> { sourceCityId };
        var queue = new Queue<(int CityId, int Links)>();
        queue.Enqueue((sourceCityId, 0));
        while (queue.Count > 0)
        {
            var (cityId, links) = queue.Dequeue();
            var city = world.GetCity(cityId);
            if (city == null)
            {
                continue;
            }

            foreach (var connectedId in city.ConnectedCityIds)
            {
                if (connectedId == targetCityId)
                {
                    return links + 1;
                }

                if (!visited.Add(connectedId))
                {
                    continue;
                }

                var connectedCity = world.GetCity(connectedId);
                if (connectedCity?.OwnerFactionId == factionId &&
                    !world.ActiveBattleCampaigns.Any(campaign => campaign.Stage != CampaignStage.Resolved && campaign.TargetCityId == connectedId))
                {
                    queue.Enqueue((connectedId, links + 1));
                }
            }
        }

        return -1;
    }

    private static bool HaveEqualTroopTotals(
        IEnumerable<CampaignBattleTeamData> left,
        IEnumerable<CampaignBattleTeamData> right,
        Func<CampaignBattleTeamData, int> selector)
    {
        foreach (var troopType in Enum.GetValues<TroopType>())
        {
            if (left.Where(team => team.TroopType == troopType).Sum(selector) !=
                right.Where(team => team.TroopType == troopType).Sum(selector))
            {
                return false;
            }
        }

        return true;
    }

    private static CampaignBattleTeamData CloneTeam(CampaignBattleTeamData team) => new()
    {
        Id = team.Id,
        FactionId = team.FactionId,
        Side = team.Side,
        ControllerType = team.ControllerType,
        OfficerId = team.OfficerId,
        TroopType = team.TroopType,
        EquipmentType = team.EquipmentType,
        SiegeEngineType = team.SiegeEngineType,
        ActiveTroops = team.ActiveTroops,
        WoundedTroops = team.WoundedTroops,
        WoundedOriginCityId = team.WoundedOriginCityId,
        TroopOrigins = team.TroopOrigins.Select(item => new CampaignTroopOriginData { CityId = item.CityId, ActiveTroops = item.ActiveTroops, WoundedTroops = item.WoundedTroops }).ToList(),
        MaximumTroops = team.MaximumTroops,
        ReorganizationCapacity = team.ReorganizationCapacity,
        AwaitingEnvoyReserveTroops = team.AwaitingEnvoyReserveTroops,
        ReturnedTroops = team.ReturnedTroops,
        DisbandedTroops = team.DisbandedTroops,
        HasEnteredBattle = team.HasEnteredBattle,
        Morale = team.Morale,
        MessTurns = team.MessTurns,
        Energy = team.Energy,
        WeaponAmmo = team.WeaponAmmo,
        MaxWeaponAmmo = team.MaxWeaponAmmo,
        ReinforcementOrderId = team.ReinforcementOrderId,
        OriginCityId = team.OriginCityId,
        OfficerReturnCityId = team.OfficerReturnCityId,
        Location = team.Location,
        CooperationObjective = team.CooperationObjective
    };

    private static void LimitDeploymentTroops(List<AttackOfficerDeploymentData> deployments, int maximumTroops)
    {
        var remaining = Math.Max(0, maximumTroops);
        foreach (var deployment in deployments)
        {
            deployment.TroopCount = Math.Min(deployment.TroopCount, remaining);
            remaining -= deployment.TroopCount;
        }
        deployments.RemoveAll(deployment => deployment.TroopCount <= 0);
    }
}
