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
        ["ui.gold"] = ("金", "Gold"),
        ["ui.food"] = ("糧", "Food"),
        ["ui.troops"] = ("兵力", "Troops"),
        ["ui.officers"] = ("武將", "Officers"),
        ["ui.lang_btn_zh"] = ("繁中", "繁中"),
        ["ui.lang_btn_en"] = ("English", "English"),
        ["log.boot"] = ("M1 初始化完成：核心服務已接線。", "M1 initialized: services wired.")
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

    public string FormatCityStats(int gold, int food, int troops, int officers)
    {
        return $"{T("ui.gold")}: {gold}\n{T("ui.food")}: {food}\n{T("ui.troops")}: {troops}\n{T("ui.officers")}: {officers}";
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
}
