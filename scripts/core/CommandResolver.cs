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
            CommandType.Develop => ExecuteDevelop(world, sourceCity),
            CommandType.Recruit => ExecuteRecruit(world, sourceCity),
            CommandType.Move => ExecuteMove(world, sourceCity, request),
            CommandType.Search => ExecuteSearch(world, sourceCity),
            CommandType.Attack => ExecuteAttack(world, sourceCity, request),
            CommandType.Pass => new CommandResult { Success = true, Message = "Pass" },
            _ => new CommandResult { Success = false, Message = "Unknown command." }
        };
    }

    private CommandResult ExecuteDevelop(WorldState world, CityData city)
    {
        if (city.Gold < DevelopGoldCost)
        {
            return new CommandResult { Success = false, Message = "Not enough gold for Develop." };
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

    private CommandResult ExecuteRecruit(WorldState world, CityData city)
    {
        if (city.Gold < RecruitGoldCost || city.Food < RecruitFoodCost)
        {
            return new CommandResult { Success = false, Message = "Not enough resources for Recruit." };
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

    private CommandResult ExecuteMove(WorldState world, CityData sourceCity, CommandRequest request)
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

        var requestedTroops = request.TroopsToSend > 0 ? request.TroopsToSend : sourceCity.Troops / 2;
        var movableTroops = requestedTroops;
        if (movableTroops > sourceCity.Troops)
        {
            movableTroops = sourceCity.Troops;
        }

        if (movableTroops <= 0)
        {
            return new CommandResult { Success = false, Message = "No troops available to move." };
        }

        sourceCity.Troops -= movableTroops;
        targetCity.Troops += movableTroops;

        return new CommandResult
        {
            Success = true,
            Message = $"Move success. {movableTroops} troops moved to \"{targetCity.Name}\"."
        };
    }

    private CommandResult ExecuteSearch(WorldState world, CityData city)
    {
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

        city.LastSearchYear = world.Year;
        city.LastSearchMonth = world.Month;

        var intelligence = GetAverageStat(world, city, officer => officer.Intelligence);
        var charm = GetAverageStat(world, city, officer => officer.Charm);
        var chance = 0.25f + intelligence / 250.0f + charm / 300.0f;

        if (_random.NextDouble() > chance)
        {
            return new CommandResult { Success = true, Message = "Search found nothing." };
        }

        if (_random.NextDouble() < 0.5)
        {
            var foundGold = 40 + _random.Next(0, 81);
            city.Gold += foundGold;
            return new CommandResult { Success = true, Message = $"Search success. Gold+{foundGold}." };
        }

        var foundFood = 60 + _random.Next(0, 121);
        city.Food += foundFood;
        return new CommandResult { Success = true, Message = $"Search success. Food+{foundFood}." };
    }

    private CommandResult ExecuteAttack(WorldState world, CityData sourceCity, CommandRequest request)
    {
        if (_combatResolver == null)
        {
            return new CommandResult { Success = false, Message = "Combat resolver not initialized." };
        }

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

        var attackingTroops = request.TroopsToSend > 0 ? request.TroopsToSend : sourceCity.Troops / 2;
        if (attackingTroops > sourceCity.Troops)
        {
            attackingTroops = sourceCity.Troops;
        }

        if (attackingTroops <= 0)
        {
            return new CommandResult { Success = false, Message = "No troops available for attack." };
        }

        var combat = _combatResolver.Resolve(sourceCity, targetCity, attackingTroops);

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
            var officer = world.Officers.Find(o => o.Id == officerId);
            if (officer == null)
            {
                continue;
            }

            total += selector(officer);
            count += 1;
        }

        return count == 0 ? 50 : total / count;
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
