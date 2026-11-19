using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class CampaignReorganizationRules
{
    public static int GetOfficerCapacity(OfficerData officer)
    {
        var bonus = OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.GrandGeneral) ? 1500 :
            OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.VanguardGeneral) || OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.RearGeneral) ? 500 : 0;
        return 1000 + Math.Clamp(officer.Leadership, 0, 100) * 40 + bonus;
    }

    public static void InitializeCapacities(WorldState world, ActiveBattleCampaignData campaign)
    {
        SynchronizeReinforcementTeamReferences(campaign);
        foreach (var team in campaign.Teams.Concat(campaign.Reinforcements.SelectMany(order => order.Teams)).Distinct())
            if (team.OfficerId > 0 && world.GetOfficer(team.OfficerId) is { } officer)
                team.ReorganizationCapacity = GetOfficerCapacity(officer);
    }

    public static int GetCapacity(CampaignBattleTeamData team) => team.ReorganizationCapacity > 0 ? team.ReorganizationCapacity : team.MaximumTroops;
    public static void SynchronizeReinforcementTeamReferences(ActiveBattleCampaignData campaign)
    {
        foreach (var order in campaign.Reinforcements)
        {
            for (var index = order.Teams.Count - 1; index >= 0; index--)
            {
                var team = order.Teams[index];
                // Older saves may retain a second serialized copy of a defector
                // in the former faction's reinforcement order.
                if (team.ReinforcementOrderId == 0 && team.OfficerId > 0 && team.FactionId != order.FactionId &&
                    campaign.Teams.Any(item => item.ReinforcementOrderId == 0 && item.Id == team.Id &&
                        item.OfficerId == team.OfficerId && item.FactionId == team.FactionId && item.Side == team.Side))
                {
                    order.Teams.RemoveAt(index);
                    continue;
                }
                var existing = campaign.Teams.FirstOrDefault(item => item.ReinforcementOrderId == order.Id &&
                    item.Id == team.Id && item.OfficerId == team.OfficerId && item.Side == team.Side);
                if (existing != null) order.Teams[index] = existing;
            }
        }

        // Campaigns saved before OriginCityId existed still need an origin
        // before a cross-city transfer creates their first troop ledger.
        foreach (var team in campaign.Teams)
        {
            var order = campaign.Reinforcements.FirstOrDefault(item => team.ReinforcementOrderId > 0 && item.Id == team.ReinforcementOrderId);
            InitializeLegacyOrigin(campaign, team, order);
        }
        foreach (var order in campaign.Reinforcements)
            foreach (var team in order.Teams)
                InitializeLegacyOrigin(campaign, team, order);
    }

    private static void InitializeLegacyOrigin(ActiveBattleCampaignData campaign, CampaignBattleTeamData team, ReinforcementOrderData? order)
    {
        if (team.OriginCityId > 0) return;
        team.OriginCityId = order?.SourceCityId > 0 ? order.SourceCityId :
            team.Side == CampaignBattleSide.Attacker ? campaign.SourceCityId : campaign.TargetCityId;
    }

    public static List<CampaignBattleTeamData> GetEligibleTeams(ActiveBattleCampaignData campaign, int factionId) =>
        GetEligibleTeams(campaign, factionId, CampaignControllerType.Player);

    internal static List<CampaignBattleTeamData> GetEligibleTeams(ActiveBattleCampaignData campaign, int factionId, CampaignControllerType controller)
    {
        SynchronizeReinforcementTeamReferences(campaign);
        var teams = campaign.Teams.Where(team => IsEligible(team, factionId, controller)).ToList();
        foreach (var order in campaign.Reinforcements.Where(order => order.Status is ReinforcementStatus.Arrived or ReinforcementStatus.Reserve))
        {
            foreach (var team in order.Teams.Where(team => team.Location == CampaignTeamLocation.Reserve && IsEligible(team, factionId, controller)))
                if (!teams.Contains(team)) teams.Add(team);
        }
        return teams;
    }

    public static bool IsEligible(CampaignBattleTeamData team, int factionId) => IsEligible(team, factionId, CampaignControllerType.Player);

    private static bool IsEligible(CampaignBattleTeamData team, int factionId, CampaignControllerType controller) =>
        team.FactionId == factionId && team.ControllerType == controller &&
        team.OfficerId > 0 && team.EquipmentType == BattleEquipmentType.None && team.SiegeEngineType == SiegeEngineType.None &&
        (team.RetreatDestinationCityId <= 0 || (team.Side == CampaignBattleSide.Defender && team.Location == CampaignTeamLocation.InnerCity)) &&
        team.Location is CampaignTeamLocation.Field or CampaignTeamLocation.InnerCity or CampaignTeamLocation.Reserve;

    public static bool TryTransfer(ActiveBattleCampaignData campaign, int factionId, int fromId, int toId, int active, int wounded, out string error)
    {
        var source = campaign.Teams.FirstOrDefault(team => team.Id == fromId);
        var target = campaign.Teams.FirstOrDefault(team => team.Id == toId);
        return TryTransfer(campaign, factionId, source, target, active, wounded, out error);
    }

    public static bool TryTransfer(ActiveBattleCampaignData campaign, int factionId, CampaignBattleTeamData? source, CampaignBattleTeamData? target, int active, int wounded, out string error)
        => TryTransfer(campaign, factionId, source, target, active, wounded, CampaignControllerType.Player, out error);

    internal static bool TryTransfer(ActiveBattleCampaignData campaign, int factionId, CampaignBattleTeamData? source, CampaignBattleTeamData? target, int active, int wounded, CampaignControllerType controller, out string error)
    {
        error = string.Empty;
        var eligible = GetEligibleTeams(campaign, factionId, controller);
        if (source == null || target == null || source == target || !eligible.Contains(source) || !eligible.Contains(target) ||
            source.Side != target.Side || source.TroopType != target.TroopType)
        {
            error = "incompatible";
            return false;
        }
        if (active < 0 || wounded < 0 || (active == 0 && wounded == 0) || active > source.ActiveTroops || wounded > source.WoundedTroops ||
            (long)target.ActiveTroops + target.WoundedTroops + active + wounded > GetCapacity(target) ||
            (long)active + wounded > source.MaximumTroops)
        {
            error = "amount";
            return false;
        }

        var oldTarget = (long)target.ActiveTroops + target.WoundedTroops;
        var transferred = (long)active + wounded;
        if (source.ReorganizationCapacity <= 0) source.ReorganizationCapacity = source.MaximumTroops;
        if (target.ReorganizationCapacity <= 0) target.ReorganizationCapacity = target.MaximumTroops;
        target.Morale = (int)((oldTarget * target.Morale + transferred * source.Morale) / (oldTarget + transferred));
        ResetBattleStatus(source);
        ResetBattleStatus(target);
        CampaignTroopOriginRules.Transfer(source, target, active, wounded);
        source.ActiveTroops -= active;
        source.WoundedTroops -= wounded;
        target.ActiveTroops += active;
        target.WoundedTroops += wounded;
        source.MaximumTroops -= active + wounded;
        target.MaximumTroops += active + wounded;
        if (source.ActiveTroops == 0)
        {
            if (source.Side == CampaignBattleSide.Defender && source.Location == CampaignTeamLocation.InnerCity &&
                source.RetreatDestinationCityId == campaign.TargetCityId)
                source.RetreatDestinationCityId = 0;
            source.Location = CampaignTeamLocation.Reserve;
        }
        return true;
    }

    public static void ReorganizeAiTeams(ActiveBattleCampaignData campaign, int playerFactionId)
    {
        ReorganizeAllies(campaign, playerFactionId, CampaignBattleSide.Attacker);
        ReorganizeAllies(campaign, playerFactionId, CampaignBattleSide.Defender);
    }

    // AI factions concentrate weak teams only when the complete active force fits.
    // Each faction and troop type remains independent; wounded stay with their team.
    public static void ReorganizeAllies(ActiveBattleCampaignData campaign, int playerFactionId, CampaignBattleSide playerSide)
    {
        SynchronizeReinforcementTeamReferences(campaign);
        var factions = campaign.Teams.Concat(campaign.Reinforcements
                .Where(order => order.Status is ReinforcementStatus.Arrived or ReinforcementStatus.Reserve)
                .SelectMany(order => order.Teams))
            .Where(team => team.Side == playerSide && team.FactionId > 0 && team.FactionId != playerFactionId &&
                           team.ControllerType == CampaignControllerType.Ai)
            .Select(team => team.FactionId).Distinct().OrderBy(id => id).ToList();
        foreach (var factionId in factions)
        {
            var groups = GetEligibleTeams(campaign, factionId, CampaignControllerType.Ai)
                .Where(team => team.Side == playerSide && team.ActiveTroops > 0)
                .GroupBy(team => team.TroopType).ToList();
            foreach (var group in groups)
            {
                var teams = group.OrderBy(team => team.Id).ThenBy(team => team.OfficerId).ToList();
                foreach (var source in teams.OrderBy(team => team.ActiveTroops).ThenBy(team => team.Id).ToList())
                {
                    if (source.ActiveTroops <= 0) continue;
                    var target = teams.Where(team => team != source && team.ActiveTroops >= source.ActiveTroops &&
                            (long)GetCapacity(team) - team.ActiveTroops - team.WoundedTroops >= source.ActiveTroops)
                        .OrderByDescending(team => team.ActiveTroops).ThenBy(team => team.Id).ThenBy(team => team.OfficerId).FirstOrDefault();
                    if (target != null)
                        TryTransfer(campaign, factionId, source, target, source.ActiveTroops, 0, CampaignControllerType.Ai, out _);
                }
            }
        }
    }

    private static void ResetBattleStatus(CampaignBattleTeamData team)
    {
        team.MessTurns = 0;
        // Null selects the battle unit's normal initial energy and ammunition on deployment.
        team.Energy = null;
        team.WeaponAmmo = null;
        team.MaxWeaponAmmo = null;
    }
}
