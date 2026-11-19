using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

// The combat model tracks casualties by team, not by individual soldier. When
// a mixed team changes strength, consume origins in city-id order consistently.
public static class CampaignTroopOriginRules
{
    public static void EnsureInitialized(CampaignBattleTeamData team)
    {
        team.TroopOrigins ??= new();
        if (team.TroopOrigins.Count != 0) return;
        if (team.ActiveTroops > 0)
            team.TroopOrigins.Add(new CampaignTroopOriginData { CityId = team.OriginCityId, ActiveTroops = team.ActiveTroops });
        if (team.WoundedTroops > 0)
            Get(team, team.WoundedOriginCityId > 0 ? team.WoundedOriginCityId : team.OriginCityId).WoundedTroops += team.WoundedTroops;
    }

    private static CampaignTroopOriginData Get(CampaignBattleTeamData team, int cityId)
    {
        var origin = team.TroopOrigins.FirstOrDefault(item => item.CityId == cityId);
        if (origin != null) return origin;
        origin = new CampaignTroopOriginData { CityId = cityId };
        team.TroopOrigins.Add(origin);
        return origin;
    }

    private static List<(CampaignTroopOriginData Origin, int Active, int Wounded)> AllocateTransfer(CampaignBattleTeamData source, int active, int wounded)
    {
        EnsureInitialized(source);
        var allocation = new List<(CampaignTroopOriginData, int, int)>();
        active = Math.Max(0, active);
        wounded = Math.Max(0, wounded);
        foreach (var item in source.TroopOrigins.OrderBy(item => item.CityId).ToList())
        {
            var a = Math.Min(active, item.ActiveTroops);
            var w = Math.Min(wounded, item.WoundedTroops);
            if (a == 0 && w == 0) continue;
            allocation.Add((item, a, w));
            active -= a; wounded -= w;
        }
        return allocation;
    }

    public static List<CampaignTroopOriginData> PreviewTransfer(CampaignBattleTeamData source, int active, int wounded) =>
        AllocateTransfer(source, active, wounded).Select(item => new CampaignTroopOriginData
        {
            CityId = item.Origin.CityId, ActiveTroops = item.Active, WoundedTroops = item.Wounded
        }).ToList();

    public static void Transfer(CampaignBattleTeamData source, CampaignBattleTeamData target, int active, int wounded)
    {
        var allocation = AllocateTransfer(source, active, wounded);
        EnsureInitialized(target);
        foreach (var (item, a, w) in allocation)
        {
            var destination = Get(target, item.CityId);
            item.ActiveTroops -= a; item.WoundedTroops -= w;
            destination.ActiveTroops += a; destination.WoundedTroops += w;
        }
        source.TroopOrigins.RemoveAll(item => item.ActiveTroops == 0 && item.WoundedTroops == 0);
        UpdateLegacyOrigin(source); UpdateLegacyOrigin(target);
    }

    public static void ApplyCounts(CampaignBattleTeamData team, int active, int wounded)
    {
        EnsureInitialized(team);
        active = Math.Max(0, active); wounded = Math.Max(0, wounded);
        var oldActive = team.TroopOrigins.Sum(item => item.ActiveTroops);
        var oldWounded = team.TroopOrigins.Sum(item => item.WoundedTroops);
        var newlyWounded = Math.Min(Math.Max(0, oldActive - active), Math.Max(0, wounded - oldWounded));
        var recovered = Math.Min(Math.Max(0, oldWounded - wounded), Math.Max(0, active - oldActive));
        foreach (var item in team.TroopOrigins.OrderBy(item => item.CityId))
        {
            var n = Math.Min(newlyWounded, item.ActiveTroops);
            item.ActiveTroops -= n; item.WoundedTroops += n; newlyWounded -= n;
            n = Math.Min(recovered, item.WoundedTroops);
            item.WoundedTroops -= n; item.ActiveTroops += n; recovered -= n;
        }
        var removeActive = Math.Max(0, team.TroopOrigins.Sum(item => item.ActiveTroops) - active);
        var removeWounded = Math.Max(0, team.TroopOrigins.Sum(item => item.WoundedTroops) - wounded);
        foreach (var item in team.TroopOrigins.OrderBy(item => item.CityId))
        {
            var n = Math.Min(removeActive, item.ActiveTroops); item.ActiveTroops -= n; removeActive -= n;
            n = Math.Min(removeWounded, item.WoundedTroops); item.WoundedTroops -= n; removeWounded -= n;
        }
        var missingActive = active - team.TroopOrigins.Sum(item => item.ActiveTroops);
        var missingWounded = wounded - team.TroopOrigins.Sum(item => item.WoundedTroops);
        if (missingActive > 0 || missingWounded > 0)
        {
            var origin = Get(team, team.OriginCityId);
            origin.ActiveTroops += Math.Max(0, missingActive); origin.WoundedTroops += Math.Max(0, missingWounded);
        }
        team.TroopOrigins.RemoveAll(item => item.ActiveTroops == 0 && item.WoundedTroops == 0);
        team.ActiveTroops = active; team.WoundedTroops = wounded;
        UpdateLegacyOrigin(team);
    }

    private static void UpdateLegacyOrigin(CampaignBattleTeamData team)
    {
        var woundedOrigins = team.TroopOrigins.Where(item => item.WoundedTroops > 0).ToList();
        team.WoundedOriginCityId = woundedOrigins.Count == 1 ? woundedOrigins[0].CityId : 0;
    }
}
