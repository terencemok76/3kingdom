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
}
