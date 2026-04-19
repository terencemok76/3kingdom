using System;
using System.Collections.Generic;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class CommandResolver
{
    private const int DevelopGoldCost = 100;
    private const int RecruitGoldCost = 120;
    private const int RecruitFoodCost = 80;

    private readonly Random _random = new();

    private TurnManager? _turnManager;
    private CombatResolver? _combatResolver;

    public void Initialize(TurnManager turnManager, CombatResolver combatResolver)
    {
        _turnManager = turnManager;
        _combatResolver = combatResolver;
    }

    public CommandResult Execute(CommandRequest request)
    {
        if (_turnManager?.World == null)
        {
            return new CommandResult { Success = false, Message = "World not initialized." };
        }

        var world = _turnManager.World;
        var sourceCity = world.GetCity(request.SourceCityId);
        if (sourceCity == null)
        {
            return new CommandResult { Success = false, Message = "Source city not found." };
        }

        if (request.Type != CommandType.Pass && sourceCity.OwnerFactionId != request.ActorFactionId)
        {
            return new CommandResult { Success = false, Message = "City is not controlled by actor faction." };
        }

        return request.Type switch
        {
            CommandType.Develop => ScheduleDevelop(world, sourceCity, request),
            CommandType.Recruit => ScheduleRecruit(world, sourceCity, request),
            CommandType.Move => ScheduleMove(world, sourceCity, request),
            CommandType.Search => ExecuteSearch(world, sourceCity),
            CommandType.Attack => ScheduleAttack(world, sourceCity, request),
            CommandType.Pass => new CommandResult { Success = true, Message = "Pass" },
            _ => new CommandResult { Success = false, Message = "Unknown command." }
        };
    }

    public CommandResult ResolvePendingCommand(PendingCommandData pendingCommand)
    {
        if (_turnManager?.World == null)
        {
            return new CommandResult { Success = false, Message = "World not initialized." };
        }

        var world = _turnManager.World;
        var sourceCity = world.GetCity(pendingCommand.SourceCityId);
        if (sourceCity == null)
        {
            return new CommandResult { Success = false, Message = "Pending source city not found." };
        }

        return pendingCommand.Type switch
        {
            CommandType.Develop => ResolveDevelop(world, sourceCity),
            CommandType.Recruit => ResolveRecruit(world, sourceCity),
            CommandType.Move => ResolveMove(world, sourceCity, pendingCommand),
            CommandType.Attack => ResolveAttack(world, sourceCity, pendingCommand),
            _ => new CommandResult { Success = false, Message = "Unsupported pending command." }
        };
    }

    private CommandResult ScheduleDevelop(WorldState world, CityData city, CommandRequest request)
    {
        if (HasUsedCoreAction(world, city))
        {
            return new CommandResult { Success = false, Message = "Core city action already used this month." };
        }

        if (city.Gold < DevelopGoldCost)
        {
            return new CommandResult { Success = false, Message = "Not enough gold for Develop." };
        }

        MarkCoreActionUsed(world, city);
        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Develop,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = city.Id
        });

        return new CommandResult
        {
            Success = true,
            Message = $"Develop order scheduled for \"{city.Name}\"."
        };
    }

    private CommandResult ResolveDevelop(WorldState world, CityData city)
    {
        if (city.Gold < DevelopGoldCost)
        {
            return new CommandResult { Success = false, Message = "Develop failed at month end: not enough gold." };
        }

        city.Gold -= DevelopGoldCost;

        var loyaltyBoost = city.Loyalty >= 80 ? 2 : 1;
        city.Farm = ClampStat(city.Farm + (2 + loyaltyBoost));
        city.Commercial = ClampStat(city.Commercial + (2 + loyaltyBoost));
        city.Defense = ClampStat(city.Defense + 1);
        city.Loyalty = ClampStat(city.Loyalty + 1);

        return new CommandResult
        {
            Success = true,
            Message = $"Develop success. Gold-{DevelopGoldCost}. Farm+{2 + loyaltyBoost}, Commercial+{2 + loyaltyBoost}, Defense+1, Loyalty+1."
        };
    }

    private CommandResult ScheduleRecruit(WorldState world, CityData city, CommandRequest request)
    {
        if (HasUsedCoreAction(world, city))
        {
            return new CommandResult { Success = false, Message = "Core city action already used this month." };
        }

        if (city.Gold < RecruitGoldCost || city.Food < RecruitFoodCost)
        {
            return new CommandResult { Success = false, Message = "Not enough resources for Recruit." };
        }

        MarkCoreActionUsed(world, city);
        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Recruit,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = city.Id
        });

        return new CommandResult
        {
            Success = true,
            Message = $"Recruit order scheduled for \"{city.Name}\"."
        };
    }

    private CommandResult ResolveRecruit(WorldState world, CityData city)
    {
        if (city.Gold < RecruitGoldCost || city.Food < RecruitFoodCost)
        {
            return new CommandResult { Success = false, Message = "Recruit failed at month end: not enough resources." };
        }

        city.Gold -= RecruitGoldCost;
        city.Food -= RecruitFoodCost;

        var charm = GetAverageStat(world, city, officer => officer.Charm);
        var recruits = 80 + charm / 2 + _random.Next(0, 41);

        city.Troops += recruits;
        city.Loyalty = ClampStat(city.Loyalty - 3);

        return new CommandResult
        {
            Success = true,
            Message = $"Recruit success. Gold-{RecruitGoldCost}, Food-{RecruitFoodCost}, Troops+{recruits}, Loyalty-3."
        };
    }

    private CommandResult ScheduleMove(WorldState world, CityData sourceCity, CommandRequest request)
    {
        if (!request.TargetCityId.HasValue)
        {
            return new CommandResult { Success = false, Message = "Move needs a target city." };
        }

        var targetCity = world.GetCity(request.TargetCityId.Value);
        if (targetCity == null)
        {
            return new CommandResult { Success = false, Message = "Target city not found." };
        }

        if (!IsConnected(sourceCity, targetCity.Id))
        {
            return new CommandResult { Success = false, Message = "Target city is not connected." };
        }

        if (targetCity.OwnerFactionId != sourceCity.OwnerFactionId)
        {
            return new CommandResult { Success = false, Message = "Move target must be same faction city." };
        }

        var selectedOfficerIds = GetMovableOfficerIds(sourceCity, request.OfficerIds);
        var movableTroops = GetTransferAmount(request.TroopsToSend, sourceCity.Troops);
        var movableGold = GetTransferAmount(request.GoldToSend, sourceCity.Gold);
        var movableFood = GetTransferAmount(request.FoodToSend, sourceCity.Food);

        if (movableTroops <= 0 && movableGold <= 0 && movableFood <= 0 && selectedOfficerIds.Count == 0)
        {
            return new CommandResult { Success = false, Message = "No troops, gold, food, or officers available to move." };
        }

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

        return new CommandResult
        {
            Success = true,
            Message = $"Move order scheduled for \"{targetCity.Name}\".",
            MessageZhHant = $"已排定移動命令，將於月末轉移至「{targetCity.NameZhHant}」。",
            MessageEn = $"Move order scheduled for \"{targetCity.NameEn}\"."
        };
    }

    private CommandResult ExecuteSearch(WorldState world, CityData city)
    {
        if (HasUsedCoreAction(world, city))
        {
            return new CommandResult
            {
                Success = false,
                Message = "Core city action already used this month.",
                MessageEn = "Core city action already used this month."
            };
        }

        if (city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month)
        {
            return new CommandResult
            {
                Success = false,
                Message = "Search already used in this city this month.",
                MessageZhHant = "本月此城市已執行過搜索。",
                MessageEn = "Search already used in this city this month."
            };
        }

        MarkCoreActionUsed(world, city);
        city.LastSearchYear = world.Year;
        city.LastSearchMonth = world.Month;

        var intelligence = GetAverageStat(world, city, officer => officer.Intelligence);
        var charm = GetAverageStat(world, city, officer => officer.Charm);
        var chance = 0.25f + intelligence / 250.0f + charm / 300.0f;

        if (_random.NextDouble() > chance)
        {
            return new CommandResult { Success = true, Message = "Search found nothing." };
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

            return new CommandResult
            {
                Success = true,
                Message = $"Search success. Officer \"{hiddenOfficer.Name}\" joined the city.",
                MessageZhHant = $"搜索成功。武將「{GetOfficerDisplayName(hiddenOfficer)}」加入了城市。",
                MessageEn = $"Search success. Officer \"{hiddenOfficer.Name}\" joined the city."
            };
        }

        if (_random.NextDouble() < 0.5)
        {
            var foundGold = 40 + _random.Next(0, 81);
            city.Gold += foundGold;
            return new CommandResult
            {
                Success = true,
                Message = $"Search success. Gold+{foundGold}.",
                MessageZhHant = $"搜索成功。金 +{foundGold}。",
                MessageEn = $"Search success. Gold+{foundGold}."
            };
        }

        var foundFood = 60 + _random.Next(0, 121);
        city.Food += foundFood;
        return new CommandResult
        {
            Success = true,
            Message = $"Search success. Food+{foundFood}.",
            MessageZhHant = $"搜索成功。糧 +{foundFood}。",
            MessageEn = $"Search success. Food+{foundFood}."
        };
    }

    private CommandResult ScheduleAttack(WorldState world, CityData sourceCity, CommandRequest request)
    {
        if (!request.TargetCityId.HasValue)
        {
            return new CommandResult { Success = false, Message = "Attack needs a target city." };
        }

        var targetCity = world.GetCity(request.TargetCityId.Value);
        if (targetCity == null)
        {
            return new CommandResult { Success = false, Message = "Target city not found." };
        }

        if (!IsConnected(sourceCity, targetCity.Id))
        {
            return new CommandResult { Success = false, Message = "Target city is not connected." };
        }

        if (targetCity.OwnerFactionId == sourceCity.OwnerFactionId)
        {
            return new CommandResult { Success = false, Message = "Cannot attack same faction city." };
        }

        var attackingTroops = GetTransferAmount(request.TroopsToSend, sourceCity.Troops);
        if (attackingTroops <= 0)
        {
            return new CommandResult { Success = false, Message = "No troops available for attack." };
        }

        UpsertPendingCommand(world, new PendingCommandData
        {
            Type = CommandType.Attack,
            ActorFactionId = request.ActorFactionId,
            SourceCityId = sourceCity.Id,
            TargetCityId = targetCity.Id,
            TroopsToSend = attackingTroops
        });

        return new CommandResult
        {
            Success = true,
            Message = $"Attack order scheduled against \"{targetCity.Name}\".",
            MessageZhHant = $"已排定攻擊命令，月末將進攻「{targetCity.NameZhHant}」。",
            MessageEn = $"Attack order scheduled against \"{targetCity.NameEn}\"."
        };
    }

    private CommandResult ResolveMove(WorldState world, CityData sourceCity, PendingCommandData pendingCommand)
    {
        var targetCity = world.GetCity(pendingCommand.TargetCityId);
        if (targetCity == null)
        {
            return new CommandResult { Success = false, Message = "Move target city not found at resolution." };
        }

        if (!IsConnected(sourceCity, targetCity.Id) || targetCity.OwnerFactionId != sourceCity.OwnerFactionId)
        {
            return new CommandResult
            {
                Success = false,
                Message = $"Move order to \"{targetCity.Name}\" was cancelled.",
                MessageZhHant = $"移動命令已取消，目標「{targetCity.NameZhHant}」已不符合條件。",
                MessageEn = $"Move order to \"{targetCity.NameEn}\" was cancelled."
            };
        }

        var movableTroops = GetTransferAmount(pendingCommand.TroopsToSend, sourceCity.Troops);
        var movableGold = GetTransferAmount(pendingCommand.GoldToSend, sourceCity.Gold);
        var movableFood = GetTransferAmount(pendingCommand.FoodToSend, sourceCity.Food);
        var movedOfficerCount = TransferOfficers(world, sourceCity, targetCity, pendingCommand.OfficerIds);

        if (movableTroops <= 0 && movableGold <= 0 && movableFood <= 0 && movedOfficerCount == 0)
        {
            return new CommandResult
            {
                Success = false,
                Message = $"Move order to \"{targetCity.Name}\" had no effect.",
                MessageZhHant = $"移動命令未生效，沒有可轉移的兵力、資源或武將。",
                MessageEn = $"Move order to \"{targetCity.NameEn}\" had no effect."
            };
        }

        sourceCity.Troops -= movableTroops;
        sourceCity.Gold -= movableGold;
        sourceCity.Food -= movableFood;

        targetCity.Troops += movableTroops;
        targetCity.Gold += movableGold;
        targetCity.Food += movableFood;

        return new CommandResult
        {
            Success = true,
            Message = $"Move resolved. Troops+{movableTroops}, Gold+{movableGold}, Food+{movableFood}, Officers+{movedOfficerCount} moved to \"{targetCity.Name}\".",
            MessageZhHant = $"移動完成。兵力 +{movableTroops}、金 +{movableGold}、糧 +{movableFood}、武將 +{movedOfficerCount} 已轉移至「{targetCity.NameZhHant}」。",
            MessageEn = $"Move resolved. Troops+{movableTroops}, Gold+{movableGold}, Food+{movableFood}, Officers+{movedOfficerCount} moved to \"{targetCity.NameEn}\"."
        };
    }

    private CommandResult ResolveAttack(WorldState world, CityData sourceCity, PendingCommandData pendingCommand)
    {
        if (_combatResolver == null)
        {
            return new CommandResult { Success = false, Message = "Combat resolver not initialized." };
        }

        var targetCity = world.GetCity(pendingCommand.TargetCityId);
        if (targetCity == null)
        {
            return new CommandResult { Success = false, Message = "Attack target city not found at resolution." };
        }

        if (!IsConnected(sourceCity, targetCity.Id) || targetCity.OwnerFactionId == sourceCity.OwnerFactionId)
        {
            return new CommandResult
            {
                Success = false,
                Message = $"Attack order against \"{targetCity.Name}\" was cancelled.",
                MessageZhHant = $"攻擊命令已取消，目標「{targetCity.NameZhHant}」已不符合條件。",
                MessageEn = $"Attack order against \"{targetCity.NameEn}\" was cancelled."
            };
        }

        var attackingTroops = GetTransferAmount(pendingCommand.TroopsToSend, sourceCity.Troops);
        if (attackingTroops <= 0)
        {
            return new CommandResult { Success = false, Message = "No troops available when attack resolved." };
        }

        var defendingFactionId = targetCity.OwnerFactionId;
        var combat = _combatResolver.Resolve(world, sourceCity, targetCity, attackingTroops);

        var effectiveAttackerLoss = combat.AttackerLosses;
        if (effectiveAttackerLoss > attackingTroops)
        {
            effectiveAttackerLoss = attackingTroops;
        }

        sourceCity.Troops -= effectiveAttackerLoss;
        if (sourceCity.Troops < 0)
        {
            sourceCity.Troops = 0;
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
            return new CommandResult
            {
                Success = true,
                Message = $"Attack failed at \"{targetCity.Name}\".",
                MessageZhHant = $"Attack failed at \"{targetCity.NameZhHant}\".",
                MessageEn = $"Attack failed at \"{targetCity.NameEn}\"."
            };
        }

        targetCity.OwnerFactionId = sourceCity.OwnerFactionId;
        ResolveCapturedCityOfficers(world, targetCity, defendingFactionId);
        var garrison = attackingTroops - effectiveAttackerLoss;
        if (garrison < 100)
        {
            garrison = 100;
        }

        targetCity.Troops = garrison;
        sourceCity.Loyalty = ClampStat(sourceCity.Loyalty + 2);

        return new CommandResult
        {
            Success = true,
            Message = $"Attack success. Captured \"{targetCity.Name}\".",
            MessageZhHant = $"Attack success. Captured \"{targetCity.NameZhHant}\".",
            MessageEn = $"Attack success. Captured \"{targetCity.NameEn}\"."
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

    private static string GetOfficerDisplayName(OfficerData officer)
    {
        return string.IsNullOrWhiteSpace(officer.NameZhHant) ? officer.Name : officer.NameZhHant;
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

    private static bool HasUsedCoreAction(WorldState world, CityData city)
    {
        return city.LastCoreActionYear == world.Year && city.LastCoreActionMonth == world.Month;
    }

    private static void MarkCoreActionUsed(WorldState world, CityData city)
    {
        city.LastCoreActionYear = world.Year;
        city.LastCoreActionMonth = world.Month;
    }

    private static void UpsertPendingCommand(WorldState world, PendingCommandData pendingCommand)
    {
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
