using System;
using System.Collections.Generic;

namespace ThreeKingdom.Battle;

/// <summary>
/// Temporary battle-officer AI profile source.  Keeping this data outside the
/// scene controller is the first step toward scenario-provided officer data.
/// </summary>
internal static class BattleOfficerAiProfiles
{
    private readonly record struct Profile(int Intelligence, int Combat);

    private static readonly IReadOnlyDictionary<string, Profile> Profiles =
        new Dictionary<string, Profile>(StringComparer.Ordinal)
        {
            ["Xiahou Yuan"] = new(65, 86),
            ["Zhang He"] = new(80, 76),
            ["Dong Zhuo"] = new(66, 88),
            ["Li Jue"] = new(54, 72),
            ["Guo Si"] = new(52, 78),
            ["Cao Hong"] = new(65, 80),
            ["Cao Chun"] = new(71, 82)
        };

    internal static int GetTacticalIntelligence(string officerName)
    {
        return Profiles.TryGetValue(officerName, out var profile) ? profile.Intelligence : 50;
    }

    internal static int GetCombatAttribute(string officerName)
    {
        return Profiles.TryGetValue(officerName, out var profile) ? profile.Combat : 70;
    }
}
