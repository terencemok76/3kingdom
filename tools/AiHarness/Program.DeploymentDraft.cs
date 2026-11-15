using ThreeKingdom.Data;
using ThreeKingdom.UI;

namespace AiHarness;

internal static partial class Program
{
    private static void RunDeploymentDraftTests()
    {
        var draft = new DeploymentDraft();
        draft.SynchronizeSelection(new[] { 2, 1 }, TroopType.Infantry);
        draft.SetTroopCount(2, 40, 100);
        draft.SetTroopCount(1, 100, 100);
        Assert(draft.GetDeployment(1).TroopCount == 60, "deployment", "shared troop limit");
        draft.SetTroopType(1, TroopType.Cavalry, 25);
        Assert(draft.GetDeployment(1).TroopCount == 25, "deployment", "type change clamps count");
        var copy = draft.GetDeployment(2);
        copy.TroopCount = 1;
        Assert(draft.GetDeployment(2).TroopCount == 40, "deployment", "returned deployment is isolated");

        draft.SetEngineerTeams(90, 100);
        var selected = draft.TrySetEquipment(BattleEquipmentType.Ram, true, 100, out var required);
        Assert(selected && required == 30 && draft.Support.EngineerTeamCount == 70, "deployment", "equipment reserves engineers");
        var rejected = draft.TrySetEquipment(BattleEquipmentType.Ladder, true, 70, out required);
        Assert(!rejected && required == 80 && !draft.Support.Ladder && draft.Support.Ram, "deployment", "insufficient engineers leave support unchanged");
        var supportCopy = draft.Support;
        supportCopy.Ram = false;
        Assert(draft.Support.Ram, "deployment", "returned support is isolated");

        draft.BeginReinforcement();
        draft.SynchronizeSelection(new[] { 3 }, TroopType.Cavalry);
        draft.SetEngineerTeams(5, 5);
        draft.RestoreDefenders();
        Assert(draft.OfficerOrder.SequenceEqual(new[] { 2, 1 }) && !draft.ContainsOfficer(3) && draft.Support.Ram && draft.Support.EngineerTeamCount == 70,
            "deployment", "reinforcement restores defender draft");
        var deployments = draft.CreateDeployments();
        var allocation = DeploymentDraft.BuildTroopAllocation(deployments);
        var city = new CityData { InfantryTroops = 39, CavalryTroops = 25, EngineerTroops = 100, RamCount = 1 };
        var equipment = DeploymentDraft.BuildSiegeEngineAllocation(draft.Support);
        Assert(DeploymentValidation.Validate(deployments, allocation, draft.Support, equipment, city) == DeploymentValidationError.TroopsExceeded,
            "deployment", "validation rejects excess troops");
        city.InfantryTroops = 40;
        Assert(DeploymentValidation.Validate(deployments, allocation, draft.Support, equipment, city) == DeploymentValidationError.None,
            "deployment", "validation accepts available resources");
        var snapshot = draft.CreateSnapshot();
        draft.SetTroopCount(2, 10, 100);
        draft.TrySetEquipment(BattleEquipmentType.Ram, false, 100, out _);
        Assert(snapshot.Troops.Infantry == 40 && snapshot.Officers.Single(item => item.OfficerId == 2).TroopCount == 40 && snapshot.Support.Ram && snapshot.Equipment.Ram == 1 &&
               DeploymentValidation.Validate(snapshot, city) == DeploymentValidationError.None,
            "deployment", "snapshot remains consistent after draft changes");
        draft.Reset();
        Assert(draft.OfficerOrder.Count == 0 && draft.Support.TotalEngineerCount == 0 && !draft.Support.Ram, "deployment", "reset clears draft");

        var source = new CityData { Id = 1, OwnerFactionId = 1 };
        var target = new CityData { Id = 2, OwnerFactionId = 2, InfantryTroops = 100, Defense = 10 };
        var world = new WorldState();
        world.Cities.AddRange(new[] { source, target });
        world.DiplomacyRelations.Add(new DiplomacyRelationData { FactionAId = 1, FactionBId = 2, Status = DiplomacyStatusType.Alliance, RemainingMonths = 1 });
        Assert(AttackAdviceRules.Evaluate(world, source, target, 0, 0) == AttackAdviceKind.Prepare, "attack advice", "deployment takes priority");
        Assert(AttackAdviceRules.Evaluate(world, source, target, 100, 0) == AttackAdviceKind.BreakAlliance, "attack advice", "pact takes priority over food");
        world.DiplomacyRelations[0].RemainingMonths = 0;
        Assert(AttackAdviceRules.Evaluate(world, source, target, 100, 0) == AttackAdviceKind.LowFood, "attack advice", "food takes priority over intel");
        Assert(AttackAdviceRules.Evaluate(world, source, target, 100, 1) == AttackAdviceKind.NoIntel, "attack advice", "unknown enemy strength");
        world.UpsertCityIntel(1, 2, 1);
        Assert(AttackAdviceRules.Evaluate(world, source, target, 99, 1) == AttackAdviceKind.Unfavorable &&
               AttackAdviceRules.Evaluate(world, source, target, 100, 1) == AttackAdviceKind.Cautious &&
               AttackAdviceRules.Evaluate(world, source, target, 150, 1) == AttackAdviceKind.Favorable,
            "attack advice", "strength thresholds");
    }
}
