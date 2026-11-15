using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace AiHarness;

internal static partial class Program
{
    private static void RunCommandHelperRulesTests()
    {
        var city = new CityData { InfantryTroops = 7, SpearmanTroops = 4, ArcherTroops = 3, EngineerTroops = 2 };
        var allocation = CommandTroopAllocationRules.CreateTroopAllocationFromTotal(city, 9);
        Assert(allocation.Infantry == 7 && allocation.Spearman == 2 && allocation.Total == 9, "command allocation", "troop allocation order");
        var scaled = CommandTroopAllocationRules.ScaleTroopAllocationToTotal(new TroopAllocationData { Infantry = 5, Cavalry = 3, Archer = 2 }, 7);
        Assert(scaled.Total == 7 && scaled.Infantry == 4 && scaled.Cavalry == 2 && scaled.Archer == 1, "command allocation", "proportional rounding");
        var clamped = CommandTroopAllocationRules.ClampTroopAllocationToCity(city, new TroopAllocationData { Infantry = 99, Spearman = -1, Siege = 99 });
        Assert(clamped.Infantry == 7 && clamped.Spearman == 0 && clamped.Siege == 2, "command allocation", "city stock limits");
        Assert(CommandTroopAllocationRules.ScaleTroopAllocationToTotal(allocation, 0).Total == 0, "command allocation", "zero allocation");

        var world = new WorldState();
        var oldBook = new ItemData { Id = 1, ItemType = ItemType.Book, OwnerFactionId = 1, EquippedOfficerId = 4 };
        var treasure = new ItemData { Id = 2, ItemType = ItemType.Treasure, OwnerFactionId = 1 };
        world.Items.AddRange(new[] { oldBook, treasure });
        OfficerItemRules.AssignItemToOfficer(world, treasure, 1, 4);
        Assert(oldBook.EquippedOfficerId == 0 && oldBook.OwnerFactionId == 1 && treasure.EquippedOfficerId == 4, "officer items", "shared special slot replacement");
        OfficerItemRules.TransferEquippedItemsToFaction(world, 4, 2);
        Assert(treasure.OwnerFactionId == 2 && treasure.EquippedOfficerId == 4 && treasure.OwnerCityId == 0, "officer items", "transfer preserves equipment");
        OfficerItemRules.MoveItemToFactionInventory(treasure, 2);
        Assert(OfficerItemRules.IsItemOwnedByFactionInventory(treasure, 2) && treasure.EquippedOfficerId == 0, "officer items", "return to inventory");

        var planningWorld = new WorldState();
        var planningCity = new CityData { Id = 5, Farm = 20, Commercial = 90, Defense = 90, DisasterPrevention = 90, Food = 100, Gold = 2000, Population = 50000 };
        var plan = InternalAffairsPlanningRules.ChooseAuthorizedPlanJob(planningWorld, planningCity);
        Assert(plan == InternalAffairsJobType.Farm && InternalAffairsPlanningRules.ChooseAuthorizedPlanDuration(planningCity, plan.Value) == 4,
            "internal affairs planning", "low farm selected with four month duration");
        planningWorld.InternalAffairsSchedules.Add(new InternalAffairsScheduleData { CityId = 5, JobType = InternalAffairsJobType.Farm, State = InternalAffairsScheduleState.Active });
        Assert(InternalAffairsPlanningRules.ChooseAuthorizedPlanJob(planningWorld, planningCity) != InternalAffairsJobType.Farm,
            "internal affairs planning", "active job excluded");
        Assert(InternalAffairsPlanningRules.GetRecommendedInternalAffairsGold(InternalAffairsJobType.Survey, 3) == 120 &&
               InternalAffairsPlanningRules.GetInternalAffairsMonthlyGoldCost(120, 3, 2) == 120 &&
               InternalAffairsPlanningRules.GetInternalAffairsMonthlyGoldCost(120, 3, 0) == 0,
            "internal affairs planning", "monthly investment and completed schedule");

        var successionWorld = new WorldState { Year = 200 };
        var ruler = new OfficerData { Id = 1, Name = "Ruler" };
        ruler.RelationshipType["Child"] = "family,blood";
        var child = new OfficerData { Id = 2, Name = "Child", CityId = 8, Politics = 20, Loyalty = 30 };
        var capable = new OfficerData { Id = 3, Name = "Capable", CityId = 8, Politics = 90, Intelligence = 90, Loyalty = 90 };
        var captive = new OfficerData { Id = 4, Name = "Captive", CityId = 8, Politics = 100, CaptiveFactionId = 2 };
        var dead = new OfficerData { Id = 5, Name = "Dead", CityId = 8, Politics = 100, DeathYear = 199 };
        successionWorld.Officers.AddRange(new[] { ruler, child, capable, captive, dead });
        var faction = new FactionData { Id = 1, RulerOfficerId = 1, OfficerIds = new() { 2, 3, 4, 5 } };
        successionWorld.Factions.Add(faction);
        var succession = OfficerSuccessionAndPrefectRules.GetSuccessionCandidates(successionWorld, faction, ruler);
        Assert(succession.SequenceEqual(new[] { 2, 3 }), "succession", "blood relation first; captive and dead excluded");
        var prefectCity = new CityData { Id = 8, OwnerFactionId = 1, OfficerIds = new() { 1, 2, 3, 5 } };
        Assert(OfficerSuccessionAndPrefectRules.GetBestAutoPrefectCandidate(successionWorld, prefectCity, faction)?.Id == 3,
            "prefect selection", "best eligible candidate selected");

        var diplomacyWorld = new WorldState { Year = 200, Month = 4 };
        var relation = CommandDiplomacyRules.GetOrCreateDiplomacyRelation(diplomacyWorld, 9, 2);
        relation.Status = DiplomacyStatusType.Alliance;
        relation.RemainingMonths = 3;
        relation.RelationScore = -95;
        Assert(ReferenceEquals(relation, CommandDiplomacyRules.GetOrCreateDiplomacyRelation(diplomacyWorld, 2, 9)) &&
               CommandDiplomacyRules.HasActiveDiplomacyBlock(diplomacyWorld, 9, 2), "diplomacy", "canonical relation and active pact");
        Assert(CommandDiplomacyRules.TryBreakDiplomacyBlockForAttack(diplomacyWorld, 9, 2) && relation.Status == DiplomacyStatusType.Neutral &&
               relation.RemainingMonths == 0 && relation.RelationScore == -100 && relation.TrustFromAToB == 15 && relation.TrustFromBToA == 50 &&
               relation.LastUpdatedYear == 200 && relation.LastUpdatedMonth == 4, "diplomacy", "break pact affects defender trust and clamps score");
        Assert(!CommandDiplomacyRules.TryBreakDiplomacyBlockForAttack(diplomacyWorld, 9, 2), "diplomacy", "inactive pact cannot be broken twice");

        var assignmentWorld = new WorldState { Year = 200, Month = 4 };
        var assigned = new OfficerData { Id = 20, CityId = 6 };
        var free = new OfficerData { Id = 21, CityId = 6 };
        assignmentWorld.Officers.AddRange(new[] { assigned, free });
        var assignmentCity = new CityData { Id = 6, OfficerIds = new() { 20, 21 } };
        Assert(CommandOfficerAssignmentRules.GetMovableOfficerIds(assignmentCity, new() { 21, 20, 21, 99 }).SequenceEqual(new[] { 21, 20 }),
            "officer assignment", "movement preserves requested order and removes invalid duplicates");
        CommandOfficerAssignmentRules.MarkOfficerAssigned(assignmentWorld, assigned, CommandType.Spy);
        Assert(!CommandOfficerAssignmentRules.AreOfficerIdsAvailableForPendingOrder(assignmentWorld, new() { 20 }) &&
               CommandOfficerAssignmentRules.GetSingleAvailableOfficer(assignmentWorld, assignmentCity, new() { 21 })?.Id == 21,
            "officer assignment", "monthly assignment blocks reuse");
        assignmentWorld.Month = 5;
        assignmentWorld.InternalAffairsSchedules.Add(new InternalAffairsScheduleData { OfficerId = 20, State = InternalAffairsScheduleState.Active });
        Assert(!CommandOfficerAssignmentRules.AreOfficerIdsAvailableForPendingOrder(assignmentWorld, new() { 20 }),
            "officer assignment", "active internal affairs blocks reuse next month");
        assignmentWorld.InternalAffairsSchedules[0].State = InternalAffairsScheduleState.Terminated;
        assignmentWorld.PendingCommands.Add(new PendingCommandData { Type = CommandType.Move, IsTraveling = true, OfficerIds = new() { 20 } });
        Assert(!CommandOfficerAssignmentRules.AreOfficerIdsAvailableForPendingOrder(assignmentWorld, new() { 20 }) &&
               CommandOfficerAssignmentRules.AreOfficerIdsAvailableForPendingOrder(assignmentWorld, new()),
            "officer assignment", "traveling officer blocked and empty order allowed");
    }
}
