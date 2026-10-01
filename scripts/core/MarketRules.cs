using System;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public enum MarketProductType
{
    Food,
    Horse,
    Metal,
    Wood,
    Stone
}

public readonly record struct MarketStorageLoss(int Food, int Horse, int Metal, int Wood, int Stone)
{
    public bool HasLoss => Food > 0 || Horse > 0 || Metal > 0 || Wood > 0 || Stone > 0;
}

// Shared rule surface for player UI and AI decisions. No caller gets a
// different price, capacity, or merchant-stock exception.
public static class MarketRules
{
    private const int BaseFoodStorageCapacity = 5000;
    private const int GranaryCapacityPerLevel = 5000;
    private const int BaseHorseStorageCapacity = 200;
    private const int HorseStableCapacityPerLevel = 200;
    private const int BaseRawResourceStorageCapacity = 500;
    private const int ResourceDepotCapacityPerLevel = 500;
    // Shared non-debug visibility for player and AI: own city or an active spy report.
    public static bool CanFactionViewMerchantInfo(WorldState world, int viewerFactionId, CityData city) =>
        world.CanFactionViewCity(viewerFactionId, city.Id);

    public static int GetTradeLotSize(MarketProductType product) => product == MarketProductType.Food ? 100 : 10;

    public static int GetAmount(CityData city, MarketProductType product) => product switch
    {
        MarketProductType.Food => city.Food,
        MarketProductType.Horse => city.Horses,
        MarketProductType.Metal => city.Metal,
        MarketProductType.Wood => city.Wood,
        MarketProductType.Stone => city.Stone,
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
            case MarketProductType.Wood: city.Wood = value; break;
            case MarketProductType.Stone: city.Stone = value; break;
        }
    }

    public static int GetCapacity(CityData city, MarketProductType product) => product switch
    {
        MarketProductType.Food => (city.FoodStorageCapacity > 0 ? city.FoodStorageCapacity : BaseFoodStorageCapacity) + city.GranaryLevel * GranaryCapacityPerLevel,
        MarketProductType.Horse => (city.HorseStorageCapacity > 0 ? city.HorseStorageCapacity : BaseHorseStorageCapacity) + city.HorseStableLevel * HorseStableCapacityPerLevel,
        MarketProductType.Metal => (city.MetalStorageCapacity > 0 ? city.MetalStorageCapacity : BaseRawResourceStorageCapacity) + city.ResourceDepotLevel * ResourceDepotCapacityPerLevel,
        MarketProductType.Wood => (city.WoodStorageCapacity > 0 ? city.WoodStorageCapacity : BaseRawResourceStorageCapacity) + city.ResourceDepotLevel * ResourceDepotCapacityPerLevel,
        MarketProductType.Stone => (city.StoneStorageCapacity > 0 ? city.StoneStorageCapacity : BaseRawResourceStorageCapacity) + city.ResourceDepotLevel * ResourceDepotCapacityPerLevel,
        _ => 0
    };

    public static int GetMerchantStock(CityData city, MarketProductType product) => product switch
    {
        MarketProductType.Food => city.MerchantFoodStock,
        MarketProductType.Horse => city.MerchantHorseStock,
        MarketProductType.Metal => city.MerchantMetalStock,
        MarketProductType.Wood => city.MerchantWoodStock,
        MarketProductType.Stone => city.MerchantStoneStock,
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
            case MarketProductType.Wood: city.MerchantWoodStock = value; break;
            case MarketProductType.Stone: city.MerchantStoneStock = value; break;
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
        var basePrice = product switch { MarketProductType.Food => 10, MarketProductType.Horse => 20, MarketProductType.Metal => 30, MarketProductType.Wood => 12, MarketProductType.Stone => 16, _ => 10 };
        var stock = GetMerchantStock(city, product);
        var target = Math.Max(GetTradeLotSize(product), GetCapacity(city, product) / 4);
        var shortagePercent = Math.Clamp((target - stock) * 60 / target, -25, 75);
        return Math.Max(1, basePrice * (100 + shortagePercent) / 100);
    }

    public static int GetDisplayBuyUnitPrice(CityData city, MarketProductType product)
    {
        var basePrice = product switch { MarketProductType.Food => 10, MarketProductType.Horse => 20, MarketProductType.Metal => 30, MarketProductType.Wood => 12, MarketProductType.Stone => 16, _ => 10 };
        var stock = GetDisplayMerchantStock(city, product);
        var target = Math.Max(GetTradeLotSize(product), GetCapacity(city, product) / 4);
        var shortagePercent = Math.Clamp((target - stock) * 60 / target, -25, 75);
        return Math.Max(1, basePrice * (100 + shortagePercent) / 100);
    }

    public static int GetSellUnitPrice(CityData city, MarketProductType product) => Math.Max(1, GetBuyUnitPrice(city, product) * 80 / 100);
    public static int GetDisplaySellUnitPrice(CityData city, MarketProductType product) => Math.Max(1, GetDisplayBuyUnitPrice(city, product) * 80 / 100);
    public static int GetAvailableCapacity(CityData city, MarketProductType product) => Math.Max(0, GetCapacity(city, product) - GetAmount(city, product));

    // Capacity applies after monthly production, upkeep, and city events. This
    // keeps player and AI inventories on the same loss rule.
    public static MarketStorageLoss ApplyMonthlyStorageLoss(CityData city)
    {
        var food = TrimToCapacity(city, MarketProductType.Food);
        var horse = TrimToCapacity(city, MarketProductType.Horse);
        var metal = TrimToCapacity(city, MarketProductType.Metal);
        var wood = TrimToCapacity(city, MarketProductType.Wood);
        var stone = TrimToCapacity(city, MarketProductType.Stone);
        return new MarketStorageLoss(food, horse, metal, wood, stone);
    }

    private static int TrimToCapacity(CityData city, MarketProductType product)
    {
        var amount = GetAmount(city, product);
        var loss = Math.Max(0, amount - GetCapacity(city, product));
        if (loss > 0)
        {
            SetAmount(city, product, amount - loss);
        }

        return loss;
    }
    public static int GetPreviousBuyPrice(CityData city, MarketProductType product) => product switch
    {
        MarketProductType.Food => city.PreviousFoodBuyPrice,
        MarketProductType.Horse => city.PreviousHorseBuyPrice,
        MarketProductType.Metal => city.PreviousMetalBuyPrice,
        MarketProductType.Wood => city.PreviousWoodBuyPrice,
        _ => city.PreviousStoneBuyPrice
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

    // Prices currently depend only on merchant supply relative to the city's
    // target market stock. Keep this explanation alongside the shared price
    // calculation so the UI never attributes a price change to a rule that is
    // not actually in effect.
    public static string GetDisplayPriceReasonKey(CityData city, MarketProductType product)
    {
        var stock = GetDisplayMerchantStock(city, product);
        var target = Math.Max(GetTradeLotSize(product), GetCapacity(city, product) / 4);
        return stock < target / 2 ? "ui.market_price_reason.low_supply" :
            stock > target * 3 / 2 ? "ui.market_price_reason.high_supply" :
            "ui.market_price_reason.balanced_supply";
    }

    private static int GetLastBuyPrice(CityData city, MarketProductType product) => product switch
    {
        MarketProductType.Food => city.LastFoodBuyPrice,
        MarketProductType.Horse => city.LastHorseBuyPrice,
        MarketProductType.Metal => city.LastMetalBuyPrice,
        MarketProductType.Wood => city.LastWoodBuyPrice,
        _ => city.LastStoneBuyPrice
    };

    private static void SetPreviousBuyPrice(CityData city, MarketProductType product, int value)
    {
        switch (product) { case MarketProductType.Food: city.PreviousFoodBuyPrice = value; break; case MarketProductType.Horse: city.PreviousHorseBuyPrice = value; break; case MarketProductType.Metal: city.PreviousMetalBuyPrice = value; break; case MarketProductType.Wood: city.PreviousWoodBuyPrice = value; break; default: city.PreviousStoneBuyPrice = value; break; }
    }

    private static void SetLastBuyPrice(CityData city, MarketProductType product, int value)
    {
        switch (product) { case MarketProductType.Food: city.LastFoodBuyPrice = value; break; case MarketProductType.Horse: city.LastHorseBuyPrice = value; break; case MarketProductType.Metal: city.LastMetalBuyPrice = value; break; case MarketProductType.Wood: city.LastWoodBuyPrice = value; break; default: city.LastStoneBuyPrice = value; break; }
    }
}
