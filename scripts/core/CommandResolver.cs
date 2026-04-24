using System;
using System.Collections.Generic;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class CommandResolver
{
    private const int DevelopGoldCost = 100;
    private const int RecruitGoldCost = 120;
    private const int RecruitFoodCost = 80;
    private const int MerchantFoodPerTrade = 100;
    private const int MerchantGoldPerTrade = 10;
    private const float FailedAttackSupplyReturnRatio = 0.5f;

    private readonly Random _random = new();

    private TurnManager? _turnManager;
    private CombatResolver? _combatResolver;
    private LocalizationService? _localization;

    public void Initialize(TurnManager turnManager, CombatResolver combatResolver, LocalizationService localization)
    {
        _turnManager = turnManager;
        _combatResolver = combatResolver;
        _localization = localization;
    }

    public CommandResult Execute(CommandRequest request)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var sourceCity = world.GetCity(request.SourceCityId);
        if (sourceCity == null)
        {
            return LocalizedResult(false, "cmd.source_city_not_found");
        }

        if (request.Type != CommandType.Pass && sourceCity.OwnerFactionId != request.ActorFactionId)
        {
            return LocalizedResult(false, "cmd.city_not_controlled");
        }

        return request.Type switch
        {
            CommandType.Develop => ScheduleDevelop(world, sourceCity, request),
            CommandType.Recruit => ScheduleRecruit(world, sourceCity, request),
            CommandType.Move => ScheduleMove(world, sourceCity, request),
            CommandType.Search => ScheduleSearch(world, sourceCity, request),
            CommandType.Merchant => ExecuteMerchant(world, sourceCity, request),
            CommandType.Attack => ScheduleAttack(world, sourceCity, request),
            CommandType.Pass => LocalizedResult(true, "cmd.pass"),
            _ => LocalizedResult(false, "cmd.unknown_command")
        };
    }

    public CommandResult ResolvePendingCommand(PendingCommandData pendingCommand)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var sourceCity = world.GetCity(pendingCommand.SourceCityId);
        if (sourceCity == null)
        {
            return LocalizedResult(false, "cmd.pending_source_city_not_found");
        }

        return pendingCommand.Type switch
        {
            CommandType.Develop => ResolveDevelop(world, sourceCity, pendingCommand),
            CommandType.Recruit => ResolveRecruit(world, sourceCity, pendingCommand),
            CommandType.Search => ResolveSearch(world, sourceCity, pendingCommand),
            CommandType.Move => ResolveMove(world, sourceCity, pendingCommand),
            CommandType.Attack => ResolveAttack(world, sourceCity, pendingCommand),
            _ => LocalizedResult(false, "cmd.unsupported_pending_command")
        };
    }

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

        if (city.Gold < RecruitGoldCost || city.Food < RecruitFoodCost)
        {
            return LocalizedResult(false, "cmd.recruit.not_enough_resources", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        city.Gold -= RecruitGoldCost;
        city.Food -= RecruitFoodCost;
        MarkRecruitUsed(world, city);
        MarkOfficerAssigned(world, assignedOfficer, CommandType.Recruit);
        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Recruit,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = city.Id,
            OfficerIds = new List<int> { assignedOfficer.Id }
        });

        return LocalizedResult(true, "cmd.recruit.scheduled", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
    }

    private CommandResult ResolveRecruit(WorldState world, CityData city, PendingCommandData pendingCommand)
    {
        var charm = GetAverageStat(world, city, officer => officer.Charm);
        var recruits = 80 + charm / 2 + _random.Next(0, 41);

        city.Troops += recruits;
        city.Loyalty = ClampStat(city.Loyalty - 3);

        return LocalizedResult(
            true,
            "cmd.recruit.resolved",
            new object[] { GetCityName(city, GameLanguage.TraditionalChinese), recruits, 3 },
            new object[] { GetCityName(city, GameLanguage.English), recruits, 3 });
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
        var movableTroops = GetTransferAmount(request.TroopsToSend, sourceCity.Troops);
        var movableGold = GetTransferAmount(request.GoldToSend, sourceCity.Gold);
        var movableFood = GetTransferAmount(request.FoodToSend, sourceCity.Food);

        if (movableTroops <= 0 && movableGold <= 0 && movableFood <= 0 && selectedOfficerIds.Count == 0)
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
            GoldToSend = movableGold,
            FoodToSend = movableFood,
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
        var intelligence = assignedOfficer?.Intelligence ?? GetAverageStat(world, city, officer => officer.Intelligence);
        var charm = assignedOfficer?.Charm ?? GetAverageStat(world, city, officer => officer.Charm);
        var chance = 0.25f + intelligence / 250.0f + charm / 300.0f;

        if (_random.NextDouble() > chance)
        {
            return LocalizedResult(true, "cmd.search.nothing_found", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        var hiddenOfficer = TryFindDiscoverableOfficer(world, city.OwnerFactionId);
        if (hiddenOfficer != null && _random.NextDouble() < 0.35)
        {
            hiddenOfficer.CityId = city.Id;
            hiddenOfficer.Loyalty = ClampStat(65 + _random.Next(0, 16));

            if (!city.OfficerIds.Contains(hiddenOfficer.Id))
            {
                city.OfficerIds.Add(hiddenOfficer.Id);
            }

            var faction = world.GetFaction(city.OwnerFactionId);
            if (faction != null && !faction.OfficerIds.Contains(hiddenOfficer.Id))
            {
                faction.OfficerIds.Add(hiddenOfficer.Id);
            }

            return LocalizedResult(
                true,
                "cmd.search.officer_joined",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), GetOfficerDisplayName(hiddenOfficer, GameLanguage.TraditionalChinese) },
                new object[] { GetCityName(city, GameLanguage.English), GetOfficerDisplayName(hiddenOfficer, GameLanguage.English) });
        }

        if (_random.NextDouble() < 0.5)
        {
            var foundGold = 40 + _random.Next(0, 81);
            city.Gold += foundGold;
            return LocalizedResult(
                true,
                "cmd.search.found_gold",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), foundGold },
                new object[] { GetCityName(city, GameLanguage.English), foundGold });
        }

        var foundFood = 60 + _random.Next(0, 121);
        city.Food += foundFood;
        return LocalizedResult(
            true,
            "cmd.search.found_food",
            new object[] { GetCityName(city, GameLanguage.TraditionalChinese), foundFood },
            new object[] { GetCityName(city, GameLanguage.English), foundFood });
    }

    private CommandResult ExecuteMerchant(WorldState world, CityData city, CommandRequest request)
    {
        var foodAmount = request.FoodToSend;
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

        var attackingTroops = request.TroopsToSend;
        var carriedGold = GetTransferAmount(request.GoldToSend, sourceCity.Gold);
        var carriedFood = GetTransferAmount(request.FoodToSend, sourceCity.Food);
        var selectedOfficerIds = GetMovableOfficerIds(sourceCity, request.OfficerIds);
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

        MarkOfficersAssigned(world, selectedOfficerIds, CommandType.Attack);
        // Reserve attack resources immediately so same-month orders see the reduced stock.
        sourceCity.Troops -= attackingTroops;
        sourceCity.Gold -= carriedGold;
        sourceCity.Food -= carriedFood;

        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Attack,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = sourceCity.Id,
            TargetCityId = targetCity.Id,
            TroopsToSend = attackingTroops,
            GoldToSend = carriedGold,
            FoodToSend = carriedFood,
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
        var movedOfficerCount = TransferOfficers(world, sourceCity, targetCity, pendingCommand.OfficerIds);

        if (movableTroops <= 0 && movableGold <= 0 && movableFood <= 0 && movedOfficerCount == 0)
        {
            return LocalizedResult(
                false,
                "cmd.move.no_effect",
                new object[] { GetCityName(sourceCity, GameLanguage.TraditionalChinese), GetCityName(targetCity, GameLanguage.TraditionalChinese) },
                new object[] { GetCityName(sourceCity, GameLanguage.English), GetCityName(targetCity, GameLanguage.English) });
        }

        sourceCity.Troops -= movableTroops;
        sourceCity.Gold -= movableGold;
        sourceCity.Food -= movableFood;

        targetCity.Troops += movableTroops;
        targetCity.Gold += movableGold;
        targetCity.Food += movableFood;

        return LocalizedResult(
            true,
            "cmd.move.resolved",
            new object[] { GetCityName(sourceCity, GameLanguage.TraditionalChinese), movableTroops, movableGold, movableFood, movedOfficerCount, GetCityName(targetCity, GameLanguage.TraditionalChinese) },
            new object[] { GetCityName(sourceCity, GameLanguage.English), movableTroops, movableGold, movableFood, movedOfficerCount, GetCityName(targetCity, GameLanguage.English) });
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
            // If the target becomes invalid before month end, return the reserved troops to the source city.
            sourceCity.Troops += pendingCommand.TroopsToSend;

            return LocalizedResult(
                false,
                "cmd.attack.cancelled",
                new object[] { GetCityName(sourceCity, GameLanguage.TraditionalChinese), GetCityName(targetCity, GameLanguage.TraditionalChinese) },
                new object[] { GetCityName(sourceCity, GameLanguage.English), GetCityName(targetCity, GameLanguage.English) });
        }

        var attackingTroops = pendingCommand.TroopsToSend;
        if (attackingTroops <= 0)
        {
            return LocalizedResult(false, "cmd.attack.no_troops_resolution");
        }

        var defendingFactionId = targetCity.OwnerFactionId;
        var combat = _combatResolver.Resolve(world, sourceCity, targetCity, attackingTroops, pendingCommand.OfficerIds);

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

        targetCity.Troops -= defenderLoss;
        if (targetCity.Troops < 0)
        {
            targetCity.Troops = 0;
        }

        if (!combat.AttackerWon)
        {
            var returnedGold = (int)(pendingCommand.GoldToSend * FailedAttackSupplyReturnRatio);
            var returnedFood = (int)(pendingCommand.FoodToSend * FailedAttackSupplyReturnRatio);
            // Only surviving attackers return; carried supply refund stays partial to preserve expedition risk.
            var returningTroops = attackingTroops - effectiveAttackerLoss;
            if (returningTroops > 0)
            {
                sourceCity.Troops += returningTroops;
            }

            sourceCity.Gold += returnedGold;
            sourceCity.Food += returnedFood;

            return LocalizedResult(
                true,
                "cmd.attack.failed",
                new object[] { GetCityName(sourceCity, GameLanguage.TraditionalChinese), GetCityName(targetCity, GameLanguage.TraditionalChinese), returnedGold, returnedFood },
                new object[] { GetCityName(sourceCity, GameLanguage.English), GetCityName(targetCity, GameLanguage.English), returnedGold, returnedFood });
        }

        targetCity.OwnerFactionId = sourceCity.OwnerFactionId;
        ResolveCapturedCityOfficers(world, targetCity, defendingFactionId);
        var garrison = attackingTroops - effectiveAttackerLoss;
        if (garrison < 100)
        {
            garrison = 100;
        }

        targetCity.Troops = garrison;
        targetCity.Gold += pendingCommand.GoldToSend;
        targetCity.Food += pendingCommand.FoodToSend;
        TransferOfficers(world, sourceCity, targetCity, pendingCommand.OfficerIds);
        sourceCity.Loyalty = ClampStat(sourceCity.Loyalty + 2);

        return LocalizedResult(
            true,
            "cmd.attack.success",
            new object[] { GetCityName(sourceCity, GameLanguage.TraditionalChinese), GetCityName(targetCity, GameLanguage.TraditionalChinese) },
            new object[] { GetCityName(sourceCity, GameLanguage.English), GetCityName(targetCity, GameLanguage.English) });
    }

    private CommandResult LocalizedResult(bool success, string key, object[]? args = null)
    {
        return LocalizedResult(success, key, args, args);
    }

    private CommandResult LocalizedResult(bool success, string key, object[]? zhArgs, object[]? enArgs)
    {
        var traditionalArgs = zhArgs ?? Array.Empty<object>();
        var englishArgs = enArgs ?? Array.Empty<object>();
        var zh = _localization?.FormatForLanguage(GameLanguage.TraditionalChinese, key, traditionalArgs) ?? key;
        var en = _localization?.FormatForLanguage(GameLanguage.English, key, englishArgs) ?? key;

        return new CommandResult
        {
            Success = success,
            Message = en,
            MessageZhHant = zh,
            MessageEn = en
        };
    }

    private object[] GetCityArgs(CityData city, GameLanguage language)
    {
        return new object[]
        {
            GetCityName(city, language)
        };
    }

    private object[] GetOfficerArgs(OfficerData officer, GameLanguage language)
    {
        return new object[]
        {
            GetOfficerDisplayName(officer, language)
        };
    }

    private static int GetAverageStat(WorldState world, CityData city, Func<OfficerData, int> selector)
    {
        var count = 0;
        var total = 0;
        foreach (var officerId in city.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            total += selector(officer);
            count += 1;
        }

        return count == 0 ? 50 : total / count;
    }

    private static int GetTransferAmount(int requestedAmount, int availableAmount)
    {
        var transferAmount = requestedAmount > 0 ? requestedAmount : availableAmount / 2;
        if (transferAmount > availableAmount)
        {
            transferAmount = availableAmount;
        }

        return transferAmount < 0 ? 0 : transferAmount;
    }

    private static List<int> GetMovableOfficerIds(CityData sourceCity, List<int> requestedOfficerIds)
    {
        var result = new List<int>();
        foreach (var officerId in requestedOfficerIds)
        {
            if (!sourceCity.OfficerIds.Contains(officerId) || result.Contains(officerId))
            {
                continue;
            }

            result.Add(officerId);
        }

        return result;
    }

    private static bool AreOfficerIdsAvailableForPendingOrder(
        WorldState world,
        List<int> requestedOfficerIds)
    {
        if (requestedOfficerIds.Count == 0)
        {
            return true;
        }

        foreach (var officerId in requestedOfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null || IsOfficerAssignedThisMonth(world, officer))
            {
                return false;
            }
        }

        return true;
    }

    private static OfficerData? GetSingleAvailableOfficer(WorldState world, CityData city, List<int> requestedOfficerIds)
    {
        if (requestedOfficerIds.Count != 1)
        {
            return null;
        }

        var officerId = requestedOfficerIds[0];
        if (!city.OfficerIds.Contains(officerId))
        {
            return null;
        }

        var officer = world.GetOfficer(officerId);
        if (officer == null || IsOfficerAssignedThisMonth(world, officer))
        {
            return null;
        }

        return officer;
    }

    private static bool IsOfficerAssignedThisMonth(WorldState world, OfficerData officer)
    {
        return officer.LastAssignedYear == world.Year && officer.LastAssignedMonth == world.Month;
    }

    private static void MarkOfficerAssigned(WorldState world, OfficerData officer, CommandType commandType)
    {
        officer.LastAssignedYear = world.Year;
        officer.LastAssignedMonth = world.Month;
        officer.LastAssignedCommand = commandType;
    }

    private static void MarkOfficersAssigned(WorldState world, List<int> officerIds, CommandType commandType)
    {
        foreach (var officerId in officerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            MarkOfficerAssigned(world, officer, commandType);
        }
    }

    private static int TransferOfficers(
        WorldState world,
        CityData sourceCity,
        CityData targetCity,
        List<int> requestedOfficerIds)
    {
        var movedOfficerCount = 0;
        foreach (var officerId in requestedOfficerIds)
        {
            if (!sourceCity.OfficerIds.Contains(officerId))
            {
                continue;
            }

            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            sourceCity.OfficerIds.Remove(officerId);
            if (!targetCity.OfficerIds.Contains(officerId))
            {
                targetCity.OfficerIds.Add(officerId);
            }

            officer.CityId = targetCity.Id;
            movedOfficerCount += 1;
        }

        return movedOfficerCount;
    }

    private OfficerData? TryFindDiscoverableOfficer(WorldState world, int factionId)
    {
        var candidates = new List<OfficerData>();
        foreach (var officer in world.Officers)
        {
            if (officer.CityId > 0)
            {
                continue;
            }

            candidates.Add(officer);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var preferred = new List<OfficerData>();
        foreach (var officer in candidates)
        {
            if (MatchesFaction(officer.Belongs, factionId))
            {
                preferred.Add(officer);
            }
        }

        var pool = preferred.Count > 0 ? preferred : candidates;
        return pool[_random.Next(pool.Count)];
    }

    private static bool MatchesFaction(string belongs, int factionId)
    {
        return factionId switch
        {
            1 => belongs.Equals("Shu", StringComparison.OrdinalIgnoreCase),
            2 => belongs.Equals("Wei", StringComparison.OrdinalIgnoreCase),
            3 => belongs.Equals("Wu", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string GetCityName(CityData city, GameLanguage language)
    {
        if (language == GameLanguage.TraditionalChinese)
        {
            if (!string.IsNullOrWhiteSpace(city.NameZhHant))
            {
                return city.NameZhHant;
            }

            if (!string.IsNullOrWhiteSpace(city.Name))
            {
                return city.Name;
            }

            return city.NameEn;
        }

        if (!string.IsNullOrWhiteSpace(city.NameEn))
        {
            return city.NameEn;
        }

        if (!string.IsNullOrWhiteSpace(city.Name))
        {
            return city.Name;
        }

        return city.NameZhHant;
    }

    private static string GetOfficerDisplayName(OfficerData officer, GameLanguage language)
    {
        if (language == GameLanguage.TraditionalChinese)
        {
            if (!string.IsNullOrWhiteSpace(officer.NameZhHant))
            {
                return officer.NameZhHant;
            }

            return officer.Name;
        }

        return !string.IsNullOrWhiteSpace(officer.Name) ? officer.Name : officer.NameZhHant;
    }

    private static void ResolveCapturedCityOfficers(WorldState world, CityData capturedCity, int previousFactionId)
    {
        if (capturedCity.OfficerIds.Count == 0)
        {
            return;
        }

        var retreatCity = FindRetreatCity(world, previousFactionId, capturedCity.Id);
        var displacedOfficerIds = new List<int>(capturedCity.OfficerIds);
        capturedCity.OfficerIds.Clear();

        foreach (var officerId in displacedOfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (retreatCity != null)
            {
                officer.CityId = retreatCity.Id;
                if (!retreatCity.OfficerIds.Contains(officer.Id))
                {
                    retreatCity.OfficerIds.Add(officer.Id);
                }
            }
            else
            {
                officer.CityId = 0;
            }
        }
    }

    private static CityData? FindRetreatCity(WorldState world, int factionId, int excludedCityId)
    {
        foreach (var city in world.Cities)
        {
            if (city.Id != excludedCityId && city.OwnerFactionId == factionId)
            {
                return city;
            }
        }

        return null;
    }

    private static bool HasUsedDevelop(WorldState world, CityData city)
    {
        return city.LastDevelopYear == world.Year && city.LastDevelopMonth == world.Month;
    }

    private static void MarkDevelopUsed(WorldState world, CityData city)
    {
        city.LastDevelopYear = world.Year;
        city.LastDevelopMonth = world.Month;
    }

    private static bool HasUsedRecruit(WorldState world, CityData city)
    {
        return city.LastRecruitYear == world.Year && city.LastRecruitMonth == world.Month;
    }

    private static void MarkRecruitUsed(WorldState world, CityData city)
    {
        city.LastRecruitYear = world.Year;
        city.LastRecruitMonth = world.Month;
    }

    private static bool HasUsedSearch(WorldState world, CityData city)
    {
        return city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month;
    }

    private static void MarkSearchUsed(WorldState world, CityData city)
    {
        city.LastSearchYear = world.Year;
        city.LastSearchMonth = world.Month;
    }

    private static void UpsertPendingCommand(WorldState world, PendingCommandData pendingCommand)
    {
        // Military orders can stack per source city; core city actions stay one-pending-per-type.
        if (pendingCommand.Type == CommandType.Move || pendingCommand.Type == CommandType.Attack)
        {
            world.PendingCommands.Add(pendingCommand);
            return;
        }

        world.PendingCommands.RemoveAll(existing =>
            existing.SourceCityId == pendingCommand.SourceCityId &&
            existing.Type == pendingCommand.Type);
        world.PendingCommands.Add(pendingCommand);
    }

    private static bool IsConnected(CityData source, int targetCityId)
    {
        return source.ConnectedCityIds.Contains(targetCityId);
    }

    private static int ClampStat(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value > 100)
        {
            return 100;
        }

        return value;
    }
}
