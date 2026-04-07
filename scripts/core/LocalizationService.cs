using System;
using System.Collections.Generic;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public enum GameLanguage
{
    TraditionalChinese,
    English
}

public class LocalizationService
{
    private static readonly Dictionary<string, (string ZhHant, string En)> TextTable = new()
    {
        ["ui.end_turn"] = ("回合結束", "End Turn"),
        ["ui.commands"] = ("指令", "Commands"),
        ["ui.develop"] = ("開發", "Develop"),
        ["ui.recruit"] = ("徵兵", "Recruit"),
        ["ui.move"] = ("移動", "Move"),
        ["ui.search"] = ("搜索", "Search"),
        ["ui.attack"] = ("攻擊", "Attack"),
        ["ui.city"] = ("城市", "City"),
        ["ui.owner"] = ("勢力", "Owner"),
        ["ui.player"] = ("玩家", "Player"),
        ["ui.gold"] = ("金", "Gold"),
        ["ui.food"] = ("糧", "Food"),
        ["ui.troops"] = ("兵力", "Troops"),
        ["ui.officers"] = ("武將", "Officers"),
        ["ui.neutral"] = ("中立", "Neutral"),
        ["ui.unknown"] = ("未知", "Unknown"),
        ["ui.lang_btn_zh"] = ("繁中", "繁中"),
        ["ui.lang_btn_en"] = ("English", "English"),
        ["log.boot"] = ("M1 初始化完成：核心服務已接線。", "M1 initialized: services wired."),
        ["log.player_end_turn"] = ("玩家回合結束，AI 勢力行動中...", "Player turn ended. AI factions are taking actions...")
    };

    public event Action? LanguageChanged;

    public GameLanguage CurrentLanguage { get; private set; } = GameLanguage.TraditionalChinese;

    public bool IsTraditionalChinese => CurrentLanguage == GameLanguage.TraditionalChinese;

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
        if (!TextTable.TryGetValue(key, out var textPair))
        {
            return key;
        }

        return IsTraditionalChinese ? textPair.ZhHant : textPair.En;
    }

    public string FormatYearMonth(int year, int month)
    {
        return IsTraditionalChinese ? $"{year}年 {month}月" : $"Year {year}  Month {month}";
    }

    public string FormatCityHeader(string cityName)
    {
        return $"{T("ui.city")}: {cityName}";
    }

    public string FormatPlayerFaction(string factionName)
    {
        return $"{T("ui.player")}: {factionName}";
    }

    public string FormatCityStats(int gold, int food, int troops, int officers)
    {
        return $"{T("ui.gold")}: {gold}\n{T("ui.food")}: {food}\n{T("ui.troops")}: {troops}\n{T("ui.officers")}: {officers}";
    }

    public string FormatOwnerLine(string ownerName)
    {
        return $"{T("ui.owner")}: {ownerName}";
    }

    public string FormatCitySelected(string cityName)
    {
        return IsTraditionalChinese ? $"已選擇城市：{cityName}" : $"Selected city: {cityName}";
    }

    public string FormatAiCityAction(string factionName, string cityName, string actionMessage)
    {
        return IsTraditionalChinese
            ? $"[{factionName}] {cityName}：{actionMessage}"
            : $"[{factionName}] {cityName}: {actionMessage}";
    }

    public string FormatMonthAdvanced(int year, int month)
    {
        return IsTraditionalChinese
            ? $"進入新月份：{year}年 {month}月"
            : $"New month: Year {year} Month {month}";
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

            if (!string.IsNullOrWhiteSpace(faction.Name))
            {
                return faction.Name;
            }

            return faction.NameEn;
        }

        if (!string.IsNullOrWhiteSpace(faction.NameEn))
        {
            return faction.NameEn;
        }

        if (!string.IsNullOrWhiteSpace(faction.Name))
        {
            return faction.Name;
        }

        return faction.NameZhHant;
    }
}
