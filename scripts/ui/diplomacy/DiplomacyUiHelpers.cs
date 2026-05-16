using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal static class DiplomacyUiHelpers
{
    public static string GetActionLocaleKey(DiplomacyActionType actionType)
    {
        return actionType switch
        {
            DiplomacyActionType.Alliance => "command.diplomacy.alliance",
            DiplomacyActionType.Truce => "command.diplomacy.truce",
            DiplomacyActionType.Gift => "command.diplomacy.gift",
            DiplomacyActionType.Demand => "command.diplomacy.demand",
            DiplomacyActionType.BreakPact => "command.diplomacy.break_pact",
            _ => "command.diplomacy.alliance"
        };
    }

    public static string BuildDemandResourceSummary(LocalizationService? localization, int gold, int food, int horses)
    {
        if (localization == null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (gold > 0)
        {
            parts.Add($"{localization.T("ui.gold")} {gold}");
        }

        if (food > 0)
        {
            parts.Add($"{localization.T("ui.food")} {food}");
        }

        if (horses > 0)
        {
            parts.Add($"{localization.T("ui.horse")} {horses}");
        }

        return parts.Count == 0 ? "-" : string.Join(" / ", parts);
    }

    public static List<int> GetAvailableOfficerIds(TurnManager? turnManager, CityData? selectedCity)
    {
        if (selectedCity == null || turnManager?.World == null)
        {
            return new List<int>();
        }

        return selectedCity.OfficerIds
            .Select(id => turnManager.World.GetOfficer(id))
            .Where(officer =>
                officer != null &&
                officer.Id != turnManager.World.GetFaction(selectedCity.OwnerFactionId)?.RulerOfficerId &&
                !(officer.LastAssignedYear == turnManager.World.Year &&
                  officer.LastAssignedMonth == turnManager.World.Month))
            .Select(officer => officer!.Id)
            .ToList();
    }
}
