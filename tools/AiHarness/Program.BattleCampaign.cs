using System.IO;
using System.Reflection;
using ThreeKingdom.Battle;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;
using ThreeKingdom.UI;

namespace AiHarness;

internal static partial class Program
{
    private static void RunEnvoyExcludedFromDeploymentTest()
    {
        var world = CreateBattleCampaignWorld();
        var attack = CreateBattleCampaignAttack();
        attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData { SourceCityId = 4, IsAllianceRequest = true, EnvoyOfficerId = 202, RequestedTroops = 100 });
        var target = world.GetCity(2)!;
        var original = target.Troops;
        var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.FieldIntercept);
        Assert(campaign.Teams.All(team => team.OfficerId != 202) && campaign.Teams.Any(team => team.Side == CampaignBattleSide.Defender && team.OfficerId == 201) &&
               campaign.Teams.Where(team => team.Side == CampaignBattleSide.Defender).Sum(team => team.ActiveTroops) + target.Troops == original,
            "envoy deployment", "assigned envoy is excluded from automatic field and inner-city teams without losing troops");
    }

    private static void RunEnvoyResponseTimingTest()
    {
        (WorldState World, ActiveBattleCampaignData Campaign, BattleInvitationData Invitation) Setup(BattleInvitationSupportType support = BattleInvitationSupportType.Resources)
        {
            var world = CreateBattleCampaignWorld();
            world.Cities.Add(TestHelpers.City(4, "Support", 3, 1500, 4000, 4000, new[] { 301 }, new[] { 2 }));
            world.Officers.Add(TestHelpers.Officer(301, "Support officer", 4));
            world.Officers.Add(TestHelpers.Officer(203, "Envoy", 2));
            world.GetOfficer(203)!.Intelligence = 40; world.GetOfficer(203)!.Charm = 40;
            world.GetFaction(2)!.OfficerIds.Add(203);
            world.Factions.Add(TestHelpers.Faction(3, "Support", false, 301, new[] { 301 }));
            world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 2, FactionBId = 3, Status = DiplomacyStatusType.Alliance, RemainingMonths = 12, RelationScore = 100 });
            var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
            world.GetCity(2)!.OfficerIds.Add(203);
            var invitation = BattleCampaignService.CreateInvitation(campaign, 2, 3, CampaignBattleSide.Defender, support, 203,
                support == BattleInvitationSupportType.Troops ? 500 : 0, 50, 90, 4);
            BattleCampaignService.ScheduleInvitationResponse(world, campaign, invitation);
            return (world, campaign, invitation);
        }

        var (world, campaign, invitation) = Setup();
        var source = world.GetCity(4)!;
        var beforeGold = source.Gold; var beforeFood = source.Food;
        Assert(invitation.ResponseBattleDays == 3 && BattleCampaignService.IsOfficerCommitted(world, 203) && campaign.Reinforcements.Count == 0,
            "envoy response timing", "low-skill envoy waits three days and remains committed");
        var fast = new OfficerData { Id = 204, Intelligence = 90, Charm = 90 };
        world.Officers.Add(fast);
        Assert(BattleCampaignService.GetInvitationResponseDays(world, 2, 3, 4, 204) == 1,
            "envoy response timing", "high-skill envoy receives a faster response");
        world.Cities.Add(TestHelpers.City(5, "Distant support", 3, 100, 100, 1200, Array.Empty<int>(), new[] { 4 }));
        Assert(BattleCampaignService.GetInvitationResponseDays(world, 2, 3, 5, 204) == 2,
            "envoy response timing", "an additional route link increases response time");
        BattleCampaignService.AdvanceCompletedBattleDay(campaign, world);
        BattleCampaignService.AdvanceCompletedBattleDay(campaign, world);
        Assert(invitation.Status == BattleInvitationStatus.Pending && invitation.ResponseBattleDays == 1 && campaign.Reinforcements.Count == 0 && source.Gold == beforeGold && source.Food == beforeFood,
            "envoy response timing", "no troops or supplies reserved before response");
        var loaded = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(campaign))!;
        world.ActiveBattleCampaigns.Remove(campaign); world.ActiveBattleCampaigns.Add(loaded);
        invitation = loaded.Invitations.Single();
        BattleCampaignService.AdvanceCompletedBattleDay(loaded, world);
        var order = loaded.Reinforcements.Single();
        Assert(invitation.Status == BattleInvitationStatus.Accepted && order.RemainingBattleDays == 2 && order.Status == ReinforcementStatus.Traveling &&
               source.Gold == beforeGold - 50 && source.Food == beforeFood - 90 && !BattleCampaignService.IsOfficerCommitted(world, 203),
            "envoy response timing", "saved response resolves once and starts a separate travel clock");
        var supplyGold = loaded.DefenderGold; var supplyFood = loaded.DefenderFood;
        BattleCampaignService.AdvanceCompletedBattleDay(loaded, world);
        Assert(order.RemainingBattleDays == 1 && loaded.DefenderGold == supplyGold && loaded.DefenderFood == supplyFood,
            "envoy response timing", "supplies remain unavailable during travel");
        BattleCampaignService.AdvanceCompletedBattleDay(loaded, world);
        Assert(order.Status == ReinforcementStatus.Deployed && loaded.DefenderGold == supplyGold + 50 && loaded.DefenderFood == supplyFood + 90,
            "envoy response timing", "accepted supplies arrive after response and travel");

        var (cancelWorld, cancelCampaign, cancelInvitation) = Setup();
        var cancelSource = cancelWorld.GetCity(4)!; var cancelGold = cancelSource.Gold; var cancelFood = cancelSource.Food;
        BattleCampaignService.CompleteCampaign(cancelWorld, cancelCampaign, CampaignBattleSide.Defender);
        Assert(cancelInvitation.Status == BattleInvitationStatus.Cancelled && cancelCampaign.Reinforcements.Count == 0 &&
               cancelSource.Gold == cancelGold && cancelSource.Food == cancelFood && !BattleCampaignService.IsOfficerCommitted(cancelWorld, 203),
            "envoy response timing", "campaign ending cancels the pending request and releases envoy without spending resources");

        var (declineWorld, declineCampaign, declineInvitation) = Setup(BattleInvitationSupportType.Troops);
        declineWorld.GetDiplomacyRelation(2, 3)!.Status = DiplomacyStatusType.Neutral;
        BattleCampaignService.AdvanceInvitationResponses(declineWorld, declineCampaign, declineInvitation.ResponseBattleDays);
        var returning = declineCampaign.Teams.Single(team => team.OfficerId == 203);
        Assert(declineInvitation.Status == BattleInvitationStatus.Declined && declineCampaign.Reinforcements.Count == 0 &&
               returning.Location == CampaignTeamLocation.Reserve && returning.AwaitingEnvoyReserveTroops && BattleCampaignService.IsOfficerCommitted(declineWorld, 203),
            "envoy response timing", "declined envoy returns to reserve and waits for troops");
        var donor = new CampaignBattleTeamData { Id = declineCampaign.Teams.Max(team => team.Id) + 1, FactionId = 2,
            Side = CampaignBattleSide.Defender, ControllerType = returning.ControllerType, OfficerId = 201,
            Location = CampaignTeamLocation.Reserve, OriginCityId = 8, ActiveTroops = 1200, MaximumTroops = 1200 };
        declineCampaign.Teams.Add(donor);
        BattleCampaignService.AdvanceCompletedBattleDay(declineCampaign, declineWorld);
        Assert(returning.ActiveTroops == 1000 && donor.ActiveTroops == 200 && !returning.AwaitingEnvoyReserveTroops &&
               returning.TroopOrigins.Single().CityId == 8 && returning.TroopOrigins.Single().ActiveTroops == 1000 &&
               returning.Location != CampaignTeamLocation.Reserve,
            "envoy return", "newly available reserves transfer once with their origin and allow deployment");

        var (siegeWorld, siegeCampaign, siegeInvitation) = Setup();
        siegeCampaign.Stage = CampaignStage.SiegePreparation;
        BattleCampaignService.AdvanceInvitationResponses(siegeWorld, siegeCampaign, BattleCampaignService.MaximumBattleDaysPerMonth);
        var siegeOrder = siegeCampaign.Reinforcements.Single();
        Assert(siegeInvitation.Status == BattleInvitationStatus.Accepted && siegeOrder.Status == ReinforcementStatus.Arrived && siegeOrder.RemainingBattleDays == 0,
            "envoy response timing", "siege preparation advances both response and remaining travel time");
        var (monthWorld, monthCampaign, monthInvitation) = Setup();
        monthCampaign.Stage = CampaignStage.SiegePreparation;
        var monthGold = monthCampaign.DefenderGold; var monthFood = monthCampaign.DefenderFood;
        CreateServices(monthWorld).Turn.AdvanceMonth();
        Assert(monthInvitation.Status == BattleInvitationStatus.Accepted && monthCampaign.Stage == CampaignStage.CityBattle &&
               monthCampaign.Reinforcements.Single().Status == ReinforcementStatus.Deployed &&
               monthCampaign.DefenderGold == monthGold + 50 && monthCampaign.DefenderFood == monthFood + 90,
            "envoy response timing", "monthly siege transition advances response, travel and delivery");
    }

    private static void ResolveScheduledAiReplies(WorldState world, ActiveBattleCampaignData campaign)
    {
        for (var day = 0; day < 6 && campaign.Invitations.Any(invitation => invitation.Status == BattleInvitationStatus.Pending &&
                     world.GetFaction(invitation.InvitedFactionId)?.IsPlayer != true); day++)
            BattleCampaignService.AdvanceInvitationResponses(world, campaign);
    }

    private static void RunAiPlayerSupportRequestTest()
    {
        (WorldState World, PendingCommandData Attack) Setup()
        {
            var world = CreateBattleCampaignWorld();
            world.GetFaction(1)!.IsPlayer = false;
            world.Cities.Add(TestHelpers.City(4, "Player ally", 3, 1500, 4000, 5000, new[] { 301 }, new[] { 2 }));
            world.Cities.Add(TestHelpers.City(5, "AI envoy city", 2, 500, 500, 1200, new[] { 203 }, new[] { 2 }));
            world.Officers.Add(TestHelpers.Officer(301, "Player officer", 4));
            world.Officers.Add(TestHelpers.Officer(203, "AI envoy", 5, intelligence: 90));
            world.Factions.Add(TestHelpers.Faction(3, "Player", true, 301, new[] { 301 }));
            world.GetFaction(2)!.OfficerIds.Add(203);
            world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 2, FactionBId = 3, Status = DiplomacyStatusType.Alliance, RemainingMonths = 12, RelationScore = 80 });
            var target = world.GetCity(2)!;
            target.InfantryTroops = 1000; target.SpearmanTroops = 0; target.CavalryTroops = 0; target.ArcherTroops = 0; target.CrossbowTroops = 0; target.SiegeTroops = 0;
            target.SyncLegacyTroops(); target.Gold = 0; target.Food = 0;
            var attack = CreateBattleCampaignAttack();
            attack.TroopsToSend = 3000; attack.OfficerIds.Add(101);
            return (world, attack);
        }

        var (world, attack) = Setup();
        var (deferredWorld, _) = Setup();
        deferredWorld.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 1, FactionBId = 3, Status = DiplomacyStatusType.Alliance, RemainingMonths = 12, RelationScore = 80 });
        var deferredServices = CreateServices(deferredWorld);
        var deferredSource = deferredWorld.GetCity(1)!;
        var initialTroops = deferredSource.Troops;
        var initialGold = deferredSource.Gold;
        var issued = deferredServices.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Attack, ActorFactionId = 1, SourceCityId = 1, TargetCityId = 2,
            TroopsToSend = 500, GoldToSend = 50, OfficerIds = new List<int> { 101 }
        });
        var deferredAttack = deferredWorld.PendingCommands.Single(item => item.Type == CommandType.Attack);
        var deferred = AiPlayerCampaignSupportRules.TryDeferAttackForPlayerSupport(deferredWorld, deferredAttack);
        var waiting = deferred && !AiPlayerCampaignSupportRules.IsDeferredResponseReady(deferredWorld, deferredAttack) &&
            BattleCampaignService.IsOfficerCommitted(deferredWorld, 101) && BattleCampaignService.IsOfficerCommitted(deferredWorld, 102);
        deferredServices.Turn.ResolvePendingCommands(deferredServices.Resolver);
        var preserved = deferredWorld.PendingCommands.Contains(deferredAttack) && deferredSource.Troops == initialTroops - 500 && deferredSource.Gold == initialGold - 50;
        var savedWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(deferredWorld))!;
        var savedAttack = savedWorld.PendingCommands.Single(item => item.IsAwaitingPrebattleSupport);
        var savedWaiting = !AiPlayerCampaignSupportRules.IsDeferredResponseReady(savedWorld, savedAttack);
        savedWorld.Month = savedWorld.Month == 12 ? 1 : savedWorld.Month + 1;
        if (savedWorld.Month == 1) savedWorld.Year++;
        var ready = AiPlayerCampaignSupportRules.IsDeferredResponseReady(savedWorld, savedAttack);
        var invalidWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(savedWorld))!;
        var invalidAttack = invalidWorld.PendingCommands.Single(item => item.IsAwaitingPrebattleSupport);
        invalidWorld.GetCity(2)!.OwnerFactionId = 3;
        CreateServices(invalidWorld).Resolver.ResolvePendingCommand(invalidAttack);
        var invalidReturned = invalidWorld.GetCity(1)!.Troops == initialTroops && invalidWorld.GetCity(1)!.Gold == initialGold &&
            !invalidWorld.PendingCommands.Contains(invalidAttack);
        var savedRequest = savedAttack.PlayerSupportRequest!;
        savedRequest.SourceCityId = 4; savedRequest.Food = 10; savedRequest.Status = BattleInvitationStatus.Accepted;
        var acceptedResult = CreateServices(savedWorld).Resolver.ResolvePendingCommand(savedAttack);
        var accepted = acceptedResult.ActiveBattleCampaignId > 0 && savedWorld.ActiveBattleCampaigns.Single().Reinforcements.Any(order => order.FactionId == 3 && order.Food == 10);
        AiPlayerCampaignSupportRules.CancelDeferredAttack(deferredWorld, deferredAttack);
        var returned = deferredSource.Troops == initialTroops && deferredSource.Gold == initialGold && !BattleCampaignService.IsOfficerCommitted(deferredWorld, 101);
        Assert(issued.Success && waiting && preserved && savedWaiting && ready && returned && invalidReturned && accepted,
            "AI deferred prebattle support", $"issued={issued.Success}, waiting={waiting}, preserved={preserved}, saved={savedWaiting}, ready={ready}, returned={returned}, invalid={invalidReturned}, accepted={accepted}");
        var (conflictWorld, conflictAttack) = Setup();
        var conflictRequest = AiPlayerCampaignSupportRules.CreatePrebattleRequest(conflictWorld, conflictAttack)!;
        conflictRequest.SourceCityId = 4; conflictRequest.Gold = 50;
        conflictWorld.PrewarAllianceRequests.Add(new PrewarAllianceRequestData
        {
            InviterFactionId = conflictAttack.ActorFactionId, AllyFactionId = 3, TargetCityId = conflictAttack.TargetCityId
        });
        Assert(!AiPlayerCampaignSupportRules.ValidateResponse(conflictWorld, conflictAttack, conflictRequest),
            "prebattle opposing support", "an attacker agreement prevents a previously offered defender response");
        var (newConflictWorld, newConflictAttack) = Setup();
        newConflictWorld.PrewarAllianceRequests.Add(new PrewarAllianceRequestData
        {
            InviterFactionId = newConflictAttack.ActorFactionId, AllyFactionId = 3, TargetCityId = newConflictAttack.TargetCityId
        });
        Assert(AiPlayerCampaignSupportRules.CreatePrebattleRequest(newConflictWorld, newConflictAttack)?.Side != CampaignBattleSide.Defender,
            "prebattle opposing support", "defender does not invite a player already reserved for the attacker");
        var playerCity = world.GetCity(4)!;
        var beforeTroops = playerCity.Troops; var beforeGold = playerCity.Gold; var beforeFood = playerCity.Food;
        var request = AiPlayerCampaignSupportRules.CreatePrebattleRequest(world, attack);
        Assert(request is { Side: CampaignBattleSide.Defender, RequestedTroops: > 0, RequestedGold: > 0, RequestedFood: > 0, Status: BattleInvitationStatus.Pending } &&
               playerCity.Troops == beforeTroops && playerCity.Gold == beforeGold && playerCity.Food == beforeFood,
            "AI player support", "AI requests mixed support without spending player resources");
        if (request == null) return;
        attack.PlayerSupportRequest = request;
        request.SourceCityId = 4; request.OfficerId = 301; request.TroopType = TroopType.Infantry;
        request.Troops = 300; request.Gold = 50; request.Food = 50;
        Assert(AiPlayerCampaignSupportRules.ValidateResponse(world, attack, request), "AI player support", "partial mixed response validates");
        world.GetOfficer(301)!.CaptiveFactionId = 2;
        Assert(!AiPlayerCampaignSupportRules.ValidateResponse(world, attack, request), "AI player support", "captured player officer cannot lead an accepted response");
        world.GetOfficer(301)!.CaptiveFactionId = 0;
        var (envoyWorld, envoyAttack) = Setup();
        envoyWorld.GetOfficer(203)!.CaptiveFactionId = 1;
        Assert(AiPlayerCampaignSupportRules.CreatePrebattleRequest(envoyWorld, envoyAttack) == null,
            "AI player support", "captured officers cannot be chosen as prebattle envoys");
        var pendingCopy = System.Text.Json.JsonSerializer.Deserialize<PendingCommandData>(System.Text.Json.JsonSerializer.Serialize(attack))!;
        Assert(pendingCopy.PlayerSupportRequest?.Status == BattleInvitationStatus.Pending && pendingCopy.PlayerSupportRequest.Troops == 300,
            "AI player support", "pending request and selected response survive serialization");
        request.Troops = 0; request.OfficerId = 0; request.Food = 0;
        Assert(AiPlayerCampaignSupportRules.ValidateResponse(world, attack, request), "AI player support", "gold-only support needs no officer");
        request.Gold = 0; request.Food = 50;
        Assert(AiPlayerCampaignSupportRules.ValidateResponse(world, attack, request), "AI player support", "food-only support needs no officer");
        request.Troops = 300; request.OfficerId = 301; request.Food = 0;
        Assert(AiPlayerCampaignSupportRules.ValidateResponse(world, attack, request), "AI player support", "troop-only support validates");
        var relation = world.GetDiplomacyRelation(2, 3)!;
        relation.Status = DiplomacyStatusType.Neutral;
        Assert(!AiPlayerCampaignSupportRules.ValidateResponse(world, attack, request), "AI player support", "non-allied player cannot send troops");
        relation.Status = DiplomacyStatusType.Alliance;
        request.Gold = 50; request.Food = 50;
        request.OfficerId = 0;
        Assert(!AiPlayerCampaignSupportRules.ValidateResponse(world, attack, request), "AI player support", "troops require an available officer");
        request.OfficerId = 301; request.Status = BattleInvitationStatus.Accepted;
        var services = CreateServices(world);
        var result = services.Resolver.ResolvePendingCommand(attack);
        var campaign = world.ActiveBattleCampaigns.SingleOrDefault();
        var order = campaign?.Reinforcements.SingleOrDefault(item => item.FactionId == 3);
        Assert(result.ActiveBattleCampaignId > 0 && campaign?.IsPlayerInvolved == true && order?.Status == ReinforcementStatus.Traveling &&
               order.Teams.Single().ControllerType == CampaignControllerType.Player && order.Teams.Single().ActiveTroops == 300 &&
               playerCity.Troops == beforeTroops - 300 && playerCity.Gold == beforeGold - 50 && playerCity.Food == beforeFood - 50,
            "AI player support", "accepted request starts campaign and reserves only selected support");

        var (declineWorld, declineAttack) = Setup();
        var declineRequest = AiPlayerCampaignSupportRules.CreatePrebattleRequest(declineWorld, declineAttack)!;
        var declineCity = declineWorld.GetCity(4)!;
        var declineTroops = declineCity.Troops; var declineGold = declineCity.Gold; var declineFood = declineCity.Food;
        declineRequest.Status = BattleInvitationStatus.Declined; declineAttack.PlayerSupportRequest = declineRequest;
        var declined = CreateServices(declineWorld).Resolver.ResolvePendingCommand(declineAttack);
        Assert(declined.ActiveBattleCampaignId <= 0 && declineWorld.ActiveBattleCampaigns.Count == 0 &&
               declineCity.Troops == declineTroops && declineCity.Gold == declineGold && declineCity.Food == declineFood,
            "AI player support", "decline leaves player resources untouched and retains strategic battle resolution");

        var (resourceWorld, resourceAttack) = Setup();
        var resourceRequest = AiPlayerCampaignSupportRules.CreatePrebattleRequest(resourceWorld, resourceAttack)!;
        resourceRequest.SourceCityId = 4; resourceRequest.Food = 50; resourceRequest.Status = BattleInvitationStatus.Accepted;
        resourceAttack.PlayerSupportRequest = resourceRequest;
        var resourceBefore = resourceWorld.GetCity(4)!.Food;
        var resourceResult = CreateServices(resourceWorld).Resolver.ResolvePendingCommand(resourceAttack);
        var resourceCampaign = resourceWorld.ActiveBattleCampaigns.SingleOrDefault();
        Assert(resourceResult.ActiveBattleCampaignId > 0 && resourceCampaign?.IsPlayerInvolved == true &&
               resourceCampaign.Reinforcements.Single(item => item.FactionId == 3).Teams.Count == 0 && resourceWorld.GetCity(4)!.Food == resourceBefore - 50,
            "AI player support", "resource-only acceptance starts a campaign without creating a troop team");

        var (attackerWorld, attackerAttack) = Setup();
        attackerWorld.GetDiplomacyRelation(2, 3)!.FactionAId = 1;
        attackerAttack.TroopsToSend = 500; attackerAttack.GoldToSend = 0; attackerAttack.FoodToSend = 0;
        attackerWorld.GetCity(2)!.InfantryTroops = 2000; attackerWorld.GetCity(2)!.SyncLegacyTroops();
        var attackerOffer = AiPlayerCampaignSupportRules.CreatePrebattleRequest(attackerWorld, attackerAttack);
        Assert(attackerOffer is { Side: CampaignBattleSide.Attacker, InviterFactionId: 1, RequestedTroops: > 0 },
            "AI player support", "AI attacker can request support from the player before combat");
    }

    private static void RunAiCampaignSupportRequestTest()
    {
        (WorldState World, ActiveBattleCampaignData Campaign) Setup(bool ally)
        {
            var world = CreateBattleCampaignWorld();
            world.Cities.Add(TestHelpers.City(4, "Support", 3, 1500, 4000, 4000, new[] { 301 }, new[] { 2 }));
            world.Officers.Add(TestHelpers.Officer(301, "Support officer", 4));
            world.Factions.Add(TestHelpers.Faction(3, "Support faction", false, 301, new[] { 301 }));
            world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 2, FactionBId = 3,
                Status = ally ? DiplomacyStatusType.Alliance : DiplomacyStatusType.Neutral, RemainingMonths = 12, RelationScore = 100 });
            var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
            world.Officers.Add(TestHelpers.Officer(203, "Available envoy", 2, intelligence: 90));
            world.GetCity(2)!.OfficerIds.Add(203);
            world.GetFaction(2)!.OfficerIds.Add(203);
            foreach (var team in campaign.Teams.Where(team => team.OfficerId > 0))
                team.ActiveTroops = team.Side == CampaignBattleSide.Defender ? 500 : 3000;
            return (world, campaign);
        }

        var (troopWorld, troopCampaign) = Setup(true);
        troopCampaign.DefenderFood = 10000; troopCampaign.DefenderGold = 10000;
        var troopRequest = AiCampaignSupportPlanner.TryRequestSupport(troopWorld, troopCampaign, CampaignBattleSide.Defender);
        ResolveScheduledAiReplies(troopWorld, troopCampaign);
        var troopOrder = troopCampaign.Reinforcements.SingleOrDefault();
        Assert(troopRequest?.SupportType == BattleInvitationSupportType.Troops && troopRequest.Status == BattleInvitationStatus.Accepted &&
               troopOrder?.Status == ReinforcementStatus.Traveling && troopOrder.RemainingBattleDays == 2 &&
               troopOrder.Teams.All(team => team.ControllerType == CampaignControllerType.Ai) &&
               AiCampaignSupportPlanner.TryRequestSupport(troopWorld, troopCampaign, CampaignBattleSide.Defender) == null,
            "AI support request", "weak AI requests allied troops once; AI envoy and troops remain AI controlled");

        var (playerWorld, playerCampaign) = Setup(true);
        playerWorld.GetFaction(1)!.IsPlayer = false; playerWorld.GetFaction(3)!.IsPlayer = true;
        playerCampaign.DefenderFood = 0; playerCampaign.DefenderGold = 0;
        var playerSource = playerWorld.GetCity(4)!;
        var playerGold = playerSource.Gold; var playerFood = playerSource.Food; var playerTroops = playerSource.Troops;
        var pendingPlayer = AiCampaignSupportPlanner.TryRequestSupport(playerWorld, playerCampaign, CampaignBattleSide.Defender);
        Assert(pendingPlayer?.InvitedFactionId == 3 && pendingPlayer.Status == BattleInvitationStatus.Pending && playerCampaign.Reinforcements.Count == 0 &&
               playerSource.Gold == playerGold && playerSource.Food == playerFood && playerSource.Troops == playerTroops,
            "AI support request", "ongoing campaign leaves player invitation pending without spending resources");
        var restoredPlayerCampaign = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(playerCampaign))!;
        Assert(restoredPlayerCampaign.Invitations.Single().Status == BattleInvitationStatus.Pending && restoredPlayerCampaign.Reinforcements.Count == 0,
            "AI support request", "ongoing player invitation remains pending after serialization");
        var (invalidWorld, invalidCampaign) = Setup(true);
        invalidWorld.GetFaction(1)!.IsPlayer = false; invalidWorld.GetFaction(3)!.IsPlayer = true;
        invalidCampaign.DefenderFood = 0; invalidCampaign.DefenderGold = 0;
        var invalidRequest = AiCampaignSupportPlanner.TryRequestSupport(invalidWorld, invalidCampaign, CampaignBattleSide.Defender)!;
        var invalidCity = invalidWorld.GetCity(4)!;
        var invalidGold = invalidCity.Gold; var invalidFood = invalidCity.Food; var invalidTroops = invalidCity.Troops;
        invalidWorld.GetOfficer(invalidRequest.EnvoyOfficerId)!.CaptiveFactionId = 1;
        BattleCampaignService.AdvanceInvitationResponses(invalidWorld, invalidCampaign, invalidRequest.ResponseBattleDays);
        Assert(invalidRequest.Status == BattleInvitationStatus.Declined && invalidCampaign.Reinforcements.Count == 0 &&
               invalidCity.Gold == invalidGold && invalidCity.Food == invalidFood && invalidCity.Troops == invalidTroops,
            "AI player support unavailable envoy", "captured envoy cancels the request at reply time without charging player resources");

        var (lateWorld, lateCampaign) = Setup(true);
        lateWorld.GetFaction(1)!.IsPlayer = false; lateWorld.GetFaction(3)!.IsPlayer = true;
        lateCampaign.DefenderFood = 0; lateCampaign.DefenderGold = 0;
        var lateRequest = AiCampaignSupportPlanner.TryRequestSupport(lateWorld, lateCampaign, CampaignBattleSide.Defender)!;
        BattleCampaignService.AdvanceInvitationResponses(lateWorld, lateCampaign, lateRequest.ResponseBattleDays);
        lateWorld.GetFaction(lateRequest.InviterFactionId)!.OfficerIds.Remove(lateRequest.EnvoyOfficerId);
        var lateAccepted = AiPlayerCampaignSupportRules.RespondToCampaignRequest(lateWorld, lateCampaign, lateRequest, new PlayerCampaignSupportRequestData(), true);
        Assert(!lateAccepted && lateRequest.Status == BattleInvitationStatus.Declined && lateCampaign.Reinforcements.Count == 0,
            "AI player support unavailable envoy", "an envoy changing faction after arrival invalidates the player response");
        if (pendingPlayer != null)
        {
            var response = new PlayerCampaignSupportRequestData { Side = pendingPlayer.Side, InviterFactionId = pendingPlayer.InviterFactionId,
                RequestedTroops = pendingPlayer.RequestedTroops, RequestedGold = pendingPlayer.RequestedGold, RequestedFood = pendingPlayer.RequestedFood,
                SourceCityId = 4, OfficerId = 301, TroopType = TroopType.Infantry, Troops = 100, Gold = 10, Food = 10 };
            Assert(!AiPlayerCampaignSupportRules.RespondToCampaignRequest(playerWorld, playerCampaign, pendingPlayer, response, true),
                "AI support request", "player cannot answer before the envoy arrives");
            BattleCampaignService.AdvanceInvitationResponses(playerWorld, playerCampaign, pendingPlayer.ResponseBattleDays);
            var acceptedPlayer = AiPlayerCampaignSupportRules.RespondToCampaignRequest(playerWorld, playerCampaign, pendingPlayer, response, true);
            Assert(acceptedPlayer && pendingPlayer.Status == BattleInvitationStatus.Accepted && playerCampaign.IsPlayerInvolved &&
                   playerCampaign.Reinforcements.Single().Teams.Single().ControllerType == CampaignControllerType.Player &&
                   playerSource.Troops == playerTroops - 100 && playerSource.Gold == playerGold - 10 && playerSource.Food == playerFood - 10 &&
                   !AiPlayerCampaignSupportRules.RespondToCampaignRequest(playerWorld, playerCampaign, pendingPlayer, response, true),
                "AI support request", "ongoing campaign accepts selected player support exactly once");
        }
        var (rejectWorld, rejectCampaign) = Setup(true);
        rejectWorld.GetFaction(1)!.IsPlayer = false; rejectWorld.GetFaction(3)!.IsPlayer = true;
        rejectCampaign.DefenderFood = 0; rejectCampaign.DefenderGold = 0;
        var rejectRequest = AiCampaignSupportPlanner.TryRequestSupport(rejectWorld, rejectCampaign, CampaignBattleSide.Defender);
        if (rejectRequest != null) BattleCampaignService.AdvanceInvitationResponses(rejectWorld, rejectCampaign, rejectRequest.ResponseBattleDays);
        Assert(rejectRequest != null && AiPlayerCampaignSupportRules.RespondToCampaignRequest(rejectWorld, rejectCampaign, rejectRequest, new PlayerCampaignSupportRequestData(), false) &&
               rejectRequest.Status == BattleInvitationStatus.Declined && rejectCampaign.Reinforcements.Count == 0,
            "AI support request", "declining an ongoing player request does not dispatch support");
        Assert(rejectRequest != null && rejectCampaign.Teams.Any(team => team.OfficerId == rejectRequest.EnvoyOfficerId &&
               team.FactionId == rejectRequest.InviterFactionId && team.ControllerType == CampaignControllerType.Ai &&
               team.Location == CampaignTeamLocation.Reserve),
            "envoy return", "player rejection returns the AI envoy under AI control");

        var (resourceWorld, resourceCampaign) = Setup(false);
        foreach (var team in resourceCampaign.Teams.Where(team => team.Side == CampaignBattleSide.Defender && team.OfficerId > 0)) team.ActiveTroops = 5000;
        resourceCampaign.DefenderFood = 0; resourceCampaign.DefenderGold = 0;
        var resourceRequest = AiCampaignSupportPlanner.TryRequestSupport(resourceWorld, resourceCampaign, CampaignBattleSide.Defender);
        ResolveScheduledAiReplies(resourceWorld, resourceCampaign);
        var resourceOrder = resourceCampaign.Reinforcements.SingleOrDefault();
        Assert(resourceRequest?.SupportType == BattleInvitationSupportType.Resources && resourceRequest.Status == BattleInvitationStatus.Accepted &&
               resourceOrder?.Teams.Count == 0 && resourceOrder.Food > 0 && resourceOrder.Gold > 0 && resourceCampaign.DefenderFood == 0 && resourceCampaign.DefenderGold == 0,
            "AI support request", "AI asks a friendly neutral faction for resources without instant delivery");

        var (mixedWorld, mixedCampaign) = Setup(true);
        mixedCampaign.DefenderFood = 0; mixedCampaign.DefenderGold = 0;
        var mixedRequest = AiCampaignSupportPlanner.TryRequestSupport(mixedWorld, mixedCampaign, CampaignBattleSide.Defender);
        ResolveScheduledAiReplies(mixedWorld, mixedCampaign);
        Assert(mixedRequest?.SupportType == BattleInvitationSupportType.FullSupport && mixedRequest.Status == BattleInvitationStatus.Accepted &&
               mixedCampaign.Reinforcements.Single().Teams.Count > 0 && mixedCampaign.Reinforcements.Single().Food > 0,
            "AI support request", "AI combines allied troop and resource requests when both are needed");
        Assert(AiCampaignSupportPlanner.TryRequestSupport(mixedWorld, mixedCampaign, CampaignBattleSide.Attacker) == null,
            "AI support request", "planner does not make decisions for the player faction");
        var (opposingWorld, opposingCampaign) = Setup(true);
        opposingCampaign.DefenderFood = 0; opposingCampaign.DefenderGold = 0;
        opposingCampaign.Reinforcements.Add(new ReinforcementOrderData { Id = 99, FactionId = 3, Side = CampaignBattleSide.Attacker, Status = ReinforcementStatus.Traveling });
        Assert(AiCampaignSupportPlanner.TryRequestSupport(opposingWorld, opposingCampaign, CampaignBattleSide.Defender) == null,
            "AI support request", "a faction already reinforcing the opposing side cannot be invited");

        var (safeWorld, safeCampaign) = Setup(true);
        safeCampaign.DefenderFood = 10000; safeCampaign.DefenderGold = 10000;
        var unsafeCity = safeWorld.GetCity(4)!; unsafeCity.Food = 1; unsafeCity.Gold = 0;
        safeWorld.Cities.Add(TestHelpers.City(6, "Safe support", 3, 1500, 4000, 4000, new[] { 302 }, new[] { 2 }));
        safeWorld.Officers.Add(TestHelpers.Officer(302, "Safe officer", 6));
        safeWorld.GetFaction(3)!.OfficerIds.Add(302);
        var safeRequest = AiCampaignSupportPlanner.TryRequestSupport(safeWorld, safeCampaign, CampaignBattleSide.Defender);
        Assert(safeRequest?.RequestedSourceCityId == 6 && safeRequest.SupportType == BattleInvitationSupportType.Troops,
            "AI support source safety", "requests troops from the viable allied city instead of a city without spare food");

        var (coldWorld, coldCampaign) = Setup(false);
        coldCampaign.DefenderFood = 0; coldCampaign.DefenderGold = 0;
        coldWorld.GetDiplomacyRelation(2, 3)!.RelationScore = 0;
        Assert(AiCampaignSupportPlanner.TryRequestSupport(coldWorld, coldCampaign, CampaignBattleSide.Defender) == null &&
               coldCampaign.Invitations.Count == 0,
            "AI support relations", "does not use an envoy to ask a neutral faction without friendly relations");

        var (capturedWorld, capturedCampaign) = Setup(true);
        capturedCampaign.DefenderFood = 10000; capturedCampaign.DefenderGold = 10000;
        var capturedRequest = AiCampaignSupportPlanner.TryRequestSupport(capturedWorld, capturedCampaign, CampaignBattleSide.Defender);
        capturedWorld.GetOfficer(301)!.CaptiveFactionId = 1;
        ResolveScheduledAiReplies(capturedWorld, capturedCampaign);
        Assert(capturedRequest?.Status == BattleInvitationStatus.Accepted && capturedCampaign.Reinforcements.Single().Teams.Count == 0 &&
               capturedWorld.GetCity(4)!.Troops == 4000,
            "AI support officer revalidation", "a newly captured allied officer is not deployed when the reply arrives");

        var (attackerWorld, attackerCampaign) = Setup(true);
        attackerWorld.GetFaction(1)!.IsPlayer = false;
        attackerWorld.GetFaction(2)!.IsPlayer = true;
        attackerWorld.DiplomacyRelations.Single().FactionAId = 1;
        attackerWorld.Officers.Add(TestHelpers.Officer(104, "Attacker envoy", 1, intelligence: 90));
        attackerWorld.GetCity(1)!.OfficerIds.Add(104);
        attackerWorld.GetFaction(1)!.OfficerIds.Add(104);
        foreach (var team in attackerCampaign.Teams.Where(team => team.OfficerId > 0))
            team.ActiveTroops = team.Side == CampaignBattleSide.Attacker ? 500 : 3000;
        attackerCampaign.AttackerGold = 0; attackerCampaign.AttackerFood = 0;
        var attackerRequest = AiCampaignSupportPlanner.TryRequestSupport(attackerWorld, attackerCampaign, CampaignBattleSide.Attacker);
        ResolveScheduledAiReplies(attackerWorld, attackerCampaign);
        Assert(attackerRequest?.Side == CampaignBattleSide.Attacker && attackerRequest.SupportType == BattleInvitationSupportType.FullSupport &&
               attackerRequest.Status == BattleInvitationStatus.Accepted,
            "AI support request", "AI attacker also asks its ally for mixed support");

        var (monthlyWorld, monthlyCampaign) = Setup(true);
        monthlyCampaign.DefenderGold = 0; monthlyCampaign.DefenderFood = 0;
        var monthlyServices = CreateServices(monthlyWorld);
        monthlyServices.Turn.AdvanceMonth();
        Assert(monthlyCampaign.Invitations.Any(invitation => invitation.InviterFactionId == 2 && invitation.Status == BattleInvitationStatus.Pending) &&
               monthlyCampaign.Reinforcements.Count == 0,
            "AI support request", "continuing campaigns evaluate requests during monthly advance");
    }

    private static void RunReinforcementBlockedRouteTest()
    {
        var world = CreateBattleCampaignWorld();
        world.Cities.Add(TestHelpers.City(4, "Ally source", 3, 1000, 1000, 1500, Array.Empty<int>(), new[] { 5 }));
        world.Cities.Add(TestHelpers.City(5, "Ally relay", 3, 1000, 1000, 1500, Array.Empty<int>(), new[] { 2 }));
        world.Factions.Add(TestHelpers.Faction(3, "Ally", false, 0, Array.Empty<int>()));
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        var other = new ActiveBattleCampaignData { Id = campaign.Id + 1, SourceCityId = 6, TargetCityId = 5, Stage = CampaignStage.SiegePreparation };
        world.ActiveBattleCampaigns.Add(other);
        var blockedLinks = BattleCampaignService.GetFriendlyRouteLinks(world, 4, 2, 3);
        var blocked = false;
        try { BattleCampaignService.DispatchReinforcement(world, campaign, 4, CampaignBattleSide.Defender, Array.Empty<AttackOfficerDeploymentData>(), 100, 0, CampaignSupplyOwnership.Gift); }
        catch (ReinforcementDispatchException) { blocked = true; }
        other.Stage = CampaignStage.Resolved;
        var openLinks = BattleCampaignService.GetFriendlyRouteLinks(world, 4, 2, 3);
        other.Stage = CampaignStage.FieldBattle; other.SourceCityId = 4; other.TargetCityId = 6;
        var busy = false;
        try { BattleCampaignService.DispatchReinforcement(world, campaign, 4, CampaignBattleSide.Defender, Array.Empty<AttackOfficerDeploymentData>(), 100, 0, CampaignSupplyOwnership.Gift); }
        catch (ReinforcementDispatchException ex) { busy = ex.Failure == ReinforcementDispatchFailure.InvalidSource; }
        Assert(blockedLinks < 0 && blocked && openLinks == 2 && busy && world.GetCity(4)!.Gold == 1000 && campaign.Reinforcements.Count == 0,
            "reinforcement blocked route", "active battle blocks transit or dispatch; resolution restores transit without reserving resources");
    }

    private static void RunCustomCampaignSupportTest()
    {
        WorldState MakeWorld()
        {
            var world = CreateBattleCampaignWorld();
            world.Cities.Add(TestHelpers.City(4, "Ally", 3, 1500, 4000, 4000, new[] { 301 }, new[] { 2 }));
            world.Officers.Add(TestHelpers.Officer(301, "AllyOfficer", 4));
            world.Factions.Add(TestHelpers.Faction(3, "Ally", false, 301, new[] { 301 }));
            world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 1, FactionBId = 3, Status = DiplomacyStatusType.Alliance, RemainingMonths = 12, RelationScore = 80 });
            world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 2, FactionBId = 3, Status = DiplomacyStatusType.Alliance, RemainingMonths = 12, RelationScore = 80 });
            return world;
        }

        var attackWorld = MakeWorld();
        attackWorld.GetDiplomacyRelation(2, 3)!.Status = DiplomacyStatusType.Neutral;
        var request = new PrewarAllianceRequestData { InviterFactionId = 1, AllyFactionId = 3, TargetCityId = 2, SourceCityId = 4, EnvoyOfficerId = 102, RequestedTroops = 600, RequestedGold = 120, RequestedFood = 300 };
        var accepted = BattleCampaignService.TrySchedulePrewarAlliance(attackWorld, request);
        CreateServices(attackWorld).Turn.AdvanceMonth();
        var attackCampaign = BattleCampaignService.CreateCampaign(attackWorld, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchPrewarAllies(attackWorld, attackCampaign);
        ResolveScheduledAiReplies(attackWorld, attackCampaign);
        var attackOrder = attackCampaign.Reinforcements.SingleOrDefault();
        Assert(accepted && attackCampaign.Invitations.Single().SupportType == BattleInvitationSupportType.FullSupport &&
               attackOrder?.SourceCityId == 4 && attackOrder.Teams.Count > 0 && attackOrder.Gold == 120 && attackOrder.Food == 300 && attackOrder.Status == ReinforcementStatus.Traveling,
            "custom prewar support", "selected allied city dispatches troops and requested resources");

        var defenseWorld = MakeWorld();
        var attack = CreateBattleCampaignAttack();
        attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData { SourceCityId = 4, IsAllianceRequest = true, RequestedGold = 120, RequestedFood = 300 });
        var defenseCampaign = BattleCampaignService.CreateCampaign(defenseWorld, attack, DefenderBattlePlan.CityDefense);
        var source = defenseWorld.GetCity(4)!;
        var goldBefore = source.Gold; var foodBefore = source.Food;
        BattleCampaignService.DispatchDefenseReinforcementRequests(defenseWorld, defenseCampaign, attack);
        ResolveScheduledAiReplies(defenseWorld, defenseCampaign);
        var order = defenseCampaign.Reinforcements.SingleOrDefault();
        Assert(defenseCampaign.Invitations.Single().SupportType == BattleInvitationSupportType.Resources && order != null && order.Teams.Count == 0 &&
               source.Gold == goldBefore - 120 && source.Food == foodBefore - 300 && order.Status == ReinforcementStatus.Traveling,
            "custom defense support", "resource-only request reserves actual gold and food without a troop team");
        var defenderGold = defenseCampaign.DefenderGold; var defenderFood = defenseCampaign.DefenderFood;
        BattleCampaignService.AdvanceCompletedBattleDay(defenseCampaign);
        Assert(defenseCampaign.DefenderGold == defenderGold && defenseCampaign.DefenderFood == defenderFood,
            "custom defense support", "traveling resources are unavailable before arrival");
        BattleCampaignService.AdvanceCompletedBattleDay(defenseCampaign);
        Assert(order?.Status == ReinforcementStatus.Deployed && defenseCampaign.DefenderGold == defenderGold + 120 && defenseCampaign.DefenderFood == defenderFood + 300,
            "custom defense support", "resource-only order delivers once at its ETA");
        BattleCampaignService.AdvanceCompletedBattleDay(defenseCampaign);
        Assert(defenseCampaign.DefenderGold == defenderGold + 120 && defenseCampaign.DefenderFood == defenderFood + 300,
            "custom defense support", "delivered resources are not duplicated");

        var neutralWorld = MakeWorld();
        var neutralRelation = neutralWorld.GetDiplomacyRelation(2, 3)!;
        neutralRelation.Status = DiplomacyStatusType.Neutral;
        neutralRelation.RelationScore = 100;
        var neutralAttack = CreateBattleCampaignAttack();
        neutralAttack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData { SourceCityId = 4, IsAllianceRequest = true, RequestedFood = 300 });
        var neutralCampaign = BattleCampaignService.CreateCampaign(neutralWorld, neutralAttack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchDefenseReinforcementRequests(neutralWorld, neutralCampaign, neutralAttack);
        ResolveScheduledAiReplies(neutralWorld, neutralCampaign);
        Assert(neutralCampaign.Invitations.Single().Status == BattleInvitationStatus.Accepted && neutralCampaign.Reinforcements.Single().Food == 300,
            "custom defense support", "friendly non-allied faction may accept a resource-only request");
    }

    private static void RunAiPostFieldDecisionTest()
    {
        var handoverWorld = CreateBattleCampaignWorld();
        var handoverSource = handoverWorld.GetCity(1)!;
        handoverWorld.GetCity(3)!.OwnerFactionId = 2;
        var governor = TestHelpers.Officer(103, "Governor", 1);
        OfficerAppointmentRules.AddAppointment(governor, OfficerAppointmentRules.Governor);
        handoverWorld.Officers.Add(governor);
        handoverWorld.GetFaction(1)!.OfficerIds.Add(103);
        handoverSource.OfficerIds.Add(103);
        var handoverCampaign = BattleCampaignService.CreateCampaign(handoverWorld, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        var handoverAgent = new WorldState.CovertOfficerData { OfficerId = 103, OriginalFactionId = 1, SponsorFactionId = 2 };
        typeof(CovertOfficerRules).GetMethod("TransferCityToSponsor", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { handoverWorld, handoverAgent, handoverSource, governor });
        var playerRuler = handoverWorld.GetOfficer(101)!;
        var staysWithArmy = handoverSource.OwnerFactionId == 2 && !handoverSource.OfficerIds.Contains(101) && playerRuler.CityId == 0 &&
            handoverWorld.GetFaction(1)!.OfficerIds.Contains(101) && handoverWorld.GetFaction(1)!.RulerOfficerId == 101 &&
            BattleCampaignService.HasFactionRulerInActiveCampaign(handoverWorld, 1);
        var handoverSaved = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(handoverWorld))!;
        var handoverSavedCampaign = handoverSaved.ActiveBattleCampaigns.Single();
        var remainsInBattle = BattleCampaignService.HasFactionRulerInActiveCampaign(handoverSaved, 1);
        handoverSavedCampaign.AttackerWonFieldBattle = true;
        BattleCampaignService.ApplyPostFieldDecision(handoverSavedCampaign, PostFieldBattleDecision.Withdraw);
        BattleCampaignService.CompleteCampaign(handoverSaved, handoverSavedCampaign, CampaignBattleSide.Defender);
        var escapesToExile = FreeOfficerMovement.GetPlayerExiledRuler(handoverSaved)?.Id == 101 &&
            !BattleCampaignService.HasFactionRulerInActiveCampaign(handoverSaved, 1);
        Assert(staysWithArmy && remainsInBattle && escapesToExile,
            "ruler campaign during city handover", $"retained={staysWithArmy}, saved={remainsInBattle}, exile={escapesToExile}");
        var attacker = new CampaignBattleTeamData { Id = 1, FactionId = 1, Side = CampaignBattleSide.Attacker, OfficerId = 101, ActiveTroops = 3000, MaximumTroops = 3000, Morale = 80, Location = CampaignTeamLocation.Field };
        var defender = new CampaignBattleTeamData { Id = 2, FactionId = 2, Side = CampaignBattleSide.Defender, OfficerId = 201, ActiveTroops = 1000, MaximumTroops = 1000, Morale = 60, Location = CampaignTeamLocation.InnerCity };
        var campaign = new ActiveBattleCampaignData { SourceCityId = 1, TargetCityId = 2, AttackerFactionId = 1, DefenderFactionId = 2, AttackerWonFieldBattle = true, AttackerFood = 1000, Teams = new() { attacker, defender } };
        var world = new WorldState();
        var source = new CityData { Id = 1, OwnerFactionId = 1 };
        world.Cities.AddRange(new[] { source, new CityData { Id = 2, OwnerFactionId = 2 } });
        world.Officers.Add(new OfficerData { Id = 101 });
        Assert(CampaignPostFieldDecisionRules.ChooseAiDecision(world, campaign) == PostFieldBattleDecision.ImmediateAssault,
            "AI post-field decision", "strong supplied attacker chooses immediate assault");
        defender.ActiveTroops = 2000;
        world.GetCity(2)!.Defense = 100;
        Assert(CampaignPostFieldDecisionRules.ChooseAiDecision(world, campaign) == PostFieldBattleDecision.PrepareNextMonthSiege,
            "AI post-field decision", "strong city defense encourages preparation");
        world.ActiveBattleCampaigns.Add(new ActiveBattleCampaignData { TargetCityId = 1, AttackerFactionId = 2, Stage = CampaignStage.FieldBattle });
        Assert(CampaignPostFieldDecisionRules.ChooseAiDecision(world, campaign) == PostFieldBattleDecision.Withdraw,
            "AI post-field decision", "threat to source encourages withdrawal when not overwhelming");
        world.ActiveBattleCampaigns.Clear();
        defender.ActiveTroops = 1000;
        campaign.BattleDaysThisMonth = BattleCampaignService.MaximumBattleDaysPerMonth;
        Assert(CampaignPostFieldDecisionRules.ChooseAiDecision(world, campaign) == PostFieldBattleDecision.PrepareNextMonthSiege,
            "AI post-field decision", "monthly limit defers assault");
        campaign.AttackerFood = 0;
        Assert(CampaignPostFieldDecisionRules.ChooseAiDecision(world, campaign) == PostFieldBattleDecision.Withdraw,
            "AI post-field decision", "no food causes withdrawal even at monthly limit");
        BattleCampaignService.ApplyPostFieldDecision(campaign, PostFieldBattleDecision.Withdraw);
        BattleCampaignService.CompleteCampaign(world, campaign, CampaignBattleSide.Defender);
        Assert(source.InfantryTroops == 3000 && source.OfficerIds.Contains(101) && world.GetOfficer(101)!.CaptiveFactionId == 0,
            "AI post-field decision", "withdrawing survivors and officer return without capture");
        foreach (var hasRefuge in new[] { true, false })
        {
            var displacedWorld = CreateBattleCampaignWorld();
            var displacedCampaign = BattleCampaignService.CreateCampaign(displacedWorld, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
            displacedCampaign.AttackerWonFieldBattle = true;
            var lostSource = displacedWorld.GetCity(1)!;
            var refuge = displacedWorld.GetCity(3)!;
            lostSource.OwnerFactionId = 2;
            if (!hasRefuge) refuge.OwnerFactionId = 2;
            var sourceGold = lostSource.Gold; var sourceFood = lostSource.Food; var sourceTroops = lostSource.Troops;
            var refugeGold = refuge.Gold; var refugeFood = refuge.Food; var refugeInfantry = refuge.InfantryTroops;
            var attackerSurvivors = displacedCampaign.Teams.Where(item => item.Side == CampaignBattleSide.Attacker).Sum(item => item.ActiveTroops + item.WoundedTroops);
            var attackerInfantry = displacedCampaign.Teams.Where(item => item.Side == CampaignBattleSide.Attacker && item.TroopType == TroopType.Infantry).Sum(item => item.ActiveTroops + item.WoundedTroops);
            var carriedGold = displacedCampaign.AttackerGold; var carriedFood = displacedCampaign.AttackerFood;
            BattleCampaignService.ApplyPostFieldDecision(displacedCampaign, PostFieldBattleDecision.Withdraw);
            BattleCampaignService.CompleteCampaign(displacedWorld, displacedCampaign, CampaignBattleSide.Defender);
            var displacedReport = displacedWorld.BattleReports.Single();
            var officer = displacedWorld.GetOfficer(101)!;
            var correctReturn = hasRefuge
                ? refuge.InfantryTroops == refugeInfantry + attackerInfantry && refuge.Gold == refugeGold + carriedGold && refuge.Food == refugeFood + carriedFood && officer.CityId == refuge.Id && refuge.OfficerIds.Contains(101)
                : FreeOfficerMovement.IsFreeOfficer(displacedWorld, officer) && displacedReport.AttackerDisbandedTroops == attackerSurvivors && displacedReport.AttackerActiveTroops == 0;
            Assert(correctReturn && lostSource.Gold == sourceGold && lostSource.Food == sourceFood && lostSource.Troops == sourceTroops,
                "withdrawal lost source", $"refuge={hasRefuge}, correct={correctReturn}, disbanded={displacedReport.AttackerDisbandedTroops}, source={lostSource.Gold}/{lostSource.Food}/{lostSource.Troops}");
        }
    }

    private static void RunAlliedReorganizationTest()
    {
        var campaign = new ActiveBattleCampaignData { Stage = CampaignStage.FieldBattle, AttackerFactionId = 1, DefenderFactionId = 2 };
        var large = new CampaignBattleTeamData { Id = 1, FactionId = 3, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Ai, OfficerId = 301, OriginCityId = 3, ActiveTroops = 70, WoundedTroops = 5, MaximumTroops = 100, Morale = 90 };
        var small = new CampaignBattleTeamData { Id = 1, FactionId = 3, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Ai, OfficerId = 302, OriginCityId = 4, ReinforcementOrderId = 9, ActiveTroops = 20, WoundedTroops = 5, MaximumTroops = 100, Morale = 40, Location = CampaignTeamLocation.Reserve };
        var enemy = new CampaignBattleTeamData { Id = 2, FactionId = 2, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Ai, OfficerId = 201, ActiveTroops = 10, MaximumTroops = 100 };
        var player = new CampaignBattleTeamData { Id = 3, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, OfficerId = 101, ActiveTroops = 10, MaximumTroops = 100 };
        var order = new ReinforcementOrderData { Id = 9, Side = CampaignBattleSide.Attacker, FactionId = 3, Status = ReinforcementStatus.Reserve, Gold = 20, Food = 30, Teams = new() { small } };
        campaign.Teams.AddRange(new[] { large, enemy, player });
        campaign.Reinforcements.Add(order);
        CampaignReorganizationRules.ReorganizeAllies(campaign, 1, CampaignBattleSide.Attacker);
        Assert(large.ActiveTroops == 90 && large.WoundedTroops == 5 && small.ActiveTroops == 0 && small.WoundedTroops == 5 &&
               large.TroopOrigins.Single(item => item.CityId == 4).ActiveTroops == 20 && order.Status == ReinforcementStatus.Reserve && campaign.AttackerGold == 0 &&
               enemy.ActiveTroops == 10 && player.ActiveTroops == 10,
            "allied reorganization", "same-faction active troops consolidate; wounded, enemy and pending supply remain");
        CampaignReorganizationRules.ReorganizeAllies(campaign, 1, CampaignBattleSide.Attacker);
        Assert(large.ActiveTroops == 90 && small.ActiveTroops == 0 && campaign.Teams.Count == 3,
            "allied reorganization", "repeating does not create troops or teams");

        var defenderLarge = new CampaignBattleTeamData { Id = 4, FactionId = 2, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Ai, OfficerId = 202, OriginCityId = 5, ActiveTroops = 50, MaximumTroops = 100, Location = CampaignTeamLocation.InnerCity };
        enemy.Location = CampaignTeamLocation.InnerCity;
        campaign.Teams.Add(defenderLarge);
        CampaignReorganizationRules.ReorganizeAiTeams(campaign, 1);
        Assert(defenderLarge.ActiveTroops == 60 && enemy.ActiveTroops == 0 && enemy.Location == CampaignTeamLocation.Reserve &&
               player.ActiveTroops == 10 && large.ActiveTroops == 90,
            "ai reorganization", "both sides consolidate separately while player troops remain untouched");
    }
    private static void RunArrivedReserveReorganizationTest()
    {
        var placementCampaign = new ActiveBattleCampaignData();
        var placed = new CampaignBattleTeamData { Id = 71, ReinforcementOrderId = 70, Location = CampaignTeamLocation.Field };
        var unplaced = new CampaignBattleTeamData { Id = 72, ReinforcementOrderId = 70, Location = CampaignTeamLocation.Field };
        var pending = new CampaignBattleTeamData { Id = 73, ReinforcementOrderId = 70, Location = CampaignTeamLocation.Reserve };
        placementCampaign.Teams.AddRange(new[] { placed, unplaced, pending });
        placementCampaign.Reinforcements.Add(new ReinforcementOrderData { Id = 70, Status = ReinforcementStatus.Reserve, Teams = new() { placed, unplaced, pending } });
        BattleCampaignService.CompleteReinforcementPlacement(placementCampaign);
        var completionDidNotPlace = !placed.HasEnteredBattle && !unplaced.HasEnteredBattle && !pending.HasEnteredBattle;
        BattleCampaignService.MarkReinforcementTeamPlaced(placementCampaign, placed);
        BattleCampaignService.MarkReinforcementTeamPlaced(placementCampaign, pending);
        var persistedPlacement = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(placementCampaign))!;
        Assert(completionDidNotPlace && placed.HasEnteredBattle && !unplaced.HasEnteredBattle && !pending.HasEnteredBattle &&
               persistedPlacement.Teams.Single(team => team.Id == 71).HasEnteredBattle && !persistedPlacement.Teams.Single(team => team.Id == 72).HasEnteredBattle,
            "reinforcement actual placement", "only a successfully placed field team is marked as entered and the state survives saving");
        var campaign = new ActiveBattleCampaignData { Stage = CampaignStage.FieldBattle, AttackerGold = 10, AttackerFood = 20 };
        var field = new CampaignBattleTeamData { Id = 1, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, OfficerId = 101, OriginCityId = 1, ActiveTroops = 50, MaximumTroops = 100 };
        var reserve = new CampaignBattleTeamData { Id = 1, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, OfficerId = 102, OriginCityId = 2, ReinforcementOrderId = 10, ActiveTroops = 40, WoundedTroops = 10, MaximumTroops = 100, Location = CampaignTeamLocation.Reserve };
        var traveling = new CampaignBattleTeamData { Id = 1, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, OfficerId = 103, OriginCityId = 3, ReinforcementOrderId = 11, ActiveTroops = 30, MaximumTroops = 100, Location = CampaignTeamLocation.Traveling };
        var arrivedOrder = new ReinforcementOrderData { Id = 10, Side = CampaignBattleSide.Attacker, FactionId = 1, SourceCityId = 2, Status = ReinforcementStatus.Reserve, Gold = 7, Food = 9, Teams = new() { reserve } };
        var travelingOrder = new ReinforcementOrderData { Id = 11, Side = CampaignBattleSide.Attacker, FactionId = 1, Status = ReinforcementStatus.Traveling, Teams = new() { traveling } };
        campaign.Teams.Add(field); campaign.Reinforcements.AddRange(new[] { arrivedOrder, travelingOrder });
        var eligible = CampaignReorganizationRules.GetEligibleTeams(campaign, 1);
        Assert(eligible.Count == 2 && eligible.Contains(field) && eligible.Contains(reserve) && !eligible.Contains(traveling),
            "arrived reserve reorganization", "includes arrived reserve despite duplicate ids; excludes traveling");
        Assert(CampaignReorganizationRules.TryTransfer(campaign, 1, reserve, field, 15, 5, out _) && field.ActiveTroops == 65 && field.WoundedTroops == 5 && reserve.ActiveTroops == 25 && reserve.WoundedTroops == 5 &&
               arrivedOrder.Status == ReinforcementStatus.Reserve && campaign.AttackerGold == 10 && campaign.AttackerFood == 20 && campaign.Teams.Count == 1,
            "arrived reserve reorganization", "transfer changes existing teams without deploying or crediting supply");
        Assert(!CampaignReorganizationRules.TryTransfer(campaign, 1, traveling, field, 1, 0, out _),
            "arrived reserve reorganization", "traveling team cannot transfer");
        var withdrawal = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(campaign))!;
        withdrawal.SourceCityId = 1; withdrawal.TargetCityId = 3; withdrawal.AttackerFactionId = 1; withdrawal.DefenderFactionId = 2;
        withdrawal.Participants.Add(new BattleParticipantData { FactionId = 1, Side = CampaignBattleSide.Attacker, TroopsCommitted = 200 });
        var withdrawalWorld = new WorldState();
        var firstCity = new CityData { Id = 1, OwnerFactionId = 1 };
        var reserveCity = new CityData { Id = 2, OwnerFactionId = 1 };
        withdrawalWorld.Cities.AddRange(new[] { firstCity, reserveCity, new CityData { Id = 3, OwnerFactionId = 2 } });
        // This test concerns the arrived order; keep the unrelated traveling order out of settlement.
        withdrawal.Reinforcements.RemoveAll(order => order.Id == 11);
        BattleCampaignService.CompleteCampaign(withdrawalWorld, withdrawal, CampaignBattleSide.Defender);
        Assert(firstCity.InfantryTroops == 50 && reserveCity.InfantryTroops == 50 && reserveCity.Gold == 7 && reserveCity.Food == 9 &&
               withdrawal.Reinforcements.Single().Status == ReinforcementStatus.Returned,
            "arrived reserve reorganization", $"withdrawal city1={firstCity.InfantryTroops}, city2={reserveCity.InfantryTroops}, gold={reserveCity.Gold}, food={reserveCity.Food}, status={withdrawal.Reinforcements.Single().Status}");
        var report = withdrawalWorld.BattleReports.Single();
        var contribution = report.ParticipantContributions.Single();
        Assert(report.AttackerCommittedTroops == 200 && report.AttackerActiveTroops == 65 && report.AttackerWoundedTroops == 5 &&
               report.AttackerReturnedTroops == 30 && report.AttackerLostTroops == 100 && contribution.TroopsCommitted == 200 &&
               contribution.TroopsReturned == 30 && contribution.TroopsLost == 100,
            "arrived reserve reorganization", "report counts undeployed survivors as returned without false losses");
        BattleCampaignService.PromoteReserves(campaign, CampaignBattleSide.Attacker);
        Assert(arrivedOrder.Status == ReinforcementStatus.Deployed && campaign.Teams.Contains(reserve) && campaign.AttackerGold == 17 && campaign.AttackerFood == 29,
            "arrived reserve reorganization", "normal promotion deploys and credits supply once");
        var loaded = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(campaign))!;
        var loadedOrder = loaded.Reinforcements.Single(item => item.Id == 10);
        var loadedTeam = loaded.Teams.Single(item => item.ReinforcementOrderId == 10);
        loadedTeam.Location = CampaignTeamLocation.Reserve;
        CampaignReorganizationRules.SynchronizeReinforcementTeamReferences(loaded);
        BattleCampaignService.PromoteReserves(loaded, CampaignBattleSide.Attacker);
        Assert(ReferenceEquals(loadedOrder.Teams[0], loadedTeam) && loadedTeam.Location == CampaignTeamLocation.Field && loaded.Teams.Count == 2 && loaded.AttackerGold == 17 && loaded.AttackerFood == 29,
            "arrived reserve reorganization", "loaded deployed team reconnects without duplicate team or supply");

        var supplyCampaign = new ActiveBattleCampaignData { Stage = CampaignStage.FieldBattle, SourceCityId = 1, TargetCityId = 3, AttackerFactionId = 1, DefenderFactionId = 2, AttackerGold = 5, AttackerFood = 6 };
        var supplyTeam = new CampaignBattleTeamData { Id = 1, FactionId = 1, Side = CampaignBattleSide.Attacker, OfficerId = 105, ActiveTroops = 10, MaximumTroops = 10, ReinforcementOrderId = 15, OriginCityId = 2, Location = CampaignTeamLocation.Reserve };
        var supplyOrder = new ReinforcementOrderData { Id = 15, FactionId = 1, Side = CampaignBattleSide.Attacker, SourceCityId = 2, Status = ReinforcementStatus.Reserve, Gold = 7, Food = 9, Teams = new() { supplyTeam } };
        supplyCampaign.Reinforcements.Add(supplyOrder);
        BattleCampaignService.PromoteReserves(supplyCampaign, CampaignBattleSide.Attacker);
        BattleCampaignService.MarkCampaignTeamSpawnBlocked(supplyCampaign, supplyTeam);
        Assert(supplyOrder.SuppliesDelivered == false && supplyCampaign.AttackerGold == 5 && supplyCampaign.AttackerFood == 6 && supplyCampaign.AttackerGoldInvested == 0 && supplyCampaign.AttackerFoodInvested == 0,
            "reinforcement supplies", "first blocked placement rolls back pending delivery and investment");
        BattleCampaignService.PromoteReserves(supplyCampaign, CampaignBattleSide.Attacker);
        BattleCampaignService.MarkReinforcementTeamPlaced(supplyCampaign, supplyTeam);
        BattleCampaignService.CompleteReinforcementPlacement(supplyCampaign);
        supplyCampaign.AttackerGold = 1; supplyCampaign.AttackerFood = 2;
        var savedSupply = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(supplyCampaign))!;
        CampaignReorganizationRules.SynchronizeReinforcementTeamReferences(savedSupply);
        var savedOrder = savedSupply.Reinforcements.Single();
        var savedTeam = savedSupply.Teams.Single();
        BattleCampaignService.MarkCampaignTeamSpawnBlocked(savedSupply, savedTeam);
        BattleCampaignService.PromoteReserves(savedSupply, CampaignBattleSide.Attacker);
        Assert(savedOrder.SuppliesDelivered == true && savedSupply.AttackerGold == 1 && savedSupply.AttackerFood == 2 && savedSupply.AttackerGoldInvested == 7 && savedSupply.AttackerFoodInvested == 9,
            "reinforcement supplies", "previously delivered and spent supplies do not reappear after loading and redeployment");
        BattleCampaignService.MarkCampaignTeamSpawnBlocked(savedSupply, savedTeam);
        var supplyWorld = new WorldState();
        supplyWorld.Cities.AddRange(new[] { new CityData { Id = 1, OwnerFactionId = 1 }, new CityData { Id = 2, OwnerFactionId = 1 }, new CityData { Id = 3, OwnerFactionId = 2 } });
        BattleCampaignService.CompleteCampaign(supplyWorld, savedSupply, CampaignBattleSide.Defender);
        Assert(supplyWorld.GetCity(2)!.Gold == 0 && supplyWorld.GetCity(2)!.Food == 0 && supplyWorld.GetCity(1)!.Gold == 1 && supplyWorld.GetCity(1)!.Food == 2,
            "reinforcement supplies", "settlement does not return supplies already delivered");

        var waiting = new ActiveBattleCampaignData { Stage = CampaignStage.FieldBattle, AttackerFood = 1 };
        var waitingTeam = new CampaignBattleTeamData { Id = 1, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, OfficerId = 104, OriginCityId = 2, ReinforcementOrderId = 12, ActiveTroops = 0, WoundedTroops = 10, MaximumTroops = 20, Location = CampaignTeamLocation.Reserve };
        var waitingOrder = new ReinforcementOrderData { Id = 12, Side = CampaignBattleSide.Attacker, FactionId = 1, Status = ReinforcementStatus.Reserve, Gold = 7, Food = 9, Teams = new() { waitingTeam } };
        waiting.Reinforcements.Add(waitingOrder);
        BattleCampaignService.PromoteReserves(waiting, CampaignBattleSide.Attacker);
        Assert(waitingOrder.Status == ReinforcementStatus.Reserve && waiting.AttackerGold == 0 && waiting.AttackerFood == 1 && waiting.Teams.Count == 0,
            "arrived reserve reorganization", "zero active reserve keeps order and supply pending");
        BattleCampaignService.BeginNextCampaignMonth(waiting, 200, 2);
        Assert(waitingTeam.ActiveTroops == 10 && waitingTeam.WoundedTroops == 0 && waiting.AttackerFood == 0 && waitingOrder.Status == ReinforcementStatus.Reserve && waiting.Teams.Count == 0,
            "arrived reserve reorganization", "undeployed reserve recovers with side food without premature deployment");
        BattleCampaignService.PromoteReserves(waiting, CampaignBattleSide.Attacker);
        Assert(waitingTeam.ActiveTroops == 10 && waitingTeam.Location == CampaignTeamLocation.Field && waiting.Teams.Contains(waitingTeam) &&
               waitingOrder.Status == ReinforcementStatus.Deployed && waiting.AttackerGold == 7 && waiting.AttackerFood == 9,
            "arrived reserve reorganization", "recovered reserve can enter once after the next month starts");
    }
    private static void RunPlayerReorganizationTransferTest()
    {
        var legacyWorld = new WorldState();
        legacyWorld.Cities.AddRange(new[] { new CityData { Id = 1, OwnerFactionId = 1 }, new CityData { Id = 2, OwnerFactionId = 1 }, new CityData { Id = 3, OwnerFactionId = 2 } });
        var legacyCampaign = new ActiveBattleCampaignData { SourceCityId = 1, TargetCityId = 3, AttackerFactionId = 1, DefenderFactionId = 2, Stage = CampaignStage.FieldBattle };
        var legacyMain = new CampaignBattleTeamData { Id = 1, OfficerId = 11, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, ActiveTroops = 40, WoundedTroops = 10, MaximumTroops = 50, ReorganizationCapacity = 100 };
        var legacyReinforcement = new CampaignBattleTeamData { Id = 2, OfficerId = 12, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, ActiveTroops = 10, MaximumTroops = 10, ReorganizationCapacity = 100, ReinforcementOrderId = 9 };
        legacyCampaign.Teams.AddRange(new[] { legacyMain, legacyReinforcement });
        legacyCampaign.Reinforcements.Add(new ReinforcementOrderData { Id = 9, FactionId = 1, Side = CampaignBattleSide.Attacker, SourceCityId = 2, Status = ReinforcementStatus.Deployed, Teams = new() { legacyReinforcement } });
        var legacyTransfer = CampaignReorganizationRules.TryTransfer(legacyCampaign, 1, legacyMain, legacyReinforcement, 20, 5, out _);
        Assert(legacyTransfer && legacyMain.OriginCityId == 1 && legacyReinforcement.OriginCityId == 2 &&
               legacyReinforcement.TroopOrigins.Single(item => item.CityId == 1).ActiveTroops == 20 &&
               legacyReinforcement.TroopOrigins.Single(item => item.CityId == 1).WoundedTroops == 5,
            "legacy origin reorganization", "missing origins use campaign and reinforcement source before transfer");
        var legacyLoaded = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(legacyCampaign))!;
        BattleCampaignService.CompleteCampaign(legacyWorld, legacyLoaded, CampaignBattleSide.Defender);
        Assert(legacyWorld.GetCity(1)!.InfantryTroops == 50 && legacyWorld.GetCity(2)!.InfantryTroops == 10,
            "legacy origin reorganization", "mixed active and wounded return to original cities after save and withdrawal");

        var fallbackWorld = new WorldState();
        var fallbackCity = new CityData { Id = 2, OwnerFactionId = 2 };
        fallbackWorld.Cities.AddRange(new[] { new CityData { Id = 1, OwnerFactionId = 1 }, fallbackCity });
        var fallenBack = new CampaignBattleTeamData { Id = 51, FactionId = 2, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Player,
            OfficerId = 51, ActiveTroops = 60, WoundedTroops = 20, MaximumTroops = 100, Location = CampaignTeamLocation.InnerCity, RetreatDestinationCityId = 2 };
        var fallbackCampaign = new ActiveBattleCampaignData { SourceCityId = 1, TargetCityId = 2, AttackerFactionId = 1, DefenderFactionId = 2,
            Stage = CampaignStage.FieldBattle, AttackerWonFieldBattle = true, Teams = new() { fallenBack } };
        Assert(CampaignReorganizationRules.GetEligibleTeams(fallbackCampaign, 2).Contains(fallenBack), "player reorganization", "defender who fell back inside remains eligible");
        BattleCampaignService.ApplyPostFieldDecision(fallbackCampaign, PostFieldBattleDecision.ImmediateAssault);
        Assert(fallenBack.Location == CampaignTeamLocation.Field && fallenBack.RetreatDestinationCityId == 0,
            "player reorganization", "fall-back marker cleared on entering siege");
        BattleCampaignService.CompleteCampaign(fallbackWorld, fallbackCampaign, CampaignBattleSide.Defender);
        Assert(fallbackCity.InfantryTroops == 80 && fallenBack.ReturnedTroops == 80,
            "player reorganization", "inner-city fallback force returned once after siege");
        var withdrawalCity = new CityData { Id = 2, OwnerFactionId = 2, InfantryTroops = 30 };
        var withdrawalWorld = new WorldState();
        withdrawalWorld.Cities.AddRange(new[] { new CityData { Id = 1, OwnerFactionId = 1 }, withdrawalCity });
        var withdrawalDefender = new CampaignBattleTeamData { Id = 61, FactionId = 2, Side = CampaignBattleSide.Defender, OfficerId = 61, ActiveTroops = 60, WoundedTroops = 20, MaximumTroops = 100, Location = CampaignTeamLocation.InnerCity, RetreatDestinationCityId = 2 };
        var alreadyReturned = new CampaignBattleTeamData { Id = 62, FactionId = 2, Side = CampaignBattleSide.Defender, OfficerId = 62, ReturnedTroops = 30, MaximumTroops = 30, Location = CampaignTeamLocation.NeighborCity, RetreatDestinationCityId = 2 };
        var withdrawalCampaign = new ActiveBattleCampaignData { SourceCityId = 1, TargetCityId = 2, AttackerFactionId = 1, DefenderFactionId = 2, Stage = CampaignStage.FieldBattle, AttackerWonFieldBattle = true, Teams = new() { withdrawalDefender, alreadyReturned } };
        BattleCampaignService.ApplyPostFieldDecision(withdrawalCampaign, PostFieldBattleDecision.Withdraw);
        BattleCampaignService.CompleteCampaign(withdrawalWorld, withdrawalCampaign, CampaignBattleSide.Defender);
        Assert(withdrawalCity.InfantryTroops == 110 && withdrawalDefender.ReturnedTroops == 80 && alreadyReturned.ReturnedTroops == 30,
            "player reorganization", "attacker withdrawal settles inner-city fallback without duplicating strategic returns");
        var reserveFallback = new CampaignBattleTeamData { Id = 52, FactionId = 2, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Player,
            OfficerId = 52, ActiveTroops = 20, MaximumTroops = 20, ReorganizationCapacity = 100, Location = CampaignTeamLocation.InnerCity, RetreatDestinationCityId = 2 };
        var reserveReceiver = new CampaignBattleTeamData { Id = 53, FactionId = 2, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Player,
            OfficerId = 53, ActiveTroops = 20, MaximumTroops = 20, ReorganizationCapacity = 100, Location = CampaignTeamLocation.InnerCity };
        var reserveFallbackCampaign = new ActiveBattleCampaignData { TargetCityId = 2, Teams = new() { reserveFallback, reserveReceiver } };
        Assert(CampaignReorganizationRules.TryTransfer(reserveFallbackCampaign, 2, reserveFallback, reserveReceiver, 20, 0, out _) &&
               reserveFallback.Location == CampaignTeamLocation.Reserve && reserveFallback.RetreatDestinationCityId == 0 &&
               CampaignReorganizationRules.GetEligibleTeams(reserveFallbackCampaign, 2).Contains(reserveFallback),
            "player reorganization", "emptied fallback reserve can be refilled");
        var capacityOfficer = new OfficerData { Id = 91, Leadership = 80 };
        Assert(CampaignReorganizationRules.GetOfficerCapacity(capacityOfficer) == 4200, "reorganization capacity", "leadership base");
        OfficerAppointmentRules.AddAppointment(capacityOfficer, OfficerAppointmentRules.VanguardGeneral);
        Assert(CampaignReorganizationRules.GetOfficerCapacity(capacityOfficer) == 4700, "reorganization capacity", "vanguard duty bonus");
        OfficerAppointmentRules.AddAppointment(capacityOfficer, OfficerAppointmentRules.GrandGeneral);
        Assert(CampaignReorganizationRules.GetOfficerCapacity(capacityOfficer) == 5700, "reorganization capacity", "highest military duty bonus does not stack");
        var capacityWorld = new WorldState(); capacityWorld.Officers.Add(capacityOfficer);
        var capacityCampaign = new ActiveBattleCampaignData();
        var oldTeam = new CampaignBattleTeamData { Id = 91, OfficerId = 91, FactionId = 1, ControllerType = CampaignControllerType.Player, ActiveTroops = 6000, MaximumTroops = 6000 };
        var receiving = new CampaignBattleTeamData { Id = 92, OfficerId = 92, FactionId = 1, ControllerType = CampaignControllerType.Player, ActiveTroops = 500, MaximumTroops = 500, ReorganizationCapacity = 3000 };
        capacityCampaign.Teams.AddRange(new[] { oldTeam, receiving });
        CampaignReorganizationRules.InitializeCapacities(capacityWorld, capacityCampaign);
        Assert(oldTeam.ReorganizationCapacity == 5700 && oldTeam.ActiveTroops == 6000 && !CampaignReorganizationRules.TryTransfer(capacityCampaign, 1, receiving, oldTeam, 1, 0, out _),
            "reorganization capacity", "legacy over-cap team preserved but cannot receive");
        Assert(CampaignReorganizationRules.TryTransfer(capacityCampaign, 1, oldTeam, receiving, 400, 0, out _) && oldTeam.MaximumTroops + receiving.MaximumTroops == 6500,
            "reorganization capacity", "transfer preserves committed troop totals for reports");
        var recoveryCampaign = new ActiveBattleCampaignData { Stage = CampaignStage.SiegePreparation, AttackerFood = 10, DefenderFood = 10 };
        var cityDefender = new CampaignBattleTeamData { Id = 21, Side = CampaignBattleSide.Defender, Location = CampaignTeamLocation.InnerCity, ActiveTroops = 50, WoundedTroops = 40, MaximumTroops = 100 };
        var outsideDefender = new CampaignBattleTeamData { Id = 22, Side = CampaignBattleSide.Defender, Location = CampaignTeamLocation.Field, ActiveTroops = 50, WoundedTroops = 40, MaximumTroops = 100 };
        var campAttacker = new CampaignBattleTeamData { Id = 23, Side = CampaignBattleSide.Attacker, Location = CampaignTeamLocation.Field, ActiveTroops = 50, WoundedTroops = 40, MaximumTroops = 100 };
        var reserveDefender = new CampaignBattleTeamData { Id = 24, Side = CampaignBattleSide.Defender, Location = CampaignTeamLocation.Reserve, ActiveTroops = 50, WoundedTroops = 40, MaximumTroops = 100 };
        var reserveAttacker = new CampaignBattleTeamData { Id = 25, Side = CampaignBattleSide.Attacker, Location = CampaignTeamLocation.Reserve, ActiveTroops = 50, WoundedTroops = 40, MaximumTroops = 100 };
        recoveryCampaign.Teams.AddRange(new[] { cityDefender, outsideDefender, campAttacker, reserveDefender, reserveAttacker });
        BattleCampaignService.BeginNextCampaignMonth(recoveryCampaign, 200, 2);
        Assert(cityDefender.WoundedTroops == 0 && cityDefender.ActiveTroops == 90 && outsideDefender.WoundedTroops == 20 &&
               campAttacker.WoundedTroops == 20 && reserveDefender.WoundedTroops == 0 && reserveAttacker.WoundedTroops == 0 && recoveryCampaign.DefenderFood == 5 && recoveryCampaign.AttackerFood == 7,
            "player reorganization", "field recovers half, both reserves and defender inner city recover fully");
        var fieldCampaign = new ActiveBattleCampaignData { Stage = CampaignStage.FieldBattle, AttackerFood = 1, DefenderFood = 10 };
        var fieldDefender = new CampaignBattleTeamData { Side = CampaignBattleSide.Defender, Location = CampaignTeamLocation.Field, ActiveTroops = 95, WoundedTroops = 40, MaximumTroops = 100 };
        var fieldAttacker = new CampaignBattleTeamData { Side = CampaignBattleSide.Attacker, Location = CampaignTeamLocation.Field, ActiveTroops = 0, WoundedTroops = 100, MaximumTroops = 100 };
        fieldCampaign.Teams.AddRange(new[] { fieldDefender, fieldAttacker });
        BattleCampaignService.BeginNextCampaignMonth(fieldCampaign, 200, 2);
        Assert(fieldDefender.ActiveTroops == 100 && fieldDefender.WoundedTroops == 35 && fieldCampaign.DefenderFood == 9 &&
               fieldAttacker.ActiveTroops == 20 && fieldAttacker.WoundedTroops == 80 && fieldCampaign.AttackerFood == 0,
            "player reorganization", "field recovery respects capacity, food and separate pools");
        var reserveCampaign = new ActiveBattleCampaignData { Stage = CampaignStage.FieldBattle, AttackerFood = 2, DefenderFood = 1 };
        var attackReserve = new CampaignBattleTeamData { Side = CampaignBattleSide.Attacker, Location = CampaignTeamLocation.Reserve, ActiveTroops = 0, WoundedTroops = 40, MaximumTroops = 40 };
        var defenseReserve = new CampaignBattleTeamData { Side = CampaignBattleSide.Defender, Location = CampaignTeamLocation.Reserve, ActiveTroops = 0, WoundedTroops = 40, MaximumTroops = 40 };
        reserveCampaign.Teams.AddRange(new[] { attackReserve, defenseReserve });
        BattleCampaignService.BeginNextCampaignMonth(reserveCampaign, 200, 2);
        Assert(attackReserve.ActiveTroops == 40 && attackReserve.WoundedTroops == 0 && reserveCampaign.AttackerFood == 0 &&
               defenseReserve.ActiveTroops == 20 && defenseReserve.WoundedTroops == 20 && reserveCampaign.DefenderFood == 0,
            "player reorganization", "field-month reserves recover fully within each side's food limit");
        var campaign = new ActiveBattleCampaignData();
        var a = new CampaignBattleTeamData { Id = 1, FactionId = 1, ControllerType = CampaignControllerType.Player, OfficerId = 11, OriginCityId = 2, TroopType = TroopType.Infantry, ActiveTroops = 80, WoundedTroops = 20, MaximumTroops = 100, Morale = 40, MessTurns = 2, Energy = 3, WeaponAmmo = 4, MaxWeaponAmmo = 10 };
        var b = new CampaignBattleTeamData { Id = 2, FactionId = 1, ControllerType = CampaignControllerType.Player, OfficerId = 12, OriginCityId = 2, TroopType = TroopType.Infantry, ActiveTroops = 20, WoundedTroops = 0, MaximumTroops = 100, Morale = 100, MessTurns = 0, Energy = 8, WeaponAmmo = 9, MaxWeaponAmmo = 10 };
        campaign.Teams.AddRange(new[] { a, b });
        Assert(CampaignReorganizationRules.TryTransfer(campaign, 1, 1, 2, 20, 10, out _) && a.ActiveTroops == 60 && a.WoundedTroops == 10 && b.ActiveTroops == 40 && b.WoundedTroops == 10 && b.Morale == 64,
            "player reorganization", "active and wounded conserved; morale weighted");
        Assert(b.MessTurns == 0 && b.Energy == null && b.WeaponAmmo == null && b.MaxWeaponAmmo == null && a.MessTurns == 0 && a.Energy == null && a.WeaponAmmo == null && b.WoundedOriginCityId == 2,
            "player reorganization", "both teams reset combat status to deployment defaults");
        var restored = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(campaign));
        var restoredTeam = restored!.Teams.Single(team => team.Id == 2);
        Assert(restoredTeam.MessTurns == 0 && restoredTeam.Energy == null && restoredTeam.WeaponAmmo == null && restoredTeam.MaxWeaponAmmo == null && restoredTeam.WoundedOriginCityId == 2,
            "player reorganization", "default status and wounded origin survive serialization");
        var before = b.ActiveTroops;
        Assert(!CampaignReorganizationRules.TryTransfer(campaign, 1, 1, 2, 60, 0, out _) && b.ActiveTroops == before,
            "player reorganization", "capacity exceeded without mutation");
        b.TroopType = TroopType.Archer;
        Assert(!CampaignReorganizationRules.TryTransfer(campaign, 1, 1, 2, 1, 0, out _) && a.ActiveTroops == 60,
            "player reorganization", "different troop types rejected");
        b.TroopType = TroopType.Infantry;
        Assert(!CampaignReorganizationRules.TryTransfer(campaign, 1, 1, 2, -1, 1, out _) && a.WoundedTroops == 10,
            "player reorganization", "negative transfer rejected");
        b.FactionId = 2;
        Assert(!CampaignReorganizationRules.TryTransfer(campaign, 1, 1, 2, 1, 0, out _), "player reorganization", "foreign teams excluded");
        b.FactionId = 1;
        b.ActiveTroops = 0; b.WoundedTroops = 0;
        Assert(CampaignReorganizationRules.TryTransfer(campaign, 1, 1, 2, 60, 10, out _) && a.Location == CampaignTeamLocation.Reserve,
            "player reorganization", "empty source moves to reserve");

        var mixedCampaign = new ActiveBattleCampaignData();
        var first = new CampaignBattleTeamData { Id = 41, FactionId = 1, ControllerType = CampaignControllerType.Player, OfficerId = 41, OriginCityId = 1, ActiveTroops = 60, WoundedTroops = 20, MaximumTroops = 100 };
        var second = new CampaignBattleTeamData { Id = 42, FactionId = 1, ControllerType = CampaignControllerType.Player, OfficerId = 42, OriginCityId = 2, ReinforcementOrderId = 5, ActiveTroops = 20, WoundedTroops = 10, MaximumTroops = 100 };
        mixedCampaign.Teams.AddRange(new[] { first, second });
        var mixed = CampaignReorganizationRules.TryTransfer(mixedCampaign, 1, 41, 42, 20, 10, out _);
        Assert(mixed && second.ActiveTroops == 40 && second.WoundedTroops == 20 && second.TroopOrigins.Single(x => x.CityId == 1).ActiveTroops == 20 &&
               second.TroopOrigins.Single(x => x.CityId == 1).WoundedTroops == 10 && second.TroopOrigins.Single(x => x.CityId == 2).WoundedTroops == 10,
            "player reorganization", "cross-origin transfer retains active and wounded origins");
        var preview = CampaignTroopOriginRules.PreviewTransfer(second, 25, 15);
        var previewCopy = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(mixedCampaign))!;
        var previewSource = previewCopy.Teams.Single(team => team.Id == 42);
        var previewTarget = previewCopy.Teams.Single(team => team.Id == 41);
        var beforeOrigins = previewSource.TroopOrigins.ToDictionary(item => item.CityId, item => (item.ActiveTroops, item.WoundedTroops));
        var previewTransferred = CampaignReorganizationRules.TryTransfer(previewCopy, 1, previewSource, previewTarget, 25, 15, out _);
        Assert(previewTransferred && preview.Count == 2 && preview[0].CityId == 1 && preview[0].ActiveTroops == 20 && preview[0].WoundedTroops == 10 &&
               preview[1].CityId == 2 && preview[1].ActiveTroops == 5 && preview[1].WoundedTroops == 5 &&
               preview.All(item => beforeOrigins[item.CityId].ActiveTroops - (previewSource.TroopOrigins.FirstOrDefault(origin => origin.CityId == item.CityId)?.ActiveTroops ?? 0) == item.ActiveTroops &&
                                   beforeOrigins[item.CityId].WoundedTroops - (previewSource.TroopOrigins.FirstOrDefault(origin => origin.CityId == item.CityId)?.WoundedTroops ?? 0) == item.WoundedTroops) &&
               second.ActiveTroops == 40 && second.WoundedTroops == 20,
            "player reorganization", "mixed-origin transfer preview matches actual active and wounded allocation without changing the source");
        CampaignTroopOriginRules.ApplyCounts(second, 30, 25);
        Assert(second.TroopOrigins.Sum(x => x.ActiveTroops) == 30 && second.TroopOrigins.Sum(x => x.WoundedTroops) == 25 &&
               second.TroopOrigins.Single(x => x.CityId == 1).WoundedTroops == 15,
            "player reorganization", "mixed-origin casualties and wounds remain conserved");
        CampaignTroopOriginRules.ApplyCounts(second, 35, 20);
        var mixedRestored = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(mixedCampaign))!;
        Assert(mixedRestored.Teams.Single(x => x.Id == 42).TroopOrigins.Sum(x => x.ActiveTroops) == 35 &&
               mixedRestored.Teams.Single(x => x.Id == 42).TroopOrigins.Sum(x => x.WoundedTroops) == 20,
            "player reorganization", "mixed-origin recovery and save round trip preserve totals");
        var returnWorld = new WorldState();
        var origin1 = new CityData { Id = 1, OwnerFactionId = 1 };
        var origin2 = new CityData { Id = 2, OwnerFactionId = 1 };
        returnWorld.Cities.AddRange(new[] { origin1, origin2 });
        var returnTeam = mixedRestored.Teams.Single(x => x.Id == 42);
        var origin1Count = returnTeam.TroopOrigins.Single(x => x.CityId == 1);
        var origin2Count = returnTeam.TroopOrigins.Single(x => x.CityId == 2);
        CampaignRetreatService.ReturnMixedTeamToOrigins(returnWorld, origin2, returnTeam, origin2);
        Assert(origin1.InfantryTroops == origin1Count.ActiveTroops + origin1Count.WoundedTroops &&
               origin2.InfantryTroops == origin2Count.ActiveTroops + origin2Count.WoundedTroops && returnTeam.ReturnedTroops == 55,
            "player reorganization", "automatic mixed-origin return credits each friendly city once");
        var settlementWorld = new WorldState();
        var cityOne = new CityData { Id = 1, OwnerFactionId = 1 };
        var cityTwo = new CityData { Id = 2, OwnerFactionId = 1 };
        settlementWorld.Cities.AddRange(new[] { cityOne, cityTwo, new CityData { Id = 3, OwnerFactionId = 2 } });
        var withdrawal = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(mixedCampaign))!;
        withdrawal.SourceCityId = 1; withdrawal.TargetCityId = 3; withdrawal.AttackerFactionId = 1; withdrawal.DefenderFactionId = 2;
        withdrawal.Stage = CampaignStage.FieldBattle;
        var expectedOne = withdrawal.Teams.SelectMany(x => x.TroopOrigins).Where(x => x.CityId == 1).Sum(x => x.ActiveTroops + x.WoundedTroops);
        var expectedTwo = withdrawal.Teams.SelectMany(x => x.TroopOrigins).Where(x => x.CityId == 2).Sum(x => x.ActiveTroops + x.WoundedTroops);
        BattleCampaignService.CompleteCampaign(settlementWorld, withdrawal, CampaignBattleSide.Defender);
        Assert(cityOne.InfantryTroops == expectedOne && cityTwo.InfantryTroops == expectedTwo && withdrawal.Stage == CampaignStage.Resolved,
            "player reorganization", "campaign withdrawal returns transferred and remaining troops by origin");

        var deployCampaign = new ActiveBattleCampaignData { Stage = CampaignStage.CityBattle };
        var emptyReserve = new CampaignBattleTeamData { Id = 1, Side = CampaignBattleSide.Attacker, Location = CampaignTeamLocation.Reserve, OfficerId = 1, ActiveTroops = 0 };
        var readyReserve = new CampaignBattleTeamData { Id = 2, Side = CampaignBattleSide.Attacker, Location = CampaignTeamLocation.Reserve, OfficerId = 2, ActiveTroops = 40 };
        var returnedReinforcement = new CampaignBattleTeamData { Id = 3, Side = CampaignBattleSide.Defender, Location = CampaignTeamLocation.Reserve, OfficerId = 3, ActiveTroops = 30, ReinforcementOrderId = 7 };
        var reinforcement = new ReinforcementOrderData { Id = 7, Side = CampaignBattleSide.Defender, Status = ReinforcementStatus.Deployed, Teams = new() { returnedReinforcement } };
        deployCampaign.Teams.AddRange(new[] { emptyReserve, readyReserve, returnedReinforcement });
        deployCampaign.Reinforcements.Add(reinforcement);
        BattleCampaignService.PromoteReserves(deployCampaign, CampaignBattleSide.Attacker);
        BattleCampaignService.PromoteReserves(deployCampaign, CampaignBattleSide.Defender);
        Assert(emptyReserve.Location == CampaignTeamLocation.Reserve && readyReserve.Location == CampaignTeamLocation.Field &&
               returnedReinforcement.Location == CampaignTeamLocation.Field && deployCampaign.Teams.Count == 3 && reinforcement.Status == ReinforcementStatus.Deployed,
            "player reorganization", "empty reserve waits and refilled deployed reinforcement returns without duplication");
    }
    private static void RunCampaignCaptiveSuccessionTest()
    {
        var world = CreateBattleCampaignWorld();
        world.Cities.Add(TestHelpers.City(4, "DefenderReserve", 2, 100, 100, 500, new[] { 203 }, Array.Empty<int>()));
        var heir = TestHelpers.Officer(203, "Heir", 4);
        world.Officers.Add(heir);
        var faction = world.GetFaction(2)!;
        faction.OfficerIds.Add(203);
        world.GetOfficer(201)!.RelationshipType[heir.Name] = "family,blood";
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        BattleCampaignService.CompleteCampaign(world, campaign, CampaignBattleSide.Attacker);
        var report = world.BattleReports.Single();
        var prisoners = world.PendingCapturedOfficerRecords.Where(record => record.WinnerFactionId == 1).Select(record => record.OfficerId).ToList();
        Assert(report.CapturedOfficerIds.OrderBy(id => id).SequenceEqual(new[] { 201, 202 }) &&
               prisoners.OrderBy(id => id).SequenceEqual(new[] { 201, 202 }) &&
               world.GetOfficer(201)!.CaptiveFactionId == 1 && world.GetOfficer(201)!.JailedCityId == 2,
            "campaign settlement", "city roster and battle teams produce one captive record per officer");
        Assert(faction.RulerOfficerId == 203 && heir.Appointments.Contains(OfficerAppointmentRules.Lord) &&
               !faction.OfficerIds.Contains(201) && !faction.OfficerIds.Contains(202),
            "campaign settlement", "ruler succession runs after captured candidates are removed");
    }

    private static void RunCampaignRetreatReturnEdgesTest()
    {
        var world = CreateBattleCampaignWorld();
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.FieldIntercept);
        var defender = campaign.Teams.First(team => team.Side == CampaignBattleSide.Defender && team.Location == CampaignTeamLocation.Field);
        var city = world.GetCity(campaign.TargetCityId)!;
        var cityTroops = city.Troops;
        var beforeActive = defender.ActiveTroops;
        var fallback = BattleCampaignService.TryRetreatTeamToCity(world, campaign, defender, city.Id);
        Assert(fallback && defender.Location == CampaignTeamLocation.InnerCity && defender.ActiveTroops == beforeActive && city.Troops == cityTroops,
            "campaign retreat", "field defender falls back without returning troops prematurely");

        var source = world.GetCity(1)!;
        var ramBefore = source.RamCount;
        var engineersBefore = source.EngineerTroops;
        var equipment = new CampaignBattleTeamData { FactionId = source.OwnerFactionId, OriginCityId = source.Id, EquipmentType = BattleEquipmentType.Ram, ActiveTroops = 1 };
        var returned = BattleCampaignService.TryReturnSupportTeamToOrigin(world, equipment);
        Assert(returned && source.RamCount == ramBefore + 1 && source.EngineerTroops == engineersBefore + BattleSupportRules.RamEngineerRequirement && equipment.ActiveTroops == 0,
            "campaign retreat", "surviving equipment and crew return to origin");
        BattleCampaignService.TryReturnSupportTeamToOrigin(world, equipment);
        Assert(source.RamCount == ramBefore + 1 && source.EngineerTroops == engineersBefore + BattleSupportRules.RamEngineerRequirement,
            "campaign retreat", "returned equipment is not credited twice");
        var invalid = new CampaignBattleTeamData { FactionId = source.OwnerFactionId, OriginCityId = 9999, ActiveTroops = 20 };
        Assert(!BattleCampaignService.TryReturnSupportTeamToOrigin(world, invalid) && invalid.ActiveTroops == 20,
            "campaign retreat", "missing origin leaves team unchanged");
    }

    private static void RunCampaignDeploymentValidationTest()
    {
        var city = new CityData { InfantryTroops = 100, SiegeTroops = 30, Gold = 50, Food = 50, OfficerIds = new() { 1 } };
        var deployment = new AttackOfficerDeploymentData { OfficerId = 1, TroopType = TroopType.Infantry, TroopCount = 100 };
        ReinforcementDispatchFailure? Check(IReadOnlyCollection<AttackOfficerDeploymentData> deployments, TroopAllocationData troops, int gold, int food)
        {
            try { CampaignDeploymentRules.ValidateCityCanSupply(city, deployments, troops, gold, food); return null; }
            catch (ReinforcementDispatchException exception) { return exception.Failure; }
        }
        Assert(Check(new[] { deployment }, new TroopAllocationData { Infantry = 100 }, 50, 50) == null,
            "campaign deployment validation", "exact available resources accepted");
        Assert(Check(Array.Empty<AttackOfficerDeploymentData>(), new TroopAllocationData(), 0, 0) == ReinforcementDispatchFailure.InvalidDeployment,
            "campaign deployment validation", "empty dispatch rejected");
        Assert(Check(Array.Empty<AttackOfficerDeploymentData>(), new TroopAllocationData(), 1, 0) == null,
            "campaign deployment validation", "supply-only dispatch accepted");
        Assert(Check(new[] { deployment }, new TroopAllocationData { Infantry = 101 }, 0, 0) == ReinforcementDispatchFailure.InsufficientResources,
            "campaign deployment validation", "excess troops rejected");
        var siege = new AttackOfficerDeploymentData { OfficerId = 1, TroopType = TroopType.Siege, TroopCount = 20, SiegeEngineType = SiegeEngineType.Ram };
        Assert(Check(new[] { siege }, new TroopAllocationData { Siege = 20 }, 0, 0) == ReinforcementDispatchFailure.InsufficientSiegeEngines,
            "campaign deployment validation", "missing engine rejected");
        var world = CreateBattleCampaignWorld();
        var target = world.GetCity(2)!;
        var beforeGold = target.Gold;
        var beforeFood = target.Food;
        var attack = CreateBattleCampaignAttack();
        world.GetCity(attack.SourceCityId)!.ConnectedCityIds.Remove(attack.TargetCityId);
        var rejected = false;
        try { BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.CityDefense); }
        catch (InvalidOperationException) { rejected = true; }
        Assert(rejected && target.Gold == beforeGold && target.Food == beforeFood && world.ActiveBattleCampaigns.Count == 0,
            "campaign creation", "invalid route leaves target and campaign list unchanged");
    }

    private static void RunBattleCampaignLifecycleTest()
    {
        var world = CreateBattleCampaignWorld();
        var attack = CreateBattleCampaignAttack();
        var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.FieldIntercept);
        var hasInnerDefense = BattleCampaignService.HasEffectiveInnerCityDefender(campaign);
        var ramReserved = campaign.Teams.Any(team => team.SiegeEngineType == SiegeEngineType.Ram && team.Location == CampaignTeamLocation.Reserve);
        for (var day = 0; day < BattleCampaignService.MaximumBattleDaysPerMonth; day++)
        {
            BattleCampaignService.AdvanceCompletedBattleDay(campaign);
        }

        var reachedLimit = BattleCampaignService.HasReachedMonthlyBattleLimit(campaign);
        campaign.Teams.First(team => team.Side == CampaignBattleSide.Attacker).WoundedTroops = 200;
        campaign.Teams.First(team => team.Side == CampaignBattleSide.Attacker).ActiveTroops -= 200;
        campaign.Teams.First(team => team.Side == CampaignBattleSide.Defender && team.Location == CampaignTeamLocation.InnerCity).WoundedTroops = 200;
        campaign.Teams.First(team => team.Side == CampaignBattleSide.Defender && team.Location == CampaignTeamLocation.InnerCity).ActiveTroops -= 200;
        var attackerBefore = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Attacker).Sum(team => team.WoundedTroops);
        var defenderBefore = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Defender).Sum(team => team.WoundedTroops);
        BattleCampaignService.BeginNextCampaignMonth(campaign, 200, 2);
        var attackerAfter = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Attacker).Sum(team => team.WoundedTroops);
        var defenderAfter = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Defender).Sum(team => team.WoundedTroops);
        Assert(
            campaign.Stage == CampaignStage.FieldBattle && hasInnerDefense && ramReserved && reachedLimit &&
            campaign.BattleDaysThisMonth == 0 && attackerAfter < attackerBefore && defenderAfter < defenderBefore,
            "Battle campaign monthly lifecycle",
            $"inner={hasInnerDefense}, ramReserved={ramReserved}, limit={reachedLimit}, wounded={attackerBefore}->{attackerAfter}/{defenderBefore}->{defenderAfter}");
    }

    private static void RunBattleCampaignReinforcementTest()
    {
        var world = CreateBattleCampaignWorld();
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        var order = BattleCampaignService.DispatchReinforcement(
            world,
            campaign,
            3,
            CampaignBattleSide.Attacker,
            new[] { new AttackOfficerDeploymentData { OfficerId = 102, TroopType = TroopType.Infantry, TroopCount = 1000 } },
            100,
            200);
        BattleCampaignService.AdvanceCompletedBattleDay(campaign);
        var firstDayTraveling = order.Status == ReinforcementStatus.Traveling;
        BattleCampaignService.AdvanceCompletedBattleDay(campaign);
        Assert(
            firstDayTraveling && order.Status == ReinforcementStatus.Deployed &&
            campaign.Teams.Any(team => team.ReinforcementOrderId == order.Id),
            "Battle campaign delayed reinforcement",
            $"eta={order.RouteLinks * 2}, status={order.Status}");
    }

    private static void RunBattleCampaignBlockedEntranceQueueTest()
    {
        var world = CreateBattleCampaignWorld();
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.FieldIntercept);
        var nextId = campaign.Teams.Max(team => team.Id) + 1;
        while (campaign.Teams.Count(team => team.Side == CampaignBattleSide.Attacker && team.Location == CampaignTeamLocation.Field) < BattleCampaignService.MaximumActivePiecesPerSide)
        {
            campaign.Teams.Add(new CampaignBattleTeamData
            {
                Id = nextId++, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player,
                TroopType = TroopType.Infantry, ActiveTroops = 100, MaximumTroops = 100, Location = CampaignTeamLocation.Field
            });
        }

        var order = BattleCampaignService.DispatchReinforcement(world, campaign, 3, CampaignBattleSide.Attacker,
            new[] { new AttackOfficerDeploymentData { OfficerId = 102, TroopType = TroopType.Infantry, TroopCount = 1000 } }, 0, 0);
        for (var day = 0; day < order.RouteLinks * 2; day++) BattleCampaignService.AdvanceCompletedBattleDay(campaign);
        var blocked = order.Status == ReinforcementStatus.Reserve && order.Teams.All(team => team.Location == CampaignTeamLocation.Reserve);
        campaign.Teams.First(team => team.Side == CampaignBattleSide.Attacker && team.Location == CampaignTeamLocation.Field && team.Id != order.Teams[0].Id).Location = CampaignTeamLocation.Eliminated;
        BattleCampaignService.PromoteReserves(campaign, CampaignBattleSide.Attacker);
        var deployed = order.Status == ReinforcementStatus.Deployed && order.Teams.All(team => team.Location == CampaignTeamLocation.Field);
        Assert(blocked && deployed, "Blocked reinforcement queue deploys after an active entrance slot opens", $"blocked={blocked}, deployed={deployed}, status={order.Status}");
    }

    private static void RunFieldReinforcementEntranceSideTest()
    {
        var attacker = InvokePrivateStatic<IEnumerable<Godot.Vector2I>>(typeof(BattleSceneController), "GetFieldBattleReinforcementEdgeGrids", CampaignBattleSide.Attacker).ToList();
        var defender = InvokePrivateStatic<IEnumerable<Godot.Vector2I>>(typeof(BattleSceneController), "GetFieldBattleReinforcementEdgeGrids", CampaignBattleSide.Defender).ToList();
        Assert(attacker.Count > 0 && defender.Count > 0 &&
               attacker.All(grid => grid.Y >= BattleMapData.Height - 2 && grid.Y < BattleMapData.Height) &&
               defender.All(grid => grid.Y >= 0 && grid.Y < 2) && !attacker.Intersect(defender).Any(),
            "Field reinforcements enter on their own rear edge", $"attacker={attacker.Count}, defender={defender.Count}");
    }

    private static void RunChangedDefenseSupportSourceTest()
    {
        foreach (var situation in new[] { "captured", "missing", "route", "other battle" })
        {
            var world = CreateBattleCampaignWorld();
            var source = TestHelpers.City(4, "Original ally", 3, 1500, 4000, 4000, new[] { 301 }, new[] { 2 });
            var second = TestHelpers.City(5, "Other ally", 4, 1500, 4000, 4000, new[] { 401 }, new[] { 2 });
            world.Cities.AddRange(new[] { source, second });
            world.Officers.AddRange(new[] { TestHelpers.Officer(301, "Ally", 4), TestHelpers.Officer(401, "Other ally", 5), TestHelpers.Officer(203, "Second envoy", 2) });
            world.GetCity(2)!.OfficerIds.Add(203);
            world.GetFaction(2)!.OfficerIds.Add(203);
            world.Factions.Add(TestHelpers.Faction(3, "Original ally", false, 301, new[] { 301 }));
            world.Factions.Add(TestHelpers.Faction(4, "Other ally", false, 401, new[] { 401 }));
            var attack = CreateBattleCampaignAttack();
            attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData { SourceCityId = 4, RequestedFactionId = 3, IsAllianceRequest = true, EnvoyOfficerId = 202, RequestedGold = 100 });
            attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData { SourceCityId = 5, RequestedFactionId = 4, IsAllianceRequest = true, EnvoyOfficerId = 203, RequestedGold = 100 });
            attack = System.Text.Json.JsonSerializer.Deserialize<PendingCommandData>(System.Text.Json.JsonSerializer.Serialize(attack))!;
            var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.CityDefense);
            if (situation == "captured") source.OwnerFactionId = 4;
            else if (situation == "missing") world.Cities.Remove(source);
            else if (situation == "route") source.ConnectedCityIds.Clear();
            else world.ActiveBattleCampaigns.Add(new ActiveBattleCampaignData { Id = campaign.Id + 1, SourceCityId = 4, TargetCityId = 6, Stage = CampaignStage.FieldBattle });
            BattleCampaignService.DispatchDefenseReinforcementRequests(world, campaign, attack);
            var rejected = campaign.Invitations.SingleOrDefault(item => item.RequestedSourceCityId == 4);
            var unaffected = campaign.Invitations.SingleOrDefault(item => item.RequestedSourceCityId == 5);
            Assert(rejected?.Status == BattleInvitationStatus.Declined && rejected.InvitedFactionId == 3 &&
                   campaign.Teams.Any(team => team.OfficerId == 202 && team.Side == CampaignBattleSide.Defender) &&
                   unaffected?.Status == BattleInvitationStatus.Pending && campaign.Reinforcements.Count == 0 && source.Gold == 1500,
                "changed defense support source", $"{situation}: request is rejected for original faction, envoy returns, other request continues");
        }
    }

    private static void RunDefenseReinforcementRequestTest()
    {
        var world = CreateBattleCampaignWorld();
        var source = world.GetCity(3)!;
        var defenderFaction = world.GetFaction(2)!;
        world.GetFaction(1)!.OfficerIds.Remove(102);
        defenderFaction.OfficerIds.Add(102);
        source.OwnerFactionId = defenderFaction.Id;
        var officer = world.GetOfficer(102)!;
        officer.CityId = source.Id;

        var attack = CreateBattleCampaignAttack();
        attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData
        {
            SourceCityId = source.Id,
            RequestedGold = 120,
            RequestedFood = 300,
            Deployments = new List<AttackOfficerDeploymentData>
            {
                new() { OfficerId = officer.Id, TroopType = TroopType.Infantry, TroopCount = 1000 }
            }
        });
        var troopsBefore = source.Troops;
        var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchDefenseReinforcementRequests(world, campaign, attack);
        var order = campaign.Reinforcements.SingleOrDefault();

        Assert(
            order?.Status == ReinforcementStatus.Traveling &&
            order.Side == CampaignBattleSide.Defender &&
            order.Gold == 120 && order.Food == 300 &&
            order.RemainingBattleDays == order.RouteLinks * 2 &&
            source.Troops == troopsBefore - 1000 &&
            BattleCampaignService.IsOfficerCommitted(world, officer.Id),
            "Defense request dispatches on campaign day one and locks its source",
            $"status={order?.Status}, eta={order?.RemainingBattleDays}/{order?.RouteLinks * 2}, troops={troopsBefore}->{source.Troops}, committed={BattleCampaignService.IsOfficerCommitted(world, officer.Id)}");
    }

    private static void RunAlliedDefenseReinforcementRequestTest()
    {
        var world = CreateBattleCampaignWorld();
        var allySource = TestHelpers.City(4, "AllyReserve", 3, 1500, 4000, 4000, new[] { 301 }, new[] { 2 });
        var allyOfficer = TestHelpers.Officer(301, "AllyOfficer", allySource.Id, strength: 80, combat: 80);
        world.Cities.Add(allySource);
        world.Officers.Add(allyOfficer);
        world.Factions.Add(TestHelpers.Faction(3, "Ally", false, allyOfficer.Id, new[] { allyOfficer.Id }));
        world.DiplomacyRelations.Add(new DiplomacyRelationData
        {
            FactionAId = 2,
            FactionBId = 3,
            Status = DiplomacyStatusType.Alliance,
            RemainingMonths = 4,
            RelationScore = 50
        });

        var attack = CreateBattleCampaignAttack();
        attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData
        {
            SourceCityId = allySource.Id,
            IsAllianceRequest = true,
            RequestedTroops = 600,
            PromisedGold = 250,
            PromisedFood = 500
        });
        var troopsBefore = allySource.Troops;
        var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchDefenseReinforcementRequests(world, campaign, attack);
        ResolveScheduledAiReplies(world, campaign);
        var invitation = campaign.Invitations.SingleOrDefault();
        var order = campaign.Reinforcements.SingleOrDefault();

        var declinedWorld = CreateBattleCampaignWorld();
        var declinedSource = TestHelpers.City(4, "NeutralReserve", 3, 1500, 4000, 4000, new[] { 301 }, new[] { 2 });
        declinedWorld.Cities.Add(declinedSource);
        declinedWorld.Officers.Add(TestHelpers.Officer(301, "NeutralOfficer", declinedSource.Id, strength: 80, combat: 80));
        declinedWorld.Factions.Add(TestHelpers.Faction(3, "Neutral", false, 301, new[] { 301 }));
        var declinedAttack = CreateBattleCampaignAttack();
        declinedAttack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData
        {
            SourceCityId = declinedSource.Id,
            IsAllianceRequest = true,
            RequestedTroops = 600
        });
        var declinedCampaign = BattleCampaignService.CreateCampaign(declinedWorld, declinedAttack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchDefenseReinforcementRequests(declinedWorld, declinedCampaign, declinedAttack);
        ResolveScheduledAiReplies(declinedWorld, declinedCampaign);
        var declinedInvitation = declinedCampaign.Invitations.SingleOrDefault();

        Assert(
            invitation?.Status == BattleInvitationStatus.Accepted &&
            invitation.RequestedSourceCityId == allySource.Id &&
            invitation.PromisedGold == 250 && invitation.PromisedFood == 500 &&
            order?.Status == ReinforcementStatus.Traveling &&
            order.Side == CampaignBattleSide.Defender &&
            order.SourceCityId == allySource.Id &&
            order.Teams.Count > 0 &&
            allySource.Troops < troopsBefore &&
            allySource.Troops >= BattleCampaignService.MinimumCityGarrison &&
            declinedInvitation?.Status == BattleInvitationStatus.Declined &&
            declinedCampaign.Reinforcements.Count == 0,
            "Allied defense request honors its selected source and resolves accept or decline",
            $"accepted={invitation?.Status}, source={invitation?.RequestedSourceCityId}/{order?.SourceCityId}, status={order?.Status}, teams={order?.Teams.Count ?? 0}, troops={troopsBefore}->{allySource.Troops}, declined={declinedInvitation?.Status}, declinedOrders={declinedCampaign.Reinforcements.Count}");
    }

    private static void RunForeignAidDiplomacyTest()
    {
        var world = CreateBattleCampaignWorld();
        world.Cities.Add(TestHelpers.City(4, "Ally", 3, 2000, 4000, 4000, new[] { 301, 302 }, new[] { 2 }));
        world.Officers.Add(TestHelpers.Officer(301, "Ally officer", 4));
        world.Officers.Add(TestHelpers.Officer(302, "Ally officer 2", 4));
        world.Factions.Add(TestHelpers.Faction(3, "Ally", false, 301, new[] { 301, 302 }));
        var pact = new DiplomacyRelationData { FactionAId = 1, FactionBId = 3, Status = DiplomacyStatusType.Truce, RemainingMonths = 6, RelationScore = 60 };
        world.DiplomacyRelations.Add(pact);
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchReinforcement(world, campaign, 4, CampaignBattleSide.Defender,
            Array.Empty<AttackOfficerDeploymentData>(), 100, 0, CampaignSupplyOwnership.Gift);
        var giftPreservedPact = pact.Status == DiplomacyStatusType.Truce && pact.RelationScore == 60 && pact.TrustFromAToB == 50;
        world.Month++;
        BattleCampaignService.DispatchReinforcement(world, campaign, 4, CampaignBattleSide.Defender,
            new[] { new AttackOfficerDeploymentData { OfficerId = 301, TroopType = TroopType.Infantry, TroopCount = 300 } }, 0, 0, CampaignSupplyOwnership.Expedition);
        var afterTroops = pact.Status == DiplomacyStatusType.Neutral && pact.RemainingMonths == 0 && pact.RelationScore == 42 && pact.TrustFromAToB == 15;
        var loaded = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(campaign))!;
        world.ActiveBattleCampaigns.Remove(campaign); world.ActiveBattleCampaigns.Add(loaded);
        world.Month++;
        BattleCampaignService.DispatchReinforcement(world, loaded, 4, CampaignBattleSide.Defender,
            new[] { new AttackOfficerDeploymentData { OfficerId = 302, TroopType = TroopType.Infantry, TroopCount = 300 } }, 0, 0, CampaignSupplyOwnership.Expedition);
        Assert(giftPreservedPact && afterTroops && pact.RelationScore == 42 && pact.TrustFromAToB == 15,
            "foreign aid diplomacy", "only the first troop dispatch breaks the pact and lowers the opponent's trust, including after saving");

        var aiWorld = CreateBattleCampaignWorld();
        aiWorld.Cities.Add(TestHelpers.City(4, "Ally", 3, 1500, 4000, 4000, new[] { 301 }, new[] { 2 }));
        aiWorld.Officers.Add(TestHelpers.Officer(301, "Ally officer", 4));
        aiWorld.Factions.Add(TestHelpers.Faction(3, "Ally", false, 301, new[] { 301 }));
        aiWorld.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 2, FactionBId = 3, Status = DiplomacyStatusType.Alliance, RemainingMonths = 6, RelationScore = 80 });
        var aiPact = new DiplomacyRelationData { FactionAId = 1, FactionBId = 3, Status = DiplomacyStatusType.Truce, RemainingMonths = 6 };
        aiWorld.DiplomacyRelations.Add(aiPact);
        var attack = CreateBattleCampaignAttack();
        attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData { SourceCityId = 4, IsAllianceRequest = true, RequestedTroops = 600 });
        var aiCampaign = BattleCampaignService.CreateCampaign(aiWorld, attack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchDefenseReinforcementRequests(aiWorld, aiCampaign, attack);
        ResolveScheduledAiReplies(aiWorld, aiCampaign);
        Assert(aiCampaign.Invitations.Single().Status == BattleInvitationStatus.Accepted && aiCampaign.Reinforcements.Single().Teams.Count == 0 &&
               aiPact.Status == DiplomacyStatusType.Truce && aiPact.RemainingMonths == 6,
            "AI aid treaty safety", "AI sends limited supplies instead of troops against a treaty partner");
    }

    private static void RunOpposingSupportExclusionTest()
    {
        var world = CreateBattleCampaignWorld();
        var ally = TestHelpers.City(4, "Ally A", 3, 1000, 1000, 2000, new[] { 301 }, new[] { 2 });
        var other = TestHelpers.City(5, "Ally B", 3, 1000, 1000, 2000, Array.Empty<int>(), new[] { 2 });
        world.Cities.AddRange(new[] { ally, other });
        world.Officers.Add(TestHelpers.Officer(301, "Ally", 4));
        world.Factions.Add(TestHelpers.Faction(3, "Ally", false, 301, new[] { 301 }));
        var attack = CreateBattleCampaignAttack();
        attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData { SourceCityId = 5, IsAllianceRequest = true, EnvoyOfficerId = 202, RequestedGold = 100 });
        var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchReinforcement(world, campaign, 4, CampaignBattleSide.Attacker,
            Array.Empty<AttackOfficerDeploymentData>(), 100, 0, CampaignSupplyOwnership.Gift);
        var before = other.Gold;
        var blocked = false;
        try
        {
            BattleCampaignService.DispatchReinforcement(world, campaign, 5, CampaignBattleSide.Defender,
                Array.Empty<AttackOfficerDeploymentData>(), 100, 0, CampaignSupplyOwnership.Gift);
        }
        catch (ReinforcementDispatchException) { blocked = true; }
        BattleCampaignService.DispatchDefenseReinforcementRequests(world, campaign, attack);
        Assert(blocked && other.Gold == before && campaign.Reinforcements.Count == 1 &&
               campaign.Invitations.Single().Status == BattleInvitationStatus.Declined && campaign.Teams.Any(team => team.OfficerId == 202),
            "opposing support exclusion", "opposing request is rejected without spending, and its envoy returns to reserve");

        var changedWorld = CreateBattleCampaignWorld();
        changedWorld.GetCity(2)!.OwnerFactionId = 3;
        changedWorld.Cities.Add(TestHelpers.City(4, "Former ally", 3, 1000, 1000, 2000, Array.Empty<int>(), new[] { 2 }));
        changedWorld.Factions.Add(TestHelpers.Faction(3, "Former ally", false, 0, Array.Empty<int>()));
        changedWorld.PrewarAllianceRequests.Add(new PrewarAllianceRequestData
        {
            InviterFactionId = 1, AllyFactionId = 3, TargetCityId = 2, TargetFactionId = 2, EnvoyOfficerId = 102, RequestedTroops = 600
        });
        var changedCampaign = BattleCampaignService.CreateCampaign(changedWorld, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchPrewarAllies(changedWorld, changedCampaign);
        Assert(changedCampaign.Invitations.Single().Status == BattleInvitationStatus.Declined && changedCampaign.Reinforcements.Count == 0 &&
               changedWorld.PrewarAllianceRequests.Count == 0 && !BattleCampaignService.IsOfficerCommitted(changedWorld, 102),
            "opposing support changed target", "a former ally now owning the target is rejected without interrupting campaign creation");
    }

    private static void RunAiAidLeadershipRiskTest()
    {
        bool Respond(int intelligence)
        {
            var world = CreateBattleCampaignWorld();
            world.Cities.Add(TestHelpers.City(4, "Support", 3, 1000, 1000, 1000, new[] { 301 }, new[] { 2 }));
            var ruler = TestHelpers.Officer(301, "Ruler", 4);
            ruler.Intelligence = intelligence;
            world.Officers.Add(ruler);
            world.Factions.Add(TestHelpers.Faction(3, "Support", false, 301, new[] { 301 }));
            world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 1, FactionBId = 3, RelationScore = 62 });
            var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
            var defender = world.GetCity(2)!; defender.InfantryTroops = 5000; defender.SyncLegacyTroops();
            var invitation = BattleCampaignService.CreateInvitation(campaign, 1, 3, CampaignBattleSide.Attacker,
                BattleInvitationSupportType.Resources, 0, 0, 100, 0, 4);
            return BattleCampaignService.ResolveAiInvitation(world, campaign, invitation);
        }
        Assert(Respond(0) && !Respond(100), "AI aid leadership caution", "intelligent leadership is more cautious against a highly threatening opponent");
    }

    private static void RunAiAidDistanceDecisionTest()
    {
        bool Respond(bool distant)
        {
            var world = CreateBattleCampaignWorld();
            world.Cities.Add(TestHelpers.City(4, "Support", 3, 1000, 1000, 1000, new[] { 301 }, new[] { distant ? 5 : 2 }));
            if (distant)
            {
                world.Cities.Add(TestHelpers.City(5, "Relay1", 3, 0, 0, 0, Array.Empty<int>(), new[] { 6 }));
                world.Cities.Add(TestHelpers.City(6, "Relay2", 3, 0, 0, 0, Array.Empty<int>(), new[] { 7 }));
                world.Cities.Add(TestHelpers.City(7, "Relay3", 3, 0, 0, 0, Array.Empty<int>(), new[] { 2 }));
            }
            world.Officers.Add(TestHelpers.Officer(301, "Support officer", 4));
            world.Factions.Add(TestHelpers.Faction(3, "Support", false, 301, new[] { 301 }));
            world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 1, FactionBId = 3, RelationScore = 50 });
            var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
            var defender = world.GetCity(2)!; defender.InfantryTroops = 1000; defender.SyncLegacyTroops();
            var invitation = BattleCampaignService.CreateInvitation(campaign, 1, 3, CampaignBattleSide.Attacker,
                BattleInvitationSupportType.Resources, 0, 0, 100, 0, 4);
            return BattleCampaignService.ResolveAiInvitation(world, campaign, invitation);
        }
        Assert(Respond(false) && !Respond(true), "AI aid distance", "a long route can change an otherwise borderline response");
    }

    private static void RunAiPromisedRewardDecisionTest()
    {
        bool Respond(int promisedGold, int trust)
        {
            var world = CreateBattleCampaignWorld();
            world.Cities.Add(TestHelpers.City(4, "Support", 3, 1000, 1000, 1000, new[] { 301 }, new[] { 2 }));
            world.Officers.Add(TestHelpers.Officer(301, "Support officer", 4));
            world.Factions.Add(TestHelpers.Faction(3, "Support", false, 301, new[] { 301 }));
            world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 1, FactionBId = 3, RelationScore = 44, TrustFromBToA = trust });
            var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
            var defender = world.GetCity(2)!; defender.InfantryTroops = 1000; defender.SyncLegacyTroops();
            var invitation = BattleCampaignService.CreateInvitation(campaign, 1, 3, CampaignBattleSide.Attacker,
                BattleInvitationSupportType.Resources, 0, 0, 100, 0, 4, promisedGold);
            return BattleCampaignService.ResolveAiInvitation(world, campaign, invitation);
        }
        Assert(!Respond(0, 50) && Respond(1000, 50) && !Respond(100000, 0),
            "AI promised reward", "a credible promise can change a borderline decision, while no trust limits its influence");
    }

    private static void RunAiInvitationOpponentThreatTest()
    {
        bool Respond(int attackerTroops, int defenderTroops)
        {
            var world = CreateBattleCampaignWorld();
            world.Cities.Add(TestHelpers.City(4, "Support", 3, 1000, 1000, 1000, new[] { 301 }, new[] { 2 }));
            world.Officers.Add(TestHelpers.Officer(301, "Support officer", 4));
            world.Factions.Add(TestHelpers.Faction(3, "Support", false, 301, new[] { 301 }));
            world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 1, FactionBId = 3, RelationScore = 48 });
            var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
            var attacker = world.GetCity(1)!; attacker.InfantryTroops = attackerTroops; attacker.SyncLegacyTroops();
            var defender = world.GetCity(2)!; defender.InfantryTroops = defenderTroops; defender.SyncLegacyTroops();
            var invitation = BattleCampaignService.CreateInvitation(campaign, 1, 3, CampaignBattleSide.Attacker,
                BattleInvitationSupportType.Resources, 0, 0, 100, 0, 4);
            return BattleCampaignService.ResolveAiInvitation(world, campaign, invitation);
        }

        Assert(Respond(1000, 1000) && Respond(100000, 1000) && !Respond(1000, 100000),
            "AI invitation opponent threat", "attacker-side support considers the defender, not the requesting attacker");
    }

    private static void RunAiAidCitySafetyTest()
    {
        (WorldState world, ActiveBattleCampaignData campaign, CityData city) Setup(int food, int enemyTroops)
        {
            var world = CreateBattleCampaignWorld();
            var city = TestHelpers.City(4, "Ally", 3, 1500, food, 4000, new[] { 301 }, new[] { 2, 5 });
            world.Cities.Add(city);
            world.Cities.Add(TestHelpers.City(5, "Threat", 4, 1000, 1000, enemyTroops, Array.Empty<int>(), new[] { 4 }));
            world.Officers.Add(TestHelpers.Officer(301, "AllyOfficer", 4));
            world.Factions.Add(TestHelpers.Faction(3, "Ally", false, 301, new[] { 301 }));
            world.Factions.Add(TestHelpers.Faction(4, "Threat", false, 0, Array.Empty<int>()));
            world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 2, FactionBId = 3, Status = DiplomacyStatusType.Alliance, RemainingMonths = 6, RelationScore = 80 });
            var attack = CreateBattleCampaignAttack();
            attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData { SourceCityId = 4, IsAllianceRequest = true, RequestedTroops = 600, RequestedFood = 300 });
            var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.CityDefense);
            BattleCampaignService.DispatchDefenseReinforcementRequests(world, campaign, attack);
            ResolveScheduledAiReplies(world, campaign);
            return (world, campaign, city);
        }
        var (_, frontier, frontierCity) = Setup(4000, 10000);
        var frontierOrder = frontier.Reinforcements.Single();
        Assert(frontierOrder.Teams.Count == 0 && frontierOrder.Gold > 0 && frontierCity.Troops == 4000,
            "AI aid frontier safety", "an exposed city preserves its garrison and sends limited supplies");
        var (_, shortage, shortageCity) = Setup(100, 0);
        var shortageOrder = shortage.Reinforcements.Single();
        Assert(shortageOrder.Teams.Count == 0 && shortageOrder.Gold > 0 && shortageOrder.Food == 0 && shortageCity.Food == 100,
            "AI aid food safety", "a short-of-food city retains food and sends limited gold instead of troops");
        var (_, capped, cappedCity) = Setup(1100, 0);
        var cappedOrder = capped.Reinforcements.Single();
        Assert(cappedOrder.Teams.Count > 0 && cappedOrder.Food == 100 && cappedCity.Food == 1000,
            "AI aid food reserve", "food support retains five days of the original city garrison's rations");
    }

    private static void RunAiAidOfficerSelectionTest()
    {
        var world = CreateBattleCampaignWorld();
        var officerIds = Enumerable.Range(301, 13).ToArray();
        var city = TestHelpers.City(4, "Large allied roster", 3, 1500, 4000, 4000, officerIds, new[] { 2 });
        world.Cities.Add(city);
        foreach (var id in officerIds)
        {
            var officer = TestHelpers.Officer(id, "Ally", city.Id);
            if (id != 313) { officer.LastAssignedYear = world.Year; officer.LastAssignedMonth = world.Month; }
            world.Officers.Add(officer);
        }
        world.Factions.Add(TestHelpers.Faction(3, "Ally", false, 313, officerIds));
        world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 2, FactionBId = 3, Status = DiplomacyStatusType.Alliance, RemainingMonths = 6, RelationScore = 80 });
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        var invitation = BattleCampaignService.CreateInvitation(campaign, 2, 3, CampaignBattleSide.Defender,
            BattleInvitationSupportType.Troops, 0, 600, 0, 0, city.Id);
        var accepted = BattleCampaignService.ResolveAiInvitation(world, campaign, invitation);
        var order = campaign.Reinforcements.SingleOrDefault();
        Assert(accepted && order != null && order.Teams.Count == 1 && order.Teams[0].OfficerId == 313 && order.Teams[0].ActiveTroops > 0,
            "AI support officer selection", "an eligible officer beyond the first twelve roster entries can lead the requested troops");
    }

    private static void RunAiLimitedSupplyFallbackTest()
    {
        var world = CreateBattleCampaignWorld();
        var allySource = TestHelpers.City(4, "ThreatenedAlly", 3, 2400, 1200, 1000, new[] { 301 }, new[] { 2 });
        var allyOfficer = TestHelpers.Officer(301, "AllyOfficer", allySource.Id, strength: 80, combat: 80);
        world.Cities.Add(allySource);
        world.Officers.Add(allyOfficer);
        world.Factions.Add(TestHelpers.Faction(3, "Ally", false, allyOfficer.Id, new[] { allyOfficer.Id }));
        world.DiplomacyRelations.Add(new DiplomacyRelationData
        {
            FactionAId = 2, FactionBId = 3, Status = DiplomacyStatusType.Alliance,
            RemainingMonths = 6, RelationScore = 80
        });

        var attack = CreateBattleCampaignAttack();
        attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData
        {
            SourceCityId = allySource.Id, IsAllianceRequest = true, EnvoyOfficerId = 202, RequestedTroops = 600
        });
        var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchDefenseReinforcementRequests(world, campaign, attack);
        ResolveScheduledAiReplies(world, campaign);
        var invitation = campaign.Invitations.Single();
        var order = campaign.Reinforcements.SingleOrDefault();

        Assert(invitation.Status == BattleInvitationStatus.Accepted &&
               invitation.DecisionReason.Contains("limited supplies") &&
               order != null && order.Teams.Count == 0 && order.SupplyOwnership == CampaignSupplyOwnership.Gift &&
               (order.Gold > 0 || order.Food > 0) && campaign.Teams.Any(team => team.OfficerId == 202),
            "AI troop refusal supplies fallback", $"status={invitation.Status}, reason={invitation.DecisionReason}, order={order?.Status}, teams={order?.Teams.Count}, supplies={order?.Gold}/{order?.Food}");

        var limitedWorld = CreateBattleCampaignWorld();
        var limitedSource = TestHelpers.City(4, "Limited ally", 3, 2400, 1200, 1100, new[] { 301 }, new[] { 2 });
        limitedWorld.Cities.Add(limitedSource);
        limitedWorld.Officers.Add(TestHelpers.Officer(301, "Ally officer", 4));
        limitedWorld.Factions.Add(TestHelpers.Faction(3, "Ally", false, 301, new[] { 301 }));
        limitedWorld.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 2, FactionBId = 3, Status = DiplomacyStatusType.Alliance, RemainingMonths = 6, RelationScore = 80 });
        var limitedAttack = CreateBattleCampaignAttack();
        limitedAttack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData { SourceCityId = 4, IsAllianceRequest = true, EnvoyOfficerId = 202, RequestedTroops = 600 });
        var limitedCampaign = BattleCampaignService.CreateCampaign(limitedWorld, limitedAttack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchDefenseReinforcementRequests(limitedWorld, limitedCampaign, limitedAttack);
        ResolveScheduledAiReplies(limitedWorld, limitedCampaign);
        var limitedOrder = limitedCampaign.Reinforcements.SingleOrDefault();
        Assert(limitedCampaign.Invitations.Single().Status == BattleInvitationStatus.Accepted && limitedOrder != null &&
               limitedOrder.Teams.Sum(team => team.ActiveTroops) == 100 && limitedSource.Troops == BattleCampaignService.MinimumCityGarrison &&
               limitedOrder.Teams.Any(team => team.OfficerId == 202),
            "AI partial troop support", "dispatch respects the minimum garrison including the envoy loan");
    }

    private static void RunAiCampaignReinforcementTest()
    {
        var attackerWorld = CreateBattleCampaignWorld();
        var attackerCampaign = BattleCampaignService.CreateCampaign(
            attackerWorld,
            CreateBattleCampaignAttack(),
            DefenderBattlePlan.CityDefense);
        var attackerServices = CreateServices(attackerWorld);
        var attackerSource = attackerWorld.GetCity(3)!;
        var attackerTroopsBefore = attackerSource.Troops;
        var attackerResult = attackerServices.Ai.RunSingleCityDecision(1, attackerSource.Id);
        var attackerOrder = attackerCampaign.Reinforcements.SingleOrDefault();
        var attackerValid = attackerOrder != null &&
                            attackerOrder.Side == CampaignBattleSide.Attacker &&
                            attackerOrder.SourceCityId == attackerSource.Id &&
                            attackerOrder.Teams.Count > 0 &&
                            attackerOrder.Teams.All(team =>
                                team.OfficerId > 0 && Enum.IsDefined(typeof(TroopType), team.TroopType) && team.ActiveTroops > 0) &&
                            attackerSource.Troops >= BattleCampaignService.MinimumCityGarrison &&
                            attackerSource.Troops < attackerTroopsBefore;
        Assert(
            attackerResult.Success && attackerValid,
            "AI attacker dispatches valid campaign reinforcement",
            $"success={attackerResult.Success}, side={attackerOrder?.Side}, teams={attackerOrder?.Teams.Count ?? 0}, troops={attackerTroopsBefore}->{attackerSource.Troops}");

        var defenderWorld = CreateBattleCampaignWorld();
        var defenderSource = defenderWorld.GetCity(3)!;
        defenderSource.OwnerFactionId = 2;
        defenderWorld.GetFaction(1)!.OfficerIds.Remove(102);
        defenderWorld.GetFaction(2)!.OfficerIds.Add(102);
        var defenderOfficer = defenderWorld.GetOfficer(102)!;
        defenderOfficer.CityId = defenderSource.Id;
        var defenderCampaign = BattleCampaignService.CreateCampaign(
            defenderWorld,
            CreateBattleCampaignAttack(),
            DefenderBattlePlan.CityDefense);
        var defenderServices = CreateServices(defenderWorld);
        var defenderTroopsBefore = defenderSource.Troops;
        var defenderResult = defenderServices.Ai.RunSingleCityDecision(2, defenderSource.Id);
        var defenderOrder = defenderCampaign.Reinforcements.SingleOrDefault();
        var defenderValid = defenderOrder != null &&
                            defenderOrder.Side == CampaignBattleSide.Defender &&
                            defenderOrder.SourceCityId == defenderSource.Id &&
                            defenderOrder.TargetCityId == defenderCampaign.TargetCityId &&
                            defenderOrder.Teams.Count > 0 &&
                            defenderOrder.Teams.All(team =>
                                team.OfficerId > 0 && Enum.IsDefined(typeof(TroopType), team.TroopType) && team.ActiveTroops > 0) &&
                            defenderSource.Troops >= BattleCampaignService.MinimumCityGarrison &&
                            defenderSource.Troops < defenderTroopsBefore;
        Assert(
            defenderResult.Success && defenderValid,
            "AI defender dispatches valid campaign reinforcement",
            $"success={defenderResult.Success}, side={defenderOrder?.Side}, teams={defenderOrder?.Teams.Count ?? 0}, target={defenderOrder?.TargetCityId}, troops={defenderTroopsBefore}->{defenderSource.Troops}");
    }

    private static void RunPlayerCampaignReinforcementCommandTest()
    {
        var world = CreateBattleCampaignWorld();
        var source = world.GetCity(3)!;
        source.InfantryTroops = 2500;
        source.SpearmanTroops = 0;
        source.CavalryTroops = 0;
        source.ArcherTroops = 0;
        source.CrossbowTroops = 0;
        source.SiegeTroops = 0;
        source.SyncLegacyTroops();
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        var services = CreateServices(world);
        var troopsBefore = source.Troops;
        var goldBefore = source.Gold;
        var foodBefore = source.Food;

        var result = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Attack,
            ActorFactionId = 1,
            SourceCityId = source.Id,
            TargetCityId = campaign.TargetCityId,
            GoldToSend = 120,
            FoodToSend = 300,
            OfficerIds = new List<int> { 102 },
            AttackOfficerDeployments = new List<AttackOfficerDeploymentData>
            {
                new() { OfficerId = 102, TroopType = TroopType.Infantry, TroopCount = 1000 }
            }
        });
        var order = campaign.Reinforcements.SingleOrDefault();

        Assert(
            result.Success &&
            order?.Status == ReinforcementStatus.Traveling &&
            order.Side == CampaignBattleSide.Attacker &&
            order.SourceCityId == source.Id &&
            order.TargetCityId == campaign.TargetCityId &&
            order.RemainingBattleDays == order.RouteLinks * 2 &&
            source.Troops == troopsBefore - 1000 &&
            source.Gold == goldBefore - 120 && source.Food == foodBefore - 300 &&
            BattleCampaignService.IsOfficerCommitted(world, 102),
            "Player attack command dispatches a campaign reinforcement immediately",
            $"success={result.Success}, eta={order?.RemainingBattleDays}/{order?.RouteLinks * 2}, " +
            $"troops={troopsBefore}->{source.Troops}, gold={goldBefore}->{source.Gold}, food={foodBefore}->{source.Food}");
    }

    private static void RunCampaignRetreatDestinationTest()
    {
        foreach (var hasAllyFallback in new[] { true, false })
        {
            var envoyWorld = CreateBattleCampaignWorld();
            envoyWorld.Cities.Add(TestHelpers.City(4, "Captured ally source", 1, 0, 1000, 1000, Array.Empty<int>(), Array.Empty<int>()));
            if (hasAllyFallback) envoyWorld.Cities.Add(TestHelpers.City(5, "Ally refuge", 3, 0, 1000, 1000, Array.Empty<int>(), Array.Empty<int>()));
            envoyWorld.Cities.Add(TestHelpers.City(6, "Envoy refuge", 2, 0, 1000, 1000, Array.Empty<int>(), Array.Empty<int>()));
            envoyWorld.GetCity(2)!.ConnectedCityIds.Add(6);
            envoyWorld.Factions.Add(TestHelpers.Faction(3, "Ally", false, 301, Array.Empty<int>()));
            var envoyCampaign = BattleCampaignService.CreateCampaign(envoyWorld, CreateBattleCampaignAttack(), DefenderBattlePlan.FieldIntercept);
            envoyCampaign.Teams.RemoveAll(item => item.OfficerId == 202);
            envoyWorld.GetCity(2)!.OfficerIds.Remove(202);
            var loan = new CampaignBattleTeamData { Id = 99, OfficerId = 202, FactionId = 3, Side = CampaignBattleSide.Defender,
                TroopType = TroopType.Infantry, ActiveTroops = 100, WoundedTroops = 20, MaximumTroops = 120,
                OriginCityId = 4, OfficerReturnCityId = 2, CooperationObjective = "allied-envoy-loan", Location = CampaignTeamLocation.Field };
            envoyCampaign.Teams.Add(loan);
            envoyCampaign.Participants.Add(new BattleParticipantData { FactionId = 3, Side = CampaignBattleSide.Defender });
            var capturedTroops = envoyWorld.GetCity(4)!.Troops;
            var withdrew = BattleCampaignService.TryRetreatTeamToCity(envoyWorld, envoyCampaign, loan, 6);
            var serialized = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(envoyWorld))!;
            var restoredCampaign = serialized.ActiveBattleCampaigns.Single();
            var restoredLoan = restoredCampaign.Teams.Single(item => item.Id == 99);
            BattleCampaignService.CompleteCampaign(serialized, restoredCampaign, CampaignBattleSide.Defender);
            var contribution = serialized.BattleReports.Single().ParticipantContributions.Single(item => item.FactionId == 3);
            Assert(withdrew && serialized.GetOfficer(202)!.CityId == 6 && serialized.GetCity(6)!.OfficerIds.Contains(202) &&
                   serialized.GetCity(4)!.Troops == capturedTroops && restoredLoan.ActiveTroops == 0 && restoredLoan.WoundedTroops == 0 &&
                   (hasAllyFallback ? serialized.GetCity(5)!.Troops == 1120 && contribution.TroopsReturned == 120 && contribution.TroopsDisbanded == 0 :
                       restoredLoan.DisbandedTroops == 120 && contribution.TroopsDisbanded == 120 && contribution.TroopsReturned == 0) &&
                   contribution.TroopsLost == 0,
                "allied envoy retreat lost origin", $"allyFallback={hasAllyFallback}, withdrew={withdrew}, officer={serialized.GetOfficer(202)!.CityId}, refuge={serialized.GetCity(5)?.Troops}, returned={contribution.TroopsReturned}, disbanded={contribution.TroopsDisbanded}, lost={contribution.TroopsLost}");
        }
        var world = CreateBattleCampaignWorld();
        var destination = world.GetCity(3)!;
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.FieldIntercept);
        var team = campaign.Teams.First(item => item.Side == CampaignBattleSide.Attacker && item.OfficerId == 101);
        var troopsBefore = destination.Troops;
        var destinations = BattleCampaignService.GetRetreatDestinations(world, campaign, team);
        var returned = BattleCampaignService.TryRetreatTeamToCity(world, campaign, team, destination.Id);
        var officer = world.GetOfficer(101)!;

        Assert(
            destinations.Any(city => city.Id == destination.Id) && returned &&
            team.Location == CampaignTeamLocation.NeighborCity &&
            team.RetreatDestinationCityId == destination.Id &&
            team.ActiveTroops == 0 && team.WoundedTroops == 0 &&
            destination.Troops == troopsBefore + team.ReturnedTroops &&
            officer.CityId == destination.Id && destination.OfficerIds.Contains(officer.Id),
            "Campaign team retreats to a selected friendly neighbor and returns its officer and troops",
            $"listed={destinations.Any(city => city.Id == destination.Id)}, returned={returned}, city={officer.CityId}, " +
            $"troops={troopsBefore}->{destination.Troops}, returnedTroops={team.ReturnedTroops}");
    }

    private static void RunBattleCampaignReorganizationTest()
    {
        var world = CreateBattleCampaignWorld();
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        var proposed = campaign.Teams
            .Where(team => team.FactionId == 1)
            .Select(team => new CampaignBattleTeamData
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
                Location = team.Location
            }).ToList();
        var accepted = BattleCampaignService.TryReorganizeFactionTeams(campaign, 1, proposed, out _);
        proposed[0].ActiveTroops++;
        var rejected = !BattleCampaignService.TryReorganizeFactionTeams(campaign, 1, proposed, out _);
        Assert(accepted && rejected, "Battle campaign reorganization preserves totals", $"accepted={accepted}, rejectedInvalid={rejected}");
    }

    private static void RunBattleCampaignCityCaptureSettlementTest()
    {
        var world = CreateBattleCampaignWorld();
        var source = world.GetCity(1)!;
        var target = world.GetCity(2)!;
        var sourceGoldBefore = source.Gold;
        var sourceFoodBefore = source.Food;
        world.InternalAffairsSchedules.Add(new InternalAffairsScheduleData
        {
            CityId = target.Id,
            OfficerId = 201,
            JobType = InternalAffairsJobType.Defend,
            RemainingMonths = 2,
            State = InternalAffairsScheduleState.Active
        });

        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        campaign.Stage = CampaignStage.CityBattle;
        BattleCampaignService.CompleteCampaign(world, campaign, CampaignBattleSide.Attacker);

        var report = world.BattleReports.SingleOrDefault();
        var capturedIds = report?.CapturedOfficerIds ?? new List<int>();
        var queuedCaptiveIds = world.PendingCapturedOfficerRecords
            .Where(record => record.WinnerFactionId == 1 && record.WinnerCityId == target.Id)
            .Select(record => record.OfficerId)
            .ToHashSet();
        var attackerReturned = target.Troops > 0 && target.OfficerIds.Contains(101);
        // CreateCampaign moves the defender's city treasury into the campaign
        // before the battle begins. A captured city must therefore receive the
        // two campaign treasuries exactly once, rather than its old balance a
        // second time.
        var resourcesTransferred = target.Gold == campaign.AttackerGold + campaign.DefenderGold &&
                                   target.Food == campaign.AttackerFood + campaign.DefenderFood &&
                                   report?.AttackerGoldGained == campaign.DefenderGold &&
                                   report?.AttackerFoodGained == campaign.DefenderFood;
        var participantContributions = report?.ParticipantContributions ?? new List<WorldState.BattleParticipantContributionData>();
        var contributionTotalsMatch = participantContributions.Count == campaign.Participants.Count &&
                                      participantContributions.All(item => item.TroopsCommitted >= item.ActiveTroops + item.WoundedTroops + item.TroopsReturned + item.TroopsLost) &&
                                      participantContributions.Any(item => item.FactionId == 1 && item.Side == CampaignBattleSide.Attacker) &&
                                      participantContributions.Any(item => item.FactionId == 2 && item.Side == CampaignBattleSide.Defender);

        Assert(
            campaign.Stage == CampaignStage.Resolved &&
            target.OwnerFactionId == 1 &&
            !world.InternalAffairsSchedules.Any(schedule => schedule.CityId == target.Id) &&
            capturedIds.OrderBy(id => id).SequenceEqual(new[] { 201, 202 }) &&
            queuedCaptiveIds.SetEquals(capturedIds) &&
            attackerReturned && resourcesTransferred && contributionTotalsMatch &&
            source.Gold == sourceGoldBefore && source.Food == sourceFoodBefore,
            "Battle campaign city capture settles map state, captives, resources, and report",
            $"owner={target.OwnerFactionId}, schedules={world.InternalAffairsSchedules.Count}, captives={string.Join(',', capturedIds)}, " +
            $"returned={attackerReturned}, resources={resourcesTransferred}, contributions={participantContributions.Count}, source={source.Gold}/{source.Food}");
    }

    private static void RunAlliedCampaignSettlementTest()
    {
        var world = CreateBattleCampaignWorld();
        var allyCity = TestHelpers.City(4, "AllyReserve", 3, 1500, 4000, 4000, new[] { 301 }, new[] { 2 });
        world.Cities.Add(allyCity);
        world.Officers.Add(TestHelpers.Officer(301, "AllyOfficer", allyCity.Id, strength: 80, combat: 80));
        world.Factions.Add(TestHelpers.Faction(3, "Ally", false, 301, new[] { 301 }));
        var relation = new DiplomacyRelationData
        {
            FactionAId = 2,
            FactionBId = 3,
            Status = DiplomacyStatusType.Alliance,
            RemainingMonths = 4,
            RelationScore = 50
        };
        world.DiplomacyRelations.Add(relation);
        var attack = CreateBattleCampaignAttack();
        attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData
        {
            SourceCityId = allyCity.Id,
            IsAllianceRequest = true,
            RequestedTroops = 600
        });
        var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchDefenseReinforcementRequests(world, campaign, attack);
        ResolveScheduledAiReplies(world, campaign);
        var invitation = campaign.Invitations.Single();
        var enRouteWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        var enRouteCampaign = enRouteWorld.ActiveBattleCampaigns.Single();
        var enRouteCity = enRouteWorld.GetCity(4)!;
        var enRouteGold = enRouteCity.Gold;
        var enRouteFood = enRouteCity.Food;
        var enRouteOrder = enRouteCampaign.Reinforcements.Single();
        foreach (var hasOfficerFallback in new[] { true, false })
        {
            var envoyWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(enRouteWorld))!;
            var envoyCampaign = envoyWorld.ActiveBattleCampaigns.Single();
            var envoyOrder = envoyCampaign.Reinforcements.Single();
            var originalAllyTroops = envoyWorld.GetCity(4)!.Troops;
            envoyWorld.GetCity(2)!.OfficerIds.Remove(202);
            envoyCampaign.Teams.RemoveAll(team => team.OfficerId == 202);
            var envoyTeam = new CampaignBattleTeamData { Id = 99, OfficerId = 202, FactionId = 3, Side = CampaignBattleSide.Defender,
                TroopType = TroopType.Infantry, ActiveTroops = 100, MaximumTroops = 100, OriginCityId = 4,
                OfficerReturnCityId = 2, ReinforcementOrderId = envoyOrder.Id, Location = CampaignTeamLocation.Traveling,
                CooperationObjective = "allied-envoy-loan" };
            envoyOrder.Teams.Add(envoyTeam);
            if (hasOfficerFallback) envoyWorld.Cities.Add(TestHelpers.City(6, "Envoy home", 2, 0, 0, 0, Array.Empty<int>(), Array.Empty<int>()));
            envoyCampaign.Stage = CampaignStage.CityBattle;
            BattleCampaignService.CompleteCampaign(envoyWorld, envoyCampaign, CampaignBattleSide.Attacker);
            var envoy = envoyWorld.GetOfficer(202)!;
            var properOfficer = hasOfficerFallback
                ? envoy.CityId == 6 && envoyWorld.GetCity(6)!.OfficerIds.Contains(202) && envoyWorld.GetFaction(2)!.OfficerIds.Contains(202)
                : FreeOfficerMovement.IsFreeOfficer(envoyWorld, envoy) && envoy.CaptiveFactionId == 0;
            Assert(properOfficer && !envoyWorld.GetCity(4)!.OfficerIds.Contains(202) && envoyWorld.GetCity(4)!.Troops >= originalAllyTroops + 100,
                "allied envoy return ownership", $"fallback={hasOfficerFallback}, officer and borrowed troops return separately");
        }
        var noCityWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(enRouteWorld))!;
        var noCityCampaign = noCityWorld.ActiveBattleCampaigns.Single();
        var noCityOrder = noCityCampaign.Reinforcements.Single();
        noCityWorld.GetCity(4)!.OwnerFactionId = 2;
        var disbandedTroops = noCityOrder.Teams.Sum(team => team.ActiveTroops + team.WoundedTroops);
        var disbandedOfficerIds = noCityOrder.Teams.Where(team => team.OfficerId > 0).Select(team => team.OfficerId).ToArray();
        BattleCampaignService.CompleteCampaign(noCityWorld, noCityCampaign, CampaignBattleSide.Defender);
        var noCityReport = noCityWorld.BattleReports.Single();
        var noCityContribution = noCityReport.ParticipantContributions.Single(item => item.FactionId == 3);
        Assert(noCityOrder.Status == ReinforcementStatus.Cancelled && disbandedOfficerIds.Length > 0 &&
               disbandedOfficerIds.All(id => FreeOfficerMovement.IsFreeOfficer(noCityWorld, noCityWorld.GetOfficer(id)!)) &&
               noCityOrder.Teams.All(team => team.ActiveTroops == 0 && team.WoundedTroops == 0) &&
               noCityReport.DefenderDisbandedTroops == disbandedTroops && noCityContribution.TroopsDisbanded == disbandedTroops &&
               noCityContribution.TroopsReturned == 0 && noCityContribution.TroopsLost == 0,
            "reinforcement no city", "troops disband, officers become free and the report does not count disbanded troops as casualties or returned");
        var lostSourceWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(enRouteWorld))!;
        var lostSourceCampaign = lostSourceWorld.ActiveBattleCampaigns.Single();
        var lostSourceOrder = lostSourceCampaign.Reinforcements.Single();
        var lostSource = lostSourceWorld.GetCity(4)!;
        lostSource.OwnerFactionId = 2;
        var fallbackCity = TestHelpers.City(6, "Fallback", 3, 0, 0, 0, Array.Empty<int>(), Array.Empty<int>());
        lostSourceWorld.Cities.Add(fallbackCity);
        var enemyGold = lostSource.Gold; var enemyFood = lostSource.Food; var enemyTroops = lostSource.Troops;
        var returningTroops = lostSourceOrder.Teams.Sum(team => team.ActiveTroops + team.WoundedTroops);
        BattleCampaignService.CompleteCampaign(lostSourceWorld, lostSourceCampaign, CampaignBattleSide.Defender);
        Assert(lostSourceOrder.Status == ReinforcementStatus.Returned && fallbackCity.Troops == returningTroops &&
               fallbackCity.Gold == lostSourceOrder.Gold && fallbackCity.Food == lostSourceOrder.Food &&
               lostSource.Gold == enemyGold && lostSource.Food == enemyFood && lostSource.Troops == enemyTroops &&
               lostSourceOrder.Teams.Where(team => team.OfficerId > 0).All(team => fallbackCity.OfficerIds.Contains(team.OfficerId)),
            "reinforcement lost source", "undelivered troops, officers and supplies return to a friendly fallback instead of a captured source");
        BattleCampaignService.CompleteCampaign(lostSourceWorld, lostSourceCampaign, CampaignBattleSide.Defender);
        Assert(fallbackCity.Troops == returningTroops && fallbackCity.Gold == lostSourceOrder.Gold && fallbackCity.Food == lostSourceOrder.Food &&
               lostSourceWorld.BattleReports.Count == 1,
            "campaign settlement idempotence", "repeating settlement does not duplicate returns or reports");
        BattleCampaignService.CompleteCampaign(enRouteWorld, enRouteCampaign, CampaignBattleSide.Defender);
        var enRouteContribution = enRouteWorld.BattleReports.Single().ParticipantContributions.Single(item => item.FactionId == 3);
        var enRouteRelation = enRouteWorld.GetDiplomacyRelation(2, 3)!;
        Assert(enRouteContribution.SupportArrived == false && enRouteContribution.RewardGold == 0 && enRouteContribution.RewardFood == 0 &&
               enRouteContribution.RelationDelta == 0 && enRouteRelation.RelationScore == 50 && enRouteRelation.TrustFromAToB == 50 &&
               enRouteCity.Gold == enRouteGold + enRouteOrder.Gold && enRouteCity.Food == enRouteFood + enRouteOrder.Food,
            "allied support en route", "returned support earns no reward or penalty when battle ends before arrival");
        BattleCampaignService.AdvanceCompletedBattleDay(campaign, world);
        BattleCampaignService.AdvanceCompletedBattleDay(campaign, world);
        foreach (var team in campaign.Teams.Where(item => item.FactionId == 3))
            BattleCampaignService.MarkReinforcementTeamPlaced(campaign, team);
        BattleCampaignService.CompleteReinforcementPlacement(campaign);
        campaign.Participants.Single(item => item.FactionId == 3).BattleDaysParticipated = 2;
        var partialWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        var partialCampaign = partialWorld.ActiveBattleCampaigns.Single();
        var partialOrder = partialCampaign.Reinforcements.Single();
        var fieldTeam = partialCampaign.Teams.Single(team => team.FactionId == 3);
        var partialSource = partialWorld.GetCity(4)!;
        partialSource.Gold -= 500; partialSource.Food -= 500;
        var partialGold = partialSource.Gold; var partialFood = partialSource.Food;
        partialOrder.Gold = 500; partialOrder.Food = 500; partialOrder.SuppliesDelivered = false; partialOrder.Status = ReinforcementStatus.Reserve;
        partialOrder.Teams.Add(new CampaignBattleTeamData { Id = 999, FactionId = 3, Side = CampaignBattleSide.Defender,
            TroopType = TroopType.Infantry, ActiveTroops = 100, MaximumTroops = 100, OriginCityId = 4,
            ReinforcementOrderId = partialOrder.Id, Location = CampaignTeamLocation.Reserve });
        var returnedReserveWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(partialWorld))!;
        var returnedReserveCampaign = returnedReserveWorld.ActiveBattleCampaigns.Single();
        var returnedReserveTeam = returnedReserveCampaign.Teams.Single(team => team.FactionId == 3);
        returnedReserveTeam.Location = CampaignTeamLocation.Reserve;
        BattleCampaignService.CompleteCampaign(returnedReserveWorld, returnedReserveCampaign, CampaignBattleSide.Defender);
        var returnedReserveContribution = returnedReserveWorld.BattleReports.Single().ParticipantContributions.Single(item => item.FactionId == 3);
        Assert(returnedReserveTeam.HasEnteredBattle && returnedReserveContribution.SupportArrived == true &&
               returnedReserveContribution.ContributionScore == returnedReserveTeam.MaximumTroops / 100 + returnedReserveContribution.BattleDaysParticipated * 20 &&
               returnedReserveContribution.RewardGold > 0,
            "previously entered allied reserve", "saved participation remains credited after the team returns to reserve before supplies arrive");
        BattleCampaignService.CompleteCampaign(partialWorld, partialCampaign, CampaignBattleSide.Defender);
        var partialContribution = partialWorld.BattleReports.Single().ParticipantContributions.Single(item => item.FactionId == 3);
        var expectedPartialScore = fieldTeam.MaximumTroops / 100 + partialContribution.TroopsLost / 200 + partialContribution.BattleDaysParticipated * 20;
        Assert(partialContribution.SupportArrived == true && partialContribution.ContributionScore == expectedPartialScore &&
               partialContribution.RewardGold > 0 && partialContribution.RelationDelta == 8 &&
               partialSource.Gold == partialGold + 500 + partialContribution.RewardGold &&
               partialSource.Food == partialFood + 500 + partialContribution.RewardFood,
            "partial allied deployment", $"arrived={partialContribution.SupportArrived}, score={partialContribution.ContributionScore}/{expectedPartialScore}, reward={partialContribution.RewardGold}, relation={partialContribution.RelationDelta}");
        var blockedWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        var blockedCampaign = blockedWorld.ActiveBattleCampaigns.Single();
        var blockedOrder = blockedCampaign.Reinforcements.Single();
        CampaignReorganizationRules.SynchronizeReinforcementTeamReferences(blockedCampaign);
        var blockedTeam = blockedCampaign.Teams.Single(item => item.FactionId == 3);
        blockedTeam.HasEnteredBattle = false;
        blockedOrder.SuppliesDelivered = false;
        BattleCampaignService.MarkCampaignTeamSpawnBlocked(blockedCampaign, blockedTeam);
        var blockedTroops = blockedTeam.ActiveTroops + blockedTeam.WoundedTroops;
        var blockedSource = blockedWorld.GetCity(4)!;
        var blockedSourceTroops = blockedSource.Troops;
        blockedCampaign.Participants.Single(item => item.FactionId == 3).BattleDaysParticipated = 0;
        BattleCampaignService.CompleteCampaign(blockedWorld, blockedCampaign, CampaignBattleSide.Defender);
        var blockedContribution = blockedWorld.BattleReports.Single().ParticipantContributions.Single(item => item.FactionId == 3);
        Assert(blockedContribution.SupportArrived == false && blockedContribution.RewardGold == 0 && blockedContribution.RelationDelta == 0 &&
               blockedContribution.ActiveTroops == 0 && blockedContribution.TroopsReturned == blockedTroops &&
               blockedSource.Troops == blockedSourceTroops + blockedTroops && blockedSource.OfficerIds.Contains(blockedTeam.OfficerId) &&
               blockedTeam.ActiveTroops == 0 && blockedTeam.Location == CampaignTeamLocation.NeighborCity,
            "blocked allied deployment", $"arrived={blockedContribution.SupportArrived}, active={blockedContribution.ActiveTroops}, returned={blockedContribution.TroopsReturned}/{blockedTroops}, source={blockedSource.Troops - blockedSourceTroops}");
        var deployedFallbackWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        var deployedFallbackCampaign = deployedFallbackWorld.ActiveBattleCampaigns.Single();
        var deployedTeam = deployedFallbackCampaign.Teams.Single(team => team.FactionId == 3);
        var deployedTroops = deployedTeam.ActiveTroops + deployedTeam.WoundedTroops;
        var deployedOfficerId = deployedTeam.OfficerId;
        var occupiedSource = deployedFallbackWorld.GetCity(4)!;
        occupiedSource.OwnerFactionId = 1;
        var occupiedTroops = occupiedSource.Troops;
        var deployedFallback = TestHelpers.City(6, "Fallback", 3, 0, 0, 0, Array.Empty<int>(), Array.Empty<int>());
        deployedFallbackWorld.Cities.Add(deployedFallback);
        BattleCampaignService.CompleteCampaign(deployedFallbackWorld, deployedFallbackCampaign, CampaignBattleSide.Defender);
        Assert(deployedFallback.Troops == deployedTroops && deployedFallback.OfficerIds.Contains(deployedOfficerId) && occupiedSource.Troops == occupiedTroops,
            "deployed ally lost source", "survivors and officer return to their faction instead of an occupied source");
        var deployedNoCityWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        var deployedNoCityCampaign = deployedNoCityWorld.ActiveBattleCampaigns.Single();
        var deployedNoCityTeam = deployedNoCityCampaign.Teams.Single(team => team.FactionId == 3);
        var deployedNoCityTroops = deployedNoCityTeam.ActiveTroops + deployedNoCityTeam.WoundedTroops;
        var deployedNoCityOfficer = deployedNoCityTeam.OfficerId;
        deployedNoCityWorld.GetCity(4)!.OwnerFactionId = 1;
        BattleCampaignService.CompleteCampaign(deployedNoCityWorld, deployedNoCityCampaign, CampaignBattleSide.Defender);
        var deployedNoCityReport = deployedNoCityWorld.BattleReports.Single();
        var deployedNoCityContribution = deployedNoCityReport.ParticipantContributions.Single(item => item.FactionId == 3);
        Assert(FreeOfficerMovement.IsFreeOfficer(deployedNoCityWorld, deployedNoCityWorld.GetOfficer(deployedNoCityOfficer)!) &&
               deployedNoCityTeam.ActiveTroops == 0 && deployedNoCityTeam.WoundedTroops == 0 &&
               deployedNoCityReport.DefenderDisbandedTroops == deployedNoCityTroops && deployedNoCityContribution.TroopsDisbanded == deployedNoCityTroops &&
               deployedNoCityContribution.ActiveTroops == 0 && deployedNoCityContribution.WoundedTroops == 0,
            "deployed ally no city", "survivors disband, the officer becomes free, and the report preserves the disbanded count");
        var mixedWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        var mixedCampaign = mixedWorld.ActiveBattleCampaigns.Single();
        mixedWorld.Month++;
        var mixedCity = mixedWorld.GetCity(4)!;
        var traveling = BattleCampaignService.DispatchReinforcement(mixedWorld, mixedCampaign, 4, CampaignBattleSide.Defender,
            Array.Empty<AttackOfficerDeploymentData>(), 300, 600, CampaignSupplyOwnership.Gift);
        var secondInvitation = BattleCampaignService.CreateInvitation(mixedCampaign, 2, 3, CampaignBattleSide.Defender,
            BattleInvitationSupportType.Resources, 0, 0, 300, 600, 4, 100000, 100000);
        secondInvitation.Status = BattleInvitationStatus.Accepted;
        traveling.InvitationId = secondInvitation.Id;
        var firstInvitation = mixedCampaign.Invitations.First();
        var firstOrder = BattleCampaignService.GetInvitationReinforcement(mixedCampaign, firstInvitation);
        var secondOrder = BattleCampaignService.GetInvitationReinforcement(mixedCampaign, secondInvitation);
        Assert(firstOrder != null && firstOrder.Id != traveling.Id && ReferenceEquals(secondOrder, traveling) &&
               firstOrder.InvitationId == firstInvitation.Id && secondOrder.InvitationId == secondInvitation.Id,
            "invitation response matching", "two requests to the same faction and city resolve to their own orders");
        var bothWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(mixedWorld))!;
        var bothCampaign = bothWorld.ActiveBattleCampaigns.Single();
        var bothSecond = bothCampaign.Invitations.Single(item => item.Id == secondInvitation.Id);
        bothSecond.PromisedGold = 100;
        bothSecond.PromisedFood = 200;
        BattleCampaignService.AdvanceCompletedBattleDay(bothCampaign, bothWorld);
        BattleCampaignService.AdvanceCompletedBattleDay(bothCampaign, bothWorld);
        BattleCampaignService.CompleteCampaign(bothWorld, bothCampaign, CampaignBattleSide.Defender);
        var bothContribution = bothWorld.BattleReports.Single().ParticipantContributions.Single(item => item.FactionId == 3);
        Assert(bothContribution.RewardGold >= 100 && bothContribution.RewardFood >= 200 &&
               bothContribution.RelationDelta == 8 && bothWorld.GetDiplomacyRelation(2, 3)!.RelationScore == 58 &&
               bothWorld.GetDiplomacyRelation(2, 3)!.TrustFromAToB == 62,
            "multiple arrived invitations", "aggregate delivered agreements and apply relation changes once");
        var mixedGoldBefore = mixedCity.Gold;
        BattleCampaignService.CompleteCampaign(mixedWorld, mixedCampaign, CampaignBattleSide.Defender);
        var mixedContribution = mixedWorld.BattleReports.Single().ParticipantContributions.Single(item => item.FactionId == 3);
        var unpaidWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        var unpaidCampaign = unpaidWorld.ActiveBattleCampaigns.Single();
        unpaidCampaign.Invitations.Single().PromisedGold = 100000;
        unpaidCampaign.Invitations.Single().PromisedFood = 100000;
        BattleCampaignService.CompleteCampaign(unpaidWorld, unpaidCampaign, CampaignBattleSide.Defender);
        var unpaidContribution = unpaidWorld.BattleReports.Single().ParticipantContributions.Single(item => item.FactionId == 3);
        var unpaidRelation = unpaidWorld.GetDiplomacyRelation(2, 3)!;
        Assert(!unpaidContribution.PromiseFulfilled && unpaidContribution.RelationDelta < 0 && unpaidContribution.TrustDelta < 0 &&
               unpaidRelation.TrustFromBToA < 50 && unpaidRelation.TrustFromAToB == 50,
            "unpaid allied reward", "unpaid promises reduce the ally's trust in the inviter after save round trip");
        var goldBefore = allyCity.Gold;
        var foodBefore = allyCity.Food;
        campaign.Stage = CampaignStage.CityBattle;
        BattleCampaignService.CompleteCampaign(world, campaign, CampaignBattleSide.Defender);

        var contribution = world.BattleReports.Single().ParticipantContributions.Single(item => item.FactionId == 3);
        Assert(mixedContribution.ContributionScore == contribution.ContributionScore &&
               mixedContribution.RewardGold == Math.Max(40, mixedContribution.ContributionScore * 2) &&
               mixedContribution.PromiseFulfilled && mixedContribution.RelationDelta == 8 &&
               mixedCity.Gold == mixedGoldBefore + traveling.Gold + mixedContribution.RewardGold,
            "mixed allied support", "traveling supplies return and do not increase contribution score or reward");
        Assert(
            invitation.Status == BattleInvitationStatus.Accepted &&
            contribution.PromiseFulfilled && contribution.ContributionScore > 0 &&
            contribution.RewardGold > 0 && contribution.RewardFood > 0 &&
            allyCity.Gold == goldBefore + contribution.RewardGold &&
            allyCity.Food == foodBefore + contribution.RewardFood &&
            contribution.RelationDelta > 0 && contribution.TrustDelta > 0 &&
            relation.RelationScore == 50 + contribution.RelationDelta &&
            relation.TrustFromAToB == 50 + contribution.TrustDelta,
            "Allied campaign settlement pays contribution reward and improves future trust",
            $"reward={contribution.RewardGold}/{contribution.RewardFood}, relation={relation.RelationScore}, trust={relation.TrustFromAToB}, score={contribution.ContributionScore}");
    }

    private static void RunAttackSchedulingTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "AiAttackCity", 2, 1200, 1200, 3000, new[] { 201 }, new[] { 1 }));
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        world.Officers.Add(TestHelpers.Officer(201, "A1", 2, 85, 60, 60, 85));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(2, 2);

        var pending = world.PendingCommands.Where(x => x.Type == CommandType.Attack && x.SourceCityId == 2 && x.TargetCityId == 1).ToList();
        Assert(pending.Count == 1, "AI attack scheduling", $"pending={pending.Count}");
        Assert(world.GetCity(2)?.Troops == 1500, "AI attack troop reservation", $"troops={world.GetCity(2)?.Troops}");
    }

    private static void RunAiKnownResourceAttackPriorityTest()
    {
        var world = TestHelpers.World();
        var poorTarget = TestHelpers.City(1, "PoorTarget", 1, 1000, 1000, 1200, Array.Empty<int>(), new[] { 2 });
        var aiSource = TestHelpers.City(2, "AiSource", 2, 1000, 1000, 1400, new[] { 201 }, new[] { 1, 3 });
        var richTarget = TestHelpers.City(3, "MetalTarget", 1, 1000, 1000, 1200, Array.Empty<int>(), new[] { 2 });
        richTarget.ResourceDeposits.Add(new CityResourceDepositData
        {
            Type = StrategicResourceType.Metal,
            MonthlyYield = 10,
            RemainingReserve = 600
        });
        world.Cities.AddRange(new[] { poorTarget, aiSource, richTarget });
        world.Officers.Add(TestHelpers.Officer(201, "A1", aiSource.Id));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        world.UpsertCityIntel(2, poorTarget.Id, 4);
        world.UpsertCityIntel(2, richTarget.Id, 4);

        var services = CreateServices(world);
        _ = services.Ai.RunSingleCityDecision(2, aiSource.Id);
        var resourceAttack = world.PendingCommands.SingleOrDefault(command => command.Type == CommandType.Attack);

        Assert(
            resourceAttack?.TargetCityId == richTarget.Id && services.Ai.LastAttackDecisionDetail.Contains("資源價值"),
            "AI prioritizes a known rich resource city without ignoring intelligence",
            $"target={resourceAttack?.TargetCityId}, detail={services.Ai.LastAttackDecisionDetail}");
    }

    private static void RunAiSiegeEquipmentAttackTest()
    {
        var world = TestHelpers.World();
        var target = TestHelpers.City(1, "FortifiedTarget", 1, 1000, 1000, 1300, new[] { 101 }, new[] { 2 });
        target.Defense = 85;
        var source = TestHelpers.City(2, "AiSiegeSource", 2, 1000, 1000, 1600, new[] { 201 }, new[] { 1 });
        source.InfantryTroops = 1500;
        source.SiegeTroops = 100;
        source.RamCount = 1;
        source.SupplyCartCount = 1;
        source.SyncLegacyTroops();
        world.Cities.AddRange(new[] { target, source });
        var defender = TestHelpers.Officer(101, "D1", target.Id, combat: 90);
        defender.Intelligence = 20;
        world.Officers.Add(defender);
        world.Officers.Add(TestHelpers.Officer(201, "A1", source.Id));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        world.UpsertCityIntel(2, target.Id, 4);

        var services = CreateServices(world);
        _ = services.Ai.RunSingleCityDecision(2, source.Id);
        var attack = world.PendingCommands.SingleOrDefault(command => command.Type == CommandType.Attack);
        var ramDeployment = attack?.AttackOfficerDeployments.SingleOrDefault(deployment => deployment.SiegeEngineType == SiegeEngineType.Ram);

        Assert(
            attack?.TargetCityId == target.Id && ramDeployment?.TroopType == TroopType.Siege && ramDeployment.TroopCount > 0 &&
            attack.BattleSupport.SupplyCart && attack.BattleSupport.EngineerTeamCount == 50 && source.SupplyCartCount == 0 && services.Ai.LastAttackDecisionDetail.Contains("攻城器"),
            "AI carries one ready ram, one supply cart, and a river-field engineer team",
            $"target={attack?.TargetCityId}, ram={ramDeployment?.TroopCount}, cart={attack?.BattleSupport.SupplyCart}, engineers={attack?.BattleSupport.EngineerTeamCount}, detail={services.Ai.LastAttackDecisionDetail}");
    }

    private static void RunAttackAutoBreakPactTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 2400, new[] { 101 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "AllyCity", 2, 900, 900, 800, new[] { 201 }, new[] { 1 }));
        world.Officers.Add(TestHelpers.Officer(101, "Attacker", 1, strength: 84, combat: 88));
        world.Officers.Add(TestHelpers.Officer(201, "Defender", 2, strength: 50, combat: 50));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Ally", false, 201, new[] { 201 }));
        world.DiplomacyRelations.Add(new DiplomacyRelationData
        {
            FactionAId = 1,
            FactionBId = 2,
            Status = DiplomacyStatusType.Alliance,
            RemainingMonths = 4,
            RelationScore = 36
        });
        var services = CreateServices(world);

        var schedule = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Attack,
            ActorFactionId = 1,
            SourceCityId = 1,
            TargetCityId = 2,
            TroopsToSend = 1200,
            OfficerIds = new List<int> { 101 }
        });
        var relation = world.GetDiplomacyRelation(1, 2)!;
        _ = services.Turn.ResolvePendingCommands(services.Resolver);

        Assert(schedule.Success, "Attack auto breaks pact schedules", $"success={schedule.Success}");
        Assert(relation.Status == DiplomacyStatusType.Neutral && relation.RemainingMonths == 0, "Attack auto breaks pact clears relation", $"status={relation.Status}, months={relation.RemainingMonths}");
        Assert(world.GetCity(2)?.OwnerFactionId == 1, "Attack after auto break pact resolves", $"owner={world.GetCity(2)?.OwnerFactionId}");
    }

    private static void RunAttackResolutionTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 900, new[] { 101 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "AiAttackCity", 2, 1200, 1200, 3200, new[] { 201 }, new[] { 1 }));
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1, 50, 50, 50, 50));
        world.Officers.Add(TestHelpers.Officer(201, "A1", 2, 90, 60, 60, 90));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        world.InternalAffairsSchedules.Add(new InternalAffairsScheduleData
        {
            CityId = 1,
            JobType = InternalAffairsJobType.Manufacturing,
            ConstructionProjectType = ConstructionProjectType.Ram,
            RemainingMonths = 1,
            TotalMonths = 1,
            State = InternalAffairsScheduleState.Paused
        });
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(2, 2);
        _ = services.Turn.ResolvePendingCommands(services.Resolver);

        Assert(world.GetCity(1)?.OwnerFactionId == 2 &&
               !world.InternalAffairsSchedules.Any(schedule => schedule.CityId == 1),
            "AI attack resolution clears captured-city schedules",
            $"owner={world.GetCity(1)?.OwnerFactionId}, schedules={world.InternalAffairsSchedules.Count}");
    }

    private static void RunAttackRulerDeathPlayerSuccessionPendingTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerFrontier", 1, 1000, 1000, 600, new[] { 101, 102, 103 }, new[] { 2, 3 }));
        world.Cities.Add(TestHelpers.City(2, "EnemyBase", 2, 1200, 1200, 2200, new[] { 201 }, new[] { 1 }));
        world.Cities.Add(TestHelpers.City(3, "FallbackCity", 1, 800, 800, 500, new[] { 104 }, new[] { 1 }));
        world.GetCity(2)!.InfantryTroops = 0;
        world.GetCity(2)!.CavalryTroops = 2200;
        world.GetCity(2)!.SyncLegacyTroops();
        world.Officers.Add(TestHelpers.Officer(101, "PlayerRuler", 1, strength: 60, intelligence: 65, charm: 70, combat: 60));
        world.Officers.Add(TestHelpers.Officer(102, "LordSuccessor", 1, strength: 72, intelligence: 74, charm: 76, combat: 72));
        world.Officers.Add(TestHelpers.Officer(103, "GeneralSuccessor", 1, strength: 84, intelligence: 68, charm: 65, combat: 80));
        world.Officers.Add(TestHelpers.Officer(104, "FallbackOfficer", 3, strength: 66, intelligence: 62, charm: 60, combat: 68));
        world.Officers.Add(TestHelpers.Officer(201, "EnemyGeneral", 2, strength: 88, intelligence: 70, charm: 70, combat: 90));
        world.GetOfficer(102)!.Role = "Lord";
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102, 103, 104 }));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        var scheduled = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Attack,
            ActorFactionId = 2,
            SourceCityId = 2,
            TargetCityId = 1,
            OfficerIds = new List<int> { 201 },
            AttackOfficerDeployments = new List<AttackOfficerDeploymentData>
            {
                new() { OfficerId = 201, TroopType = TroopType.Cavalry, TroopCount = 2200 }
            }
        });
        var pendingAttack = world.PendingCommands.Single(command => command.Type == CommandType.Attack);
        var resolved = services.Resolver.ResolvePendingCommand(pendingAttack);

        var pendingSuccession = world.GetPendingSuccession(1);
        Assert(scheduled.Success, "Attack ruler death scheduling", $"success={scheduled.Success}");
        Assert(resolved.Success, "Attack ruler death resolution", $"success={resolved.Success}");
        Assert(world.GetOfficer(101)!.DeathYear == world.Year, "Attack ruler death marks player ruler dead", $"deathYear={world.GetOfficer(101)!.DeathYear}");
        Assert(world.GetFaction(1)!.RulerOfficerId == 0, "Attack ruler death clears player ruler id", $"ruler={world.GetFaction(1)!.RulerOfficerId}");
        Assert(pendingSuccession != null, "Attack ruler death creates player succession prompt", $"pending={(pendingSuccession != null ? 1 : 0)}");
        Assert(world.GetOfficer(102)!.CaptiveFactionId == 2 && world.GetOfficer(103)!.CaptiveFactionId == 2 &&
               pendingSuccession != null && pendingSuccession.CandidateOfficerIds.SequenceEqual(new[] { 104 }),
            "Attack ruler death excludes captured succession candidates", pendingSuccession == null ? "pending=null" : $"candidates={string.Join(',', pendingSuccession.CandidateOfficerIds)}");
        Assert(resolved.MessageEn.Contains("must choose a successor"), "Attack ruler death result mentions succession", resolved.MessageEn);
    }

    private static void RunDefensePromptEligibilityTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1400, new[] { 101, 102 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "AiAttackCity", 2, 1200, 1200, 2600, new[] { 201 }, new[] { 1 }));
        world.GetCity(2)!.InfantryTroops = 0;
        world.GetCity(2)!.CavalryTroops = 1200;
        world.GetCity(2)!.Troops = 1200;
        world.GetCity(2)!.SyncLegacyTroops();
        world.Officers.Add(TestHelpers.Officer(101, "PlayerRuler", 1, strength: 70, intelligence: 65, charm: 70, combat: 72));
        world.Officers.Add(TestHelpers.Officer(102, "PlayerGeneral", 1, strength: 78, intelligence: 55, charm: 60, combat: 80));
        world.Officers.Add(TestHelpers.Officer(201, "AiGeneral", 2, strength: 84, intelligence: 60, charm: 60, combat: 84));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        var scheduled = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Attack,
            ActorFactionId = 2,
            SourceCityId = 2,
            TargetCityId = 1,
            OfficerIds = new List<int> { 201 },
            AttackOfficerDeployments = new List<AttackOfficerDeploymentData>
            {
                new() { OfficerId = 201, TroopType = TroopType.Cavalry, TroopCount = 1200 }
            }
        });

        var pendingAttack = world.PendingCommands.FirstOrDefault(command =>
            command.Type == CommandType.Attack &&
            command.SourceCityId == 2 &&
            command.TargetCityId == 1);

        var shouldPromptDefense = pendingAttack != null &&
                                  world.GetCity(1)!.OwnerFactionId == services.Turn.GetPlayerFactionId() &&
                                  world.GetCity(1)!.Troops > 0 &&
                                  world.GetCity(1)!.OfficerIds.Count > 0 &&
                                  pendingAttack.DefenderOfficerDeployments.Count == 0;

        Assert(scheduled.Success, "Defense prompt eligibility scheduling", $"success={scheduled.Success}");
        Assert(pendingAttack != null, "Defense prompt eligibility attack exists", $"pending={(pendingAttack != null ? 1 : 0)}");
        Assert(shouldPromptDefense, "Defense prompt eligibility conditions", $"targetTroops={world.GetCity(1)!.Troops}, defenders={world.GetCity(1)!.OfficerIds.Count}, defenderDeployments={pendingAttack?.DefenderOfficerDeployments.Count ?? -1}");
    }

    private static void RunDefenseDeploymentAffectsCombatOutcomeTest()
    {
        var baseWorld = TestHelpers.World(month: 2);
        baseWorld.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1600, new[] { 101, 102 }, new[] { 2 }));
        baseWorld.Cities.Add(TestHelpers.City(2, "AiAttackCity", 2, 1200, 1200, 2100, new[] { 201 }, new[] { 1 }));
        baseWorld.GetCity(1)!.InfantryTroops = 0;
        baseWorld.GetCity(1)!.SpearmanTroops = 1600;
        baseWorld.GetCity(1)!.Defense = 55;
        baseWorld.GetCity(1)!.SyncLegacyTroops();
        baseWorld.GetCity(2)!.InfantryTroops = 0;
        baseWorld.GetCity(2)!.CavalryTroops = 2100;
        baseWorld.GetCity(2)!.SyncLegacyTroops();
        baseWorld.Officers.Add(TestHelpers.Officer(101, "PlayerRuler", 1, strength: 72, intelligence: 65, charm: 70, combat: 75));
        baseWorld.Officers.Add(TestHelpers.Officer(102, "PlayerSpearGeneral", 1, strength: 85, intelligence: 58, charm: 62, combat: 88));
        baseWorld.Officers.Add(TestHelpers.Officer(201, "AiCavalryGeneral", 2, strength: 86, intelligence: 60, charm: 60, combat: 86));
        baseWorld.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        baseWorld.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));

        var fullDefenseWorld = CloneWorld(baseWorld);
        var fullDefenseServices = CreateServices(fullDefenseWorld);
        var fullDefenseSchedule = fullDefenseServices.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Attack,
            ActorFactionId = 2,
            SourceCityId = 2,
            TargetCityId = 1,
            OfficerIds = new List<int> { 201 },
            AttackOfficerDeployments = new List<AttackOfficerDeploymentData>
            {
                new() { OfficerId = 201, TroopType = TroopType.Cavalry, TroopCount = 2100 }
            }
        });
        var fullDefensePending = fullDefenseWorld.PendingCommands.Single(command => command.Type == CommandType.Attack);
        fullDefensePending.DefenderOfficerDeployments = new List<AttackOfficerDeploymentData>
        {
            new() { OfficerId = 102, TroopType = TroopType.Spearman, TroopCount = 1600 }
        };
        var fullDefenseResolved = fullDefenseServices.Turn.ResolvePendingCommands(fullDefenseServices.Resolver).Single();

        var weakDefenseWorld = CloneWorld(baseWorld);
        var weakDefenseServices = CreateServices(weakDefenseWorld);
        var weakDefenseSchedule = weakDefenseServices.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Attack,
            ActorFactionId = 2,
            SourceCityId = 2,
            TargetCityId = 1,
            OfficerIds = new List<int> { 201 },
            AttackOfficerDeployments = new List<AttackOfficerDeploymentData>
            {
                new() { OfficerId = 201, TroopType = TroopType.Cavalry, TroopCount = 2100 }
            }
        });
        var weakDefensePending = weakDefenseWorld.PendingCommands.Single(command => command.Type == CommandType.Attack);
        weakDefensePending.DefenderOfficerDeployments = new List<AttackOfficerDeploymentData>
        {
            new() { OfficerId = 101, TroopType = TroopType.Infantry, TroopCount = 100 }
        };
        var weakDefenseResolved = weakDefenseServices.Turn.ResolvePendingCommands(weakDefenseServices.Resolver).Single();

        Assert(fullDefenseSchedule.Success, "Defense deployment scheduling baseline", $"success={fullDefenseSchedule.Success}");
        Assert(weakDefenseSchedule.Success, "Defense deployment scheduling weak defense", $"success={weakDefenseSchedule.Success}");
        Assert(fullDefenseResolved.Success &&
               fullDefenseWorld.GetCity(1)!.OwnerFactionId == 1 &&
               fullDefenseResolved.MessageEn.Contains("attack against"),
            "Defense deployment holds player city",
            $"owner={fullDefenseWorld.GetCity(1)!.OwnerFactionId}, message={fullDefenseResolved.MessageEn}");
        Assert(weakDefenseResolved.Success && weakDefenseWorld.GetCity(1)!.OwnerFactionId == 2, "Defense deployment affects attack outcome", $"owner={weakDefenseWorld.GetCity(1)!.OwnerFactionId}, message={weakDefenseResolved.MessageEn}");
    }

    private static void RunTroopCounterCombatTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "SpearmanCity", 1, 1000, 1000, 600, new[] { 101 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "CavalryCity", 2, 1000, 1000, 600, new[] { 201 }, new[] { 1 }));
        world.GetCity(1)!.Defense = 0;
        world.GetCity(2)!.Defense = 0;
        world.GetCity(1)!.InfantryTroops = 0;
        world.GetCity(1)!.SpearmanTroops = 600;
        world.GetCity(1)!.SyncLegacyTroops();
        world.GetCity(2)!.InfantryTroops = 0;
        world.GetCity(2)!.CavalryTroops = 600;
        world.GetCity(2)!.SyncLegacyTroops();
        world.Officers.Add(TestHelpers.Officer(101, "SpearmanGeneral", 1, strength: 70, intelligence: 60, charm: 60, combat: 70));
        world.Officers.Add(TestHelpers.Officer(201, "CavalryGeneral", 2, strength: 70, intelligence: 60, charm: 60, combat: 70));

        var resolver = new CombatResolver();
        var attackCounterResult = resolver.Resolve(
            world,
            world.GetCity(1)!,
            world.GetCity(2)!,
            600,
            new List<int> { 101 },
            new List<AttackOfficerDeploymentData> { new() { OfficerId = 101, TroopType = TroopType.Spearman, TroopCount = 600 } },
            new TroopAllocationData { Spearman = 600 });

        var defendCounterResult = resolver.Resolve(
            world,
            world.GetCity(2)!,
            world.GetCity(1)!,
            600,
            new List<int> { 201 },
            new List<AttackOfficerDeploymentData> { new() { OfficerId = 201, TroopType = TroopType.Cavalry, TroopCount = 600 } },
            new TroopAllocationData { Cavalry = 600 });

        var crossbowWorld = TestHelpers.World(month: 2);
        crossbowWorld.Cities.Add(TestHelpers.City(1, "CrossbowCity", 1, 1000, 1000, 500, new[] { 101 }, new[] { 2 }));
        crossbowWorld.Cities.Add(TestHelpers.City(2, "HorseCity", 2, 1000, 1000, 500, new[] { 201 }, new[] { 1 }));
        crossbowWorld.GetCity(1)!.Defense = 0;
        crossbowWorld.GetCity(2)!.Defense = 0;
        crossbowWorld.GetCity(1)!.InfantryTroops = 0;
        crossbowWorld.GetCity(1)!.CrossbowTroops = 500;
        crossbowWorld.GetCity(1)!.SyncLegacyTroops();
        crossbowWorld.GetCity(2)!.InfantryTroops = 0;
        crossbowWorld.GetCity(2)!.CavalryTroops = 500;
        crossbowWorld.GetCity(2)!.SyncLegacyTroops();
        crossbowWorld.Officers.Add(TestHelpers.Officer(101, "CrossbowGeneral", 1, strength: 70, intelligence: 60, charm: 60, combat: 70));
        crossbowWorld.Officers.Add(TestHelpers.Officer(201, "HorseGeneral", 2, strength: 70, intelligence: 60, charm: 60, combat: 70));

        var crossbowCounterResult = resolver.Resolve(
            crossbowWorld,
            crossbowWorld.GetCity(1)!,
            crossbowWorld.GetCity(2)!,
            500,
            new List<int> { 101 },
            new List<AttackOfficerDeploymentData> { new() { OfficerId = 101, TroopType = TroopType.Crossbow, TroopCount = 500 } },
            new TroopAllocationData { Crossbow = 500 });

        var archerWorld = TestHelpers.World(month: 2);
        archerWorld.Cities.Add(TestHelpers.City(1, "ArcherCity", 1, 1000, 1000, 500, new[] { 101 }, new[] { 2 }));
        archerWorld.Cities.Add(TestHelpers.City(2, "SpearCity", 2, 1000, 1000, 500, new[] { 201 }, new[] { 1 }));
        archerWorld.GetCity(1)!.Defense = 0;
        archerWorld.GetCity(2)!.Defense = 0;
        archerWorld.GetCity(1)!.InfantryTroops = 0;
        archerWorld.GetCity(1)!.ArcherTroops = 500;
        archerWorld.GetCity(1)!.SyncLegacyTroops();
        archerWorld.GetCity(2)!.InfantryTroops = 0;
        archerWorld.GetCity(2)!.SpearmanTroops = 500;
        archerWorld.GetCity(2)!.SyncLegacyTroops();
        archerWorld.Officers.Add(TestHelpers.Officer(101, "ArcherGeneral", 1, strength: 70, intelligence: 60, charm: 60, combat: 70));
        archerWorld.Officers.Add(TestHelpers.Officer(201, "SpearGeneral", 2, strength: 70, intelligence: 60, charm: 60, combat: 70));

        var archerCounterResult = resolver.Resolve(
            archerWorld,
            archerWorld.GetCity(1)!,
            archerWorld.GetCity(2)!,
            500,
            new List<int> { 101 },
            new List<AttackOfficerDeploymentData> { new() { OfficerId = 101, TroopType = TroopType.Archer, TroopCount = 500 } },
            new TroopAllocationData { Archer = 500 });

        Assert(attackCounterResult.AttackerWon, "Troop counter spearman beats cavalry", $"won={attackCounterResult.AttackerWon}");
        Assert(!defendCounterResult.AttackerWon, "Troop counter cavalry loses into spearman", $"won={defendCounterResult.AttackerWon}");
        Assert(crossbowCounterResult.AttackerWon, "Troop counter crossbow beats cavalry", $"won={crossbowCounterResult.AttackerWon}");
        Assert(archerCounterResult.AttackerWon, "Troop counter archer beats spearman", $"won={archerCounterResult.AttackerWon}");
    }

    private static void RunAttackSuccessFlowTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "SourceCity", 1, 1000, 1000, 1800, new[] { 101, 102 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "TargetCity", 2, 400, 300, 500, new[] { 201 }, new[] { 1 }));
        world.GetCity(1)!.InfantryTroops = 900;
        world.GetCity(1)!.SpearmanTroops = 900;
        world.GetCity(1)!.SyncLegacyTroops();
        world.Officers.Add(TestHelpers.Officer(101, "LiuBei", 1, strength: 80, intelligence: 80, charm: 85, combat: 80));
        world.Officers.Add(TestHelpers.Officer(102, "GuanYu", 1, strength: 95, intelligence: 70, charm: 75, combat: 95));
        world.Officers.Add(TestHelpers.Officer(201, "Defender", 2, strength: 50, intelligence: 50, charm: 50, combat: 50));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        var scheduled = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Attack,
            ActorFactionId = 1,
            SourceCityId = 1,
            TargetCityId = 2,
            GoldToSend = 200,
            FoodToSend = 300,
            OfficerIds = new List<int> { 101, 102 },
            AttackOfficerDeployments = new List<AttackOfficerDeploymentData>
            {
                new() { OfficerId = 101, TroopType = TroopType.Infantry, TroopCount = 600 },
                new() { OfficerId = 102, TroopType = TroopType.Spearman, TroopCount = 400 }
            }
        });
        var resolvedResults = services.Turn.ResolvePendingCommands(services.Resolver);
        var sourceCity = world.GetCity(1)!;
        var targetCity = world.GetCity(2)!;

        Assert(scheduled.Success, "Attack success scheduling", $"success={scheduled.Success}");
        Assert(resolvedResults.Count == 1, "Attack success resolve count", $"count={resolvedResults.Count}");
        var result = resolvedResults.FirstOrDefault() ?? new CommandResult();
        Assert(targetCity.OwnerFactionId == 1, "Attack success captures city", $"owner={targetCity.OwnerFactionId}");
        Assert(targetCity.OfficerIds.Contains(101) && targetCity.OfficerIds.Contains(102), "Attack success moves officers into captured city", $"officers={string.Join(',', targetCity.OfficerIds)}");
        Assert(targetCity.Gold >= 600 && targetCity.Food >= 600, "Attack success carries supplies into captured city", $"gold={targetCity.Gold}, food={targetCity.Food}");
        Assert(targetCity.Troops > 0 && targetCity.InfantryTroops > 0, "Attack success leaves garrison allocation", $"troops={targetCity.Troops}, infantry={targetCity.InfantryTroops}, spearman={targetCity.SpearmanTroops}");
        Assert(sourceCity.Loyalty == 82, "Attack success raises source city loyalty", $"loyalty={sourceCity.Loyalty}");
        Assert(result.MessageZhHant.Contains("留守兵力") && result.MessageZhHant.Contains("帶入金") && result.MessageZhHant.Contains("帶入糧"), "Attack success log includes garrison and supply details", result.MessageZhHant);
    }

    private static void RunAttackFailureFlowTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "SourceCity", 1, 1000, 1000, 1000, new[] { 101 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "TargetCity", 2, 600, 800, 2200, new[] { 201, 202 }, new[] { 1 }));
        world.Officers.Add(TestHelpers.Officer(101, "Attacker", 1, strength: 55, intelligence: 50, charm: 55, combat: 55));
        world.Officers.Add(TestHelpers.Officer(201, "DefenderA", 2, strength: 90, intelligence: 70, charm: 70, combat: 92));
        world.Officers.Add(TestHelpers.Officer(202, "DefenderB", 2, strength: 88, intelligence: 68, charm: 68, combat: 88));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201, 202 }));
        var services = CreateServices(world);

        var scheduled = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Attack,
            ActorFactionId = 1,
            SourceCityId = 1,
            TargetCityId = 2,
            GoldToSend = 200,
            FoodToSend = 300,
            OfficerIds = new List<int> { 101 },
            AttackOfficerDeployments = new List<AttackOfficerDeploymentData>
            {
                new() { OfficerId = 101, TroopType = TroopType.Infantry, TroopCount = 600 }
            }
        });
        var resolvedResults = services.Turn.ResolvePendingCommands(services.Resolver);
        var sourceCity = world.GetCity(1)!;
        var targetCity = world.GetCity(2)!;

        Assert(scheduled.Success, "Attack failure scheduling", $"success={scheduled.Success}");
        Assert(resolvedResults.Count == 1, "Attack failure resolve count", $"count={resolvedResults.Count}");
        var result = resolvedResults.FirstOrDefault() ?? new CommandResult();
        Assert(targetCity.OwnerFactionId == 2, "Attack failure keeps defender owner", $"owner={targetCity.OwnerFactionId}");
        Assert(sourceCity.OfficerIds.Contains(101), "Attack failure returns officer to source city", $"officers={string.Join(',', sourceCity.OfficerIds)}");
        Assert(sourceCity.Gold == 900 && sourceCity.Food == 850, "Attack failure returns half supplies", $"gold={sourceCity.Gold}, food={sourceCity.Food}");
        Assert(sourceCity.Troops > 400 && sourceCity.Troops < 1000, "Attack failure returns surviving troops only", $"troops={sourceCity.Troops}");
        Assert(result.MessageZhHant.Contains("返還兵力") && result.MessageZhHant.Contains("金返還 +100") && result.MessageZhHant.Contains("糧返還 +150"), "Attack failure log includes troop and supply returns", result.MessageZhHant);
    }

    private static void RunAttackCancellationFlowTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "SourceCity", 1, 1000, 1000, 1200, new[] { 101 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "TargetCity", 2, 600, 600, 900, new[] { 201 }, new[] { 1 }));
        world.Officers.Add(TestHelpers.Officer(101, "Attacker", 1, strength: 80, intelligence: 70, charm: 70, combat: 80));
        world.Officers.Add(TestHelpers.Officer(201, "Other", 2, strength: 65, intelligence: 60, charm: 60, combat: 65));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        var scheduled = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Attack,
            ActorFactionId = 1,
            SourceCityId = 1,
            TargetCityId = 2,
            GoldToSend = 120,
            FoodToSend = 240,
            OfficerIds = new List<int> { 101 },
            AttackOfficerDeployments = new List<AttackOfficerDeploymentData>
            {
                new() { OfficerId = 101, TroopType = TroopType.Infantry, TroopCount = 500 }
            }
        });
        world.GetCity(2)!.OwnerFactionId = 1;
        var resolvedResults = services.Turn.ResolvePendingCommands(services.Resolver);
        var sourceCity = world.GetCity(1)!;

        Assert(scheduled.Success, "Attack cancellation scheduling", $"success={scheduled.Success}");
        Assert(resolvedResults.Count == 1, "Attack cancellation resolve count", $"count={resolvedResults.Count}");
        var result = resolvedResults.FirstOrDefault() ?? new CommandResult();
        Assert(sourceCity.OwnerFactionId == 1 && sourceCity.Troops == 1200, "Attack cancellation returns reserved troops", $"troops={sourceCity.Troops}");
        Assert(sourceCity.Gold == 1000 && sourceCity.Food == 1000, "Attack cancellation returns full supplies", $"gold={sourceCity.Gold}, food={sourceCity.Food}");
        Assert(result.MessageZhHant.Contains("兵力返還 +500") && result.MessageZhHant.Contains("金返還 +120") && result.MessageZhHant.Contains("糧返還 +240"), "Attack cancellation log includes full returns", result.MessageZhHant);
    }
}
