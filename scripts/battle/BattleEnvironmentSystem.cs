namespace ThreeKingdom.Battle;

internal readonly record struct BattleEnvironmentForecast(
    BattleTimeOfDay TimeOfDay,
    BattleWeatherType Weather,
    BattleWindDirection WindDirection,
    BattleWindPower WindPower,
    bool StartsNewDay);

internal static class BattleEnvironmentSystem
{
    internal static BattleWeatherType GetNextWeather(BattleWeatherType weather) => weather switch
    {
        BattleWeatherType.Sunny => BattleWeatherType.Cloudy,
        BattleWeatherType.Cloudy => BattleWeatherType.Rain,
        _ => BattleWeatherType.Sunny
    };

    internal static BattleTimeOfDay GetNextTimeOfDay(BattleTimeOfDay timeOfDay) => timeOfDay switch
    {
        BattleTimeOfDay.Dawn => BattleTimeOfDay.Morning,
        BattleTimeOfDay.Morning => BattleTimeOfDay.Afternoon,
        BattleTimeOfDay.Afternoon => BattleTimeOfDay.Night,
        _ => BattleTimeOfDay.Dawn
    };

    internal static BattleWindDirection GetNextWindDirection(BattleWindDirection direction) => direction switch
    {
        BattleWindDirection.NorthEast => BattleWindDirection.NorthWest,
        BattleWindDirection.NorthWest => BattleWindDirection.SouthWest,
        BattleWindDirection.SouthWest => BattleWindDirection.SouthEast,
        _ => BattleWindDirection.NorthEast
    };

    internal static BattleWindPower GetNextWindPower(BattleWindPower power) => power switch
    {
        BattleWindPower.Calm => BattleWindPower.Breeze,
        BattleWindPower.Breeze => BattleWindPower.Strong,
        _ => BattleWindPower.Calm
    };

    internal static BattleEnvironmentForecast CreateForecast(
        uint seed,
        int step,
        BattleTimeOfDay currentTime,
        BattleWeatherType currentWeather,
        BattleWindDirection currentWindDirection,
        BattleWindPower currentWindPower)
    {
        var nextTime = GetNextTimeOfDay(currentTime);
        var startsNewDay = nextTime == BattleTimeOfDay.Dawn;
        var nextWeather = startsNewDay
            ? RollNextWeather(currentWeather, RollPercent(seed, step, 0x9E3779B9u))
            : currentWeather;
        var nextWindDirection = RollNextWindDirection(
            currentWindDirection,
            RollPercent(seed, step, 0x85EBCA6Bu));
        var nextWindPower = RollNextWindPower(
            currentWindPower,
            nextWeather,
            RollPercent(seed, step, 0xC2B2AE35u));
        return new BattleEnvironmentForecast(
            nextTime,
            nextWeather,
            nextWindDirection,
            nextWindPower,
            startsNewDay);
    }

    private static BattleWeatherType RollNextWeather(BattleWeatherType weather, int roll) => weather switch
    {
        BattleWeatherType.Sunny => roll < 70 ? BattleWeatherType.Sunny : BattleWeatherType.Cloudy,
        BattleWeatherType.Cloudy => roll < 25
            ? BattleWeatherType.Sunny
            : roll < 75 ? BattleWeatherType.Cloudy : BattleWeatherType.Rain,
        BattleWeatherType.Rain => roll < 60 ? BattleWeatherType.Rain : BattleWeatherType.Cloudy,
        _ => BattleWeatherType.Cloudy
    };

    private static BattleWindDirection RollNextWindDirection(BattleWindDirection direction, int roll)
    {
        if (roll < 60)
        {
            return direction;
        }

        return RotateWindDirection(direction, roll < 80 ? -1 : 1);
    }

    private static BattleWindDirection RotateWindDirection(BattleWindDirection direction, int delta) =>
        (direction, delta) switch
        {
            (BattleWindDirection.NorthEast, -1) => BattleWindDirection.NorthWest,
            (BattleWindDirection.NorthWest, -1) => BattleWindDirection.SouthWest,
            (BattleWindDirection.SouthWest, -1) => BattleWindDirection.SouthEast,
            (BattleWindDirection.SouthEast, -1) => BattleWindDirection.NorthEast,
            (BattleWindDirection.NorthEast, _) => BattleWindDirection.SouthEast,
            (BattleWindDirection.SouthEast, _) => BattleWindDirection.SouthWest,
            (BattleWindDirection.SouthWest, _) => BattleWindDirection.NorthWest,
            _ => BattleWindDirection.NorthEast
        };

    private static BattleWindPower RollNextWindPower(
        BattleWindPower power,
        BattleWeatherType weather,
        int roll)
    {
        var decreaseThreshold = weather switch
        {
            BattleWeatherType.Sunny => 25,
            BattleWeatherType.Cloudy => 20,
            _ => 10
        };
        var increaseThreshold = weather switch
        {
            BattleWeatherType.Sunny => 90,
            BattleWeatherType.Cloudy => 80,
            _ => 65
        };
        if (roll < decreaseThreshold)
        {
            return power switch
            {
                BattleWindPower.Strong => BattleWindPower.Breeze,
                BattleWindPower.Breeze => BattleWindPower.Calm,
                _ => BattleWindPower.Calm
            };
        }

        if (roll < increaseThreshold)
        {
            return power;
        }

        return power switch
        {
            BattleWindPower.Calm => BattleWindPower.Breeze,
            BattleWindPower.Breeze => BattleWindPower.Strong,
            _ => BattleWindPower.Strong
        };
    }

    private static int RollPercent(uint seed, int step, uint salt)
    {
        unchecked
        {
            var value = seed ^ salt ^ ((uint)step + 1u) * 0x27D4EB2Du;
            value ^= value >> 15;
            value *= 0x85EBCA6Bu;
            value ^= value >> 13;
            value *= 0xC2B2AE35u;
            value ^= value >> 16;
            return (int)(value % 100u);
        }
    }
}
