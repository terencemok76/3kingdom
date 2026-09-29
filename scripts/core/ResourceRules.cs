using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

// City resource points are strategic, finite inputs. They deliberately do not
// depend on merchant presence or Commercial, so the same rule is used by AI
// and player cities.
public static class ResourceRules
{
    private const int MaximumDiscoveredWoodDeposits = 6;
    private const int MaximumDiscoveredMetalDeposits = 3;
    private const int MaximumDiscoveredStoneDeposits = 4;

    public enum SurveyAvailability
    {
        Available,
        AvailableWithoutResourcePoint,
        ResourceStillAvailable,
        RenewableResource,
        AttemptsExhausted,
        DiscoveryPoolExhausted
    }

    public readonly record struct ExtractionResult(StrategicResourceType Type, int Amount)
    {
        public bool HasOutput => Amount > 0;
    }

    public readonly record struct SurveyResult(StrategicResourceType Type, bool Success, int Reserve, int Attempt, int Chance)
    {
        public bool WasAttempted => Attempt > 0;
    }

    public static bool HasExtractableResource(CityData city) =>
        city.ResourceDeposits?.Any(deposit => deposit.RemainingReserve > 0 && deposit.MonthlyYield > 0) == true;

    public static CityResourceDepositData? GetPreferredDeposit(CityData city) =>
        city.ResourceDeposits?
            .Where(deposit => deposit.RemainingReserve > 0 && deposit.MonthlyYield > 0)
            .OrderByDescending(deposit => deposit.RemainingReserve)
            .ThenByDescending(deposit => deposit.MonthlyYield)
            .ThenBy(deposit => (int)deposit.Type)
            .FirstOrDefault();

    public static int GetExtractionPriority(CityData city)
    {
        var deposit = GetPreferredDeposit(city);
        return deposit == null ? int.MaxValue : Math.Max(0, 80 - deposit.MonthlyYield * 3);
    }

    public static bool HasSurveyableResource(CityData city) => GetSurveyAvailability(city) is
        SurveyAvailability.Available or SurveyAvailability.AvailableWithoutResourcePoint;

    public static bool HasSurveyableResource(WorldState world, CityData city) => GetSurveyAvailability(world, city) is
        SurveyAvailability.Available or SurveyAvailability.AvailableWithoutResourcePoint;

    public static SurveyAvailability GetSurveyAvailability(WorldState world, CityData city)
    {
        var availability = GetSurveyAvailability(city);
        return availability == SurveyAvailability.AvailableWithoutResourcePoint && !GetDiscoverableResourceTypes(world).Any()
            ? SurveyAvailability.DiscoveryPoolExhausted
            : availability;
    }

    public static SurveyAvailability GetSurveyAvailability(CityData city)
    {
        var deposit = city.ResourceDeposits?.FirstOrDefault();
        if (deposit == null)
        {
            return GetUndiscoveredSurveyBaseChance(city.UndiscoveredResourceSurveyAttempts) > 0
                ? SurveyAvailability.AvailableWithoutResourcePoint
                : SurveyAvailability.AttemptsExhausted;
        }

        if (deposit.Type == StrategicResourceType.Wood)
        {
            return SurveyAvailability.RenewableResource;
        }

        if (deposit.RemainingReserve > 0)
        {
            return SurveyAvailability.ResourceStillAvailable;
        }

        return GetSurveyBaseChance(deposit.Type, deposit.SurveyAttempts) > 0
            ? SurveyAvailability.Available
            : SurveyAvailability.AttemptsExhausted;
    }

    public static int GetSurveyPriority(CityData city)
    {
        var deposit = GetSurveyableDeposit(city);
        return deposit != null
            ? 95 + deposit.SurveyAttempts * 10
            : GetSurveyAvailability(city) == SurveyAvailability.AvailableWithoutResourcePoint
                ? 120 + city.UndiscoveredResourceSurveyAttempts * 10
                : int.MaxValue;
    }

    public static int GetSurveyPriority(WorldState world, CityData city) =>
        GetSurveyAvailability(world, city) == SurveyAvailability.DiscoveryPoolExhausted
            ? int.MaxValue
            : GetSurveyPriority(city);

    public static ExtractionResult ApplyExtraction(CityData city, int workBonus)
    {
        var deposit = GetPreferredDeposit(city);
        if (deposit == null)
        {
            return default;
        }

        var amount = Math.Min(deposit.RemainingReserve, Math.Max(1, deposit.MonthlyYield + workBonus));
        deposit.RemainingReserve -= amount;
        switch (deposit.Type)
        {
            case StrategicResourceType.Wood: city.Wood += amount; break;
            case StrategicResourceType.Metal: city.Metal += amount; break;
            case StrategicResourceType.Stone: city.Stone += amount; break;
        }

        return new ExtractionResult(deposit.Type, amount);
    }

    public static SurveyResult ApplySurvey(WorldState world, CityData city, int workBonus, Random random)
    {
        var deposit = GetSurveyableDeposit(city);
        if (deposit == null)
        {
            return ApplyUndiscoveredResourceSurvey(world, city, workBonus, random);
        }

        var attempt = deposit.SurveyAttempts + 1;
        var chance = Math.Clamp(GetSurveyBaseChance(deposit.Type, deposit.SurveyAttempts) + Math.Min(12, Math.Max(0, workBonus) * 2), 0, 75);
        deposit.SurveyAttempts = attempt;
        if (random.Next(100) >= chance)
        {
            return new SurveyResult(deposit.Type, false, 0, attempt, chance);
        }

        var reserve = GetSurveyReserve(deposit.Type, attempt, random);
        deposit.RemainingReserve = reserve;
        deposit.MonthlyYield = Math.Max(3, deposit.MonthlyYield - (attempt > 1 ? 1 : 0));
        return new SurveyResult(deposit.Type, true, reserve, attempt, chance);
    }

    public static void RefreshMonthlyDeposits(CityData city)
    {
        if (city.ResourceDeposits == null)
        {
            return;
        }

        foreach (var deposit in city.ResourceDeposits.Where(item => item.Type == StrategicResourceType.Wood && item.MaxReserve > 0))
        {
            var regrowth = Math.Max(1, deposit.MonthlyYield / 2);
            deposit.RemainingReserve = Math.Min(deposit.MaxReserve, deposit.RemainingReserve + regrowth);
        }
    }

    // Existing scenarios predate authored deposits. Use a deterministic
    // fallback so old scenario/save data immediately has varied resources;
    // scenario data can override it with one explicit ResourceDeposits entry.
    public static void EnsureCityDeposits(CityData city)
    {
        city.ResourceDeposits ??= new List<CityResourceDepositData>();
        if (city.ResourceDeposits.Count > 1)
        {
            // A city has at most one strategic resource point. Keep the first
            // authored entry for backwards-compatible saves/scenarios.
            city.ResourceDeposits.RemoveRange(1, city.ResourceDeposits.Count - 1);
        }

        if (city.ResourceDeposits.Count == 1)
        {
            return;
        }

        // Some cities have no extractable strategic resource at all.
        if (city.Id % 5 == 0)
        {
            return;
        }

        var primary = (StrategicResourceType)((city.Id - 1) % 3);
        AddDeposit(city, primary, 8 + city.Id % 5, 900 + city.Id % 7 * 180);
    }

    private static void AddDeposit(CityData city, StrategicResourceType type, int monthlyYield, int reserve)
    {
        city.ResourceDeposits.Add(new CityResourceDepositData
        {
            Type = type,
            MonthlyYield = monthlyYield,
            RemainingReserve = reserve,
            MaxReserve = type == StrategicResourceType.Wood ? reserve : 0
        });
    }

    private static CityResourceDepositData? GetSurveyableDeposit(CityData city) =>
        city.ResourceDeposits?
            .Where(deposit =>
                deposit.Type is StrategicResourceType.Metal or StrategicResourceType.Stone &&
                deposit.RemainingReserve <= 0 &&
                GetSurveyBaseChance(deposit.Type, deposit.SurveyAttempts) > 0)
            .OrderBy(deposit => deposit.SurveyAttempts)
            .ThenBy(deposit => (int)deposit.Type)
            .FirstOrDefault();

    private static int GetSurveyBaseChance(StrategicResourceType type, int completedAttempts)
    {
        var chances = type switch
        {
            StrategicResourceType.Metal => new[] { 40, 20, 5 },
            StrategicResourceType.Stone => new[] { 35, 18, 5 },
            _ => Array.Empty<int>()
        };
        return completedAttempts >= 0 && completedAttempts < chances.Length ? chances[completedAttempts] : 0;
    }

    private static SurveyResult ApplyUndiscoveredResourceSurvey(WorldState world, CityData city, int workBonus, Random random)
    {
        var types = GetDiscoverableResourceTypes(world).ToList();
        if (types.Count == 0)
        {
            return default;
        }

        var attempt = city.UndiscoveredResourceSurveyAttempts + 1;
        var chance = Math.Clamp(GetUndiscoveredSurveyBaseChance(city.UndiscoveredResourceSurveyAttempts) + Math.Min(12, Math.Max(0, workBonus) * 2), 0, 40);
        city.UndiscoveredResourceSurveyAttempts = attempt;
        if (random.Next(100) >= chance)
        {
            return new SurveyResult(default, false, 0, attempt, chance);
        }

        var type = RollUndiscoveredResourceType(types, random);
        var reserve = type switch
        {
            StrategicResourceType.Wood => 700 + random.Next(-120, 121),
            StrategicResourceType.Stone => 650 + random.Next(-110, 111),
            _ => 550 + random.Next(-100, 101)
        };
        var monthlyYield = type switch
        {
            StrategicResourceType.Wood => 9,
            StrategicResourceType.Stone => 8,
            _ => 7
        };
        city.ResourceDeposits ??= new List<CityResourceDepositData>();
        city.ResourceDeposits.Add(new CityResourceDepositData
        {
            Type = type,
            MonthlyYield = monthlyYield,
            RemainingReserve = reserve,
            MaxReserve = type == StrategicResourceType.Wood ? reserve : 0,
            IsDiscoveredBySurvey = true
        });
        return new SurveyResult(type, true, reserve, attempt, chance);
    }

    private static int GetUndiscoveredSurveyBaseChance(int completedAttempts)
    {
        var chances = new[] { 15, 8, 3 };
        return completedAttempts >= 0 && completedAttempts < chances.Length ? chances[completedAttempts] : 0;
    }

    private static IEnumerable<StrategicResourceType> GetDiscoverableResourceTypes(WorldState world)
    {
        foreach (var type in new[] { StrategicResourceType.Wood, StrategicResourceType.Stone, StrategicResourceType.Metal })
        {
            if (CountDiscoveredResourcePoints(world, type) < GetMaximumDiscoveredResourcePoints(type))
            {
                yield return type;
            }
        }
    }

    private static int CountDiscoveredResourcePoints(WorldState world, StrategicResourceType type) => world.Cities
        .SelectMany(city => city.ResourceDeposits ?? Enumerable.Empty<CityResourceDepositData>())
        .Count(deposit => deposit.Type == type && deposit.IsDiscoveredBySurvey);

    private static int GetMaximumDiscoveredResourcePoints(StrategicResourceType type) => type switch
    {
        StrategicResourceType.Wood => MaximumDiscoveredWoodDeposits,
        StrategicResourceType.Metal => MaximumDiscoveredMetalDeposits,
        StrategicResourceType.Stone => MaximumDiscoveredStoneDeposits,
        _ => 0
    };

    private static StrategicResourceType RollUndiscoveredResourceType(IReadOnlyCollection<StrategicResourceType> availableTypes, Random random)
    {
        var weightedTypes = new List<(StrategicResourceType Type, int Weight)>();
        if (availableTypes.Contains(StrategicResourceType.Wood)) weightedTypes.Add((StrategicResourceType.Wood, 55));
        if (availableTypes.Contains(StrategicResourceType.Stone)) weightedTypes.Add((StrategicResourceType.Stone, 30));
        if (availableTypes.Contains(StrategicResourceType.Metal)) weightedTypes.Add((StrategicResourceType.Metal, 15));

        var roll = random.Next(weightedTypes.Sum(item => item.Weight));
        foreach (var item in weightedTypes)
        {
            if (roll < item.Weight)
            {
                return item.Type;
            }

            roll -= item.Weight;
        }

        return weightedTypes[^1].Type;
    }

    private static int GetSurveyReserve(StrategicResourceType type, int attempt, Random random)
    {
        var baseReserve = type == StrategicResourceType.Metal ? 600 : 700;
        var diminishedReserve = Math.Max(150, baseReserve / (1 << Math.Min(2, attempt - 1)));
        var variance = Math.Max(25, diminishedReserve / 5);
        return diminishedReserve + random.Next(-variance, variance + 1);
    }
}
