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
    private static void RunAlliedReorganizationTest()
    {
        var campaign = new ActiveBattleCampaignData { Stage = CampaignStage.FieldBattle, AttackerFactionId = 1, DefenderFactionId = 2 };
        var large = new CampaignBattleTeamData { Id = 1, FactionId = 3, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Ai, OfficerId = 301, OriginCityId = 3, ActiveTroops = 70, WoundedTroops = 5, MaximumTroops = 100, Morale = 90 };
        var small = new CampaignBattleTeamData { Id = 1, FactionId = 3, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Ai, OfficerId = 302, OriginCityId = 4, ReinforcementOrderId = 9, ActiveTroops = 20, WoundedTroops = 5, MaximumTroops = 100, Morale = 40, Location = CampaignTeamLocation.Reserve };
        var enemy = new CampaignBattleTeamData { Id = 2, FactionId = 2, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Ai, OfficerId = 201, ActiveTroops = 10, MaximumTroops = 100 };
        var player = new CampaignBattleTeamData { Id = 3, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, OfficerId = 101, ActiveTroops = 10, MaximumTroops = 100 };
        var order = new ReinforcementOrderData { Id = 9, Side = CampaignBattleSide.Attacker, FactionId = 3, Status = ReinforcementStatus.Reserve, Gold = 20, Food = 30, Teams = new() { small } };
        campaign.Teams.AddRange(new[] { large, enemy, player });
        campaign.Reinforcements.Add(order);
        CampaignReorganizationRules.ReorganizeAllies(campaign, 1, CampaignBattleSide.Attacker);
        Assert(large.ActiveTroops == 90 && large.WoundedTroops == 5 && small.ActiveTroops == 0 && small.WoundedTroops == 5 &&
               large.TroopOrigins.Single(item => item.CityId == 4).ActiveTroops == 20 && order.Status == ReinforcementStatus.Reserve && campaign.AttackerGold == 0 &&
               enemy.ActiveTroops == 10 && player.ActiveTroops == 10,
            "allied reorganization", "same-faction active troops consolidate; wounded, enemy and pending supply remain");
        CampaignReorganizationRules.ReorganizeAllies(campaign, 1, CampaignBattleSide.Attacker);
        Assert(large.ActiveTroops == 90 && small.ActiveTroops == 0 && campaign.Teams.Count == 3,
            "allied reorganization", "repeating does not create troops or teams");

        var defenderLarge = new CampaignBattleTeamData { Id = 4, FactionId = 2, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Ai, OfficerId = 202, OriginCityId = 5, ActiveTroops = 50, MaximumTroops = 100, Location = CampaignTeamLocation.InnerCity };
        enemy.Location = CampaignTeamLocation.InnerCity;
        campaign.Teams.Add(defenderLarge);
        CampaignReorganizationRules.ReorganizeAiTeams(campaign, 1);
        Assert(defenderLarge.ActiveTroops == 60 && enemy.ActiveTroops == 0 && enemy.Location == CampaignTeamLocation.Reserve &&
               player.ActiveTroops == 10 && large.ActiveTroops == 90,
            "ai reorganization", "both sides consolidate separately while player troops remain untouched");
    }
    private static void RunArrivedReserveReorganizationTest()
    {
        var campaign = new ActiveBattleCampaignData { Stage = CampaignStage.FieldBattle, AttackerGold = 10, AttackerFood = 20 };
        var field = new CampaignBattleTeamData { Id = 1, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, OfficerId = 101, OriginCityId = 1, ActiveTroops = 50, MaximumTroops = 100 };
        var reserve = new CampaignBattleTeamData { Id = 1, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, OfficerId = 102, OriginCityId = 2, ReinforcementOrderId = 10, ActiveTroops = 40, WoundedTroops = 10, MaximumTroops = 100, Location = CampaignTeamLocation.Reserve };
        var traveling = new CampaignBattleTeamData { Id = 1, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, OfficerId = 103, OriginCityId = 3, ReinforcementOrderId = 11, ActiveTroops = 30, MaximumTroops = 100, Location = CampaignTeamLocation.Traveling };
        var arrivedOrder = new ReinforcementOrderData { Id = 10, Side = CampaignBattleSide.Attacker, FactionId = 1, SourceCityId = 2, Status = ReinforcementStatus.Reserve, Gold = 7, Food = 9, Teams = new() { reserve } };
        var travelingOrder = new ReinforcementOrderData { Id = 11, Side = CampaignBattleSide.Attacker, FactionId = 1, Status = ReinforcementStatus.Traveling, Teams = new() { traveling } };
        campaign.Teams.Add(field); campaign.Reinforcements.AddRange(new[] { arrivedOrder, travelingOrder });
        var eligible = CampaignReorganizationRules.GetEligibleTeams(campaign, 1);
        Assert(eligible.Count == 2 && eligible.Contains(field) && eligible.Contains(reserve) && !eligible.Contains(traveling),
            "arrived reserve reorganization", "includes arrived reserve despite duplicate ids; excludes traveling");
        Assert(CampaignReorganizationRules.TryTransfer(campaign, 1, reserve, field, 15, 5, out _) && field.ActiveTroops == 65 && field.WoundedTroops == 5 && reserve.ActiveTroops == 25 && reserve.WoundedTroops == 5 &&
               arrivedOrder.Status == ReinforcementStatus.Reserve && campaign.AttackerGold == 10 && campaign.AttackerFood == 20 && campaign.Teams.Count == 1,
            "arrived reserve reorganization", "transfer changes existing teams without deploying or crediting supply");
        Assert(!CampaignReorganizationRules.TryTransfer(campaign, 1, traveling, field, 1, 0, out _),
            "arrived reserve reorganization", "traveling team cannot transfer");
        var withdrawal = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(campaign))!;
        withdrawal.SourceCityId = 1; withdrawal.TargetCityId = 3; withdrawal.AttackerFactionId = 1; withdrawal.DefenderFactionId = 2;
        withdrawal.Participants.Add(new BattleParticipantData { FactionId = 1, Side = CampaignBattleSide.Attacker, TroopsCommitted = 200 });
        var withdrawalWorld = new WorldState();
        var firstCity = new CityData { Id = 1, OwnerFactionId = 1 };
        var reserveCity = new CityData { Id = 2, OwnerFactionId = 1 };
        withdrawalWorld.Cities.AddRange(new[] { firstCity, reserveCity, new CityData { Id = 3, OwnerFactionId = 2 } });
        // This test concerns the arrived order; keep the unrelated traveling order out of settlement.
        withdrawal.Reinforcements.RemoveAll(order => order.Id == 11);
        BattleCampaignService.CompleteCampaign(withdrawalWorld, withdrawal, CampaignBattleSide.Defender);
        Assert(firstCity.InfantryTroops == 50 && reserveCity.InfantryTroops == 50 && reserveCity.Gold == 7 && reserveCity.Food == 9 &&
               withdrawal.Reinforcements.Single().Status == ReinforcementStatus.Returned,
            "arrived reserve reorganization", $"withdrawal city1={firstCity.InfantryTroops}, city2={reserveCity.InfantryTroops}, gold={reserveCity.Gold}, food={reserveCity.Food}, status={withdrawal.Reinforcements.Single().Status}");
        var report = withdrawalWorld.BattleReports.Single();
        var contribution = report.ParticipantContributions.Single();
        Assert(report.AttackerCommittedTroops == 200 && report.AttackerActiveTroops == 65 && report.AttackerWoundedTroops == 5 &&
               report.AttackerReturnedTroops == 30 && report.AttackerLostTroops == 100 && contribution.TroopsCommitted == 200 &&
               contribution.TroopsReturned == 30 && contribution.TroopsLost == 100,
            "arrived reserve reorganization", "report counts undeployed survivors as returned without false losses");
        BattleCampaignService.PromoteReserves(campaign, CampaignBattleSide.Attacker);
        Assert(arrivedOrder.Status == ReinforcementStatus.Deployed && campaign.Teams.Contains(reserve) && campaign.AttackerGold == 17 && campaign.AttackerFood == 29,
            "arrived reserve reorganization", "normal promotion deploys and credits supply once");
        var loaded = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(campaign))!;
        var loadedOrder = loaded.Reinforcements.Single(item => item.Id == 10);
        var loadedTeam = loaded.Teams.Single(item => item.ReinforcementOrderId == 10);
        loadedTeam.Location = CampaignTeamLocation.Reserve;
        CampaignReorganizationRules.SynchronizeReinforcementTeamReferences(loaded);
        BattleCampaignService.PromoteReserves(loaded, CampaignBattleSide.Attacker);
        Assert(ReferenceEquals(loadedOrder.Teams[0], loadedTeam) && loadedTeam.Location == CampaignTeamLocation.Field && loaded.Teams.Count == 2 && loaded.AttackerGold == 17 && loaded.AttackerFood == 29,
            "arrived reserve reorganization", "loaded deployed team reconnects without duplicate team or supply");

        var supplyCampaign = new ActiveBattleCampaignData { Stage = CampaignStage.FieldBattle, SourceCityId = 1, TargetCityId = 3, AttackerFactionId = 1, DefenderFactionId = 2, AttackerGold = 5, AttackerFood = 6 };
        var supplyTeam = new CampaignBattleTeamData { Id = 1, FactionId = 1, Side = CampaignBattleSide.Attacker, OfficerId = 105, ActiveTroops = 10, MaximumTroops = 10, ReinforcementOrderId = 15, OriginCityId = 2, Location = CampaignTeamLocation.Reserve };
        var supplyOrder = new ReinforcementOrderData { Id = 15, FactionId = 1, Side = CampaignBattleSide.Attacker, SourceCityId = 2, Status = ReinforcementStatus.Reserve, Gold = 7, Food = 9, Teams = new() { supplyTeam } };
        supplyCampaign.Reinforcements.Add(supplyOrder);
        BattleCampaignService.PromoteReserves(supplyCampaign, CampaignBattleSide.Attacker);
        BattleCampaignService.MarkCampaignTeamSpawnBlocked(supplyCampaign, supplyTeam);
        Assert(supplyOrder.SuppliesDelivered == false && supplyCampaign.AttackerGold == 5 && supplyCampaign.AttackerFood == 6 && supplyCampaign.AttackerGoldInvested == 0 && supplyCampaign.AttackerFoodInvested == 0,
            "reinforcement supplies", "first blocked placement rolls back pending delivery and investment");
        BattleCampaignService.PromoteReserves(supplyCampaign, CampaignBattleSide.Attacker);
        BattleCampaignService.CompleteReinforcementPlacement(supplyCampaign);
        supplyCampaign.AttackerGold = 1; supplyCampaign.AttackerFood = 2;
        var savedSupply = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(supplyCampaign))!;
        CampaignReorganizationRules.SynchronizeReinforcementTeamReferences(savedSupply);
        var savedOrder = savedSupply.Reinforcements.Single();
        var savedTeam = savedSupply.Teams.Single();
        BattleCampaignService.MarkCampaignTeamSpawnBlocked(savedSupply, savedTeam);
        BattleCampaignService.PromoteReserves(savedSupply, CampaignBattleSide.Attacker);
        Assert(savedOrder.SuppliesDelivered == true && savedSupply.AttackerGold == 1 && savedSupply.AttackerFood == 2 && savedSupply.AttackerGoldInvested == 7 && savedSupply.AttackerFoodInvested == 9,
            "reinforcement supplies", "previously delivered and spent supplies do not reappear after loading and redeployment");
        BattleCampaignService.MarkCampaignTeamSpawnBlocked(savedSupply, savedTeam);
        var supplyWorld = new WorldState();
        supplyWorld.Cities.AddRange(new[] { new CityData { Id = 1, OwnerFactionId = 1 }, new CityData { Id = 2, OwnerFactionId = 1 }, new CityData { Id = 3, OwnerFactionId = 2 } });
        BattleCampaignService.CompleteCampaign(supplyWorld, savedSupply, CampaignBattleSide.Defender);
        Assert(supplyWorld.GetCity(2)!.Gold == 0 && supplyWorld.GetCity(2)!.Food == 0 && supplyWorld.GetCity(1)!.Gold == 1 && supplyWorld.GetCity(1)!.Food == 2,
            "reinforcement supplies", "settlement does not return supplies already delivered");

        var waiting = new ActiveBattleCampaignData { Stage = CampaignStage.FieldBattle, AttackerFood = 1 };
        var waitingTeam = new CampaignBattleTeamData { Id = 1, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player, OfficerId = 104, OriginCityId = 2, ReinforcementOrderId = 12, ActiveTroops = 0, WoundedTroops = 10, MaximumTroops = 20, Location = CampaignTeamLocation.Reserve };
        var waitingOrder = new ReinforcementOrderData { Id = 12, Side = CampaignBattleSide.Attacker, FactionId = 1, Status = ReinforcementStatus.Reserve, Gold = 7, Food = 9, Teams = new() { waitingTeam } };
        waiting.Reinforcements.Add(waitingOrder);
        BattleCampaignService.PromoteReserves(waiting, CampaignBattleSide.Attacker);
        Assert(waitingOrder.Status == ReinforcementStatus.Reserve && waiting.AttackerGold == 0 && waiting.AttackerFood == 1 && waiting.Teams.Count == 0,
            "arrived reserve reorganization", "zero active reserve keeps order and supply pending");
        BattleCampaignService.BeginNextCampaignMonth(waiting, 200, 2);
        Assert(waitingTeam.ActiveTroops == 10 && waitingTeam.WoundedTroops == 0 && waiting.AttackerFood == 0 && waitingOrder.Status == ReinforcementStatus.Reserve && waiting.Teams.Count == 0,
            "arrived reserve reorganization", "undeployed reserve recovers with side food without premature deployment");
    }
    private static void RunPlayerReorganizationTransferTest()
    {
        var fallbackWorld = new WorldState();
        var fallbackCity = new CityData { Id = 2, OwnerFactionId = 2 };
        fallbackWorld.Cities.AddRange(new[] { new CityData { Id = 1, OwnerFactionId = 1 }, fallbackCity });
        var fallenBack = new CampaignBattleTeamData { Id = 51, FactionId = 2, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Player,
            OfficerId = 51, ActiveTroops = 60, WoundedTroops = 20, MaximumTroops = 100, Location = CampaignTeamLocation.InnerCity, RetreatDestinationCityId = 2 };
        var fallbackCampaign = new ActiveBattleCampaignData { SourceCityId = 1, TargetCityId = 2, AttackerFactionId = 1, DefenderFactionId = 2,
            Stage = CampaignStage.FieldBattle, AttackerWonFieldBattle = true, Teams = new() { fallenBack } };
        Assert(CampaignReorganizationRules.GetEligibleTeams(fallbackCampaign, 2).Contains(fallenBack), "player reorganization", "defender who fell back inside remains eligible");
        BattleCampaignService.ApplyPostFieldDecision(fallbackCampaign, PostFieldBattleDecision.ImmediateAssault);
        Assert(fallenBack.Location == CampaignTeamLocation.Field && fallenBack.RetreatDestinationCityId == 0,
            "player reorganization", "fall-back marker cleared on entering siege");
        BattleCampaignService.CompleteCampaign(fallbackWorld, fallbackCampaign, CampaignBattleSide.Defender);
        Assert(fallbackCity.InfantryTroops == 80 && fallenBack.ReturnedTroops == 80,
            "player reorganization", "inner-city fallback force returned once after siege");
        var reserveFallback = new CampaignBattleTeamData { Id = 52, FactionId = 2, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Player,
            OfficerId = 52, ActiveTroops = 20, MaximumTroops = 20, ReorganizationCapacity = 100, Location = CampaignTeamLocation.InnerCity, RetreatDestinationCityId = 2 };
        var reserveReceiver = new CampaignBattleTeamData { Id = 53, FactionId = 2, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Player,
            OfficerId = 53, ActiveTroops = 20, MaximumTroops = 20, ReorganizationCapacity = 100, Location = CampaignTeamLocation.InnerCity };
        var reserveFallbackCampaign = new ActiveBattleCampaignData { TargetCityId = 2, Teams = new() { reserveFallback, reserveReceiver } };
        Assert(CampaignReorganizationRules.TryTransfer(reserveFallbackCampaign, 2, reserveFallback, reserveReceiver, 20, 0, out _) &&
               reserveFallback.Location == CampaignTeamLocation.Reserve && reserveFallback.RetreatDestinationCityId == 0 &&
               CampaignReorganizationRules.GetEligibleTeams(reserveFallbackCampaign, 2).Contains(reserveFallback),
            "player reorganization", "emptied fallback reserve can be refilled");
        var capacityOfficer = new OfficerData { Id = 91, Leadership = 80 };
        Assert(CampaignReorganizationRules.GetOfficerCapacity(capacityOfficer) == 4200, "reorganization capacity", "leadership base");
        OfficerAppointmentRules.AddAppointment(capacityOfficer, OfficerAppointmentRules.VanguardGeneral);
        Assert(CampaignReorganizationRules.GetOfficerCapacity(capacityOfficer) == 4700, "reorganization capacity", "vanguard duty bonus");
        OfficerAppointmentRules.AddAppointment(capacityOfficer, OfficerAppointmentRules.GrandGeneral);
        Assert(CampaignReorganizationRules.GetOfficerCapacity(capacityOfficer) == 5700, "reorganization capacity", "highest military duty bonus does not stack");
        var capacityWorld = new WorldState(); capacityWorld.Officers.Add(capacityOfficer);
        var capacityCampaign = new ActiveBattleCampaignData();
        var oldTeam = new CampaignBattleTeamData { Id = 91, OfficerId = 91, FactionId = 1, ControllerType = CampaignControllerType.Player, ActiveTroops = 6000, MaximumTroops = 6000 };
        var receiving = new CampaignBattleTeamData { Id = 92, OfficerId = 92, FactionId = 1, ControllerType = CampaignControllerType.Player, ActiveTroops = 500, MaximumTroops = 500, ReorganizationCapacity = 3000 };
        capacityCampaign.Teams.AddRange(new[] { oldTeam, receiving });
        CampaignReorganizationRules.InitializeCapacities(capacityWorld, capacityCampaign);
        Assert(oldTeam.ReorganizationCapacity == 5700 && oldTeam.ActiveTroops == 6000 && !CampaignReorganizationRules.TryTransfer(capacityCampaign, 1, receiving, oldTeam, 1, 0, out _),
            "reorganization capacity", "legacy over-cap team preserved but cannot receive");
        Assert(CampaignReorganizationRules.TryTransfer(capacityCampaign, 1, oldTeam, receiving, 400, 0, out _) && oldTeam.MaximumTroops + receiving.MaximumTroops == 6500,
            "reorganization capacity", "transfer preserves committed troop totals for reports");
        var recoveryCampaign = new ActiveBattleCampaignData { Stage = CampaignStage.SiegePreparation, AttackerFood = 10, DefenderFood = 10 };
        var cityDefender = new CampaignBattleTeamData { Id = 21, Side = CampaignBattleSide.Defender, Location = CampaignTeamLocation.InnerCity, ActiveTroops = 50, WoundedTroops = 40, MaximumTroops = 100 };
        var outsideDefender = new CampaignBattleTeamData { Id = 22, Side = CampaignBattleSide.Defender, Location = CampaignTeamLocation.Field, ActiveTroops = 50, WoundedTroops = 40, MaximumTroops = 100 };
        var campAttacker = new CampaignBattleTeamData { Id = 23, Side = CampaignBattleSide.Attacker, Location = CampaignTeamLocation.Field, ActiveTroops = 50, WoundedTroops = 40, MaximumTroops = 100 };
        var reserveDefender = new CampaignBattleTeamData { Id = 24, Side = CampaignBattleSide.Defender, Location = CampaignTeamLocation.Reserve, ActiveTroops = 50, WoundedTroops = 40, MaximumTroops = 100 };
        var reserveAttacker = new CampaignBattleTeamData { Id = 25, Side = CampaignBattleSide.Attacker, Location = CampaignTeamLocation.Reserve, ActiveTroops = 50, WoundedTroops = 40, MaximumTroops = 100 };
        recoveryCampaign.Teams.AddRange(new[] { cityDefender, outsideDefender, campAttacker, reserveDefender, reserveAttacker });
        BattleCampaignService.BeginNextCampaignMonth(recoveryCampaign, 200, 2);
        Assert(cityDefender.WoundedTroops == 0 && cityDefender.ActiveTroops == 90 && outsideDefender.WoundedTroops == 20 &&
               campAttacker.WoundedTroops == 20 && reserveDefender.WoundedTroops == 0 && reserveAttacker.WoundedTroops == 0 && recoveryCampaign.DefenderFood == 5 && recoveryCampaign.AttackerFood == 7,
            "player reorganization", "field recovers half, both reserves and defender inner city recover fully");
        var fieldCampaign = new ActiveBattleCampaignData { Stage = CampaignStage.FieldBattle, AttackerFood = 1, DefenderFood = 10 };
        var fieldDefender = new CampaignBattleTeamData { Side = CampaignBattleSide.Defender, Location = CampaignTeamLocation.Field, ActiveTroops = 95, WoundedTroops = 40, MaximumTroops = 100 };
        var fieldAttacker = new CampaignBattleTeamData { Side = CampaignBattleSide.Attacker, Location = CampaignTeamLocation.Field, ActiveTroops = 0, WoundedTroops = 100, MaximumTroops = 100 };
        fieldCampaign.Teams.AddRange(new[] { fieldDefender, fieldAttacker });
        BattleCampaignService.BeginNextCampaignMonth(fieldCampaign, 200, 2);
        Assert(fieldDefender.ActiveTroops == 100 && fieldDefender.WoundedTroops == 35 && fieldCampaign.DefenderFood == 9 &&
               fieldAttacker.ActiveTroops == 20 && fieldAttacker.WoundedTroops == 80 && fieldCampaign.AttackerFood == 0,
            "player reorganization", "field recovery respects capacity, food and separate pools");
        var reserveCampaign = new ActiveBattleCampaignData { Stage = CampaignStage.FieldBattle, AttackerFood = 2, DefenderFood = 1 };
        var attackReserve = new CampaignBattleTeamData { Side = CampaignBattleSide.Attacker, Location = CampaignTeamLocation.Reserve, ActiveTroops = 0, WoundedTroops = 40, MaximumTroops = 40 };
        var defenseReserve = new CampaignBattleTeamData { Side = CampaignBattleSide.Defender, Location = CampaignTeamLocation.Reserve, ActiveTroops = 0, WoundedTroops = 40, MaximumTroops = 40 };
        reserveCampaign.Teams.AddRange(new[] { attackReserve, defenseReserve });
        BattleCampaignService.BeginNextCampaignMonth(reserveCampaign, 200, 2);
        Assert(attackReserve.ActiveTroops == 40 && attackReserve.WoundedTroops == 0 && reserveCampaign.AttackerFood == 0 &&
               defenseReserve.ActiveTroops == 20 && defenseReserve.WoundedTroops == 20 && reserveCampaign.DefenderFood == 0,
            "player reorganization", "field-month reserves recover fully within each side's food limit");
        var campaign = new ActiveBattleCampaignData();
        var a = new CampaignBattleTeamData { Id = 1, FactionId = 1, ControllerType = CampaignControllerType.Player, OfficerId = 11, OriginCityId = 2, TroopType = TroopType.Infantry, ActiveTroops = 80, WoundedTroops = 20, MaximumTroops = 100, Morale = 40, MessTurns = 2, Energy = 3, WeaponAmmo = 4, MaxWeaponAmmo = 10 };
        var b = new CampaignBattleTeamData { Id = 2, FactionId = 1, ControllerType = CampaignControllerType.Player, OfficerId = 12, OriginCityId = 2, TroopType = TroopType.Infantry, ActiveTroops = 20, WoundedTroops = 0, MaximumTroops = 100, Morale = 100, MessTurns = 0, Energy = 8, WeaponAmmo = 9, MaxWeaponAmmo = 10 };
        campaign.Teams.AddRange(new[] { a, b });
        Assert(CampaignReorganizationRules.TryTransfer(campaign, 1, 1, 2, 20, 10, out _) && a.ActiveTroops == 60 && a.WoundedTroops == 10 && b.ActiveTroops == 40 && b.WoundedTroops == 10 && b.Morale == 64,
            "player reorganization", "active and wounded conserved; morale weighted");
        Assert(b.MessTurns == 0 && b.Energy == null && b.WeaponAmmo == null && b.MaxWeaponAmmo == null && a.MessTurns == 0 && a.Energy == null && a.WeaponAmmo == null && b.WoundedOriginCityId == 2,
            "player reorganization", "both teams reset combat status to deployment defaults");
        var restored = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(campaign));
        var restoredTeam = restored!.Teams.Single(team => team.Id == 2);
        Assert(restoredTeam.MessTurns == 0 && restoredTeam.Energy == null && restoredTeam.WeaponAmmo == null && restoredTeam.MaxWeaponAmmo == null && restoredTeam.WoundedOriginCityId == 2,
            "player reorganization", "default status and wounded origin survive serialization");
        var before = b.ActiveTroops;
        Assert(!CampaignReorganizationRules.TryTransfer(campaign, 1, 1, 2, 60, 0, out _) && b.ActiveTroops == before,
            "player reorganization", "capacity exceeded without mutation");
        b.TroopType = TroopType.Archer;
        Assert(!CampaignReorganizationRules.TryTransfer(campaign, 1, 1, 2, 1, 0, out _) && a.ActiveTroops == 60,
            "player reorganization", "different troop types rejected");
        b.TroopType = TroopType.Infantry;
        Assert(!CampaignReorganizationRules.TryTransfer(campaign, 1, 1, 2, -1, 1, out _) && a.WoundedTroops == 10,
            "player reorganization", "negative transfer rejected");
        b.FactionId = 2;
        Assert(!CampaignReorganizationRules.TryTransfer(campaign, 1, 1, 2, 1, 0, out _), "player reorganization", "foreign teams excluded");
        b.FactionId = 1;
        b.ActiveTroops = 0; b.WoundedTroops = 0;
        Assert(CampaignReorganizationRules.TryTransfer(campaign, 1, 1, 2, 60, 10, out _) && a.Location == CampaignTeamLocation.Reserve,
            "player reorganization", "empty source moves to reserve");

        var mixedCampaign = new ActiveBattleCampaignData();
        var first = new CampaignBattleTeamData { Id = 41, FactionId = 1, ControllerType = CampaignControllerType.Player, OfficerId = 41, OriginCityId = 1, ActiveTroops = 60, WoundedTroops = 20, MaximumTroops = 100 };
        var second = new CampaignBattleTeamData { Id = 42, FactionId = 1, ControllerType = CampaignControllerType.Player, OfficerId = 42, OriginCityId = 2, ReinforcementOrderId = 5, ActiveTroops = 20, WoundedTroops = 10, MaximumTroops = 100 };
        mixedCampaign.Teams.AddRange(new[] { first, second });
        var mixed = CampaignReorganizationRules.TryTransfer(mixedCampaign, 1, 41, 42, 20, 10, out _);
        Assert(mixed && second.ActiveTroops == 40 && second.WoundedTroops == 20 && second.TroopOrigins.Single(x => x.CityId == 1).ActiveTroops == 20 &&
               second.TroopOrigins.Single(x => x.CityId == 1).WoundedTroops == 10 && second.TroopOrigins.Single(x => x.CityId == 2).WoundedTroops == 10,
            "player reorganization", "cross-origin transfer retains active and wounded origins");
        CampaignTroopOriginRules.ApplyCounts(second, 30, 25);
        Assert(second.TroopOrigins.Sum(x => x.ActiveTroops) == 30 && second.TroopOrigins.Sum(x => x.WoundedTroops) == 25 &&
               second.TroopOrigins.Single(x => x.CityId == 1).WoundedTroops == 15,
            "player reorganization", "mixed-origin casualties and wounds remain conserved");
        CampaignTroopOriginRules.ApplyCounts(second, 35, 20);
        var mixedRestored = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(mixedCampaign))!;
        Assert(mixedRestored.Teams.Single(x => x.Id == 42).TroopOrigins.Sum(x => x.ActiveTroops) == 35 &&
               mixedRestored.Teams.Single(x => x.Id == 42).TroopOrigins.Sum(x => x.WoundedTroops) == 20,
            "player reorganization", "mixed-origin recovery and save round trip preserve totals");
        var returnWorld = new WorldState();
        var origin1 = new CityData { Id = 1, OwnerFactionId = 1 };
        var origin2 = new CityData { Id = 2, OwnerFactionId = 1 };
        returnWorld.Cities.AddRange(new[] { origin1, origin2 });
        var returnTeam = mixedRestored.Teams.Single(x => x.Id == 42);
        var origin1Count = returnTeam.TroopOrigins.Single(x => x.CityId == 1);
        var origin2Count = returnTeam.TroopOrigins.Single(x => x.CityId == 2);
        CampaignRetreatService.ReturnMixedTeamToOrigins(returnWorld, origin2, returnTeam, origin2);
        Assert(origin1.InfantryTroops == origin1Count.ActiveTroops + origin1Count.WoundedTroops &&
               origin2.InfantryTroops == origin2Count.ActiveTroops + origin2Count.WoundedTroops && returnTeam.ReturnedTroops == 55,
            "player reorganization", "automatic mixed-origin return credits each friendly city once");
        var settlementWorld = new WorldState();
        var cityOne = new CityData { Id = 1, OwnerFactionId = 1 };
        var cityTwo = new CityData { Id = 2, OwnerFactionId = 1 };
        settlementWorld.Cities.AddRange(new[] { cityOne, cityTwo, new CityData { Id = 3, OwnerFactionId = 2 } });
        var withdrawal = System.Text.Json.JsonSerializer.Deserialize<ActiveBattleCampaignData>(System.Text.Json.JsonSerializer.Serialize(mixedCampaign))!;
        withdrawal.SourceCityId = 1; withdrawal.TargetCityId = 3; withdrawal.AttackerFactionId = 1; withdrawal.DefenderFactionId = 2;
        withdrawal.Stage = CampaignStage.FieldBattle;
        var expectedOne = withdrawal.Teams.SelectMany(x => x.TroopOrigins).Where(x => x.CityId == 1).Sum(x => x.ActiveTroops + x.WoundedTroops);
        var expectedTwo = withdrawal.Teams.SelectMany(x => x.TroopOrigins).Where(x => x.CityId == 2).Sum(x => x.ActiveTroops + x.WoundedTroops);
        BattleCampaignService.CompleteCampaign(settlementWorld, withdrawal, CampaignBattleSide.Defender);
        Assert(cityOne.InfantryTroops == expectedOne && cityTwo.InfantryTroops == expectedTwo && withdrawal.Stage == CampaignStage.Resolved,
            "player reorganization", "campaign withdrawal returns transferred and remaining troops by origin");

        var deployCampaign = new ActiveBattleCampaignData { Stage = CampaignStage.CityBattle };
        var emptyReserve = new CampaignBattleTeamData { Id = 1, Side = CampaignBattleSide.Attacker, Location = CampaignTeamLocation.Reserve, OfficerId = 1, ActiveTroops = 0 };
        var readyReserve = new CampaignBattleTeamData { Id = 2, Side = CampaignBattleSide.Attacker, Location = CampaignTeamLocation.Reserve, OfficerId = 2, ActiveTroops = 40 };
        var returnedReinforcement = new CampaignBattleTeamData { Id = 3, Side = CampaignBattleSide.Defender, Location = CampaignTeamLocation.Reserve, OfficerId = 3, ActiveTroops = 30, ReinforcementOrderId = 7 };
        var reinforcement = new ReinforcementOrderData { Id = 7, Side = CampaignBattleSide.Defender, Status = ReinforcementStatus.Deployed, Teams = new() { returnedReinforcement } };
        deployCampaign.Teams.AddRange(new[] { emptyReserve, readyReserve, returnedReinforcement });
        deployCampaign.Reinforcements.Add(reinforcement);
        BattleCampaignService.PromoteReserves(deployCampaign, CampaignBattleSide.Attacker);
        BattleCampaignService.PromoteReserves(deployCampaign, CampaignBattleSide.Defender);
        Assert(emptyReserve.Location == CampaignTeamLocation.Reserve && readyReserve.Location == CampaignTeamLocation.Field &&
               returnedReinforcement.Location == CampaignTeamLocation.Field && deployCampaign.Teams.Count == 3 && reinforcement.Status == ReinforcementStatus.Deployed,
            "player reorganization", "empty reserve waits and refilled deployed reinforcement returns without duplication");
    }
    private static void RunCampaignCaptiveSuccessionTest()
    {
        var world = CreateBattleCampaignWorld();
        world.Cities.Add(TestHelpers.City(4, "DefenderReserve", 2, 100, 100, 500, new[] { 203 }, Array.Empty<int>()));
        var heir = TestHelpers.Officer(203, "Heir", 4);
        world.Officers.Add(heir);
        var faction = world.GetFaction(2)!;
        faction.OfficerIds.Add(203);
        world.GetOfficer(201)!.RelationshipType[heir.Name] = "family,blood";
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        BattleCampaignService.CompleteCampaign(world, campaign, CampaignBattleSide.Attacker);
        var report = world.BattleReports.Single();
        var prisoners = world.PendingCapturedOfficerRecords.Where(record => record.WinnerFactionId == 1).Select(record => record.OfficerId).ToList();
        Assert(report.CapturedOfficerIds.OrderBy(id => id).SequenceEqual(new[] { 201, 202 }) &&
               prisoners.OrderBy(id => id).SequenceEqual(new[] { 201, 202 }) &&
               world.GetOfficer(201)!.CaptiveFactionId == 1 && world.GetOfficer(201)!.JailedCityId == 2,
            "campaign settlement", "city roster and battle teams produce one captive record per officer");
        Assert(faction.RulerOfficerId == 203 && heir.Appointments.Contains(OfficerAppointmentRules.Lord) &&
               !faction.OfficerIds.Contains(201) && !faction.OfficerIds.Contains(202),
            "campaign settlement", "ruler succession runs after captured candidates are removed");
    }

    private static void RunCampaignRetreatReturnEdgesTest()
    {
        var world = CreateBattleCampaignWorld();
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.FieldIntercept);
        var defender = campaign.Teams.First(team => team.Side == CampaignBattleSide.Defender && team.Location == CampaignTeamLocation.Field);
        var city = world.GetCity(campaign.TargetCityId)!;
        var cityTroops = city.Troops;
        var beforeActive = defender.ActiveTroops;
        var fallback = BattleCampaignService.TryRetreatTeamToCity(world, campaign, defender, city.Id);
        Assert(fallback && defender.Location == CampaignTeamLocation.InnerCity && defender.ActiveTroops == beforeActive && city.Troops == cityTroops,
            "campaign retreat", "field defender falls back without returning troops prematurely");

        var source = world.GetCity(1)!;
        var ramBefore = source.RamCount;
        var engineersBefore = source.EngineerTroops;
        var equipment = new CampaignBattleTeamData { FactionId = source.OwnerFactionId, OriginCityId = source.Id, EquipmentType = BattleEquipmentType.Ram, ActiveTroops = 1 };
        var returned = BattleCampaignService.TryReturnSupportTeamToOrigin(world, equipment);
        Assert(returned && source.RamCount == ramBefore + 1 && source.EngineerTroops == engineersBefore + BattleSupportRules.RamEngineerRequirement && equipment.ActiveTroops == 0,
            "campaign retreat", "surviving equipment and crew return to origin");
        BattleCampaignService.TryReturnSupportTeamToOrigin(world, equipment);
        Assert(source.RamCount == ramBefore + 1 && source.EngineerTroops == engineersBefore + BattleSupportRules.RamEngineerRequirement,
            "campaign retreat", "returned equipment is not credited twice");
        var invalid = new CampaignBattleTeamData { FactionId = source.OwnerFactionId, OriginCityId = 9999, ActiveTroops = 20 };
        Assert(!BattleCampaignService.TryReturnSupportTeamToOrigin(world, invalid) && invalid.ActiveTroops == 20,
            "campaign retreat", "missing origin leaves team unchanged");
    }

    private static void RunCampaignDeploymentValidationTest()
    {
        var city = new CityData { InfantryTroops = 100, SiegeTroops = 30, Gold = 50, Food = 50, OfficerIds = new() { 1 } };
        var deployment = new AttackOfficerDeploymentData { OfficerId = 1, TroopType = TroopType.Infantry, TroopCount = 100 };
        ReinforcementDispatchFailure? Check(IReadOnlyCollection<AttackOfficerDeploymentData> deployments, TroopAllocationData troops, int gold, int food)
        {
            try { CampaignDeploymentRules.ValidateCityCanSupply(city, deployments, troops, gold, food); return null; }
            catch (ReinforcementDispatchException exception) { return exception.Failure; }
        }
        Assert(Check(new[] { deployment }, new TroopAllocationData { Infantry = 100 }, 50, 50) == null,
            "campaign deployment validation", "exact available resources accepted");
        Assert(Check(Array.Empty<AttackOfficerDeploymentData>(), new TroopAllocationData(), 0, 0) == ReinforcementDispatchFailure.InvalidDeployment,
            "campaign deployment validation", "empty dispatch rejected");
        Assert(Check(Array.Empty<AttackOfficerDeploymentData>(), new TroopAllocationData(), 1, 0) == null,
            "campaign deployment validation", "supply-only dispatch accepted");
        Assert(Check(new[] { deployment }, new TroopAllocationData { Infantry = 101 }, 0, 0) == ReinforcementDispatchFailure.InsufficientResources,
            "campaign deployment validation", "excess troops rejected");
        var siege = new AttackOfficerDeploymentData { OfficerId = 1, TroopType = TroopType.Siege, TroopCount = 20, SiegeEngineType = SiegeEngineType.Ram };
        Assert(Check(new[] { siege }, new TroopAllocationData { Siege = 20 }, 0, 0) == ReinforcementDispatchFailure.InsufficientSiegeEngines,
            "campaign deployment validation", "missing engine rejected");
        var world = CreateBattleCampaignWorld();
        var target = world.GetCity(2)!;
        var beforeGold = target.Gold;
        var beforeFood = target.Food;
        var attack = CreateBattleCampaignAttack();
        world.GetCity(attack.SourceCityId)!.ConnectedCityIds.Remove(attack.TargetCityId);
        var rejected = false;
        try { BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.CityDefense); }
        catch (InvalidOperationException) { rejected = true; }
        Assert(rejected && target.Gold == beforeGold && target.Food == beforeFood && world.ActiveBattleCampaigns.Count == 0,
            "campaign creation", "invalid route leaves target and campaign list unchanged");
    }

    private static void RunBattleCampaignLifecycleTest()
    {
        var world = CreateBattleCampaignWorld();
        var attack = CreateBattleCampaignAttack();
        var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.FieldIntercept);
        var hasInnerDefense = BattleCampaignService.HasEffectiveInnerCityDefender(campaign);
        var ramReserved = campaign.Teams.Any(team => team.SiegeEngineType == SiegeEngineType.Ram && team.Location == CampaignTeamLocation.Reserve);
        for (var day = 0; day < BattleCampaignService.MaximumBattleDaysPerMonth; day++)
        {
            BattleCampaignService.AdvanceCompletedBattleDay(campaign);
        }

        var reachedLimit = BattleCampaignService.HasReachedMonthlyBattleLimit(campaign);
        campaign.Teams.First(team => team.Side == CampaignBattleSide.Attacker).WoundedTroops = 200;
        campaign.Teams.First(team => team.Side == CampaignBattleSide.Attacker).ActiveTroops -= 200;
        campaign.Teams.First(team => team.Side == CampaignBattleSide.Defender && team.Location == CampaignTeamLocation.InnerCity).WoundedTroops = 200;
        campaign.Teams.First(team => team.Side == CampaignBattleSide.Defender && team.Location == CampaignTeamLocation.InnerCity).ActiveTroops -= 200;
        var attackerBefore = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Attacker).Sum(team => team.WoundedTroops);
        var defenderBefore = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Defender).Sum(team => team.WoundedTroops);
        BattleCampaignService.BeginNextCampaignMonth(campaign, 200, 2);
        var attackerAfter = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Attacker).Sum(team => team.WoundedTroops);
        var defenderAfter = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Defender).Sum(team => team.WoundedTroops);
        Assert(
            campaign.Stage == CampaignStage.FieldBattle && hasInnerDefense && ramReserved && reachedLimit &&
            campaign.BattleDaysThisMonth == 0 && attackerAfter < attackerBefore && defenderAfter < defenderBefore,
            "Battle campaign monthly lifecycle",
            $"inner={hasInnerDefense}, ramReserved={ramReserved}, limit={reachedLimit}, wounded={attackerBefore}->{attackerAfter}/{defenderBefore}->{defenderAfter}");
    }

    private static void RunBattleCampaignReinforcementTest()
    {
        var world = CreateBattleCampaignWorld();
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        var order = BattleCampaignService.DispatchReinforcement(
            world,
            campaign,
            3,
            CampaignBattleSide.Attacker,
            new[] { new AttackOfficerDeploymentData { OfficerId = 102, TroopType = TroopType.Infantry, TroopCount = 1000 } },
            100,
            200);
        BattleCampaignService.AdvanceCompletedBattleDay(campaign);
        var firstDayTraveling = order.Status == ReinforcementStatus.Traveling;
        BattleCampaignService.AdvanceCompletedBattleDay(campaign);
        Assert(
            firstDayTraveling && order.Status == ReinforcementStatus.Deployed &&
            campaign.Teams.Any(team => team.ReinforcementOrderId == order.Id),
            "Battle campaign delayed reinforcement",
            $"eta={order.RouteLinks * 2}, status={order.Status}");
    }

    private static void RunBattleCampaignBlockedEntranceQueueTest()
    {
        var world = CreateBattleCampaignWorld();
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.FieldIntercept);
        var nextId = campaign.Teams.Max(team => team.Id) + 1;
        while (campaign.Teams.Count(team => team.Side == CampaignBattleSide.Attacker && team.Location == CampaignTeamLocation.Field) < BattleCampaignService.MaximumActivePiecesPerSide)
        {
            campaign.Teams.Add(new CampaignBattleTeamData
            {
                Id = nextId++, FactionId = 1, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Player,
                TroopType = TroopType.Infantry, ActiveTroops = 100, MaximumTroops = 100, Location = CampaignTeamLocation.Field
            });
        }

        var order = BattleCampaignService.DispatchReinforcement(world, campaign, 3, CampaignBattleSide.Attacker,
            new[] { new AttackOfficerDeploymentData { OfficerId = 102, TroopType = TroopType.Infantry, TroopCount = 1000 } }, 0, 0);
        for (var day = 0; day < order.RouteLinks * 2; day++) BattleCampaignService.AdvanceCompletedBattleDay(campaign);
        var blocked = order.Status == ReinforcementStatus.Reserve && order.Teams.All(team => team.Location == CampaignTeamLocation.Reserve);
        campaign.Teams.First(team => team.Side == CampaignBattleSide.Attacker && team.Location == CampaignTeamLocation.Field && team.Id != order.Teams[0].Id).Location = CampaignTeamLocation.Eliminated;
        BattleCampaignService.PromoteReserves(campaign, CampaignBattleSide.Attacker);
        var deployed = order.Status == ReinforcementStatus.Deployed && order.Teams.All(team => team.Location == CampaignTeamLocation.Field);
        Assert(blocked && deployed, "Blocked reinforcement queue deploys after an active entrance slot opens", $"blocked={blocked}, deployed={deployed}, status={order.Status}");
    }

    private static void RunFieldReinforcementEntranceSideTest()
    {
        var attacker = InvokePrivateStatic<IEnumerable<Godot.Vector2I>>(typeof(BattleSceneController), "GetFieldBattleReinforcementEdgeGrids", CampaignBattleSide.Attacker).ToList();
        var defender = InvokePrivateStatic<IEnumerable<Godot.Vector2I>>(typeof(BattleSceneController), "GetFieldBattleReinforcementEdgeGrids", CampaignBattleSide.Defender).ToList();
        Assert(attacker.Count > 0 && defender.Count > 0 &&
               attacker.All(grid => grid.Y >= BattleMapData.Height - 2 && grid.Y < BattleMapData.Height) &&
               defender.All(grid => grid.Y >= 0 && grid.Y < 2) && !attacker.Intersect(defender).Any(),
            "Field reinforcements enter on their own rear edge", $"attacker={attacker.Count}, defender={defender.Count}");
    }

    private static void RunDefenseReinforcementRequestTest()
    {
        var world = CreateBattleCampaignWorld();
        var source = world.GetCity(3)!;
        var defenderFaction = world.GetFaction(2)!;
        world.GetFaction(1)!.OfficerIds.Remove(102);
        defenderFaction.OfficerIds.Add(102);
        source.OwnerFactionId = defenderFaction.Id;
        var officer = world.GetOfficer(102)!;
        officer.CityId = source.Id;

        var attack = CreateBattleCampaignAttack();
        attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData
        {
            SourceCityId = source.Id,
            Deployments = new List<AttackOfficerDeploymentData>
            {
                new() { OfficerId = officer.Id, TroopType = TroopType.Infantry, TroopCount = 1000 }
            }
        });
        var troopsBefore = source.Troops;
        var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchDefenseReinforcementRequests(world, campaign, attack);
        var order = campaign.Reinforcements.SingleOrDefault();

        Assert(
            order?.Status == ReinforcementStatus.Traveling &&
            order.Side == CampaignBattleSide.Defender &&
            order.RemainingBattleDays == order.RouteLinks * 2 &&
            source.Troops == troopsBefore - 1000 &&
            BattleCampaignService.IsOfficerCommitted(world, officer.Id),
            "Defense request dispatches on campaign day one and locks its source",
            $"status={order?.Status}, eta={order?.RemainingBattleDays}/{order?.RouteLinks * 2}, troops={troopsBefore}->{source.Troops}, committed={BattleCampaignService.IsOfficerCommitted(world, officer.Id)}");
    }

    private static void RunAlliedDefenseReinforcementRequestTest()
    {
        var world = CreateBattleCampaignWorld();
        var allySource = TestHelpers.City(4, "AllyReserve", 3, 1500, 4000, 4000, new[] { 301 }, new[] { 2 });
        var allyOfficer = TestHelpers.Officer(301, "AllyOfficer", allySource.Id, strength: 80, combat: 80);
        world.Cities.Add(allySource);
        world.Officers.Add(allyOfficer);
        world.Factions.Add(TestHelpers.Faction(3, "Ally", false, allyOfficer.Id, new[] { allyOfficer.Id }));
        world.DiplomacyRelations.Add(new DiplomacyRelationData
        {
            FactionAId = 2,
            FactionBId = 3,
            Status = DiplomacyStatusType.Alliance,
            RemainingMonths = 4,
            RelationScore = 50
        });

        var attack = CreateBattleCampaignAttack();
        attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData
        {
            SourceCityId = allySource.Id,
            IsAllianceRequest = true,
            RequestedTroops = 600,
            PromisedGold = 250,
            PromisedFood = 500
        });
        var troopsBefore = allySource.Troops;
        var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchDefenseReinforcementRequests(world, campaign, attack);
        var invitation = campaign.Invitations.SingleOrDefault();
        var order = campaign.Reinforcements.SingleOrDefault();

        var declinedWorld = CreateBattleCampaignWorld();
        var declinedSource = TestHelpers.City(4, "NeutralReserve", 3, 1500, 4000, 4000, new[] { 301 }, new[] { 2 });
        declinedWorld.Cities.Add(declinedSource);
        declinedWorld.Officers.Add(TestHelpers.Officer(301, "NeutralOfficer", declinedSource.Id, strength: 80, combat: 80));
        declinedWorld.Factions.Add(TestHelpers.Faction(3, "Neutral", false, 301, new[] { 301 }));
        var declinedAttack = CreateBattleCampaignAttack();
        declinedAttack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData
        {
            SourceCityId = declinedSource.Id,
            IsAllianceRequest = true,
            RequestedTroops = 600
        });
        var declinedCampaign = BattleCampaignService.CreateCampaign(declinedWorld, declinedAttack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchDefenseReinforcementRequests(declinedWorld, declinedCampaign, declinedAttack);
        var declinedInvitation = declinedCampaign.Invitations.SingleOrDefault();

        Assert(
            invitation?.Status == BattleInvitationStatus.Accepted &&
            invitation.RequestedSourceCityId == allySource.Id &&
            invitation.PromisedGold == 250 && invitation.PromisedFood == 500 &&
            order?.Status == ReinforcementStatus.Traveling &&
            order.Side == CampaignBattleSide.Defender &&
            order.SourceCityId == allySource.Id &&
            order.Teams.Count > 0 &&
            allySource.Troops < troopsBefore &&
            allySource.Troops >= BattleCampaignService.MinimumCityGarrison &&
            declinedInvitation?.Status == BattleInvitationStatus.Declined &&
            declinedCampaign.Reinforcements.Count == 0,
            "Allied defense request honors its selected source and resolves accept or decline",
            $"accepted={invitation?.Status}, source={invitation?.RequestedSourceCityId}/{order?.SourceCityId}, status={order?.Status}, teams={order?.Teams.Count ?? 0}, troops={troopsBefore}->{allySource.Troops}, declined={declinedInvitation?.Status}, declinedOrders={declinedCampaign.Reinforcements.Count}");
    }

    private static void RunAiCampaignReinforcementTest()
    {
        var attackerWorld = CreateBattleCampaignWorld();
        var attackerCampaign = BattleCampaignService.CreateCampaign(
            attackerWorld,
            CreateBattleCampaignAttack(),
            DefenderBattlePlan.CityDefense);
        var attackerServices = CreateServices(attackerWorld);
        var attackerSource = attackerWorld.GetCity(3)!;
        var attackerTroopsBefore = attackerSource.Troops;
        var attackerResult = attackerServices.Ai.RunSingleCityDecision(1, attackerSource.Id);
        var attackerOrder = attackerCampaign.Reinforcements.SingleOrDefault();
        var attackerValid = attackerOrder != null &&
                            attackerOrder.Side == CampaignBattleSide.Attacker &&
                            attackerOrder.SourceCityId == attackerSource.Id &&
                            attackerOrder.Teams.Count > 0 &&
                            attackerOrder.Teams.All(team =>
                                team.OfficerId > 0 && Enum.IsDefined(typeof(TroopType), team.TroopType) && team.ActiveTroops > 0) &&
                            attackerSource.Troops >= BattleCampaignService.MinimumCityGarrison &&
                            attackerSource.Troops < attackerTroopsBefore;
        Assert(
            attackerResult.Success && attackerValid,
            "AI attacker dispatches valid campaign reinforcement",
            $"success={attackerResult.Success}, side={attackerOrder?.Side}, teams={attackerOrder?.Teams.Count ?? 0}, troops={attackerTroopsBefore}->{attackerSource.Troops}");

        var defenderWorld = CreateBattleCampaignWorld();
        var defenderSource = defenderWorld.GetCity(3)!;
        defenderSource.OwnerFactionId = 2;
        defenderWorld.GetFaction(1)!.OfficerIds.Remove(102);
        defenderWorld.GetFaction(2)!.OfficerIds.Add(102);
        var defenderOfficer = defenderWorld.GetOfficer(102)!;
        defenderOfficer.CityId = defenderSource.Id;
        var defenderCampaign = BattleCampaignService.CreateCampaign(
            defenderWorld,
            CreateBattleCampaignAttack(),
            DefenderBattlePlan.CityDefense);
        var defenderServices = CreateServices(defenderWorld);
        var defenderTroopsBefore = defenderSource.Troops;
        var defenderResult = defenderServices.Ai.RunSingleCityDecision(2, defenderSource.Id);
        var defenderOrder = defenderCampaign.Reinforcements.SingleOrDefault();
        var defenderValid = defenderOrder != null &&
                            defenderOrder.Side == CampaignBattleSide.Defender &&
                            defenderOrder.SourceCityId == defenderSource.Id &&
                            defenderOrder.TargetCityId == defenderCampaign.TargetCityId &&
                            defenderOrder.Teams.Count > 0 &&
                            defenderOrder.Teams.All(team =>
                                team.OfficerId > 0 && Enum.IsDefined(typeof(TroopType), team.TroopType) && team.ActiveTroops > 0) &&
                            defenderSource.Troops >= BattleCampaignService.MinimumCityGarrison &&
                            defenderSource.Troops < defenderTroopsBefore;
        Assert(
            defenderResult.Success && defenderValid,
            "AI defender dispatches valid campaign reinforcement",
            $"success={defenderResult.Success}, side={defenderOrder?.Side}, teams={defenderOrder?.Teams.Count ?? 0}, target={defenderOrder?.TargetCityId}, troops={defenderTroopsBefore}->{defenderSource.Troops}");
    }

    private static void RunPlayerCampaignReinforcementCommandTest()
    {
        var world = CreateBattleCampaignWorld();
        var source = world.GetCity(3)!;
        source.InfantryTroops = 2500;
        source.SpearmanTroops = 0;
        source.CavalryTroops = 0;
        source.ArcherTroops = 0;
        source.CrossbowTroops = 0;
        source.SiegeTroops = 0;
        source.SyncLegacyTroops();
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        var services = CreateServices(world);
        var troopsBefore = source.Troops;
        var goldBefore = source.Gold;
        var foodBefore = source.Food;

        var result = services.Resolver.Execute(new CommandRequest
        {
            Type = CommandType.Attack,
            ActorFactionId = 1,
            SourceCityId = source.Id,
            TargetCityId = campaign.TargetCityId,
            GoldToSend = 120,
            FoodToSend = 300,
            OfficerIds = new List<int> { 102 },
            AttackOfficerDeployments = new List<AttackOfficerDeploymentData>
            {
                new() { OfficerId = 102, TroopType = TroopType.Infantry, TroopCount = 1000 }
            }
        });
        var order = campaign.Reinforcements.SingleOrDefault();

        Assert(
            result.Success &&
            order?.Status == ReinforcementStatus.Traveling &&
            order.Side == CampaignBattleSide.Attacker &&
            order.SourceCityId == source.Id &&
            order.TargetCityId == campaign.TargetCityId &&
            order.RemainingBattleDays == order.RouteLinks * 2 &&
            source.Troops == troopsBefore - 1000 &&
            source.Gold == goldBefore - 120 && source.Food == foodBefore - 300 &&
            BattleCampaignService.IsOfficerCommitted(world, 102),
            "Player attack command dispatches a campaign reinforcement immediately",
            $"success={result.Success}, eta={order?.RemainingBattleDays}/{order?.RouteLinks * 2}, " +
            $"troops={troopsBefore}->{source.Troops}, gold={goldBefore}->{source.Gold}, food={foodBefore}->{source.Food}");
    }

    private static void RunCampaignRetreatDestinationTest()
    {
        var world = CreateBattleCampaignWorld();
        var destination = world.GetCity(3)!;
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.FieldIntercept);
        var team = campaign.Teams.First(item => item.Side == CampaignBattleSide.Attacker && item.OfficerId == 101);
        var troopsBefore = destination.Troops;
        var destinations = BattleCampaignService.GetRetreatDestinations(world, campaign, team);
        var returned = BattleCampaignService.TryRetreatTeamToCity(world, campaign, team, destination.Id);
        var officer = world.GetOfficer(101)!;

        Assert(
            destinations.Any(city => city.Id == destination.Id) && returned &&
            team.Location == CampaignTeamLocation.NeighborCity &&
            team.RetreatDestinationCityId == destination.Id &&
            team.ActiveTroops == 0 && team.WoundedTroops == 0 &&
            destination.Troops == troopsBefore + team.ReturnedTroops &&
            officer.CityId == destination.Id && destination.OfficerIds.Contains(officer.Id),
            "Campaign team retreats to a selected friendly neighbor and returns its officer and troops",
            $"listed={destinations.Any(city => city.Id == destination.Id)}, returned={returned}, city={officer.CityId}, " +
            $"troops={troopsBefore}->{destination.Troops}, returnedTroops={team.ReturnedTroops}");
    }

    private static void RunBattleCampaignReorganizationTest()
    {
        var world = CreateBattleCampaignWorld();
        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        var proposed = campaign.Teams
            .Where(team => team.FactionId == 1)
            .Select(team => new CampaignBattleTeamData
            {
                Id = team.Id,
                FactionId = team.FactionId,
                Side = team.Side,
                ControllerType = team.ControllerType,
                OfficerId = team.OfficerId,
                TroopType = team.TroopType,
                SiegeEngineType = team.SiegeEngineType,
                ActiveTroops = team.ActiveTroops,
                WoundedTroops = team.WoundedTroops,
                MaximumTroops = team.MaximumTroops,
                Location = team.Location
            }).ToList();
        var accepted = BattleCampaignService.TryReorganizeFactionTeams(campaign, 1, proposed, out _);
        proposed[0].ActiveTroops++;
        var rejected = !BattleCampaignService.TryReorganizeFactionTeams(campaign, 1, proposed, out _);
        Assert(accepted && rejected, "Battle campaign reorganization preserves totals", $"accepted={accepted}, rejectedInvalid={rejected}");
    }

    private static void RunBattleCampaignCityCaptureSettlementTest()
    {
        var world = CreateBattleCampaignWorld();
        var source = world.GetCity(1)!;
        var target = world.GetCity(2)!;
        var sourceGoldBefore = source.Gold;
        var sourceFoodBefore = source.Food;
        world.InternalAffairsSchedules.Add(new InternalAffairsScheduleData
        {
            CityId = target.Id,
            OfficerId = 201,
            JobType = InternalAffairsJobType.Defend,
            RemainingMonths = 2,
            State = InternalAffairsScheduleState.Active
        });

        var campaign = BattleCampaignService.CreateCampaign(world, CreateBattleCampaignAttack(), DefenderBattlePlan.CityDefense);
        campaign.Stage = CampaignStage.CityBattle;
        BattleCampaignService.CompleteCampaign(world, campaign, CampaignBattleSide.Attacker);

        var report = world.BattleReports.SingleOrDefault();
        var capturedIds = report?.CapturedOfficerIds ?? new List<int>();
        var queuedCaptiveIds = world.PendingCapturedOfficerRecords
            .Where(record => record.WinnerFactionId == 1 && record.WinnerCityId == target.Id)
            .Select(record => record.OfficerId)
            .ToHashSet();
        var attackerReturned = target.Troops > 0 && target.OfficerIds.Contains(101);
        // CreateCampaign moves the defender's city treasury into the campaign
        // before the battle begins. A captured city must therefore receive the
        // two campaign treasuries exactly once, rather than its old balance a
        // second time.
        var resourcesTransferred = target.Gold == campaign.AttackerGold + campaign.DefenderGold &&
                                   target.Food == campaign.AttackerFood + campaign.DefenderFood &&
                                   report?.AttackerGoldGained == campaign.DefenderGold &&
                                   report?.AttackerFoodGained == campaign.DefenderFood;
        var participantContributions = report?.ParticipantContributions ?? new List<WorldState.BattleParticipantContributionData>();
        var contributionTotalsMatch = participantContributions.Count == campaign.Participants.Count &&
                                      participantContributions.All(item => item.TroopsCommitted >= item.ActiveTroops + item.WoundedTroops + item.TroopsReturned + item.TroopsLost) &&
                                      participantContributions.Any(item => item.FactionId == 1 && item.Side == CampaignBattleSide.Attacker) &&
                                      participantContributions.Any(item => item.FactionId == 2 && item.Side == CampaignBattleSide.Defender);

        Assert(
            campaign.Stage == CampaignStage.Resolved &&
            target.OwnerFactionId == 1 &&
            !world.InternalAffairsSchedules.Any(schedule => schedule.CityId == target.Id) &&
            capturedIds.OrderBy(id => id).SequenceEqual(new[] { 201, 202 }) &&
            queuedCaptiveIds.SetEquals(capturedIds) &&
            attackerReturned && resourcesTransferred && contributionTotalsMatch &&
            source.Gold == sourceGoldBefore && source.Food == sourceFoodBefore,
            "Battle campaign city capture settles map state, captives, resources, and report",
            $"owner={target.OwnerFactionId}, schedules={world.InternalAffairsSchedules.Count}, captives={string.Join(',', capturedIds)}, " +
            $"returned={attackerReturned}, resources={resourcesTransferred}, contributions={participantContributions.Count}, source={source.Gold}/{source.Food}");
    }

    private static void RunAlliedCampaignSettlementTest()
    {
        var world = CreateBattleCampaignWorld();
        var allyCity = TestHelpers.City(4, "AllyReserve", 3, 1500, 4000, 4000, new[] { 301 }, new[] { 2 });
        world.Cities.Add(allyCity);
        world.Officers.Add(TestHelpers.Officer(301, "AllyOfficer", allyCity.Id, strength: 80, combat: 80));
        world.Factions.Add(TestHelpers.Faction(3, "Ally", false, 301, new[] { 301 }));
        var relation = new DiplomacyRelationData
        {
            FactionAId = 2,
            FactionBId = 3,
            Status = DiplomacyStatusType.Alliance,
            RemainingMonths = 4,
            RelationScore = 50
        };
        world.DiplomacyRelations.Add(relation);
        var attack = CreateBattleCampaignAttack();
        attack.DefenseReinforcementRequests.Add(new DefenseReinforcementRequestData
        {
            SourceCityId = allyCity.Id,
            IsAllianceRequest = true,
            RequestedTroops = 600
        });
        var campaign = BattleCampaignService.CreateCampaign(world, attack, DefenderBattlePlan.CityDefense);
        BattleCampaignService.DispatchDefenseReinforcementRequests(world, campaign, attack);
        var invitation = campaign.Invitations.Single();
        var goldBefore = allyCity.Gold;
        var foodBefore = allyCity.Food;
        campaign.Participants.Single(item => item.FactionId == 3).BattleDaysParticipated = 2;
        campaign.Stage = CampaignStage.CityBattle;
        BattleCampaignService.CompleteCampaign(world, campaign, CampaignBattleSide.Defender);

        var contribution = world.BattleReports.Single().ParticipantContributions.Single(item => item.FactionId == 3);
        Assert(
            invitation.Status == BattleInvitationStatus.Accepted &&
            contribution.PromiseFulfilled && contribution.ContributionScore > 0 &&
            contribution.RewardGold > 0 && contribution.RewardFood > 0 &&
            allyCity.Gold == goldBefore + contribution.RewardGold &&
            allyCity.Food == foodBefore + contribution.RewardFood &&
            contribution.RelationDelta > 0 && contribution.TrustDelta > 0 &&
            relation.RelationScore == 50 + contribution.RelationDelta &&
            relation.TrustFromAToB == 50 + contribution.TrustDelta,
            "Allied campaign settlement pays contribution reward and improves future trust",
            $"reward={contribution.RewardGold}/{contribution.RewardFood}, relation={relation.RelationScore}, trust={relation.TrustFromAToB}, score={contribution.ContributionScore}");
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

    private static void RunAiKnownResourceAttackPriorityTest()
    {
        var world = TestHelpers.World();
        var poorTarget = TestHelpers.City(1, "PoorTarget", 1, 1000, 1000, 1200, Array.Empty<int>(), new[] { 2 });
        var aiSource = TestHelpers.City(2, "AiSource", 2, 1000, 1000, 1400, new[] { 201 }, new[] { 1, 3 });
        var richTarget = TestHelpers.City(3, "MetalTarget", 1, 1000, 1000, 1200, Array.Empty<int>(), new[] { 2 });
        richTarget.ResourceDeposits.Add(new CityResourceDepositData
        {
            Type = StrategicResourceType.Metal,
            MonthlyYield = 10,
            RemainingReserve = 600
        });
        world.Cities.AddRange(new[] { poorTarget, aiSource, richTarget });
        world.Officers.Add(TestHelpers.Officer(201, "A1", aiSource.Id));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 0, Array.Empty<int>()));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        world.UpsertCityIntel(2, poorTarget.Id, 4);
        world.UpsertCityIntel(2, richTarget.Id, 4);

        var services = CreateServices(world);
        _ = services.Ai.RunSingleCityDecision(2, aiSource.Id);
        var resourceAttack = world.PendingCommands.SingleOrDefault(command => command.Type == CommandType.Attack);

        Assert(
            resourceAttack?.TargetCityId == richTarget.Id && services.Ai.LastAttackDecisionDetail.Contains("資源價值"),
            "AI prioritizes a known rich resource city without ignoring intelligence",
            $"target={resourceAttack?.TargetCityId}, detail={services.Ai.LastAttackDecisionDetail}");
    }

    private static void RunAiSiegeEquipmentAttackTest()
    {
        var world = TestHelpers.World();
        var target = TestHelpers.City(1, "FortifiedTarget", 1, 1000, 1000, 1300, new[] { 101 }, new[] { 2 });
        target.Defense = 85;
        var source = TestHelpers.City(2, "AiSiegeSource", 2, 1000, 1000, 1600, new[] { 201 }, new[] { 1 });
        source.InfantryTroops = 1500;
        source.SiegeTroops = 100;
        source.RamCount = 1;
        source.SupplyCartCount = 1;
        source.SyncLegacyTroops();
        world.Cities.AddRange(new[] { target, source });
        var defender = TestHelpers.Officer(101, "D1", target.Id, combat: 90);
        defender.Intelligence = 20;
        world.Officers.Add(defender);
        world.Officers.Add(TestHelpers.Officer(201, "A1", source.Id));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        world.UpsertCityIntel(2, target.Id, 4);

        var services = CreateServices(world);
        _ = services.Ai.RunSingleCityDecision(2, source.Id);
        var attack = world.PendingCommands.SingleOrDefault(command => command.Type == CommandType.Attack);
        var ramDeployment = attack?.AttackOfficerDeployments.SingleOrDefault(deployment => deployment.SiegeEngineType == SiegeEngineType.Ram);

        Assert(
            attack?.TargetCityId == target.Id && ramDeployment?.TroopType == TroopType.Siege && ramDeployment.TroopCount > 0 &&
            attack.BattleSupport.SupplyCart && attack.BattleSupport.EngineerTeamCount == 50 && source.SupplyCartCount == 0 && services.Ai.LastAttackDecisionDetail.Contains("攻城器"),
            "AI carries one ready ram, one supply cart, and a river-field engineer team",
            $"target={attack?.TargetCityId}, ram={ramDeployment?.TroopCount}, cart={attack?.BattleSupport.SupplyCart}, engineers={attack?.BattleSupport.EngineerTeamCount}, detail={services.Ai.LastAttackDecisionDetail}");
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

    private static void RunAttackResolutionTest()
    {
        var world = TestHelpers.World();
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 900, new[] { 101 }, new[] { 2 }));
        world.Cities.Add(TestHelpers.City(2, "AiAttackCity", 2, 1200, 1200, 3200, new[] { 201 }, new[] { 1 }));
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1, 50, 50, 50, 50));
        world.Officers.Add(TestHelpers.Officer(201, "A1", 2, 90, 60, 60, 90));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        world.Factions.Add(TestHelpers.Faction(2, "AI", false, 201, new[] { 201 }));
        world.InternalAffairsSchedules.Add(new InternalAffairsScheduleData
        {
            CityId = 1,
            JobType = InternalAffairsJobType.Manufacturing,
            ConstructionProjectType = ConstructionProjectType.Ram,
            RemainingMonths = 1,
            TotalMonths = 1,
            State = InternalAffairsScheduleState.Paused
        });
        var services = CreateServices(world);

        _ = services.Ai.RunSingleCityDecision(2, 2);
        _ = services.Turn.ResolvePendingCommands(services.Resolver);

        Assert(world.GetCity(1)?.OwnerFactionId == 2 &&
               !world.InternalAffairsSchedules.Any(schedule => schedule.CityId == 1),
            "AI attack resolution clears captured-city schedules",
            $"owner={world.GetCity(1)?.OwnerFactionId}, schedules={world.InternalAffairsSchedules.Count}");
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
        Assert(world.GetOfficer(102)!.CaptiveFactionId == 2 && world.GetOfficer(103)!.CaptiveFactionId == 2 &&
               pendingSuccession != null && pendingSuccession.CandidateOfficerIds.SequenceEqual(new[] { 104 }),
            "Attack ruler death excludes captured succession candidates", pendingSuccession == null ? "pending=null" : $"candidates={string.Join(',', pendingSuccession.CandidateOfficerIds)}");
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
}
