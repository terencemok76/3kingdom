using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;
using static ThreeKingdom.Core.CampaignCaptiveService;
using static ThreeKingdom.Core.CampaignRetreatService;

namespace ThreeKingdom.Core;

internal static class CampaignSettlementService
{
    // Orders not yet deployed remain outside campaign.Teams, but their troops
    // were dispatched and are returned when the campaign ends.
    private static bool IsUndeployedTeam(ActiveBattleCampaignData campaign, CampaignBattleTeamData team) =>
        !campaign.Teams.Contains(team) || team.Location == CampaignTeamLocation.Reserve && !team.HasEnteredBattle;

    private static List<CampaignBattleTeamData> GetUndeployedReportTeams(ActiveBattleCampaignData campaign) =>
        campaign.Reinforcements
            .Where(order => order.Status is ReinforcementStatus.Traveling or ReinforcementStatus.Arrived or ReinforcementStatus.Reserve)
            .SelectMany(order => order.Teams)
            .Where(team => IsUndeployedTeam(campaign, team))
            .Distinct().ToList();

    private static bool HasDeliveredSupplies(ReinforcementOrderData order) =>
        order.SuppliesDelivered ?? order.Status == ReinforcementStatus.Deployed;

    private static bool HasParticipatingTeam(ActiveBattleCampaignData campaign, ReinforcementOrderData order) =>
        order.Teams.Any(team => campaign.Teams.Contains(team) && team.HasEnteredBattle);

    private static bool HasArrivedSupport(ActiveBattleCampaignData campaign, ReinforcementOrderData order) =>
        HasDeliveredSupplies(order) || HasParticipatingTeam(campaign, order);

    private static List<WorldState.BattleParticipantContributionData> UpdateParticipantContributions(ActiveBattleCampaignData campaign, List<CampaignBattleTeamData> undeployedTeams, HashSet<CampaignBattleTeamData> disbandedTeams)
    {
        var contributions = new List<WorldState.BattleParticipantContributionData>();
        foreach (var participant in campaign.Participants)
        {
            var teams = campaign.Teams.Where(team =>
                team.FactionId == participant.FactionId && team.Side == participant.Side && !undeployedTeams.Contains(team)).ToList();
            var pending = undeployedTeams.Where(team => team.FactionId == participant.FactionId && team.Side == participant.Side).ToList();
            var committed = teams.Concat(pending).Sum(team => Math.Max(0, team.MaximumTroops));
            var active = teams.Sum(team => Math.Max(0, team.ActiveTroops));
            var wounded = teams.Sum(team => Math.Max(0, team.WoundedTroops));
            var returned = teams.Sum(team => Math.Max(0, team.ReturnedTroops)) +
                pending.Where(team => !disbandedTeams.Contains(team)).Sum(team => Math.Max(0, team.ActiveTroops) + Math.Max(0, team.WoundedTroops));
            var disbanded = teams.Sum(team => Math.Max(0, team.DisbandedTroops)) +
                pending.Where(disbandedTeams.Contains).Sum(team => Math.Max(0, team.ActiveTroops) + Math.Max(0, team.WoundedTroops));
            participant.TroopsCommitted = Math.Max(participant.TroopsCommitted, committed);
            participant.TroopsLost = Math.Max(0, participant.TroopsCommitted - active - wounded - returned - disbanded);
            var deliveredOrders = campaign.Reinforcements.Where(order =>
                order.FactionId == participant.FactionId && order.Side == participant.Side &&
                HasArrivedSupport(campaign, order)).ToList();
            var deployedTroops = deliveredOrders.SelectMany(order => order.Teams)
                .Where(team => campaign.Teams.Contains(team) && team.HasEnteredBattle)
                .Distinct().Sum(team => (long)Math.Max(0, team.MaximumTroops));
            var deliveredSupplies = deliveredOrders.Where(HasDeliveredSupplies).Sum(order => (long)Math.Max(0, order.Gold) + Math.Max(0, order.Food));
            contributions.Add(new WorldState.BattleParticipantContributionData
            {
                FactionId = participant.FactionId,
                Side = participant.Side,
                TroopsCommitted = participant.TroopsCommitted,
                TroopsLost = participant.TroopsLost,
                GoldContribution = participant.GoldContribution,
                FoodContribution = participant.FoodContribution,
                TroopsReturned = returned,
                TroopsDisbanded = disbanded,
                ActiveTroops = active,
                WoundedTroops = wounded,
                BattleDaysParticipated = participant.BattleDaysParticipated,
                SupportArrived = participant.IsMainFaction || deliveredOrders.Count > 0,
                ContributionScore = (int)Math.Min(1000L, deployedTroops / 100 + participant.TroopsLost / 200 +
                    (long)participant.BattleDaysParticipated * 20 + deliveredSupplies / 100)
            });
        }

        return contributions;
    }

    private static void ApplyAlliedCampaignSettlement(
        WorldState world,
        ActiveBattleCampaignData campaign,
        WorldState.BattleReportData report,
        List<BattleInvitationData> arrivedInvitations,
        CampaignBattleSide winner,
        CityData payerCity)
    {
        foreach (var group in arrivedInvitations.GroupBy(item => (item.InviterFactionId, item.InvitedFactionId, item.Side)))
        {
            var invitation = group.First();
            var contribution = report.ParticipantContributions.FirstOrDefault(item =>
                item.FactionId == invitation.InvitedFactionId && item.Side == invitation.Side);
            if (contribution == null)
            {
                continue;
            }

            // A campaign can end while support is still traveling or waiting
            // for placement. Its resources return; neither side broke a promise.
            if (contribution.SupportArrived == false) continue;

            var deployedTroops = contribution.TroopsCommitted > 0;
            var suppliedResources = contribution.GoldContribution > 0 || contribution.FoodContribution > 0;
            contribution.PromiseFulfilled = deployedTroops || suppliedResources;

            var relation = world.GetDiplomacyRelation(invitation.InviterFactionId, invitation.InvitedFactionId);
            var trustObserver = invitation.InviterFactionId;
            if (!contribution.PromiseFulfilled)
            {
                contribution.RelationDelta = -12;
                contribution.TrustDelta = -18;
            }
            else
            {
                var allyWon = invitation.Side == winner;
                contribution.RelationDelta = allyWon ? 8 : 3;
                contribution.TrustDelta = allyWon ? 12 : 6;
                if (allyWon)
                {
                    var rewardCity = world.Cities
                        .Where(city => city.OwnerFactionId == invitation.InvitedFactionId)
                        .OrderBy(city => city.Id)
                        .FirstOrDefault();
                    if (rewardCity == null)
                    {
                        continue;
                    }

                    var requestedGold = (int)Math.Min(int.MaxValue, group.Sum(item => (long)Math.Max(0, item.PromisedGold)));
                    var requestedFood = (int)Math.Min(int.MaxValue, group.Sum(item => (long)Math.Max(0, item.PromisedFood)));
                    var calculatedGold = Math.Clamp(contribution.ContributionScore * 2, 40, 300);
                    var calculatedFood = Math.Clamp(contribution.ContributionScore * 4, 80, 600);
                    contribution.RewardGold = Math.Min(Math.Max(requestedGold, calculatedGold), Math.Max(0, payerCity.Gold));
                    contribution.RewardFood = Math.Min(Math.Max(requestedFood, calculatedFood), Math.Max(0, payerCity.Food));
                    payerCity.Gold -= contribution.RewardGold;
                    payerCity.Food -= contribution.RewardFood;
                    rewardCity.Gold += contribution.RewardGold;
                    rewardCity.Food += contribution.RewardFood;
                    if (contribution.RewardGold < requestedGold || contribution.RewardFood < requestedFood)
                    {
                        contribution.PromiseFulfilled = false;
                        contribution.RelationDelta = -12;
                        contribution.TrustDelta = -18;
                        // The ally loses confidence in the faction that promised payment.
                        trustObserver = invitation.InvitedFactionId;
                    }
                }
            }

            if (relation == null)
            {
                continue;
            }

            relation.RelationScore = Math.Clamp(relation.RelationScore + contribution.RelationDelta, -100, 100);
            DiplomacyScoreRules.AdjustTrust(relation, trustObserver, contribution.TrustDelta);
            relation.LastUpdatedYear = campaign.CurrentYear;
            relation.LastUpdatedMonth = campaign.CurrentMonth;
        }
    }


    internal static void CompleteCampaign(WorldState world, ActiveBattleCampaignData campaign, CampaignBattleSide winner)
    {
        if (campaign.SettlementCompleted) return;
        CampaignReorganizationRules.SynchronizeReinforcementTeamReferences(campaign);
        foreach (var invitation in campaign.Invitations.Where(item => item.Status == BattleInvitationStatus.Pending))
        {
            invitation.Status = BattleInvitationStatus.Cancelled;
            invitation.DecisionReason = "Campaign ended before a response.";
        }
        campaign.IsAwaitingPlayerSupportResponse = false;
        var sourceCity = world.GetCity(campaign.SourceCityId);
        var targetCity = world.GetCity(campaign.TargetCityId);
        if (sourceCity == null || targetCity == null)
        {
            campaign.Stage = CampaignStage.Resolved;
            campaign.SettlementCompleted = true;
            return;
        }

        // Falling back inside the defended city is only a tactical movement.
        // If the campaign ends before the siege, these teams still need their
        // normal settlement; NeighborCity teams were already returned.
        foreach (var team in campaign.Teams.Where(team => team.Side == CampaignBattleSide.Defender &&
                     team.Location == CampaignTeamLocation.InnerCity && team.RetreatDestinationCityId == targetCity.Id))
            team.RetreatDestinationCityId = 0;

        var winnerFactionId = winner == CampaignBattleSide.Attacker
            ? campaign.AttackerFactionId
            : campaign.DefenderFactionId;
        // A city defender can be represented both by its city roster and its
        // campaign team.  Capture resolution visits both sources, so retain
        // one authoritative capture per officer.
        var capturedOfficerIds = new HashSet<int>();
        var undeployedTeams = GetUndeployedReportTeams(campaign);
        var attackerTeams = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Attacker && !undeployedTeams.Contains(team)).ToList();
        var defenderTeams = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Defender && !undeployedTeams.Contains(team)).ToList();
        var attackerWonCity = winner == CampaignBattleSide.Attacker && campaign.Stage == CampaignStage.CityBattle;
        var disbandedTeams = campaign.Reinforcements.Where(order =>
                order.Status is ReinforcementStatus.Traveling or ReinforcementStatus.Arrived or ReinforcementStatus.Reserve &&
                FindReinforcementReturnCity(world, campaign, order, attackerWonCity) == null)
            .SelectMany(order => order.Teams).Where(team => IsUndeployedTeam(campaign, team)).ToHashSet();
        var attackerPending = undeployedTeams.Where(team => team.Side == CampaignBattleSide.Attacker).ToList();
        var defenderPending = undeployedTeams.Where(team => team.Side == CampaignBattleSide.Defender).ToList();
        var attackerCommittedTroops = attackerTeams.Concat(attackerPending).Sum(team => Math.Max(0, team.MaximumTroops));
        var attackerActiveTroops = attackerTeams.Sum(team => Math.Max(0, team.ActiveTroops));
        var attackerWoundedTroops = attackerTeams.Sum(team => Math.Max(0, team.WoundedTroops));
        var attackerDisbanded = attackerTeams.Sum(team => Math.Max(0, team.DisbandedTroops)) + attackerPending.Where(disbandedTeams.Contains).Sum(team => Math.Max(0, team.ActiveTroops) + Math.Max(0, team.WoundedTroops));
        var attackerReturnedTroops = attackerTeams.Sum(team => Math.Max(0, team.ReturnedTroops)) + attackerPending.Where(team => !disbandedTeams.Contains(team)).Sum(team => Math.Max(0, team.ActiveTroops) + Math.Max(0, team.WoundedTroops));
        var defenderCommittedTroops = defenderTeams.Concat(defenderPending).Sum(team => Math.Max(0, team.MaximumTroops));
        var defenderActiveTroops = defenderTeams.Sum(team => Math.Max(0, team.ActiveTroops));
        var defenderWoundedTroops = defenderTeams.Sum(team => Math.Max(0, team.WoundedTroops));
        var defenderDisbanded = defenderTeams.Sum(team => Math.Max(0, team.DisbandedTroops)) + defenderPending.Where(disbandedTeams.Contains).Sum(team => Math.Max(0, team.ActiveTroops) + Math.Max(0, team.WoundedTroops));
        var defenderReturnedTroops = defenderTeams.Sum(team => Math.Max(0, team.ReturnedTroops)) + defenderPending.Where(team => !disbandedTeams.Contains(team)).Sum(team => Math.Max(0, team.ActiveTroops) + Math.Max(0, team.WoundedTroops));
        var report = new WorldState.BattleReportData
        {
            Id = world.BattleReports.Count == 0 ? 1 : world.BattleReports.Max(item => item.Id) + 1,
            Year = campaign.CurrentYear,
            Month = campaign.CurrentMonth,
            SourceCityId = campaign.SourceCityId,
            TargetCityId = campaign.TargetCityId,
            AttackerFactionId = campaign.AttackerFactionId,
            AttackerRulerOfficerId = campaign.AttackerRulerOfficerId,
            DefenderFactionId = campaign.DefenderFactionId,
            WinnerFactionId = winnerFactionId,
            Stage = campaign.Stage,
            AttackerCommittedTroops = attackerCommittedTroops,
            AttackerLostTroops = Math.Max(0, attackerCommittedTroops - attackerActiveTroops - attackerWoundedTroops - attackerReturnedTroops - attackerDisbanded),
            AttackerReturnedTroops = attackerReturnedTroops,
            AttackerDisbandedTroops = attackerDisbanded,
            AttackerActiveTroops = attackerActiveTroops,
            AttackerWoundedTroops = attackerWoundedTroops,
            AttackerGoldSpent = campaign.AttackerGoldSpent,
            AttackerFoodSpent = campaign.AttackerFoodSpent,
            AttackerGoldGained = campaign.AttackerGoldGained + (attackerWonCity ? Math.Max(0, campaign.DefenderGold) : 0),
            AttackerFoodGained = campaign.AttackerFoodGained + (attackerWonCity ? Math.Max(0, campaign.DefenderFood) : 0),
            DefenderCommittedTroops = defenderCommittedTroops,
            DefenderLostTroops = Math.Max(0, defenderCommittedTroops - defenderActiveTroops - defenderWoundedTroops - defenderReturnedTroops - defenderDisbanded),
            DefenderReturnedTroops = defenderReturnedTroops,
            DefenderDisbandedTroops = defenderDisbanded,
            DefenderActiveTroops = defenderActiveTroops,
            DefenderWoundedTroops = defenderWoundedTroops,
            DefenderGoldSpent = campaign.DefenderGoldSpent,
            DefenderFoodSpent = campaign.DefenderFoodSpent,
            DefenderGoldGained = campaign.DefenderGoldGained,
            DefenderFoodGained = campaign.DefenderFoodGained
        };
        report.ParticipantContributions = UpdateParticipantContributions(campaign, undeployedTeams, disbandedTeams);

        var arrivedInvitations = campaign.Invitations.Where(invitation => invitation.Status == BattleInvitationStatus.Accepted &&
            campaign.Reinforcements.Any(order => order.FactionId == invitation.InvitedFactionId && order.Side == invitation.Side &&
                HasArrivedSupport(campaign, order) &&
                order.InvitationId == invitation.Id)).ToList();

        ReturnUndeployedReinforcements(world, campaign, attackerWonCity);
        var deferredRulerSuccessions = new Dictionary<int, int>();

        if (attackerWonCity)
        {
            // Campaign captures use the same ownership rule as normal attacks:
            // no internal-affairs schedule may survive the change of faction.
            world.InternalAffairsSchedules.RemoveAll(schedule => schedule.CityId == targetCity.Id);
            targetCity.OwnerFactionId = campaign.AttackerFactionId;
            foreach (var officerId in targetCity.OfficerIds.ToList())
            {
                if (world.GetOfficer(officerId) is { } officer)
                {
                    officer.HomeCityId = targetCity.Id;
                    RemoveCapturedOfficerFromFaction(world, officer, deferredRulerSuccessions);
                    officer.CityId = 0;
                    officer.CaptiveFactionId = campaign.AttackerFactionId;
                    officer.JailedCityId = targetCity.Id;
                    capturedOfficerIds.Add(officerId);
                }
            }
            targetCity.OfficerIds.Clear();
        }

        CaptureDefeatedCampaignTeams(world, campaign, winner, targetCity.Id, capturedOfficerIds, deferredRulerSuccessions);
        ResolveDeferredCapturedRulerSuccessions(world, deferredRulerSuccessions);

        foreach (var team in campaign.Teams.Where(team =>
                     team.Location is not (CampaignTeamLocation.Eliminated or CampaignTeamLocation.Captured) &&
                     team.RetreatDestinationCityId <= 0))
        {
            var destination = ResolveReturnCity(world, campaign, team, winner, attackerWonCity);
            if (destination == null)
            {
                var foreignOrder = campaign.Reinforcements.FirstOrDefault(order => order.Id == team.ReinforcementOrderId &&
                    order.FactionId == team.FactionId && order.FactionId != campaign.AttackerFactionId && order.FactionId != campaign.DefenderFactionId);
                if (foreignOrder != null || team.Side == CampaignBattleSide.Attacker && team.FactionId == campaign.AttackerFactionId && !attackerWonCity)
                {
                    var active = Math.Max(0, team.ActiveTroops);
                    var wounded = Math.Max(0, team.WoundedTroops);
                    if (team.Side == CampaignBattleSide.Attacker)
                    {
                        report.AttackerActiveTroops -= active;
                        report.AttackerWoundedTroops -= wounded;
                        report.AttackerDisbandedTroops += active + wounded;
                    }
                    else
                    {
                        report.DefenderActiveTroops -= active;
                        report.DefenderWoundedTroops -= wounded;
                        report.DefenderDisbandedTroops += active + wounded;
                    }
                    var contribution = report.ParticipantContributions.FirstOrDefault(item => item.FactionId == team.FactionId && item.Side == team.Side);
                    if (contribution != null)
                    {
                        contribution.ActiveTroops -= active;
                        contribution.WoundedTroops -= wounded;
                        contribution.TroopsDisbanded += active + wounded;
                    }
                    DisbandReinforcementTeam(world, campaign, team, foreignOrder?.SourceCityId ?? campaign.SourceCityId, attackerWonCity);
                }
                continue;
            }

            var officerDestination = team.OfficerReturnCityId > 0
                ? world.GetCity(team.OfficerReturnCityId) is { } officerCity && officerCity.OwnerFactionId == GetRetreatFactionId(world, team) ? officerCity : destination
                : destination;
            if (!attackerWonCity && team.Side == CampaignBattleSide.Attacker && team.EquipmentType == BattleEquipmentType.None &&
                team.SiegeEngineType == SiegeEngineType.None && team.TroopOrigins != null && team.TroopOrigins.Count > 0)
                ReturnMixedTeamToOrigins(world, destination, team, officerDestination);
            else
                ReturnTeamToCity(world, destination, team, officerDestination);
            PlaceReinforcementOfficer(world, campaign, team, destination.Id, attackerWonCity);
        }

        if (attackerWonCity)
        {
            targetCity.Gold += campaign.AttackerGold + campaign.DefenderGold;
            targetCity.Food += campaign.AttackerFood + campaign.DefenderFood;
        }
        else
        {
            var attackerHome = sourceCity.OwnerFactionId == campaign.AttackerFactionId ? sourceCity : world.Cities
                .Where(city => city.OwnerFactionId == campaign.AttackerFactionId).OrderBy(city => city.Id).FirstOrDefault();
            if (attackerHome != null)
            {
                attackerHome.Gold += campaign.AttackerGold;
                attackerHome.Food += campaign.AttackerFood;
            }
            targetCity.Gold += campaign.DefenderGold;
            targetCity.Food += campaign.DefenderFood;
        }

        var settlementPayerCity = winner == CampaignBattleSide.Attacker && attackerWonCity
            ? targetCity
            : winner == CampaignBattleSide.Attacker ? (sourceCity.OwnerFactionId == campaign.AttackerFactionId ? sourceCity : world.Cities
                .Where(city => city.OwnerFactionId == campaign.AttackerFactionId).OrderBy(city => city.Id).FirstOrDefault()) : targetCity;
        if (settlementPayerCity != null)
            ApplyAlliedCampaignSettlement(world, campaign, report, arrivedInvitations, winner, settlementPayerCity);

        campaign.Stage = CampaignStage.Resolved;
        campaign.SettlementCompleted = true;
        campaign.IsAwaitingPlayerDecision = false;
        campaign.BattleSnapshotJson = string.Empty;
        report.CapturedOfficerIds = capturedOfficerIds.ToList();
        world.BattleReports.Add(report);

        foreach (var officerId in capturedOfficerIds)
        {
            if (world.PendingCapturedOfficerRecords.Any(record =>
                    record.WinnerFactionId == winnerFactionId && record.OfficerId == officerId))
            {
                continue;
            }

            world.PendingCapturedOfficerRecords.Add(new WorldState.PendingCapturedOfficerData
            {
                WinnerFactionId = winnerFactionId,
                WinnerCityId = targetCity.Id,
                OfficerId = officerId
            });
        }
    }

    /// <summary>
    /// Resolves a road-escort field battle without applying siege, capture, or
    /// generic attacker/defender return rules. The convoy cargo remains in the
    /// campaign until this point so it only reaches the destination on delivery.
    /// </summary>
    internal static void CompleteCaravanEscortCampaign(
        WorldState world,
        ActiveBattleCampaignData campaign,
        CaravanEscortOutcome outcome)
    {
        if (!campaign.IsCaravanEscortBattle || campaign.Stage == CampaignStage.Resolved)
        {
            return;
        }

        var sourceCity = world.GetCity(campaign.SourceCityId);
        var targetCity = world.GetCity(campaign.TargetCityId);
        if (sourceCity == null || targetCity == null)
        {
            campaign.Stage = CampaignStage.Resolved;
            return;
        }

        var delivered = outcome == CaravanEscortOutcome.Delivered;
        var returnCity = delivered ? targetCity : sourceCity;
        ReleaseDefeatedCaravanOfficers(world, campaign, sourceCity);
        foreach (var team in campaign.Teams.Where(team =>
                     team.Side == CampaignBattleSide.Defender &&
                     team.Location is not (CampaignTeamLocation.Eliminated or CampaignTeamLocation.Captured) &&
                     team.RetreatDestinationCityId <= 0))
        {
            ReturnTeamToCity(world, returnCity, team, returnCity);
            CampaignTroopOriginRules.ApplyCounts(team, 0, 0);
            team.Location = CampaignTeamLocation.NeighborCity;
            team.RetreatDestinationCityId = returnCity.Id;
        }

        var transferredGold = 0;
        var transferredFood = 0;
        if (outcome is CaravanEscortOutcome.Delivered or CaravanEscortOutcome.ReturnedToSource)
        {
            returnCity.Gold += campaign.CaravanGold;
            returnCity.Food += campaign.CaravanFood;
            returnCity.Horses += campaign.CaravanHorses;
            returnCity.Wood += campaign.CaravanWood;
            returnCity.Metal += campaign.CaravanMetal;
            returnCity.Stone += campaign.CaravanStone;
            transferredGold = campaign.CaravanGold;
            transferredFood = campaign.CaravanFood;
        }

        var attackerTeams = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Attacker).ToList();
        var defenderTeams = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Defender).ToList();
        // Vehicle markers are not troop casualties.  Count only actual troop
        // teams here so an infantry force of 200 does not report 202 committed
        // and 250 returned merely because a ram and cart have engineer crews.
        var defenderTroopTeams = defenderTeams
            .Where(team => team.EquipmentType == BattleEquipmentType.None)
            .ToList();
        var attackerCommitted = attackerTeams.Sum(team => Math.Max(0, team.MaximumTroops));
        var attackerActive = attackerTeams.Sum(team => Math.Max(0, team.ActiveTroops));
        var attackerWounded = attackerTeams.Sum(team => Math.Max(0, team.WoundedTroops));
        var defenderCommitted = defenderTroopTeams.Sum(team => Math.Max(0, team.MaximumTroops));
        var defenderArrived = defenderTroopTeams
            .Where(team => team.RetreatDestinationCityId == targetCity.Id)
            .Sum(team => Math.Max(0, team.ReturnedTroops));
        var defenderReturned = defenderTroopTeams
            .Where(team => team.RetreatDestinationCityId == sourceCity.Id)
            .Sum(team => Math.Max(0, team.ReturnedTroops));
        var winnerFactionId = outcome == CaravanEscortOutcome.Plundered
            ? campaign.AttackerFactionId
            : campaign.DefenderFactionId;

        world.BattleReports.Add(new WorldState.BattleReportData
        {
            Id = world.BattleReports.Count == 0 ? 1 : world.BattleReports.Max(item => item.Id) + 1,
            Year = campaign.CurrentYear,
            Month = campaign.CurrentMonth,
            SourceCityId = campaign.SourceCityId,
            TargetCityId = campaign.TargetCityId,
            AttackerFactionId = campaign.AttackerFactionId,
            AttackerRulerOfficerId = campaign.AttackerRulerOfficerId,
            DefenderFactionId = campaign.DefenderFactionId,
            WinnerFactionId = winnerFactionId,
            Stage = CampaignStage.FieldBattle,
            AttackerCommittedTroops = attackerCommitted,
            AttackerLostTroops = Math.Max(0, attackerCommitted - attackerActive - attackerWounded),
            AttackerActiveTroops = attackerActive,
            AttackerWoundedTroops = attackerWounded,
            AttackerGoldSpent = campaign.AttackerGoldSpent,
            AttackerFoodSpent = campaign.AttackerFoodSpent,
            AttackerGoldGained = campaign.AttackerGoldGained,
            AttackerFoodGained = campaign.AttackerFoodGained,
            DefenderCommittedTroops = defenderCommitted,
            DefenderLostTroops = Math.Max(0, defenderCommitted - defenderArrived - defenderReturned),
            DefenderReturnedTroops = defenderReturned,
            CaravanArrivedTroops = defenderArrived,
            DefenderGoldSpent = campaign.DefenderGoldSpent,
            DefenderFoodSpent = campaign.DefenderFoodSpent,
            DefenderGoldGained = transferredGold,
            DefenderFoodGained = transferredFood,
            CaravanEscortOutcome = outcome
        });

        campaign.CaravanEscortOutcome = outcome;
        campaign.CaravanGold = 0;
        campaign.CaravanFood = 0;
        campaign.CaravanHorses = 0;
        campaign.CaravanWood = 0;
        campaign.CaravanMetal = 0;
        campaign.CaravanStone = 0;
        campaign.Stage = CampaignStage.Resolved;
        campaign.IsAwaitingPlayerDecision = false;
        campaign.BattleSnapshotJson = string.Empty;
    }


    private static CityData? FindReinforcementReturnCity(WorldState world, ActiveBattleCampaignData campaign, ReinforcementOrderData order, bool attackerWonCity)
    {
        bool SafeDestination(CityData city) => city.OwnerFactionId == order.FactionId &&
            !(attackerWonCity && city.Id == campaign.TargetCityId && order.FactionId != campaign.AttackerFactionId);
        var source = world.GetCity(order.SourceCityId);
        return source != null && SafeDestination(source) ? source : world.Cities.Where(SafeDestination).OrderBy(city => city.Id).FirstOrDefault();
    }

    private static void ReturnUndeployedReinforcements(WorldState world, ActiveBattleCampaignData campaign, bool attackerWonCity)
    {
        foreach (var order in campaign.Reinforcements.Where(order => order.Status is ReinforcementStatus.Traveling or ReinforcementStatus.Arrived or ReinforcementStatus.Reserve))
        {
            var sourceCity = FindReinforcementReturnCity(world, campaign, order, attackerWonCity);
            if (sourceCity == null)
            {
                foreach (var team in order.Teams.Where(team => IsUndeployedTeam(campaign, team)))
                {
                    DisbandReinforcementTeam(world, campaign, team, order.SourceCityId, attackerWonCity);
                }
                order.Status = ReinforcementStatus.Cancelled;
                continue;
            }

            foreach (var team in order.Teams.Where(team => IsUndeployedTeam(campaign, team)))
            {
                if (team.EquipmentType == BattleEquipmentType.None && team.SiegeEngineType == SiegeEngineType.None && team.TroopOrigins != null && team.TroopOrigins.Count > 0)
                    ReturnMixedTeamToOrigins(world, sourceCity, team, sourceCity,
                        attackerWonCity && order.FactionId != campaign.AttackerFactionId ? campaign.TargetCityId : 0);
                else
                    ReturnTeamToCity(world, sourceCity, team);
                PlaceReinforcementOfficer(world, campaign, team, sourceCity.Id, attackerWonCity);
                CampaignTroopOriginRules.ApplyCounts(team, 0, 0);
                team.Location = CampaignTeamLocation.NeighborCity;
                team.RetreatDestinationCityId = sourceCity.Id;
            }

            if (order.SuppliesDelivered != true)
            {
                sourceCity.Gold += order.Gold;
                sourceCity.Food += order.Food;
            }
            order.Status = ReinforcementStatus.Returned;
        }
    }

    private static void DisbandReinforcementTeam(WorldState world, ActiveBattleCampaignData campaign, CampaignBattleTeamData team, int sourceCityId, bool attackerWonCity)
    {
        PlaceReinforcementOfficer(world, campaign, team, sourceCityId, attackerWonCity);
        CampaignTroopOriginRules.ApplyCounts(team, 0, 0);
        team.Location = CampaignTeamLocation.Eliminated;
    }

    private static void PlaceReinforcementOfficer(WorldState world, ActiveBattleCampaignData campaign, CampaignBattleTeamData team, int fallbackCityId, bool attackerWonCity)
    {
        if (world.GetOfficer(team.OfficerId) is { } officer && officer.CaptiveFactionId <= 0 &&
            (officer.DeathYear <= 0 || world.Year <= officer.DeathYear))
        {
            var owner = world.Factions.FirstOrDefault(faction => faction.OfficerIds.Contains(officer.Id) || faction.RulerOfficerId == officer.Id);
            if (owner == null) return;
            bool SafeHome(CityData city) => city.OwnerFactionId == owner.Id &&
                !(attackerWonCity && city.Id == campaign.TargetCityId && owner.Id != campaign.AttackerFactionId);
            var preferred = world.GetCity(team.OfficerReturnCityId);
            var fallback = world.GetCity(fallbackCityId);
            var officerHome = preferred != null && SafeHome(preferred) ? preferred :
                fallback != null && SafeHome(fallback) ? fallback : world.Cities.Where(SafeHome).OrderBy(city => city.Id).FirstOrDefault();
            RemoveOfficerFromAllCities(world, officer.Id);
            if (officerHome != null)
            {
                officerHome.OfficerIds.Add(officer.Id);
                officer.CityId = officerHome.Id;
            }
            else
            {
                if (owner != null)
                {
                    owner.OfficerIds.Remove(officer.Id);
                    if (owner.ChiefStrategistOfficerId == officer.Id) owner.ChiefStrategistOfficerId = 0;
                    if (owner.RulerOfficerId == officer.Id) { owner.RulerOfficerId = 0; officer.DisplacedRulerFactionId = owner.Id; }
                }
                officer.Appointments.Clear();
                officer.CityId = world.GetCity(fallbackCityId)?.Id ?? campaign.SourceCityId;
                officer.CaptiveFactionId = 0;
                officer.JailedCityId = 0;
                officer.FreeOfficerStayMonths = 2;
            }
        }
    }

}
