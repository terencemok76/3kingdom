using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal static class SpyLogVisibilityRules
{
    internal static bool IsForeignSecretAction(PendingCommandData command, int playerFactionId) =>
        playerFactionId > 0 && command.ActorFactionId != playerFactionId &&
        command.Type is CommandType.Spy or CommandType.ContactOfficer;

    internal static string GetVisibleEffectKey(PendingCommandData command, CommandResult result, bool targetsPlayer)
    {
        if (!targetsPlayer || command.Type != CommandType.Spy || !result.Success) return string.Empty;
        return command.SpyActionType switch
        {
            SpyActionType.Sabotage => "log.foreign_spy_damage",
            SpyActionType.Incite => "log.foreign_spy_unrest",
            SpyActionType.SowDiscord => "log.foreign_spy_loyalty",
            SpyActionType.Assassination => "log.foreign_spy_death",
            _ => string.Empty
        };
    }
}
