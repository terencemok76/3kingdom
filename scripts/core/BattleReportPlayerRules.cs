using System.Linq;
using System.Collections.Generic;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class BattleReportPlayerRules
{
    public static WorldState.BattleReportData? GetNextPending(IEnumerable<WorldState.BattleReportData> reports, int factionId) =>
        reports.FirstOrDefault(report => !report.PlayerAcknowledged && IsPlayerInvolved(report, factionId));

    public static bool IsPlayerInvolved(WorldState.BattleReportData report, int factionId) =>
        factionId > 0 && (report.AttackerFactionId == factionId || report.DefenderFactionId == factionId ||
            report.ParticipantContributions.Any(item => item.FactionId == factionId));

    public static bool? DidPlayerWin(WorldState.BattleReportData report, int factionId)
    {
        if (factionId <= 0 || report.IncompleteSettlement ||
            (report.WinnerFactionId != report.AttackerFactionId && report.WinnerFactionId != report.DefenderFactionId) ||
            report.WinnerFactionId <= 0)
            return null;

        if (report.AttackerFactionId == factionId || report.DefenderFactionId == factionId)
            return report.WinnerFactionId == factionId;

        var participant = report.ParticipantContributions.FirstOrDefault(item => item.FactionId == factionId);
        if (participant == null) return null;
        return report.WinnerFactionId == (participant.Side == CampaignBattleSide.Attacker
            ? report.AttackerFactionId : report.DefenderFactionId);
    }
}
