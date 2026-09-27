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
    public readonly record struct ExtractionResult(StrategicResourceType Type, int Amount)
    {
        public bool HasOutput => Amount > 0;
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
}
