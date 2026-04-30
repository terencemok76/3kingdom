using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    private CommandResult LocalizedResult(bool success, string key, object[]? args = null)
    {
        return LocalizedResult(success, key, args, args);
    }

    private CommandResult LocalizedResult(bool success, string key, object[]? zhArgs, object[]? enArgs)
    {
        var traditionalArgs = zhArgs ?? Array.Empty<object>();
        var englishArgs = enArgs ?? Array.Empty<object>();
        var zh = _localization?.FormatForLanguage(GameLanguage.TraditionalChinese, key, traditionalArgs) ?? key;
        var en = _localization?.FormatForLanguage(GameLanguage.English, key, englishArgs) ?? key;

        return new CommandResult
        {
            Success = success,
            Message = en,
            MessageZhHant = zh,
            MessageEn = en
        };
    }

    private object[] GetCityArgs(CityData city, GameLanguage language)
    {
        return new object[]
        {
            GetCityName(city, language)
        };
    }

    private object[] GetOfficerArgs(OfficerData officer, GameLanguage language)
    {
        return new object[]
        {
            GetOfficerDisplayName(officer, language)
        };
    }

    private static int GetAverageStat(WorldState world, CityData city, Func<OfficerData, int> selector)
    {
        var count = 0;
        var total = 0;
        foreach (var officerId in city.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            total += selector(officer);
            count += 1;
        }

        return count == 0 ? 50 : total / count;
    }

    private static int GetTransferAmount(int requestedAmount, int availableAmount)
    {
        var transferAmount = requestedAmount > 0 ? requestedAmount : availableAmount / 2;
        if (transferAmount > availableAmount)
        {
            transferAmount = availableAmount;
        }

        return transferAmount < 0 ? 0 : transferAmount;
    }

    private static List<int> GetMovableOfficerIds(CityData sourceCity, List<int> requestedOfficerIds)
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

    private static bool AreOfficerIdsAvailableForPendingOrder(
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
            if (officer == null || IsOfficerAssignedThisMonth(world, officer))
            {
                return false;
            }
        }

        return true;
    }

    private static OfficerData? GetSingleAvailableOfficer(WorldState world, CityData city, List<int> requestedOfficerIds)
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
        if (officer == null || IsOfficerAssignedThisMonth(world, officer))
        {
            return null;
        }

        return officer;
    }

    private static bool IsOfficerAssignedThisMonth(WorldState world, OfficerData officer)
    {
        return officer.LastAssignedYear == world.Year && officer.LastAssignedMonth == world.Month;
    }

    private static bool HasActiveInternalAffairsSchedule(WorldState world, int officerId)
    {
        return world.InternalAffairsSchedules.Any(schedule =>
            schedule.State == InternalAffairsScheduleState.Active &&
            schedule.OfficerId == officerId);
    }

    private static bool HasActiveInternalAffairsJob(WorldState world, int cityId, InternalAffairsJobType jobType)
    {
        return world.InternalAffairsSchedules.Any(schedule =>
            schedule.State == InternalAffairsScheduleState.Active &&
            schedule.CityId == cityId &&
            schedule.JobType == jobType);
    }

    private static bool IsFactionRuler(WorldState world, int officerId)
    {
        return world.Factions.Any(faction => faction.RulerOfficerId == officerId);
    }

    private static bool IsAssignableRole(string role)
    {
        return role.Equals("General", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Strategist", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Advisor", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Governor", StringComparison.OrdinalIgnoreCase);
    }

    private string GetOfficerRoleName(string role, GameLanguage language)
    {
        var key = role.ToLowerInvariant() switch
        {
            "general" => "role.general",
            "strategist" => "role.strategist",
            "advisor" => "role.advisor",
            "governor" => "role.governor",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(key)
            ? role
            : _localization?.FormatForLanguage(language, key) ?? role;
    }

    private static int GetNextInternalAffairsScheduleId(WorldState world)
    {
        return world.InternalAffairsSchedules.Count == 0
            ? 1
            : world.InternalAffairsSchedules.Max(schedule => schedule.Id) + 1;
    }

    private static (int Farm, int Commercial, int Defense, int Loyalty) ApplyInternalAffairsJob(
        CityData city,
        OfficerData officer,
        InternalAffairsJobType jobType)
    {
        var officerBonus = Math.Max(0, (officer.Intelligence + officer.Politics + officer.Charm) / 90);
        var primaryGain = 2 + officerBonus;
        var secondaryGain = 1;
        var gains = jobType switch
        {
            InternalAffairsJobType.Farm => (primaryGain, 0, 0, 0),
            InternalAffairsJobType.Commercial => (0, primaryGain, 0, 0),
            InternalAffairsJobType.Defend => (0, 0, primaryGain, 0),
            InternalAffairsJobType.WaterControl => (secondaryGain, 0, 0, secondaryGain),
            InternalAffairsJobType.Construction => (0, secondaryGain, secondaryGain, 0),
            _ => (0, 0, 0, 0)
        };

        city.Farm = ClampStat(city.Farm + gains.Item1);
        city.Commercial = ClampStat(city.Commercial + gains.Item2);
        city.Defense = ClampStat(city.Defense + gains.Item3);
        city.Loyalty = ClampStat(city.Loyalty + gains.Item4);
        return gains;
    }

    private string GetInternalAffairsJobName(InternalAffairsJobType jobType, GameLanguage language)
    {
        var key = jobType switch
        {
            InternalAffairsJobType.Farm => "internal_affairs.job.farm",
            InternalAffairsJobType.Commercial => "internal_affairs.job.commercial",
            InternalAffairsJobType.Defend => "internal_affairs.job.defend",
            InternalAffairsJobType.WaterControl => "internal_affairs.job.water_control",
            InternalAffairsJobType.Construction => "internal_affairs.job.construction",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(key)
            ? jobType.ToString()
            : _localization?.FormatForLanguage(language, key) ?? jobType.ToString();
    }

    private static void MarkOfficerAssigned(WorldState world, OfficerData officer, CommandType commandType)
    {
        officer.LastAssignedYear = world.Year;
        officer.LastAssignedMonth = world.Month;
        officer.LastAssignedCommand = commandType;
    }

    private static void MarkOfficersAssigned(WorldState world, List<int> officerIds, CommandType commandType)
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

    private static int TransferOfficers(
        WorldState world,
        CityData sourceCity,
        CityData targetCity,
        List<int> requestedOfficerIds)
    {
        var movedOfficerCount = 0;
        foreach (var officerId in requestedOfficerIds)
        {
            if (!sourceCity.OfficerIds.Contains(officerId))
            {
                continue;
            }

            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            sourceCity.OfficerIds.Remove(officerId);
            if (!targetCity.OfficerIds.Contains(officerId))
            {
                targetCity.OfficerIds.Add(officerId);
            }

            officer.CityId = targetCity.Id;
            movedOfficerCount += 1;
        }

        return movedOfficerCount;
    }

    private OfficerData? TryFindDiscoverableOfficer(WorldState world, int factionId)
    {
        var candidates = new List<OfficerData>();
        foreach (var officer in world.Officers)
        {
            if (officer.CityId > 0)
            {
                continue;
            }

            if (!IsOfficerOldEnoughToJoin(world, officer))
            {
                continue;
            }

            candidates.Add(officer);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var preferred = new List<OfficerData>();
        foreach (var officer in candidates)
        {
            if (MatchesFaction(officer.Belongs, factionId))
            {
                preferred.Add(officer);
            }
        }

        var pool = preferred.Count > 0 ? preferred : candidates;
        return pool[_random.Next(pool.Count)];
    }

    private static bool MatchesFaction(string belongs, int factionId)
    {
        return factionId switch
        {
            1 => belongs.Equals("Shu", StringComparison.OrdinalIgnoreCase),
            2 => belongs.Equals("Wei", StringComparison.OrdinalIgnoreCase),
            3 => belongs.Equals("Wu", StringComparison.OrdinalIgnoreCase),
            4 => belongs.Equals("YellowTurban", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool IsOfficerOldEnoughToJoin(WorldState world, OfficerData officer)
    {
        if (officer.BirthYear <= 0)
        {
            return true;
        }

        return world.Year - officer.BirthYear >= 18;
    }

    private static string GetCityName(CityData city, GameLanguage language)
    {
        if (language == GameLanguage.TraditionalChinese)
        {
            if (!string.IsNullOrWhiteSpace(city.NameZhHant))
            {
                return city.NameZhHant;
            }

            if (!string.IsNullOrWhiteSpace(city.Name))
            {
                return city.Name;
            }

            return city.NameEn;
        }

        if (!string.IsNullOrWhiteSpace(city.NameEn))
        {
            return city.NameEn;
        }

        if (!string.IsNullOrWhiteSpace(city.Name))
        {
            return city.Name;
        }

        return city.NameZhHant;
    }

    private static string GetOfficerDisplayName(OfficerData officer, GameLanguage language)
    {
        if (language == GameLanguage.TraditionalChinese)
        {
            if (!string.IsNullOrWhiteSpace(officer.NameZhHant))
            {
                return officer.NameZhHant;
            }

            return officer.Name;
        }

        return !string.IsNullOrWhiteSpace(officer.Name) ? officer.Name : officer.NameZhHant;
    }

    private static string GetRulerDisplayName(WorldState world, int factionId, GameLanguage language)
    {
        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return factionId > 0 ? factionId.ToString() : "-";
        }

        var ruler = world.GetOfficer(faction.RulerOfficerId);
        if (ruler != null)
        {
            return GetOfficerDisplayName(ruler, language);
        }

        if (language == GameLanguage.TraditionalChinese && !string.IsNullOrWhiteSpace(faction.NameZhHant))
        {
            return faction.NameZhHant;
        }

        return !string.IsNullOrWhiteSpace(faction.NameEn) ? faction.NameEn : faction.NameZhHant;
    }

    private static void ResolveCapturedCityOfficers(WorldState world, CityData capturedCity, int previousFactionId)
    {
        if (capturedCity.OfficerIds.Count == 0)
        {
            return;
        }

        var retreatCity = FindRetreatCity(world, previousFactionId, capturedCity.Id);
        var displacedOfficerIds = new List<int>(capturedCity.OfficerIds);
        capturedCity.OfficerIds.Clear();

        foreach (var officerId in displacedOfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (retreatCity != null)
            {
                officer.CityId = retreatCity.Id;
                if (!retreatCity.OfficerIds.Contains(officer.Id))
                {
                    retreatCity.OfficerIds.Add(officer.Id);
                }
            }
            else
            {
                officer.CityId = 0;
            }
        }
    }

    private static CityData? FindRetreatCity(WorldState world, int factionId, int excludedCityId)
    {
        foreach (var city in world.Cities)
        {
            if (city.Id != excludedCityId && city.OwnerFactionId == factionId)
            {
                return city;
            }
        }

        return null;
    }

    private static bool HasUsedDevelop(WorldState world, CityData city)
    {
        return city.LastDevelopYear == world.Year && city.LastDevelopMonth == world.Month;
    }

    private static void MarkDevelopUsed(WorldState world, CityData city)
    {
        city.LastDevelopYear = world.Year;
        city.LastDevelopMonth = world.Month;
    }

    private static bool HasUsedRecruit(WorldState world, CityData city)
    {
        return city.LastRecruitYear == world.Year && city.LastRecruitMonth == world.Month;
    }

    private static void MarkRecruitUsed(WorldState world, CityData city)
    {
        city.LastRecruitYear = world.Year;
        city.LastRecruitMonth = world.Month;
    }

    private static bool HasUsedSearch(WorldState world, CityData city)
    {
        return city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month;
    }

    private static void MarkSearchUsed(WorldState world, CityData city)
    {
        city.LastSearchYear = world.Year;
        city.LastSearchMonth = world.Month;
    }

    private static void UpsertPendingCommand(WorldState world, PendingCommandData pendingCommand)
    {
        // Military orders can stack per source city; core city actions stay one-pending-per-type.
        if (pendingCommand.Type == CommandType.Move || pendingCommand.Type == CommandType.Attack)
        {
            world.PendingCommands.Add(pendingCommand);
            return;
        }

        world.PendingCommands.RemoveAll(existing =>
            existing.SourceCityId == pendingCommand.SourceCityId &&
            existing.Type == pendingCommand.Type);
        world.PendingCommands.Add(pendingCommand);
    }

    private static bool IsConnected(CityData source, int targetCityId)
    {
        return source.ConnectedCityIds.Contains(targetCityId);
    }

    private static int ClampStat(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value > 100)
        {
            return 100;
        }

        return value;
    }

}
