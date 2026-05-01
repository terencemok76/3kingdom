using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace AiHarness;

internal static class Program
{
    private static readonly List<string> Passes = new();
    private static readonly List<string> Failures = new();

    private static void Main()
    {
        // Keep test order stable so regression diffs stay easy to compare across runs.
        RunAttackSchedulingTest();
        RunMoveSchedulingTest();
        RunCoreActionsTest();
        RunAttackResolutionTest();
        RunSeasonalGoldTest();
        RunSeasonalFoodTest();
        RunUpkeepShortageTest();
        RunInternalAffairsScheduleTest();
        RunInternalAffairsOfficerLockTest();
        RunPersonnelBonusTest();
        RunAssignOfficerRoleTest();
        RunHireOfficerTest();
        RunCivilReliefTest();
        RunCivilInvestigationTest();
        RunCivilInvestigationFindsOfficerTest();
        RunFreeOfficerMovementTest();
        RunMultiMonthSoakTest();

        Console.WriteLine($"AI TEST SUMMARY: PASS={Passes.Count} FAIL={Failures.Count}");
        foreach (var line in Passes)
        {
            Console.WriteLine(line);
        }

        foreach (var line in Failures)
        {
            Console.WriteLine(line);
        }

        Environment.ExitCode = Failures.Count == 0 ? 0 : 1;
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
        Assert(city.Gold == 380 && city.Food == 420, "AI core action immediate costs", $"gold={city.Gold}, food={city.Food}");
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
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(2, 2);
        _ = services.Turn.ResolvePendingCommands(services.Resolver);

        Assert(world.GetCity(1)?.OwnerFactionId == 2, "AI attack resolution", $"owner={world.GetCity(1)?.OwnerFactionId}");
    }

    private static void RunSeasonalGoldTest()
    {
        var world = TestHelpers.World(month: 4);
        world.Cities.Add(TestHelpers.City(2, "AiGoldCity", 2, 1000, 1000, 1200, new[] { 201 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(201, "A1", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
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
        world.Officers.Add(TestHelpers.Officer(201, "A1", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        var result = services.Turn.ApplyMonthlyEconomy();
        var city = world.GetCity(2)!;

        Assert(result.AnnualFoodCollected == 2736, "AI seasonal food total", $"annualFood={result.AnnualFoodCollected}");
        Assert(city.Food == 3686, "AI seasonal food applied with upkeep", $"food={city.Food}");
    }

    private static void RunUpkeepShortageTest()
    {
        var world = TestHelpers.World(month: 5);
        world.Cities.Add(TestHelpers.City(2, "AiShortageCity", 2, 1000, 10, 2000, new[] { 201 }, Array.Empty<int>()));
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

    private static void RunInternalAffairsOfficerLockTest()
    {
        var world = TestHelpers.World(month: 3);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1500, new[] { 101, 102 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "FriendlyCity", 1, 1000, 1000, 1000, Array.Empty<int>(), new[] { 1 }));
        world.Officers.Add(TestHelpers.Officer(101, "Worker", 1, intelligence: 85, charm: 75));
        world.Officers.Add(TestHelpers.Officer(102, "Reserve", 1));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        var services = CreateServices(world);

        var scheduled = services.Resolver.ScheduleInternalAffairs(1, 1, 101, InternalAffairsJobType.Farm, 3);
        var recruitBlocked = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Recruit,
            ActorFactionId = 1,
            SourceCityId = 1,
            OfficerIds = new List<int> { 101 }
        });
        var moveBlocked = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Move,
            ActorFactionId = 1,
            SourceCityId = 1,
            TargetCityId = 2,
            OfficerIds = new List<int> { 101 }
        });

        Assert(scheduled.Success, "Internal affairs lock setup", $"success={scheduled.Success}");
        Assert(!recruitBlocked.Success, "Internal affairs blocks recruit reuse", $"success={recruitBlocked.Success}");
        Assert(!moveBlocked.Success, "Internal affairs blocks move reuse", $"success={moveBlocked.Success}");
    }

    private static void RunPersonnelBonusTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        var services = CreateServices(world);

        var result = services.Resolver.ExecutePersonnelBonus(1, 1, 101, 200, 500);
        var city = world.GetCity(1)!;
        var officer = world.GetOfficer(101)!;

        Assert(result.Success, "Personnel bonus resolves", $"success={result.Success}");
        Assert(city.Gold == 800 && city.Food == 500, "Personnel bonus resource cost", $"gold={city.Gold}, food={city.Food}");
        Assert(officer.Loyalty == 83, "Personnel bonus loyalty gain", $"loyalty={officer.Loyalty}");
    }

    private static void RunAssignOfficerRoleTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101, 102 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Ruler", 1));
        world.Officers.Add(TestHelpers.Officer(102, "Officer", 1));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        var services = CreateServices(world);

        var result = services.Resolver.ExecuteAssignOfficerRole(1, 1, 102, "Strategist");
        var blocked = services.Resolver.ExecuteAssignOfficerRole(1, 1, 101, "Strategist");
        var officer = world.GetOfficer(102)!;

        Assert(result.Success, "Assign officer role resolves", $"success={result.Success}");
        Assert(officer.Role == "Strategist", "Assign officer role applies", $"role={officer.Role}");
        Assert(!blocked.Success, "Assign officer role blocks ruler", $"success={blocked.Success}");
    }

    private static void RunHireOfficerTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "OtherCity", 2, 1000, 1000, 1000, new[] { 201, 202, 203 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Ruler", 1));
        world.Officers.Add(TestHelpers.Officer(201, "LowLoyaltyOfficer", 2));
        world.Officers.Add(TestHelpers.Officer(202, "HighLoyaltyOfficer", 2));
        world.Officers.Add(TestHelpers.Officer(203, "OtherRuler", 2));
        world.GetOfficer(201)!.Loyalty = 55;
        world.GetOfficer(202)!.Loyalty = 90;
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Other", false, 203, new[] { 201, 202, 203 }));
        var services = CreateServices(world);

        var result = services.Resolver.ExecuteHireOfficer(1, 1, 201);
        var refused = services.Resolver.ExecuteHireOfficer(1, 1, 202);
        var rulerBlocked = services.Resolver.ExecuteHireOfficer(1, 1, 203);
        var playerCity = world.GetCity(1)!;
        var otherCity = world.GetCity(2)!;

        Assert(result.Success, "Hire officer resolves", $"success={result.Success}");
        Assert(playerCity.Gold == 800, "Hire officer gold cost", $"gold={playerCity.Gold}");
        Assert(playerCity.OfficerIds.Contains(201) && !otherCity.OfficerIds.Contains(201), "Hire officer moves city", $"playerHas={playerCity.OfficerIds.Contains(201)}");
        Assert(world.GetFaction(1)!.OfficerIds.Contains(201) && !world.GetFaction(2)!.OfficerIds.Contains(201), "Hire officer moves faction", $"playerFactionHas={world.GetFaction(1)!.OfficerIds.Contains(201)}");
        Assert(!refused.Success, "Hire officer blocks high loyalty", $"success={refused.Success}");
        Assert(!rulerBlocked.Success, "Hire officer blocks ruler", $"success={rulerBlocked.Success}");

        var freeWorld = TestHelpers.World(year: 200, month: 2);
        freeWorld.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101 }, Array.Empty<int>()));
        freeWorld.Cities.Add(TestHelpers.City(3, "PlayerFreeCity", 1, 1000, 1000, 1000, Array.Empty<int>(), Array.Empty<int>()));
        freeWorld.Officers.Add(TestHelpers.Officer(101, "Ruler", 1));
        freeWorld.Officers.Add(TestHelpers.Officer(301, "FreeOfficerInPlayerCity", 3));
        freeWorld.GetOfficer(301)!.BirthYear = 170;
        freeWorld.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        var freeServices = CreateServices(freeWorld);
        var freeResult = freeServices.Resolver.ExecuteHireOfficer(1, 1, 301);

        Assert(freeResult.Success, "Hire free officer from player city resolves", $"success={freeResult.Success}");
        Assert(freeWorld.GetCity(1)!.OfficerIds.Contains(301), "Hire free officer from player city joins target", $"targetHas={freeWorld.GetCity(1)!.OfficerIds.Contains(301)}");
    }

    private static void RunCivilReliefTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        var services = CreateServices(world);

        var result = services.Resolver.ExecuteCivilRelief(1, 1, 100, 1000);
        var city = world.GetCity(1)!;

        Assert(result.Success, "Civil relief resolves", $"success={result.Success}");
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

        var result = services.Resolver.ExecuteCivilInvestigation(1, 1);
        var changed = city.Gold > beforeGold || city.Food > beforeFood || city.Farm > beforeFarm || city.Loyalty > beforeLoyalty;

        Assert(result.Success, "Civil investigation resolves", $"success={result.Success}");
        Assert(changed, "Civil investigation changes city", $"gold={city.Gold}, food={city.Food}, farm={city.Farm}, loyalty={city.Loyalty}");
    }

    private static void RunCivilInvestigationFindsOfficerTest()
    {
        var world = TestHelpers.World(year: 200, month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        world.Officers.Add(TestHelpers.Officer(150, "FreeOfficer", 1));
        world.GetOfficer(150)!.Belongs = "Shu";
        world.GetOfficer(150)!.BirthYear = 170;
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        var services = CreateServices(world);

        var result = services.Resolver.ExecuteCivilInvestigation(1, 1);
        var city = world.GetCity(1)!;
        var faction = world.GetFaction(1)!;
        var officer = world.GetOfficer(150)!;

        Assert(result.Success, "Civil investigation finds free officer", $"success={result.Success}");
        Assert(!city.OfficerIds.Contains(150), "Civil investigation does not recruit free officer", $"cityHas={city.OfficerIds.Contains(150)}");
        Assert(!faction.OfficerIds.Contains(150), "Civil investigation does not add free officer to faction", $"factionHas={faction.OfficerIds.Contains(150)}");
        Assert(officer.CityId == 1, "Civil investigation reveals officer city", $"cityId={officer.CityId}");
    }

    private static void RunFreeOfficerMovementTest()
    {
        var world = TestHelpers.World(year: 200, month: 1);
        world.Cities.Add(TestHelpers.City(1, "FreeStart", 0, 1000, 1000, 1000, Array.Empty<int>(), new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "FreeNext", 0, 1000, 1000, 1000, Array.Empty<int>(), new[] { 1 }));
        world.Officers.Add(TestHelpers.Officer(150, "FreeOfficer", 1));
        var officer = world.GetOfficer(150)!;
        officer.BirthYear = 170;
        officer.FreeOfficerStayMonths = 1;
        var services = CreateServices(world);

        services.Turn.AdvanceMonth();

        Assert(officer.CityId == 0 || officer.CityId == 2, "Free officer moves or hides after stay", $"cityId={officer.CityId}");
        Assert(officer.FreeOfficerStayMonths > 0, "Free officer resets stay months", $"stay={officer.FreeOfficerStayMonths}");

        var hiddenWorld = TestHelpers.World(year: 200, month: 2);
        hiddenWorld.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101 }, Array.Empty<int>()));
        hiddenWorld.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        hiddenWorld.Officers.Add(TestHelpers.Officer(151, "HiddenFreeOfficer", 0));
        hiddenWorld.GetOfficer(151)!.BirthYear = 170;
        hiddenWorld.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        var hiddenServices = CreateServices(hiddenWorld);
        var result = hiddenServices.Resolver.ExecuteCivilInvestigation(1, 1);

        Assert(result.Success, "Civil investigation can discover hidden free officer", $"success={result.Success}");
        Assert(!hiddenWorld.GetCity(1)!.OfficerIds.Contains(151), "Hidden free officer is revealed but not recruited", $"cityHas={hiddenWorld.GetCity(1)!.OfficerIds.Contains(151)}");
        Assert(hiddenWorld.GetOfficer(151)!.CityId == 1, "Hidden free officer appears in investigated city", $"cityId={hiddenWorld.GetOfficer(151)!.CityId}");

        var rejectWorld = TestHelpers.World(year: 200, month: 2);
        rejectWorld.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101 }, Array.Empty<int>()));
        rejectWorld.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        rejectWorld.Officers.Add(TestHelpers.Officer(152, "UnwillingFreeOfficer", 1, charm: 20));
        rejectWorld.GetOfficer(152)!.BirthYear = 170;
        rejectWorld.GetOfficer(152)!.Ambition = 100;
        rejectWorld.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        var rejectServices = CreateServices(rejectWorld);
        var rejectResult = rejectServices.Resolver.ExecuteHireOfficer(1, 1, 152);

        Assert(!rejectResult.Success, "Hire free officer can be refused", $"success={rejectResult.Success}");
        Assert(!rejectWorld.GetCity(1)!.OfficerIds.Contains(152), "Refused free officer does not join city", $"cityHas={rejectWorld.GetCity(1)!.OfficerIds.Contains(152)}");

        var offerWorld = TestHelpers.World(year: 200, month: 2);
        offerWorld.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 3000, 2000, 1000, new[] { 101 }, Array.Empty<int>()));
        offerWorld.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        offerWorld.Officers.Add(TestHelpers.Officer(153, "GiftedFreeOfficer", 1, charm: 20));
        offerWorld.GetOfficer(153)!.BirthYear = 170;
        offerWorld.GetOfficer(153)!.Ambition = 100;
        offerWorld.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        var offerServices = CreateServices(offerWorld);
        var offerResult = offerServices.Resolver.ExecuteHireOfficer(1, 1, 153, goldOffer: 2500, foodOffer: 0);

        Assert(offerResult.Success, "Hire free officer can accept generous offer", $"success={offerResult.Success}");
        Assert(offerWorld.GetCity(1)!.Gold == 300, "Hire offer deducts base cost plus gift", $"gold={offerWorld.GetCity(1)!.Gold}");
        Assert(offerWorld.GetCity(1)!.OfficerIds.Contains(153), "Accepted offer joins city", $"cityHas={offerWorld.GetCity(1)!.OfficerIds.Contains(153)}");
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

    private static void Assert(bool condition, string name, string detail)
    {
        if (condition)
        {
            Passes.Add($"PASS: {name} - {detail}");
        }
        else
        {
            Failures.Add($"FAIL: {name} - {detail}");
        }
    }

    private static (TurnManager Turn, CommandResolver Resolver, AiController Ai) CreateServices(WorldState world)
    {
        var turn = new TurnManager();
        turn.Initialize(world);
        var resolver = new CommandResolver();
        resolver.Initialize(turn, new CombatResolver(), new LocalizationService());
        var ai = new AiController();
        ai.Initialize(resolver, turn, new LocalizationService());
        return (turn, resolver, ai);
    }
}

internal static class TestHelpers
{
    public static OfficerData Officer(int id, string name, int cityId, int strength = 70, int intelligence = 60, int charm = 60, int combat = 70)
    {
        return new OfficerData
        {
            Id = id,
            Name = name,
            NameZhHant = name,
            Role = "General",
            CityId = cityId,
            Strength = strength,
            Intelligence = intelligence,
            Charm = charm,
            Leadership = 70,
            Politics = 50,
            Loyalty = 80,
            Ambition = 50,
            Combat = combat
        };
    }

    public static CityData City(int id, string name, int ownerFactionId, int gold, int food, int troops, IEnumerable<int> officers, IEnumerable<int> connected)
    {
        return new CityData
        {
            Id = id,
            Name = name,
            NameEn = name,
            NameZhHant = name,
            OwnerFactionId = ownerFactionId,
            Gold = gold,
            Food = food,
            Troops = troops,
            Farm = 50,
            Commercial = 50,
            Defense = 40,
            Loyalty = 80,
            OfficerIds = new List<int>(officers),
            ConnectedCityIds = new List<int>(connected)
        };
    }

    public static FactionData Faction(int id, string name, bool isPlayer, int rulerOfficerId, IEnumerable<int> officers)
    {
        return new FactionData
        {
            Id = id,
            NameEn = name,
            NameZhHant = name,
            IsPlayer = isPlayer,
            RulerOfficerId = rulerOfficerId,
            OfficerIds = new List<int>(officers)
        };
    }

    public static WorldState World(int year = 200, int month = 1)
    {
        return new WorldState
        {
            Year = year,
            Month = month,
            RandomSeed = 1
        };
    }
}
