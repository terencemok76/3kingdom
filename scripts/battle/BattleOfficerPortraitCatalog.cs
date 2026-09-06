using Godot;
using System;
using System.Collections.Generic;

namespace ThreeKingdom.Battle;

internal readonly record struct BattleOfficerPortraitDefinition(string SheetPath, Rect2 Region);

internal static class BattleOfficerPortraitCatalog
{
    private static readonly IReadOnlyDictionary<string, BattleOfficerPortraitDefinition> Definitions =
        new Dictionary<string, BattleOfficerPortraitDefinition>(StringComparer.Ordinal)
        {
            ["Xiahou Yuan"] = new("res://assets/portrait/team2.png", new Rect2(1234, 5, 300, 258)),
            ["Zhang He"] = new("res://assets/portrait/team2.png", new Rect2(612, 270, 304, 242)),
            ["Dong Zhuo"] = new("res://assets/portrait/team4.png", new Rect2(284, 7, 272, 226)),
            ["Li Jue"] = new("res://assets/portrait/team4.png", new Rect2(841, 237, 272, 220)),
            ["Guo Si"] = new("res://assets/portrait/team4.png", new Rect2(1120, 237, 276, 220)),
            ["Cao Hong"] = new("res://assets/portrait/team5.png", new Rect2(501, 250, 250, 252)),
            ["Cao Chun"] = new("res://assets/portrait/team7.png", new Rect2(250, 1003, 252, 250))
        };

    internal static bool TryGet(string officerName, out BattleOfficerPortraitDefinition definition)
    {
        return Definitions.TryGetValue(officerName, out definition);
    }
}
