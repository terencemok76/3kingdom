using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;
using static ThreeKingdom.Core.BattleCampaignService;
using static ThreeKingdom.Core.CampaignDeploymentRules;

namespace ThreeKingdom.Core;

internal static class BattleCampaignCreationService
{
    private const int DailyFoodPer100Troops = 5;
    internal static ActiveBattleCampaignData CreateCampaign(
        WorldState world,
        PendingCommandData attack,
        DefenderBattlePlan defenderPlan,
        IReadOnlyList<PendingCommandData>? jointAttacks = null)
    {
        var sourceCity = world.GetCity(attack.SourceCityId) ??
            throw new InvalidOperationException("Attack source city is missing.");
        var targetCity = world.GetCity(attack.TargetCityId) ??
            throw new InvalidOperationException("Attack target city is missing.");
        if (!sourceCity.ConnectedCityIds.Contains(targetCity.Id) ||
            sourceCity.OwnerFactionId == targetCity.OwnerFactionId)
        {
            throw new InvalidOperationException("Attack route is no longer valid.");
        }

        var attackingOrders = new List<PendingCommandData> { attack };
        if (jointAttacks != null)
        {
            attackingOrders.AddRange(jointAttacks.Where(order =>
                order.Type == CommandType.Attack &&
                order.ActorFactionId == attack.ActorFactionId &&
                order.TargetCityId == attack.TargetCityId &&
                order.SourceCityId != attack.SourceCityId));
        }

        var campaignId = world.ActiveBattleCampaigns.Count == 0
            ? 1
            : world.ActiveBattleCampaigns.Max(item => item.Id) + 1;
        var playerFactionId = world.Factions.FirstOrDefault(faction => faction.IsPlayer)?.Id ?? -1;
        var campaign = new ActiveBattleCampaignData
        {
            Id = campaignId,
            AttackerFactionId = sourceCity.OwnerFactionId,
            AttackerRulerOfficerId = world.GetFaction(sourceCity.OwnerFactionId)?.RulerOfficerId ?? 0,
            DefenderFactionId = targetCity.OwnerFactionId,
            SourceCityId = sourceCity.Id,
            TargetCityId = targetCity.Id,
            StartedYear = world.Year,
            StartedMonth = world.Month,
            CurrentYear = world.Year,
            CurrentMonth = world.Month,
            DefenderPlan = defenderPlan,
            Stage = defenderPlan == DefenderBattlePlan.FieldIntercept
                ? CampaignStage.FieldBattle
                : CampaignStage.CityBattle,
            IsPlayerInvolved = sourceCity.OwnerFactionId == playerFactionId || targetCity.OwnerFactionId == playerFactionId,
            AttackerGold = attackingOrders.Sum(order => Math.Max(0, order.GoldToSend)),
            AttackerFood = attackingOrders.Sum(order => Math.Max(0, order.FoodToSend)),
            DefenderGold = Math.Max(0, targetCity.Gold),
            DefenderFood = Math.Max(0, targetCity.Food),
            AttackerGoldInvested = attackingOrders.Sum(order => Math.Max(0, order.GoldToSend)),
            AttackerFoodInvested = attackingOrders.Sum(order => Math.Max(0, order.FoodToSend)),
            DefenderGoldInvested = Math.Max(0, targetCity.Gold),
            DefenderFoodInvested = Math.Max(0, targetCity.Food)
        };
        campaign.AttackerBattleSupport = attack.BattleSupport?.Clone() ?? new BattleSupportDeploymentData();
        campaign.DefenderBattleSupport = attack.DefenderBattleSupport?.Clone() ?? new BattleSupportDeploymentData();
        targetCity.Gold = 0;
        targetCity.Food = 0;

        foreach (var attackingOrder in attackingOrders)
        {
            var attackingSourceCity = world.GetCity(attackingOrder.SourceCityId) ??
                throw new InvalidOperationException("Joint attack source city is missing.");
            campaign.Teams.AddRange(CreateTeams(
                attackingOrder.AttackOfficerDeployments,
                campaign.AttackerFactionId,
                CampaignBattleSide.Attacker,
                playerFactionId,
                originCityId: attackingSourceCity.Id));
            campaign.Teams.AddRange(CreateBattleSupportTeams(
                attackingOrder.BattleSupport,
                campaign.AttackerFactionId,
                CampaignBattleSide.Attacker,
                playerFactionId,
                attackingSourceCity.Id));
        }

        // Envoys assigned to pre-battle support requests cannot simultaneously
        // be assigned to either the field force or the automatic city garrison.
        var envoyIds = attack.DefenseReinforcementRequests
            .Where(request => request.IsAllianceRequest && request.EnvoyOfficerId > 0)
            .Select(request => request.EnvoyOfficerId).ToHashSet();
        var defenderDeployments = attack.DefenderOfficerDeployments.Count > 0
            ? CloneDeployments(attack.DefenderOfficerDeployments.Where(deployment => !envoyIds.Contains(deployment.OfficerId)))
            : CreateDefaultDefenderDeployments(
                targetCity,
                defenderPlan == DefenderBattlePlan.FieldIntercept ? 70 : 100,
                defenderPlan == DefenderBattlePlan.FieldIntercept,
                envoyIds);
        var defenderAllocation = BuildTroopAllocation(defenderDeployments);
        defenderAllocation.Siege += Math.Max(0, attack.DefenderBattleSupport?.TotalEngineerCount ?? 0);
        targetCity.RemoveTroopAllocation(defenderAllocation);
        targetCity.RemoveSiegeEngineAllocation(new SiegeEngineAllocationData
        {
            Ram = attack.DefenderBattleSupport?.Ram == true ? 1 : 0,
            Catapult = attack.DefenderBattleSupport?.Catapult == true ? 1 : 0,
            Ladder = attack.DefenderBattleSupport?.Ladder == true ? 1 : 0
        });
        if (attack.DefenderBattleSupport?.SupplyCart == true)
        {
            targetCity.SupplyCartCount = Math.Max(0, targetCity.SupplyCartCount - 1);
        }
        campaign.Teams.AddRange(CreateTeams(
            defenderDeployments,
            campaign.DefenderFactionId,
            CampaignBattleSide.Defender,
            playerFactionId,
            defenderPlan == DefenderBattlePlan.CityDefense
                ? CampaignTeamLocation.InnerCity
                : CampaignTeamLocation.Field,
            originCityId: targetCity.Id));
        campaign.Teams.AddRange(CreateBattleSupportTeams(
            attack.DefenderBattleSupport,
            campaign.DefenderFactionId,
            CampaignBattleSide.Defender,
            playerFactionId,
            targetCity.Id,
            defenderPlan == DefenderBattlePlan.CityDefense
                ? CampaignTeamLocation.InnerCity
                : CampaignTeamLocation.Field));

        if (defenderPlan == DefenderBattlePlan.FieldIntercept)
        {
            var usedOfficerIds = defenderDeployments.Select(item => item.OfficerId).ToHashSet();
            usedOfficerIds.UnionWith(envoyIds);
            var cityDefenseDeployments = CreateDefaultDefenderDeployments(targetCity, 100, false, usedOfficerIds);
            var cityDefenseAllocation = BuildTroopAllocation(cityDefenseDeployments);
            targetCity.RemoveTroopAllocation(cityDefenseAllocation);
            campaign.Teams.AddRange(CreateTeams(
                cityDefenseDeployments,
                campaign.DefenderFactionId,
                CampaignBattleSide.Defender,
                playerFactionId,
                CampaignTeamLocation.InnerCity,
                originCityId: targetCity.Id));
        }

        for (var index = 0; index < campaign.Teams.Count; index++)
        {
            campaign.Teams[index].Id = index + 1;
        }
        ApplyInitialDeploymentCaps(campaign, CampaignBattleSide.Attacker);
        ApplyInitialDeploymentCaps(campaign, CampaignBattleSide.Defender);

        campaign.Participants.Add(CreateParticipant(
            campaign.AttackerFactionId,
            CampaignBattleSide.Attacker,
            true,
            campaign.AttackerGold,
            campaign.AttackerFood,
            campaign.Teams));
        campaign.Participants.Add(CreateParticipant(
            campaign.DefenderFactionId,
            CampaignBattleSide.Defender,
            true,
            0,
            0,
            campaign.Teams));
        world.ActiveBattleCampaigns.Add(campaign);
        return campaign;
    }

    internal static ActiveBattleCampaignData CreateCaravanEscortCampaign(
        WorldState world,
        CityData sourceCity,
        CityData targetCity,
        TroopAllocationData escort,
        IReadOnlyCollection<int> officerIds,
        int gold, int food, int horses, int wood, int metal, int stone,
        SiegeEngineAllocationData equipment)
    {
        var playerFactionId = world.Factions.FirstOrDefault(faction => faction.IsPlayer)?.Id ?? -1;
        var campaign = new ActiveBattleCampaignData
        {
            Id = world.ActiveBattleCampaigns.Count == 0 ? 1 : world.ActiveBattleCampaigns.Max(item => item.Id) + 1,
            // Bandits are intentionally factionless and officerless.
            AttackerFactionId = 0,
            DefenderFactionId = sourceCity.OwnerFactionId,
            SourceCityId = sourceCity.Id,
            TargetCityId = targetCity.Id,
            StartedYear = world.Year,
            StartedMonth = world.Month,
            CurrentYear = world.Year,
            CurrentMonth = world.Month,
            Stage = CampaignStage.FieldBattle,
            IsPlayerInvolved = sourceCity.OwnerFactionId == playerFactionId,
            IsCaravanEscortBattle = true,
            CaravanGold = gold,
            CaravanFood = food,
            CaravanHorses = horses,
            CaravanWood = wood,
            CaravanMetal = metal,
            CaravanStone = stone,
            CaravanEquipment = equipment
        };

        var nextId = 1;
        var availableOfficerIds = officerIds.Where(id => id > 0).Distinct().ToList();
        var troopTeams = new List<(TroopType Type, int Troops)>();
        foreach (var (type, count) in new[]
                 {
                     (TroopType.Infantry, escort.Infantry), (TroopType.Spearman, escort.Spearman),
                     (TroopType.Cavalry, escort.Cavalry), (TroopType.Archer, escort.Archer),
                     (TroopType.Crossbow, escort.Crossbow), (TroopType.Engineer, escort.Siege)
                 }.Where(item => item.Item2 > 0))
        {
            var remaining = count;
            while (remaining > 0)
            {
                var teamTroops = Math.Min(CaravanEscortTeamMaximumTroops, remaining);
                troopTeams.Add((type, teamTroops));
                remaining -= teamTroops;
            }
        }

        var hasCargoCart = gold + food + horses + wood + metal + stone > 0 || equipment.SupplyCart > 0;
        var equipmentTeamCount = (hasCargoCart ? 1 : 0) + equipment.Ram + equipment.Ladder + equipment.Catapult;
        var maximumTroopTeams = Math.Max(troopTeams.Count, MaximumActivePiecesPerSide - equipmentTeamCount);
        var desiredTroopTeams = Math.Min(availableOfficerIds.Count, maximumTroopTeams);
        while (troopTeams.Count < desiredTroopTeams)
        {
            var splitIndex = troopTeams
                .Select((team, index) => (team, index))
                .Where(item => item.team.Troops > 1)
                .OrderByDescending(item => item.team.Troops)
                .Select(item => item.index)
                .FirstOrDefault(-1);
            if (splitIndex < 0)
            {
                break;
            }

            var team = troopTeams[splitIndex];
            var firstTeamTroops = (team.Troops + 1) / 2;
            troopTeams[splitIndex] = (team.Type, firstTeamTroops);
            troopTeams.Insert(splitIndex + 1, (team.Type, team.Troops - firstTeamTroops));
        }

        for (var index = 0; index < troopTeams.Count; index++)
        {
            var team = troopTeams[index];
            campaign.Teams.Add(new CampaignBattleTeamData
            {
                Id = nextId++,
                FactionId = sourceCity.OwnerFactionId,
                Side = CampaignBattleSide.Defender,
                ControllerType = CampaignControllerType.Player,
                OfficerId = index < availableOfficerIds.Count ? availableOfficerIds[index] : 0,
                TroopType = team.Type,
                ActiveTroops = team.Troops,
                MaximumTroops = team.Troops,
                OriginCityId = sourceCity.Id
            });
        }

        // Cargo is represented by a real supply cart; all amounts remain attached
        // to the campaign until it reaches NE or returns through SW.
        if (hasCargoCart)
        {
            campaign.Teams.Add(new CampaignBattleTeamData { Id = nextId++, FactionId = sourceCity.OwnerFactionId, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Player, EquipmentType = BattleEquipmentType.SupplyCart, OriginCityId = sourceCity.Id, MaximumTroops = 1, ActiveTroops = 1 });
        }
        foreach (var equipmentType in Enumerable.Repeat(BattleEquipmentType.Ram, equipment.Ram)
                     .Concat(Enumerable.Repeat(BattleEquipmentType.Ladder, equipment.Ladder))
                     .Concat(Enumerable.Repeat(BattleEquipmentType.Catapult, equipment.Catapult)))
        {
            campaign.Teams.Add(new CampaignBattleTeamData { Id = nextId++, FactionId = sourceCity.OwnerFactionId, Side = CampaignBattleSide.Defender, ControllerType = CampaignControllerType.Player, EquipmentType = equipmentType, OriginCityId = sourceCity.Id, MaximumTroops = 1, ActiveTroops = 1 });
        }
        var banditCount = Math.Clamp(100 + (gold + food + horses + wood + metal + stone) / 30, 100, 450);
        var banditDailyFood = (int)Math.Ceiling(banditCount / 100.0) * DailyFoodPer100Troops;
        // Road ambushers bring their own supplies.  This keeps their ration
        // pool independent of cargo, while still allowing stolen carts to add
        // to it through the normal battle resource transfer rules.
        campaign.AttackerFood = banditDailyFood * CaravanBanditRationDays;
        campaign.Teams.Add(new CampaignBattleTeamData { Id = nextId, FactionId = 0, Side = CampaignBattleSide.Attacker, ControllerType = CampaignControllerType.Ai, TroopType = TroopType.Infantry, ActiveTroops = banditCount, MaximumTroops = banditCount, OriginCityId = 0, CooperationObjective = "bandits" });
        world.ActiveBattleCampaigns.Add(campaign);
        return campaign;
    }


    private static void ApplyInitialDeploymentCaps(ActiveBattleCampaignData campaign, CampaignBattleSide side)
    {
        var activeCount = 0;
        var siegeCount = 0;
        foreach (var team in campaign.Teams.Where(team => team.Side == side && IsActiveBattleLocation(campaign, team)).OrderBy(team => team.Id))
        {
            var exceedsTotal = activeCount >= MaximumActivePiecesPerSide;
            var exceedsSiege = team.EquipmentType != BattleEquipmentType.None && siegeCount >= MaximumSiegeEnginesPerSide;
            if (exceedsTotal || exceedsSiege)
            {
                team.Location = CampaignTeamLocation.Reserve;
                continue;
            }

            activeCount++;
            if (team.EquipmentType != BattleEquipmentType.None)
            {
                siegeCount++;
            }
        }
    }

    internal static bool IsActiveBattleLocation(ActiveBattleCampaignData campaign, CampaignBattleTeamData team) =>
        team.Location == CampaignTeamLocation.Field ||
        (campaign.Stage == CampaignStage.CityBattle && team.Location == CampaignTeamLocation.InnerCity);


    private static BattleParticipantData CreateParticipant(
        int factionId,
        CampaignBattleSide side,
        bool isMainFaction,
        int gold,
        int food,
        IEnumerable<CampaignBattleTeamData> teams)
    {
        var factionTeams = teams.Where(team => team.FactionId == factionId && team.Side == side).ToList();
        return new BattleParticipantData
        {
            FactionId = factionId,
            Side = side,
            IsMainFaction = isMainFaction,
            ControllerType = factionTeams.FirstOrDefault()?.ControllerType ?? CampaignControllerType.Ai,
            GoldContribution = gold,
            FoodContribution = food,
            TroopsCommitted = factionTeams.Sum(team => team.ActiveTroops)
        };
    }


    internal static List<CampaignBattleTeamData> CreateTeams(
        IEnumerable<AttackOfficerDeploymentData> deployments,
        int factionId,
        CampaignBattleSide side,
        int playerFactionId,
        CampaignTeamLocation defaultLocation = CampaignTeamLocation.Field,
        int reinforcementOrderId = 0,
        int originCityId = 0)
    {
        var teams = new List<CampaignBattleTeamData>();
        foreach (var deployment in deployments.Where(item => item.TroopCount > 0))
        {
            var location = deployment.TroopType == TroopType.Siege &&
                           deployment.SiegeEngineType is SiegeEngineType.Ram or SiegeEngineType.Ladder &&
                           defaultLocation == CampaignTeamLocation.Field
                ? CampaignTeamLocation.Reserve
                : defaultLocation;
            teams.Add(new CampaignBattleTeamData
            {
                Id = teams.Count + 1,
                FactionId = factionId,
                Side = side,
                ControllerType = factionId == playerFactionId ? CampaignControllerType.Player : CampaignControllerType.Ai,
                OfficerId = deployment.OfficerId,
                TroopType = deployment.TroopType,
                SiegeEngineType = deployment.SiegeEngineType,
                ActiveTroops = deployment.TroopCount,
                MaximumTroops = deployment.TroopCount,
                ReinforcementOrderId = reinforcementOrderId,
                OriginCityId = originCityId,
                Location = location
            });
        }

        return teams;
    }

    private static List<CampaignBattleTeamData> CreateBattleSupportTeams(
        BattleSupportDeploymentData? support,
        int factionId,
        CampaignBattleSide side,
        int playerFactionId,
        int originCityId,
        CampaignTeamLocation location = CampaignTeamLocation.Field)
    {
        support ??= new BattleSupportDeploymentData();
        var teams = new List<CampaignBattleTeamData>();
        if (support.EngineerTeamCount > 0)
        {
            teams.Add(new CampaignBattleTeamData
            {
                FactionId = factionId, Side = side,
                ControllerType = factionId == playerFactionId ? CampaignControllerType.Player : CampaignControllerType.Ai,
                TroopType = TroopType.Engineer, ActiveTroops = support.EngineerTeamCount,
                MaximumTroops = support.EngineerTeamCount, OriginCityId = originCityId, Location = location
            });
        }

        foreach (var equipmentType in new[] { BattleEquipmentType.SupplyCart, BattleEquipmentType.Ram, BattleEquipmentType.Ladder, BattleEquipmentType.Catapult })
        {
            var selected = equipmentType switch
            {
                BattleEquipmentType.SupplyCart => support.SupplyCart,
                BattleEquipmentType.Ram => support.Ram,
                BattleEquipmentType.Ladder => support.Ladder,
                BattleEquipmentType.Catapult => support.Catapult,
                _ => false
            };
            if (!selected) continue;
            teams.Add(new CampaignBattleTeamData
            {
                FactionId = factionId, Side = side,
                ControllerType = factionId == playerFactionId ? CampaignControllerType.Player : CampaignControllerType.Ai,
                TroopType = TroopType.Engineer, EquipmentType = equipmentType,
                ActiveTroops = 1, MaximumTroops = 1, OriginCityId = originCityId, Location = location
            });
        }
        return teams;
    }

    internal static List<AttackOfficerDeploymentData> CreateDefaultDefenderDeployments(
        CityData city,
        int troopPercent = 100,
        bool reserveOneOfficer = false,
        IReadOnlySet<int>? excludedOfficerIds = null)
    {
        var availableOfficerIds = city.OfficerIds
            .Where(id => excludedOfficerIds == null || !excludedOfficerIds.Contains(id))
            .ToList();
        var maximumOfficers = reserveOneOfficer && availableOfficerIds.Count > 1
            ? Math.Min(MaximumActivePiecesPerSide, availableOfficerIds.Count - 1)
            : MaximumActivePiecesPerSide;
        var officerIds = availableOfficerIds.Take(maximumOfficers).ToList();
        if (officerIds.Count == 0)
        {
            return new List<AttackOfficerDeploymentData>();
        }

        var troopPools = new List<(TroopType Type, int Count)>
        {
            (TroopType.Infantry, city.InfantryTroops * troopPercent / 100),
            (TroopType.Spearman, city.SpearmanTroops * troopPercent / 100),
            (TroopType.Cavalry, city.CavalryTroops * troopPercent / 100),
            (TroopType.Archer, city.ArcherTroops * troopPercent / 100),
            (TroopType.Crossbow, city.CrossbowTroops * troopPercent / 100),
            (TroopType.Siege, city.SiegeTroops * troopPercent / 100)
        }.Where(item => item.Count > 0).ToList();
        if (troopPools.Count == 0)
        {
            return new List<AttackOfficerDeploymentData>();
        }

        var result = new List<AttackOfficerDeploymentData>();
        for (var index = 0; index < officerIds.Count; index++)
        {
            var poolIndex = index % troopPools.Count;
            var remainingOfficersForPool = 1 + (officerIds.Count - index - 1) / troopPools.Count;
            var count = Math.Max(0, troopPools[poolIndex].Count / remainingOfficersForPool);
            if (count <= 0)
            {
                continue;
            }

            result.Add(new AttackOfficerDeploymentData
            {
                OfficerId = officerIds[index],
                TroopType = troopPools[poolIndex].Type,
                TroopCount = count
            });
            troopPools[poolIndex] = (troopPools[poolIndex].Type, troopPools[poolIndex].Count - count);
        }

        return result;
    }


    private static List<AttackOfficerDeploymentData> CloneDeployments(IEnumerable<AttackOfficerDeploymentData> deployments) =>
        deployments.Select(CloneDeployment).ToList();

    internal static AttackOfficerDeploymentData CloneDeployment(AttackOfficerDeploymentData item) => new()
    {
        OfficerId = item.OfficerId,
        TroopType = item.TroopType,
        TroopCount = item.TroopCount,
        SiegeEngineType = item.SiegeEngineType
    };

}
