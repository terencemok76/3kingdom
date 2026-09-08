using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class BattleCampaignService
{
    public const int MaximumBattleDaysPerMonth = 10;
    public const int MaximumActivePiecesPerSide = 12;
    public const int MaximumSupplyCartsPerSide = 2;
    public const int MaximumSiegeEnginesPerSide = 3;
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

    public static ActiveBattleCampaignData CreateCampaign(
        WorldState world,
        PendingCommandData attack,
        DefenderBattlePlan defenderPlan)
    {
        var sourceCity = world.GetCity(attack.SourceCityId) ??
            throw new InvalidOperationException("Attack source city is missing.");
        var targetCity = world.GetCity(attack.TargetCityId) ??
            throw new InvalidOperationException("Attack target city is missing.");
        if (!sourceCity.ConnectedCityIds.Contains(targetCity.Id) ||
            sourceCity.OwnerFactionId == targetCity.OwnerFactionId)
        {
            throw new InvalidOperationException("Attack route is no longer valid.");
        }

        var campaignId = world.ActiveBattleCampaigns.Count == 0
            ? 1
            : world.ActiveBattleCampaigns.Max(item => item.Id) + 1;
        var playerFactionId = world.Factions.FirstOrDefault(faction => faction.IsPlayer)?.Id ?? -1;
        var campaign = new ActiveBattleCampaignData
        {
            Id = campaignId,
            AttackerFactionId = sourceCity.OwnerFactionId,
            DefenderFactionId = targetCity.OwnerFactionId,
            SourceCityId = sourceCity.Id,
            TargetCityId = targetCity.Id,
            StartedYear = world.Year,
            StartedMonth = world.Month,
            CurrentYear = world.Year,
            CurrentMonth = world.Month,
            DefenderPlan = defenderPlan,
            Stage = defenderPlan == DefenderBattlePlan.FieldIntercept
                ? CampaignStage.FieldBattle
                : CampaignStage.CityBattle,
            IsPlayerInvolved = sourceCity.OwnerFactionId == playerFactionId || targetCity.OwnerFactionId == playerFactionId,
            AttackerGold = Math.Max(0, attack.GoldToSend),
            AttackerFood = Math.Max(0, attack.FoodToSend),
            DefenderGold = Math.Max(0, targetCity.Gold),
            DefenderFood = Math.Max(0, targetCity.Food)
        };
        targetCity.Gold = 0;
        targetCity.Food = 0;

        campaign.Teams.AddRange(CreateTeams(
            attack.AttackOfficerDeployments,
            campaign.AttackerFactionId,
            CampaignBattleSide.Attacker,
            playerFactionId));

        var defenderDeployments = attack.DefenderOfficerDeployments.Count > 0
            ? CloneDeployments(attack.DefenderOfficerDeployments)
            : CreateDefaultDefenderDeployments(
                targetCity,
                defenderPlan == DefenderBattlePlan.FieldIntercept ? 70 : 100,
                defenderPlan == DefenderBattlePlan.FieldIntercept);
        var defenderAllocation = BuildTroopAllocation(defenderDeployments);
        targetCity.RemoveTroopAllocation(defenderAllocation);
        campaign.Teams.AddRange(CreateTeams(
            defenderDeployments,
            campaign.DefenderFactionId,
            CampaignBattleSide.Defender,
            playerFactionId,
            defenderPlan == DefenderBattlePlan.CityDefense
                ? CampaignTeamLocation.InnerCity
                : CampaignTeamLocation.Field));

        if (defenderPlan == DefenderBattlePlan.FieldIntercept)
        {
            var usedOfficerIds = defenderDeployments.Select(item => item.OfficerId).ToHashSet();
            var cityDefenseDeployments = CreateDefaultDefenderDeployments(targetCity, 100, false, usedOfficerIds);
            var cityDefenseAllocation = BuildTroopAllocation(cityDefenseDeployments);
            targetCity.RemoveTroopAllocation(cityDefenseAllocation);
            campaign.Teams.AddRange(CreateTeams(
                cityDefenseDeployments,
                campaign.DefenderFactionId,
                CampaignBattleSide.Defender,
                playerFactionId,
                CampaignTeamLocation.InnerCity));
        }

        for (var index = 0; index < campaign.Teams.Count; index++)
        {
            campaign.Teams[index].Id = index + 1;
        }
        ApplyInitialDeploymentCaps(campaign, CampaignBattleSide.Attacker);
        ApplyInitialDeploymentCaps(campaign, CampaignBattleSide.Defender);

        campaign.Participants.Add(CreateParticipant(
            campaign.AttackerFactionId,
            CampaignBattleSide.Attacker,
            true,
            campaign.AttackerGold,
            campaign.AttackerFood,
            campaign.Teams));
        campaign.Participants.Add(CreateParticipant(
            campaign.DefenderFactionId,
            CampaignBattleSide.Defender,
            true,
            0,
            0,
            campaign.Teams));
        world.ActiveBattleCampaigns.Add(campaign);
        return campaign;
    }

    public static bool IsOfficerCommitted(WorldState world, int officerId) =>
        world.ActiveBattleCampaigns.Any(campaign =>
            campaign.Stage != CampaignStage.Resolved &&
            (campaign.Teams.Any(team => team.OfficerId == officerId &&
                                        team.Location is not (CampaignTeamLocation.Eliminated or CampaignTeamLocation.Captured or CampaignTeamLocation.NeighborCity)) ||
             campaign.Reinforcements.Any(order =>
                 (order.Status is ReinforcementStatus.Traveling or ReinforcementStatus.Arrived or ReinforcementStatus.Reserve or ReinforcementStatus.Deployed) &&
                 order.Teams.Any(team => team.OfficerId == officerId)) ||
             campaign.Invitations.Any(invitation =>
                 invitation.Status == BattleInvitationStatus.Pending && invitation.EnvoyOfficerId == officerId)));

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
        var sourceCity = world.GetCity(sourceCityId) ??
            throw new InvalidOperationException("Reinforcement source city is missing.");
        var expectedFactionId = side == CampaignBattleSide.Attacker
            ? campaign.AttackerFactionId
            : campaign.DefenderFactionId;
        if (supplyOwnership == CampaignSupplyOwnership.MainFaction && sourceCity.OwnerFactionId != expectedFactionId)
        {
            throw new InvalidOperationException("Domestic reinforcements must come from the main faction.");
        }

        if (campaign.Reinforcements.Any(order =>
                order.SourceCityId == sourceCityId &&
                order.DispatchYear == world.Year &&
                order.DispatchMonth == world.Month &&
                order.Status != ReinforcementStatus.Cancelled))
        {
            throw new InvalidOperationException("This city has already reinforced the campaign this month.");
        }

        var routeLinks = FindFriendlyRouteLinks(world, sourceCity.Id, campaign.TargetCityId, sourceCity.OwnerFactionId);
        if (routeLinks <= 0)
        {
            throw new InvalidOperationException("No valid reinforcement route reaches the battlefield.");
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
            throw new InvalidOperationException("The source city must retain its minimum garrison.");
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
            orderId));

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
        int requestedFood)
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
            Side = side,
            SupportType = supportType,
            EnvoyOfficerId = envoyOfficerId,
            RequestedTroops = Math.Max(0, requestedTroops),
            RequestedGold = Math.Max(0, requestedGold),
            RequestedFood = Math.Max(0, requestedFood),
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
            .Select(city => (City: city, Links: FindFriendlyRouteLinks(world, city.Id, campaign.TargetCityId, invitation.InvitedFactionId)))
            .Where(item => item.Links > 0)
            .OrderBy(item => item.Links)
            .ThenByDescending(item => item.City.Troops)
            .ToList();
        var source = candidateCities.FirstOrDefault();
        var relationScore = relation?.RelationScore ?? 0;
        var safeTroops = source.City == null ? 0 : Math.Max(0, source.City.Troops - MinimumCityGarrison);
        var acceptanceScore = relationScore + (isAlliance ? 35 : 0) + Math.Min(25, safeTroops / 400);
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
            : CreateDefaultDefenderDeployments(source.City, 25);
        if (invitation.RequestedTroops > 0)
        {
            LimitDeploymentTroops(deployments, invitation.RequestedTroops);
        }

        var gold = Math.Min(invitation.RequestedGold, Math.Max(0, source.City.Gold / 3));
        var food = Math.Min(invitation.RequestedFood, Math.Max(0, source.City.Food / 3));
        try
        {
            DispatchReinforcement(
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
        }
        catch (InvalidOperationException ex)
        {
            invitation.Status = BattleInvitationStatus.Declined;
            invitation.DecisionReason = ex.Message;
            return false;
        }
        return true;
    }

    public static void AdvanceCompletedBattleDay(ActiveBattleCampaignData campaign)
    {
        campaign.BattleDaysThisMonth++;
        campaign.TotalBattleDays++;
        foreach (var participant in campaign.Participants)
        {
            participant.BattleDaysParticipated++;
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

    public static bool HasReachedMonthlyBattleLimit(ActiveBattleCampaignData campaign) =>
        campaign.BattleDaysThisMonth >= MaximumBattleDaysPerMonth;

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

    public static void CompleteCampaign(WorldState world, ActiveBattleCampaignData campaign, CampaignBattleSide winner)
    {
        var sourceCity = world.GetCity(campaign.SourceCityId);
        var targetCity = world.GetCity(campaign.TargetCityId);
        if (sourceCity == null || targetCity == null)
        {
            campaign.Stage = CampaignStage.Resolved;
            return;
        }

        ReturnUndeployedReinforcements(world, campaign);

        var attackerWonCity = winner == CampaignBattleSide.Attacker && campaign.Stage == CampaignStage.CityBattle;
        if (attackerWonCity)
        {
            targetCity.OwnerFactionId = campaign.AttackerFactionId;
            foreach (var officerId in targetCity.OfficerIds.ToList())
            {
                if (world.GetOfficer(officerId) is { } officer)
                {
                    officer.CityId = 0;
                    officer.CaptiveFactionId = campaign.AttackerFactionId;
                    officer.JailedCityId = targetCity.Id;
                }
            }
            targetCity.OfficerIds.Clear();
        }

        foreach (var team in campaign.Teams.Where(team => team.Location is not (CampaignTeamLocation.Eliminated or CampaignTeamLocation.Captured)))
        {
            var destination = ResolveReturnCity(world, campaign, team, winner, attackerWonCity);
            if (destination == null)
            {
                continue;
            }

            destination.AddTroops(team.TroopType, Math.Max(0, team.ActiveTroops + team.WoundedTroops));
            if (team.TroopType == TroopType.Siege && team.SiegeEngineType != SiegeEngineType.None)
            {
                var engines = new SiegeEngineAllocationData();
                switch (team.SiegeEngineType)
                {
                    case SiegeEngineType.Ram: engines.Ram = 1; break;
                    case SiegeEngineType.Catapult: engines.Catapult = 1; break;
                    case SiegeEngineType.Ladder: engines.Ladder = 1; break;
                }
                destination.AddSiegeEngineAllocation(engines);
            }
            if (team.OfficerId > 0 && !destination.OfficerIds.Contains(team.OfficerId))
            {
                RemoveOfficerFromAllCities(world, team.OfficerId);
                destination.OfficerIds.Add(team.OfficerId);
                if (world.GetOfficer(team.OfficerId) is { } officer)
                {
                    officer.CityId = destination.Id;
                }
            }
        }

        if (attackerWonCity)
        {
            targetCity.Gold += campaign.AttackerGold + campaign.DefenderGold;
            targetCity.Food += campaign.AttackerFood + campaign.DefenderFood;
        }
        else
        {
            sourceCity.Gold += campaign.AttackerGold;
            sourceCity.Food += campaign.AttackerFood;
            targetCity.Gold += campaign.DefenderGold;
            targetCity.Food += campaign.DefenderFood;
        }

        campaign.Stage = CampaignStage.Resolved;
        campaign.IsAwaitingPlayerDecision = false;
        campaign.BattleSnapshotJson = string.Empty;
    }

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

                team.Location = campaign.Stage == CampaignStage.CityBattle && side == CampaignBattleSide.Defender
                    ? CampaignTeamLocation.InnerCity
                    : CampaignTeamLocation.Field;
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
                }
                else
                {
                    campaign.DefenderGold += order.Gold;
                    campaign.DefenderFood += order.Food;
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

    private static void ApplyInitialDeploymentCaps(ActiveBattleCampaignData campaign, CampaignBattleSide side)
    {
        var activeCount = 0;
        var siegeCount = 0;
        foreach (var team in campaign.Teams.Where(team => team.Side == side && IsActiveBattleLocation(campaign, team)).OrderBy(team => team.Id))
        {
            var exceedsTotal = activeCount >= MaximumActivePiecesPerSide;
            var exceedsSiege = team.TroopType == TroopType.Siege && siegeCount >= MaximumSiegeEnginesPerSide;
            if (exceedsTotal || exceedsSiege)
            {
                team.Location = CampaignTeamLocation.Reserve;
                continue;
            }

            activeCount++;
            if (team.TroopType == TroopType.Siege)
            {
                siegeCount++;
            }
        }
    }

    private static bool IsActiveBattleLocation(ActiveBattleCampaignData campaign, CampaignBattleTeamData team) =>
        team.Location == CampaignTeamLocation.Field ||
        (campaign.Stage == CampaignStage.CityBattle && team.Location == CampaignTeamLocation.InnerCity);

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

    private static BattleParticipantData CreateParticipant(
        int factionId,
        CampaignBattleSide side,
        bool isMainFaction,
        int gold,
        int food,
        IEnumerable<CampaignBattleTeamData> teams)
    {
        var factionTeams = teams.Where(team => team.FactionId == factionId && team.Side == side).ToList();
        return new BattleParticipantData
        {
            FactionId = factionId,
            Side = side,
            IsMainFaction = isMainFaction,
            ControllerType = factionTeams.FirstOrDefault()?.ControllerType ?? CampaignControllerType.Ai,
            GoldContribution = gold,
            FoodContribution = food,
            TroopsCommitted = factionTeams.Sum(team => team.ActiveTroops)
        };
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

    private static List<CampaignBattleTeamData> CreateTeams(
        IEnumerable<AttackOfficerDeploymentData> deployments,
        int factionId,
        CampaignBattleSide side,
        int playerFactionId,
        CampaignTeamLocation defaultLocation = CampaignTeamLocation.Field,
        int reinforcementOrderId = 0)
    {
        var teams = new List<CampaignBattleTeamData>();
        foreach (var deployment in deployments.Where(item => item.TroopCount > 0))
        {
            var location = deployment.TroopType == TroopType.Siege &&
                           deployment.SiegeEngineType is SiegeEngineType.Ram or SiegeEngineType.Ladder &&
                           defaultLocation == CampaignTeamLocation.Field
                ? CampaignTeamLocation.Reserve
                : defaultLocation;
            teams.Add(new CampaignBattleTeamData
            {
                Id = teams.Count + 1,
                FactionId = factionId,
                Side = side,
                ControllerType = factionId == playerFactionId ? CampaignControllerType.Player : CampaignControllerType.Ai,
                OfficerId = deployment.OfficerId,
                TroopType = deployment.TroopType,
                SiegeEngineType = deployment.SiegeEngineType,
                ActiveTroops = deployment.TroopCount,
                MaximumTroops = deployment.TroopCount,
                ReinforcementOrderId = reinforcementOrderId,
                Location = location
            });
        }

        return teams;
    }

    private static List<AttackOfficerDeploymentData> CreateDefaultDefenderDeployments(
        CityData city,
        int troopPercent = 100,
        bool reserveOneOfficer = false,
        IReadOnlySet<int>? excludedOfficerIds = null)
    {
        var availableOfficerIds = city.OfficerIds
            .Where(id => excludedOfficerIds == null || !excludedOfficerIds.Contains(id))
            .ToList();
        var maximumOfficers = reserveOneOfficer && availableOfficerIds.Count > 1
            ? Math.Min(MaximumActivePiecesPerSide, availableOfficerIds.Count - 1)
            : MaximumActivePiecesPerSide;
        var officerIds = availableOfficerIds.Take(maximumOfficers).ToList();
        if (officerIds.Count == 0)
        {
            return new List<AttackOfficerDeploymentData>();
        }

        var troopPools = new List<(TroopType Type, int Count)>
        {
            (TroopType.Infantry, city.InfantryTroops * troopPercent / 100),
            (TroopType.Spearman, city.SpearmanTroops * troopPercent / 100),
            (TroopType.Cavalry, city.CavalryTroops * troopPercent / 100),
            (TroopType.Archer, city.ArcherTroops * troopPercent / 100),
            (TroopType.Crossbow, city.CrossbowTroops * troopPercent / 100),
            (TroopType.Siege, city.SiegeTroops * troopPercent / 100)
        }.Where(item => item.Count > 0).ToList();
        if (troopPools.Count == 0)
        {
            return new List<AttackOfficerDeploymentData>();
        }

        var result = new List<AttackOfficerDeploymentData>();
        for (var index = 0; index < officerIds.Count; index++)
        {
            var poolIndex = index % troopPools.Count;
            var remainingOfficersForPool = 1 + (officerIds.Count - index - 1) / troopPools.Count;
            var count = Math.Max(0, troopPools[poolIndex].Count / remainingOfficersForPool);
            if (count <= 0)
            {
                continue;
            }

            result.Add(new AttackOfficerDeploymentData
            {
                OfficerId = officerIds[index],
                TroopType = troopPools[poolIndex].Type,
                TroopCount = count
            });
            troopPools[poolIndex] = (troopPools[poolIndex].Type, troopPools[poolIndex].Count - count);
        }

        return result;
    }

    private static CityData? ResolveReturnCity(
        WorldState world,
        ActiveBattleCampaignData campaign,
        CampaignBattleTeamData team,
        CampaignBattleSide winner,
        bool attackerWonCity)
    {
        var reinforcement = campaign.Reinforcements.FirstOrDefault(order => order.Id == team.ReinforcementOrderId);
        if (reinforcement != null && reinforcement.FactionId != campaign.AttackerFactionId && reinforcement.FactionId != campaign.DefenderFactionId)
        {
            return world.GetCity(reinforcement.SourceCityId);
        }

        if (team.Side == CampaignBattleSide.Attacker)
        {
            return attackerWonCity ? world.GetCity(campaign.TargetCityId) : world.GetCity(campaign.SourceCityId);
        }

        return winner == CampaignBattleSide.Defender || !attackerWonCity
            ? world.GetCity(campaign.TargetCityId)
            : null;
    }

    private static void RemoveOfficerFromAllCities(WorldState world, int officerId)
    {
        foreach (var city in world.Cities)
        {
            city.OfficerIds.Remove(officerId);
        }
    }

    private static void ReturnUndeployedReinforcements(WorldState world, ActiveBattleCampaignData campaign)
    {
        foreach (var order in campaign.Reinforcements.Where(order => order.Status is ReinforcementStatus.Traveling or ReinforcementStatus.Arrived or ReinforcementStatus.Reserve))
        {
            var sourceCity = world.GetCity(order.SourceCityId);
            if (sourceCity == null)
            {
                continue;
            }

            foreach (var team in order.Teams.Where(team => !campaign.Teams.Contains(team)))
            {
                sourceCity.AddTroops(team.TroopType, team.ActiveTroops + team.WoundedTroops);
                if (team.TroopType == TroopType.Siege && team.SiegeEngineType != SiegeEngineType.None)
                {
                    var engines = new SiegeEngineAllocationData();
                    switch (team.SiegeEngineType)
                    {
                        case SiegeEngineType.Ram: engines.Ram = 1; break;
                        case SiegeEngineType.Catapult: engines.Catapult = 1; break;
                        case SiegeEngineType.Ladder: engines.Ladder = 1; break;
                    }
                    sourceCity.AddSiegeEngineAllocation(engines);
                }
            }

            sourceCity.Gold += order.Gold;
            sourceCity.Food += order.Food;
            order.Status = ReinforcementStatus.Returned;
        }
    }

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

    private static void ValidateCityCanSupply(
        CityData city,
        IReadOnlyCollection<AttackOfficerDeploymentData> deployments,
        TroopAllocationData allocation,
        int gold,
        int food)
    {
        if ((deployments.Count == 0 && gold <= 0 && food <= 0) || deployments.Any(item => IsInvalidDeployment(item, city)))
        {
            throw new InvalidOperationException("The reinforcement deployment is invalid.");
        }

        if (allocation.Infantry > city.InfantryTroops ||
            allocation.Spearman > city.SpearmanTroops ||
            allocation.Cavalry > city.CavalryTroops ||
            allocation.Archer > city.ArcherTroops ||
            allocation.Crossbow > city.CrossbowTroops ||
            allocation.Siege > city.SiegeTroops ||
            gold < 0 || gold > city.Gold || food < 0 || food > city.Food)
        {
            throw new InvalidOperationException("The source city lacks the selected troops or resources.");
        }

        var siege = BuildSiegeEngineAllocation(deployments);
        if (siege.Ram > city.RamCount || siege.Catapult > city.CatapultCount || siege.Ladder > city.LadderCount)
        {
            throw new InvalidOperationException("The source city lacks the selected siege engines.");
        }
    }

    private static bool IsInvalidDeployment(AttackOfficerDeploymentData deployment, CityData city) =>
        deployment.OfficerId <= 0 || !city.OfficerIds.Contains(deployment.OfficerId) || deployment.TroopCount <= 0;

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

    private static TroopAllocationData BuildTroopAllocation(IEnumerable<AttackOfficerDeploymentData> deployments)
    {
        var allocation = new TroopAllocationData();
        foreach (var item in deployments)
        {
            switch (item.TroopType)
            {
                case TroopType.Infantry: allocation.Infantry += item.TroopCount; break;
                case TroopType.Spearman: allocation.Spearman += item.TroopCount; break;
                case TroopType.Cavalry: allocation.Cavalry += item.TroopCount; break;
                case TroopType.Archer: allocation.Archer += item.TroopCount; break;
                case TroopType.Crossbow: allocation.Crossbow += item.TroopCount; break;
                case TroopType.Siege: allocation.Siege += item.TroopCount; break;
            }
        }

        return allocation;
    }

    private static SiegeEngineAllocationData BuildSiegeEngineAllocation(IEnumerable<AttackOfficerDeploymentData> deployments)
    {
        var result = new SiegeEngineAllocationData();
        foreach (var item in deployments.Where(item => item.TroopType == TroopType.Siege))
        {
            switch (item.SiegeEngineType)
            {
                case SiegeEngineType.Ram: result.Ram++; break;
                case SiegeEngineType.Catapult: result.Catapult++; break;
                case SiegeEngineType.Ladder: result.Ladder++; break;
            }
        }

        return result;
    }

    private static List<AttackOfficerDeploymentData> CloneDeployments(IEnumerable<AttackOfficerDeploymentData> deployments) =>
        deployments.Select(CloneDeployment).ToList();

    private static AttackOfficerDeploymentData CloneDeployment(AttackOfficerDeploymentData item) => new()
    {
        OfficerId = item.OfficerId,
        TroopType = item.TroopType,
        TroopCount = item.TroopCount,
        SiegeEngineType = item.SiegeEngineType
    };

    private static CampaignBattleTeamData CloneTeam(CampaignBattleTeamData team) => new()
    {
        Id = team.Id,
        FactionId = team.FactionId,
        Side = team.Side,
        ControllerType = team.ControllerType,
        OfficerId = team.OfficerId,
        TroopType = team.TroopType,
        SiegeEngineType = team.SiegeEngineType,
        ActiveTroops = team.ActiveTroops,
        WoundedTroops = team.WoundedTroops,
        MaximumTroops = team.MaximumTroops,
        Morale = team.Morale,
        ReinforcementOrderId = team.ReinforcementOrderId,
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
