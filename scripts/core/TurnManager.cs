using System;
using System.Collections.Generic;
using System.Linq;
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
    public List<MonthlyCityEvent> AllCityEvents { get; } = new();
    public List<MonthlyCityEvent> PlayerCityEvents { get; } = new();
}

public enum MonthlyCityEventType
{
    Flooding,
    Drought,
    Earthquake,
    InsectDisaster,
    Plague,
    Rebellion,
    Bandit,
    Snow,
    Typhoon,
    BumperHarvest,
    Fire
}

public class MonthlyCityEvent
{
    public int CityId { get; set; }
    public MonthlyCityEventType EventType { get; set; }
    public int GoldDelta { get; set; }
    public int FoodDelta { get; set; }
    public int LoyaltyDelta { get; set; }
    public int FarmDelta { get; set; }
    public int DefenseDelta { get; set; }
    public int PopulationDelta { get; set; }
    public int TroopDelta { get; set; }
}

public class OfficerEscapeEvent
{
    public int OfficerId { get; set; }
    public int CaptorFactionId { get; set; }
    public int JailedCityId { get; set; }
}

public class TurnManager
{
    private const int MonthlyUpkeepDivisor = 40;
    private const double BaseHorseBirthRate = 0.10;
    private const double BaseDisasterChance = 0.08;
    private const double BaseBumperHarvestChance = 0.05;
    private const int NaturalDeathStartAge = 50;
    private const int RebellionLoyaltyThreshold = 55;
    private const int BanditLoyaltyThreshold = 65;
    private const int BanditDefenseThreshold = 45;
    private readonly List<OfficerEscapeEvent> _latestOfficerEscapeEvents = new();

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
        ResolvePendingCommandsOfType(resolver, CommandType.Spy, results);
        ResolvePendingCommandsOfType(resolver, CommandType.Move, results);
        ResolvePendingCommandsOfType(resolver, CommandType.Attack, results);
        World.PendingCommands.Clear();
        return results;
    }

    public List<CommandResult> ResolvePendingCommandsExceptAttack(CommandResolver resolver)
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
        ResolvePendingCommandsOfType(resolver, CommandType.Spy, results);
        ResolvePendingCommandsOfType(resolver, CommandType.Move, results);
        World.PendingCommands.RemoveAll(command => command.Type != CommandType.Attack);
        return results;
    }

    public List<PendingCommandData> GetPendingCommandsOfType(CommandType commandType)
    {
        if (World == null)
        {
            return new List<PendingCommandData>();
        }

        return World.PendingCommands
            .Where(command => command.Type == commandType)
            .ToList();
    }

    public List<PendingCommandData> GetPendingCommandsExceptAttackInResolutionOrder()
    {
        if (World == null)
        {
            return new List<PendingCommandData>();
        }

        var orderedTypes = new[]
        {
            CommandType.Develop,
            CommandType.Recruit,
            CommandType.Search,
            CommandType.CivilRelief,
            CommandType.Diplomacy,
            CommandType.Spy,
            CommandType.Move
        };

        var results = new List<PendingCommandData>();
        foreach (var commandType in orderedTypes)
        {
            results.AddRange(World.PendingCommands.Where(command => command.Type == commandType));
        }

        return results;
    }

    public void RemovePendingCommandsOfType(CommandType commandType)
    {
        if (World == null)
        {
            return;
        }

        World.PendingCommands.RemoveAll(command => command.Type == commandType);
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

            ApplyMonthlyCityEvent(city, playerFactionId, result);
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
        AdvanceCityIntel();
        AdvanceOfficerNaturalDeaths();
        AdvanceCapturedOfficerEscapes();
        FreeOfficerMovement.Advance(World);
    }

    public List<OfficerEscapeEvent> ConsumeOfficerEscapeEvents()
    {
        var events = _latestOfficerEscapeEvents.ToList();
        _latestOfficerEscapeEvents.Clear();
        return events;
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

    private void AdvanceCityIntel()
    {
        if (World == null)
        {
            return;
        }

        foreach (var record in World.CityIntelRecords)
        {
            if (record.RemainingMonths <= 0)
            {
                continue;
            }

            record.RemainingMonths -= 1;
        }

        World.CityIntelRecords.RemoveAll(record => record.RemainingMonths <= 0);
    }

    private void AdvanceOfficerNaturalDeaths()
    {
        if (World == null)
        {
            return;
        }

        foreach (var officer in World.Officers.ToList())
        {
            if (!ShouldApplyNaturalDeathChance(World, officer))
            {
                continue;
            }

            var deathChance = GetNaturalDeathChance(GetOfficerAge(World, officer));
            if (deathChance <= 0.0)
            {
                continue;
            }

            var random = new Random(HashCode.Combine(World.RandomSeed, World.Year, World.Month, officer.Id, 4703));
            if (random.NextDouble() >= deathChance)
            {
                continue;
            }

            EliminateOfficer(World, officer);
        }
    }

    private void AdvanceCapturedOfficerEscapes()
    {
        if (World == null)
        {
            return;
        }

        foreach (var officer in World.Officers.ToList())
        {
            if (officer.CaptiveFactionId <= 0)
            {
                continue;
            }

            var jailCity = officer.JailedCityId > 0 ? World.GetCity(officer.JailedCityId) : null;
            if (jailCity == null || jailCity.OwnerFactionId != officer.CaptiveFactionId)
            {
                ReleaseCapturedOfficerAsFreeOfficer(officer);
                continue;
            }

            var escapeChance = GetCapturedOfficerEscapeChance(jailCity, officer);
            var random = new Random(HashCode.Combine(World.RandomSeed, World.Year, World.Month, officer.Id, 8811));
            if (random.NextDouble() >= escapeChance)
            {
                continue;
            }

            _latestOfficerEscapeEvents.Add(new OfficerEscapeEvent
            {
                OfficerId = officer.Id,
                CaptorFactionId = officer.CaptiveFactionId,
                JailedCityId = jailCity.Id
            });
            ReleaseCapturedOfficerAsFreeOfficer(officer);
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

    private static void ApplyCityTroopEventLoss(CityData city, int troopDelta)
    {
        if (troopDelta >= 0)
        {
            return;
        }

        var losses = -troopDelta;
        if (losses <= 0 || city.Troops <= 0)
        {
            return;
        }

        if (losses > city.Troops)
        {
            losses = city.Troops;
        }

        city.RemoveTroopAllocation(CreateDeserterAllocation(city, losses));
    }

    private static int CalculatePopulationLoss(CityData city, int baseAmount, int randomRange, System.Random random, double severityMultiplier)
    {
        return System.Math.Min(city.Population, (int)System.Math.Round((baseAmount + random.Next(0, randomRange + 1)) * severityMultiplier));
    }

    private static int CalculateTroopLoss(CityData city, int baseAmount, int randomRange, System.Random random, double severityMultiplier)
    {
        return System.Math.Min(city.Troops, (int)System.Math.Round((baseAmount + random.Next(0, randomRange + 1)) * severityMultiplier));
    }

    private void ApplyMonthlyCityEvent(CityData city, int playerFactionId, MonthlyEconomyResult result)
    {
        if (World == null)
        {
            return;
        }

        var month = World.Month;
        var disasterPrevention = System.Math.Max(0, city.DisasterPrevention);
        var chanceMultiplier = System.Math.Max(0.2, 1.0 - disasterPrevention / 150.0);
        var random = new System.Random(System.HashCode.Combine(World.RandomSeed, World.Year, World.Month, city.Id, 991));
        var harvestChance = IsBumperHarvestSeason(month)
            ? BaseBumperHarvestChance * System.Math.Min(1.6, 0.7 + city.Farm / 120.0 + disasterPrevention / 400.0)
            : 0.0;
        var disasterRoll = random.NextDouble();
        if (disasterRoll >= BaseDisasterChance * chanceMultiplier && random.NextDouble() >= harvestChance)
        {
            return;
        }

        var severityMultiplier = System.Math.Max(0.12, 1.0 - disasterPrevention / 90.0);
        MonthlyCityEvent? cityEvent;
        if (disasterRoll < BaseDisasterChance * chanceMultiplier)
        {
            cityEvent = CreateDisasterEvent(city, month, random, severityMultiplier);
        }
        else
        {
            cityEvent = CreateBumperHarvestEvent(city, random);
        }

        if (cityEvent == null)
        {
            return;
        }

        city.Gold = ClampCityStat(city.Gold + cityEvent.GoldDelta);
        city.Food = ClampCityStat(city.Food + cityEvent.FoodDelta);
        city.Loyalty = ClampCityStat(city.Loyalty + cityEvent.LoyaltyDelta);
        city.Farm = ClampCityStat(city.Farm + cityEvent.FarmDelta);
        city.Defense = ClampCityStat(city.Defense + cityEvent.DefenseDelta);
        city.Population = ClampCityStat(city.Population + cityEvent.PopulationDelta);
        ApplyCityTroopEventLoss(city, cityEvent.TroopDelta);

        result.AllCityEvents.Add(cityEvent);

        if (city.OwnerFactionId == playerFactionId)
        {
            result.PlayerCityEvents.Add(cityEvent);
        }
    }

    private static MonthlyCityEvent? CreateDisasterEvent(CityData city, int month, System.Random random, double severityMultiplier)
    {
        var availableEvents = GetAvailableDisasterEvents(city, month);
        if (availableEvents.Count == 0)
        {
            return null;
        }

        var eventType = availableEvents[random.Next(availableEvents.Count)];
        return eventType switch
        {
            MonthlyCityEventType.Flooding => CreateFloodingEvent(city, random, severityMultiplier),
            MonthlyCityEventType.Drought => CreateDroughtEvent(city, random, severityMultiplier),
            MonthlyCityEventType.Earthquake => CreateEarthquakeEvent(city, random, severityMultiplier),
            MonthlyCityEventType.InsectDisaster => CreateInsectDisasterEvent(city, random, severityMultiplier),
            MonthlyCityEventType.Plague => CreatePlagueEvent(city, random, severityMultiplier),
            MonthlyCityEventType.Rebellion => CreateRebellionEvent(city, random, severityMultiplier),
            MonthlyCityEventType.Bandit => CreateBanditEvent(city, random, severityMultiplier),
            MonthlyCityEventType.Snow => CreateSnowEvent(city, random, severityMultiplier),
            MonthlyCityEventType.Typhoon => CreateTyphoonEvent(city, random, severityMultiplier),
            MonthlyCityEventType.Fire => CreateFireEvent(city, random, severityMultiplier),
            _ => null
        };
    }

    private static List<MonthlyCityEventType> GetAvailableDisasterEvents(CityData city, int month)
    {
        var events = new List<MonthlyCityEventType> { MonthlyCityEventType.Earthquake };
        if (IsRainySeasonFloodMonth(month))
        {
            events.Add(MonthlyCityEventType.Flooding);
        }

        if (IsDroughtSeason(month))
        {
            events.Add(MonthlyCityEventType.Drought);
        }

        if (IsPlagueSeason(month))
        {
            events.Add(MonthlyCityEventType.Plague);
        }

        if (IsInsectSeason(month))
        {
            events.Add(MonthlyCityEventType.InsectDisaster);
        }

        if (IsSnowSeason(month))
        {
            events.Add(MonthlyCityEventType.Snow);
        }

        if (IsTyphoonSeason(month))
        {
            events.Add(MonthlyCityEventType.Typhoon);
        }

        if (IsDrySeasonFireMonth(month))
        {
            events.Add(MonthlyCityEventType.Fire);
        }

        if (city.Loyalty <= RebellionLoyaltyThreshold)
        {
            events.Add(MonthlyCityEventType.Rebellion);
        }

        if (city.Loyalty <= BanditLoyaltyThreshold && city.Defense <= BanditDefenseThreshold)
        {
            events.Add(MonthlyCityEventType.Bandit);
        }

        return events;
    }

    private static bool IsRainySeasonFloodMonth(int month)
    {
        return month is 6 or 7 or 8;
    }

    private static bool IsInsectSeason(int month)
    {
        return month is 5 or 6 or 7;
    }

    private static bool IsDroughtSeason(int month)
    {
        return month is 5 or 6 or 7 or 8;
    }

    private static bool IsDrySeasonFireMonth(int month)
    {
        return month is 11 or 12 or 1;
    }

    private static bool IsPlagueSeason(int month)
    {
        return month is 11 or 12 or 1 or 2 or 3;
    }

    private static bool IsSnowSeason(int month)
    {
        return month is 12 or 1 or 2;
    }

    private static bool IsTyphoonSeason(int month)
    {
        return month is 9 or 10 or 11;
    }

    private static bool IsBumperHarvestSeason(int month)
    {
        return month is 8 or 9 or 10;
    }

    private static MonthlyCityEvent? CreateFloodingEvent(CityData city, System.Random random, double severityMultiplier)
    {
        var foodLoss = System.Math.Min(city.Food, (int)System.Math.Round((120 + random.Next(0, 181)) * severityMultiplier));
        var farmLoss = System.Math.Min(city.Farm, System.Math.Max(1, (int)System.Math.Round((2 + random.Next(0, 5)) * severityMultiplier)));
        var loyaltyLoss = System.Math.Min(city.Loyalty, System.Math.Max(1, (int)System.Math.Round((1 + random.Next(0, 3)) * severityMultiplier)));
        var populationLoss = CalculatePopulationLoss(city, 600, 900, random, severityMultiplier);
        var troopLoss = CalculateTroopLoss(city, 40, 80, random, severityMultiplier);
        if (foodLoss <= 0 && farmLoss <= 0 && loyaltyLoss <= 0 && populationLoss <= 0 && troopLoss <= 0)
        {
            return null;
        }

        return new MonthlyCityEvent
        {
            CityId = city.Id,
            EventType = MonthlyCityEventType.Flooding,
            FoodDelta = -foodLoss,
            LoyaltyDelta = -loyaltyLoss,
            FarmDelta = -farmLoss,
            PopulationDelta = -populationLoss,
            TroopDelta = -troopLoss
        };
    }

    private static MonthlyCityEvent? CreateDroughtEvent(CityData city, System.Random random, double severityMultiplier)
    {
        var foodLoss = System.Math.Min(city.Food, (int)System.Math.Round((110 + random.Next(0, 171)) * severityMultiplier));
        var farmLoss = System.Math.Min(city.Farm, System.Math.Max(1, (int)System.Math.Round((2 + random.Next(0, 4)) * severityMultiplier)));
        var loyaltyLoss = System.Math.Min(city.Loyalty, System.Math.Max(1, (int)System.Math.Round((1 + random.Next(0, 3)) * severityMultiplier)));
        var populationLoss = CalculatePopulationLoss(city, 500, 800, random, severityMultiplier);
        var troopLoss = CalculateTroopLoss(city, 35, 70, random, severityMultiplier);
        if (foodLoss <= 0 && farmLoss <= 0 && loyaltyLoss <= 0 && populationLoss <= 0 && troopLoss <= 0)
        {
            return null;
        }

        return new MonthlyCityEvent
        {
            CityId = city.Id,
            EventType = MonthlyCityEventType.Drought,
            FoodDelta = -foodLoss,
            LoyaltyDelta = -loyaltyLoss,
            FarmDelta = -farmLoss,
            PopulationDelta = -populationLoss,
            TroopDelta = -troopLoss
        };
    }

    private static MonthlyCityEvent? CreatePlagueEvent(CityData city, System.Random random, double severityMultiplier)
    {
        var foodLoss = System.Math.Min(city.Food, (int)System.Math.Round((70 + random.Next(0, 121)) * severityMultiplier));
        var loyaltyLoss = System.Math.Min(city.Loyalty, System.Math.Max(1, (int)System.Math.Round((2 + random.Next(0, 4)) * severityMultiplier)));
        var populationLoss = CalculatePopulationLoss(city, 700, 1100, random, severityMultiplier);
        var troopLoss = CalculateTroopLoss(city, 30, 60, random, severityMultiplier);
        if (foodLoss <= 0 && loyaltyLoss <= 0 && populationLoss <= 0 && troopLoss <= 0)
        {
            return null;
        }

        return new MonthlyCityEvent
        {
            CityId = city.Id,
            EventType = MonthlyCityEventType.Plague,
            FoodDelta = -foodLoss,
            LoyaltyDelta = -loyaltyLoss,
            PopulationDelta = -populationLoss,
            TroopDelta = -troopLoss
        };
    }

    private static MonthlyCityEvent? CreateEarthquakeEvent(CityData city, System.Random random, double severityMultiplier)
    {
        var goldLoss = System.Math.Min(city.Gold, (int)System.Math.Round((80 + random.Next(0, 121)) * severityMultiplier));
        var defenseLoss = System.Math.Min(city.Defense, System.Math.Max(1, (int)System.Math.Round((3 + random.Next(0, 6)) * severityMultiplier)));
        var loyaltyLoss = System.Math.Min(city.Loyalty, System.Math.Max(1, (int)System.Math.Round((1 + random.Next(0, 3)) * severityMultiplier)));
        var populationLoss = CalculatePopulationLoss(city, 900, 1500, random, severityMultiplier);
        var troopLoss = CalculateTroopLoss(city, 50, 110, random, severityMultiplier);
        if (goldLoss <= 0 && defenseLoss <= 0 && loyaltyLoss <= 0 && populationLoss <= 0 && troopLoss <= 0)
        {
            return null;
        }

        return new MonthlyCityEvent
        {
            CityId = city.Id,
            EventType = MonthlyCityEventType.Earthquake,
            GoldDelta = -goldLoss,
            LoyaltyDelta = -loyaltyLoss,
            DefenseDelta = -defenseLoss,
            PopulationDelta = -populationLoss,
            TroopDelta = -troopLoss
        };
    }

    private static MonthlyCityEvent? CreateInsectDisasterEvent(CityData city, System.Random random, double severityMultiplier)
    {
        var foodLoss = System.Math.Min(city.Food, (int)System.Math.Round((100 + random.Next(0, 161)) * severityMultiplier));
        var farmLoss = System.Math.Min(city.Farm, System.Math.Max(1, (int)System.Math.Round((1 + random.Next(0, 4)) * severityMultiplier)));
        var loyaltyLoss = System.Math.Min(city.Loyalty, System.Math.Max(1, (int)System.Math.Round((1 + random.Next(0, 2)) * severityMultiplier)));
        if (foodLoss <= 0 && farmLoss <= 0 && loyaltyLoss <= 0)
        {
            return null;
        }

        return new MonthlyCityEvent
        {
            CityId = city.Id,
            EventType = MonthlyCityEventType.InsectDisaster,
            FoodDelta = -foodLoss,
            LoyaltyDelta = -loyaltyLoss,
            FarmDelta = -farmLoss
        };
    }

    private static MonthlyCityEvent? CreateFireEvent(CityData city, System.Random random, double severityMultiplier)
    {
        var goldLoss = System.Math.Min(city.Gold, (int)System.Math.Round((60 + random.Next(0, 101)) * severityMultiplier));
        var foodLoss = System.Math.Min(city.Food, (int)System.Math.Round((50 + random.Next(0, 91)) * severityMultiplier));
        var defenseLoss = System.Math.Min(city.Defense, System.Math.Max(1, (int)System.Math.Round((1 + random.Next(0, 4)) * severityMultiplier)));
        var loyaltyLoss = System.Math.Min(city.Loyalty, System.Math.Max(1, (int)System.Math.Round((1 + random.Next(0, 2)) * severityMultiplier)));
        var populationLoss = CalculatePopulationLoss(city, 450, 850, random, severityMultiplier);
        var troopLoss = CalculateTroopLoss(city, 25, 55, random, severityMultiplier);
        if (goldLoss <= 0 && foodLoss <= 0 && defenseLoss <= 0 && loyaltyLoss <= 0 && populationLoss <= 0 && troopLoss <= 0)
        {
            return null;
        }

        return new MonthlyCityEvent
        {
            CityId = city.Id,
            EventType = MonthlyCityEventType.Fire,
            GoldDelta = -goldLoss,
            FoodDelta = -foodLoss,
            LoyaltyDelta = -loyaltyLoss,
            DefenseDelta = -defenseLoss,
            PopulationDelta = -populationLoss,
            TroopDelta = -troopLoss
        };
    }

    private static MonthlyCityEvent? CreateRebellionEvent(CityData city, System.Random random, double severityMultiplier)
    {
        var goldLoss = System.Math.Min(city.Gold, (int)System.Math.Round((90 + random.Next(0, 141)) * severityMultiplier));
        var foodLoss = System.Math.Min(city.Food, (int)System.Math.Round((60 + random.Next(0, 101)) * severityMultiplier));
        var defenseLoss = System.Math.Min(city.Defense, System.Math.Max(1, (int)System.Math.Round((2 + random.Next(0, 4)) * severityMultiplier)));
        var loyaltyLoss = System.Math.Min(city.Loyalty, System.Math.Max(2, (int)System.Math.Round((3 + random.Next(0, 4)) * severityMultiplier)));
        if (goldLoss <= 0 && foodLoss <= 0 && defenseLoss <= 0 && loyaltyLoss <= 0)
        {
            return null;
        }

        return new MonthlyCityEvent
        {
            CityId = city.Id,
            EventType = MonthlyCityEventType.Rebellion,
            GoldDelta = -goldLoss,
            FoodDelta = -foodLoss,
            LoyaltyDelta = -loyaltyLoss,
            DefenseDelta = -defenseLoss
        };
    }

    private static MonthlyCityEvent? CreateBanditEvent(CityData city, System.Random random, double severityMultiplier)
    {
        var goldLoss = System.Math.Min(city.Gold, (int)System.Math.Round((70 + random.Next(0, 111)) * severityMultiplier));
        var foodLoss = System.Math.Min(city.Food, (int)System.Math.Round((50 + random.Next(0, 81)) * severityMultiplier));
        var loyaltyLoss = System.Math.Min(city.Loyalty, System.Math.Max(1, (int)System.Math.Round((2 + random.Next(0, 3)) * severityMultiplier)));
        if (goldLoss <= 0 && foodLoss <= 0 && loyaltyLoss <= 0)
        {
            return null;
        }

        return new MonthlyCityEvent
        {
            CityId = city.Id,
            EventType = MonthlyCityEventType.Bandit,
            GoldDelta = -goldLoss,
            FoodDelta = -foodLoss,
            LoyaltyDelta = -loyaltyLoss
        };
    }

    private static MonthlyCityEvent? CreateSnowEvent(CityData city, System.Random random, double severityMultiplier)
    {
        var foodLoss = System.Math.Min(city.Food, (int)System.Math.Round((80 + random.Next(0, 131)) * severityMultiplier));
        var loyaltyLoss = System.Math.Min(city.Loyalty, System.Math.Max(1, (int)System.Math.Round((1 + random.Next(0, 3)) * severityMultiplier)));
        var defenseLoss = System.Math.Min(city.Defense, System.Math.Max(1, (int)System.Math.Round((1 + random.Next(0, 2)) * severityMultiplier)));
        var populationLoss = CalculatePopulationLoss(city, 400, 700, random, severityMultiplier);
        var troopLoss = CalculateTroopLoss(city, 20, 45, random, severityMultiplier);
        if (foodLoss <= 0 && loyaltyLoss <= 0 && defenseLoss <= 0 && populationLoss <= 0 && troopLoss <= 0)
        {
            return null;
        }

        return new MonthlyCityEvent
        {
            CityId = city.Id,
            EventType = MonthlyCityEventType.Snow,
            FoodDelta = -foodLoss,
            LoyaltyDelta = -loyaltyLoss,
            DefenseDelta = -defenseLoss,
            PopulationDelta = -populationLoss,
            TroopDelta = -troopLoss
        };
    }

    private static MonthlyCityEvent? CreateTyphoonEvent(CityData city, System.Random random, double severityMultiplier)
    {
        var foodLoss = System.Math.Min(city.Food, (int)System.Math.Round((90 + random.Next(0, 151)) * severityMultiplier));
        var farmLoss = System.Math.Min(city.Farm, System.Math.Max(1, (int)System.Math.Round((2 + random.Next(0, 5)) * severityMultiplier)));
        var defenseLoss = System.Math.Min(city.Defense, System.Math.Max(1, (int)System.Math.Round((2 + random.Next(0, 4)) * severityMultiplier)));
        var loyaltyLoss = System.Math.Min(city.Loyalty, System.Math.Max(1, (int)System.Math.Round((1 + random.Next(0, 3)) * severityMultiplier)));
        var populationLoss = CalculatePopulationLoss(city, 650, 1200, random, severityMultiplier);
        var troopLoss = CalculateTroopLoss(city, 35, 85, random, severityMultiplier);
        if (foodLoss <= 0 && farmLoss <= 0 && defenseLoss <= 0 && loyaltyLoss <= 0 && populationLoss <= 0 && troopLoss <= 0)
        {
            return null;
        }

        return new MonthlyCityEvent
        {
            CityId = city.Id,
            EventType = MonthlyCityEventType.Typhoon,
            FoodDelta = -foodLoss,
            LoyaltyDelta = -loyaltyLoss,
            FarmDelta = -farmLoss,
            DefenseDelta = -defenseLoss,
            PopulationDelta = -populationLoss,
            TroopDelta = -troopLoss
        };
    }

    private static MonthlyCityEvent CreateBumperHarvestEvent(CityData city, System.Random random)
    {
        var foodGain = 120 + random.Next(0, 181) + city.Farm;
        var loyaltyGain = 1 + random.Next(0, 3);
        return new MonthlyCityEvent
        {
            CityId = city.Id,
            EventType = MonthlyCityEventType.BumperHarvest,
            FoodDelta = foodGain,
            LoyaltyDelta = loyaltyGain
        };
    }

    private static int ClampCityStat(int value)
    {
        return value < 0 ? 0 : value;
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
        var facilityBonus = city.HorsePastureLevel * 0.05;
        return BaseHorseBirthRate + facilityBonus;
    }

    private static bool ShouldApplyNaturalDeathChance(WorldState world, OfficerData officer)
    {
        if (officer.DeathYear > 0 || officer.BirthYear <= 0)
        {
            return false;
        }

        return GetOfficerAge(world, officer) >= NaturalDeathStartAge;
    }

    private static int GetOfficerAge(WorldState world, OfficerData officer)
    {
        return world.Year - officer.BirthYear;
    }

    private static double GetNaturalDeathChance(int age)
    {
        return age switch
        {
            < 50 => 0.0,
            <= 59 => 0.0015,
            <= 69 => 0.0050,
            <= 79 => 0.0150,
            <= 89 => 0.0400,
            _ => 0.0800
        };
    }

    private static double GetCapturedOfficerEscapeChance(CityData jailCity, OfficerData officer)
    {
        var baseChance = 0.015;
        var lowDefenseBonus = Math.Max(0, 70 - jailCity.Defense) / 900.0;
        var lowLoyaltyBonus = Math.Max(0, 70 - jailCity.Loyalty) / 700.0;
        var officerAbilityBonus = Math.Max(0, officer.Intelligence + officer.Combat + officer.Charm - 180) / 2500.0;
        var ambitionBonus = Math.Max(0, officer.Ambition - 50) / 1200.0;
        var total = baseChance + lowDefenseBonus + lowLoyaltyBonus + officerAbilityBonus + ambitionBonus;
        return Math.Clamp(total, 0.01, 0.35);
    }

    private static void ReleaseCapturedOfficerAsFreeOfficer(OfficerData officer)
    {
        officer.CaptiveFactionId = 0;
        officer.JailedCityId = 0;
        officer.CityId = 0;
        officer.FreeOfficerStayMonths = 2;
    }

    private static void EliminateOfficer(WorldState world, OfficerData officer)
    {
        var city = officer.CityId > 0 ? world.GetCity(officer.CityId) : null;
        var faction = world.Factions.FirstOrDefault(item =>
            item.RulerOfficerId == officer.Id ||
            item.OfficerIds.Contains(officer.Id));
        var factionId = faction?.Id ?? 0;
        var wasRuler = faction?.RulerOfficerId == officer.Id;

        city?.OfficerIds.Remove(officer.Id);
        faction?.OfficerIds.Remove(officer.Id);

        foreach (var item in world.Items.Where(item => item.EquippedOfficerId == officer.Id))
        {
            item.EquippedOfficerId = 0;
            if (city != null && city.OwnerFactionId > 0)
            {
                item.OwnerFactionId = city.OwnerFactionId;
                item.OwnerCityId = 0;
            }
            else
            {
                item.OwnerFactionId = 0;
                item.OwnerCityId = 0;
            }
        }

        world.InternalAffairsSchedules.RemoveAll(schedule => schedule.OfficerId == officer.Id);
        world.PendingCommands.RemoveAll(command => command.OfficerIds.Contains(officer.Id));

        officer.CityId = 0;
        officer.FreeOfficerStayMonths = 0;
        officer.DeathYear = world.Year;
        officer.Appointments.Clear();

        foreach (var otherFaction in world.Factions)
        {
            if (otherFaction.ChancellorOfficerId == officer.Id)
            {
                otherFaction.ChancellorOfficerId = 0;
            }

            if (otherFaction.ChiefStrategistOfficerId == officer.Id)
            {
                otherFaction.ChiefStrategistOfficerId = 0;
            }
        }

        if (faction == null)
        {
            return;
        }

        if (wasRuler)
        {
            faction.RulerOfficerId = 0;
            ResolveRulerDeath(world, factionId);
        }
    }

    private static void ResolveRulerDeath(WorldState world, int factionId)
    {
        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return;
        }

        var candidateIds = faction.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null && IsOfficerAlive(world, officer))
            .OrderByDescending(officer => officer!.Leadership + officer.Intelligence + officer.Politics + officer.Charm)
            .ThenByDescending(officer => officer!.Loyalty)
            .Select(officer => officer!.Id)
            .ToList();

        if (candidateIds.Count == 0)
        {
            CollapseFaction(world, factionId);
            return;
        }

        var successor = world.GetOfficer(candidateIds[0]);
        if (successor == null)
        {
            CollapseFaction(world, factionId);
            return;
        }

        ApplyFactionSuccessor(world, faction, successor);
    }

    private static void ApplyFactionSuccessor(WorldState world, FactionData faction, OfficerData successor)
    {
        faction.RulerOfficerId = successor.Id;
        if (faction.ChancellorOfficerId == successor.Id)
        {
            faction.ChancellorOfficerId = 0;
        }

        if (faction.ChiefStrategistOfficerId == successor.Id)
        {
            faction.ChiefStrategistOfficerId = 0;
        }

        OfficerAppointmentRules.AddAppointment(successor, OfficerAppointmentRules.Lord);
        successor.Belongs = faction.Id.ToString();

        if (!faction.OfficerIds.Contains(successor.Id))
        {
            faction.OfficerIds.Add(successor.Id);
        }

        foreach (var officerId in faction.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            officer.Belongs = faction.Id.ToString();
            if (officer.Id != successor.Id)
            {
                OfficerAppointmentRules.RemoveAppointment(officer, OfficerAppointmentRules.Lord);
            }
        }
    }

    private static void CollapseFaction(WorldState world, int factionId)
    {
        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return;
        }

        var cityIds = world.Cities
            .Where(city => city.OwnerFactionId == factionId)
            .Select(city => city.Id)
            .ToList();

        foreach (var cityId in cityIds)
        {
            var city = world.GetCity(cityId);
            if (city == null)
            {
                continue;
            }

            city.OwnerFactionId = 0;
            city.Loyalty = 50;
            city.PrefectAuthorizationType = PrefectAuthorizationType.None;
            city.PrefectPlanJobType = InternalAffairsJobType.Farm;
            city.PrefectPlanConstructionProjectType = ConstructionProjectType.None;
            city.PrefectPlanInvestedGold = 0;
            city.PrefectPlanTotalMonths = 0;
            city.PrefectPlanRemainingMonths = 0;
            city.PrefectPlanIsPlayerDirected = false;
        }

        foreach (var officerId in faction.OfficerIds.ToList())
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            OfficerAppointmentRules.RemoveAppointment(officer, OfficerAppointmentRules.Lord);
            if (officer.CityId > 0)
            {
                officer.FreeOfficerStayMonths = Math.Max(officer.FreeOfficerStayMonths, 2);
            }
        }

        faction.OfficerIds.Clear();
        faction.RulerOfficerId = 0;
        faction.ChancellorOfficerId = 0;
        faction.ChiefStrategistOfficerId = 0;
        world.DiplomacyRelations.RemoveAll(relation => relation.FactionAId == factionId || relation.FactionBId == factionId);
        world.PendingCommands.RemoveAll(command => command.ActorFactionId == factionId);
        world.InternalAffairsSchedules.RemoveAll(schedule => cityIds.Contains(schedule.CityId));
        world.PendingSuccessionRecords.RemoveAll(record => record.FactionId == factionId);
    }

    private static bool IsOfficerAlive(WorldState world, OfficerData? officer)
    {
        if (officer == null)
        {
            return false;
        }

        return officer.DeathYear <= 0 || world.Year <= officer.DeathYear;
    }
}
