using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using static ThreeKingdom.Battle.BattleAiSettings;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattlePresentationSettings;
using static ThreeKingdom.Battle.BattleResourcePaths;
using static ThreeKingdom.Battle.BattleUnitTypes;
using static ThreeKingdom.Battle.BattleUnitVisualCatalog;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private BattleWeatherType GetCurrentBattleWeather()
    {
        return _currentBattleWeather ?? ResolveScenarioDefinition().Weather;
    }

    private BattleTimeOfDay GetCurrentBattleTimeOfDay()
    {
        return _currentBattleTimeOfDay ?? ResolveScenarioDefinition().TimeOfDay;
    }

    private BattleWindDirection GetCurrentBattleWindDirection()
    {
        return _currentBattleWindDirection ?? ResolveScenarioDefinition().WindDirection;
    }

    private BattleWindPower GetCurrentBattleWindPower()
    {
        return _currentBattleWindPower ?? ResolveScenarioDefinition().WindPower;
    }

    private static BattleWeatherType GetNextBattleWeather(BattleWeatherType weather)
    {
        return BattleEnvironmentSystem.GetNextWeather(weather);
    }

    private static BattleTimeOfDay GetNextBattleTimeOfDay(BattleTimeOfDay timeOfDay)
    {
        return BattleEnvironmentSystem.GetNextTimeOfDay(timeOfDay);
    }

    private static BattleWindDirection GetNextBattleWindDirection(BattleWindDirection direction)
    {
        return BattleEnvironmentSystem.GetNextWindDirection(direction);
    }

    private static BattleWindPower GetNextBattleWindPower(BattleWindPower power)
    {
        return BattleEnvironmentSystem.GetNextWindPower(power);
    }

    private string FormatBattleWeather(BattleWeatherType weather)
    {
        return weather switch
        {
            BattleWeatherType.Cloudy => BattleText("ui.battle.weather_cloudy", "Cloudy"),
            BattleWeatherType.Rain => BattleText("ui.battle.weather_rain", "Rain"),
            _ => BattleText("ui.battle.weather_sunny", "Sunny")
        };
    }

    private string FormatBattleTimeOfDay(BattleTimeOfDay timeOfDay)
    {
        return timeOfDay switch
        {
            BattleTimeOfDay.Dawn => BattleText("ui.battle.time_dawn", "Dawn"),
            BattleTimeOfDay.Afternoon => BattleText("ui.battle.time_afternoon", "Afternoon"),
            BattleTimeOfDay.Night => BattleText("ui.battle.time_night", "Night"),
            _ => BattleText("ui.battle.time_morning", "Morning")
        };
    }

    private void ApplyTimeOfDayVisual(bool animate)
    {
        if (_timeOfDayOverlay == null)
        {
            return;
        }

        var targetColor = GetTimeOfDayOverlayColor(GetCurrentBattleTimeOfDay());
        _timeOfDayOverlayTween?.Kill();
        if (!animate)
        {
            _timeOfDayOverlay.Color = targetColor;
            return;
        }

        _timeOfDayOverlayTween = _timeOfDayOverlay.CreateTween();
        _timeOfDayOverlayTween.SetEase(Tween.EaseType.InOut);
        _timeOfDayOverlayTween.SetTrans(Tween.TransitionType.Sine);
        _timeOfDayOverlayTween.TweenProperty(_timeOfDayOverlay, "color", targetColor, 0.45);
    }

    private static Color GetTimeOfDayOverlayColor(BattleTimeOfDay timeOfDay)
    {
        return timeOfDay switch
        {
            BattleTimeOfDay.Dawn => new Color(1.0f, 0.52f, 0.24f, 0.16f),
            BattleTimeOfDay.Afternoon => new Color(1.0f, 0.86f, 0.32f, 0.08f),
            BattleTimeOfDay.Night => new Color(0.03f, 0.08f, 0.22f, 0.42f),
            _ => new Color(1.0f, 0.95f, 0.78f, 0.04f)
        };
    }

    private void BuildWeatherVisuals()
    {
        if (_weatherOverlay == null || _rainStreaks.Count > 0)
        {
            return;
        }

        const int rainStreakCount = 76;
        for (var index = 0; index < rainStreakCount; index++)
        {
            var streak = new ColorRect
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Color = new Color(0.72f, 0.84f, 1.0f, 0.44f),
                Size = new Vector2(index % 3 == 0 ? 1.4f : 1.0f, 30.0f + (index % 5) * 5.0f),
                Rotation = -0.34f,
                Visible = false
            };
            _weatherOverlay.AddChild(streak);
            _rainStreaks.Add(streak);
        }
    }

    private void ApplyWeatherVisual(bool animate)
    {
        if (_weatherOverlay == null)
        {
            return;
        }

        var targetColor = GetWeatherOverlayColor(GetCurrentBattleWeather());
        _weatherOverlayTween?.Kill();
        if (!animate)
        {
            _weatherOverlay.Color = targetColor;
        }
        else
        {
            _weatherOverlayTween = _weatherOverlay.CreateTween();
            _weatherOverlayTween.SetEase(Tween.EaseType.InOut);
            _weatherOverlayTween.SetTrans(Tween.TransitionType.Sine);
            _weatherOverlayTween.TweenProperty(_weatherOverlay, "color", targetColor, 0.35);
        }

        var showRain = GetCurrentBattleWeather() == BattleWeatherType.Rain;
        foreach (var streak in _rainStreaks)
        {
            streak.Visible = showRain;
        }
    }

    private void UpdateWeatherVisual(double delta)
    {
        if (_weatherOverlay == null || GetCurrentBattleWeather() != BattleWeatherType.Rain)
        {
            return;
        }

        _weatherEffectTime += delta;
        var overlaySize = _weatherOverlay.Size;
        if (overlaySize.X <= 0.0f || overlaySize.Y <= 0.0f)
        {
            overlaySize = GetViewportRect().Size;
        }

        var travelHeight = overlaySize.Y + 120.0f;
        var travelWidth = overlaySize.X + 180.0f;
        for (var index = 0; index < _rainStreaks.Count; index++)
        {
            var streak = _rainStreaks[index];
            var speed = 280.0f + (index % 7) * 36.0f;
            var phase = (float)((_weatherEffectTime * speed) + index * 53.0);
            var y = PositiveModulo(phase, travelHeight) - 80.0f;
            var xBase = PositiveModulo((index * 97.0f) + (float)_weatherEffectTime * 46.0f, travelWidth) - 90.0f;
            streak.Position = new Vector2(xBase, y);
        }
    }

    private static Color GetWeatherOverlayColor(BattleWeatherType weather)
    {
        return weather switch
        {
            BattleWeatherType.Cloudy => new Color(0.34f, 0.43f, 0.52f, 0.18f),
            BattleWeatherType.Rain => new Color(0.18f, 0.28f, 0.42f, 0.30f),
            _ => new Color(1.0f, 1.0f, 1.0f, 0.0f)
        };
    }

    private static float PositiveModulo(float value, float modulus)
    {
        if (modulus <= 0.0f)
        {
            return 0.0f;
        }

        var result = value % modulus;
        return result < 0.0f ? result + modulus : result;
    }

    private string FormatBattleWindDirection(BattleWindDirection direction)
    {
        return direction switch
        {
            BattleWindDirection.NorthWest => BattleText("ui.battle.wind_north_west", "NorthWest"),
            BattleWindDirection.SouthEast => BattleText("ui.battle.wind_south_east", "SouthEast"),
            BattleWindDirection.SouthWest => BattleText("ui.battle.wind_south_west", "SouthWest"),
            _ => BattleText("ui.battle.wind_north_east", "NorthEast")
        };
    }

    private string FormatBattleWindDirectionShort(BattleWindDirection direction)
    {
        return direction switch
        {
            BattleWindDirection.NorthWest => BattleText("ui.battle.wind_north_west_short", "NW"),
            BattleWindDirection.SouthEast => BattleText("ui.battle.wind_south_east_short", "SE"),
            BattleWindDirection.SouthWest => BattleText("ui.battle.wind_south_west_short", "SW"),
            _ => BattleText("ui.battle.wind_north_east_short", "NE")
        };
    }

    private string FormatBattleWindPower(BattleWindPower power)
    {
        return power switch
        {
            BattleWindPower.Calm => BattleText("ui.battle.wind_power_calm", "Calm"),
            BattleWindPower.Strong => BattleText("ui.battle.wind_power_strong", "Strong"),
            _ => BattleText("ui.battle.wind_power_breeze", "Breeze")
        };
    }

    private void ConfigureEnvironmentForecastTooltips()
    {
        if (_environmentSeed == 0)
        {
            return;
        }

        var forecast = GetEnvironmentForecast();
        var tooltip = BattleFormat(
            "ui.battle.environment_forecast",
            "Next period: {0} / {1} / Wind {2} {3}",
            FormatBattleTimeOfDay(forecast.TimeOfDay),
            FormatBattleWeather(forecast.Weather),
            FormatBattleWindDirection(forecast.WindDirection),
            FormatBattleWindPower(forecast.WindPower));
        foreach (var button in new[] { _timeButton, _weatherButton, _windButton, _windPowerButton })
        {
            if (button != null)
            {
                button.Disabled = !CanUseEnvironmentDebugControls;
                button.TooltipText = tooltip;
            }
        }
    }

    private static Vector2I GetWindGridOffset(BattleWindDirection direction)
    {
        return direction switch
        {
            BattleWindDirection.NorthEast => new Vector2I(1, -1),
            BattleWindDirection.NorthWest => new Vector2I(-1, -1),
            BattleWindDirection.SouthWest => new Vector2I(-1, 1),
            _ => new Vector2I(1, 1)
        };
    }
}
