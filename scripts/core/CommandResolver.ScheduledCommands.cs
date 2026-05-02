using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    private CommandResult ScheduleDevelop(WorldState world, CityData city, CommandRequest request)
    {
        if (HasUsedDevelop(world, city))
        {
            return LocalizedResult(false, "cmd.develop.already_used", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var assignedOfficer = GetSingleAvailableOfficer(world, city, request.OfficerIds);
        if (assignedOfficer == null)
        {
            return request.OfficerIds.Count == 0
                ? LocalizedResult(false, "cmd.develop.officer_required", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English))
                : LocalizedResult(false, "cmd.develop.officer_unavailable", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        if (city.Gold < DevelopGoldCost)
        {
            return LocalizedResult(false, "cmd.develop.not_enough_gold", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        city.Gold -= DevelopGoldCost;
        MarkDevelopUsed(world, city);
        MarkOfficerAssigned(world, assignedOfficer, CommandType.Develop);
        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Develop,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = city.Id,
            OfficerIds = new List<int> { assignedOfficer.Id }
        });

        return LocalizedResult(true, "cmd.develop.scheduled", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
    }

    private CommandResult ResolveDevelop(WorldState world, CityData city, PendingCommandData pendingCommand)
    {
        var loyaltyBoost = city.Loyalty >= 80 ? 2 : 1;
        city.Farm = ClampStat(city.Farm + (2 + loyaltyBoost));
        city.Commercial = ClampStat(city.Commercial + (2 + loyaltyBoost));
        city.Defense = ClampStat(city.Defense + 1);
        city.Loyalty = ClampStat(city.Loyalty + 1);

        return LocalizedResult(
            true,
            "cmd.develop.resolved",
            new object[] { GetCityName(city, GameLanguage.TraditionalChinese), 2 + loyaltyBoost, 2 + loyaltyBoost, 1, 1 },
            new object[] { GetCityName(city, GameLanguage.English), 2 + loyaltyBoost, 2 + loyaltyBoost, 1, 1 });
    }

    private CommandResult ScheduleRecruit(WorldState world, CityData city, CommandRequest request)
    {
        if (HasUsedRecruit(world, city))
        {
            return LocalizedResult(false, "cmd.recruit.already_used", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var assignedOfficer = GetSingleAvailableOfficer(world, city, request.OfficerIds);
        if (assignedOfficer == null)
        {
            return request.OfficerIds.Count == 0
                ? LocalizedResult(false, "cmd.recruit.officer_required", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English))
                : LocalizedResult(false, "cmd.recruit.officer_unavailable", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var recruitTroopType = request.RecruitTroopType;
        var recruitGoldCost = GetTroopTypeRecruitGoldCost(recruitTroopType);
        var recruitFoodCost = GetTroopTypeRecruitFoodCost(recruitTroopType);
        if (!CanRecruitTroopType(city, recruitTroopType))
        {
            return LocalizedResult(false, "cmd.recruit.requirement_not_met", new object[]
            {
                GetCityName(city, GameLanguage.TraditionalChinese),
                GetTroopTypeName(recruitTroopType, GameLanguage.TraditionalChinese)
            }, new object[]
            {
                GetCityName(city, GameLanguage.English),
                GetTroopTypeName(recruitTroopType, GameLanguage.English)
            });
        }

        if (city.Gold < recruitGoldCost || city.Food < recruitFoodCost)
        {
            return LocalizedResult(false, "cmd.recruit.not_enough_resources", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        city.Gold -= recruitGoldCost;
        city.Food -= recruitFoodCost;
        MarkRecruitUsed(world, city);
        MarkOfficerAssigned(world, assignedOfficer, CommandType.Recruit);
        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Recruit,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = city.Id,
            RecruitTroopType = recruitTroopType,
            OfficerIds = new List<int> { assignedOfficer.Id }
        });

        return LocalizedResult(true, "cmd.recruit.scheduled", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
    }

    private CommandResult ResolveRecruit(WorldState world, CityData city, PendingCommandData pendingCommand)
    {
        var charm = GetAverageEffectiveStat(world, city, officer => officer.Charm, item => item.CharmBonus, OfficerProgressionStat.Charm);
        var recruits = 80 + charm / 2 + _random.Next(0, 41);
        if (pendingCommand.RecruitTroopType == TroopType.Cavalry)
        {
            recruits = Math.Min(recruits, city.Horses);
            city.Horses = Math.Max(0, city.Horses - recruits);
        }

        city.AddTroops(pendingCommand.RecruitTroopType, recruits);
        city.Loyalty = ClampStat(city.Loyalty - 3);

        return LocalizedResult(
            true,
            "cmd.recruit.resolved",
            new object[] { GetCityName(city, GameLanguage.TraditionalChinese), GetTroopTypeName(pendingCommand.RecruitTroopType, GameLanguage.TraditionalChinese), recruits, 3 },
            new object[] { GetCityName(city, GameLanguage.English), GetTroopTypeName(pendingCommand.RecruitTroopType, GameLanguage.English), recruits, 3 });
    }

    private CommandResult ScheduleMove(WorldState world, CityData sourceCity, CommandRequest request)
    {
        if (!request.TargetCityId.HasValue)
        {
            return LocalizedResult(false, "cmd.move.target_required");
        }

        var targetCity = world.GetCity(request.TargetCityId.Value);
        if (targetCity == null)
        {
            return LocalizedResult(false, "cmd.target_city_not_found");
        }

        if (!IsConnected(sourceCity, targetCity.Id))
        {
            return LocalizedResult(false, "cmd.target_city_not_connected");
        }

        if (targetCity.OwnerFactionId != sourceCity.OwnerFactionId)
        {
            return LocalizedResult(false, "cmd.move.must_be_same_faction");
        }

        if (!AreOfficerIdsAvailableForPendingOrder(world, request.OfficerIds))
        {
            return LocalizedResult(false, "cmd.move.officer_already_assigned", GetCityArgs(sourceCity, GameLanguage.TraditionalChinese), GetCityArgs(sourceCity, GameLanguage.English));
        }

        var selectedOfficerIds = GetMovableOfficerIds(sourceCity, request.OfficerIds);
        var troopAllocation = CreateTroopAllocationFromTotal(sourceCity, request.TroopsToSend);
        var movableTroops = troopAllocation.Total;
        var movableGold = GetTransferAmount(request.GoldToSend, sourceCity.Gold);
        var movableFood = GetTransferAmount(request.FoodToSend, sourceCity.Food);
        var movableHorses = GetTransferAmount(request.HorsesToSend, sourceCity.Horses);

        if (movableTroops <= 0 && movableGold <= 0 && movableFood <= 0 && movableHorses <= 0 && selectedOfficerIds.Count == 0)
        {
            return LocalizedResult(false, "cmd.move.nothing_to_move");
        }

        MarkOfficersAssigned(world, selectedOfficerIds, CommandType.Move);
        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Move,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = sourceCity.Id,
            TargetCityId = targetCity.Id,
            TroopsToSend = movableTroops,
            TroopAllocation = troopAllocation,
            GoldToSend = movableGold,
            FoodToSend = movableFood,
            HorsesToSend = movableHorses,
            OfficerIds = selectedOfficerIds
        });

        return LocalizedResult(
            true,
            "cmd.move.scheduled",
            new object[] { GetCityName(sourceCity, GameLanguage.TraditionalChinese), GetCityName(targetCity, GameLanguage.TraditionalChinese) },
            new object[] { GetCityName(sourceCity, GameLanguage.English), GetCityName(targetCity, GameLanguage.English) });
    }

    private CommandResult ScheduleSearch(WorldState world, CityData city, CommandRequest request)
    {
        if (HasUsedSearch(world, city))
        {
            return LocalizedResult(false, "cmd.search.already_used", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var assignedOfficer = GetSingleAvailableOfficer(world, city, request.OfficerIds);
        if (assignedOfficer == null)
        {
            return request.OfficerIds.Count == 0
                ? LocalizedResult(false, "cmd.search.officer_required", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English))
                : LocalizedResult(false, "cmd.search.officer_unavailable", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        MarkSearchUsed(world, city);
        MarkOfficerAssigned(world, assignedOfficer, CommandType.Search);
        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Search,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = city.Id,
            OfficerIds = new List<int> { assignedOfficer.Id }
        });

        return LocalizedResult(true, "cmd.search.scheduled", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
    }

    private CommandResult ResolveSearch(WorldState world, CityData city, PendingCommandData pendingCommand)
    {
        var assignedOfficer = pendingCommand.OfficerIds.Count > 0 ? world.GetOfficer(pendingCommand.OfficerIds[0]) : null;
        var intelligence = assignedOfficer != null
            ? GetEffectiveStat(world, assignedOfficer, officer => officer.Intelligence, item => item.IntelligenceBonus, OfficerProgressionStat.Intelligence)
            : GetAverageEffectiveStat(world, city, officer => officer.Intelligence, item => item.IntelligenceBonus, OfficerProgressionStat.Intelligence);
        var charm = assignedOfficer != null
            ? GetEffectiveStat(world, assignedOfficer, officer => officer.Charm, item => item.CharmBonus, OfficerProgressionStat.Charm)
            : GetAverageEffectiveStat(world, city, officer => officer.Charm, item => item.CharmBonus, OfficerProgressionStat.Charm);
        var chance = 0.25f + intelligence / 250.0f + charm / 300.0f;

        if (_random.NextDouble() > chance)
        {
            if (assignedOfficer != null)
            {
                OfficerProgressionRules.AwardStrategistExperience(assignedOfficer, 8);
            }
            return LocalizedResult(true, "cmd.search.nothing_found", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var hiddenOfficer = TryFindDiscoverableOfficer(world, city.OwnerFactionId, city.Id);
        if (hiddenOfficer != null && _random.NextDouble() < 0.35)
        {
            RecruitFreeOfficerToCity(world, city, hiddenOfficer);
            if (assignedOfficer != null)
            {
                OfficerProgressionRules.AwardStrategistExperience(assignedOfficer, 18);
            }

            return LocalizedResult(
                true,
                "cmd.search.officer_joined",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), GetOfficerDisplayName(hiddenOfficer, GameLanguage.TraditionalChinese) },
                new object[] { GetCityName(city, GameLanguage.English), GetOfficerDisplayName(hiddenOfficer, GameLanguage.English) });
        }

        var hiddenItem = TryFindDiscoverableItem(world, city.Id);
        if (hiddenItem != null && _random.NextDouble() < 0.45)
        {
            MoveItemToFactionInventory(hiddenItem, city.OwnerFactionId);
            if (assignedOfficer != null)
            {
                OfficerProgressionRules.AwardStrategistExperience(assignedOfficer, 15);
            }
            return LocalizedResult(
                true,
                "cmd.search.found_item",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), GetItemDisplayName(hiddenItem, GameLanguage.TraditionalChinese) },
                new object[] { GetCityName(city, GameLanguage.English), GetItemDisplayName(hiddenItem, GameLanguage.English) });
        }

        if (_random.NextDouble() < 0.5)
        {
            var foundGold = 40 + _random.Next(0, 81);
            city.Gold += foundGold;
            if (assignedOfficer != null)
            {
                OfficerProgressionRules.AwardStrategistExperience(assignedOfficer, 12);
            }
            return LocalizedResult(
                true,
                "cmd.search.found_gold",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), foundGold },
                new object[] { GetCityName(city, GameLanguage.English), foundGold });
        }

        var foundFood = 60 + _random.Next(0, 121);
        city.Food += foundFood;
        if (assignedOfficer != null)
        {
            OfficerProgressionRules.AwardStrategistExperience(assignedOfficer, 12);
        }
        return LocalizedResult(
            true,
            "cmd.search.found_food",
            new object[] { GetCityName(city, GameLanguage.TraditionalChinese), foundFood },
            new object[] { GetCityName(city, GameLanguage.English), foundFood });
    }

    private CommandResult ScheduleCivilRelief(WorldState world, CityData city, CommandRequest request)
    {
        if (HasUsedCivilRelief(world, city))
        {
            return LocalizedResult(false, "cmd.civil_relief.already_used", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var assignedOfficer = GetSingleAvailableOfficer(world, city, request.OfficerIds);
        if (assignedOfficer == null)
        {
            return request.OfficerIds.Count == 0
                ? LocalizedResult(false, "cmd.civil_relief.officer_required", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English))
                : LocalizedResult(false, "cmd.civil_relief.officer_unavailable", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        if (request.GoldToSend <= 0 && request.FoodToSend <= 0)
        {
            return LocalizedResult(false, "cmd.civil_relief.empty");
        }

        if (request.GoldToSend < 0 || request.FoodToSend < 0 || city.Gold < request.GoldToSend || city.Food < request.FoodToSend)
        {
            return LocalizedResult(false, "cmd.civil_relief.not_enough_resources", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var loyaltyGain = request.GoldToSend / CivilReliefGoldPerTenLoyalty * 10 + request.FoodToSend / CivilReliefFoodPerTenLoyalty * 10;
        if (loyaltyGain <= 0)
        {
            return LocalizedResult(false, "cmd.civil_relief.too_small");
        }

        city.Gold -= request.GoldToSend;
        city.Food -= request.FoodToSend;
        MarkCivilReliefUsed(world, city);
        MarkOfficerAssigned(world, assignedOfficer, CommandType.CivilRelief);
        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.CivilRelief,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = city.Id,
            GoldToSend = request.GoldToSend,
            FoodToSend = request.FoodToSend,
            OfficerIds = new List<int> { assignedOfficer.Id }
        });

        return LocalizedResult(true, "cmd.civil_relief.scheduled", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
    }

    private CommandResult ResolveCivilRelief(WorldState world, CityData city, PendingCommandData pendingCommand)
    {
        var loyaltyGain = pendingCommand.GoldToSend / CivilReliefGoldPerTenLoyalty * 10 +
                          pendingCommand.FoodToSend / CivilReliefFoodPerTenLoyalty * 10;
        city.Loyalty = ClampStat(city.Loyalty + loyaltyGain);

        return LocalizedResult(
            true,
            "cmd.civil_relief.resolved",
            new object[] { GetCityName(city, GameLanguage.TraditionalChinese), pendingCommand.GoldToSend, pendingCommand.FoodToSend, loyaltyGain },
            new object[] { GetCityName(city, GameLanguage.English), pendingCommand.GoldToSend, pendingCommand.FoodToSend, loyaltyGain });
    }

    private CommandResult ExecuteMerchant(WorldState world, CityData city, CommandRequest request)
    {
        var amount = request.FoodToSend;
        if (request.MerchantTradeMode == MerchantTradeMode.BuyHorse)
        {
            if (amount <= 0 || amount % MerchantHorsePerTrade != 0)
            {
                return LocalizedResult(false, "cmd.merchant.invalid_amount", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
            }

            var goldCost = amount / MerchantHorsePerTrade * MerchantGoldPerHorseTrade;
            if (city.Gold < goldCost)
            {
                return LocalizedResult(false, "cmd.merchant.not_enough_gold", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
            }

            city.Gold -= goldCost;
            city.Horses += amount;
            return LocalizedResult(
                true,
                "cmd.merchant.buy_horse_success",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), goldCost, amount },
                new object[] { GetCityName(city, GameLanguage.English), goldCost, amount });
        }

        var foodAmount = amount;
        if (foodAmount <= 0 || foodAmount % MerchantFoodPerTrade != 0)
        {
            return LocalizedResult(false, "cmd.merchant.invalid_amount", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var goldAmount = foodAmount / MerchantFoodPerTrade * MerchantGoldPerTrade;
        if (request.SellFood)
        {
            if (city.Food < foodAmount)
            {
                return LocalizedResult(false, "cmd.merchant.not_enough_food", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
            }

            city.Food -= foodAmount;
            city.Gold += goldAmount;
            return LocalizedResult(
                true,
                "cmd.merchant.sell_success",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), foodAmount, goldAmount },
                new object[] { GetCityName(city, GameLanguage.English), foodAmount, goldAmount });
        }

        if (city.Gold < goldAmount)
        {
            return LocalizedResult(false, "cmd.merchant.not_enough_gold", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        city.Gold -= goldAmount;
        city.Food += foodAmount;
        return LocalizedResult(
            true,
            "cmd.merchant.buy_success",
            new object[] { GetCityName(city, GameLanguage.TraditionalChinese), goldAmount, foodAmount },
            new object[] { GetCityName(city, GameLanguage.English), goldAmount, foodAmount });
    }

    private CommandResult ScheduleAttack(WorldState world, CityData sourceCity, CommandRequest request)
    {
        if (!request.TargetCityId.HasValue)
        {
            return LocalizedResult(false, "cmd.attack.target_required");
        }

        var targetCity = world.GetCity(request.TargetCityId.Value);
        if (targetCity == null)
        {
            return LocalizedResult(false, "cmd.target_city_not_found");
        }

        if (!IsConnected(sourceCity, targetCity.Id))
        {
            return LocalizedResult(false, "cmd.target_city_not_connected");
        }

        if (targetCity.OwnerFactionId == sourceCity.OwnerFactionId)
        {
            return LocalizedResult(false, "cmd.attack.same_faction");
        }

        if (!AreOfficerIdsAvailableForPendingOrder(world, request.OfficerIds))
        {
            return LocalizedResult(false, "cmd.attack.officer_already_assigned", GetCityArgs(sourceCity, GameLanguage.TraditionalChinese), GetCityArgs(sourceCity, GameLanguage.English));
        }

        var carriedGold = GetTransferAmount(request.GoldToSend, sourceCity.Gold);
        var carriedFood = GetTransferAmount(request.FoodToSend, sourceCity.Food);
        var selectedOfficerIds = request.AttackOfficerDeployments.Count > 0
            ? GetMovableOfficerIds(sourceCity, request.AttackOfficerDeployments.Select(item => item.OfficerId).Distinct().ToList())
            : GetMovableOfficerIds(sourceCity, request.OfficerIds);
        var validDeployments = request.AttackOfficerDeployments
            .Where(item => item.TroopCount > 0 && selectedOfficerIds.Contains(item.OfficerId))
            .Select(item => new AttackOfficerDeploymentData
            {
                OfficerId = item.OfficerId,
                TroopType = item.TroopType,
                TroopCount = item.TroopCount
            })
            .ToList();
        var troopAllocation = validDeployments.Count > 0
            ? CreateTroopAllocationFromAttackDeployments(validDeployments)
            : CreateTroopAllocationFromTotal(sourceCity, request.TroopsToSend);
        var attackingTroops = troopAllocation.Total;
        if (selectedOfficerIds.Count == 0)
        {
            return LocalizedResult(false, "cmd.attack.officer_required", GetCityArgs(sourceCity, GameLanguage.TraditionalChinese), GetCityArgs(sourceCity, GameLanguage.English));
        }

        if (attackingTroops <= 0)
        {
            return LocalizedResult(false, "cmd.attack.no_troops");
        }

        if (attackingTroops > sourceCity.Troops)
        {
            return LocalizedResult(false, "cmd.attack.too_many_troops", GetCityArgs(sourceCity, GameLanguage.TraditionalChinese), GetCityArgs(sourceCity, GameLanguage.English));
        }

        if (troopAllocation.Infantry > sourceCity.InfantryTroops ||
            troopAllocation.Spearman > sourceCity.SpearmanTroops ||
            troopAllocation.Cavalry > sourceCity.CavalryTroops ||
            troopAllocation.Archer > sourceCity.ArcherTroops ||
            troopAllocation.Crossbow > sourceCity.CrossbowTroops ||
            troopAllocation.Siege > sourceCity.SiegeTroops)
        {
            return LocalizedResult(false, "cmd.attack.too_many_troops", GetCityArgs(sourceCity, GameLanguage.TraditionalChinese), GetCityArgs(sourceCity, GameLanguage.English));
        }

        MarkOfficersAssigned(world, selectedOfficerIds, CommandType.Attack);
        // Reserve attack resources immediately so same-month orders see the reduced stock.
        sourceCity.RemoveTroopAllocation(troopAllocation);
        sourceCity.Gold -= carriedGold;
        sourceCity.Food -= carriedFood;

        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Attack,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = sourceCity.Id,
            TargetCityId = targetCity.Id,
            TroopsToSend = attackingTroops,
            TroopAllocation = troopAllocation,
            GoldToSend = carriedGold,
            FoodToSend = carriedFood,
            AttackOfficerDeployments = validDeployments,
            OfficerIds = selectedOfficerIds
        });

        return LocalizedResult(
            true,
            "cmd.attack.scheduled",
            new object[] { GetCityName(sourceCity, GameLanguage.TraditionalChinese), GetCityName(targetCity, GameLanguage.TraditionalChinese) },
            new object[] { GetCityName(sourceCity, GameLanguage.English), GetCityName(targetCity, GameLanguage.English) });
    }

    private CommandResult ResolveMove(WorldState world, CityData sourceCity, PendingCommandData pendingCommand)
    {
        var targetCity = world.GetCity(pendingCommand.TargetCityId);
        if (targetCity == null)
        {
            return LocalizedResult(false, "cmd.move.target_not_found_resolution");
        }

        if (!IsConnected(sourceCity, targetCity.Id) || targetCity.OwnerFactionId != sourceCity.OwnerFactionId)
        {
            return LocalizedResult(
                false,
                "cmd.move.cancelled",
                new object[] { GetCityName(sourceCity, GameLanguage.TraditionalChinese), GetCityName(targetCity, GameLanguage.TraditionalChinese) },
                new object[] { GetCityName(sourceCity, GameLanguage.English), GetCityName(targetCity, GameLanguage.English) });
        }

        var movableTroops = GetTransferAmount(pendingCommand.TroopsToSend, sourceCity.Troops);
        var movableGold = GetTransferAmount(pendingCommand.GoldToSend, sourceCity.Gold);
        var movableFood = GetTransferAmount(pendingCommand.FoodToSend, sourceCity.Food);
        var movableHorses = GetTransferAmount(pendingCommand.HorsesToSend, sourceCity.Horses);
        var movedOfficerCount = TransferOfficers(world, sourceCity, targetCity, pendingCommand.OfficerIds);

        if (movableTroops <= 0 && movableGold <= 0 && movableFood <= 0 && movableHorses <= 0 && movedOfficerCount == 0)
        {
            return LocalizedResult(
                false,
                "cmd.move.no_effect",
                new object[] { GetCityName(sourceCity, GameLanguage.TraditionalChinese), GetCityName(targetCity, GameLanguage.TraditionalChinese) },
                new object[] { GetCityName(sourceCity, GameLanguage.English), GetCityName(targetCity, GameLanguage.English) });
        }

        sourceCity.RemoveTroopAllocation(pendingCommand.TroopAllocation);
        sourceCity.Gold -= movableGold;
        sourceCity.Food -= movableFood;
        sourceCity.Horses -= movableHorses;

        targetCity.AddTroopAllocation(pendingCommand.TroopAllocation);
        targetCity.Gold += movableGold;
        targetCity.Food += movableFood;
        targetCity.Horses += movableHorses;

        return LocalizedResult(
            true,
            "cmd.move.resolved",
            new object[] { GetCityName(sourceCity, GameLanguage.TraditionalChinese), movableTroops, movableGold, movableFood, movableHorses, movedOfficerCount, GetCityName(targetCity, GameLanguage.TraditionalChinese) },
            new object[] { GetCityName(sourceCity, GameLanguage.English), movableTroops, movableGold, movableFood, movableHorses, movedOfficerCount, GetCityName(targetCity, GameLanguage.English) });
    }

    private CommandResult ResolveAttack(WorldState world, CityData sourceCity, PendingCommandData pendingCommand)
    {
        if (_combatResolver == null)
        {
            return LocalizedResult(false, "cmd.combat_not_initialized");
        }

        var targetCity = world.GetCity(pendingCommand.TargetCityId);
        if (targetCity == null)
        {
            return LocalizedResult(false, "cmd.attack.target_not_found_resolution");
        }

        if (!IsConnected(sourceCity, targetCity.Id) || targetCity.OwnerFactionId == sourceCity.OwnerFactionId)
        {
            // If the target becomes invalid before month end, return all reserved troops and supplies.
            sourceCity.AddTroopAllocation(pendingCommand.TroopAllocation);
            sourceCity.Gold += pendingCommand.GoldToSend;
            sourceCity.Food += pendingCommand.FoodToSend;

            return LocalizedResult(
                false,
                "cmd.attack.cancelled",
                new object[]
                {
                    GetCityName(sourceCity, GameLanguage.TraditionalChinese),
                    GetCityName(targetCity, GameLanguage.TraditionalChinese),
                    pendingCommand.TroopsToSend,
                    pendingCommand.GoldToSend,
                    pendingCommand.FoodToSend
                },
                new object[]
                {
                    GetCityName(sourceCity, GameLanguage.English),
                    GetCityName(targetCity, GameLanguage.English),
                    pendingCommand.TroopsToSend,
                    pendingCommand.GoldToSend,
                    pendingCommand.FoodToSend
                });
        }

        var attackingTroops = pendingCommand.TroopsToSend;
        if (attackingTroops <= 0)
        {
            return LocalizedResult(false, "cmd.attack.no_troops_resolution");
        }

        var defendingFactionId = targetCity.OwnerFactionId;
        var defendingOfficerIds = new List<int>(targetCity.OfficerIds);
        var combat = _combatResolver.Resolve(world, sourceCity, targetCity, attackingTroops, pendingCommand.OfficerIds, pendingCommand.AttackOfficerDeployments, pendingCommand.TroopAllocation);

        var effectiveAttackerLoss = combat.AttackerLosses;
        if (effectiveAttackerLoss > attackingTroops)
        {
            effectiveAttackerLoss = attackingTroops;
        }

        var defenderLoss = combat.DefenderLosses;
        if (defenderLoss > targetCity.Troops)
        {
            defenderLoss = targetCity.Troops;
        }

        var defenderLossAllocation = CreateTroopAllocationFromCityProportion(targetCity, defenderLoss);
        targetCity.RemoveTroopAllocation(defenderLossAllocation);

        if (!combat.AttackerWon)
        {
            AwardBattleExperience(world, pendingCommand.OfficerIds, 16);
            AwardBattleExperience(world, defendingOfficerIds, 22);
            var returnedGold = (int)(pendingCommand.GoldToSend * FailedAttackSupplyReturnRatio);
            var returnedFood = (int)(pendingCommand.FoodToSend * FailedAttackSupplyReturnRatio);
            // Only surviving attackers return; carried supply refund stays partial to preserve expedition risk.
            var returningTroops = attackingTroops - effectiveAttackerLoss;
            if (returningTroops > 0)
            {
                var survivors = ScaleTroopAllocationToTotal(pendingCommand.TroopAllocation, returningTroops);
                sourceCity.AddTroopAllocation(survivors);
            }

            sourceCity.Gold += returnedGold;
            sourceCity.Food += returnedFood;

            return LocalizedResult(
                true,
                "cmd.attack.failed",
                new object[]
                {
                    GetRulerDisplayName(world, sourceCity.OwnerFactionId, GameLanguage.TraditionalChinese),
                    GetCityName(sourceCity, GameLanguage.TraditionalChinese),
                    GetRulerDisplayName(world, targetCity.OwnerFactionId, GameLanguage.TraditionalChinese),
                    GetCityName(targetCity, GameLanguage.TraditionalChinese),
                    returningTroops,
                    returnedGold,
                    returnedFood
                },
                new object[]
                {
                    GetRulerDisplayName(world, sourceCity.OwnerFactionId, GameLanguage.English),
                    GetCityName(sourceCity, GameLanguage.English),
                    GetRulerDisplayName(world, targetCity.OwnerFactionId, GameLanguage.English),
                    GetCityName(targetCity, GameLanguage.English),
                    returningTroops,
                    returnedGold,
                    returnedFood
                });
        }

        targetCity.OwnerFactionId = sourceCity.OwnerFactionId;
        AwardBattleExperience(world, pendingCommand.OfficerIds, 26);
        AwardBattleExperience(world, defendingOfficerIds, 12);
        ResolveCapturedCityOfficers(world, targetCity, defendingFactionId);
        var garrison = attackingTroops - effectiveAttackerLoss;
        if (garrison < 100)
        {
            garrison = 100;
        }

        var garrisonAllocation = ScaleTroopAllocationToTotal(pendingCommand.TroopAllocation, garrison);
        targetCity.AddTroopAllocation(garrisonAllocation);
        targetCity.Gold += pendingCommand.GoldToSend;
        targetCity.Food += pendingCommand.FoodToSend;
        TransferOfficers(world, sourceCity, targetCity, pendingCommand.OfficerIds);
        sourceCity.Loyalty = ClampStat(sourceCity.Loyalty + 2);

        return LocalizedResult(
            true,
            "cmd.attack.success",
            new object[]
            {
                GetRulerDisplayName(world, sourceCity.OwnerFactionId, GameLanguage.TraditionalChinese),
                GetCityName(sourceCity, GameLanguage.TraditionalChinese),
                GetRulerDisplayName(world, targetCity.OwnerFactionId, GameLanguage.TraditionalChinese),
                GetCityName(targetCity, GameLanguage.TraditionalChinese),
                garrison,
                pendingCommand.GoldToSend,
                pendingCommand.FoodToSend
            },
            new object[]
            {
                GetRulerDisplayName(world, sourceCity.OwnerFactionId, GameLanguage.English),
                GetCityName(sourceCity, GameLanguage.English),
                GetRulerDisplayName(world, targetCity.OwnerFactionId, GameLanguage.English),
                GetCityName(targetCity, GameLanguage.English),
                garrison,
                pendingCommand.GoldToSend,
                pendingCommand.FoodToSend
            });
    }


}
