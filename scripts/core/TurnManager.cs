using System.Collections.Generic;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class MonthlyEconomyResult
{
    // Keep both world totals and player-city breakdown so HUD can choose the right summary.
    public int AnnualGoldCollected { get; set; }
    public int AnnualFoodCollected { get; set; }
    public List<(int CityId, int Amount)> PlayerCityHorseBirths { get; } = new();
    public List<(int CityId, int Amount)> PlayerCityGoldIncome { get; } = new();
    public List<(int CityId, int Amount)> PlayerCityFoodIncome { get; } = new();
    public List<MonthlyDisasterEvent> PlayerCityDisasters { get; } = new();
}

public class MonthlyDisasterEvent
{
    public int CityId { get; set; }
    public int GoldLoss { get; set; }
    public int FoodLoss { get; set; }
    public int LoyaltyLoss { get; set; }
}

public class TurnManager
{
    private const int MonthlyUpkeepDivisor = 40;
    private const double BaseDisasterChance = 0.08;
    private const double BaseHorseBirthRate = 0.10;

    public WorldState? World { get; private set; }
    public int ActiveFactionId { get; private set; }

    public void Initialize(WorldState world)
    {
        World = world;
        ActiveFactionId = GetPlayerFactionId();
    }

    public int GetPlayerFactionId()
    {
        if (World == null)
        {
            return -1;
        }

        foreach (var faction in World.Factions)
        {
            if (faction.IsPlayer)
            {
                return faction.Id;
            }
        }

        return -1;
    }

    public List<CommandResult> ResolvePendingCommands(CommandResolver resolver)
    {
        var results = new List<CommandResult>();
        if (World == null)
        {
            return results;
        }

        results.AddRange(resolver.ResolveInternalAffairsSchedules());
        ResolvePendingCommandsOfType(resolver, CommandType.Develop, results);
        ResolvePendingCommandsOfType(resolver, CommandType.Recruit, results);
        ResolvePendingCommandsOfType(resolver, CommandType.Search, results);
        ResolvePendingCommandsOfType(resolver, CommandType.CivilRelief, results);
        ResolvePendingCommandsOfType(resolver, CommandType.Diplomacy, results);
        ResolvePendingCommandsOfType(resolver, CommandType.Move, results);
        ResolvePendingCommandsOfType(resolver, CommandType.Attack, results);
        World.PendingCommands.Clear();
        return results;
    }

    public MonthlyEconomyResult ApplyMonthlyEconomy()
    {
        var result = new MonthlyEconomyResult();
        if (World == null)
        {
            return result;
        }

        var playerFactionId = GetPlayerFactionId();

        foreach (var city in World.Cities)
        {
            var loyaltyFactor = 0.8f + city.Loyalty / 200.0f;
            var goldIncome = (int)((30 + city.Commercial * 2.0f) * loyaltyFactor);
            var foodIncome = (int)((40 + city.Farm * 3.0f) * loyaltyFactor);

            if (World.Month == 1)
            {
                var horseBirths = CalculateHorseBirths(city);
                if (horseBirths > 0)
                {
                    city.Horses += horseBirths;
                    if (city.OwnerFactionId == playerFactionId)
                    {
                        result.PlayerCityHorseBirths.Add((city.Id, horseBirths));
                    }
                }
            }

            // Seasonal income is paid when the game enters April/August, not after those months end.
            if (World.Month == 4)
            {
                var annualGold = goldIncome * 12;
                city.Gold += annualGold;
                result.AnnualGoldCollected += annualGold;
                if (city.OwnerFactionId == playerFactionId)
                {
                    result.PlayerCityGoldIncome.Add((city.Id, annualGold));
                }
            }

            if (World.Month == 8)
            {
                var annualFood = foodIncome * 12;
                city.Food += annualFood;
                result.AnnualFoodCollected += annualFood;
                if (city.OwnerFactionId == playerFactionId)
                {
                    result.PlayerCityFoodIncome.Add((city.Id, annualFood));
                }
            }

            var upkeep = city.Troops / MonthlyUpkeepDivisor;
            city.Food -= upkeep;

            if (city.Food < 0)
            {
                // Resolve shortage immediately here so later systems do not see negative food carry-over.
                var shortage = -city.Food;
                var deserters = shortage * 2;
                if (deserters > city.Troops)
                {
                    deserters = city.Troops;
                }

                if (city.Troops > 0 && deserters > 0)
                {
                    city.RemoveTroopAllocation(CreateDeserterAllocation(city, deserters));
                }
                city.Food = 0;
                city.Loyalty = city.Loyalty > 2 ? city.Loyalty - 2 : 0;
            }

            ApplyMonthlyDisaster(city, playerFactionId, result);
        }

        return result;
    }

    public void AdvanceMonth()
    {
        if (World == null)
        {
            return;
        }

        World.Month += 1;
        if (World.Month > 12)
        {
            World.Month = 1;
            World.Year += 1;
        }

        AdvanceDiplomacyRelations();
        FreeOfficerMovement.Advance(World);
    }

    private void AdvanceDiplomacyRelations()
    {
        if (World == null)
        {
            return;
        }

        foreach (var relation in World.DiplomacyRelations)
        {
            if (relation.Status == DiplomacyStatusType.Neutral || relation.RemainingMonths <= 0)
            {
                continue;
            }

            relation.RemainingMonths -= 1;
            if (relation.RemainingMonths > 0)
            {
                continue;
            }

            relation.RemainingMonths = 0;
            relation.Status = DiplomacyStatusType.Neutral;
        }
    }

    private void ResolvePendingCommandsOfType(
        CommandResolver resolver,
        CommandType commandType,
        List<CommandResult> results)
    {
        if (World == null)
        {
            return;
        }

        foreach (var pendingCommand in World.PendingCommands)
        {
            if (pendingCommand.Type != commandType)
            {
                continue;
            }

            results.Add(resolver.ResolvePendingCommand(pendingCommand));
        }
    }

    private static TroopAllocationData CreateDeserterAllocation(CityData city, int deserters)
    {
        var allocation = new TroopAllocationData();
        var remaining = deserters;
        foreach (var troopType in new[]
                 {
                     TroopType.Infantry,
                     TroopType.Spearman,
                     TroopType.Archer,
                     TroopType.Cavalry,
                     TroopType.Crossbow,
                     TroopType.Siege
                 })
        {
            if (remaining <= 0)
            {
                break;
            }

            var available = city.GetTroops(troopType);
            if (available <= 0)
            {
                continue;
            }

            var loss = System.Math.Min(available, remaining);
            switch (troopType)
            {
                case TroopType.Infantry:
                    allocation.Infantry = loss;
                    break;
                case TroopType.Spearman:
                    allocation.Spearman = loss;
                    break;
                case TroopType.Cavalry:
                    allocation.Cavalry = loss;
                    break;
                case TroopType.Archer:
                    allocation.Archer = loss;
                    break;
                case TroopType.Crossbow:
                    allocation.Crossbow = loss;
                    break;
                case TroopType.Siege:
                    allocation.Siege = loss;
                    break;
            }

            remaining -= loss;
        }

        return allocation;
    }

    private void ApplyMonthlyDisaster(CityData city, int playerFactionId, MonthlyEconomyResult result)
    {
        if (World == null)
        {
            return;
        }

        var disasterPrevention = System.Math.Max(0, city.DisasterPrevention);
        var chanceMultiplier = System.Math.Max(0.2, 1.0 - disasterPrevention / 150.0);
        var random = new System.Random(System.HashCode.Combine(World.RandomSeed, World.Year, World.Month, city.Id, 991));
        if (random.NextDouble() >= BaseDisasterChance * chanceMultiplier)
        {
            return;
        }

        var severityMultiplier = System.Math.Max(0.25, 1.0 - disasterPrevention / 120.0);
        var goldLoss = System.Math.Min(city.Gold, (int)System.Math.Round((20 + random.Next(0, 61)) * severityMultiplier));
        var foodLoss = System.Math.Min(city.Food, (int)System.Math.Round((40 + random.Next(0, 121)) * severityMultiplier));
        var loyaltyLoss = System.Math.Min(city.Loyalty, System.Math.Max(1, (int)System.Math.Round((1 + random.Next(0, 4)) * severityMultiplier)));

        if (goldLoss <= 0 && foodLoss <= 0 && loyaltyLoss <= 0)
        {
            return;
        }

        city.Gold -= goldLoss;
        city.Food -= foodLoss;
        city.Loyalty = System.Math.Max(0, city.Loyalty - loyaltyLoss);

        if (city.OwnerFactionId == playerFactionId)
        {
            result.PlayerCityDisasters.Add(new MonthlyDisasterEvent
            {
                CityId = city.Id,
                GoldLoss = goldLoss,
                FoodLoss = foodLoss,
                LoyaltyLoss = loyaltyLoss
            });
        }
    }

    private static int CalculateHorseBirths(CityData city)
    {
        if (city.Horses <= 0)
        {
            return 0;
        }

        var birthRate = GetHorseBirthRate(city);
        var births = (int)System.Math.Floor(city.Horses * birthRate);
        return births <= 0 ? 1 : births;
    }

    private static double GetHorseBirthRate(CityData city)
    {
        var facilityBonus = 0.0;
        return BaseHorseBirthRate + facilityBonus;
    }
}
