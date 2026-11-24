using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

internal static class CommandOfficerAssignmentRules
{
    internal static List<int> GetMovableOfficerIds(CityData sourceCity, List<int> requestedOfficerIds)
    {
        var result = new List<int>();
        foreach (var officerId in requestedOfficerIds)
        {
            if (!sourceCity.OfficerIds.Contains(officerId) || result.Contains(officerId))
            {
                continue;
            }

            result.Add(officerId);
        }

        return result;
    }

    internal static bool AreOfficerIdsAvailableForPendingOrder(
        WorldState world,
        List<int> requestedOfficerIds)
    {
        if (requestedOfficerIds.Count == 0)
        {
            return true;
        }

        foreach (var officerId in requestedOfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null ||
                IsOfficerAssignedThisMonth(world, officer) ||
                BattleCampaignService.IsOfficerCommitted(world, officerId) ||
                HasActiveInternalAffairsSchedule(world, officerId))
            {
                return false;
            }
        }

        return true;
    }

    internal static OfficerData? GetSingleAvailableOfficer(WorldState world, CityData city, List<int> requestedOfficerIds)
    {
        if (requestedOfficerIds.Count != 1)
        {
            return null;
        }

        var officerId = requestedOfficerIds[0];
        if (!city.OfficerIds.Contains(officerId))
        {
            return null;
        }

        var officer = world.GetOfficer(officerId);
        if (officer == null ||
            IsOfficerAssignedThisMonth(world, officer) ||
            BattleCampaignService.IsOfficerCommitted(world, officerId) ||
            HasActiveInternalAffairsSchedule(world, officerId))
        {
            return null;
        }

        return officer;
    }

    internal static bool IsOfficerAssignedThisMonth(WorldState world, OfficerData officer)
    {
        return OfficerAvailabilityRules.IsTravelingWithLogistics(world, officer.Id) ||
               (officer.LastAssignedYear == world.Year && officer.LastAssignedMonth == world.Month);
    }

    internal static bool HasActiveInternalAffairsSchedule(WorldState world, int officerId)
    {
        return world.InternalAffairsSchedules.Any(schedule =>
            schedule.State == InternalAffairsScheduleState.Active &&
            schedule.OfficerId == officerId);
    }


    internal static void MarkOfficerAssigned(WorldState world, OfficerData officer, CommandType commandType)
    {
        officer.LastAssignedYear = world.Year;
        officer.LastAssignedMonth = world.Month;
        officer.LastAssignedCommand = commandType;
    }

    internal static void MarkOfficersAssigned(WorldState world, List<int> officerIds, CommandType commandType)
    {
        foreach (var officerId in officerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            MarkOfficerAssigned(world, officer, commandType);
        }
    }

}
