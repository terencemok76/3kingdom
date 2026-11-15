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
        world.ActiveBattleCampaigns.Any(campaign =>
            campaign.Stage != CampaignStage.Resolved &&
            (campaign.Teams.Any(team => team.OfficerId == officerId &&
                                        team.Location is not (CampaignTeamLocation.Eliminated or CampaignTeamLocation.Captured or CampaignTeamLocation.NeighborCity)) ||
             campaign.Reinforcements.Any(order =>
                 (order.Status is ReinforcementStatus.Traveling or ReinforcementStatus.Arrived or ReinforcementStatus.Reserve or ReinforcementStatus.Deployed) &&
                 order.Teams.Any(team => team.OfficerId == officerId)) ||
             campaign.Invitations.Any(invitation =>
                 invitation.Status is BattleInvitationStatus.Pending or BattleInvitationStatus.Accepted &&
                 invitation.EnvoyOfficerId == officerId)));

    public static int GetFriendlyRouteLinks(WorldState world, int sourceCityId, int targetCityId, int factionId) =>
        FindFriendlyRouteLinks(world, sourceCityId, targetCityId, factionId);

    public static bool TryAcceptPrewarAlliance(WorldState world, PrewarAllianceRequestData request)
    {
        var target = world.GetCity(request.TargetCityId);
        var relation = world.GetDiplomacyRelation(request.InviterFactionId, request.AllyFactionId);
        if (target == null || target.OwnerFactionId <= 0 ||
            target.OwnerFactionId == request.InviterFactionId || target.OwnerFactionId == request.AllyFactionId ||
            relation is not { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 } ||
            request.RequestedTroops <= 0 ||
            world.PrewarAllianceRequests.Any(item => item.InviterFactionId == request.InviterFactionId && item.AllyFactionId == request.AllyFactionId && item.TargetCityId == request.TargetCityId)) return false;
        var trust = DiplomacyScoreRules.GetTrustScore(relation, request.AllyFactionId, request.InviterFactionId);
        var threat = DiplomacyScoreRules.CalculateThreatScore(world, request.AllyFactionId, request.InviterFactionId, relation);
        var envoy = world.GetOfficer(request.EnvoyOfficerId);
        var safeTroops = world.Cities.Where(c => c.OwnerFactionId == request.AllyFactionId).Select(c => Math.Max(0, c.Troops - MinimumCityGarrison)).DefaultIfEmpty(0).Max();
        var score = relation.RelationScore + (trust - 50) / 2 + 35 + Math.Min(25, safeTroops / 400) +
                    (envoy == null ? 0 : Math.Min(20, (envoy.Intelligence + envoy.Charm) / 10)) - threat / 5;
        if (score < 45) return false;
        request.TargetFactionId = target.OwnerFactionId;
        world.PrewarAllianceRequests.Add(request);
        return true;
    }

    public static void DispatchPrewarAllies(WorldState world, ActiveBattleCampaignData campaign)
    {
        var requests = world.PrewarAllianceRequests.Where(item => item.InviterFactionId == campaign.AttackerFactionId && item.TargetCityId == campaign.TargetCityId).ToList();
        foreach (var request in requests)
        {
            world.PrewarAllianceRequests.Remove(request);
            var relation = world.GetDiplomacyRelation(request.InviterFactionId, request.AllyFactionId);
            var invitation = CreateInvitation(campaign, request.InviterFactionId, request.AllyFactionId,
                CampaignBattleSide.Attacker, BattleInvitationSupportType.Troops, request.EnvoyOfficerId,
                request.RequestedTroops, 0, 0, 0, request.PromisedGold, request.PromisedFood);
            if (world.GetCity(request.TargetCityId)?.OwnerFactionId != request.TargetFactionId ||
                relation is not { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 })
            {
                invitation.Status = BattleInvitationStatus.Declined;
                invitation.DecisionReason = "Circumstances changed.";
            }
            else ResolveAiInvitation(world, campaign, invitation);
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
        if (campaign.Invitations.Any(item =>
                item.InvitedFactionId == invitedFactionId &&
                item.Side != side &&
                item.Status is BattleInvitationStatus.Pending or BattleInvitationStatus.Accepted))
        {
            throw new InvalidOperationException("The invited faction is already committed to the opposing side.");
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
            .Where(city => resourcesOnly || (city.Troops > MinimumCityGarrison && CreateAiAllianceReinforcementDeployments(world, city, invitation.RequestedTroops).Count > 0))
            .Select(city => (City: city, Links: FindFriendlyRouteLinks(world, city.Id, campaign.TargetCityId, invitation.InvitedFactionId)))
            .Where(item => item.Links > 0)
            .OrderBy(item => item.City.ConnectedCityIds.Count(id => world.GetCity(id) is { OwnerFactionId: > 0 } neighbor && neighbor.OwnerFactionId != invitation.InvitedFactionId && neighbor.OwnerFactionId != invitation.InviterFactionId))
            .ThenBy(item => item.Links)
            .ThenByDescending(item => item.City.Troops)
            .ToList();
        var source = candidateCities.FirstOrDefault();
        var relationScore = relation?.RelationScore ?? 0;
        var trustScore = DiplomacyScoreRules.GetTrustScore(relation, invitation.InvitedFactionId, invitation.InviterFactionId);
        var threatScore = DiplomacyScoreRules.CalculateThreatScore(world, invitation.InvitedFactionId, campaign.AttackerFactionId, relation);
        var envoy = world.GetOfficer(invitation.EnvoyOfficerId);
        var envoyBonus = envoy == null ? 0 : Math.Min(20, (envoy.Intelligence + envoy.Charm) / 10);
        var safeTroops = source.City == null ? 0 : Math.Max(0, source.City.Troops - MinimumCityGarrison);
        var acceptanceScore = relationScore + (trustScore - 50) / 2 + (isAlliance ? 35 : 0) + Math.Min(25, safeTroops / 400) + envoyBonus - threatScore / 5;
        if (source.City == null || acceptanceScore < 45)
        {
            invitation.Status = BattleInvitationStatus.Declined;
            invitation.DecisionReason = source.City == null
                ? "No friendly route reaches the battlefield."
                : "The faction cannot safely spare the requested support.";
            return false;
        }

        invitation.Status = BattleInvitationStatus.Accepted;
        invitation.DecisionReason = "Support accepted.";
        var deployments = invitation.SupportType == BattleInvitationSupportType.Resources
            ? new List<AttackOfficerDeploymentData>()
            : CreateAiAllianceReinforcementDeployments(world, source.City, invitation.RequestedTroops);
        var envoyLoanedInfantry = DetermineAiEnvoyLoanedInfantry(world, campaign, invitation, source.City);
        if (invitation.RequestedTroops > 0)
        {
            LimitDeploymentTroops(deployments, Math.Max(1, invitation.RequestedTroops - envoyLoanedInfantry));
        }
        var allocation = BuildTroopAllocation(deployments);
        envoyLoanedInfantry = Math.Min(envoyLoanedInfantry, Math.Max(0, source.City.InfantryTroops - allocation.Infantry));
        envoyLoanedInfantry = Math.Min(envoyLoanedInfantry, Math.Max(0, source.City.Troops - allocation.Total - MinimumCityGarrison));

        var gold = Math.Min(invitation.RequestedGold, Math.Max(0, source.City.Gold / 3));
        var food = Math.Min(invitation.RequestedFood, Math.Max(0, source.City.Food / 3));
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
            .Where(officer => officer != null)
            .Cast<OfficerData>()
            .OrderByDescending(officer => officer.Leadership + officer.Combat)
            .ThenByDescending(officer => officer.Intelligence)
            .Take(teamLimit)
            .Select(officer => officer.Id)
            .ToHashSet();
        return CreateDefaultDefenderDeployments(sourceCity, 25)
            .Where(deployment => selectedOfficers.Contains(deployment.OfficerId))
            .ToList();
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
            ControllerType = CampaignControllerType.Player,
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
        foreach (var request in attack.DefenseReinforcementRequests)
        {
            try
            {
                var source = world.GetCity(request.SourceCityId);
                if (source == null || source.Id == campaign.TargetCityId)
                {
                    continue;
                }

                if (!request.IsAllianceRequest && source.OwnerFactionId == campaign.DefenderFactionId)
                {
                    DispatchReinforcement(world, campaign, source.Id, CampaignBattleSide.Defender,
                        request.Deployments, 0, 0);
                    continue;
                }

                if (!request.IsAllianceRequest || source.OwnerFactionId == campaign.DefenderFactionId)
                {
                    continue;
                }

                var invitation = CreateInvitation(campaign, campaign.DefenderFactionId,
                    source.OwnerFactionId, CampaignBattleSide.Defender,
                    BattleInvitationSupportType.Troops, request.EnvoyOfficerId, request.RequestedTroops, 0, 0, source.Id,
                    request.PromisedGold, request.PromisedFood);
                ResolveAiInvitation(world, campaign, invitation);
            }
            catch (ReinforcementDispatchException)
            {
                // A source can become unavailable while another pending command
                // resolves.  Preserve all other defense requests.
            }
        }
    }

    public static void AdvanceCompletedBattleDay(ActiveBattleCampaignData campaign)
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

        PromoteReserves(campaign, CampaignBattleSide.Attacker);
        PromoteReserves(campaign, CampaignBattleSide.Defender);
    }

    public static bool HasReachedMonthlyBattleLimit(
        ActiveBattleCampaignData campaign,
        int maximumBattleDays = MaximumBattleDaysPerMonth) =>
        campaign.BattleDaysThisMonth >= Math.Max(1, maximumBattleDays);

    public static void BeginNextCampaignMonth(ActiveBattleCampaignData campaign, int year, int month)
    {
        campaign.CurrentYear = year;
        campaign.CurrentMonth = month;
        campaign.BattleDaysThisMonth = 0;
        var defenderFood = campaign.DefenderFood;
        RecoverWounded(campaign, CampaignBattleSide.Defender, DefenderCityWoundedRecoveryPercent, ref defenderFood);
        campaign.DefenderFood = defenderFood;
        var attackerFood = campaign.AttackerFood;
        RecoverWounded(campaign, CampaignBattleSide.Attacker, AttackerCampWoundedRecoveryPercent, ref attackerFood);
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
        var activeCount = campaign.Teams.Count(team => team.Side == side && IsActiveBattleLocation(campaign, team));
        var activeSiegeCount = campaign.Teams.Count(team => team.Side == side && IsActiveBattleLocation(campaign, team) && team.TroopType == TroopType.Siege);
        foreach (var team in campaign.Teams
                     .Where(team => team.Side == side && team.Location == CampaignTeamLocation.Reserve && team.ReinforcementOrderId == 0)
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

            team.Location = campaign.Stage == CampaignStage.CityBattle && side == CampaignBattleSide.Defender
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
            foreach (var team in order.Teams.Where(team => team.Location == CampaignTeamLocation.Reserve))
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
                team.Id = campaign.Teams.Count == 0 ? 1 : campaign.Teams.Max(item => item.Id) + 1;
                campaign.Teams.Add(team);
                activeCount++;
                if (team.TroopType == TroopType.Siege)
                {
                    activeSiegeCount++;
                }
            }

            order.Status = order.Teams.All(team => team.Location != CampaignTeamLocation.Reserve)
                ? ReinforcementStatus.Deployed
                : ReinforcementStatus.Reserve;
            if (order.Status == ReinforcementStatus.Deployed)
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
            }
        }
    }

    private static void RecoverWounded(
        ActiveBattleCampaignData campaign,
        CampaignBattleSide side,
        int recoveryPercent,
        ref int food)
    {
        foreach (var team in campaign.Teams.Where(team =>
                     team.Side == side &&
                     team.WoundedTroops > 0 &&
                     team.Location is CampaignTeamLocation.Field or CampaignTeamLocation.InnerCity or CampaignTeamLocation.Reserve))
        {
            var capacity = Math.Max(0, team.MaximumTroops - team.ActiveTroops);
            var recoverable = Math.Min(capacity, team.WoundedTroops * recoveryPercent / 100);
            var foodLimitedRecovery = Math.Min(recoverable, Math.Max(0, food) * 20);
            var foodCost = (foodLimitedRecovery + 19) / 20;
            team.ActiveTroops += foodLimitedRecovery;
            team.WoundedTroops -= foodLimitedRecovery;
            food = Math.Max(0, food - foodCost);
        }
    }

    private static void MoveInnerCityDefendersToField(ActiveBattleCampaignData campaign)
    {
        foreach (var team in campaign.Teams.Where(team =>
                     team.Side == CampaignBattleSide.Defender && team.Location == CampaignTeamLocation.InnerCity))
        {
            team.Location = CampaignTeamLocation.Field;
        }
    }

    private static void ReleaseCityBattleSiegeEquipment(ActiveBattleCampaignData campaign)
    {
        PromoteReserves(campaign, CampaignBattleSide.Attacker);
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
                if (connectedCity?.OwnerFactionId == factionId)
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
        MaximumTroops = team.MaximumTroops,
        Morale = team.Morale,
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
