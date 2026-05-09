using System.IO;
using System.Reflection;
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
        RunAttackAutoBreakPactTest();
        RunDefensePromptEligibilityTest();
        RunDefenseDeploymentAffectsCombatOutcomeTest();
        RunTroopCounterCombatTest();
        RunAttackSuccessFlowTest();
        RunAttackFailureFlowTest();
        RunAttackCancellationFlowTest();
        RunMoveSchedulingTest();
        RunCoreActionsTest();
        RunAiDefensiveDiplomacyTruceTest();
        RunAiSpyReconPriorityTest();
        RunAiSpyAssassinationTargetSelectionTest();
        RunAiDiplomacyGiftTest();
        RunAiDiplomacyAllianceTest();
        RunAiDiplomacyDemandTest();
        RunAiDiplomacyBreakPactTest();
        RunDiplomacyGiftScheduleTest();
        RunDiplomacyDemandScheduleTest();
        RunDiplomacyBreakPactResolutionTest();
        RunSpyAssassinationResolutionTest();
        RunSpyAssassinationDesignatedTargetTest();
        RunSpyAssassinationPlayerSuccessionPendingTest();
        RunSpyAssassinationFactionCollapseTest();
        RunAttackResolutionTest();
        RunAttackRulerDeathPlayerSuccessionPendingTest();
        RunSeasonalGoldTest();
        RunSeasonalFoodTest();
        RunUpkeepShortageTest();
        RunOfficerProgressionBuffTest();
        RunSpyAndDiplomacyProgressionSuccessBuffTest();
        RunInternalAffairsScheduleTest();
        RunInternalAffairsOfficerLockTest();
        RunPersonnelBonusTest();
        RunAssignOfficerRoleTest();
        RunFireOfficerTest();
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

    private static void RunAiDefensiveDiplomacyTruceTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "AiBorderCity", 1, 900, 900, 1400, new[] { 101, 102 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "ThreatCity", 2, 1000, 1000, 2300, new[] { 201 }, new[] { 1 }));
        world.Officers.Add(TestHelpers.Officer(101, "AiRuler", 1, charm: 80, intelligence: 72));
        world.Officers.Add(TestHelpers.Officer(102, "Diplomat", 1, charm: 92, intelligence: 78));
        world.Officers.Add(TestHelpers.Officer(201, "Enemy", 2, combat: 85));
        world.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(1, 1);

        var pending = world.PendingCommands.SingleOrDefault(command =>
            command.Type == CommandType.Diplomacy &&
            command.DiplomacyActionType == DiplomacyActionType.Truce &&
            command.ActorFactionId == 1 &&
            command.TargetFactionId == 2);
        Assert(pending != null, "AI defensive diplomacy truce", $"pending={(pending != null ? 1 : 0)}");
    }

    private static void RunAiSpyReconPriorityTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "AiScoutCity", 1, 500, 800, 2000, new[] { 101 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "HiddenEnemyCity", 2, 1000, 1000, 1500, new[] { 201 }, new[] { 1 }));
        world.Officers.Add(TestHelpers.Officer(101, "SpyOfficer", 1, intelligence: 92, charm: 82));
        world.Officers.Add(TestHelpers.Officer(201, "Enemy", 2, combat: 70));
        world.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(1, 1);

        var pending = world.PendingCommands.SingleOrDefault(command =>
            command.Type == CommandType.Spy &&
            command.SpyActionType == SpyActionType.Reconnaissance &&
            command.ActorFactionId == 1 &&
            command.TargetCityId == 2);
        Assert(pending != null, "AI spy recon hidden target first", $"pending={(pending != null ? 1 : 0)}");
    }

    private static void RunAiSpyAssassinationTargetSelectionTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "AiSpyCity", 1, 700, 900, 1000, new[] { 101 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "EnemyCourt", 2, 180, 420, 900, new[] { 201, 202 }, new[] { 1 }));
        world.GetCity(2)!.Defense = 18;
        world.Officers.Add(TestHelpers.Officer(101, "SpyMaster", 1, intelligence: 95, charm: 84));
        world.Officers.Add(TestHelpers.Officer(201, "EnemyRuler", 2, combat: 84));
        world.Officers.Add(TestHelpers.Officer(202, "EnemyGeneral", 2, combat: 82));
        world.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201, 202 }));
        world.UpsertCityIntel(1, 2, 3);
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(1, 1);

        var pending = world.PendingCommands.SingleOrDefault(command =>
            command.Type == CommandType.Spy &&
            command.SpyActionType == SpyActionType.Assassination &&
            command.ActorFactionId == 1 &&
            command.TargetCityId == 2);
        Assert(pending != null, "AI spy assassination schedules", $"pending={(pending != null ? 1 : 0)}");
        Assert(pending != null && pending.TargetOfficerId == 201, "AI spy assassination picks best target officer", pending == null ? "pending=null" : $"targetOfficer={pending.TargetOfficerId}");
    }

    private static void RunAiDiplomacyGiftTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "AiRichCity", 1, 800, 800, 900, new[] { 101, 102 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "OtherCity", 2, 1000, 1000, 1100, new[] { 201 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "AiRuler", 1, charm: 80, intelligence: 70));
        world.Officers.Add(TestHelpers.Officer(102, "Diplomat", 1, charm: 95, intelligence: 82));
        world.Officers.Add(TestHelpers.Officer(201, "Other", 2));
        world.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "Other", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(1, 1);

        var pending = world.PendingCommands.SingleOrDefault(command =>
            command.Type == CommandType.Diplomacy &&
            command.DiplomacyActionType == DiplomacyActionType.Gift &&
            command.ActorFactionId == 1 &&
            command.TargetFactionId == 2 &&
            command.GoldToSend == 200);
        Assert(pending != null, "AI diplomacy gift baseline", $"pending={(pending != null ? 1 : 0)}");
    }

    private static void RunAiDiplomacyAllianceTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "AiCity", 1, 450, 700, 900, new[] { 101, 102 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "FriendlyOtherCity", 2, 1000, 1000, 1000, new[] { 201 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "AiRuler", 1, charm: 80, intelligence: 70));
        world.Officers.Add(TestHelpers.Officer(102, "Diplomat", 1, charm: 94, intelligence: 84));
        world.Officers.Add(TestHelpers.Officer(201, "OtherRuler", 2, charm: 75));
        world.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "Other", false, 201, new[] { 201 }));
        world.DiplomacyRelations.Add(new DiplomacyRelationData
        {
            FactionAId = 1,
            FactionBId = 2,
            Status = DiplomacyStatusType.Neutral,
            RemainingMonths = 0,
            RelationScore = 36
        });
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(1, 1);

        var pending = world.PendingCommands.SingleOrDefault(command =>
            command.Type == CommandType.Diplomacy &&
            command.DiplomacyActionType == DiplomacyActionType.Alliance &&
            command.ActorFactionId == 1 &&
            command.TargetFactionId == 2);
        Assert(pending != null, "AI diplomacy alliance baseline", $"pending={(pending != null ? 1 : 0)}");
    }

    private static void RunAiDiplomacyDemandTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "AiCity", 1, 700, 700, 2600, new[] { 101, 102 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "WeakTargetCity", 2, 900, 900, 800, new[] { 201 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "AiRuler", 1, charm: 80, intelligence: 70));
        world.Officers.Add(TestHelpers.Officer(102, "Diplomat", 1, charm: 95, intelligence: 84));
        world.Officers.Add(TestHelpers.Officer(201, "WeakRuler", 2, charm: 65));
        world.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "WeakTarget", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(1, 1);

        var pending = world.PendingCommands.SingleOrDefault(command =>
            command.Type == CommandType.Diplomacy &&
            command.DiplomacyActionType == DiplomacyActionType.Demand &&
            command.ActorFactionId == 1 &&
            command.TargetFactionId == 2 &&
            command.GoldToSend == 200);
        Assert(pending != null, "AI diplomacy demand baseline", $"pending={(pending != null ? 1 : 0)}");
    }

    private static void RunAiDiplomacyBreakPactTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "AiCity", 1, 450, 700, 2600, new[] { 101, 102 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "FormerAllyCity", 2, 900, 900, 900, new[] { 201 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "AiRuler", 1, charm: 80, intelligence: 70));
        world.Officers.Add(TestHelpers.Officer(102, "Diplomat", 1, charm: 94, intelligence: 84));
        world.Officers.Add(TestHelpers.Officer(201, "FormerAllyRuler", 2, charm: 75));
        world.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "FormerAlly", false, 201, new[] { 201 }));
        world.DiplomacyRelations.Add(new DiplomacyRelationData
        {
            FactionAId = 1,
            FactionBId = 2,
            Status = DiplomacyStatusType.Alliance,
            RemainingMonths = 4,
            RelationScore = 0
        });
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(1, 1);

        var pending = world.PendingCommands.SingleOrDefault(command =>
            command.Type == CommandType.Diplomacy &&
            command.DiplomacyActionType == DiplomacyActionType.BreakPact &&
            command.ActorFactionId == 1 &&
            command.TargetFactionId == 2);
        Assert(pending != null, "AI diplomacy break pact baseline", $"pending={(pending != null ? 1 : 0)}");
    }

    private static void RunDiplomacyDemandScheduleTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 600, 700, 900, new[] { 101, 102 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "TargetCity", 2, 900, 800, 1000, new[] { 201 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Ruler", 1, charm: 80, intelligence: 70));
        world.Officers.Add(TestHelpers.Officer(102, "Envoy", 1, charm: 92, intelligence: 78));
        world.Officers.Add(TestHelpers.Officer(201, "TargetRuler", 2, charm: 70));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "Target", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        var result = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Diplomacy,
            ActorFactionId = 1,
            SourceCityId = 1,
            TargetFactionId = 2,
            DiplomacyActionType = DiplomacyActionType.Demand,
            GoldToSend = 200,
            FoodToSend = 300,
            HorsesToSend = 40,
            OfficerIds = new List<int> { 102 }
        });

        var pending = world.PendingCommands.SingleOrDefault(command =>
            command.Type == CommandType.Diplomacy &&
            command.DiplomacyActionType == DiplomacyActionType.Demand &&
            command.ActorFactionId == 1 &&
            command.TargetFactionId == 2 &&
            command.GoldToSend == 200 &&
            command.FoodToSend == 300 &&
            command.HorsesToSend == 40);
        Assert(result.Success, "Diplomacy demand schedules", $"success={result.Success}");
        Assert(pending != null, "Diplomacy demand pending command", $"pending={(pending != null ? 1 : 0)}");
    }

    private static void RunDiplomacyGiftScheduleTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1200, 2200, 900, new[] { 101, 102 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "TargetCity", 2, 900, 800, 1000, new[] { 201 }, Array.Empty<int>()));
        world.GetCity(1)!.Horses = 120;
        world.Officers.Add(TestHelpers.Officer(101, "Ruler", 1, charm: 80, intelligence: 70));
        world.Officers.Add(TestHelpers.Officer(102, "Envoy", 1, charm: 92, intelligence: 78));
        world.Officers.Add(TestHelpers.Officer(201, "TargetRuler", 2, charm: 70));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "Target", false, 201, new[] { 201 }));
        var services = CreateServices(world);

        var result = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Diplomacy,
            ActorFactionId = 1,
            SourceCityId = 1,
            TargetFactionId = 2,
            DiplomacyActionType = DiplomacyActionType.Gift,
            GoldToSend = 200,
            FoodToSend = 500,
            HorsesToSend = 30,
            OfficerIds = new List<int> { 102 }
        });

        var sourceCity = world.GetCity(1)!;
        var pending = world.PendingCommands.SingleOrDefault(command =>
            command.Type == CommandType.Diplomacy &&
            command.DiplomacyActionType == DiplomacyActionType.Gift &&
            command.ActorFactionId == 1 &&
            command.TargetFactionId == 2 &&
            command.GoldToSend == 200 &&
            command.FoodToSend == 500 &&
            command.HorsesToSend == 30);
        Assert(result.Success, "Diplomacy gift schedules", $"success={result.Success}");
        Assert(pending != null, "Diplomacy gift pending command", $"pending={(pending != null ? 1 : 0)}");
        Assert(sourceCity.Gold == 1000 && sourceCity.Food == 1700 && sourceCity.Horses == 90, "Diplomacy gift reserves resources", $"gold={sourceCity.Gold}, food={sourceCity.Food}, horses={sourceCity.Horses}");
    }

    private static void RunDiplomacyBreakPactResolutionTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 600, 700, 900, new[] { 101, 102 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "TargetCity", 2, 900, 800, 1000, new[] { 201 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Ruler", 1, charm: 80, intelligence: 70));
        world.Officers.Add(TestHelpers.Officer(102, "Envoy", 1, charm: 92, intelligence: 78));
        world.Officers.Add(TestHelpers.Officer(201, "TargetRuler", 2, charm: 70));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "Target", false, 201, new[] { 201 }));
        world.DiplomacyRelations.Add(new DiplomacyRelationData
        {
            FactionAId = 1,
            FactionBId = 2,
            Status = DiplomacyStatusType.Alliance,
            RemainingMonths = 4,
            RelationScore = 40
        });
        var services = CreateServices(world);

        var scheduled = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Diplomacy,
            ActorFactionId = 1,
            SourceCityId = 1,
            TargetFactionId = 2,
            DiplomacyActionType = DiplomacyActionType.BreakPact,
            OfficerIds = new List<int> { 102 }
        });
        var resolved = services.Turn.ResolvePendingCommands(services.Resolver).Single();
        var relation = world.GetDiplomacyRelation(1, 2)!;

        Assert(scheduled.Success, "Diplomacy break pact schedules", $"success={scheduled.Success}");
        Assert(resolved.Success, "Diplomacy break pact resolves", $"success={resolved.Success}");
        Assert(relation.Status == DiplomacyStatusType.Neutral && relation.RemainingMonths == 0, "Diplomacy break pact clears treaty", $"status={relation.Status}, months={relation.RemainingMonths}");
    }

    private static void RunSpyAssassinationResolutionTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "SpyCity", 1, 800, 800, 1200, new[] { 101 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "TargetCity", 2, 900, 900, 1000, new[] { 201, 202 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "SpyOfficer", 1, intelligence: 95, charm: 90));
        world.Officers.Add(TestHelpers.Officer(201, "EnemyRuler", 2, strength: 85, intelligence: 75, charm: 80, combat: 85));
        world.Officers.Add(TestHelpers.Officer(202, "EnemyGeneral", 2, strength: 80, intelligence: 70, charm: 65, combat: 82));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201, 202 }));
        var services = CreateServices(world);
        var targetCity = world.GetCity(2)!;

        var schedule = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Spy,
            ActorFactionId = 1,
            SourceCityId = 1,
            TargetCityId = 2,
            SpyActionType = SpyActionType.Assassination,
            OfficerIds = new List<int> { 101 }
        });
        var result = InvokePrivateInstance<CommandResult>(
            services.Resolver,
            "ResolveSuccessfulSpyAction",
            world,
            1,
            targetCity,
            world.GetOfficer(101)!,
            SpyActionType.Assassination);

        Assert(schedule.Success, "Spy assassination schedules", $"success={schedule.Success}");
        Assert(result.Success, "Spy assassination resolves", $"success={result.Success}");
        Assert(!targetCity.OfficerIds.Contains(201), "Spy assassination removes ruler target from city", $"cityHas={targetCity.OfficerIds.Contains(201)}");
        Assert(!world.GetFaction(2)!.OfficerIds.Contains(201), "Spy assassination removes ruler target from faction", $"factionHas={world.GetFaction(2)!.OfficerIds.Contains(201)}");
        Assert(world.GetOfficer(201)!.DeathYear == world.Year, "Spy assassination marks ruler dead", $"deathYear={world.GetOfficer(201)!.DeathYear}");
        Assert(world.GetFaction(2)!.RulerOfficerId == 202, "Spy assassination assigns AI successor", $"ruler={world.GetFaction(2)!.RulerOfficerId}");
        Assert(world.GetOfficer(202)!.Role == "Lord", "Spy assassination successor gets ruler role", $"role={world.GetOfficer(202)!.Role}");
        Assert(world.GetFaction(2)!.NameZhHant == "EnemyGeneral軍", "Spy assassination successor updates faction name", $"name={world.GetFaction(2)!.NameZhHant}");
        Assert(targetCity.OfficerIds.Contains(202), "Spy assassination keeps successor alive", $"cityHasSuccessor={targetCity.OfficerIds.Contains(202)}");
    }

    private static void RunSpyAssassinationDesignatedTargetTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "SpyCity", 1, 800, 800, 1200, new[] { 101 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "EnemyCity", 2, 900, 900, 1000, new[] { 201, 202 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "SpyOfficer", 1, intelligence: 95, charm: 90));
        world.Officers.Add(TestHelpers.Officer(201, "EnemyRuler", 2, strength: 85, intelligence: 75, charm: 80, combat: 85));
        world.Officers.Add(TestHelpers.Officer(202, "EnemyStrategist", 2, strength: 65, intelligence: 94, charm: 82, combat: 60));
        world.Factions.Add(TestHelpers.Faction(1, "Enemy", false, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Target", false, 201, new[] { 201, 202 }));
        var services = CreateServices(world);

        var schedule = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Spy,
            ActorFactionId = 1,
            SourceCityId = 1,
            TargetCityId = 2,
            TargetOfficerId = 202,
            SpyActionType = SpyActionType.Assassination,
            OfficerIds = new List<int> { 101 }
        });
        var pending = world.PendingCommands.Single(command => command.Type == CommandType.Spy);
        var result = InvokePrivateInstance<CommandResult>(
            services.Resolver,
            "ResolveSuccessfulSpyAction",
            world,
            1,
            world.GetCity(2)!,
            world.GetOfficer(101)!,
            SpyActionType.Assassination,
            202);

        Assert(schedule.Success, "Spy assassination designated target schedules", $"success={schedule.Success}");
        Assert(pending.TargetOfficerId == 202, "Spy assassination stores designated target", $"targetOfficer={pending.TargetOfficerId}");
        Assert(result.Success, "Spy assassination designated target resolves", $"success={result.Success}");
        Assert(world.GetOfficer(202)!.DeathYear == world.Year, "Spy assassination designated target marks chosen officer dead", $"deathYear={world.GetOfficer(202)!.DeathYear}");
        Assert(world.GetOfficer(201)!.DeathYear == 0, "Spy assassination designated target leaves ruler alive", $"deathYear={world.GetOfficer(201)!.DeathYear}");
        Assert(world.GetFaction(2)!.RulerOfficerId == 201, "Spy assassination designated target keeps existing ruler", $"ruler={world.GetFaction(2)!.RulerOfficerId}");
    }

    private static void RunSpyAssassinationPlayerSuccessionPendingTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "SpyCity", 1, 800, 800, 1200, new[] { 101 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "PlayerCity", 2, 900, 900, 1000, new[] { 201, 202, 203 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "SpyOfficer", 1, intelligence: 95, charm: 90));
        world.Officers.Add(TestHelpers.Officer(201, "PlayerRuler", 2, strength: 85, intelligence: 75, charm: 80, combat: 85));
        world.Officers.Add(TestHelpers.Officer(202, "LordSuccessor", 2, strength: 70, intelligence: 72, charm: 74, combat: 70));
        world.Officers.Add(TestHelpers.Officer(203, "GeneralSuccessor", 2, strength: 80, intelligence: 70, charm: 65, combat: 82));
        world.GetOfficer(202)!.Role = "Lord";
        world.Factions.Add(TestHelpers.Faction(1, "Enemy", false, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Player", true, 201, new[] { 201, 202, 203 }));
        var services = CreateServices(world);

        _ = InvokePrivateInstance<CommandResult>(
            services.Resolver,
            "ResolveSuccessfulSpyAction",
            world,
            1,
            world.GetCity(2)!,
            world.GetOfficer(101)!,
            SpyActionType.Assassination);

        var pending = world.GetPendingSuccession(2);
        Assert(pending != null, "Spy assassination creates player succession prompt", $"pending={(pending != null ? 1 : 0)}");
        Assert(world.GetFaction(2)!.RulerOfficerId == 0, "Spy assassination clears dead player ruler id", $"ruler={world.GetFaction(2)!.RulerOfficerId}");
        Assert(pending != null && pending.CandidateOfficerIds.SequenceEqual(new[] { 202, 203 }), "Spy assassination orders player succession candidates", pending == null ? "pending=null" : $"candidates={string.Join(',', pending.CandidateOfficerIds)}");

        var resolveResult = services.Resolver.ResolvePlayerSuccession(2, 202);
        Assert(resolveResult.Success, "Player succession resolves", $"success={resolveResult.Success}");
        Assert(world.GetFaction(2)!.RulerOfficerId == 202, "Player succession assigns selected ruler", $"ruler={world.GetFaction(2)!.RulerOfficerId}");
        Assert(world.GetOfficer(202)!.Role == "Lord", "Player succession assigns ruler role", $"role={world.GetOfficer(202)!.Role}");
        Assert(world.GetFaction(2)!.NameZhHant == "LordSuccessor軍", "Player succession updates faction name", $"name={world.GetFaction(2)!.NameZhHant}");
        Assert(world.GetPendingSuccession(2) == null, "Player succession clears pending record", $"pending={(world.GetPendingSuccession(2) != null ? 1 : 0)}");
    }

    private static void RunSpyAssassinationFactionCollapseTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "SpyCity", 1, 800, 800, 1200, new[] { 101 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "LastCity", 2, 900, 900, 1000, new[] { 201 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "SpyOfficer", 1, intelligence: 95, charm: 90));
        world.Officers.Add(TestHelpers.Officer(201, "LonelyRuler", 2, strength: 85, intelligence: 75, charm: 80, combat: 85));
        world.Factions.Add(TestHelpers.Faction(1, "Enemy", false, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Collapsed", false, 201, new[] { 201 }));
        world.DiplomacyRelations.Add(new DiplomacyRelationData
        {
            FactionAId = 1,
            FactionBId = 2,
            Status = DiplomacyStatusType.Truce,
            RemainingMonths = 3,
            RelationScore = 10
        });
        world.PendingCommands.Add(new PendingCommandData
        {
            Type = CommandType.Move,
            ActorFactionId = 2,
            SourceCityId = 2,
            TargetCityId = 1,
            OfficerIds = new List<int> { 201 }
        });
        var services = CreateServices(world);

        _ = InvokePrivateInstance<CommandResult>(
            services.Resolver,
            "ResolveSuccessfulSpyAction",
            world,
            1,
            world.GetCity(2)!,
            world.GetOfficer(101)!,
            SpyActionType.Assassination);

        Assert(world.GetFaction(2)!.RulerOfficerId == 0, "Faction collapse clears ruler id", $"ruler={world.GetFaction(2)!.RulerOfficerId}");
        Assert(world.GetFaction(2)!.OfficerIds.Count == 0, "Faction collapse clears faction officers", $"count={world.GetFaction(2)!.OfficerIds.Count}");
        Assert(world.GetCity(2)!.OwnerFactionId == 0, "Faction collapse neutralizes cities", $"owner={world.GetCity(2)!.OwnerFactionId}");
        Assert(world.DiplomacyRelations.Count == 0, "Faction collapse clears diplomacy relations", $"count={world.DiplomacyRelations.Count}");
        Assert(world.PendingCommands.Count == 0, "Faction collapse cancels faction pending commands", $"count={world.PendingCommands.Count}");
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
        Assert(pendingSuccession != null && pendingSuccession.CandidateOfficerIds.SequenceEqual(new[] { 102, 103, 104 }), "Attack ruler death orders player succession candidates", pendingSuccession == null ? "pending=null" : $"candidates={string.Join(',', pendingSuccession.CandidateOfficerIds)}");
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

    private static void RunSeasonalGoldTest()
    {
        var world = TestHelpers.World(month: 4);
        world.RandomSeed = 999;
        world.Cities.Add(TestHelpers.City(2, "AiGoldCity", 2, 1000, 1000, 1200, new[] { 201 }, Array.Empty<int>()));
        world.GetCity(2)!.DisasterPrevention = 120;
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
        world.RandomSeed = 999;
        world.Cities.Add(TestHelpers.City(2, "AiFoodCity", 2, 1000, 1000, 2000, new[] { 201 }, Array.Empty<int>()));
        world.GetCity(2)!.DisasterPrevention = 120;
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

    private static void RunOfficerProgressionBuffTest()
    {
        var officer = TestHelpers.Officer(900, "ProgressionOfficer", 1);

        Assert(officer.MilitaryRank == 0, "Officer progression initial military rank", $"rank={officer.MilitaryRank}");
        Assert(officer.StrategistRank == 0, "Officer progression initial strategist rank", $"rank={officer.StrategistRank}");
        Assert(officer.CivilRank == 0, "Officer progression initial civil rank", $"rank={officer.CivilRank}");
        Assert(string.IsNullOrWhiteSpace(officer.GeneralTitle), "Officer progression initial general title", $"title={officer.GeneralTitle}");
        Assert(string.IsNullOrWhiteSpace(officer.StrategistTitle), "Officer progression initial strategist title", $"title={officer.StrategistTitle}");
        Assert(string.IsNullOrWhiteSpace(officer.CivilTitle), "Officer progression initial civil title", $"title={officer.CivilTitle}");

        OfficerProgressionRules.AwardInternalAffairsExperience(officer, InternalAffairsJobType.Commercial, 220);
        OfficerProgressionRules.AwardBattleExperience(officer, 520);
        OfficerProgressionRules.AwardStrategistExperience(officer, 320);
        OfficerProgressionRules.AwardCivilExperience(officer, 220);

        Assert(officer.CommercialRank == 3, "Officer progression commercial rank", $"rank={officer.CommercialRank}");
        Assert(officer.MilitaryRank == 5, "Officer progression military rank", $"rank={officer.MilitaryRank}");
        Assert(officer.StrategistRank == 4, "Officer progression strategist rank", $"rank={officer.StrategistRank}");
        Assert(officer.CivilRank == 3, "Officer progression civil rank", $"rank={officer.CivilRank}");
        Assert(OfficerProgressionRules.GetStatBonus(officer, OfficerProgressionStat.Leadership) > 0, "Officer progression leadership buff", $"bonus={OfficerProgressionRules.GetStatBonus(officer, OfficerProgressionStat.Leadership)}");
        Assert(OfficerProgressionRules.GetStatBonus(officer, OfficerProgressionStat.Intelligence) > 0, "Officer progression intelligence buff", $"bonus={OfficerProgressionRules.GetStatBonus(officer, OfficerProgressionStat.Intelligence)}");
        Assert(OfficerProgressionRules.GetStatBonus(officer, OfficerProgressionStat.Politics) > 0, "Officer progression politics buff", $"bonus={OfficerProgressionRules.GetStatBonus(officer, OfficerProgressionStat.Politics)}");
        Assert(OfficerProgressionRules.GetInternalAffairsOutputBonus(officer, InternalAffairsJobType.Commercial) == 2, "Officer progression internal affairs output bonus", $"bonus={OfficerProgressionRules.GetInternalAffairsOutputBonus(officer, InternalAffairsJobType.Commercial)}");
    }

    private static void RunSpyAndDiplomacyProgressionSuccessBuffTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "SourceCity", 1, 1200, 1200, 1200, new[] { 101 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "TargetCity", 2, 1000, 1000, 1000, new[] { 201 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "EnvoySpy", 1, strength: 70, intelligence: 78, charm: 78, combat: 70));
        world.Officers.Add(TestHelpers.Officer(201, "TargetOfficer", 2, strength: 70, intelligence: 78, charm: 78, combat: 70));
        world.Factions.Add(TestHelpers.Faction(1, "FactionA", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "FactionB", false, 201, new[] { 201 }));
        var sourceCity = world.GetCity(1)!;
        var targetCity = world.GetCity(2)!;
        var officer = world.GetOfficer(101)!;
        var targetFaction = world.GetFaction(2)!;
        var relation = new DiplomacyRelationData
        {
            FactionAId = 1,
            FactionBId = 2,
            Status = DiplomacyStatusType.Neutral,
            RemainingMonths = 0,
            RelationScore = 0
        };

        var baseSpyChance = InvokePrivateStatic<int>(
            typeof(CommandResolver),
            "CalculateSpySuccessChance",
            world,
            sourceCity,
            targetCity,
            officer,
            SpyActionType.Sabotage);
        var baseDiplomacyChance = InvokePrivateStatic<int>(
            typeof(CommandResolver),
            "CalculateDiplomacySuccessChance",
            world,
            sourceCity,
            officer,
            targetFaction,
            relation,
            DiplomacyActionType.Alliance);

        OfficerProgressionRules.AwardSpyExperience(officer, 180);
        OfficerProgressionRules.AwardDiplomacyExperience(officer, 180);

        var rankedSpyChance = InvokePrivateStatic<int>(
            typeof(CommandResolver),
            "CalculateSpySuccessChance",
            world,
            sourceCity,
            targetCity,
            officer,
            SpyActionType.Sabotage);
        var rankedDiplomacyChance = InvokePrivateStatic<int>(
            typeof(CommandResolver),
            "CalculateDiplomacySuccessChance",
            world,
            sourceCity,
            officer,
            targetFaction,
            relation,
            DiplomacyActionType.Alliance);

        Assert(officer.SpyRank == 3, "Spy progression rank upgrade", $"rank={officer.SpyRank}");
        Assert(officer.DiplomacyRank == 3, "Diplomacy progression rank upgrade", $"rank={officer.DiplomacyRank}");
        Assert(!string.IsNullOrWhiteSpace(officer.SpyTitle), "Spy progression title upgrade", $"title={officer.SpyTitle}");
        Assert(!string.IsNullOrWhiteSpace(officer.DiplomacyTitle), "Diplomacy progression title upgrade", $"title={officer.DiplomacyTitle}");
        Assert(OfficerProgressionRules.GetSpySuccessBonus(officer) > 0, "Spy progression success bonus active", $"bonus={OfficerProgressionRules.GetSpySuccessBonus(officer)}");
        Assert(OfficerProgressionRules.GetDiplomacySuccessBonus(officer) > 0, "Diplomacy progression success bonus active", $"bonus={OfficerProgressionRules.GetDiplomacySuccessBonus(officer)}");
        Assert(rankedSpyChance > baseSpyChance, "Spy progression increases success chance", $"base={baseSpyChance}, ranked={rankedSpyChance}");
        Assert(rankedDiplomacyChance > baseDiplomacyChance, "Diplomacy progression increases success chance", $"base={baseDiplomacyChance}, ranked={rankedDiplomacyChance}");
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

    private static void RunFireOfficerTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101, 102 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Ruler", 1));
        world.Officers.Add(TestHelpers.Officer(102, "Officer", 1));
        world.Items.Add(new ItemData
        {
            Id = 1,
            NameEn = "Sword",
            NameZhHant = "寶劍",
            ItemType = ItemType.Weapon,
            OwnerFactionId = 1,
            EquippedOfficerId = 102
        });
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        var services = CreateServices(world);

        var result = services.Resolver.ExecuteFireOfficer(1, 1, 102);
        var rulerBlocked = services.Resolver.ExecuteFireOfficer(1, 1, 101);
        var city = world.GetCity(1)!;
        var faction = world.GetFaction(1)!;
        var officer = world.GetOfficer(102)!;
        var item = world.GetItem(1)!;

        Assert(result.Success, "Fire officer resolves", $"success={result.Success}");
        Assert(!city.OfficerIds.Contains(102), "Fire officer removes city assignment", $"cityHas={city.OfficerIds.Contains(102)}");
        Assert(!faction.OfficerIds.Contains(102), "Fire officer removes faction assignment", $"factionHas={faction.OfficerIds.Contains(102)}");
        Assert(FreeOfficerMovement.IsVisibleFreeOfficer(world, officer), "Fire officer becomes visible free officer", $"cityId={officer.CityId}, stay={officer.FreeOfficerStayMonths}");
        Assert(item.EquippedOfficerId == 0 && item.OwnerFactionId == 1, "Fire officer returns equipped item to faction inventory", $"equipped={item.EquippedOfficerId}, ownerFaction={item.OwnerFactionId}");
        Assert(!rulerBlocked.Success, "Fire officer blocks ruler", $"success={rulerBlocked.Success}");
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

        var result = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Search,
            ActorFactionId = 1,
            SourceCityId = 1,
            OfficerIds = new List<int> { 101 }
        });
        var resolved = services.Turn.ResolvePendingCommands(services.Resolver);
        var city = world.GetCity(1)!;
        var faction = world.GetFaction(1)!;
        var officer = world.GetOfficer(150)!;

        Assert(result.Success, "Civil investigation finds free officer", $"success={result.Success}");
        Assert(resolved.Count == 1 && resolved[0].Success, "Civil investigation finds officer month-end resolves", $"count={resolved.Count}");
        Assert(officer.CityId == 1, "Civil investigation reveals officer city", $"cityId={officer.CityId}");
        Assert(city.OfficerIds.Contains(150) == faction.OfficerIds.Contains(150), "Civil investigation keeps city/faction officer state consistent", $"cityHas={city.OfficerIds.Contains(150)}, factionHas={faction.OfficerIds.Contains(150)}");
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
        var result = hiddenServices.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Search,
            ActorFactionId = 1,
            SourceCityId = 1,
            OfficerIds = new List<int> { 101 }
        });
        var hiddenResolved = hiddenServices.Turn.ResolvePendingCommands(hiddenServices.Resolver);
        var hiddenOfficer = hiddenWorld.GetOfficer(151)!;
        var hiddenOfficerInCity = hiddenWorld.GetCity(1)!.OfficerIds.Contains(151);

        Assert(result.Success, "Civil investigation can discover hidden free officer", $"success={result.Success}");
        Assert(hiddenResolved.Count == 1 && hiddenResolved[0].Success, "Civil investigation hidden officer month-end resolves", $"count={hiddenResolved.Count}");
        Assert(hiddenOfficer.CityId is 0 or 1, "Hidden free officer investigation remains valid", $"cityId={hiddenOfficer.CityId}");
        Assert(hiddenOfficerInCity == (hiddenOfficer.CityId == 1), "Hidden free officer city membership stays consistent", $"cityHas={hiddenOfficerInCity}, cityId={hiddenOfficer.CityId}");

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
        var localization = new LocalizationService();
        localization.LoadFromFileSystem(GetLocalizationDirectoryPath());
        var resolver = new CommandResolver();
        resolver.Initialize(turn, new CombatResolver(), localization);
        var ai = new AiController();
        ai.Initialize(resolver, turn, localization);
        return (turn, resolver, ai);
    }

    private static string GetLocalizationDirectoryPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "data", "localization");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "data", "localization");
    }

    private static T InvokePrivateStatic<T>(Type targetType, string methodName, params object[] args)
    {
        var method = targetType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null)
        {
            throw new InvalidOperationException($"Method not found: {targetType.FullName}.{methodName}");
        }

        var result = method.Invoke(null, args);
        if (result is not T typedResult)
        {
            throw new InvalidOperationException($"Unexpected result type from {targetType.FullName}.{methodName}");
        }

        return typedResult;
    }

    private static T InvokePrivateInstance<T>(object target, string methodName, params object[] args)
    {
        var method = target.GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
        if (method == null)
        {
            throw new InvalidOperationException($"Method not found: {target.GetType().FullName}.{methodName}");
        }

        var result = method.Invoke(target, args);
        if (result is not T typedResult)
        {
            throw new InvalidOperationException($"Unexpected result type from {target.GetType().FullName}.{methodName}");
        }

        return typedResult;
    }

    private static WorldState CloneWorld(WorldState source)
    {
        var clone = new WorldState
        {
            Year = source.Year,
            Month = source.Month,
            RandomSeed = source.RandomSeed,
            ViewAllInformationEnabled = source.ViewAllInformationEnabled
        };

        clone.Cities.AddRange(source.Cities.Select(city => new CityData
        {
            Id = city.Id,
            Name = city.Name,
            NameEn = city.NameEn,
            NameZhHant = city.NameZhHant,
            OwnerFactionId = city.OwnerFactionId,
            Gold = city.Gold,
            Food = city.Food,
            Horses = city.Horses,
            Troops = city.Troops,
            InfantryTroops = city.InfantryTroops,
            SpearmanTroops = city.SpearmanTroops,
            CavalryTroops = city.CavalryTroops,
            ArcherTroops = city.ArcherTroops,
            CrossbowTroops = city.CrossbowTroops,
            SiegeTroops = city.SiegeTroops,
            Farm = city.Farm,
            Commercial = city.Commercial,
            Defense = city.Defense,
            DisasterPrevention = city.DisasterPrevention,
            Loyalty = city.Loyalty,
            HasBowWorkshop = city.HasBowWorkshop,
            HasSiegeWorkshop = city.HasSiegeWorkshop,
            OfficerIds = new List<int>(city.OfficerIds),
            ConnectedCityIds = new List<int>(city.ConnectedCityIds),
            LastSearchYear = city.LastSearchYear,
            LastSearchMonth = city.LastSearchMonth
        }));

        clone.Officers.AddRange(source.Officers.Select(officer => new OfficerData
        {
            Id = officer.Id,
            Name = officer.Name,
            NameZhHant = officer.NameZhHant,
            Role = officer.Role,
            Belongs = officer.Belongs,
            BirthYear = officer.BirthYear,
            DeathYear = officer.DeathYear,
            Strength = officer.Strength,
            Intelligence = officer.Intelligence,
            Charm = officer.Charm,
            Leadership = officer.Leadership,
            Politics = officer.Politics,
            Loyalty = officer.Loyalty,
            Ambition = officer.Ambition,
            Combat = officer.Combat,
            RelationshipType = officer.RelationshipType,
            CityId = officer.CityId,
            LastAssignedYear = officer.LastAssignedYear,
            LastAssignedMonth = officer.LastAssignedMonth,
            LastAssignedCommand = officer.LastAssignedCommand,
            MilitaryRank = officer.MilitaryRank,
            StrategistRank = officer.StrategistRank,
            CivilRank = officer.CivilRank,
            SpyRank = officer.SpyRank,
            DiplomacyRank = officer.DiplomacyRank,
            FarmRank = officer.FarmRank,
            CommercialRank = officer.CommercialRank,
            DefendRank = officer.DefendRank,
            DisasterPreventionRank = officer.DisasterPreventionRank,
            ConstructionRank = officer.ConstructionRank,
            BattleExperience = officer.BattleExperience,
            StrategistExperience = officer.StrategistExperience,
            CivilExperience = officer.CivilExperience,
            SpyExperience = officer.SpyExperience,
            DiplomacyExperience = officer.DiplomacyExperience,
            FarmExperience = officer.FarmExperience,
            CommercialExperience = officer.CommercialExperience,
            DefendExperience = officer.DefendExperience,
            DisasterPreventionExperience = officer.DisasterPreventionExperience,
            ConstructionExperience = officer.ConstructionExperience,
            GeneralTitle = officer.GeneralTitle,
            StrategistTitle = officer.StrategistTitle,
            CivilTitle = officer.CivilTitle,
            SpyTitle = officer.SpyTitle,
            DiplomacyTitle = officer.DiplomacyTitle,
            FarmTitle = officer.FarmTitle,
            CommercialTitle = officer.CommercialTitle,
            DefendTitle = officer.DefendTitle,
            DisasterPreventionTitle = officer.DisasterPreventionTitle,
            ConstructionTitle = officer.ConstructionTitle,
            FreeOfficerStayMonths = officer.FreeOfficerStayMonths
        }));

        clone.Factions.AddRange(source.Factions.Select(faction => new FactionData
        {
            Id = faction.Id,
            NameEn = faction.NameEn,
            NameZhHant = faction.NameZhHant,
            IsPlayer = faction.IsPlayer,
            RulerOfficerId = faction.RulerOfficerId,
            OfficerIds = new List<int>(faction.OfficerIds)
        }));

        clone.Items.AddRange(source.Items.Select(item => new ItemData
        {
            Id = item.Id,
            NameEn = item.NameEn,
            NameZhHant = item.NameZhHant,
            ItemType = item.ItemType,
            StrengthBonus = item.StrengthBonus,
            IntelligenceBonus = item.IntelligenceBonus,
            CharmBonus = item.CharmBonus,
            LeadershipBonus = item.LeadershipBonus,
            PoliticsBonus = item.PoliticsBonus,
            CombatBonus = item.CombatBonus,
            LoyaltyBonus = item.LoyaltyBonus,
            OwnerFactionId = item.OwnerFactionId,
            OwnerCityId = item.OwnerCityId,
            EquippedOfficerId = item.EquippedOfficerId,
            Rarity = item.Rarity
        }));

        clone.PendingSuccessionRecords.AddRange(source.PendingSuccessionRecords.Select(record => new WorldState.PendingSuccessionData
        {
            FactionId = record.FactionId,
            CandidateOfficerIds = new List<int>(record.CandidateOfficerIds)
        }));

        return clone;
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
            InfantryTroops = troops,
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
