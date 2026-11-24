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

    private static void RunAssignOfficerAppointmentTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101, 102 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Ruler", 1));
        world.Officers.Add(TestHelpers.Officer(102, "Officer", 1));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        var services = CreateServices(world);

        var result = services.Resolver.ExecuteAssignOfficerAppointment(1, 1, 102, OfficerAppointmentRules.Governor);
        var blocked = services.Resolver.ExecuteAssignOfficerAppointment(1, 1, 101, OfficerAppointmentRules.Governor);
        var officer = world.GetOfficer(102)!;

        Assert(result.Success, "Assign officer appointment resolves", $"success={result.Success}");
        Assert(OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Governor), "Assign officer appointment applies", string.Join(',', officer.Appointments));
        Assert(!blocked.Success, "Assign officer appointment blocks ruler", $"success={blocked.Success}");
    }

    private static void RunAiOfficerHireTest()
    {
        var world = TestHelpers.World(year: 200, month: 2);
        world.Cities.Add(TestHelpers.City(1, "AI City", 1, 1600, 5000, 3000, new[] { 101 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "AI Ruler", 1));
        world.Officers.Add(TestHelpers.Officer(102, "Free", 1, charm: 75));
        world.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101 }));
        var services = CreateServices(world);
        var result = services.Ai.RunSingleCityDecision(1, 1);
        var hire = world.PendingCommands.FirstOrDefault(command => command.Type == CommandType.HireOfficer);
        Assert(result.Success && hire?.ActorFactionId == 1 && hire.TargetOfficerId == 102 && hire.OfficerIds.SequenceEqual(new[] { 101 }) &&
               world.GetOfficer(101)!.LastAssignedYear == world.Year && world.GetOfficer(101)!.LastAssignedMonth == world.Month,
            "AI schedules local free officer hire with available envoy", hire == null ? "no hire order" : $"target={hire.TargetOfficerId}, envoy={hire.OfficerIds[0]}");
        Assert(hire != null && MapCommandPresentationRules.GetFactionCommands(world, 1, 2).Contains(hire),
            "AI hire enters map presentation queue", "AI recruitment visible");
        var beforeGold = world.GetCity(1)!.Gold;
        var resolved = services.Turn.ResolvePendingCommands(services.Resolver);
        Assert(resolved.Any(item => item.Success) && world.GetFaction(1)!.OfficerIds.Contains(102) && world.GetCity(1)!.OfficerIds.Contains(102) &&
               world.GetCity(1)!.Gold == beforeGold - 200 - (hire?.GoldToSend ?? 0),
            "AI hire resolves using normal transfer and cost", $"gold={beforeGold}->{world.GetCity(1)!.Gold}");

        var enemyWorld = TestHelpers.World(year: 200, month: 2);
        enemyWorld.Cities.Add(TestHelpers.City(1, "AI City", 1, 1600, 5000, 3000, new[] { 101 }, new[] { 2 }));
        enemyWorld.Cities.Add(TestHelpers.City(2, "Enemy City", 2, 1000, 5000, 3000, new[] { 201, 202 }, new[] { 1 }));
        enemyWorld.Officers.Add(TestHelpers.Officer(101, "AI Ruler", 1));
        enemyWorld.Officers.Add(TestHelpers.Officer(201, "Enemy Ruler", 2));
        enemyWorld.Officers.Add(TestHelpers.Officer(202, "Enemy Officer", 2, charm: 75));
        enemyWorld.GetOfficer(202)!.Loyalty = 95;
        enemyWorld.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101 }));
        enemyWorld.Factions.Add(TestHelpers.Faction(2, "Enemy", true, 201, new[] { 201, 202 }));
        var enemyServices = CreateServices(enemyWorld);
        var method = typeof(AiController).GetMethod("TryIssueOfficerHire", BindingFlags.NonPublic | BindingFlags.Instance)!;
        CommandResult? TryHire() => (CommandResult?)method.Invoke(enemyServices.Ai, new object[] { enemyWorld, enemyWorld.GetCity(1)!, 1, new List<int> { 101 } });
        Assert(TryHire() == null, "AI does not hire foreign officer without city intelligence", "unseen city");
        enemyWorld.CityIntelRecords.Add(new WorldState.CityIntelData { ViewerFactionId = 1, TargetCityId = 2, RemainingMonths = 3 });
        Assert(TryHire() == null, "AI does not read unknown foreign loyalty", "no officer intelligence");
        enemyWorld.OfficerIntelRecords.Add(new WorldState.OfficerIntelData { ViewerFactionId = 1, OfficerId = 202, CityId = 2, Loyalty = 40, Year = 199, Month = 1 });
        var estimated = TryHire();
        var enemyHire = enemyWorld.PendingCommands.FirstOrDefault(command => command.Type == CommandType.HireOfficer);
        Assert(estimated?.Success == true && enemyHire?.TargetOfficerId == 202, "AI uses stored loyalty snapshot", "observed 40, actual 95");
        Assert(enemyHire != null && HudController.IsPlayerRelatedPendingCommandForWorld(enemyWorld, enemyHire, 2),
            "AI hire targeting player officer is player-related before resolution", "player log retains outcome");
        var actual = enemyHire == null ? null : enemyServices.Resolver.ResolvePendingCommand(enemyHire);
        Assert(actual?.Success == false && enemyWorld.GetFaction(2)!.OfficerIds.Contains(202),
            "Outdated intelligence does not guarantee recruitment", "actual loyalty evaluated at resolution");

        var poorWorld = TestHelpers.World(year: 200, month: 2);
        poorWorld.Cities.Add(TestHelpers.City(1, "Poor AI", 1, 799, 5000, 3000, new[] { 101 }, Array.Empty<int>()));
        poorWorld.Officers.Add(TestHelpers.Officer(101, "Ruler", 1));
        poorWorld.Officers.Add(TestHelpers.Officer(102, "Free", 1));
        poorWorld.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101 }));
        var poorServices = CreateServices(poorWorld);
        var lowFunds = (CommandResult?)method.Invoke(poorServices.Ai, new object[] { poorWorld, poorWorld.GetCity(1)!, 1, new List<int> { 101 } });
        Assert(lowFunds == null && !poorWorld.PendingCommands.Any(command => command.Type == CommandType.HireOfficer), "AI preserves its hire treasury reserve", "799 gold");
        poorWorld.GetCity(1)!.Gold = 1600;
        poorWorld.PendingCommands.Add(new PendingCommandData { Type = CommandType.Move, ActorFactionId = 1, SourceCityId = 1, GoldToSend = 900 });
        var reservedHire = (CommandResult?)method.Invoke(poorServices.Ai, new object[] { poorWorld, poorWorld.GetCity(1)!, 1, new List<int> { 101 } });
        Assert(reservedHire == null, "AI accounts for gold scheduled for transport", "1600 minus 900 leaves insufficient reserve");

        var noAttackWorld = TestHelpers.World(year: 200, month: 2);
        noAttackWorld.Cities.Add(TestHelpers.City(1, "AI City", 1, 1600, 5000, 3000, new[] { 101 }, Array.Empty<int>()));
        noAttackWorld.Officers.Add(TestHelpers.Officer(101, "Ruler", 1));
        noAttackWorld.Officers.Add(TestHelpers.Officer(102, "Free", 1));
        noAttackWorld.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101 }));
        var noAttackServices = CreateServices(noAttackWorld);
        noAttackServices.Ai.RunSingleCityDecision(1, 1);
        noAttackServices.Turn.ResolvePendingCommandsExceptAttack(noAttackServices.Resolver);
        Assert(noAttackWorld.GetFaction(1)!.OfficerIds.Contains(102), "Non-attack turn resolution completes AI hire", "free officer joins");

        var reservedWorld = TestHelpers.World(year: 200, month: 2);
        reservedWorld.Cities.Add(TestHelpers.City(1, "AI City", 1, 1600, 5000, 3000, new[] { 101 }, Array.Empty<int>()));
        reservedWorld.Officers.Add(TestHelpers.Officer(101, "Ruler", 1));
        reservedWorld.Officers.Add(TestHelpers.Officer(102, "Free", 1));
        reservedWorld.Factions.Add(TestHelpers.Faction(1, "AI", false, 101, new[] { 101 }));
        reservedWorld.PendingCommands.Add(new PendingCommandData { Type = CommandType.HireOfficer, ActorFactionId = 1, SourceCityId = 2, TargetOfficerId = 102 });
        var reservedServices = CreateServices(reservedWorld);
        var duplicate = (CommandResult?)method.Invoke(reservedServices.Ai, new object[] { reservedWorld, reservedWorld.GetCity(1)!, 1, new List<int> { 101 } });
        Assert(duplicate == null && reservedWorld.PendingCommands.Count(command => command.Type == CommandType.HireOfficer) == 1,
            "AI does not duplicate its pending target", "one order per target");
        reservedWorld.PendingCommands.Clear();
        var noEnvoy = (CommandResult?)method.Invoke(reservedServices.Ai, new object[] { reservedWorld, reservedWorld.GetCity(1)!, 1, new List<int>() });
        Assert(noEnvoy == null && !reservedWorld.PendingCommands.Any(command => command.Type == CommandType.HireOfficer),
            "AI cannot hire without an available envoy", "no order scheduled");
        reservedWorld.GetOfficer(101)!.LastAssignedYear = reservedWorld.Year;
        reservedWorld.GetOfficer(101)!.LastAssignedMonth = reservedWorld.Month;
        reservedWorld.Officers.Add(TestHelpers.Officer(103, "Available", 1, charm: 30));
        reservedWorld.GetCity(1)!.OfficerIds.Add(103);
        reservedWorld.GetFaction(1)!.OfficerIds.Add(103);
        var freshEnvoy = (CommandResult?)method.Invoke(reservedServices.Ai, new object[] { reservedWorld, reservedWorld.GetCity(1)!, 1, new List<int> { 101, 103 } });
        Assert(freshEnvoy?.Success == true && reservedWorld.PendingCommands.Any(command => command.Type == CommandType.HireOfficer && command.OfficerIds.SequenceEqual(new[] { 103 })),
            "AI rechecks envoy availability after other orders", "assigned officer skipped");
    }

    private static void RunAppointmentOverviewTest()
    {
        var world = TestHelpers.World(month: 2);
        var ids = new[] { 101, 102, 103, 104, 105 };
        world.Cities.Add(TestHelpers.City(1, "Capital", 1, 1000, 1000, 1000, ids, Array.Empty<int>()));
        world.Cities.Add(TestHelpers.City(2, "Empty", 1, 1000, 1000, 1000, Array.Empty<int>(), Array.Empty<int>()));
        foreach (var id in ids) world.Officers.Add(TestHelpers.Officer(id, $"Officer{id}", 1));
        var faction = TestHelpers.Faction(1, "Faction", true, 101, ids);
        faction.ChancellorOfficerId = 102;
        faction.ChiefStrategistOfficerId = 103;
        world.Factions.Add(faction);
        OfficerAppointmentRules.AddAppointment(world.GetOfficer(102)!, OfficerAppointmentRules.Governor);
        OfficerAppointmentRules.AddAppointment(world.GetOfficer(104)!, OfficerAppointmentRules.GrandGeneral);
        foreach (var id in new[] { 102, 104, 105 }) OfficerAppointmentRules.AddAppointment(world.GetOfficer(id)!, OfficerAppointmentRules.Strategist);
        var rows = AppointmentOverviewRules.Build(world, faction);
        Assert(rows.Any(row => row.Appointment == OfficerAppointmentRules.Chancellor && row.OfficerId == 102 && row.CityId == 1 && row.Used == 1 && row.Quota == 1) &&
               rows.Any(row => row.Appointment == OfficerAppointmentRules.ChiefStrategist && row.OfficerId == 103),
            "Overview reads faction-level appointment holders", "chancellor and chief");
        Assert(rows.Any(row => row.Appointment == OfficerAppointmentRules.GrandGeneral && row.OfficerId == 104 && row.EffectKey == "ui.military_effect_grand_general") &&
               rows.Any(row => row.Appointment == OfficerAppointmentRules.VanguardGeneral && row.OfficerId == 0 && row.Used == 0),
            "Overview includes military effect and vacancy", "appointed and vacant");
        Assert(rows.Count(row => row.Appointment == OfficerAppointmentRules.Strategist && row.Used == 3 && row.Quota == 2) == 3,
            "Overview preserves all over-quota strategists", "3/2");
        Assert(rows.Any(row => row.Appointment == OfficerAppointmentRules.Governor && row.CityId == 1 && row.OfficerId == 102) &&
               rows.Any(row => row.Appointment == OfficerAppointmentRules.Governor && row.CityId == 2 && row.OfficerId == 0),
            "Overview lists each city and vacant governor", "two cities");
    }

    private static void RunStrategistAppointmentTest()
    {
        var world = TestHelpers.World(month: 2);
        var ids = new[] { 101, 102, 103, 104, 105 };
        world.Cities.Add(TestHelpers.City(1, "City", 1, 1000, 1000, 1000, ids, Array.Empty<int>()));
        foreach (var id in ids) world.Officers.Add(TestHelpers.Officer(id, $"Officer{id}", 1));
        var faction = TestHelpers.Faction(1, "Faction", false, 101, ids);
        world.Factions.Add(faction);
        var services = CreateServices(world);
        var resolver = services.Resolver;
        Assert(resolver.ExecuteAssignOfficerAppointment(1, 1, 102, OfficerAppointmentRules.Strategist).Success &&
               resolver.ExecuteAssignOfficerAppointment(1, 1, 103, OfficerAppointmentRules.Strategist).Success,
            "Two strategists can be assigned", "2/2");
        Assert(!resolver.ExecuteAssignOfficerAppointment(1, 1, 104, OfficerAppointmentRules.Strategist).Success &&
               OfficerAppointmentRules.CountStrategists(world, faction) == 2, "Strategist quota enforced", "2/2");
        Assert(OfficerAppointmentRules.GetStrategySuccessBonus(world.GetOfficer(102), faction) == 5, "Strategist success bonus", "+5 points");
        Assert(resolver.ExecuteAssignFactionAdvisor(1, 1, 102, OfficerAppointmentRules.ChiefStrategist).Success &&
               !OfficerAppointmentRules.HasAppointment(world.GetOfficer(102)!, OfficerAppointmentRules.Strategist) &&
               OfficerAppointmentRules.GetStrategySuccessBonus(world.GetOfficer(102), faction) == 8,
            "Chief replaces strategist and grants chief bonus", "+8 points");
        var activeChief = new[] { (OfficerId: 102, TeamName: "A", TroopCount: 100) };
        var present = OfficerAppointmentRules.IsChiefStrategistPresent(faction, "A", activeChief);
        Assert(present && OfficerAppointmentRules.GetStrategySuccessBonus(world.GetOfficer(105), faction, present) == 3 &&
               OfficerAppointmentRules.GetStrategySuccessBonus(world.GetOfficer(103), faction, present) == 5 &&
               OfficerAppointmentRules.GetStrategySuccessBonus(world.GetOfficer(102), faction, present) == 8,
            "Chief supports allies without stacking strategist bonus", "ordinary +3, strategist +5, chief +8");
        Assert(!OfficerAppointmentRules.IsChiefStrategistPresent(faction, "A", Array.Empty<(int, string, int)>()) &&
               !OfficerAppointmentRules.IsChiefStrategistPresent(faction, "A", new[] { (102, "A", 0) }) &&
               !OfficerAppointmentRules.IsChiefStrategistPresent(faction, "A", new[] { (102, "B", 100) }) &&
               OfficerAppointmentRules.GetStrategySuccessBonus(world.GetOfficer(105), faction) == 0,
            "Absent, defeated or opposing chief gives no support", "absent, zero troops, opposing side");
        var otherFaction = TestHelpers.Faction(2, "Other", false, 201, new[] { 201 });
        Assert(!OfficerAppointmentRules.IsChiefStrategistPresent(otherFaction, "A", activeChief),
            "Chief does not support a different faction", "same side is insufficient");
        Assert(resolver.ExecuteAssignOfficerAppointment(1, 1, 104, OfficerAppointmentRules.Strategist).Success &&
               !resolver.ExecuteAssignOfficerAppointment(1, 1, 102, OfficerAppointmentRules.Strategist).Success &&
               faction.ChiefStrategistOfficerId == 102, "Full quota preserves chief on failed reassignment", "chief preserved");
        var copy = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world));
        Assert(copy != null && OfficerAppointmentRules.CountStrategists(copy, copy.GetFaction(1)!) == 2 &&
               OfficerAppointmentRules.GetStrategySuccessBonus(copy.GetOfficer(102), copy.GetFaction(1)) == 8,
            "Strategist appointments survive serialization", "round trip");
        var old = System.Text.Json.JsonSerializer.Deserialize<OfficerData>("{\"Id\":9,\"Name\":\"Old\"}");
        Assert(old != null && old.Appointments.Count == 0, "Legacy officer has no new appointment", "legacy JSON");
        resolver.ExecuteClearOfficerAppointment(1, 1, 103, OfficerAppointmentRules.Strategist);
        services.Ai.RunFactionAppointmentDecisions(1);
        Assert(OfficerAppointmentRules.CountStrategists(world, faction) == 2 &&
               !OfficerAppointmentRules.HasAppointment(world.GetOfficer(faction.ChiefStrategistOfficerId)!, OfficerAppointmentRules.Strategist),
            "AI fills vacant strategist slot without duplicating chief", "2/2");
        OfficerAppointmentRules.AddAppointment(world.GetOfficer(105)!, OfficerAppointmentRules.Strategist);
        var legacy = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        var legacyFaction = legacy.GetFaction(1)!;
        Assert(OfficerAppointmentRules.CountStrategists(legacy, legacyFaction) == 3 &&
               !CreateServices(legacy).Resolver.ExecuteAssignOfficerAppointment(1, 1, 102, OfficerAppointmentRules.Strategist).Success &&
               OfficerAppointmentRules.CountStrategists(legacy, legacyFaction) == 3,
            "Legacy over-quota posts are preserved and block new appointments", "3/2");

        var scaled = TestHelpers.World(month: 2);
        var scaledIds = new[] { 201, 202, 203, 204, 205, 206 };
        scaled.Cities.Add(TestHelpers.City(1, "Capital", 2, 1000, 1000, 1000, scaledIds, Array.Empty<int>()));
        foreach (var id in scaledIds) scaled.Officers.Add(TestHelpers.Officer(id, $"AI{id}", 1));
        var scaledFaction = TestHelpers.Faction(2, "AI", false, 201, scaledIds);
        scaled.Factions.Add(scaledFaction);
        var expectedQuotas = new[] { 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 6, 6 };
        var boundariesPass = true;
        for (var cityCount = 1; cityCount <= expectedQuotas.Length; cityCount++)
        {
            if (cityCount > 1)
                scaled.Cities.Add(TestHelpers.City(cityCount, $"City{cityCount}", 2, 1000, 1000, 1000, Array.Empty<int>(), Array.Empty<int>()));
            boundariesPass &= OfficerAppointmentRules.GetStrategistQuota(scaled, scaledFaction) == expectedQuotas[cityCount - 1];
        }
        Assert(boundariesPass, "Strategist quota follows city count at all boundaries", "1-13 cities");
        foreach (var city in scaled.Cities.Where(city => city.Id > 3)) city.OwnerFactionId = 0;
        var scaledServices = CreateServices(scaled);
        scaledServices.Ai.RunFactionAppointmentDecisions(2);
        Assert(OfficerAppointmentRules.CountStrategists(scaled, scaledFaction) == 3, "AI fills scaled strategist quota", "3/3");
        var beforeLoss = scaled.Officers.Where(officer => OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Strategist)).Select(officer => officer.Id).ToArray();
        scaled.GetCity(3)!.OwnerFactionId = 0;
        scaledServices.Ai.RunFactionAppointmentDecisions(2);
        var afterLoss = scaled.Officers.Where(officer => OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Strategist)).Select(officer => officer.Id).ToArray();
        var available = scaled.Officers.First(officer => officer.Id != scaledFaction.RulerOfficerId && !beforeLoss.Contains(officer.Id));
        var blockedOverQuota = scaledServices.Resolver.ExecuteAssignOfficerAppointment(2, 1, available.Id, OfficerAppointmentRules.Strategist);
        Assert(OfficerAppointmentRules.GetStrategistQuota(scaled, scaledFaction) == 2 && beforeLoss.SequenceEqual(afterLoss) && !blockedOverQuota.Success,
            "City loss retains strategists and blocks extra appointments", "3/2");
    }

    private static void RunMilitaryAppointmentTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "City", 1, 1000, 1000, 1000, new[] { 101, 102, 103 }, Array.Empty<int>()));
        foreach (var id in new[] { 101, 102, 103 }) world.Officers.Add(TestHelpers.Officer(id, $"Officer{id}", 1));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102, 103 }));
        var resolver = CreateServices(world).Resolver;
        var first = world.GetOfficer(102)!;
        var second = world.GetOfficer(103)!;
        resolver.ExecuteAssignOfficerAppointment(1, 1, 102, OfficerAppointmentRules.Governor);
        var assigned = resolver.ExecuteAssignOfficerAppointment(1, 1, 102, OfficerAppointmentRules.GrandGeneral);
        var replaced = resolver.ExecuteAssignOfficerAppointment(1, 1, 103, OfficerAppointmentRules.GrandGeneral);
        var changed = resolver.ExecuteAssignOfficerAppointment(1, 1, 103, OfficerAppointmentRules.RearGeneral);
        Assert(assigned.Success && replaced.Success && changed.Success, "Military appointments resolve", "assign, replace, change");
        Assert(!OfficerAppointmentRules.HasAppointment(first, OfficerAppointmentRules.GrandGeneral) && OfficerAppointmentRules.HasAppointment(first, OfficerAppointmentRules.Governor), "Replacement preserves nonmilitary posts", string.Join(',', first.Appointments));
        Assert(!OfficerAppointmentRules.HasAppointment(second, OfficerAppointmentRules.GrandGeneral) && OfficerAppointmentRules.HasAppointment(second, OfficerAppointmentRules.RearGeneral), "One military post per officer", string.Join(',', second.Appointments));
        resolver.ExecuteAssignOfficerAppointment(1, 1, 102, OfficerAppointmentRules.Strategist);
        var promoted = resolver.ExecuteAssignFactionAdvisor(1, 1, 102, OfficerAppointmentRules.ChiefStrategist);
        Assert(promoted.Success && !OfficerAppointmentRules.HasAppointment(first, OfficerAppointmentRules.Strategist) && world.GetFaction(1)!.ChiefStrategistOfficerId == 102, "Chief strategist replaces strategist on same officer", string.Join(',', first.Appointments));
        var newChief = resolver.ExecuteAssignFactionAdvisor(1, 1, 103, OfficerAppointmentRules.ChiefStrategist);
        Assert(newChief.Success && world.GetFaction(1)!.ChiefStrategistOfficerId == 103 && !OfficerAppointmentRules.HasAppointment(first, OfficerAppointmentRules.ChiefStrategist), "Chief strategist has one faction holder", string.Join(',', first.Appointments));
        var demoted = resolver.ExecuteAssignOfficerAppointment(1, 1, 103, OfficerAppointmentRules.Strategist);
        Assert(demoted.Success && world.GetFaction(1)!.ChiefStrategistOfficerId == 0 && !OfficerAppointmentRules.HasAppointment(second, OfficerAppointmentRules.ChiefStrategist), "Strategist clears same officer's chief post", string.Join(',', second.Appointments));
        var copy = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world));
        Assert(copy != null && OfficerAppointmentRules.HasAppointment(copy.GetOfficer(103)!, OfficerAppointmentRules.RearGeneral), "Military appointment survives serialization", "round trip");
        var old = System.Text.Json.JsonSerializer.Deserialize<OfficerData>("{\"Id\":9,\"Name\":\"Old\"}");
        Assert(old != null && old.Appointments.Count == 0, "Old officer data defaults to no appointments", "legacy JSON");
        var aiWorld = TestHelpers.World(month: 2);
        aiWorld.Cities.Add(TestHelpers.City(2, "AI City", 2, 1000, 1000, 1000, new[] { 201, 202, 203, 204 }, Array.Empty<int>()));
        foreach (var id in new[] { 201, 202, 203, 204 }) aiWorld.Officers.Add(TestHelpers.Officer(id, $"AI{id}", 2));
        aiWorld.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201, 202, 203, 204 }));
        var ai = CreateServices(aiWorld).Ai;
        ai.RunFactionAppointmentDecisions(2);
        var holders = OfficerAppointmentRules.MilitaryAppointments.Select(post => aiWorld.Officers.Single(officer => OfficerAppointmentRules.HasAppointment(officer, post)).Id).ToArray();
        ai.RunFactionAppointmentDecisions(2);
        var holdersAgain = OfficerAppointmentRules.MilitaryAppointments.Select(post => aiWorld.Officers.Single(officer => OfficerAppointmentRules.HasAppointment(officer, post)).Id).ToArray();
        Assert(holders.Distinct().Count() == 3 && holders.SequenceEqual(holdersAgain), "AI fills each post once and keeps holders", string.Join(',', holdersAgain));
    }

    private static void RunMilitaryCombatBonusTest()
    {
        var world = TestHelpers.World(month: 2);
        var attacker = TestHelpers.City(1, "Attacker", 1, 5000, 1000, 5000, new[] { 101, 102 }, Array.Empty<int>());
        var defender = TestHelpers.City(2, "Defender", 2, 5000, 1000, 5000, new[] { 201 }, Array.Empty<int>());
        world.Cities.AddRange(new[] { attacker, defender });
        foreach (var id in new[] { 101, 102, 201 }) world.Officers.Add(TestHelpers.Officer(id, $"Officer{id}", id == 201 ? 2 : 1));
        var first = world.GetOfficer(101)!;
        var guard = world.GetOfficer(201)!;
        var resolver = new CombatResolver();
        var baseResult = resolver.Resolve(world, attacker, defender, 5000, new List<int> { 101 }, defendingOfficerIds: new List<int> { 201 });
        OfficerAppointmentRules.AddAppointment(first, OfficerAppointmentRules.VanguardGeneral);
        var attackResult = resolver.Resolve(world, attacker, defender, 5000, new List<int> { 101 }, defendingOfficerIds: new List<int> { 201 });
        Assert(attackResult.DefenderLosses > baseResult.DefenderLosses, "Vanguard increases enemy troop casualties", $"{baseResult.DefenderLosses} -> {attackResult.DefenderLosses}");
        OfficerAppointmentRules.AddAppointment(guard, OfficerAppointmentRules.RearGeneral);
        var defended = resolver.Resolve(world, attacker, defender, 5000, new List<int> { 101 }, defendingOfficerIds: new List<int> { 201 });
        Assert(defended.DefenderLosses < attackResult.DefenderLosses, "Rear general reduces incoming casualties", $"{attackResult.DefenderLosses} -> {defended.DefenderLosses}");
        Assert(MilitaryAppointmentCombatRules.GetAttackBonusPercent(first) == 5 && MilitaryAppointmentCombatRules.GetDamageReductionPercent(guard) == 5, "Appointment bonuses bounded", "5 percent");
        OfficerAppointmentRules.RemoveAppointment(first, OfficerAppointmentRules.VanguardGeneral);
        OfficerAppointmentRules.AddAppointment(first, OfficerAppointmentRules.GrandGeneral);
        Assert(MilitaryAppointmentCombatRules.GetAttackBonusPercent(first) == 3 && MilitaryAppointmentCombatRules.GetDamageReductionPercent(first) == 3, "Grand general grants both bonuses", "3 percent");
        OfficerAppointmentRules.RemoveAppointment(guard, OfficerAppointmentRules.RearGeneral);
        OfficerAppointmentRules.RemoveAppointment(first, OfficerAppointmentRules.GrandGeneral);
        var noPost = resolver.Resolve(world, attacker, defender, 5000, new List<int> { 101 }, defendingOfficerIds: new List<int> { 201 });
        Assert(noPost.AttackerLosses == baseResult.AttackerLosses && noPost.DefenderLosses == baseResult.DefenderLosses, "Removing appointments restores baseline", "baseline");
        OfficerAppointmentRules.AddAppointment(first, OfficerAppointmentRules.VanguardGeneral);
        var reserveOnly = new List<AttackOfficerDeploymentData> { new() { OfficerId = 101, TroopCount = 0 }, new() { OfficerId = 102, TroopCount = 5000 } };
        var reserveResult = resolver.Resolve(world, attacker, defender, 5000, new List<int> { 101, 102 }, reserveOnly, defendingOfficerIds: new List<int> { 201 });
        OfficerAppointmentRules.RemoveAppointment(first, OfficerAppointmentRules.VanguardGeneral);
        var reserveBaseline = resolver.Resolve(world, attacker, defender, 5000, new List<int> { 101, 102 }, reserveOnly, defendingOfficerIds: new List<int> { 201 });
        Assert(reserveResult.DefenderLosses == reserveBaseline.DefenderLosses && reserveResult.AttackerLosses == reserveBaseline.AttackerLosses, "Undeployed general does not grant combat bonus", "zero troops");
    }

    private static void RunAssignFactionAdvisorTest()
    {
        var world = TestHelpers.World(month: 2);
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1000, new[] { 101, 102, 103 }, Array.Empty<int>()));
        world.Officers.Add(TestHelpers.Officer(101, "Ruler", 1));
        world.Officers.Add(TestHelpers.Officer(102, "CivilOfficer", 1, intelligence: 84, charm: 76));
        world.Officers.Add(TestHelpers.Officer(103, "StrategistOfficer", 1, intelligence: 92, charm: 81));
        world.GetOfficer(102)!.Politics = 88;
        world.GetOfficer(103)!.Politics = 70;
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102, 103 }));
        var services = CreateServices(world);

        var chancellorResult = services.Resolver.ExecuteAssignFactionAdvisor(1, 1, 102, "Chancellor");
        var chiefStrategistResult = services.Resolver.ExecuteAssignFactionAdvisor(1, 1, 103, "ChiefStrategist");
        var reassignResult = services.Resolver.ExecuteAssignFactionAdvisor(1, 1, 103, "Chancellor");
        var rulerBlocked = services.Resolver.ExecuteAssignFactionAdvisor(1, 1, 101, "Chancellor");
        var faction = world.GetFaction(1)!;

        Assert(chancellorResult.Success, "Assign faction advisor chancellor resolves", $"success={chancellorResult.Success}");
        Assert(chiefStrategistResult.Success, "Assign faction advisor chief strategist resolves", $"success={chiefStrategistResult.Success}");
        Assert(faction.ChancellorOfficerId == 103, "Assign faction advisor reassigns unique chancellor", $"chancellor={faction.ChancellorOfficerId}");
        Assert(faction.ChiefStrategistOfficerId == 103, "Assign faction advisor keeps chief strategist slot", $"chiefStrategist={faction.ChiefStrategistOfficerId}");
        Assert(reassignResult.Success, "Assign faction advisor reassign resolves", $"success={reassignResult.Success}");
        Assert(!rulerBlocked.Success, "Assign faction advisor blocks ruler", $"success={rulerBlocked.Success}");
    }

    private static void RunAiCentralAppointmentAssignmentTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "AiCapital", 2, 1000, 1000, 1800, new[] { 201, 202, 203 }, Array.Empty<int>()));

        var ruler = TestHelpers.Officer(201, "AiRuler", 1, charm: 75, intelligence: 68);
        ruler.Politics = 55;
        ruler.Leadership = 72;

        var chancellorCandidate = TestHelpers.Officer(202, "CivilStar", 1, intelligence: 84, charm: 78);
        chancellorCandidate.Politics = 95;
        chancellorCandidate.Leadership = 62;

        var strategistCandidate = TestHelpers.Officer(203, "StrategyStar", 1, intelligence: 97, charm: 70);
        strategistCandidate.Politics = 70;
        strategistCandidate.Leadership = 88;

        world.Officers.AddRange(new[] { ruler, chancellorCandidate, strategistCandidate });
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201, 202, 203 }));
        var services = CreateServices(world);

        var results = services.Ai.RunFactionAppointmentDecisions(2);
        var faction = world.GetFaction(2)!;

        Assert(faction.ChancellorOfficerId == 202, "AI central appointment assigns chancellor", $"chancellor={faction.ChancellorOfficerId}");
        Assert(faction.ChiefStrategistOfficerId == 203, "AI central appointment assigns chief strategist", $"chiefStrategist={faction.ChiefStrategistOfficerId}");
        Assert(OfficerAppointmentRules.HasAppointment(chancellorCandidate, OfficerAppointmentRules.Chancellor), "AI central appointment marks chancellor appointment", string.Join(',', chancellorCandidate.Appointments));
        Assert(OfficerAppointmentRules.HasAppointment(strategistCandidate, OfficerAppointmentRules.ChiefStrategist), "AI central appointment marks chief strategist appointment", string.Join(',', strategistCandidate.Appointments));
        Assert(results.Count(result => result.Success) >= 2, "AI central appointment returns success results", $"results={results.Count}");
    }

    private static void RunAiCityPrefectAppointmentTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "AiCapital", 2, 1000, 1000, 1800, new[] { 201 }, new[] { 2, 3 }));
        world.Cities.Add(TestHelpers.City(2, "AiFrontier", 2, 900, 900, 1600, new[] { 202, 203 }, new[] { 1 }));
        world.Cities.Add(TestHelpers.City(3, "AiHarbor", 2, 950, 950, 1500, new[] { 204, 205 }, new[] { 1 }));

        var ruler = TestHelpers.Officer(201, "AiRuler", 1, charm: 78, intelligence: 70);
        ruler.Politics = 60;

        var reservedChancellor = TestHelpers.Officer(202, "ReservedCivil", 2, intelligence: 88, charm: 82);
        reservedChancellor.Politics = 92;
        reservedChancellor.Appointments.Add(OfficerAppointmentRules.Chancellor);

        var frontierPrefect = TestHelpers.Officer(203, "FrontierPrefect", 2, intelligence: 76, charm: 74);
        frontierPrefect.Politics = 83;
        frontierPrefect.Leadership = 81;

        var reservedStrategist = TestHelpers.Officer(204, "ReservedStrategist", 3, intelligence: 95, charm: 72);
        reservedStrategist.Politics = 68;
        reservedStrategist.Leadership = 89;
        reservedStrategist.Appointments.Add(OfficerAppointmentRules.ChiefStrategist);

        var harborPrefect = TestHelpers.Officer(205, "HarborPrefect", 3, intelligence: 79, charm: 76);
        harborPrefect.Politics = 84;
        harborPrefect.Leadership = 80;

        world.Officers.AddRange(new[] { ruler, reservedChancellor, frontierPrefect, reservedStrategist, harborPrefect });
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(new FactionData
        {
            Id = 2,
            NameEn = "AI",
            NameZhHant = "AI",
            IsPlayer = false,
            RulerOfficerId = 201,
            ChancellorOfficerId = 202,
            ChiefStrategistOfficerId = 204,
            OfficerIds = new List<int> { 201, 202, 203, 204, 205 }
        });
        var services = CreateServices(world);

        var results = services.Ai.RunFactionAppointmentDecisions(2);

        Assert(GetCityPrefectOfficerId(world, 2) == 203, "AI prefect appointment fills frontier city", $"prefect={GetCityPrefectOfficerId(world, 2)}");
        Assert(GetCityPrefectOfficerId(world, 3) == 205, "AI prefect appointment fills harbor city", $"prefect={GetCityPrefectOfficerId(world, 3)}");
        Assert(!OfficerAppointmentRules.HasAppointment(reservedChancellor, OfficerAppointmentRules.Governor), "AI prefect appointment avoids reserved chancellor", string.Join(',', reservedChancellor.Appointments));
        Assert(!OfficerAppointmentRules.HasAppointment(reservedStrategist, OfficerAppointmentRules.Governor), "AI prefect appointment avoids reserved strategist", string.Join(',', reservedStrategist.Appointments));
        Assert(results.Count(result => result.Success) >= 2, "AI prefect appointment returns success results", $"results={results.Count}");
    }

    private static void RunAiCityPrefectReservedFallbackTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "AiCapital", 2, 1000, 1000, 1800, new[] { 201 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "IsolatedCity", 2, 900, 900, 1200, new[] { 202 }, new[] { 1 }));

        var ruler = TestHelpers.Officer(201, "AiRuler", 1, charm: 78, intelligence: 70);
        ruler.Politics = 60;

        var reservedOnlyCandidate = TestHelpers.Officer(202, "ReservedOnly", 2, intelligence: 90, charm: 82);
        reservedOnlyCandidate.Politics = 94;
        reservedOnlyCandidate.Leadership = 75;
        reservedOnlyCandidate.Appointments.Add(OfficerAppointmentRules.Chancellor);

        world.Officers.Add(ruler);
        world.Officers.Add(reservedOnlyCandidate);
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(new FactionData
        {
            Id = 2,
            NameEn = "AI",
            NameZhHant = "AI",
            IsPlayer = false,
            RulerOfficerId = 201,
            ChancellorOfficerId = 202,
            OfficerIds = new List<int> { 201, 202 }
        });
        var services = CreateServices(world);

        _ = services.Ai.RunFactionAppointmentDecisions(2);

        Assert(GetCityPrefectOfficerId(world, 2) == 202, "AI prefect appointment falls back to reserved officer", $"prefect={GetCityPrefectOfficerId(world, 2)}");
        Assert(OfficerAppointmentRules.HasAppointment(reservedOnlyCandidate, OfficerAppointmentRules.Governor), "AI prefect appointment fallback marks governor appointment", string.Join(',', reservedOnlyCandidate.Appointments));
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
        world.GetFaction(1)!.ChancellorOfficerId = 102;
        world.GetFaction(1)!.ChiefStrategistOfficerId = 102;

        var result = services.Resolver.ExecuteFireOfficer(1, 1, 102);
        var rulerBlocked = services.Resolver.ExecuteFireOfficer(1, 1, 101);
        var city = world.GetCity(1)!;
        var faction = world.GetFaction(1)!;
        var officer = world.GetOfficer(102)!;
        var item = world.GetItem(1)!;

        Assert(result.Success, "Fire officer resolves", $"success={result.Success}");
        Assert(!city.OfficerIds.Contains(102), "Fire officer removes city assignment", $"cityHas={city.OfficerIds.Contains(102)}");
        Assert(!faction.OfficerIds.Contains(102), "Fire officer removes faction assignment", $"factionHas={faction.OfficerIds.Contains(102)}");
        Assert(faction.ChancellorOfficerId == 0 && faction.ChiefStrategistOfficerId == 0, "Fire officer clears faction advisor posts", $"chancellor={faction.ChancellorOfficerId}, chiefStrategist={faction.ChiefStrategistOfficerId}");
        Assert(FreeOfficerMovement.IsVisibleFreeOfficer(world, officer), "Fire officer becomes visible free officer", $"cityId={officer.CityId}, stay={officer.FreeOfficerStayMonths}");
        Assert(item.EquippedOfficerId == 0 && item.OwnerFactionId == 1, "Fire officer returns equipped item to faction inventory", $"equipped={item.EquippedOfficerId}, ownerFaction={item.OwnerFactionId}");
        Assert(!rulerBlocked.Success, "Fire officer blocks ruler", $"success={rulerBlocked.Success}");
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

    private static void RunPlayerRulerExileTest()
    {
        var world = TestHelpers.World(year: 200, month: 2);
        world.Cities.Add(TestHelpers.City(1, "Occupied", 2, 1000, 1000, 1000, Array.Empty<int>(), new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "Neighbor", 2, 1000, 1000, 1000, new[] { 201 }, new[] { 1, 3 }));
        world.Cities.Add(TestHelpers.City(3, "Unowned", 0, 1000, 1000, 1000, Array.Empty<int>(), new[] { 2 }));
        var ruler = TestHelpers.Officer(101, "Exiled ruler", 1);
        ruler.DisplacedRulerFactionId = 1;
        world.Officers.Add(ruler);
        world.Officers.Add(TestHelpers.Officer(201, "Enemy", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201 }));
        FreeOfficerMovement.Advance(world);
        var protectedExile = FreeOfficerMovement.GetPlayerExiledRuler(world)?.Id == 101 && ruler.CityId == 1 && !FreeOfficerMovement.IsVisibleFreeOfficer(world, ruler);
        var cannotSkip = !FreeOfficerMovement.TryMovePlayerExiledRuler(world, 3);
        var moved = FreeOfficerMovement.TryMovePlayerExiledRuler(world, 2);
        var onlyOnce = !FreeOfficerMovement.TryMovePlayerExiledRuler(world, 3);
        var saved = System.Text.Json.JsonSerializer.Deserialize<WorldState>(System.Text.Json.JsonSerializer.Serialize(world))!;
        var savedRuler = saved.GetOfficer(101)!;
        var savedExile = FreeOfficerMovement.GetPlayerExiledRuler(saved)?.Id == 101 && !FreeOfficerMovement.TryMovePlayerExiledRuler(saved, 3);
        saved.Month++;
        var arrived = FreeOfficerMovement.TryMovePlayerExiledRuler(saved, 3);
        FreeOfficerMovement.Advance(saved);
        FreeOfficerMovement.InitializeLocations(saved);
        var waitsForPlayer = saved.GetCity(3)!.OwnerFactionId == 0 && FreeOfficerMovement.GetPlayerExiledRuler(saved)?.Id == 101;
        var wrongCityRejected = !FreeOfficerMovement.TryClaimPlayerExiledCity(saved, 1);
        saved.ActiveBattleCampaigns.Add(new ActiveBattleCampaignData { SourceCityId = 3, Stage = CampaignStage.FieldBattle });
        var contestedRejected = !FreeOfficerMovement.TryClaimPlayerExiledCity(saved, 3);
        saved.ActiveBattleCampaigns.Clear();
        var restored = FreeOfficerMovement.TryClaimPlayerExiledCity(saved, 3);
        var duplicateRejected = !FreeOfficerMovement.TryClaimPlayerExiledCity(saved, 3);
        var faction = saved.GetFaction(1)!;
        Assert(protectedExile && cannotSkip && moved && onlyOnce && savedExile && arrived && waitsForPlayer && wrongCityRejected && contestedRejected && restored && duplicateRejected && saved.GetCity(3)!.OwnerFactionId == 1 &&
               saved.GetCity(3)!.OfficerIds.Contains(101) && faction.RulerOfficerId == 101 && faction.OfficerIds.Contains(101) &&
               savedRuler.DisplacedRulerFactionId == 0 && OfficerAppointmentRules.HasAppointment(savedRuler, OfficerAppointmentRules.Lord),
            "player ruler exile", $"protected={protectedExile}, route={cannotSkip}, moved={moved}, once={onlyOnce}, saved={savedExile}, arrived={arrived}, waits={waitsForPlayer}, wrong={wrongCityRejected}, contested={contestedRejected}, restored={restored}, duplicate={duplicateRejected}");
        ruler.CaptiveFactionId = 2;
        Assert(FreeOfficerMovement.GetPlayerExiledRuler(world) == null, "player ruler exile captivity", "a captive ruler cannot use the free exile movement flow");
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
}
