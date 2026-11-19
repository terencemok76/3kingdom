using System;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class CampaignPostFieldDecisionRules
{
    public static PostFieldBattleDecision ChooseAiDecision(WorldState world, ActiveBattleCampaignData campaign, int monthlyDayLimit = BattleCampaignService.MaximumBattleDaysPerMonth)
    {
        var attackers = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Attacker && team.OfficerId > 0 &&
            team.Location is CampaignTeamLocation.Field or CampaignTeamLocation.Reserve && team.ActiveTroops > 0).ToList();
        var defenders = campaign.Teams.Where(team => team.Side == CampaignBattleSide.Defender && team.OfficerId > 0 &&
            team.Location == CampaignTeamLocation.InnerCity && team.ActiveTroops > 0).ToList();
        var attackingTroops = attackers.Sum(team => (long)team.ActiveTroops);
        var defendingTroops = defenders.Sum(team => (long)team.ActiveTroops);
        if (attackingTroops == 0) return PostFieldBattleDecision.Withdraw;

        // Battle upkeep uses five food per 100 active soldiers per day.
        var dailyFood = Math.Max(1L, (attackingTroops * 5 + 99) / 100);
        var source = world.GetCity(campaign.SourceCityId);
        var sourceThreatened = source != null && (source.OwnerFactionId != campaign.AttackerFactionId ||
            world.ActiveBattleCampaigns.Any(other => other != campaign && other.TargetCityId == source.Id &&
                other.AttackerFactionId != campaign.AttackerFactionId && other.Stage != CampaignStage.Resolved));
        if (campaign.AttackerFood < dailyFood || attackingTroops * 4 < defendingTroops ||
            (sourceThreatened && attackingTroops < defendingTroops * 2))
            return PostFieldBattleDecision.Withdraw;

        if (BattleCampaignService.HasReachedMonthlyBattleLimit(campaign, monthlyDayLimit))
            return PostFieldBattleDecision.PrepareNextMonthSiege;

        var attackerMorale = attackers.Sum(team => (long)team.ActiveTroops * Math.Clamp(team.Morale, 0, 100)) / attackingTroops;
        var defenderMorale = defendingTroops == 0 ? 0 : defenders.Sum(team => (long)team.ActiveTroops * Math.Clamp(team.Morale, 0, 100)) / defendingTroops;
        var arrivingAttackers = campaign.Reinforcements.Where(order => order.Side == CampaignBattleSide.Attacker &&
            order.Status == ReinforcementStatus.Traveling && order.RemainingBattleDays <= 10)
            .SelectMany(order => order.Teams).Sum(team => (long)Math.Max(0, team.ActiveTroops));
        var arrivingDefenders = campaign.Reinforcements.Where(order => order.Side == CampaignBattleSide.Defender &&
            order.Status == ReinforcementStatus.Traveling && order.RemainingBattleDays <= 10)
            .SelectMany(order => order.Teams).Sum(team => (long)Math.Max(0, team.ActiveTroops));
        var commander = attackers.Select(team => world.GetOfficer(team.OfficerId)).Where(officer => officer != null)
            .OrderByDescending(officer => officer!.Leadership).FirstOrDefault();
        var combat = Math.Clamp(commander?.Combat ?? 50, 0, 100);
        var intelligence = Math.Clamp(commander?.Intelligence ?? 50, 0, 100);
        var cityDefense = Math.Clamp(world.GetCity(campaign.TargetCityId)?.Defense ?? 0, 0, 100);
        var hasSiegeEngine = campaign.Teams.Any(team => team.Side == CampaignBattleSide.Attacker &&
            team.ActiveTroops > 0 && team.SiegeEngineType is SiegeEngineType.Ram or SiegeEngineType.Ladder or SiegeEngineType.Catapult &&
            team.Location is CampaignTeamLocation.Field or CampaignTeamLocation.Reserve);
        var requiredRatioPercent = 150 + cityDefense / 3 - (combat - 50) / 5 - (hasSiegeEngine ? 10 : 0);
        var enemyReliefUrgent = arrivingDefenders * (100 + intelligence) > arrivingAttackers * (100 + intelligence) + defendingTroops * 50;

        if (campaign.AttackerFood >= dailyFood * 3 && attackingTroops * 100 >= defendingTroops * requiredRatioPercent &&
            (attackerMorale >= defenderMorale || attackingTroops >= defendingTroops * 2 || enemyReliefUrgent))
            return PostFieldBattleDecision.ImmediateAssault;
        return PostFieldBattleDecision.PrepareNextMonthSiege;
    }
}
