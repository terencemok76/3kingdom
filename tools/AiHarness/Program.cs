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
        world.Cities.Add(TestHelpers.City(2, "CoreCity", 2, 500, 500, 1500, new[] { 201 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(201, "A1", 2, 70, 80, 75, 70));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(2, 2);

        var recruitPending = world.PendingCommands.Count(x => x.Type == CommandType.Recruit);
        var developPending = world.PendingCommands.Count(x => x.Type == CommandType.Develop);
        var city = world.GetCity(2)!;
        Assert(recruitPending == 1, "AI recruit scheduling", $"pending={recruitPending}");
        Assert(developPending == 1, "AI develop scheduling", $"pending={developPending}");
        Assert(city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month, "AI search execution", $"lastSearch={city.LastSearchYear}/{city.LastSearchMonth}");
        // Search can grant random rewards, so this only checks that required costs were applied.
        Assert(city.Gold >= 280 && city.Food >= 420, "AI core action immediate costs", $"gold={city.Gold}, food={city.Food}");
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
