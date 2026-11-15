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
    private static void RunPrewarAllianceRequestTest()
    {
        var world = CreateBattleCampaignWorld();
        var ally = TestHelpers.City(4, "Ally", 3, 1500, 4000, 4000, new[] { 301 }, new[] { 2 });
        world.Cities.Add(ally);
        world.Officers.Add(TestHelpers.Officer(301, "AllyOfficer", 4));
        world.Factions.Add(TestHelpers.Faction(3, "Ally", false, 301, new[] { 301 }));
        world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 1, FactionBId = 3, Status = DiplomacyStatusType.Alliance, RemainingMonths = 12, RelationScore = 80 });
        var request = new PrewarAllianceRequestData { InviterFactionId = 1, AllyFactionId = 3, TargetCityId = 2, EnvoyOfficerId = 102, RequestedTroops = 600, PromisedGold = 100 };
        var accepted = BattleCampaignService.TryAcceptPrewarAlliance(world, request);
        var duplicateRejected = !BattleCampaignService.TryAcceptPrewarAlliance(world, request);
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.FieldIntercept);
        BattleCampaignService.DispatchPrewarAllies(world, campaign);
        var order = campaign.Reinforcements.SingleOrDefault(item => item.FactionId == 3);
        BattleCampaignService.DispatchPrewarAllies(world, campaign);
        Assert(accepted && duplicateRejected && order != null && order.Side == CampaignBattleSide.Attacker && order.Status == ReinforcementStatus.Traveling &&
               order.SourceCityId == 4 && order.Teams.Count > 0 && world.PrewarAllianceRequests.Count == 0 && campaign.Reinforcements.Count == 1 && campaign.Invitations.Single().PromisedGold == 100,
            "Prewar ally accepts then dispatches once when the target is attacked", $"accepted={accepted}, duplicateRejected={duplicateRejected}, order={order?.Status}, teams={order?.Teams.Count}");
    }

    private static void RunDefenderPlanOverrideTest()
    {
        var world = CreateBattleCampaignWorld();
        var services = CreateServices(world);
        var scheduled = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Attack,
            ActorFactionId = 1,
            SourceCityId = 1,
            TargetCityId = 2,
            GoldToSend = 100,
            FoodToSend = 500,
            DefenderBattlePlanOverride = DefenderBattlePlan.FieldIntercept,
            OfficerIds = new List<int> { 101 },
            AttackOfficerDeployments = new List<AttackOfficerDeploymentData>
            {
                new() { OfficerId = 101, TroopType = TroopType.Infantry, TroopCount = 1000 }
            }
        });
        var pending = world.PendingCommands.Single(command => command.Type == CommandType.Attack);
        var resolved = services.Turn.ResolvePendingCommands(services.Resolver).Single();
        var campaign = world.ActiveBattleCampaigns.Single();
        Assert(
            scheduled.Success && resolved.Success &&
            pending.DefenderBattlePlanOverride == DefenderBattlePlan.FieldIntercept &&
            campaign.DefenderPlan == DefenderBattlePlan.FieldIntercept && campaign.Stage == CampaignStage.FieldBattle,
            "Debug defender plan override",
            $"scheduled={scheduled.Success}, override={pending.DefenderBattlePlanOverride}, stage={campaign.Stage}");
    }

    private static void RunAiMultiCityLogisticsTest()
    {
        var world = TestHelpers.World();
        var source = TestHelpers.City(31, "AiDepot", 2, 600, 900, 1200, Array.Empty<int>(), new[] { 32 });
        var relay = TestHelpers.City(32, "AiRelay", 2, 700, 700, 900, Array.Empty<int>(), new[] { 31, 33 });
        var frontline = TestHelpers.City(33, "AiFrontline", 2, 500, 100, 100, Array.Empty<int>(), new[] { 32, 34 });
        var enemy = TestHelpers.City(34, "EnemyBorder", 3, 500, 500, 500, Array.Empty<int>(), new[] { 33 });
        source.Horses = 90;
        source.Wood = 90;
        source.Metal = 60;
        source.Stone = 30;
        world.Cities.AddRange(new[] { source, relay, frontline, enemy });
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(3, "Enemy", false, 0, Array.Empty<int>()));
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(2, source.Id);

        var order = world.PendingCommands.SingleOrDefault(command => command.Type == CommandType.Move && command.SourceCityId == source.Id);
        Assert(
            order?.TargetCityId == frontline.Id && order.RouteCityIds.SequenceEqual(new[] { source.Id, relay.Id, frontline.Id }) &&
            order.TroopsToSend > 0 && order.GoldToSend > 0 && order.FoodToSend > 0 &&
            order.HorsesToSend > 0 && order.WoodToSend > 0 && order.MetalToSend > 0 && order.StoneToSend > 0,
            "AI schedules multi-city logistics without the old 800-troop gap",
            $"target={order?.TargetCityId}, route={string.Join('>', order?.RouteCityIds ?? new List<int>())}, troops={order?.TroopsToSend}, gold={order?.GoldToSend}, food={order?.FoodToSend}, raw={order?.WoodToSend}/{order?.MetalToSend}/{order?.StoneToSend}");
    }

    private static void RunTestCityOwnerSwitchTest()
    {
        var world = TestHelpers.World();
        var city = TestHelpers.City(21, "Relay", 1, 0, 0, 0, Array.Empty<int>(), Array.Empty<int>());
        var aiCity = TestHelpers.City(22, "AiCity", 2, 0, 0, 0, Array.Empty<int>(), Array.Empty<int>());
        world.Cities.AddRange(new[] { city, aiCity });
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 0, Array.Empty<int>()));
        world.InternalAffairsSchedules.Add(new InternalAffairsScheduleData { Id = 1, CityId = city.Id, State = InternalAffairsScheduleState.Active });

        var switched = TestCityOwnershipRules.TrySwitchToNextActiveFaction(world, city, out var previousFactionId, out var nextFactionId);
        Assert(
            switched && previousFactionId == 1 && nextFactionId == 2 && city.OwnerFactionId == 2 && world.InternalAffairsSchedules.Count == 0,
            "Test city ownership switch cycles to the next active faction and clears city schedules",
            $"switched={switched}, owner={city.OwnerFactionId}, previous={previousFactionId}, next={nextFactionId}, schedules={world.InternalAffairsSchedules.Count}");
    }

    private static void RunCoreActionsTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(2, "CoreCity", 2, 500, 500, 1500, new[] { 201, 202, 203 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(201, "A1", 2, 70, 80, 75, 70));
        world.Officers.Add(TestHelpers.Officer(202, "A2", 2, 65, 88, 60, 68));
        world.Officers.Add(TestHelpers.Officer(203, "A3", 2, 60, 70, 85, 72));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201, 202, 203 }));
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(2, 2);

        var recruitPending = world.PendingCommands.Count(x => x.Type == CommandType.Recruit);
        var internalAffairsCount = world.InternalAffairsSchedules.Count(x => x.State == InternalAffairsScheduleState.Active);
        var searchPending = world.PendingCommands.Count(x => x.Type == CommandType.Search);
        var city = world.GetCity(2)!;
        Assert(recruitPending == 1, "AI recruit scheduling", $"pending={recruitPending}");
        Assert(internalAffairsCount == 1, "AI internal affairs scheduling", $"active={internalAffairsCount}");
        Assert(searchPending == 1, "AI search scheduling", $"pending={searchPending}");
        Assert(city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month, "AI search marked used", $"lastSearch={city.LastSearchYear}/{city.LastSearchMonth}");
        // Search resolves at month end, and internal affairs does not consume resources immediately.
        var expectedGold = 500 - RecruitRules.GetRecruitGoldCost(TroopType.Infantry, 200);
        var expectedFood = 500 - RecruitRules.GetRecruitFoodCost(TroopType.Infantry, 200);
        Assert(city.Gold == expectedGold && city.Food == expectedFood, "AI core action immediate costs", $"gold={city.Gold}/{expectedGold}, food={city.Food}/{expectedFood}");
    }

    private static void RunMainCityRulesTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(4, "Fallback", 1, 0, 0, 0, Array.Empty<int>(), Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(8, "RulerCity", 1, 0, 0, 0, new[] { 101 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Ruler", 8));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        Assert(MainCityRules.FindMainCity(world, 1)?.Id == 8, "Main city follows ruler", "ruler present");
        world.GetOfficer(101)!.CaptiveFactionId = 2;
        Assert(MainCityRules.FindMainCity(world, 1)?.Id == 4, "Main city falls back when ruler captive", "captive");
        world.GetCity(4)!.OwnerFactionId = 2;
        world.GetCity(8)!.OwnerFactionId = 2;
        Assert(MainCityRules.FindMainCity(world, 1) == null, "Main city unavailable without owned cities", "no city");
    }

    private static void RunSeasonalGoldTest()
    {
        var world = TestHelpers.World(month: 4);
        world.Cities.Add(TestHelpers.City(2, "AiGoldCity", 2, 1000, 1000, 1200, new[] { 201 }, Array.Empty<int>()));
        world.GetCity(2)!.DisasterPrevention = 120;
        world.Officers.Add(TestHelpers.Officer(201, "A1", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        world.RandomSeed = FindSeedForWorldWithoutMonthlyEvents(world);
        var services = CreateServices(world);

        var result = services.Turn.ApplyMonthlyEconomy();
        var city = world.GetCity(2)!;

        Assert(result.AnnualGoldCollected == 1872, "AI seasonal gold total", $"annualGold={result.AnnualGoldCollected}");
        Assert(city.Gold == 2872, "AI seasonal gold applied", $"gold={city.Gold}");
    }

    private static void RunSeasonalFoodTest()
    {
        var world = TestHelpers.World(month: 8);
        world.Cities.Add(TestHelpers.City(2, "AiFoodCity", 2, 1000, 1000, 2000, new[] { 201 }, Array.Empty<int>()));
        world.GetCity(2)!.DisasterPrevention = 120;
        world.Officers.Add(TestHelpers.Officer(201, "A1", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        world.RandomSeed = FindSeedForWorldWithoutMonthlyEvents(world);
        var services = CreateServices(world);

        var result = services.Turn.ApplyMonthlyEconomy();
        var city = world.GetCity(2)!;

        Assert(result.AnnualFoodCollected == 2736, "AI seasonal food total", $"annualFood={result.AnnualFoodCollected}");
        Assert(city.Food == 3686, "AI seasonal food applied with upkeep", $"food={city.Food}");
    }

    private static void RunResourceDepotStorageTest()
    {
        var world = TestHelpers.World(month: 2);
        var playerCity = TestHelpers.City(1, "PlayerCity", 1, 1000, 12000, 0, Array.Empty<int>(), Array.Empty<int>());
        var aiCity = TestHelpers.City(2, "AiCity", 2, 1000, 1000, 0, Array.Empty<int>(), Array.Empty<int>());
        playerCity.GranaryLevel = 1;
        playerCity.HorseStableLevel = 1;
        playerCity.Horses = 600;
        playerCity.Metal = 1200;
        playerCity.Wood = 1200;
        playerCity.Stone = 1200;
        aiCity.Metal = 1200;
        aiCity.Wood = 1200;
        aiCity.Stone = 1200;
        aiCity.ResourceDepotLevel = 1;
        world.Cities.Add(playerCity);
        world.Cities.Add(aiCity);
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 0, Array.Empty<int>()));
        world.RandomSeed = FindSeedForWorldWithoutMonthlyEvents(world);

        var result = CreateServices(world).Turn.ApplyMonthlyEconomy();
        var playerReport = result.PlayerCityEconomyReports.SingleOrDefault(report => report.CityId == playerCity.Id);
        var aiDepotChosen = AiConstructionRules.ChooseConstructionProjectType(world, aiCity) == ConstructionProjectType.ResourceDepot;
        var granaryCity = new CityData { GranaryLevel = 4, Food = 19000, Wood = 100, Stone = 100 };
        var horseStableCity = new CityData { HorseStableLevel = 4, Horses = 750, Wood = 100, Stone = 100 };
        var pastureCity = new CityData { BowWorkshopLevel = 1, SiegeWorkshopLevel = 3, HorsePastureLevel = 4, Wood = 100, Metal = 100, Stone = 100 };
        var workshopCity = new CityData
        {
            Id = 4,
            OwnerFactionId = 1,
            BowWorkshopLevel = 1,
            SiegeWorkshopLevel = 2,
            HorsePastureLevel = 5,
            GranaryLevel = 5,
            HorseStableLevel = 5,
            ResourceDepotLevel = 5,
            Wood = 100,
            Metal = 100,
            Stone = 100,
            ConnectedCityIds = new List<int> { 5 }
        };
        var enemyCity = new CityData { Id = 5, OwnerFactionId = 2 };
        world.Cities.Add(workshopCity);
        world.Cities.Add(enemyCity);
        var aiGranaryChosen = AiConstructionRules.ChooseConstructionProjectType(world, granaryCity) == ConstructionProjectType.Granary;
        var aiHorseStableChosen = AiConstructionRules.ChooseConstructionProjectType(world, horseStableCity) == ConstructionProjectType.HorseStable;
        var aiPastureChosen = AiConstructionRules.ChooseConstructionProjectType(world, pastureCity) == ConstructionProjectType.HorsePasture;
        var aiWorkshopChosen = AiConstructionRules.ChooseConstructionProjectType(world, workshopCity) == ConstructionProjectType.SiegeWorkshop;
        var strategist = new OfficerData { Id = 901, CityId = playerCity.Id, Intelligence = 90 };
        world.Officers.Add(strategist);
        var playerFaction = world.GetFaction(1)!;
        playerFaction.OfficerIds.Add(strategist.Id);
        playerFaction.ChiefStrategistOfficerId = strategist.Id;
        var plannedSupplyCity = new CityData { Food = 2500, Wood = 100, Stone = 100 };
        var strategistPlan = AiConstructionRules.ChooseCityDevelopmentPlan(world, plannedSupplyCity, playerFaction);
        var strategistProject = AiConstructionRules.ChooseConstructionProjectType(world, plannedSupplyCity, strategistPlan);
        var basicPlan = AiConstructionRules.ChooseCityDevelopmentPlan(world, plannedSupplyCity, world.GetFaction(2));

        var productionCity = TestHelpers.City(6, "WorkshopCity", 1, 1000, 1000, 3000, new[] { 902 }, new[] { 7 });
        productionCity.SiegeWorkshopLevel = 1;
        productionCity.Wood = 100;
        productionCity.Metal = 100;
        productionCity.Stone = 100;
        var frontlineCity = TestHelpers.City(7, "FrontlineCity", 1, 1000, 1000, 500, Array.Empty<int>(), new[] { 6, 8 });
        var enemyFrontierCity = TestHelpers.City(8, "EnemyFrontier", 2, 1000, 1000, 500, Array.Empty<int>(), new[] { 7 });
        enemyFrontierCity.Defense = 85;
        var workshopGovernor = new OfficerData { Id = 902, CityId = productionCity.Id, Intelligence = 60, Politics = 90, Appointments = new List<string> { OfficerAppointmentRules.Governor } };
        var chancellor = new OfficerData { Id = 903, CityId = playerCity.Id, Intelligence = 70, Politics = 90 };
        world.Cities.AddRange(new[] { productionCity, frontlineCity, enemyFrontierCity });
        world.Officers.AddRange(new[] { workshopGovernor, chancellor });
        playerFaction.OfficerIds.AddRange(new[] { workshopGovernor.Id, chancellor.Id });
        playerFaction.ChancellorOfficerId = chancellor.Id;
        var equipmentPlan = AiConstructionRules.ChooseSiegeEquipmentPlan(world, productionCity, playerFaction);
        var logisticsServices = CreateServices(world);
        var manufacturingResult = logisticsServices.Ai.RunSingleCityDecision(playerFaction.Id, productionCity.Id);
        var manufacturingScheduled = world.InternalAffairsSchedules.Any(schedule =>
            schedule.CityId == productionCity.Id &&
            schedule.JobType == InternalAffairsJobType.Manufacturing &&
            schedule.ConstructionProjectType == ConstructionProjectType.Ram);
        productionCity.RamCount = 1;
        var deliveryResult = logisticsServices.Ai.RunSingleCityDecision(playerFaction.Id, productionCity.Id);
        var deliveryScheduled = world.PendingCommands.Any(command =>
            command.Type == CommandType.Move && command.SourceCityId == productionCity.Id && command.TargetCityId == frontlineCity.Id &&
            command.SiegeEngineAllocation.Ram == 1 && command.WoodToSend == 0 && command.MetalToSend == 0 && command.StoneToSend == 0);

        var materialCity = TestHelpers.City(12, "MaterialWorkshop", 1, 1000, 1000, 3000, new[] { 904 }, new[] { 13 });
        materialCity.SiegeWorkshopLevel = 1;
        materialCity.Wood = 40;
        materialCity.ResourceDeposits.Add(new CityResourceDepositData
        {
            Type = StrategicResourceType.Metal,
            MonthlyYield = 30,
            RemainingReserve = 300
        });
        var materialFrontline = TestHelpers.City(13, "MaterialFrontline", 1, 1000, 1000, 500, Array.Empty<int>(), new[] { 12, 14 });
        var materialEnemy = TestHelpers.City(14, "MaterialEnemy", 2, 1000, 1000, 500, Array.Empty<int>(), new[] { 13 });
        materialEnemy.Defense = 85;
        var materialOfficer = new OfficerData { Id = 904, CityId = materialCity.Id, Intelligence = 70, Politics = 80 };
        world.Cities.AddRange(new[] { materialCity, materialFrontline, materialEnemy });
        world.Officers.Add(materialOfficer);
        playerFaction.OfficerIds.Add(materialOfficer.Id);
        _ = logisticsServices.Ai.RunSingleCityDecision(playerFaction.Id, materialCity.Id);
        var materialExtractionScheduled = world.InternalAffairsSchedules.Any(schedule =>
            schedule.CityId == materialCity.Id && schedule.JobType == InternalAffairsJobType.Extraction && schedule.RemainingMonths == 1);

        var poorWorkshop = TestHelpers.City(15, "PoorWorkshop", 1, 1000, 1000, 3000, Array.Empty<int>(), new[] { 17 });
        poorWorkshop.SiegeWorkshopLevel = 1;
        poorWorkshop.Wood = 40;
        var richWorkshop = TestHelpers.City(16, "RichWorkshop", 1, 1000, 1000, 3000, Array.Empty<int>(), new[] { 17 });
        richWorkshop.SiegeWorkshopLevel = 2;
        richWorkshop.Wood = 100;
        richWorkshop.Metal = 100;
        var coordinatedFrontline = TestHelpers.City(17, "CoordinatedFrontline", 1, 1000, 1000, 500, Array.Empty<int>(), new[] { 15, 16, 18 });
        var coordinatedEnemy = TestHelpers.City(18, "CoordinatedEnemy", 2, 1000, 1000, 500, Array.Empty<int>(), new[] { 17 });
        coordinatedEnemy.Defense = 85;
        world.Cities.AddRange(new[] { poorWorkshop, richWorkshop, coordinatedFrontline, coordinatedEnemy });
        var poorWorkshopPlan = AiConstructionRules.ChooseSiegeEquipmentPlan(world, poorWorkshop, playerFaction);
        var richWorkshopPlan = AiConstructionRules.ChooseSiegeEquipmentPlan(world, richWorkshop, playerFaction);
        var coordinatedProduction = !poorWorkshopPlan.IsValid && richWorkshopPlan.IsValid &&
                                    richWorkshopPlan.TargetCityId == coordinatedFrontline.Id && richWorkshopPlan.ProjectType == ConstructionProjectType.Ram;

        var horseWorld = TestHelpers.World(month: 1);
        var horseCity = TestHelpers.City(3, "HorseCity", 1, 1000, 1000, 0, Array.Empty<int>(), Array.Empty<int>());
        horseCity.Horses = 399;
        horseCity.HorseStableLevel = 1;
        horseWorld.Cities.Add(horseCity);
        horseWorld.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        horseWorld.RandomSeed = FindSeedForWorldWithoutMonthlyEvents(horseWorld);
        var horseResult = CreateServices(horseWorld).Turn.ApplyMonthlyEconomy();
        var constrainedBirths = horseResult.PlayerCityHorseBirths.SingleOrDefault(entry => entry.CityId == horseCity.Id).Amount;

        Assert(
            playerCity.Food == 10000 && playerCity.Horses == 400 && playerCity.Metal == 500 && playerCity.Wood == 500 && playerCity.Stone == 500 &&
            aiCity.Metal == 1000 && aiCity.Wood == 1000 && aiCity.Stone == 1000 &&
            playerReport != null &&
            playerReport.FoodCapacity == 10000 && playerReport.HorseCapacity == 400 && playerReport.MetalCapacity == 500 && playerReport.WoodCapacity == 500 && playerReport.StoneCapacity == 500 &&
            playerReport.StorageLoss.Food == 2000 && playerReport.StorageLoss.Horse == 200 && playerReport.StorageLoss.Metal == 700 && playerReport.StorageLoss.Wood == 700 && playerReport.StorageLoss.Stone == 700 &&
            aiDepotChosen && aiGranaryChosen && aiHorseStableChosen && aiPastureChosen && aiWorkshopChosen &&
            strategistPlan == AiCityDevelopmentPlan.Supply && strategistProject == ConstructionProjectType.Granary && basicPlan == AiCityDevelopmentPlan.None &&
            equipmentPlan.IsValid && equipmentPlan.TargetCityId == frontlineCity.Id && equipmentPlan.ProjectType == ConstructionProjectType.Ram &&
            manufacturingResult.Success && manufacturingScheduled && deliveryResult.Success && deliveryScheduled &&
            materialExtractionScheduled && coordinatedProduction &&
            horseCity.Horses == 400 && constrainedBirths == 1,
            "Fixed storage capacity, shared overflow loss, stable-limited horse births, AI facility upgrades, strategist plans, and siege-equipment logistics",
            $"player food/horse={playerCity.Food}/{playerCity.Horses}, raw={playerCity.Metal}/{playerCity.Wood}/{playerCity.Stone}, ai={aiCity.Metal}/{aiCity.Wood}/{aiCity.Stone}, choices=depot:{aiDepotChosen}/granary:{aiGranaryChosen}/stable:{aiHorseStableChosen}/pasture:{aiPastureChosen}/workshop:{aiWorkshopChosen}, plans=strategist:{strategistPlan}/{strategistProject}, equipment:{equipmentPlan.ProjectType}->{equipmentPlan.TargetCityId}/manufacturing:{manufacturingScheduled}/delivery:{deliveryScheduled}/extraction:{materialExtractionScheduled}/coordinated:{coordinatedProduction}, basic:{basicPlan}, births={constrainedBirths}, loss={playerReport?.StorageLoss}");
    }

    private static void RunResourceGatedConstructionTest()
    {
        var city = new CityData { Wood = 34, Metal = 15, Stone = 3 };
        var blocked = ConstructionRules.ApplyProgress(city, ConstructionProjectType.BowWorkshop, 100);
        var defenseGain = ConstructionRules.ApplyDefenseRepair(city, 4);
        city.Wood = 1;
        city.Stone = 3;
        var preventionGain = ConstructionRules.ApplyDisasterPrevention(city, 4);

        Assert(
            blocked.ValuesGained == 0 && blocked.CurrentProgress == 100 && blocked.WaitingForMaterials &&
            city.BowWorkshopLevel == 0 && defenseGain == 1 && preventionGain == 1 &&
            city.Wood >= 0 && city.Metal >= 0 && city.Stone >= 0,
            "Construction holds progress for materials and repairs never overspend",
            $"level={city.BowWorkshopLevel}, progress={blocked.CurrentProgress}, waiting={blocked.WaitingForMaterials}, defenseGain={defenseGain}, preventionGain={preventionGain}, wood/metal/stone={city.Wood}/{city.Metal}/{city.Stone}");
    }

    private static void RunConstructionAndManufacturingScheduleTest()
    {
        var world = TestHelpers.World(month: 2);
        var city = TestHelpers.City(1, "WorkshopCity", 1, 1000, 1000, 1000, new[] { 101, 102 }, Array.Empty<int>());
        city.SiegeWorkshopLevel = 1;
        city.Wood = 100;
        city.Metal = 100;
        city.Stone = 100;
        world.Cities.Add(city);
        world.Officers.Add(TestHelpers.Officer(101, "Builder", 1, intelligence: 80, charm: 70));
        world.Officers.Add(TestHelpers.Officer(102, "Maker", 1, intelligence: 75, charm: 65));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        var services = CreateServices(world);

        var build = services.Resolver.ScheduleInternalAffairs(1, city.Id, 101, InternalAffairsJobType.Construction, 1, ConstructionProjectType.BowWorkshop);
        var manufacture = services.Resolver.ScheduleInternalAffairs(1, city.Id, 102, InternalAffairsJobType.Manufacturing, 1, ConstructionProjectType.Ram);
        var constructionScheduled = world.InternalAffairsSchedules.Any(schedule =>
            schedule.JobType == InternalAffairsJobType.Construction &&
            schedule.ConstructionProjectType == ConstructionProjectType.BowWorkshop);
        var manufacturingScheduled = world.InternalAffairsSchedules.Any(schedule =>
            schedule.JobType == InternalAffairsJobType.Manufacturing &&
            schedule.ConstructionProjectType == ConstructionProjectType.Ram);
        var results = services.Turn.ResolvePendingCommands(services.Resolver);
        var repairCity = new CityData { SiegeWorkshopLevel = 2, Stone = 6 };
        var repairGain = ConstructionRules.ApplyDefenseRepair(repairCity, 1);
        var preventionCity = new CityData { SiegeWorkshopLevel = 2, Wood = 3, Stone = 6 };
        var preventionGain = ConstructionRules.ApplyDisasterPrevention(preventionCity, 1);
        var maximumWorkshopCity = new CityData { SiegeWorkshopLevel = 2, Wood = 200, Metal = 200, Stone = 200 };
        var levelUpToMaximum = ConstructionRules.ApplyProgress(maximumWorkshopCity, ConstructionProjectType.SiegeWorkshop, 1000);
        var levelUpPastMaximum = ConstructionRules.ApplyProgress(maximumWorkshopCity, ConstructionProjectType.SiegeWorkshop, 1000);
        var maximumGranaryCity = new CityData { GranaryLevel = 4, Wood = 100, Stone = 100 };
        var levelUpGranaryToMaximum = ConstructionRules.ApplyProgress(maximumGranaryCity, ConstructionProjectType.Granary, 1000);
        var levelUpGranaryPastMaximum = ConstructionRules.ApplyProgress(maximumGranaryCity, ConstructionProjectType.Granary, 1000);
        var maximumBowWorkshopCity = new CityData { Wood = 100, Metal = 100 };
        var levelUpBowWorkshopToMaximum = ConstructionRules.ApplyProgress(maximumBowWorkshopCity, ConstructionProjectType.BowWorkshop, 1000);
        var levelUpBowWorkshopPastMaximum = ConstructionRules.ApplyProgress(maximumBowWorkshopCity, ConstructionProjectType.BowWorkshop, 1000);
        var maximumHorsePastureCity = new CityData { HorsePastureLevel = 4, Wood = 100, Stone = 100 };
        var levelUpHorsePastureToMaximum = ConstructionRules.ApplyProgress(maximumHorsePastureCity, ConstructionProjectType.HorsePasture, 1000);
        var levelUpHorsePasturePastMaximum = ConstructionRules.ApplyProgress(maximumHorsePastureCity, ConstructionProjectType.HorsePasture, 1000);

        Assert(
            build.Success && manufacture.Success && results.Count >= 2 &&
            constructionScheduled && manufacturingScheduled &&
            city.BowWorkshopProgress > 0 && city.RamProgress > city.BowWorkshopProgress &&
            repairGain == 3 && repairCity.Stone == 0 &&
            preventionGain == 3 && preventionCity.Wood == 0 && preventionCity.Stone == 0 &&
            ConstructionRules.GetSiegeWorkshopDisasterPreventionBonus(4) == ConstructionRules.MaximumSiegeWorkshopLevel &&
            levelUpToMaximum.ValuesGained == 1 && levelUpToMaximum.CurrentValue == ConstructionRules.MaximumSiegeWorkshopLevel && levelUpToMaximum.CurrentProgress == 0 &&
            levelUpPastMaximum.ValuesGained == 0 && ConstructionRules.IsAtMaximumLevel(maximumWorkshopCity, ConstructionProjectType.SiegeWorkshop) &&
            levelUpGranaryToMaximum.ValuesGained == 1 && levelUpGranaryToMaximum.CurrentValue == ConstructionRules.MaximumStorageFacilityLevel && levelUpGranaryToMaximum.CurrentProgress == 0 &&
            levelUpGranaryPastMaximum.ValuesGained == 0 && ConstructionRules.IsAtMaximumLevel(maximumGranaryCity, ConstructionProjectType.Granary) &&
            levelUpBowWorkshopToMaximum.ValuesGained == 1 && levelUpBowWorkshopToMaximum.CurrentValue == ConstructionRules.MaximumBowWorkshopLevel && levelUpBowWorkshopPastMaximum.ValuesGained == 0 &&
            levelUpHorsePastureToMaximum.ValuesGained == 1 && levelUpHorsePastureToMaximum.CurrentValue == ConstructionRules.MaximumHorsePastureLevel && levelUpHorsePasturePastMaximum.ValuesGained == 0 &&
            ConstructionRules.IsAtMaximumLevel(maximumBowWorkshopCity, ConstructionProjectType.BowWorkshop) && ConstructionRules.IsAtMaximumLevel(maximumHorsePastureCity, ConstructionProjectType.HorsePasture),
            "Facilities stop at their configured maximum levels",
            $"build={build.Success}, manufacture={manufacture.Success}, scheduled={constructionScheduled}/{manufacturingScheduled}, bow/ramProgress={city.BowWorkshopProgress}/{city.RamProgress}, repair={repairGain}, prevention={preventionGain}, workshop={maximumWorkshopCity.SiegeWorkshopLevel}/{ConstructionRules.MaximumSiegeWorkshopLevel}, granary={maximumGranaryCity.GranaryLevel}/{ConstructionRules.MaximumStorageFacilityLevel}, bow={maximumBowWorkshopCity.BowWorkshopLevel}/{ConstructionRules.MaximumBowWorkshopLevel}, pasture={maximumHorsePastureCity.HorsePastureLevel}/{ConstructionRules.MaximumHorsePastureLevel}, results={results.Count}");
    }

    private static void RunMarketMonthlySnapshotAndTradeTest()
    {
        var city = TestHelpers.City(1, "MarketCity", 1, 2000, 1000, 0, Array.Empty<int>(), Array.Empty<int>());
        city.HasMerchant = true;
        MarketRules.EnsureMarketInitialized(city);
        MarketRules.SetMerchantStock(city, MarketProductType.Food, 100);
        var expensivePrice = MarketRules.GetBuyUnitPrice(city, MarketProductType.Food);
        city.LastFoodBuyPrice = expensivePrice;
        MarketRules.RefreshMonthlyMarket(city);
        var refreshedPrice = MarketRules.GetBuyUnitPrice(city, MarketProductType.Food);

        var world = TestHelpers.World();
        world.Cities.Add(city);
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        var resolver = CreateServices(world).Resolver;
        var buy = resolver.Execute(new CommandRequest
        {
            Type = CommandType.Merchant,
            ActorFactionId = 1,
            SourceCityId = city.Id,
            FoodToSend = 100,
            MerchantTradeMode = MerchantTradeMode.BuyFood
        });
        var sell = resolver.Execute(new CommandRequest
        {
            Type = CommandType.Merchant,
            ActorFactionId = 1,
            SourceCityId = city.Id,
            FoodToSend = 100,
            MerchantTradeMode = MerchantTradeMode.SellFood
        });

        Assert(
            city.PreviousFoodBuyPrice == expensivePrice && refreshedPrice < expensivePrice &&
            buy.Success && sell.Success && city.Food == 1000 && city.Gold > 0,
            "Monthly market snapshot and buy/sell use the shared market rules",
            $"previous/current={city.PreviousFoodBuyPrice}/{refreshedPrice}, buy={buy.Success}, sell={sell.Success}, food={city.Food}, gold={city.Gold}");
    }

    private static void RunMonthlyAdvisorRetrospectiveTest()
    {
        MonthlyAdvisorRetrospectiveSummary? Select(MonthlyCityEconomyReport report, MonthlyCityEvent? cityEvent = null)
        {
            var result = new MonthlyEconomyResult();
            result.PlayerCityEconomyReports.Add(report);
            if (cityEvent != null)
            {
                result.PlayerCityEvents.Add(cityEvent);
            }
            return MonthlyAdvisorRetrospectiveRules.SelectPriority(result);
        }

        var storage = Select(new MonthlyCityEconomyReport { CityId = 1, FoodCapacity = 100, FoodAmount = 90, StorageLoss = new MarketStorageLoss(10, 0, 0, 0, 0) });
        var cityEvent = Select(new MonthlyCityEconomyReport { CityId = 2, FoodCapacity = 100, FoodAmount = 90 }, new MonthlyCityEvent { CityId = 2, FoodDelta = -10 });
        var food = Select(new MonthlyCityEconomyReport { CityId = 3, FoodCapacity = 100, FoodAmount = 20, FoodDelta = -10, FoodUpkeep = 5 });
        var resource = Select(new MonthlyCityEconomyReport { CityId = 4, FoodCapacity = 100, FoodAmount = 90, MetalDelta = 25 });
        var stable = Select(new MonthlyCityEconomyReport { CityId = 5, FoodCapacity = 100, FoodAmount = 90 });

        Assert(
            storage?.Focus == MonthlyAdvisorRetrospectiveFocus.StorageLoss &&
            cityEvent?.Focus == MonthlyAdvisorRetrospectiveFocus.CityEvent &&
            food?.Focus == MonthlyAdvisorRetrospectiveFocus.FoodPressure &&
            resource?.Focus == MonthlyAdvisorRetrospectiveFocus.ResourceOpportunity &&
            stable?.Focus == MonthlyAdvisorRetrospectiveFocus.Stable,
            "Monthly advisor retrospective prioritizes the most actionable causal outcome",
            $"storage={storage?.Focus}, event={cityEvent?.Focus}, food={food?.Focus}, resource={resource?.Focus}, stable={stable?.Focus}");
    }

    private static void RunMonthlyAdvisorSpeakerSelectionTest()
    {
        var world = TestHelpers.World();
        var ruler = TestHelpers.Officer(101, "Ruler", 1, intelligence: 70);
        var chief = TestHelpers.Officer(102, "Chief", 1, intelligence: 80);
        var strategist = TestHelpers.Officer(103, "Strategist", 1, intelligence: 85);
        strategist.Appointments.Add(OfficerAppointmentRules.Strategist);
        var substitute = TestHelpers.Officer(104, "Substitute", 1, intelligence: 90);
        ruler.Politics = 70;
        chief.Politics = 70;
        strategist.Politics = 75;
        substitute.Politics = 80;
        world.Officers.AddRange(new[] { ruler, chief, strategist, substitute });
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, ruler.Id, new[] { ruler.Id, chief.Id, strategist.Id, substitute.Id }));
        var faction = world.GetFaction(1)!;
        faction.ChiefStrategistOfficerId = chief.Id;

        var chiefSelection = MonthlyAdvisorSpeakerRules.Select(world, faction.Id);
        faction.ChiefStrategistOfficerId = 0;
        var strategistSelection = MonthlyAdvisorSpeakerRules.Select(world, faction.Id);
        strategist.Appointments.Clear();
        var officerSelection = MonthlyAdvisorSpeakerRules.Select(world, faction.Id);

        Assert(
            chiefSelection.OfficerId == chief.Id && chiefSelection.Role == MonthlyAdvisorSpeakerRole.ChiefStrategist &&
            strategistSelection.OfficerId == strategist.Id && strategistSelection.Role == MonthlyAdvisorSpeakerRole.Strategist &&
            officerSelection.OfficerId == substitute.Id && officerSelection.Role == MonthlyAdvisorSpeakerRole.Officer,
            "Monthly advisor speaker labels chief strategist, strategist, and suitable officer correctly",
            $"chief={chiefSelection.OfficerId}/{chiefSelection.Role}, strategist={strategistSelection.OfficerId}/{strategistSelection.Role}, officer={officerSelection.OfficerId}/{officerSelection.Role}");
    }

    private static void RunMonthlyCityEventsTest()
    {
        foreach (var eventType in Enum.GetValues<MonthlyCityEventType>())
        {
            var seed = FindSeedForMonthlyCityEvent(eventType);
            Assert(seed > 0, $"Monthly city event seed found for {eventType}", $"seed={seed}");
            if (seed <= 0)
            {
                continue;
            }

            var world = CreateMonthlyEventTestWorldForEventType(eventType, seed, disasterPrevention: 0);
            var services = CreateServices(world);

            var result = services.Turn.ApplyMonthlyEconomy();
            var cityEvent = result.PlayerCityEvents.SingleOrDefault();
            Assert(cityEvent != null, $"Monthly city event resolves for {eventType}", $"count={result.PlayerCityEvents.Count}");
            Assert(cityEvent != null && cityEvent.EventType == eventType, $"Monthly city event type matches {eventType}", cityEvent == null ? "event=null" : $"type={cityEvent.EventType}");
            if (cityEvent == null)
            {
                continue;
            }

            switch (eventType)
            {
                case MonthlyCityEventType.Flooding:
                    Assert(cityEvent.FoodDelta < 0 && cityEvent.FarmDelta < 0 && cityEvent.PopulationDelta < 0 && cityEvent.TroopDelta < 0, "Flooding lowers food farm population and troops", $"food={cityEvent.FoodDelta}, farm={cityEvent.FarmDelta}, population={cityEvent.PopulationDelta}, troops={cityEvent.TroopDelta}");
                    break;
                case MonthlyCityEventType.Drought:
                    Assert(cityEvent.FoodDelta < 0 && cityEvent.FarmDelta < 0 && cityEvent.PopulationDelta < 0 && cityEvent.TroopDelta < 0, "Drought lowers food farm population and troops", $"food={cityEvent.FoodDelta}, farm={cityEvent.FarmDelta}, population={cityEvent.PopulationDelta}, troops={cityEvent.TroopDelta}");
                    break;
                case MonthlyCityEventType.Earthquake:
                    Assert(cityEvent.GoldDelta < 0 && cityEvent.DefenseDelta < 0 && cityEvent.PopulationDelta < 0 && cityEvent.TroopDelta < 0, "Earthquake lowers gold defense population and troops", $"gold={cityEvent.GoldDelta}, defense={cityEvent.DefenseDelta}, population={cityEvent.PopulationDelta}, troops={cityEvent.TroopDelta}");
                    break;
                case MonthlyCityEventType.InsectDisaster:
                    Assert(cityEvent.FoodDelta < 0 && cityEvent.FarmDelta < 0, "Insect disaster lowers food and farm", $"food={cityEvent.FoodDelta}, farm={cityEvent.FarmDelta}");
                    break;
                case MonthlyCityEventType.Plague:
                    Assert(cityEvent.FoodDelta < 0 && cityEvent.LoyaltyDelta < 0 && cityEvent.PopulationDelta < 0 && cityEvent.TroopDelta < 0, "Plague lowers food loyalty population and troops", $"food={cityEvent.FoodDelta}, loyalty={cityEvent.LoyaltyDelta}, population={cityEvent.PopulationDelta}, troops={cityEvent.TroopDelta}");
                    break;
                case MonthlyCityEventType.Rebellion:
                    Assert(cityEvent.GoldDelta < 0 && cityEvent.LoyaltyDelta < 0, "Rebellion lowers gold and loyalty", $"gold={cityEvent.GoldDelta}, loyalty={cityEvent.LoyaltyDelta}");
                    break;
                case MonthlyCityEventType.Bandit:
                    Assert(cityEvent.GoldDelta < 0 && cityEvent.FoodDelta < 0, "Bandit lowers gold and food", $"gold={cityEvent.GoldDelta}, food={cityEvent.FoodDelta}");
                    break;
                case MonthlyCityEventType.Snow:
                    Assert(cityEvent.FoodDelta < 0 && cityEvent.DefenseDelta < 0 && cityEvent.PopulationDelta < 0 && cityEvent.TroopDelta < 0, "Snow lowers food defense population and troops", $"food={cityEvent.FoodDelta}, defense={cityEvent.DefenseDelta}, population={cityEvent.PopulationDelta}, troops={cityEvent.TroopDelta}");
                    break;
                case MonthlyCityEventType.Typhoon:
                    Assert(cityEvent.FoodDelta < 0 && cityEvent.FarmDelta < 0 && cityEvent.DefenseDelta < 0 && cityEvent.PopulationDelta < 0 && cityEvent.TroopDelta < 0, "Typhoon lowers food farm defense population and troops", $"food={cityEvent.FoodDelta}, farm={cityEvent.FarmDelta}, defense={cityEvent.DefenseDelta}, population={cityEvent.PopulationDelta}, troops={cityEvent.TroopDelta}");
                    break;
                case MonthlyCityEventType.BumperHarvest:
                    Assert(cityEvent.FoodDelta > 0 && cityEvent.LoyaltyDelta > 0 && cityEvent.PopulationDelta == 0 && cityEvent.TroopDelta == 0, "Bumper harvest raises food and loyalty only", $"food={cityEvent.FoodDelta}, loyalty={cityEvent.LoyaltyDelta}, population={cityEvent.PopulationDelta}, troops={cityEvent.TroopDelta}");
                    break;
                case MonthlyCityEventType.Fire:
                    Assert(cityEvent.GoldDelta < 0 && cityEvent.DefenseDelta < 0 && cityEvent.PopulationDelta < 0 && cityEvent.TroopDelta < 0, "Fire lowers gold defense population and troops", $"gold={cityEvent.GoldDelta}, defense={cityEvent.DefenseDelta}, population={cityEvent.PopulationDelta}, troops={cityEvent.TroopDelta}");
                    break;
            }
        }
    }

    private static void RunMonthlyCityEventSeasonRulesTest()
    {
        Assert(!CanMonthlyCityEventOccur(MonthlyCityEventType.Flooding, 5), "Flooding blocked outside rainy season", "month=5");
        Assert(CanMonthlyCityEventOccur(MonthlyCityEventType.Flooding, 6), "Flooding allowed in rainy season", "month=6");
        Assert(!CanMonthlyCityEventOccur(MonthlyCityEventType.Fire, 8), "Fire blocked outside dry season", "month=8");
        Assert(CanMonthlyCityEventOccur(MonthlyCityEventType.Fire, 11), "Fire allowed in dry season", "month=11");
        Assert(!CanMonthlyCityEventOccur(MonthlyCityEventType.BumperHarvest, 7), "Bumper harvest blocked before harvest season", "month=7");
        Assert(CanMonthlyCityEventOccur(MonthlyCityEventType.BumperHarvest, 9), "Bumper harvest allowed in harvest season", "month=9");
        Assert(!CanMonthlyCityEventOccur(MonthlyCityEventType.InsectDisaster, 8), "Insect disaster blocked outside insect season", "month=8");
        Assert(CanMonthlyCityEventOccur(MonthlyCityEventType.InsectDisaster, 5), "Insect disaster allowed in insect season", "month=5");
        Assert(!CanMonthlyCityEventOccur(MonthlyCityEventType.Drought, 9), "Drought blocked outside drought season", "month=9");
        Assert(CanMonthlyCityEventOccur(MonthlyCityEventType.Drought, 6), "Drought allowed in drought season", "month=6");
        Assert(!CanMonthlyCityEventOccur(MonthlyCityEventType.Plague, 10), "Plague blocked outside plague season", "month=10");
        Assert(CanMonthlyCityEventOccur(MonthlyCityEventType.Plague, 12), "Plague allowed in plague season", "month=12");
        Assert(!CanMonthlyCityEventOccur(MonthlyCityEventType.Snow, 11), "Snow blocked outside snow season", "month=11");
        Assert(CanMonthlyCityEventOccur(MonthlyCityEventType.Snow, 12), "Snow allowed in snow season", "month=12");
        Assert(!CanMonthlyCityEventOccur(MonthlyCityEventType.Typhoon, 8), "Typhoon blocked outside typhoon season", "month=8");
        Assert(CanMonthlyCityEventOccur(MonthlyCityEventType.Typhoon, 10), "Typhoon allowed in typhoon season", "month=10");
    }

    private static void RunMonthlyCityEventPreventionSeverityTest()
    {
        var month = 6;
        var seed = FindComparableMonthlyCityEventSeed(MonthlyCityEventType.Flooding, month, lowPrevention: 0, highPrevention: 60);
        Assert(seed > 0, "Monthly city event prevention seed found", $"seed={seed}");
        if (seed <= 0)
        {
            return;
        }

        var lowPrevention = CreateMonthlyEventTestWorld(month, seed, disasterPrevention: 0);
        var highPrevention = CreateMonthlyEventTestWorld(month, seed, disasterPrevention: 60);

        var lowServices = CreateServices(lowPrevention);
        var highServices = CreateServices(highPrevention);
        var lowEvent = lowServices.Turn.ApplyMonthlyEconomy().PlayerCityEvents.SingleOrDefault();
        var highEvent = highServices.Turn.ApplyMonthlyEconomy().PlayerCityEvents.SingleOrDefault();

        Assert(lowEvent != null && highEvent != null, "Monthly city event prevention produced comparable events", $"low={(lowEvent != null ? lowEvent.EventType : null)}, high={(highEvent != null ? highEvent.EventType : null)}");
        Assert(lowEvent != null && highEvent != null && lowEvent.EventType == highEvent.EventType, "Monthly city event prevention keeps event type stable", $"low={(lowEvent != null ? lowEvent.EventType : null)}, high={(highEvent != null ? highEvent.EventType : null)}");
        if (lowEvent == null || highEvent == null)
        {
            return;
        }

        var lowDamage = System.Math.Abs(lowEvent.FoodDelta) + System.Math.Abs(lowEvent.GoldDelta) + System.Math.Abs(lowEvent.FarmDelta) + System.Math.Abs(lowEvent.DefenseDelta) + System.Math.Abs(lowEvent.LoyaltyDelta);
        var highDamage = System.Math.Abs(highEvent.FoodDelta) + System.Math.Abs(highEvent.GoldDelta) + System.Math.Abs(highEvent.FarmDelta) + System.Math.Abs(highEvent.DefenseDelta) + System.Math.Abs(highEvent.LoyaltyDelta);
        Assert(highDamage < lowDamage, "Monthly city event prevention reduces damage", $"low={lowDamage}, high={highDamage}");
    }

    private static void RunMonthlyCityEventConditionRulesTest()
    {
        Assert(!CanMonthlyCityEventOccur(MonthlyCityEventType.Rebellion, 6, loyalty: 70), "Rebellion blocked when loyalty is not low", "loyalty=70");
        Assert(CanMonthlyCityEventOccur(MonthlyCityEventType.Rebellion, 6, loyalty: 40), "Rebellion allowed when loyalty is low", "loyalty=40");
        Assert(!CanMonthlyCityEventOccur(MonthlyCityEventType.Bandit, 6, loyalty: 80, defense: 20), "Bandit blocked when loyalty is not low", "loyalty=80, defense=20");
        Assert(!CanMonthlyCityEventOccur(MonthlyCityEventType.Bandit, 6, loyalty: 40, defense: 70), "Bandit blocked when defense is not low", "loyalty=40, defense=70");
        Assert(CanMonthlyCityEventOccur(MonthlyCityEventType.Bandit, 6, loyalty: 40, defense: 20), "Bandit allowed when loyalty and defense are both low", "loyalty=40, defense=20");
    }

    private static void RunMonthlyCityEventAllCitiesRecordedTest()
    {
        var seed = FindSeedForSpecificCityMonthlyEvent(MonthlyCityEventType.Earthquake, month: 4, cityId: 2);
        Assert(seed > 0, "Monthly all-city event seed found", $"seed={seed}");
        if (seed <= 0)
        {
            return;
        }

        var world = TestHelpers.World(month: 4);
        world.RandomSeed = seed;
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 0, new[] { 101 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "NeutralCity", 0, 1000, 1000, 0, Array.Empty<int>(), Array.Empty<int>()));
        world.GetCity(1)!.Farm = 80;
        world.GetCity(1)!.Defense = 70;
        world.GetCity(1)!.Loyalty = 85;
        world.GetCity(1)!.DisasterPrevention = 120;
        world.GetCity(2)!.Farm = 80;
        world.GetCity(2)!.Defense = 70;
        world.GetCity(2)!.Loyalty = 85;
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        var services = CreateServices(world);

        var result = services.Turn.ApplyMonthlyEconomy();

        Assert(result.AllCityEvents.Count >= result.PlayerCityEvents.Count, "Monthly all-city events include player subset", $"all={result.AllCityEvents.Count}, player={result.PlayerCityEvents.Count}");
        Assert(result.AllCityEvents.Any(item => item.CityId == 2), "Monthly all-city events include neutral city", $"neutralCount={result.AllCityEvents.Count(item => item.CityId == 2)}");
    }

    private static void RunUpkeepShortageTest()
    {
        var world = TestHelpers.World(month: 5);
        world.RandomSeed = 999;
        world.Cities.Add(TestHelpers.City(2, "AiShortageCity", 2, 1000, 10, 2000, new[] { 201 }, Array.Empty<int>()));
        world.GetCity(2)!.DisasterPrevention = 120;
        world.Officers.Add(TestHelpers.Officer(201, "A1", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        services.Turn.ApplyMonthlyEconomy();
        var city = world.GetCity(2)!;

        Assert(city.Food == 0, "AI upkeep shortage food clamp", $"food={city.Food}");
        Assert(city.Troops == 1920, "AI upkeep shortage desertion", $"troops={city.Troops}");
        Assert(city.Loyalty == 78, "AI upkeep shortage loyalty penalty", $"loyalty={city.Loyalty}");
    }

    private static void RunInternalAffairsScheduleTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1, intelligence: 80, charm: 70));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        var services = CreateServices(world);

        var scheduled = services.Resolver.ScheduleInternalAffairs(1, 1, 101, InternalAffairsJobType.Farm, 2);
        var firstResolve = services.Turn.ResolvePendingCommands(services.Resolver);
        var city = world.GetCity(1)!;
        var remaining = world.InternalAffairsSchedules.FirstOrDefault()?.RemainingMonths ?? -1;
        var terminated = services.Resolver.TerminateInternalAffairsSchedule(1, world.InternalAffairsSchedules.First().Id);

        Assert(scheduled.Success, "Internal affairs scheduling", $"success={scheduled.Success}");
        Assert(city.Farm > 50, "Internal affairs monthly effect", $"farm={city.Farm}");
        Assert(firstResolve.Any(result => result.Success), "Internal affairs month-end result", $"results={firstResolve.Count}");
        Assert(remaining == 1, "Internal affairs remaining month", $"remaining={remaining}");
        Assert(terminated.Success && world.InternalAffairsSchedules.First().State == InternalAffairsScheduleState.Terminated, "Internal affairs termination", $"state={world.InternalAffairsSchedules.First().State}");
    }

    private static void RunCivilReliefTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        var services = CreateServices(world);

        var result = services.Resolver.ExecuteCivilRelief(1, 1, 101, 100, 1000);
        var resolved = services.Turn.ResolvePendingCommands(services.Resolver);
        var city = world.GetCity(1)!;

        Assert(result.Success, "Civil relief resolves", $"success={result.Success}");
        Assert(resolved.Count == 1 && resolved[0].Success, "Civil relief month-end resolves", $"count={resolved.Count}");
        Assert(city.Gold == 900 && city.Food == 0, "Civil relief resource cost", $"gold={city.Gold}, food={city.Food}");
        Assert(city.Loyalty == 100, "Civil relief loyalty gain", $"loyalty={city.Loyalty}");
    }

    private static void RunCivilInvestigationTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        var services = CreateServices(world);
        var city = world.GetCity(1)!;
        var beforeGold = city.Gold;
        var beforeFood = city.Food;
        var beforeFarm = city.Farm;
        var beforeLoyalty = city.Loyalty;

        var result = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Search,
            ActorFactionId = 1,
            SourceCityId = 1,
            OfficerIds = new List<int> { 101 }
        });
        var resolved = services.Turn.ResolvePendingCommands(services.Resolver);
        var stable = city.Gold >= beforeGold && city.Food >= beforeFood && city.Farm >= beforeFarm && city.Loyalty >= beforeLoyalty;

        Assert(result.Success, "Civil investigation resolves", $"success={result.Success}");
        Assert(resolved.Count == 1 && resolved[0].Success, "Civil investigation month-end resolves", $"count={resolved.Count}");
        Assert(stable, "Civil investigation keeps city state valid", $"gold={city.Gold}, food={city.Food}, farm={city.Farm}, loyalty={city.Loyalty}");
    }

    private static void RunMultiMonthSoakTest()
    {
        var world = TestHelpers.World(year: 200, month: 1);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1200, 1600, 1800, new[] { 101 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "AiFrontier", 2, 1400, 1800, 2600, new[] { 201 }, new[] { 1, 3 }));
        world.Cities.Add(TestHelpers.City(3, "AiRear", 2, 1300, 1700, 1500, new[] { 202 }, new[] { 2 }));
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        world.Officers.Add(TestHelpers.Officer(201, "A1", 2, 82, 70, 68, 80));
        world.Officers.Add(TestHelpers.Officer(202, "A2", 3, 68, 76, 74, 66));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201, 202 }));
        var services = CreateServices(world);

        var ok = true;
        string detail = "12 months stable";

        try
        {
            // This is a stability test, not a strategy-quality test: state must stay internally valid for 12 months.
            for (var monthIndex = 0; monthIndex < 12; monthIndex++)
            {
                var aiCityIds = world.Cities
                    .Where(city => city.OwnerFactionId == 2)
                    .Select(city => city.Id)
                    .ToList();

                foreach (var cityId in aiCityIds)
                {
                    _ = services.Ai.RunSingleCityDecision(2, cityId);
                }

                _ = services.Turn.ResolvePendingCommands(services.Resolver);
                services.Turn.AdvanceMonth();
                _ = services.Turn.ApplyMonthlyEconomy();

                if (world.PendingCommands.Count != 0)
                {
                    ok = false;
                    detail = $"pendingCommands={world.PendingCommands.Count}";
                    break;
                }

                foreach (var city in world.Cities)
                {
                    if (city.Gold < 0 || city.Food < 0 || city.Troops < 0)
                    {
                        ok = false;
                        detail = $"negative resource in city {city.Id}";
                        break;
                    }
                }

                if (!ok)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ok = false;
            detail = ex.GetType().Name;
        }

        Assert(ok, "AI multi-month soak", detail);
    }
}
