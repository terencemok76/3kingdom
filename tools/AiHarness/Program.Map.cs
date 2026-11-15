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
    private static void RunMoveSchedulingTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(2, "StrongCity", 2, 1500, 1500, 4000, new[] { 201 }, new[] { 3 }));
        world.Cities.Add(TestHelpers.City(3, "WeakCity", 2, 800, 800, 1000, new[] { 202 }, new[] { 2 }));
        world.Officers.Add(TestHelpers.Officer(201, "A1", 2));
        world.Officers.Add(TestHelpers.Officer(202, "A2", 3));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201, 202 }));
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(2, 2);

        var pending = world.PendingCommands.Where(x => x.Type == CommandType.Move && x.SourceCityId == 2 && x.TargetCityId == 3).ToList();
        Assert(pending.Count == 1, "AI move scheduling", $"pending={pending.Count}");
    }

    private static void RunMoveTroopAllocationTest()
    {
        var world = TestHelpers.World();
        var source = TestHelpers.City(1, "Source", 1, 1000, 1000, 0, Array.Empty<int>(), new[] { 2 });
        source.InfantryTroops = 400;
        source.SpearmanTroops = 300;
        source.ArcherTroops = 50;
        source.EngineerTroops = 40;
        source.SyncLegacyTroops();
        var target = TestHelpers.City(2, "Target", 1, 1000, 1000, 0, Array.Empty<int>(), new[] { 1 });
        world.Cities.Add(source);
        world.Cities.Add(target);
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        var services = CreateServices(world);
        var requested = new TroopAllocationData
        {
            Infantry = 120,
            Archer = 30,
            Siege = 10
        };

        var schedule = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Move,
            ActorFactionId = 1,
            SourceCityId = source.Id,
            TargetCityId = target.Id,
            TroopsToSend = requested.Total,
            HasTroopAllocation = true,
            TroopAllocation = requested
        });
        var pending = world.PendingCommands.SingleOrDefault(command => command.Type == CommandType.Move);
        Assert(
            schedule.Success && pending?.TroopAllocation.Infantry == 120 && pending.TroopAllocation.Archer == 30 && pending.TroopAllocation.Siege == 10 && pending.TroopAllocation.Spearman == 0,
            "Move keeps requested troop types while scheduling",
            $"success={schedule.Success}, infantry={pending?.TroopAllocation.Infantry}, archer={pending?.TroopAllocation.Archer}, engineer={pending?.TroopAllocation.Siege}, spearman={pending?.TroopAllocation.Spearman}");

        _ = services.Turn.ResolvePendingCommands(services.Resolver);
        Assert(
            source.InfantryTroops == 280 && source.SpearmanTroops == 300 && source.ArcherTroops == 20 && source.EngineerTroops == 30 &&
            target.InfantryTroops == 120 && target.SpearmanTroops == 0 && target.ArcherTroops == 30 && target.EngineerTroops == 10,
            "Move resolves exact troop-type allocation",
            $"source={source.InfantryTroops}/{source.SpearmanTroops}/{source.ArcherTroops}/{source.EngineerTroops}, target={target.InfantryTroops}/{target.SpearmanTroops}/{target.ArcherTroops}/{target.EngineerTroops}");

        var resourceOnly = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Move,
            ActorFactionId = 1,
            SourceCityId = source.Id,
            TargetCityId = target.Id,
            GoldToSend = 100,
            HasTroopAllocation = true,
            TroopAllocation = new TroopAllocationData()
        });
        var resourceOnlyPending = world.PendingCommands.SingleOrDefault(command => command.Type == CommandType.Move);
        Assert(
            resourceOnly.Success && resourceOnlyPending?.TroopAllocation.Total == 0,
            "Move accepts an explicit zero troop allocation",
            $"success={resourceOnly.Success}, troops={resourceOnlyPending?.TroopAllocation.Total}");
    }

    private static void RunLongRouteCaravanTest()
    {
        var world = TestHelpers.World();
        var source = TestHelpers.City(31, "Origin", 1, 1000, 1000, 0, Array.Empty<int>(), new[] { 32 });
        var relay = TestHelpers.City(32, "Relay", 1, 1000, 1000, 0, Array.Empty<int>(), new[] { 31, 33 });
        var destination = TestHelpers.City(33, "Destination", 1, 1000, 1000, 0, Array.Empty<int>(), new[] { 32 });
        source.Gold = 500;
        source.Food = 800;
        source.InfantryTroops = 150;
        source.SyncLegacyTroops();
        source.Defense = relay.Defense = destination.Defense = 100;
        source.Loyalty = relay.Loyalty = destination.Loyalty = 100;
        world.Cities.AddRange(new[] { source, relay, destination });
        source.OfficerIds.Add(301);
        world.Officers.Add(TestHelpers.Officer(301, "CaravanOfficer", source.Id));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, new[] { 301 }));
        var services = CreateServices(world);
        var localization = new LocalizationService();
        localization.LoadFromFileSystem(GetLocalizationDirectoryPath());

        var route = CaravanRouteRules.FindFriendlyRoute(world, source.Id, destination.Id, 1);
        var schedule = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Move,
            ActorFactionId = 1,
            SourceCityId = source.Id,
            TargetCityId = destination.Id,
            GoldToSend = 200,
            FoodToSend = 300,
            HasTroopAllocation = true,
            TroopAllocation = new TroopAllocationData { Infantry = 100 },
            OfficerIds = new List<int> { 301 }
        });
        var firstMonth = services.Turn.ResolvePendingCommands(services.Resolver).Single();
        var traveling = world.PendingCommands.SingleOrDefault(command => command.Type == CommandType.Move);
        var wasTravelingAfterFirstMonth = traveling?.IsTraveling == true && traveling.CurrentRouteIndex == 1;
        var travelingOfficerStatus = localization.GetOfficerStatus(world, world.GetOfficer(301)!);
        var conflictingMove = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Move,
            ActorFactionId = 1,
            SourceCityId = relay.Id,
            TargetCityId = destination.Id,
            GoldToSend = 1,
            OfficerIds = new List<int> { 301 }
        });
        var secondMonth = services.Turn.ResolvePendingCommands(services.Resolver).Single();

        Assert(
            route.SequenceEqual(new[] { source.Id, relay.Id, destination.Id }) && schedule.Success &&
            firstMonth.MessageZhHant.Contains("下月繼續", StringComparison.Ordinal) &&
            wasTravelingAfterFirstMonth && travelingOfficerStatus == "移動" && !conflictingMove.Success && world.PendingCommands.Count == 0 &&
            source.Gold == 300 && source.Food == 500 && source.InfantryTroops == 50 &&
            relay.Gold == 1000 && relay.Food == 1000 && relay.InfantryTroops == 0 &&
            destination.Gold == 1200 && destination.Food == 1300 && destination.InfantryTroops == 100 &&
            secondMonth.Success,
            "Long-route caravan travels one friendly edge per month and arrives on the second month",
            $"route={string.Join('>', route)}, first={firstMonth.MessageZhHant}, officerStatus={travelingOfficerStatus}, conflictingMove={conflictingMove.Success}, source={source.Gold}/{source.Food}/{source.InfantryTroops}, relay={relay.Gold}/{relay.Food}/{relay.InfantryTroops}, target={destination.Gold}/{destination.Food}/{destination.InfantryTroops}, pending={world.PendingCommands.Count}");
    }

    private static void RunLongRouteInterruptionTest()
    {
        var world = TestHelpers.World();
        var source = TestHelpers.City(41, "Origin", 1, 1000, 1000, 0, Array.Empty<int>(), new[] { 42 });
        var firstRelay = TestHelpers.City(42, "FirstRelay", 1, 1000, 1000, 0, Array.Empty<int>(), new[] { 41, 43 });
        var lastFriendlyRelay = TestHelpers.City(43, "LastFriendlyRelay", 1, 1000, 1000, 0, Array.Empty<int>(), new[] { 42, 44 });
        var destination = TestHelpers.City(44, "Destination", 1, 1000, 1000, 0, Array.Empty<int>(), new[] { 43 });
        source.Gold = 500;
        source.Defense = firstRelay.Defense = lastFriendlyRelay.Defense = destination.Defense = 100;
        source.Loyalty = firstRelay.Loyalty = lastFriendlyRelay.Loyalty = destination.Loyalty = 100;
        world.Cities.AddRange(new[] { source, firstRelay, lastFriendlyRelay, destination });
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 0, Array.Empty<int>()));
        var services = CreateServices(world);
        var schedule = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Move,
            ActorFactionId = 1,
            SourceCityId = source.Id,
            TargetCityId = destination.Id,
            GoldToSend = 200,
            HasTroopAllocation = true,
            TroopAllocation = new TroopAllocationData()
        });

        _ = services.Turn.ResolvePendingCommands(services.Resolver);
        _ = services.Turn.ResolvePendingCommands(services.Resolver);
        destination.OwnerFactionId = 2;
        var interruption = services.Turn.ResolvePendingCommands(services.Resolver).Single();
        Assert(
            schedule.Success && !interruption.Success && interruption.MessageZhHant.Contains("路線已中斷", StringComparison.Ordinal) &&
            firstRelay.Gold == 1000 && lastFriendlyRelay.Gold == 1200 && destination.Gold == 1000 && world.PendingCommands.Count == 0,
            "Long-route caravan continues through friendly relays and stops at the first blocked next city",
            $"result={interruption.MessageZhHant}, firstRelay={firstRelay.Gold}, lastFriendlyRelay={lastFriendlyRelay.Gold}, destination={destination.Gold}, pending={world.PendingCommands.Count}");
    }

    private static void RunCaravanRoadRiskTest()
    {
        var source = TestHelpers.City(1, "Source", 1, 1000, 1000, 0, Array.Empty<int>(), new[] { 2 });
        var target = TestHelpers.City(2, "Target", 1, 1000, 1000, 0, Array.Empty<int>(), new[] { 1 });
        source.Defense = 0;
        source.Loyalty = 0;
        target.Defense = 0;
        target.Loyalty = 0;
        var risk = CaravanRoadRiskRules.Assess(source, target);
        var seed = Enumerable.Range(1, 500)
            .FirstOrDefault(candidate =>
            {
                var probe = TestHelpers.World();
                probe.RandomSeed = candidate;
                return CaravanRoadRiskRules.Resolve(probe, source, target, new TroopAllocationData(), 200).EncounteredBandits;
            });
        var world = TestHelpers.World();
        world.RandomSeed = seed;
        world.Cities.Add(source);
        world.Cities.Add(target);
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        var services = CreateServices(world);
        var schedule = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Move,
            ActorFactionId = 1,
            SourceCityId = source.Id,
            TargetCityId = target.Id,
            GoldToSend = 200,
            HasTroopAllocation = true,
            TroopAllocation = new TroopAllocationData()
        });
        var resolution = services.Turn.ResolvePendingCommands(services.Resolver).Single();
        Assert(
            seed > 0 && risk.Level == CaravanRoadRiskLevel.High && schedule.Success &&
            source.Gold == 800 && target.Gold == 1100 &&
            resolution.MessageZhHant.Contains("未設護衛", StringComparison.Ordinal),
            "Caravan road risk uses both cities and steals cargo from an unescorted convoy",
            $"seed={seed}, risk={risk.Level}/{risk.EncounterChancePercent}%, gold={source.Gold}->{target.Gold}, result={resolution.MessageZhHant}");

        var escort = new TroopAllocationData { Infantry = CaravanRoadRiskRules.MinimumEscortTroops };
        var escorted = CaravanRoadRiskRules.Resolve(world, source, target, escort, 200);
        Assert(
            escorted.HasEscort && escorted.Risk.Level == CaravanRoadRiskLevel.High,
            "Caravan escort threshold enables road field-battle handling",
            $"escort={escort.Total}, hasEscort={escorted.HasEscort}, fieldBattle={escorted.FieldBattleResolved}");

        var safeSource = TestHelpers.City(3, "SafeSource", 1, 0, 0, 0, Array.Empty<int>(), new[] { 4 });
        var safeTarget = TestHelpers.City(4, "SafeTarget", 1, 0, 0, 0, Array.Empty<int>(), new[] { 3 });
        safeSource.Defense = safeTarget.Defense = 100;
        safeSource.Loyalty = safeTarget.Loyalty = 100;
        var forced = CaravanRoadRiskRules.Resolve(world, safeSource, safeTarget, escort, 200, forceEncounterForDebug: true);
        Assert(
            forced.Risk.Level == CaravanRoadRiskLevel.Safe && forced.EncounteredBandits && forced.FieldBattleResolved,
            "Debug caravan action forces a road field battle even on a safe route",
            $"risk={forced.Risk.Level}, ambush={forced.EncounteredBandits}, fieldBattle={forced.FieldBattleResolved}");

        var armedWorld = TestHelpers.World();
        var armedSource = TestHelpers.City(5, "ArmedSource", 1, 500, 500, 0, Array.Empty<int>(), new[] { 6 });
        var armedTarget = TestHelpers.City(6, "ArmedTarget", 1, 500, 500, 0, Array.Empty<int>(), new[] { 5 });
        armedWorld.Cities.Add(armedSource);
        armedWorld.Cities.Add(armedTarget);
        armedWorld.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        armedWorld.ForceNextCaravanAmbushForDebug = true;
        var armedServices = CreateServices(armedWorld);
        var armedSchedule = armedServices.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Move,
            ActorFactionId = 1,
            SourceCityId = armedSource.Id,
            TargetCityId = armedTarget.Id,
            GoldToSend = 100,
            HasTroopAllocation = true,
            TroopAllocation = new TroopAllocationData(),
            ForceCaravanAmbushForDebug = armedWorld.ForceNextCaravanAmbushForDebug
        });
        var armedPending = armedWorld.PendingCommands.SingleOrDefault();
        Assert(
            armedSchedule.Success && armedPending?.ForceCaravanAmbushForDebug == true && !armedWorld.ForceNextCaravanAmbushForDebug,
            "Debug arm is consumed only by the next scheduled cargo Move",
            $"success={armedSchedule.Success}, pendingForce={armedPending?.ForceCaravanAmbushForDebug}, stillArmed={armedWorld.ForceNextCaravanAmbushForDebug}");

        var deliveryWorld = TestHelpers.World();
        var deliverySource = TestHelpers.City(7, "DeliverySource", 1, 0, 0, 0, Array.Empty<int>(), new[] { 8 });
        var deliveryTarget = TestHelpers.City(8, "DeliveryTarget", 1, 0, 0, 0, Array.Empty<int>(), new[] { 7 });
        deliveryWorld.Cities.Add(deliverySource);
        deliveryWorld.Cities.Add(deliveryTarget);
        deliverySource.OfficerIds.AddRange(new[] { 601, 602 });
        deliveryWorld.Officers.Add(TestHelpers.Officer(601, "Escort One", deliverySource.Id));
        deliveryWorld.Officers.Add(TestHelpers.Officer(602, "Escort Two", deliverySource.Id));
        deliveryWorld.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, new[] { 601, 602 }));
        var deliveryCampaign = BattleCampaignService.CreateCaravanEscortCampaign(
            deliveryWorld,
            deliverySource,
            deliveryTarget,
            new TroopAllocationData { Infantry = 120 },
            new[] { 601, 602 },
            gold: 250,
            food: 400,
            horses: 25,
            wood: 30,
            metal: 20,
            stone: 10,
            equipment: new SiegeEngineAllocationData { SupplyCart = 1 });
        var officerLedTransportTeams = deliveryCampaign.Teams
            .Where(team => team.Side == CampaignBattleSide.Defender && team.EquipmentType == BattleEquipmentType.None)
            .ToList();
        Assert(
            officerLedTransportTeams.Count == 2 &&
            officerLedTransportTeams.Sum(team => team.ActiveTroops) == 120 &&
            officerLedTransportTeams.Select(team => team.OfficerId).SequenceEqual(new[] { 601, 602 }),
            "Caravan troops split so every selected officer leads a battle team",
            $"teams={officerLedTransportTeams.Count}, troops={string.Join(',', officerLedTransportTeams.Select(team => team.ActiveTroops))}, officers={string.Join(',', officerLedTransportTeams.Select(team => team.OfficerId))}");
        var everyTransportArrived = deliveryCampaign.Teams
            .Where(team => team.Side == CampaignBattleSide.Defender)
            .All(team => BattleCampaignService.TryDeliverCaravanEscortTeam(deliveryWorld, deliveryCampaign, team));
        Assert(
            deliveryCampaign.AttackerFood == 50,
            "Caravan bandits prepare five days of their own food",
            $"bandits={deliveryCampaign.Teams.Single(team => team.Side == CampaignBattleSide.Attacker).ActiveTroops}, food={deliveryCampaign.AttackerFood}");
        deliveryCampaign.DefenderFoodSpent = 25;
        deliveryCampaign.CaravanFood -= 25;
        deliveryCampaign.AttackerFoodSpent = 22;
        BattleCampaignService.CompleteCaravanEscortCampaign(deliveryWorld, deliveryCampaign, CaravanEscortOutcome.Delivered);
        var deliveryReport = deliveryWorld.BattleReports.SingleOrDefault();
        Assert(
            everyTransportArrived &&
            deliveryCampaign.Stage == CampaignStage.Resolved &&
            deliveryTarget.Gold == 250 && deliveryTarget.Food == 375 &&
            deliveryTarget.Horses == 25 && deliveryTarget.Wood == 30 &&
            deliveryTarget.Metal == 20 && deliveryTarget.Stone == 10 &&
            deliveryTarget.InfantryTroops == 120 &&
            deliveryReport?.CaravanEscortOutcome == CaravanEscortOutcome.Delivered &&
            deliveryReport.DefenderFoodSpent == 25 && deliveryReport.DefenderFoodGained == 375 &&
            deliveryReport.AttackerFoodSpent == 22 &&
            deliveryReport.CaravanArrivedTroops == 120 && deliveryReport.DefenderReturnedTroops == 0 &&
            deliveryReport.WinnerFactionId == 1,
            "Caravan arrival delivers transport teams and cargo to the destination city",
            $"arrived={everyTransportArrived}, targetGold={deliveryTarget.Gold}, targetFood={deliveryTarget.Food}, banditFoodSpent={deliveryReport?.AttackerFoodSpent}, spentFood={deliveryReport?.DefenderFoodSpent}, deliveredFood={deliveryReport?.DefenderFoodGained}, arrivedTroops={deliveryReport?.CaravanArrivedTroops}, outcome={deliveryReport?.CaravanEscortOutcome}, winner={deliveryReport?.WinnerFactionId}");

        var returnWorld = TestHelpers.World();
        var returnSource = TestHelpers.City(9, "ReturnSource", 1, 0, 0, 0, Array.Empty<int>(), new[] { 10 });
        var returnTarget = TestHelpers.City(10, "ReturnTarget", 1, 0, 0, 0, Array.Empty<int>(), new[] { 9 });
        returnWorld.Cities.Add(returnSource);
        returnWorld.Cities.Add(returnTarget);
        returnWorld.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        var returnCampaign = BattleCampaignService.CreateCaravanEscortCampaign(
            returnWorld,
            returnSource,
            returnTarget,
            new TroopAllocationData { Infantry = 100 },
            Array.Empty<int>(),
            gold: 120,
            food: 200,
            horses: 0,
            wood: 0,
            metal: 0,
            stone: 0,
            equipment: new SiegeEngineAllocationData());
        var everyTransportReturned = returnCampaign.Teams
            .Where(team => team.Side == CampaignBattleSide.Defender)
            .All(team => BattleCampaignService.TryReturnCaravanEscortTeamToSource(returnWorld, returnCampaign, team));
        BattleCampaignService.CompleteCaravanEscortCampaign(returnWorld, returnCampaign, CaravanEscortOutcome.ReturnedToSource);
        var returnReport = returnWorld.BattleReports.SingleOrDefault();
        Assert(
            everyTransportReturned && returnSource.Gold == 120 && returnSource.Food == 200 &&
            returnTarget.Gold == 0 && returnTarget.Food == 0 &&
            returnReport?.CaravanEscortOutcome == CaravanEscortOutcome.ReturnedToSource,
            "Caravan retreat from SW returns transport teams and cargo to the source city",
            $"returned={everyTransportReturned}, sourceGold={returnSource.Gold}, targetGold={returnTarget.Gold}, outcome={returnReport?.CaravanEscortOutcome}");

        var mixedWorld = TestHelpers.World();
        var mixedSource = TestHelpers.City(11, "MixedSource", 1, 0, 0, 0, Array.Empty<int>(), new[] { 12 });
        var mixedTarget = TestHelpers.City(12, "MixedTarget", 1, 0, 0, 0, Array.Empty<int>(), new[] { 11 });
        mixedWorld.Cities.Add(mixedSource);
        mixedWorld.Cities.Add(mixedTarget);
        mixedWorld.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        var mixedCampaign = BattleCampaignService.CreateCaravanEscortCampaign(
            mixedWorld,
            mixedSource,
            mixedTarget,
            new TroopAllocationData { Infantry = 100 },
            Array.Empty<int>(),
            gold: 180,
            food: 300,
            horses: 0,
            wood: 0,
            metal: 0,
            stone: 0,
            equipment: new SiegeEngineAllocationData());
        var mixedCart = mixedCampaign.Teams.Single(team => team.EquipmentType == BattleEquipmentType.SupplyCart);
        var mixedEscort = mixedCampaign.Teams.Single(team => team.Side == CampaignBattleSide.Defender && team.EquipmentType == BattleEquipmentType.None);
        var cartArrived = BattleCampaignService.TryDeliverCaravanEscortTeam(mixedWorld, mixedCampaign, mixedCart);
        var escortReturned = BattleCampaignService.TryReturnCaravanEscortTeamToSource(mixedWorld, mixedCampaign, mixedEscort);
        BattleCampaignService.CompleteCaravanEscortCampaign(mixedWorld, mixedCampaign, CaravanEscortOutcome.Delivered);
        Assert(
            cartArrived && escortReturned && mixedTarget.Gold == 180 && mixedTarget.Food == 300 &&
            mixedSource.InfantryTroops == 100 && mixedTarget.InfantryTroops == 0 &&
            mixedWorld.BattleReports.Single().CaravanEscortOutcome == CaravanEscortOutcome.Delivered,
            "Transport succeeds after every team leaves the map with one arriving and one returning",
            $"cartArrived={cartArrived}, escortReturned={escortReturned}, targetGold={mixedTarget.Gold}, sourceInfantry={mixedSource.InfantryTroops}");

        var freeOfficerWorld = TestHelpers.World();
        var freeOfficerSource = TestHelpers.City(13, "OfficerSource", 1, 0, 0, 0, new[] { 701 }, new[] { 14 });
        var freeOfficerTarget = TestHelpers.City(14, "OfficerTarget", 1, 0, 0, 0, Array.Empty<int>(), new[] { 13 });
        freeOfficerWorld.Cities.Add(freeOfficerSource);
        freeOfficerWorld.Cities.Add(freeOfficerTarget);
        freeOfficerWorld.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, new[] { 701 }));
        freeOfficerWorld.Officers.Add(TestHelpers.Officer(701, "EscortedOfficer", freeOfficerSource.Id));
        var freeOfficerCampaign = BattleCampaignService.CreateCaravanEscortCampaign(
            freeOfficerWorld,
            freeOfficerSource,
            freeOfficerTarget,
            new TroopAllocationData { Infantry = 100 },
            new[] { 701 },
            gold: 0,
            food: 0,
            horses: 0,
            wood: 0,
            metal: 0,
            stone: 0,
            equipment: new SiegeEngineAllocationData());
        var defeatedOfficerTeam = freeOfficerCampaign.Teams.Single(team => team.OfficerId == 701);
        defeatedOfficerTeam.Location = CampaignTeamLocation.Eliminated;
        defeatedOfficerTeam.ActiveTroops = 0;
        BattleCampaignService.CompleteCaravanEscortCampaign(freeOfficerWorld, freeOfficerCampaign, CaravanEscortOutcome.Plundered);
        var releasedOfficer = freeOfficerWorld.GetOfficer(701)!;
        Assert(
            FreeOfficerMovement.IsVisibleFreeOfficer(freeOfficerWorld, releasedOfficer) &&
            releasedOfficer.CityId == freeOfficerSource.Id &&
            releasedOfficer.CaptiveFactionId == 0 && releasedOfficer.JailedCityId == 0 &&
            !freeOfficerWorld.Factions.Single().OfficerIds.Contains(releasedOfficer.Id),
            "A defeated caravan officer becomes a free officer instead of a prisoner",
            $"city={releasedOfficer.CityId}, captive={releasedOfficer.CaptiveFactionId}, jail={releasedOfficer.JailedCityId}, employed={freeOfficerWorld.Factions.Single().OfficerIds.Contains(releasedOfficer.Id)}");
    }

    private static void RunAiCaravanAmbushAutoResolutionTest()
    {
        var world = TestHelpers.World();
        world.InteractiveBattlesEnabled = true;
        var playerCity = TestHelpers.City(1, "PlayerCity", 1, 0, 0, 0, Array.Empty<int>(), Array.Empty<int>());
        var aiSource = TestHelpers.City(2, "AiSource", 2, 500, 500, 500, Array.Empty<int>(), new[] { 3 });
        var aiTarget = TestHelpers.City(3, "AiTarget", 2, 0, 0, 0, Array.Empty<int>(), new[] { 2 });
        aiSource.Defense = aiTarget.Defense = 100;
        aiSource.Loyalty = aiTarget.Loyalty = 100;
        world.Cities.AddRange(new[] { playerCity, aiSource, aiTarget });
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 0, Array.Empty<int>()));
        var services = CreateServices(world);

        var schedule = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Move,
            ActorFactionId = 2,
            SourceCityId = aiSource.Id,
            TargetCityId = aiTarget.Id,
            GoldToSend = 100,
            HasTroopAllocation = true,
            TroopAllocation = new TroopAllocationData { Infantry = CaravanRoadRiskRules.MinimumEscortTroops },
            ForceCaravanAmbushForDebug = true
        });
        var resolution = services.Turn.ResolvePendingCommands(services.Resolver).Single();

        Assert(
            schedule.Success && resolution.Success && resolution.ActiveBattleCampaignId == 0 &&
            world.ActiveBattleCampaigns.Count == 0 && aiSource.Gold == 400 && aiSource.InfantryTroops == 400 &&
            aiTarget.Gold <= 100 && aiTarget.InfantryTroops is > 0 and < CaravanRoadRiskRules.MinimumEscortTroops &&
            resolution.MessageZhHant.Contains("道路風險", StringComparison.Ordinal),
            "AI escorted caravan ambush resolves automatically without creating a player battle",
            $"scheduled={schedule.Success}, campaign={resolution.ActiveBattleCampaignId}, active={world.ActiveBattleCampaigns.Count}, sourceGold={aiSource.Gold}, targetGold={aiTarget.Gold}, targetTroops={aiTarget.InfantryTroops}, result={resolution.MessageZhHant}");
    }

    private static void RunMapMissionRoadTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "A", 1, 1000, 1000, 1000, Array.Empty<int>(), new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "B", 2, 1000, 1000, 1000, Array.Empty<int>(), new[] { 1, 3 }));
        world.Cities.Add(TestHelpers.City(3, "C", 3, 1000, 1000, 1000, Array.Empty<int>(), new[] { 2 }));
        world.Cities.Add(TestHelpers.City(4, "D", 1, 1000, 1000, 1000, Array.Empty<int>(), Array.Empty<int>()));
        Assert(MapRoadRouting.FindRoadRoute(world, 1, 3).SequenceEqual(new[] { 1, 2, 3 }) &&
               MapRoadRouting.FindRoadRoute(world, 3, 1).SequenceEqual(new[] { 3, 2, 1 }),
            "Mission uses rendered roads in both directions across factions", "1-2-3 and 3-2-1");
        Assert(MapRoadRouting.FindRoadRoute(world, 1, 1).SequenceEqual(new[] { 1 }) &&
               MapRoadRouting.FindRoadRoute(world, 1, 4).Count == 0 && MapRoadRouting.FindRoadRoute(world, 1, 999).Count == 0,
            "Local, disconnected and missing destinations are distinct", "local and no road");
        world.GetCity(2)!.ConnectedCityIds.Remove(3);
        world.GetCity(3)!.ConnectedCityIds.Add(2);
        Assert(MapRoadRouting.FindRoadRoute(world, 1, 3).Count == 0,
            "Mission ignores a one-way edge that is not rendered", "no invisible shortcut");
    }

    private static void RunMapMissionPresentationTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "Source", 1, 1000, 1000, 1000, new[] { 101 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "Target", 2, 1000, 1000, 1000, new[] { 201, 202 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Player", 1));
        world.Officers.Add(TestHelpers.Officer(201, "Ruler", 2));
        world.Officers.Add(TestHelpers.Officer(202, "Target", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201, 202 }));
        var spy = new PendingCommandData { Type = CommandType.Spy, ActorFactionId = 1, SourceCityId = 1, TargetCityId = 2 };
        var diplomacy = new PendingCommandData { Type = CommandType.Diplomacy, ActorFactionId = 1, SourceCityId = 1, TargetFactionId = 2 };
        var hire = new PendingCommandData { Type = CommandType.HireOfficer, ActorFactionId = 1, SourceCityId = 1, TargetOfficerId = 202 };
        var contact = new PendingCommandData { Type = CommandType.ContactOfficer, ActorFactionId = 2, SourceCityId = 2, TargetCityId = 1 };
        var move = new PendingCommandData { Type = CommandType.Move, ActorFactionId = 1, SourceCityId = 1, TargetCityId = 2 };
        var attack = new PendingCommandData { Type = CommandType.Attack, ActorFactionId = 2, SourceCityId = 2, TargetCityId = 1 };
        world.PendingCommands.AddRange(new[] { spy, contact, diplomacy, move, hire, attack, new PendingCommandData { Type = CommandType.Search, ActorFactionId = 1 } });
        Assert(MapCommandPresentationRules.GetFactionCommands(world, 1, 1).SequenceEqual(new[] { spy, diplomacy, move, hire }) &&
               MapCommandPresentationRules.GetFactionCommands(world, 2, 1).SequenceEqual(new[] { contact, attack }),
            "Map presentation preserves per-faction order for player and AI", "spy, diplomacy, hire, contact and military");
        Assert(MapCommandPresentationRules.TryGetMission(world, spy, out var spyCity, out var spyIcon) && spyCity == 2 && spyIcon == MapMovementIcon.Spy &&
               MapCommandPresentationRules.TryGetMission(world, diplomacy, out var diplomacyCity, out var diplomacyIcon) && diplomacyCity == 2 && diplomacyIcon == MapMovementIcon.Diplomacy,
            "Spy and diplomacy resolve their correct destinations", "target city and ruler city");
        Assert(MapCommandPresentationRules.TryGetMission(world, hire, out var hireCity, out var hireIcon) && hireCity == 2 && hireIcon == MapMovementIcon.RecruitOfficer &&
               MapCommandPresentationRules.TryGetMission(world, contact, out var contactCity, out _) && contactCity == 1,
            "Hire and officer contact resolve their target cities", "target officer and scheduled city");
        world.GetOfficer(202)!.CityId = 1;
        Assert(MapCommandPresentationRules.TryGetMission(world, hire, out hireCity, out _) && hireCity == 1,
            "Same-city recruitment retains a valid mission", "local destination");
        Assert(!MapCommandPresentationRules.TryGetMission(world, new PendingCommandData { Type = CommandType.Diplomacy, SourceCityId = 1, TargetFactionId = 999 }, out _, out _) &&
               !MapCommandPresentationRules.TryGetMission(world, new PendingCommandData { Type = CommandType.HireOfficer, SourceCityId = 1, TargetOfficerId = 999 }, out _, out _),
            "Missing mission destinations are skipped", "no invalid target");
        world.GetOfficer(202)!.CityId = 2;
        world.CovertOfficerRecords.Add(new WorldState.CovertOfficerData { SponsorFactionId = 1, OriginalFactionId = 2, OfficerId = 202, DiscordTargetOfficerId = 201, DiscordScheduledMonth = 1 });
        var pendingCount = world.PendingCommands.Count;
        var covertPresentation = MapCommandPresentationRules.GetFactionCommands(world, 1, 1).Last();
        Assert(covertPresentation.Type == CommandType.Spy && covertPresentation.SourceCityId == 2 && covertPresentation.TargetCityId == 2 &&
               world.PendingCommands.Count == pendingCount && !world.PendingCommands.Contains(covertPresentation),
            "Covert operations animate locally without creating a pending order", "presentation only");
        var enemySpy = new PendingCommandData { Type = CommandType.Spy, ActorFactionId = 2, SourceCityId = 2, TargetCityId = 1 };
        world.PendingCommands.Add(enemySpy);
        world.CovertOfficerRecords.Add(new WorldState.CovertOfficerData { SponsorFactionId = 2, OriginalFactionId = 1, OfficerId = 101, DiscordTargetOfficerId = 102, DiscordScheduledMonth = 1 });
        Assert(!MapCommandPresentationRules.GetFactionCommands(world, 2, 1).Any(command => command.Type == CommandType.Spy) &&
               MapCommandPresentationRules.GetFactionCommands(world, 2, 2).Any(command => command == enemySpy),
            "Foreign spy and covert activity stay hidden from player", "viewer sees only own spy actions");
    }
}
