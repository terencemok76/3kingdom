using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public enum GameLanguage
{
    TraditionalChinese,
    English
}

public class LocalizationService
{
    private const string LocalePath = "res://data/localization/locale.json";

    private readonly Dictionary<string, LocaleTextEntry> _textTable = new(StringComparer.OrdinalIgnoreCase);

    public event Action? LanguageChanged;

    public GameLanguage CurrentLanguage { get; private set; } = GameLanguage.TraditionalChinese;

    public bool IsTraditionalChinese => CurrentLanguage == GameLanguage.TraditionalChinese;

    public void Load()
    {
        _textTable.Clear();

        if (!FileAccess.FileExists(LocalePath))
        {
            GD.PushWarning($"Locale file missing: {LocalePath}");
            return;
        }

        using var file = FileAccess.Open(LocalePath, FileAccess.ModeFlags.Read);
        var json = file.GetAsText();
        var document = JsonSerializer.Deserialize<Dictionary<string, LocaleTextEntry>>(json);
        if (document == null)
        {
            GD.PushWarning($"Locale file could not be parsed: {LocalePath}");
            return;
        }

        foreach (var pair in document)
        {
            _textTable[pair.Key] = pair.Value;
        }
    }

    public void ToggleLanguage()
    {
        SetLanguage(IsTraditionalChinese ? GameLanguage.English : GameLanguage.TraditionalChinese);
    }

    public void SetLanguage(GameLanguage language)
    {
        if (CurrentLanguage == language)
        {
            return;
        }

        CurrentLanguage = language;
        LanguageChanged?.Invoke();
    }

    public string T(string key)
    {
        return TForLanguage(CurrentLanguage, key);
    }

    public string TForLanguage(GameLanguage language, string key)
    {
        if (!_textTable.TryGetValue(key, out var textPair))
        {
            return key;
        }

        return language == GameLanguage.TraditionalChinese ? textPair.ZhHant : textPair.En;
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }

    public string FormatForLanguage(GameLanguage language, string key, params object[] args)
    {
        return string.Format(TForLanguage(language, key), args);
    }

    public string FormatYearMonth(int year, int month)
    {
        return Format("fmt.year_month", year, month);
    }

    public string FormatCityHeader(string cityName)
    {
        return Format("fmt.city_header", T("ui.city"), cityName);
    }

    public string FormatPlayerFaction(string factionName)
    {
        return Format("fmt.player_faction", T("ui.player"), factionName);
    }

    public string FormatCityStats(CityData city)
    {
        return
            $"{T("ui.gold")}: {city.Gold}\n" +
            $"{T("ui.food")}: {city.Food}\n" +
            $"{T("ui.troops")}: {city.Troops}\n" +
            $"{T("ui.officers")}: {city.OfficerIds.Count}\n" +
            $"{T("ui.farm")}: {city.Farm}\n" +
            $"{T("ui.commercial")}: {city.Commercial}\n" +
            $"{T("ui.defense")}: {city.Defense}\n" +
            $"{T("ui.loyalty")}: {city.Loyalty}";
    }

    public string FormatEmptyCityStats()
    {
        return
            $"{T("ui.gold")}: 0\n" +
            $"{T("ui.food")}: 0\n" +
            $"{T("ui.troops")}: 0\n" +
            $"{T("ui.officers")}: 0\n" +
            $"{T("ui.farm")}: 0\n" +
            $"{T("ui.commercial")}: 0\n" +
            $"{T("ui.defense")}: 0\n" +
            $"{T("ui.loyalty")}: 0";
    }

    public string FormatOwnerLine(string ownerName)
    {
        return Format("fmt.owner_line", T("ui.owner"), ownerName);
    }

    public string FormatCitySelected(string cityName)
    {
        return Format("fmt.city_selected", cityName);
    }

    public string FormatAiCityAction(string factionName, string cityName, string actionMessage)
    {
        return Format("fmt.ai_city_action", factionName, cityName, actionMessage);
    }

    public string FormatMonthAdvanced(int year, int month)
    {
        return Format("fmt.month_advanced", year, month);
    }

    public string FormatFactionDestroyed(string factionName)
    {
        return Format("fmt.faction_destroyed", factionName);
    }

    public string GetCityName(CityData city)
    {
        if (IsTraditionalChinese)
        {
            if (!string.IsNullOrWhiteSpace(city.NameZhHant))
            {
                return city.NameZhHant;
            }

            if (!string.IsNullOrWhiteSpace(city.Name))
            {
                return city.Name;
            }

            return city.NameEn;
        }

        if (!string.IsNullOrWhiteSpace(city.NameEn))
        {
            return city.NameEn;
        }

        if (!string.IsNullOrWhiteSpace(city.Name))
        {
            return city.Name;
        }

        return city.NameZhHant;
    }

    public string GetFactionName(WorldState world, int factionId)
    {
        if (factionId <= 0)
        {
            return T("ui.neutral");
        }

        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return T("ui.unknown");
        }

        if (IsTraditionalChinese)
        {
            if (!string.IsNullOrWhiteSpace(faction.NameZhHant))
            {
                return faction.NameZhHant;
            }

            return faction.NameEn;
        }

        if (!string.IsNullOrWhiteSpace(faction.NameEn))
        {
            return faction.NameEn;
        }

        return faction.NameZhHant;
    }

    public string GetOfficerName(OfficerData officer)
    {
        if (IsTraditionalChinese)
        {
            if (!string.IsNullOrWhiteSpace(officer.NameZhHant))
            {
                return officer.NameZhHant;
            }

            return officer.Name;
        }

        return !string.IsNullOrWhiteSpace(officer.Name) ? officer.Name : officer.NameZhHant;
    }

    public string GetOfficerRole(OfficerData officer)
    {
        var key = officer.Role.ToLowerInvariant() switch
        {
            "lord" => "role.lord",
            "general" => "role.general",
            "strategist" => "role.strategist",
            "advisor" => "role.advisor",
            "governor" => "role.governor",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(key))
        {
            return officer.Role;
        }

        var localized = T(key);
        return string.Equals(localized, key, StringComparison.Ordinal) ? officer.Role : localized;
    }

    public string GetOfficerStatus(WorldState world, OfficerData officer)
    {
        foreach (var schedule in world.InternalAffairsSchedules)
        {
            if (schedule.State == InternalAffairsScheduleState.Active && schedule.OfficerId == officer.Id)
            {
                return T("status.internal_affairs");
            }
        }

        if (officer.LastAssignedYear != world.Year || officer.LastAssignedMonth != world.Month)
        {
            return T("status.idle");
        }

        var key = officer.LastAssignedCommand switch
        {
            CommandType.InternalAffairs => "status.internal_affairs",
            CommandType.Develop => "status.develop",
            CommandType.Recruit => "status.recruit",
            CommandType.Move => "status.move",
            CommandType.Search => "status.search",
            CommandType.Attack => "status.attack",
            _ => "status.idle"
        };

        return T(key);
    }

    private sealed class LocaleTextEntry
    {
        [JsonPropertyName("zhHant")]
        public string ZhHant { get; set; } = string.Empty;

        [JsonPropertyName("en")]
        public string En { get; set; } = string.Empty;
    }
}
