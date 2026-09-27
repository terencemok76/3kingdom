using System;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public enum MarketProductType
{
    Food,
    Horse,
    Metal
}

// Shared rule surface for player UI and AI decisions. No caller gets a
// different price, capacity, or merchant-stock exception.
public static class MarketRules
{
    // Shared non-debug visibility for player and AI: own city or an active spy report.
    public static bool CanFactionViewMerchantInfo(WorldState world, int viewerFactionId, CityData city) =>
        world.CanFactionViewCity(viewerFactionId, city.Id);

    public static int GetTradeLotSize(MarketProductType product) => product == MarketProductType.Food ? 100 : 10;

    public static int GetAmount(CityData city, MarketProductType product) => product switch
    {
        MarketProductType.Food => city.Food,
        MarketProductType.Horse => city.Horses,
        MarketProductType.Metal => city.Metal,
        _ => 0
    };

    public static void SetAmount(CityData city, MarketProductType product, int value)
    {
        value = Math.Max(0, value);
        switch (product)
        {
            case MarketProductType.Food: city.Food = value; break;
            case MarketProductType.Horse: city.Horses = value; break;
            case MarketProductType.Metal: city.Metal = value; break;
        }
    }

    public static int GetCapacity(CityData city, MarketProductType product) => product switch
    {
        MarketProductType.Food => city.FoodStorageCapacity > 0 ? city.FoodStorageCapacity : Math.Max(1000, city.Population * 4 + city.Farm * 50),
        MarketProductType.Horse => city.HorseStorageCapacity > 0 ? city.HorseStorageCapacity : 200 + city.HorsePastureLevel * 200,
        MarketProductType.Metal => city.MetalStorageCapacity > 0 ? city.MetalStorageCapacity : 300 + city.SiegeWorkshopLevel * 150,
        _ => 0
    };

    public static int GetMerchantStock(CityData city, MarketProductType product) => product switch
    {
        MarketProductType.Food => city.MerchantFoodStock,
        MarketProductType.Horse => city.MerchantHorseStock,
        MarketProductType.Metal => city.MerchantMetalStock,
        _ => 0
    };

    // The strategic map can preview a newly created city's market without
    // mutating its lazy-initialized merchant state.
    public static int GetDisplayMerchantStock(CityData city, MarketProductType product) =>
        !city.HasMerchant
            ? 0
            : city.MarketInitialized
            ? GetMerchantStock(city, product)
            : GetBaseMerchantStock(city, product);

    public static void SetMerchantStock(CityData city, MarketProductType product, int value)
    {
        value = Math.Max(0, value);
        switch (product)
        {
            case MarketProductType.Food: city.MerchantFoodStock = value; break;
            case MarketProductType.Horse: city.MerchantHorseStock = value; break;
            case MarketProductType.Metal: city.MerchantMetalStock = value; break;
        }
    }

    public static void RefreshMonthlyMarket(CityData city)
    {
        if (!city.HasMerchant)
        {
            return;
        }

        EnsureMarketInitialized(city);
        foreach (var product in Enum.GetValues<MarketProductType>())
        {
            SetPreviousBuyPrice(city, product, GetLastBuyPrice(city, product));
        }
        foreach (var product in Enum.GetValues<MarketProductType>())
        {
            var baseStock = GetBaseMerchantStock(city, product);
            SetMerchantStock(city, product, Math.Max(GetMerchantStock(city, product), baseStock));
        }

        var metalProduction = Math.Max(0, city.SiegeWorkshopLevel * 10 + city.Commercial / 10);
        SetAmount(city, MarketProductType.Metal, Math.Min(GetCapacity(city, MarketProductType.Metal), city.Metal + metalProduction));
        foreach (var product in Enum.GetValues<MarketProductType>())
        {
            SetLastBuyPrice(city, product, GetBuyUnitPrice(city, product));
        }
    }

    public static void EnsureMarketInitialized(CityData city)
    {
        if (!city.HasMerchant || city.MarketInitialized)
        {
            return;
        }

        foreach (var product in Enum.GetValues<MarketProductType>())
        {
            var baseStock = GetBaseMerchantStock(city, product);
            SetMerchantStock(city, product, baseStock);
        }

        city.MarketInitialized = true;
        foreach (var product in Enum.GetValues<MarketProductType>())
        {
            SetLastBuyPrice(city, product, GetBuyUnitPrice(city, product));
        }
    }

    private static int GetBaseMerchantStock(CityData city, MarketProductType product) =>
        Math.Max(GetTradeLotSize(product), GetCapacity(city, product) / 4 + city.Commercial * GetTradeLotSize(product) / 2);

    public static int GetBuyUnitPrice(CityData city, MarketProductType product)
    {
        var basePrice = product switch { MarketProductType.Food => 10, MarketProductType.Horse => 20, MarketProductType.Metal => 30, _ => 10 };
        var stock = GetMerchantStock(city, product);
        var target = Math.Max(GetTradeLotSize(product), GetCapacity(city, product) / 4);
        var shortagePercent = Math.Clamp((target - stock) * 60 / target, -25, 75);
        return Math.Max(1, basePrice * (100 + shortagePercent) / 100);
    }

    public static int GetDisplayBuyUnitPrice(CityData city, MarketProductType product)
    {
        var basePrice = product switch { MarketProductType.Food => 10, MarketProductType.Horse => 20, MarketProductType.Metal => 30, _ => 10 };
        var stock = GetDisplayMerchantStock(city, product);
        var target = Math.Max(GetTradeLotSize(product), GetCapacity(city, product) / 4);
        var shortagePercent = Math.Clamp((target - stock) * 60 / target, -25, 75);
        return Math.Max(1, basePrice * (100 + shortagePercent) / 100);
    }

    public static int GetSellUnitPrice(CityData city, MarketProductType product) => Math.Max(1, GetBuyUnitPrice(city, product) * 80 / 100);
    public static int GetDisplaySellUnitPrice(CityData city, MarketProductType product) => Math.Max(1, GetDisplayBuyUnitPrice(city, product) * 80 / 100);
    public static int GetAvailableCapacity(CityData city, MarketProductType product) => Math.Max(0, GetCapacity(city, product) - GetAmount(city, product));
    public static int GetPreviousBuyPrice(CityData city, MarketProductType product) => product switch
    {
        MarketProductType.Food => city.PreviousFoodBuyPrice,
        MarketProductType.Horse => city.PreviousHorseBuyPrice,
        _ => city.PreviousMetalBuyPrice
    };

    public static string GetStatusKey(CityData city, MarketProductType product)
    {
        var stock = GetMerchantStock(city, product);
        var target = Math.Max(GetTradeLotSize(product), GetCapacity(city, product) / 4);
        return stock < target / 2 ? "ui.market_status.shortage" : stock > target * 3 / 2 ? "ui.market_status.surplus" : "ui.market_status.normal";
    }

    public static string GetDisplayStatusKey(CityData city, MarketProductType product)
    {
        var stock = GetDisplayMerchantStock(city, product);
        var target = Math.Max(GetTradeLotSize(product), GetCapacity(city, product) / 4);
        return stock < target / 2 ? "ui.market_status.shortage" : stock > target * 3 / 2 ? "ui.market_status.surplus" : "ui.market_status.normal";
    }

    private static int GetLastBuyPrice(CityData city, MarketProductType product) => product switch
    {
        MarketProductType.Food => city.LastFoodBuyPrice,
        MarketProductType.Horse => city.LastHorseBuyPrice,
        _ => city.LastMetalBuyPrice
    };

    private static void SetPreviousBuyPrice(CityData city, MarketProductType product, int value)
    {
        switch (product) { case MarketProductType.Food: city.PreviousFoodBuyPrice = value; break; case MarketProductType.Horse: city.PreviousHorseBuyPrice = value; break; default: city.PreviousMetalBuyPrice = value; break; }
    }

    private static void SetLastBuyPrice(CityData city, MarketProductType product, int value)
    {
        switch (product) { case MarketProductType.Food: city.LastFoodBuyPrice = value; break; case MarketProductType.Horse: city.LastHorseBuyPrice = value; break; default: city.LastMetalBuyPrice = value; break; }
    }
}
