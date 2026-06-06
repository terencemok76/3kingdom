using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    public CommandResult ResolveCapturedOfficerDisposition(int actorFactionId, int officerId, CapturedOfficerDisposition disposition)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var officer = world.GetOfficer(officerId);
        if (officer == null)
        {
            world.PendingCapturedOfficerRecords.RemoveAll(record => record.WinnerFactionId == actorFactionId && record.OfficerId == officerId);
            return LocalizedResult(false, "cmd.captured_officer.not_found");
        }

        var pendingRecord = world.PendingCapturedOfficerRecords.FirstOrDefault(record =>
            record.WinnerFactionId == actorFactionId &&
            record.OfficerId == officerId);
        if (pendingRecord == null && officer.CaptiveFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.captured_officer.not_pending");
        }

        if (officer.CaptiveFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.captured_officer.invalid_owner");
        }

        var cityId = pendingRecord?.WinnerCityId ?? officer.JailedCityId;
        var city = world.GetCity(cityId);
        if (city == null || city.OwnerFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.captured_officer.invalid_city");
        }

        CommandResult result = disposition switch
        {
            CapturedOfficerDisposition.Kill => ResolveCapturedOfficerKill(world, city, officer),
            CapturedOfficerDisposition.Recruit => ResolveCapturedOfficerRecruit(world, city, officer),
            CapturedOfficerDisposition.Free => ResolveCapturedOfficerFree(world, city, officer),
            CapturedOfficerDisposition.Jail => ResolveCapturedOfficerJail(world, city, officer),
            _ => LocalizedResult(false, "cmd.captured_officer.invalid_action")
        };

        if (result.Success)
        {
            world.PendingCapturedOfficerRecords.RemoveAll(record =>
                record.WinnerFactionId == actorFactionId &&
                record.OfficerId == officerId);
        }

        return result;
    }

    private List<int> CaptureBattleLoserOfficers(
        WorldState world,
        int winnerFactionId,
        int winnerCityId,
        IEnumerable<int> candidateOfficerIds,
        List<AttackOfficerDeploymentData> deployments,
        TroopAllocationData lossAllocation,
        int totalTroopsBeforeBattle,
        int totalLoss)
    {
        var capturedOfficerIds = new HashSet<int>();
        foreach (var officerId in GetBattleCaptureOfficerIds(candidateOfficerIds, deployments, lossAllocation, totalTroopsBeforeBattle, totalLoss))
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null || officer.DeathYear > 0 && world.Year > officer.DeathYear)
            {
                continue;
            }

            CaptureOfficer(world, officer, winnerFactionId, winnerCityId);
            capturedOfficerIds.Add(officerId);
        }

        return capturedOfficerIds.ToList();
    }

    private void QueueCapturedOfficersForWinner(WorldState world, int winnerFactionId, int winnerCityId, IEnumerable<int> officerIds)
    {
        foreach (var officerId in officerIds.Distinct())
        {
            if (officerId <= 0)
            {
                continue;
            }

            if (world.PendingCapturedOfficerRecords.Any(record => record.WinnerFactionId == winnerFactionId && record.OfficerId == officerId))
            {
                continue;
            }

            world.PendingCapturedOfficerRecords.Add(new WorldState.PendingCapturedOfficerData
            {
                WinnerFactionId = winnerFactionId,
                WinnerCityId = winnerCityId,
                OfficerId = officerId
            });
        }
    }

    private void AutoResolvePendingCapturedOfficers(WorldState world, int winnerFactionId)
    {
        while (true)
        {
            var pendingRecord = world.GetNextPendingCapturedOfficer(winnerFactionId);
            if (pendingRecord == null)
            {
                return;
            }

            var officer = world.GetOfficer(pendingRecord.OfficerId);
            if (officer == null)
            {
                world.PendingCapturedOfficerRecords.Remove(pendingRecord);
                continue;
            }

            var action = ChooseAiCapturedOfficerDisposition(officer);
            var result = ResolveCapturedOfficerDisposition(winnerFactionId, officer.Id, action);
            if (!result.Success)
            {
                world.PendingCapturedOfficerRecords.Remove(pendingRecord);
            }
        }
    }

    private static CapturedOfficerDisposition ChooseAiCapturedOfficerDisposition(OfficerData officer)
    {
        var totalCoreStats = officer.Strength + officer.Intelligence + officer.Charm + officer.Leadership + officer.Politics + officer.Combat;
        if (officer.Loyalty <= 60 || totalCoreStats >= 430)
        {
            return CapturedOfficerDisposition.Recruit;
        }

        return totalCoreStats >= 340
            ? CapturedOfficerDisposition.Jail
            : CapturedOfficerDisposition.Free;
    }

    private static List<int> GetBattleCaptureOfficerIds(
        IEnumerable<int> candidateOfficerIds,
        List<AttackOfficerDeploymentData> deployments,
        TroopAllocationData lossAllocation,
        int totalTroopsBeforeBattle,
        int totalLoss)
    {
        if (totalLoss <= 0)
        {
            return new List<int>();
        }

        if (deployments.Count == 0)
        {
            return totalLoss >= totalTroopsBeforeBattle
                ? candidateOfficerIds.Distinct().ToList()
                : new List<int>();
        }

        var capturedOfficerIds = new HashSet<int>();
        MarkCapturedDeployments(capturedOfficerIds, deployments, lossAllocation.Infantry, TroopType.Infantry);
        MarkCapturedDeployments(capturedOfficerIds, deployments, lossAllocation.Spearman, TroopType.Spearman);
        MarkCapturedDeployments(capturedOfficerIds, deployments, lossAllocation.Cavalry, TroopType.Cavalry);
        MarkCapturedDeployments(capturedOfficerIds, deployments, lossAllocation.Archer, TroopType.Archer);
        MarkCapturedDeployments(capturedOfficerIds, deployments, lossAllocation.Crossbow, TroopType.Crossbow);
        MarkCapturedDeployments(capturedOfficerIds, deployments, lossAllocation.Siege, TroopType.Siege);
        return capturedOfficerIds.ToList();
    }

    private static void MarkCapturedDeployments(
        HashSet<int> capturedOfficerIds,
        List<AttackOfficerDeploymentData> deployments,
        int troopLoss,
        TroopType troopType)
    {
        if (troopLoss <= 0)
        {
            return;
        }

        foreach (var deployment in deployments
                     .Where(item => item.TroopType == troopType && item.TroopCount > 0)
                     .OrderBy(item => item.TroopCount)
                     .ThenBy(item => item.OfficerId))
        {
            if (troopLoss < deployment.TroopCount)
            {
                continue;
            }

            capturedOfficerIds.Add(deployment.OfficerId);
            troopLoss -= deployment.TroopCount;
            if (troopLoss <= 0)
            {
                return;
            }
        }
    }

    private void CaptureOfficer(WorldState world, OfficerData officer, int captorFactionId, int jailedCityId)
    {
        var removalOutcome = RemoveOfficerFromCurrentService(world, officer, clearDeathYear: false);
        officer.CaptiveFactionId = captorFactionId;
        officer.JailedCityId = jailedCityId;
        officer.FreeOfficerStayMonths = 0;

        if (removalOutcome.RemovedFactionId > 0 && removalOutcome.WasRuler)
        {
            ResolveRulerDeath(world, removalOutcome.RemovedFactionId);
        }
    }

    private CommandResult ResolveCapturedOfficerKill(WorldState world, CityData city, OfficerData officer)
    {
        officer.CaptiveFactionId = 0;
        officer.JailedCityId = 0;
        _ = EliminateOfficer(world, officer);

        return LocalizedResult(
            true,
            "cmd.captured_officer.kill",
            new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetCityName(city, GameLanguage.TraditionalChinese) },
            new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetCityName(city, GameLanguage.English) });
    }

    private CommandResult ResolveCapturedOfficerRecruit(WorldState world, CityData city, OfficerData officer)
    {
        var faction = world.GetFaction(city.OwnerFactionId);
        if (faction == null)
        {
            return LocalizedResult(false, "cmd.captured_officer.invalid_owner");
        }

        officer.CaptiveFactionId = 0;
        officer.JailedCityId = 0;
        officer.CityId = city.Id;
        officer.FreeOfficerStayMonths = 0;
        officer.Loyalty = Math.Max(officer.Loyalty, HireOfficerDefaultLoyalty);
        if (!city.OfficerIds.Contains(officer.Id))
        {
            city.OfficerIds.Add(officer.Id);
        }

        if (!faction.OfficerIds.Contains(officer.Id))
        {
            faction.OfficerIds.Add(officer.Id);
        }

        TransferEquippedItemsToFaction(world, officer.Id, faction.Id);

        return LocalizedResult(
            true,
            "cmd.captured_officer.recruit",
            new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetCityName(city, GameLanguage.TraditionalChinese) },
            new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetCityName(city, GameLanguage.English) });
    }

    private CommandResult ResolveCapturedOfficerFree(WorldState world, CityData city, OfficerData officer)
    {
        officer.CaptiveFactionId = 0;
        officer.JailedCityId = 0;
        officer.CityId = city.Id;
        officer.FreeOfficerStayMonths = 2;

        return LocalizedResult(
            true,
            "cmd.captured_officer.free",
            new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetCityName(city, GameLanguage.TraditionalChinese) },
            new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetCityName(city, GameLanguage.English) });
    }

    private CommandResult ResolveCapturedOfficerJail(WorldState world, CityData city, OfficerData officer)
    {
        officer.CaptiveFactionId = city.OwnerFactionId;
        officer.JailedCityId = city.Id;
        officer.CityId = 0;
        officer.FreeOfficerStayMonths = 0;

        return LocalizedResult(
            true,
            "cmd.captured_officer.jail",
            new object[] { GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese), GetCityName(city, GameLanguage.TraditionalChinese) },
            new object[] { GetOfficerDisplayName(officer, GameLanguage.English), GetCityName(city, GameLanguage.English) });
    }

    private (int RemovedCityId, int RemovedFactionId, bool WasRuler) RemoveOfficerFromCurrentService(WorldState world, OfficerData officer, bool clearDeathYear)
    {
        var city = officer.CityId > 0 ? world.GetCity(officer.CityId) : null;
        var removedCityId = city?.Id ?? 0;
        city?.OfficerIds.Remove(officer.Id);

        var faction = world.Factions.FirstOrDefault(item =>
            item.RulerOfficerId == officer.Id ||
            item.OfficerIds.Contains(officer.Id));
        var removedFactionId = faction?.Id ?? 0;
        var wasRuler = faction?.RulerOfficerId == officer.Id;
        faction?.OfficerIds.Remove(officer.Id);

        foreach (var item in world.Items.Where(item => item.EquippedOfficerId == officer.Id))
        {
            if (city != null && city.OwnerFactionId > 0)
            {
                MoveItemToFactionInventory(item, city.OwnerFactionId);
            }
            else
            {
                item.EquippedOfficerId = 0;
                item.OwnerFactionId = 0;
                item.OwnerCityId = 0;
            }
        }

        world.InternalAffairsSchedules.RemoveAll(schedule => schedule.OfficerId == officer.Id);
        world.PendingCommands.RemoveAll(command => command.OfficerIds.Contains(officer.Id));
        world.PendingCapturedOfficerRecords.RemoveAll(record => record.OfficerId == officer.Id);

        officer.CityId = 0;
        officer.FreeOfficerStayMonths = 0;
        if (clearDeathYear)
        {
            officer.DeathYear = world.Year;
        }

        ClearAllOfficerAppointments(officer);

        if (wasRuler && faction != null)
        {
            faction.RulerOfficerId = 0;
        }

        ClearFactionAdvisorPosts(world, officer.Id);
        if (removedCityId > 0)
        {
            var removedCity = world.GetCity(removedCityId);
            if (removedCity != null)
            {
                _ = EnsureCityPrefectAppointment(world, removedCity);
            }
        }

        return (removedCityId, removedFactionId, wasRuler);
    }

    private void AppendCaptureSummaryText(CommandResult result, WorldState world, IReadOnlyList<int> capturedOfficerIds)
    {
        if (capturedOfficerIds.Count == 0 || _localization == null)
        {
            return;
        }

        var capturedNamesZh = capturedOfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null)
            .Select(officer => GetOfficerDisplayName(officer!, GameLanguage.TraditionalChinese))
            .ToList();
        var capturedNamesEn = capturedOfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null)
            .Select(officer => GetOfficerDisplayName(officer!, GameLanguage.English))
            .ToList();
        if (capturedNamesZh.Count == 0 || capturedNamesEn.Count == 0)
        {
            return;
        }

        AppendLocalizedText(
            result,
            _localization.FormatForLanguage(GameLanguage.TraditionalChinese, "cmd.attack.capture_suffix", string.Join("、", capturedNamesZh)),
            _localization.FormatForLanguage(GameLanguage.English, "cmd.attack.capture_suffix", string.Join(", ", capturedNamesEn)));
    }
}
