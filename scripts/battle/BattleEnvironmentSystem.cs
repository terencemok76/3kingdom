namespace ThreeKingdom.Battle;

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
}
