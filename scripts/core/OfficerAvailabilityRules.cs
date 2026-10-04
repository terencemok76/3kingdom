using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class OfficerAvailabilityRules
{
    public static bool IsTravelingWithLogistics(WorldState world, int officerId) =>
        world.PendingCommands.Any(command =>
            command.Type == CommandType.Move &&
            command.IsTraveling &&
            command.OfficerIds.Contains(officerId));
}
