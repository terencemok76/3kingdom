using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

public static class MapCommandPresentationRules
{
    public static IEnumerable<PendingCommandData> GetFactionCommands(WorldState world, int factionId, int viewerFactionId)
    {
        foreach (var command in world.PendingCommands.Where(command => command.ActorFactionId == factionId &&
                     command.Type is CommandType.Move or CommandType.Attack or CommandType.Spy or CommandType.Diplomacy or CommandType.HireOfficer or CommandType.ContactOfficer))
        {
            if (command.Type == CommandType.Spy && factionId != viewerFactionId) continue;
            yield return command;
        }

        // Covert operations are stored on agent records rather than pending commands.
        // Create presentation-only entries; never add them to the resolution queue.
        if (factionId != viewerFactionId) yield break;
        foreach (var agent in world.CovertOfficerRecords.Where(agent => agent.SponsorFactionId == factionId &&
                     ((agent.DiscordTargetOfficerId > 0 && agent.DiscordScheduledMonth > 0) || agent.CitySurrenderScheduledMonth > 0)))
        {
            var cityId = agent.CitySurrenderScheduledMonth > 0 && agent.CitySurrenderCityId > 0
                ? agent.CitySurrenderCityId : world.GetOfficer(agent.OfficerId)?.CityId ?? 0;
            if (world.GetCity(cityId) == null) continue;
            yield return new PendingCommandData { Type = CommandType.Spy, ActorFactionId = factionId, SourceCityId = cityId, TargetCityId = cityId };
        }
    }

    public static bool TryGetMission(WorldState world, PendingCommandData command, out int targetCityId, out MapMovementIcon icon)
    {
        targetCityId = 0;
        icon = default;
        switch (command.Type)
        {
            case CommandType.Spy:
                icon = MapMovementIcon.Spy;
                targetCityId = command.TargetCityId;
                break;
            case CommandType.Diplomacy:
                icon = MapMovementIcon.Diplomacy;
                targetCityId = MainCityRules.FindMainCity(world, command.TargetFactionId)?.Id ?? 0;
                break;
            case CommandType.HireOfficer:
                icon = MapMovementIcon.RecruitOfficer;
                targetCityId = world.GetOfficer(command.TargetOfficerId)?.CityId ?? 0;
                break;
            case CommandType.ContactOfficer:
                icon = MapMovementIcon.ContactOfficer;
                targetCityId = command.TargetCityId;
                break;
            default:
                return false;
        }
        return world.GetCity(command.SourceCityId) != null && world.GetCity(targetCityId) != null;
    }
}
