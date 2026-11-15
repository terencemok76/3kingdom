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
        Assert(OfficerAppointmentRules.HasAppointment(world.GetOfficer(202)!, OfficerAppointmentRules.Lord), "Spy assassination successor gets ruler appointment", string.Join(',', world.GetOfficer(202)!.Appointments));
        Assert(world.GetFaction(2)!.NameZhHant == "EnemyGeneral軍", "Spy assassination successor updates faction name", $"name={world.GetFaction(2)!.NameZhHant}");
        Assert(targetCity.OfficerIds.Contains(202), "Spy assassination keeps successor alive", $"cityHasSuccessor={targetCity.OfficerIds.Contains(202)}");
    }

    private static void RunCovertOfficerResponseTest()
    {
        var world = TestHelpers.World();
        world.Year = 190; world.Month = 1;
        world.Cities.Add(TestHelpers.City(1, "Home", 1, 3000, 500, 500, new[] { 101 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "Enemy", 2, 500, 500, 500, new[] { 201, 202 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Spy", 1, intelligence: 90));
        world.Officers.Add(TestHelpers.Officer(201, "Ruler", 2));
        world.Officers.Add(TestHelpers.Officer(202, "Target", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201, 202 }));
        world.OfficerAffectionRecords.Add(new WorldState.OfficerAffectionData { FactionId = 1, OfficerId = 202, Score = 80 });
        var target = world.GetOfficer(202)!;
        target.Loyalty = 95;
        var services = CreateServices(world);
        CommandRequest MakeRequest() => new() { Type = CommandType.Spy, ActorFactionId = 1, SourceCityId = 1, TargetCityId = 2, TargetOfficerId = 202, SpyActionType = SpyActionType.RecruitCovertOfficer, OfficerIds = new List<int> { 101 } };
        var scheduled = services.Resolver.Execute(MakeRequest());
        var pending = world.PendingCommands.Single();
        var result = services.Resolver.ResolvePendingCommand(pending);
        Assert(scheduled.Success && !result.Success && world.GetCity(1)!.Gold == 2700 && result.MessageZhHant.Contains("不會背棄主公") && world.CovertOfficerRecords.Count == 0, "Covert approach without intel reveals loyal refusal after payment", result.MessageZhHant);
        world.PendingCommands.Clear(); world.Month = 2;
        target.Loyalty = 20;
        world.GetOfficerAffection(1, 202)!.Score = 0;
        scheduled = services.Resolver.Execute(MakeRequest());
        result = services.Resolver.ResolvePendingCommand(world.PendingCommands.Single());
        Assert(scheduled.Success && !result.Success && world.GetCity(1)!.Gold == 2400 && result.MessageZhHant.Contains("交情尚淺"), "Covert approach with low affinity gets refusal after payment", result.MessageZhHant);
        world.PendingCommands.Clear(); world.Month = 3;
        scheduled = services.Resolver.Execute(MakeRequest());
        target.CityId = 1;
        result = services.Resolver.ResolvePendingCommand(world.PendingCommands.Single());
        Assert(scheduled.Success && !result.Success && world.GetCity(1)!.Gold == 2400, "Covert canceled before contact spends nothing", $"gold={world.GetCity(1)!.Gold}");
        target.CityId = 2;
        world.PendingCommands.Clear();
        var sample = new CommandResult { Success = true };
        typeof(CommandResolver).GetMethod("AppendCovertResponse", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(services.Resolver, new object[] { world, new PendingCommandData { SpyActionType = SpyActionType.RecruitCovertOfficer, ActorFactionId = 1, TargetOfficerId = 202 }, sample, true });
        Assert(sample.MessageZhHant.Contains("願暗中相助"), "Covert acceptance has officer speech", sample.MessageZhHant);
        world.ForceNextCovertRecruitmentForDebug = true;
        var saved = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        Assert(saved.ForceNextCovertRecruitmentForDebug, "Forced covert test survives save", "armed");
        var invalidPending = new PendingCommandData { Type = CommandType.Spy, ActorFactionId = 1, SourceCityId = 1, TargetCityId = 2, TargetFactionId = 2, TargetOfficerId = 201, SpyActionType = SpyActionType.RecruitCovertOfficer, OfficerIds = new List<int> { 101 } };
        var invalidResult = services.Resolver.ResolvePendingCommand(invalidPending);
        Assert(!invalidResult.Success && world.ForceNextCovertRecruitmentForDebug, "Invalid covert target preserves debug force", "ruler target");
        world.Officers.Add(TestHelpers.Officer(102, "PlayerTarget", 1));
        world.GetCity(1)!.OfficerIds.Add(102);
        world.GetFaction(1)!.OfficerIds.Add(102);
        world.GetOfficer(102)!.Loyalty = 100;
        var aiPending = new PendingCommandData { Type = CommandType.Spy, ActorFactionId = 2, SourceCityId = 2, TargetCityId = 1, TargetFactionId = 1, TargetOfficerId = 102, SpyActionType = SpyActionType.RecruitCovertOfficer, OfficerIds = new List<int> { 201 } };
        var aiResult = services.Resolver.ResolvePendingCommand(aiPending);
        Assert(!aiResult.Success && world.ForceNextCovertRecruitmentForDebug, "AI covert attempt does not consume player debug force", $"success={aiResult.Success}");
        world.Month = 4;
        target.Loyalty = 100;
        scheduled = services.Resolver.Execute(MakeRequest());
        result = services.Resolver.ResolvePendingCommand(world.PendingCommands.Single());
        Assert(scheduled.Success && result.Success && !world.ForceNextCovertRecruitmentForDebug && world.CovertOfficerRecords.Any(item => item.OfficerId == 202 && item.SponsorFactionId == 1) && result.MessageZhHant.Contains("願暗中相助"), "Debug guarantees covert recruitment without exposure and consumes switch", $"success={result.Success}");
    }

    private static void RunCovertOfficerRulesTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(2, "Enemy", 2, 0, 0, 0, new[] { 201, 202 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(201, "Ruler", 2));
        world.Officers.Add(TestHelpers.Officer(202, "Agent", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Sponsor", true, 101, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "Original", false, 201, new[] { 201, 202 }));
        var agent = new WorldState.CovertOfficerData { SponsorFactionId = 1, OriginalFactionId = 2, OfficerId = 202, AwaitingDefection = true };
        world.CovertOfficerRecords.Add(agent);
        Assert(CovertOfficerRules.IsValid(world, agent), "Covert officer accepts valid foreign officer", "valid");
        world.Officers.Add(TestHelpers.Officer(204, "Discord Target", 2));
        world.GetFaction(2)!.OfficerIds.Add(204);
        world.GetCity(2)!.OfficerIds.Add(204);
        Assert(!CovertOfficerRules.ScheduleDiscord(world, agent, 201), "Covert discord rejects ruler", "ruler");
        Assert(CovertOfficerRules.ScheduleDiscord(world, agent, 204) && !CovertOfficerRules.ScheduleDiscord(world, agent, 204), "Covert discord reserves one target", "scheduled");
        var pendingCopy = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        Assert(pendingCopy.CovertOfficerRecords.Single().DiscordTargetOfficerId == 204, "Covert discord schedule survives save", "saved");
        world.GetOfficer(204)!.CityId = 0;
        world.Month++;
        var discordResults = CovertOfficerRules.ProcessDiscord(world);
        Assert(discordResults.Count == 1 && discordResults[0].Kind == "cancelled" && agent.DiscordTargetOfficerId == 0, "Moved discord target cancels safely", "cancelled");
        world.GetOfficer(204)!.CityId = 2;
        var resolutionWorld = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        var resolutionAgent = resolutionWorld.CovertOfficerRecords.Single();
        var priorLoyalty = resolutionWorld.GetOfficer(204)!.Loyalty;
        var queued = CovertOfficerRules.ScheduleDiscord(resolutionWorld, resolutionAgent, 204);
        resolutionWorld.Month++;
        var resolved = CovertOfficerRules.ProcessDiscord(resolutionWorld);
        var loyaltyLoss = priorLoyalty - resolutionWorld.GetOfficer(204)!.Loyalty;
        Assert(queued && resolved.Count == 1 && loyaltyLoss >= 0 && loyaltyLoss <= 8 && CovertOfficerRules.ProcessDiscord(resolutionWorld).Count == 0 && (resolved[0].Kind != "exposed" || !resolutionWorld.CovertOfficerRecords.Contains(resolutionAgent)), "Covert discord resolves once and exposure removes agent", $"result={resolved[0].Kind}, loss={loyaltyLoss}");
        agent.AwaitingDefection = true;
        var split = CovertOfficerRules.SplitTroops(101);
        Assert(split.Joining == 60 && split.Withdrawing == 41, "Covert troop split conserves survivors", $"joining={split.Joining}, withdrawing={split.Withdrawing}");
        var copy = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        Assert(copy.CovertOfficerRecords.Single().AwaitingDefection, "Covert officer survives serialization", "saved");
        agent.OfficerId = 201;
        Assert(!CovertOfficerRules.IsValid(world, agent), "Covert officer rejects ruler", "ruler");
        world.CovertOfficerRecords.Clear();
        world.Cities.Add(TestHelpers.City(1, "Home", 1, 1000, 500, 500, new[] { 101 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Spy", 1, intelligence: 90));
        world.GetFaction(1)!.OfficerIds.Add(101);
        world.GetOfficer(202)!.Loyalty = 60;
        world.RecordCityOfficerIntel(1, 2);
        world.OfficerAffectionRecords.Add(new WorldState.OfficerAffectionData { FactionId = 1, OfficerId = 202, Score = 40 });
        var services = CreateServices(world);
        var scheduled = services.Resolver.Execute(new CommandRequest { Type = CommandType.Spy, ActorFactionId = 1, SourceCityId = 1, TargetCityId = 2, TargetOfficerId = 202, SpyActionType = SpyActionType.RecruitCovertOfficer, OfficerIds = new List<int> { 101 } });
        Assert(scheduled.Success && world.PendingCommands.Single().TargetOfficerId == 202, "Covert recruitment schedules target", $"success={scheduled.Success}");
        var success = InvokePrivateInstance<CommandResult>(services.Resolver, "ResolveSuccessfulSpyAction", world, 1, world.GetCity(2)!, world.GetOfficer(101)!, SpyActionType.RecruitCovertOfficer, 202);
        Assert(success.Success && world.CovertOfficerRecords.Count == 1, "Covert recruitment creates agent", $"count={world.CovertOfficerRecords.Count}");
        services.Turn.AdvanceMonth();
        Assert(world.HasActiveCityIntel(1, 2) && world.GetOfficerIntel(1, 202)?.Month == world.Month, "Covert officer reports city intel", $"month={world.Month}");
        var recruited = world.CovertOfficerRecords.Single();
        recruited.AwaitingDefection = true;
        var treasury = world.GetCity(1)!;
        treasury.Gold = 0;
        var unpaidEvents = CovertOfficerRules.ProcessMaintenance(world, true);
        Assert(recruited.MaintenanceUnpaid && unpaidEvents.Any(item => item.Kind == "unpaid"), "Unpaid covert maintenance suspends agent", "unpaid");
        var unpaidCopy = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        Assert(unpaidCopy.CovertOfficerRecords.Single().MaintenanceUnpaid, "Unpaid covert status survives save", "saved");
        treasury.Gold = 100;
        var paidEvents = CovertOfficerRules.ProcessMaintenance(world, false);
        Assert(!recruited.MaintenanceUnpaid && treasury.Gold == 0 && paidEvents.Any(item => item.Kind == "recovered"), "Covert maintenance recovery charges treasury once", "paid");
        var campaign = new ActiveBattleCampaignData { AttackerFactionId = 1, DefenderFactionId = 2, SourceCityId = 1, TargetCityId = 2, TotalBattleDays = 1, Stage = CampaignStage.FieldBattle };
        var team = new CampaignBattleTeamData { Id = 1, OfficerId = 202, FactionId = 2, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Ai, ActiveTroops = 101, MaximumTroops = 101, OriginCityId = 2, Location = CampaignTeamLocation.Field };
        campaign.Teams.Add(team);
        recruited.MaintenanceUnpaid = true;
        Assert(!CovertOfficerRules.TryDefect(world, campaign, team), "Unpaid agent cannot defect", "suspended");
        recruited.MaintenanceUnpaid = false;
        var originBefore = world.GetCity(2)!.Troops;
        Assert(CovertOfficerRules.TryDefect(world, campaign, team), "Covert officer defects in battle", "defender to attacker");
        Assert(team.Side == CampaignBattleSide.Attacker && team.ControllerType == CampaignControllerType.Player && team.ActiveTroops == 60 && world.GetCity(2)!.Troops == originBefore + 41, "Defection transfers control and returns remainder", $"active={team.ActiveTroops}");
        Assert(world.GetFaction(1)!.OfficerIds.Contains(202) && !world.GetFaction(2)!.OfficerIds.Contains(202) && world.CovertOfficerRecords.Count == 0, "Defection transfers strategic officer ownership", "faction roster");
        Assert(!CovertOfficerRules.TryDefect(world, campaign, team), "Defection cannot execute twice", "consumed agent");
        world.Officers.Add(TestHelpers.Officer(203, "PlayerAgent", 1));
        world.GetFaction(1)!.OfficerIds.Add(203);
        world.GetCity(1)!.OfficerIds.Add(203);
        world.CovertOfficerRecords.Add(new WorldState.CovertOfficerData { SponsorFactionId = 2, OriginalFactionId = 1, OfficerId = 203, AwaitingDefection = true });
        var reverseTeam = new CampaignBattleTeamData { Id = 3, OfficerId = 203, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, ActiveTroops = 100, MaximumTroops = 100, OriginCityId = 1, Location = CampaignTeamLocation.Field };
        campaign.Teams.Add(reverseTeam);
        Assert(CovertOfficerRules.TryDefect(world, campaign, reverseTeam) && reverseTeam.Side == CampaignBattleSide.Defender && reverseTeam.ControllerType == CampaignControllerType.Ai && reverseTeam.ActiveTroops == 60, "Defection applies same rules to AI control", $"side={reverseTeam.Side}, control={reverseTeam.ControllerType}");
        BattleCampaignService.CompleteCampaign(world, campaign, CampaignBattleSide.Attacker);
        Assert(world.GetCity(1)!.OfficerIds.Contains(202) && world.GetOfficer(202)!.CaptiveFactionId == 0 && world.GetFaction(1)!.OfficerIds.Contains(202), "Defector returns to new side after campaign", $"city={world.GetOfficer(202)!.CityId}");
    }

    private static void RunMultipleOfficerContactTest()
    {
        var world = TestHelpers.World();
        world.Year = 190; world.Month = 1;
        world.Cities.Add(TestHelpers.City(1, "Home", 1, 200, 500, 500, new[] { 101, 102, 103, 104 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "Enemy", 2, 500, 500, 500, new[] { 201, 202, 203, 204 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Ruler", 1));
        world.Officers.Add(TestHelpers.Officer(102, "Envoy1", 1));
        world.Officers.Add(TestHelpers.Officer(103, "Envoy2", 1));
        world.Officers.Add(TestHelpers.Officer(104, "Envoy3", 1));
        world.Officers.Add(TestHelpers.Officer(201, "EnemyRuler", 2));
        world.Officers.Add(TestHelpers.Officer(202, "Target1", 2));
        world.Officers.Add(TestHelpers.Officer(203, "Target2", 2));
        world.Officers.Add(TestHelpers.Officer(204, "Target3", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102, 103, 104 }));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201, 202, 203, 204 }));
        var services = CreateServices(world);
        var first = services.Resolver.ScheduleOfficerContact(1, 1, 202, 102, false);
        var sameEnvoy = services.Resolver.ScheduleOfficerContact(1, 1, 203, 102, false);
        var sameTarget = services.Resolver.ScheduleOfficerContact(1, 1, 202, 103, false);
        var second = services.Resolver.ScheduleOfficerContact(1, 1, 203, 103, true);
        var overBudget = services.Resolver.ScheduleOfficerContact(1, 1, 204, 104, false);
        var pending = world.PendingCommands.Where(p => p.Type == CommandType.ContactOfficer).ToList();
        Assert(first.Success && second.Success && pending.Count == 2 && pending.Select(p => p.TargetOfficerId).Order().SequenceEqual(new[] { 202, 203 }), "Multiple contacts from same city remain scheduled", $"count={pending.Count}");
        Assert(!sameEnvoy.Success && !sameTarget.Success, "Contact rejects reused envoy and target", $"envoy={sameEnvoy.Success}, target={sameTarget.Success}");
        Assert(!overBudget.Success && world.GetOfficer(104)!.LastAssignedYear != world.Year, "Contact rejects overcommitted funds without assigning envoy", $"success={overBudget.Success}");
        var localization = new LocalizationService();
        localization.LoadFromFileSystem(GetLocalizationDirectoryPath());
        Assert(localization.GetOfficerStatus(world, world.GetOfficer(102)!) == localization.T("status.contact"), "Contact envoy has contact status", "status");
        var saved = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        Assert(saved.PendingCommands.Count(p => p.Type == CommandType.ContactOfficer) == 2, "Multiple contacts survive save serialization", "pending=2");
        var results = services.Turn.ResolvePendingCommandsExceptAttack(services.Resolver);
        Assert(results.Count(result => result.Success) == 2 && world.GetOfficerAffection(1, 202)?.Score == 5 && world.GetOfficerAffection(1, 203)?.Score == 12 && world.GetCity(1)!.Gold == 0, "Multiple contacts resolve independently", $"gold={world.GetCity(1)!.Gold}");
    }

    private static void RunOfficerContactTest()
    {
        var world = TestHelpers.World();
        world.Year = 190; world.Month = 1;
        world.Cities.Add(TestHelpers.City(1, "Home", 1, 1000, 800, 1200, new[] { 101, 102 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "Foreign", 2, 900, 900, 1000, new[] { 201, 202 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Ruler", 1));
        world.Officers.Add(TestHelpers.Officer(102, "Envoy", 1));
        world.Officers.Add(TestHelpers.Officer(201, "EnemyRuler", 2));
        world.Officers.Add(TestHelpers.Officer(202, "Target", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201, 202 }));
        var services = CreateServices(world);
        var city = world.GetCity(1)!;
        var target = world.GetOfficer(202)!;
        var before = InvokePrivateStatic<int>(typeof(CommandResolver), "GetHireAcceptanceScore", world, city, target, 1, 0, 0, null!);
        var beforeGold = city.Gold;
        var scheduled = services.Resolver.ScheduleOfficerContact(1, 1, 202, 102, false);
        Assert(scheduled.Success && city.Gold == beforeGold, "Contact schedules without early charge", $"gold={city.Gold}");
        var pending = world.PendingCommands.Single(p => p.Type == CommandType.ContactOfficer);
        var result = services.Resolver.ResolvePendingCommand(pending);
        var score = world.GetOfficerAffection(1, 202)?.Score;
        Assert(result.Success && score == 5 && city.Gold == beforeGold - 50, "Letter resolves affinity and cost", $"score={score}, gold={city.Gold}");
        var after = InvokePrivateStatic<int>(typeof(CommandResolver), "GetHireAcceptanceScore", world, city, target, 1, 0, 0, null!);
        Assert(after == before + 1 && world.GetOfficerAffection(2, 202) == null, "Affinity boosts only actor hiring", $"before={before}, after={after}");
        Assert(!services.Resolver.ResolvePendingCommand(pending).Success, "Officer cannot be contacted twice in the same month", "same month");
        world.PendingCommands.Clear();
        world.Month = 2;
        Assert(services.Resolver.ScheduleOfficerContact(1, 1, 202, 102, true).Success, "Contact allowed next month", "month 2");
        world.PendingCommands.Clear();
        var saved = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        Assert(saved.GetOfficerAffection(1, 202)?.Score == 5, "Contact affinity survives serialization", "score=5");
        world.Month = 4;
        scheduled = services.Resolver.ScheduleOfficerContact(1, 1, 202, 102, true);
        pending = world.PendingCommands.Single(p => p.Type == CommandType.ContactOfficer);
        target.CityId = 1;
        beforeGold = city.Gold;
        result = services.Resolver.ResolvePendingCommand(pending);
        Assert(scheduled.Success && !result.Success && city.Gold == beforeGold && world.GetOfficerAffection(1, 202)?.Score == 5, "Contact cancels without charge when target moves", $"gold={city.Gold}");
        world.PendingCommands.Clear();
        target.CityId = 2;
        world.Month = 5;
        scheduled = services.Resolver.ScheduleOfficerContact(1, 1, 202, 102, true);
        pending = world.PendingCommands.Single(p => p.Type == CommandType.ContactOfficer);
        result = services.Resolver.ResolvePendingCommand(pending);
        Assert(scheduled.Success && result.Success && city.Gold == beforeGold - 150 && world.GetOfficerAffection(1, 202)?.Score == 17, "Gift applies cost and affinity", $"gold={city.Gold}, score={world.GetOfficerAffection(1, 202)?.Score}");
    }

    private static void RunOfficerIntelSnapshotTest()
    {
        var world = TestHelpers.World();
        world.Year = 190; world.Month = 3;
        world.Cities.Add(TestHelpers.City(1, "C", 2, 0, 0, 0, new[] { 201 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "D", 2, 0, 0, 0, Array.Empty<int>(), Array.Empty<int>()));
        var officer = TestHelpers.Officer(201, "A", 1);
        officer.Loyalty = 78;
        world.Officers.Add(officer);
        world.RecordCityOfficerIntel(1, 1);
        officer.CityId = 2; officer.Loyalty = 45;
        world.Year = 190; world.Month = 4;
        var record = world.GetOfficerIntel(1, 201);
        Assert(record != null && record.Loyalty == 78 && record.CityId == 1 && record.Month == 3, "Officer intel remains a dated snapshot after movement", $"loyalty={record?.Loyalty}");
        Assert(world.GetOfficerIntel(2, 201) == null, "Officer intel is faction-specific", "other viewer");
        var copy = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        Assert(copy.GetOfficerIntel(1, 201)?.Loyalty == 78, "Officer intel survives serialization", "saved snapshot");
        world.GetCity(2)!.OfficerIds.Add(201);
        world.RecordCityOfficerIntel(1, 2);
        record = world.GetOfficerIntel(1, 201);
        Assert(record != null && record.Loyalty == 45 && record.CityId == 2 && record.Month == 4 && world.OfficerIntelRecords.Count == 1, "Recon refreshes only the target officer", $"loyalty={record?.Loyalty}");
    }

    private static void RunSpySowDiscordTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "SpyCity", 1, 800, 800, 1200, new[] { 101 }, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "EnemyCity", 2, 900, 900, 1000, new[] { 201, 202 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Spy", 1));
        world.Officers.Add(TestHelpers.Officer(201, "Ruler", 2));
        world.Officers.Add(TestHelpers.Officer(202, "Target", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Actor", false, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Target", false, 201, new[] { 201, 202 }));
        var services = CreateServices(world);
        var request = new CommandRequest { Type = CommandType.Spy, ActorFactionId = 1, SourceCityId = 1, TargetCityId = 2, TargetOfficerId = 201, SpyActionType = SpyActionType.SowDiscord, OfficerIds = new List<int> { 101 } };
        Assert(!services.Resolver.Execute(request).Success, "Discord rejects ruler", "ruler target");
        request.TargetOfficerId = 202;
        Assert(services.Resolver.Execute(request).Success, "Discord schedules target", "officer target");
        var pending = world.PendingCommands.Single(command => command.Type == CommandType.Spy);
        Assert(pending.TargetOfficerId == 202, "Discord stores target", $"target={pending.TargetOfficerId}");
        var target = world.GetOfficer(202)!;
        target.Loyalty = 5;
        var result = InvokePrivateInstance<CommandResult>(services.Resolver, "ResolveSuccessfulSpyAction", world, 1, world.GetCity(2)!, world.GetOfficer(101)!, SpyActionType.SowDiscord, 202);
        Assert(result.Success && target.Loyalty == 0, "Discord lowers loyalty with floor", $"loyalty={target.Loyalty}");
        target.CityId = 1;
        result = InvokePrivateInstance<CommandResult>(services.Resolver, "ResolveSpy", world, world.GetCity(1)!, pending);
        Assert(!result.Success, "Discord cancels if target moved", "target moved");
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
        Assert(OfficerAppointmentRules.HasAppointment(world.GetOfficer(202)!, OfficerAppointmentRules.Lord), "Player succession assigns ruler appointment", string.Join(',', world.GetOfficer(202)!.Appointments));
        Assert(world.GetFaction(2)!.NameZhHant == "LordSuccessor", "Player succession updates faction name", $"name={world.GetFaction(2)!.NameZhHant}");
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

    private static void RunSpyLogPrivacyTest()
    {
        Assert(!LogPanelController.IsEntryVisible(true, true, false, false) &&
               !LogPanelController.IsEntryVisible(true, true, false, true) &&
               LogPanelController.IsEntryVisible(true, true, true, false) &&
               LogPanelController.IsEntryVisible(true, true, true, true) &&
               LogPanelController.IsEntryVisible(true, false, false, true),
            "God-only logs obey both filters and mode toggles", "hidden in normal mode; visible in god mode");
        var ownSpy = new PendingCommandData { Type = CommandType.Spy, ActorFactionId = 1, SpyActionType = SpyActionType.Reconnaissance };
        var foreignSpy = new PendingCommandData { Type = CommandType.Spy, ActorFactionId = 2, SpyActionType = SpyActionType.Reconnaissance };
        var foreignContact = new PendingCommandData { Type = CommandType.ContactOfficer, ActorFactionId = 2 };
        Assert(!SpyLogVisibilityRules.IsForeignSecretAction(ownSpy, 1) &&
               !SpyLogVisibilityRules.IsForeignSecretAction(new PendingCommandData { Type = CommandType.ContactOfficer, ActorFactionId = 1 }, 1) &&
               SpyLogVisibilityRules.IsForeignSecretAction(foreignSpy, 1) &&
               SpyLogVisibilityRules.IsForeignSecretAction(foreignContact, 1),
            "Own spy stays visible and foreign espionage stays secret", "spy and covert contact");
        var success = new CommandResult { Success = true };
        Assert(SpyLogVisibilityRules.GetVisibleEffectKey(foreignSpy, success, true) == string.Empty &&
               SpyLogVisibilityRules.GetVisibleEffectKey(new PendingCommandData { Type = CommandType.Spy, SpyActionType = SpyActionType.RecruitCovertOfficer }, success, true) == string.Empty &&
               SpyLogVisibilityRules.GetVisibleEffectKey(new PendingCommandData { Type = CommandType.Spy, SpyActionType = SpyActionType.Sabotage }, success, true) == "log.foreign_spy_damage" &&
               SpyLogVisibilityRules.GetVisibleEffectKey(new PendingCommandData { Type = CommandType.Spy, SpyActionType = SpyActionType.Sabotage }, new CommandResult { Success = false }, true) == string.Empty &&
               SpyLogVisibilityRules.GetVisibleEffectKey(new PendingCommandData { Type = CommandType.Spy, SpyActionType = SpyActionType.Sabotage }, success, false) == string.Empty,
            "Only observable successful damage is reported to the target", "recon and covert recruitment remain hidden");

        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "AiScoutCity", 1, 500, 800, 2000, new[] { 101 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "PlayerCity", 2, 1000, 1000, 1500, new[] { 201 }, new[] { 1 }));
        world.Officers.Add(TestHelpers.Officer(101, "SpyOfficer", 1, intelligence: 92, charm: 82));
        world.Officers.Add(TestHelpers.Officer(201, "PlayerRuler", 2));
        world.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "Player", true, 201, new[] { 201 }));
        var services = CreateServices(world);
        var fullResult = services.Ai.RunSingleCityDecision(1, 1);
        var secret = services.Ai.LastSpyDecisionResult;
        var publicResult = services.Ai.LastPublicDecisionResult;
        Assert(secret != null && world.PendingCommands.Any(command => command.Type == CommandType.Spy) &&
               fullResult.MessageEn.Contains(secret.MessageEn, StringComparison.Ordinal) &&
               (publicResult == null || !publicResult.MessageEn.Contains(secret.MessageEn, StringComparison.Ordinal)),
            "AI city summary separates spy scheduling from public actions", "complete result preserved for god mode");
        var mixedWorld = TestHelpers.World();
        mixedWorld.Cities.Add(TestHelpers.City(1, "AiCity", 1, 1000, 2000, 2000, new[] { 101, 102 }, new[] { 2 }));
        mixedWorld.Cities.Add(TestHelpers.City(2, "PlayerCity", 2, 1000, 1000, 1500, new[] { 201 }, new[] { 1 }));
        mixedWorld.Officers.Add(TestHelpers.Officer(101, "Spy", 1, intelligence: 95, charm: 90));
        mixedWorld.Officers.Add(TestHelpers.Officer(102, "Recruiter", 1, intelligence: 40, charm: 50));
        mixedWorld.Officers.Add(TestHelpers.Officer(201, "Player", 2));
        mixedWorld.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101, 102 }));
        mixedWorld.Factions.Add(TestHelpers.Faction(2, "Player", true, 201, new[] { 201 }));
        var mixedServices = CreateServices(mixedWorld);
        mixedServices.Ai.RunSingleCityDecision(1, 1);
        var mixedSecret = mixedServices.Ai.LastSpyDecisionResult;
        var mixedPublic = mixedServices.Ai.LastPublicDecisionResult;
        Assert(mixedSecret != null && mixedPublic != null && mixedWorld.PendingCommands.Any(command => command.Type == CommandType.Recruit) &&
               !mixedPublic.MessageEn.Contains(mixedSecret.MessageEn, StringComparison.Ordinal),
            "Public AI summary retains non-spy actions", "recruitment visible while spy is hidden");
        var sawExposed = false;
        var sawUnexposed = false;
        var exposureMatchesMessage = true;
        for (var seed = 1; seed <= 50 && (!sawExposed || !sawUnexposed); seed++)
        {
            var testWorld = CloneWorld(world);
            testWorld.RandomSeed = seed;
            var testServices = CreateServices(testWorld);
            var spy = world.PendingCommands.First(command => command.Type == CommandType.Spy);
            var spyResult = testServices.Resolver.ResolvePendingCommand(spy);
            sawExposed |= spyResult.SpyExposed;
            sawUnexposed |= !spyResult.SpyExposed;
            exposureMatchesMessage &= spyResult.SpyExposed == spyResult.MessageEn.Contains("exposed", StringComparison.OrdinalIgnoreCase);
        }
        Assert(sawExposed && sawUnexposed && exposureMatchesMessage, "Spy resolution exposes structured detection status", "detected and undetected seeds");
        var contactWorld = TestHelpers.World();
        contactWorld.Cities.Add(TestHelpers.City(1, "AI", 1, 1000, 1000, 1000, new[] { 101 }, Array.Empty<int>()));
        contactWorld.Cities.Add(TestHelpers.City(2, "Player", 2, 1000, 1000, 1000, new[] { 201, 202 }, Array.Empty<int>()));
        contactWorld.Officers.Add(TestHelpers.Officer(101, "Envoy", 1));
        contactWorld.Officers.Add(TestHelpers.Officer(201, "Ruler", 2));
        contactWorld.Officers.Add(TestHelpers.Officer(202, "Target", 2));
        contactWorld.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101 }));
        contactWorld.Factions.Add(TestHelpers.Faction(2, "Player", true, 201, new[] { 201, 202 }));
        var contact = new PendingCommandData { Type = CommandType.ContactOfficer, ActorFactionId = 1, SourceCityId = 1, TargetCityId = 2, TargetOfficerId = 202, GoldToSend = 150, OfficerIds = new List<int> { 101 } };
        var contactExposed = false;
        var contactHidden = false;
        var contactMatches = true;
        for (var seed = 1; seed <= 100 && (!contactExposed || !contactHidden); seed++)
        {
            var testWorld = CloneWorld(contactWorld);
            testWorld.RandomSeed = seed;
            var contactResult = CreateServices(testWorld).Resolver.ResolvePendingCommand(contact);
            contactExposed |= contactResult.SpyExposed;
            contactHidden |= !contactResult.SpyExposed;
            contactMatches &= contactResult.SpyExposed == contactResult.MessageEn.Contains("exposed", StringComparison.OrdinalIgnoreCase);
        }
        Assert(contactExposed && contactHidden && contactMatches, "Covert contact exposes structured detection status", "detected and undetected seeds");
    }
}
