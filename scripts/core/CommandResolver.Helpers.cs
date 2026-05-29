using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    private sealed class PrefectAutoAppointmentOutcome
    {
        public required CityData City { get; init; }
        public OfficerData? NewPrefect { get; init; }
        public bool IsVacant => NewPrefect == null;
    }

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

    private void AppendLocalizedText(CommandResult result, string zhSuffix, string enSuffix)
    {
        if (!string.IsNullOrWhiteSpace(zhSuffix))
        {
            result.MessageZhHant = $"{result.MessageZhHant} {zhSuffix}".Trim();
        }

        if (!string.IsNullOrWhiteSpace(enSuffix))
        {
            result.MessageEn = $"{result.MessageEn} {enSuffix}".Trim();
            result.Message = result.MessageEn;
        }
    }

    private void AppendPrefectAutoAppointmentOutcome(CommandResult result, PrefectAutoAppointmentOutcome? outcome)
    {
        if (outcome == null)
        {
            return;
        }

        AppendLocalizedText(
            result,
            GetPrefectAutoAppointmentMessage(outcome, GameLanguage.TraditionalChinese),
            GetPrefectAutoAppointmentMessage(outcome, GameLanguage.English));
    }

    private string GetPrefectAutoAppointmentMessage(PrefectAutoAppointmentOutcome outcome, GameLanguage language)
    {
        if (outcome.IsVacant)
        {
            return _localization?.FormatForLanguage(
                       language,
                       "cmd.prefect.auto_vacant",
                       GetCityName(outcome.City, language))
                   ?? string.Empty;
        }

        return _localization?.FormatForLanguage(
                   language,
                   "cmd.prefect.auto_reassigned",
                   GetCityName(outcome.City, language),
                   GetOfficerDisplayName(outcome.NewPrefect!, language))
               ?? string.Empty;
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

    private object[] GetFactionArgs(FactionData faction, GameLanguage language)
    {
        return new object[]
        {
            GetFactionName(faction, language)
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

    private static int GetAverageEffectiveStat(WorldState world, CityData city, Func<OfficerData, int> selector, Func<ItemData, int> bonusSelector, OfficerProgressionStat progressionStat)
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

            total += GetEffectiveStat(world, officer, selector, bonusSelector, progressionStat);
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

    private static TroopAllocationData CreateTroopAllocationFromTotal(CityData city, int requestedTroops)
    {
        var allocation = new TroopAllocationData();
        var remaining = GetTransferAmount(requestedTroops, city.Troops);
        if (remaining <= 0)
        {
            return allocation;
        }

        foreach (var troopType in new[]
                 {
                     TroopType.Infantry,
                     TroopType.Spearman,
                     TroopType.Archer,
                     TroopType.Cavalry,
                     TroopType.Crossbow,
                     TroopType.Siege
                 })
        {
            if (remaining <= 0)
            {
                break;
            }

            var available = city.GetTroops(troopType);
            if (available <= 0)
            {
                continue;
            }

            var toTake = Math.Min(available, remaining);
            SetTroopAllocationValue(allocation, troopType, toTake);
            remaining -= toTake;
        }

        return allocation;
    }

    private static TroopAllocationData CreateTroopAllocationFromAttackDeployments(IEnumerable<AttackOfficerDeploymentData> deployments)
    {
        var allocation = new TroopAllocationData();
        foreach (var deployment in deployments)
        {
            switch (deployment.TroopType)
            {
                case TroopType.Infantry:
                    allocation.Infantry += deployment.TroopCount;
                    break;
                case TroopType.Spearman:
                    allocation.Spearman += deployment.TroopCount;
                    break;
                case TroopType.Cavalry:
                    allocation.Cavalry += deployment.TroopCount;
                    break;
                case TroopType.Archer:
                    allocation.Archer += deployment.TroopCount;
                    break;
                case TroopType.Crossbow:
                    allocation.Crossbow += deployment.TroopCount;
                    break;
                case TroopType.Siege:
                    allocation.Siege += deployment.TroopCount;
                    break;
            }
        }

        return allocation;
    }

    private static SiegeEngineAllocationData CreateSiegeEngineAllocationFromAttackDeployments(IEnumerable<AttackOfficerDeploymentData> deployments)
    {
        var allocation = new SiegeEngineAllocationData();
        foreach (var deployment in deployments)
        {
            if (deployment.TroopType != TroopType.Siege || deployment.TroopCount <= 0)
            {
                continue;
            }

            switch (deployment.SiegeEngineType)
            {
                case SiegeEngineType.Ram:
                    allocation.Ram += 1;
                    break;
                case SiegeEngineType.Catapult:
                    allocation.Catapult += 1;
                    break;
                case SiegeEngineType.Ladder:
                    allocation.Ladder += 1;
                    break;
            }
        }

        return allocation;
    }

    private static void SetTroopAllocationValue(TroopAllocationData allocation, TroopType troopType, int value)
    {
        switch (troopType)
        {
            case TroopType.Infantry:
                allocation.Infantry = value;
                break;
            case TroopType.Spearman:
                allocation.Spearman = value;
                break;
            case TroopType.Cavalry:
                allocation.Cavalry = value;
                break;
            case TroopType.Archer:
                allocation.Archer = value;
                break;
            case TroopType.Crossbow:
                allocation.Crossbow = value;
                break;
            case TroopType.Siege:
                allocation.Siege = value;
                break;
        }
    }

    private static int GetTroopAllocationValue(TroopAllocationData allocation, TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Infantry => allocation.Infantry,
            TroopType.Spearman => allocation.Spearman,
            TroopType.Cavalry => allocation.Cavalry,
            TroopType.Archer => allocation.Archer,
            TroopType.Crossbow => allocation.Crossbow,
            TroopType.Siege => allocation.Siege,
            _ => 0
        };
    }

    private static TroopAllocationData ScaleTroopAllocationToTotal(TroopAllocationData source, int targetTotal)
    {
        var allocation = new TroopAllocationData();
        var sourceTotal = source.Total;
        if (sourceTotal <= 0 || targetTotal <= 0)
        {
            return allocation;
        }

        if (targetTotal >= sourceTotal)
        {
            allocation.Infantry = source.Infantry;
            allocation.Spearman = source.Spearman;
            allocation.Cavalry = source.Cavalry;
            allocation.Archer = source.Archer;
            allocation.Crossbow = source.Crossbow;
            allocation.Siege = source.Siege;
            return allocation;
        }

        var troopTypes = new[]
        {
            TroopType.Infantry,
            TroopType.Spearman,
            TroopType.Cavalry,
            TroopType.Archer,
            TroopType.Crossbow,
            TroopType.Siege
        };
        var remainders = new List<(TroopType TroopType, double Fraction)>();
        var allocated = 0;
        foreach (var troopType in troopTypes)
        {
            var current = GetTroopAllocationValue(source, troopType);
            if (current <= 0)
            {
                SetTroopAllocationValue(allocation, troopType, 0);
                continue;
            }

            var scaled = current * targetTotal / (double)sourceTotal;
            var whole = (int)Math.Floor(scaled);
            SetTroopAllocationValue(allocation, troopType, whole);
            allocated += whole;
            remainders.Add((troopType, scaled - whole));
        }

        var remaining = targetTotal - allocated;
        foreach (var (troopType, _) in remainders
                     .OrderByDescending(item => item.Fraction)
                     .ThenByDescending(item => GetTroopAllocationValue(source, item.TroopType)))
        {
            if (remaining <= 0)
            {
                break;
            }

            var current = GetTroopAllocationValue(allocation, troopType);
            var sourceValue = GetTroopAllocationValue(source, troopType);
            if (current >= sourceValue)
            {
                continue;
            }

            SetTroopAllocationValue(allocation, troopType, current + 1);
            remaining -= 1;
        }

        return allocation;
    }

    private static TroopAllocationData CreateTroopAllocationFromCityProportion(CityData city, int targetTotal)
    {
        return ScaleTroopAllocationToTotal(new TroopAllocationData
        {
            Infantry = city.InfantryTroops,
            Spearman = city.SpearmanTroops,
            Cavalry = city.CavalryTroops,
            Archer = city.ArcherTroops,
            Crossbow = city.CrossbowTroops,
            Siege = city.SiegeTroops
        }, targetTotal);
    }

    private static string GetTroopTypeLocaleKey(TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Infantry => "troop_type.infantry",
            TroopType.Spearman => "troop_type.spearman",
            TroopType.Cavalry => "troop_type.cavalry",
            TroopType.Archer => "troop_type.archer",
            TroopType.Crossbow => "troop_type.crossbow",
            TroopType.Siege => "troop_type.siege",
            _ => "troop_type.infantry"
        };
    }

    private string GetTroopTypeName(TroopType troopType, GameLanguage language)
    {
        var key = GetTroopTypeLocaleKey(troopType);
        return _localization?.TForLanguage(language, key) ?? troopType.ToString();
    }

    private static bool CanRecruitTroopType(CityData city, TroopType troopType) => RecruitRules.CanRecruitTroopType(city, troopType);

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
            if (officer == null ||
                IsOfficerAssignedThisMonth(world, officer) ||
                HasActiveInternalAffairsSchedule(world, officerId))
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
        if (officer == null ||
            IsOfficerAssignedThisMonth(world, officer) ||
            HasActiveInternalAffairsSchedule(world, officerId))
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

    private static bool HasActiveInternalAffairsJob(WorldState world, int cityId, InternalAffairsJobType jobType, ConstructionProjectType constructionProjectType = ConstructionProjectType.None)
    {
        return world.InternalAffairsSchedules.Any(schedule =>
        {
            if (schedule.State is not (InternalAffairsScheduleState.Active or InternalAffairsScheduleState.Paused) ||
                schedule.CityId != cityId ||
                schedule.JobType != jobType)
            {
                return false;
            }

            if (jobType != InternalAffairsJobType.Construction)
            {
                return true;
            }

            return schedule.ConstructionProjectType == constructionProjectType;
        });
    }

    private static bool IsFactionRuler(WorldState world, int officerId)
    {
        return world.Factions.Any(faction => faction.RulerOfficerId == officerId);
    }

    private static bool IsFactionAlive(WorldState world, int factionId)
    {
        return world.Cities.Any(city => city.OwnerFactionId == factionId);
    }

    private PrefectAutoAppointmentOutcome? EliminateOfficer(WorldState world, OfficerData officer)
    {
        var city = officer.CityId > 0 ? world.GetCity(officer.CityId) : null;
        var removedCityId = city?.Id ?? 0;
        city?.OfficerIds.Remove(officer.Id);

        var faction = world.Factions.FirstOrDefault(item =>
            item.RulerOfficerId == officer.Id ||
            item.OfficerIds.Contains(officer.Id));
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

        officer.CityId = 0;
        officer.FreeOfficerStayMonths = 0;
        officer.DeathYear = world.Year;
        ClearAllOfficerAppointments(officer);

        if (faction != null && faction.RulerOfficerId == officer.Id)
        {
            faction.RulerOfficerId = 0;
        }

        ClearFactionAdvisorPosts(world, officer.Id);
        if (removedCityId > 0)
        {
            var removedCity = world.GetCity(removedCityId);
            if (removedCity != null)
            {
                return EnsureCityPrefectAppointment(world, removedCity);
            }
        }

        return null;
    }

    private void ResolveRulerDeath(WorldState world, int factionId)
    {
        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return;
        }

        var candidateIds = faction.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null && IsOfficerAlive(world, officer))
            .OrderByDescending(officer => officer!.Leadership + officer.Intelligence + officer.Politics + officer.Charm)
            .ThenByDescending(officer => officer!.Loyalty)
            .Select(officer => officer!.Id)
            .ToList();

        if (candidateIds.Count == 0)
        {
            CollapseFaction(world, factionId);
            return;
        }

        if (faction.IsPlayer)
        {
            world.PendingSuccessionRecords.RemoveAll(record => record.FactionId == factionId);
            world.PendingSuccessionRecords.Add(new WorldState.PendingSuccessionData
            {
                FactionId = factionId,
                CandidateOfficerIds = candidateIds
            });
            return;
        }

        var successor = world.GetOfficer(candidateIds[0]);
        if (successor == null)
        {
            CollapseFaction(world, factionId);
            return;
        }

        ApplyFactionSuccessor(world, faction, successor);
        if (!faction.OfficerIds.Contains(candidateIds[0]))
        {
            faction.OfficerIds.Add(candidateIds[0]);
        }
    }

    private void ApplyFactionSuccessor(WorldState world, FactionData faction, OfficerData successor)
    {
        var previousRuler = world.GetOfficer(faction.RulerOfficerId);
        if (previousRuler != null && previousRuler.Id != successor.Id)
        {
            ClearOfficerAppointment(previousRuler, OfficerAppointmentRules.Lord);
        }

        faction.RulerOfficerId = successor.Id;
        if (faction.ChancellorOfficerId == successor.Id)
        {
            faction.ChancellorOfficerId = 0;
            ClearOfficerAppointment(successor, OfficerAppointmentRules.Chancellor);
        }

        if (faction.ChiefStrategistOfficerId == successor.Id)
        {
            faction.ChiefStrategistOfficerId = 0;
            ClearOfficerAppointment(successor, OfficerAppointmentRules.ChiefStrategist);
        }

        AssignOfficerAppointment(successor, OfficerAppointmentRules.Lord);
        successor.Belongs = faction.Id.ToString();
        var successorNameZh = !string.IsNullOrWhiteSpace(successor.NameZhHant) ? successor.NameZhHant : successor.Name;
        var successorNameEn = !string.IsNullOrWhiteSpace(successor.Name) ? successor.Name : successor.NameZhHant;
        faction.NameZhHant = string.IsNullOrWhiteSpace(successorNameZh)
            ? faction.NameZhHant
            : _localization?.FormatForLanguage(GameLanguage.TraditionalChinese, "fmt.faction_name_ruler", successorNameZh) ?? $"{successorNameZh}軍";
        faction.NameEn = string.IsNullOrWhiteSpace(successorNameEn)
            ? faction.NameEn
            : _localization?.FormatForLanguage(GameLanguage.English, "fmt.faction_name_ruler", successorNameEn) ?? $"{successorNameEn} Forces";

        if (!faction.OfficerIds.Contains(successor.Id))
        {
            faction.OfficerIds.Add(successor.Id);
        }

        foreach (var officerId in faction.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            officer.Belongs = faction.Id.ToString();
        }
    }

    private (string ZhHant, string En)? TryResolveBattleRulerDeath(WorldState world, int officerId, float casualtyRatio)
    {
        if (casualtyRatio < 0.999f)
        {
            return null;
        }

        var officer = world.GetOfficer(officerId);
        if (officer == null || !IsOfficerAlive(world, officer) || !IsFactionRuler(world, officer.Id))
        {
            return null;
        }

        var faction = world.Factions.FirstOrDefault(item => item.RulerOfficerId == officer.Id);
        if (faction == null)
        {
            return null;
        }

        var factionId = faction.Id;
        var factionNameZh = GetFactionName(faction, GameLanguage.TraditionalChinese);
        var factionNameEn = GetFactionName(faction, GameLanguage.English);
        var rulerNameZh = GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese);
        var rulerNameEn = GetOfficerDisplayName(officer, GameLanguage.English);

        _ = EliminateOfficer(world, officer);
        ResolveRulerDeath(world, factionId);

        var updatedFaction = world.GetFaction(factionId);
        if (updatedFaction == null || !IsFactionAlive(world, factionId))
        {
            return (
                _localization?.FormatForLanguage(GameLanguage.TraditionalChinese, "cmd.attack.ruler_fell_faction_destroyed_suffix", rulerNameZh, factionNameZh) ?? string.Empty,
                _localization?.FormatForLanguage(GameLanguage.English, "cmd.attack.ruler_fell_faction_destroyed_suffix", rulerNameEn, factionNameEn) ?? string.Empty);
        }

        if (updatedFaction.IsPlayer || updatedFaction.RulerOfficerId <= 0)
        {
            return (
                _localization?.FormatForLanguage(GameLanguage.TraditionalChinese, "cmd.attack.ruler_fell_pending_succession_suffix", rulerNameZh, factionNameZh) ?? string.Empty,
                _localization?.FormatForLanguage(GameLanguage.English, "cmd.attack.ruler_fell_pending_succession_suffix", rulerNameEn, factionNameEn) ?? string.Empty);
        }

        var successor = world.GetOfficer(updatedFaction.RulerOfficerId);
        if (successor == null)
        {
            return (
                _localization?.FormatForLanguage(GameLanguage.TraditionalChinese, "cmd.attack.ruler_fell_pending_succession_suffix", rulerNameZh, factionNameZh) ?? string.Empty,
                _localization?.FormatForLanguage(GameLanguage.English, "cmd.attack.ruler_fell_pending_succession_suffix", rulerNameEn, factionNameEn) ?? string.Empty);
        }

        return (
            _localization?.FormatForLanguage(
                GameLanguage.TraditionalChinese,
                "cmd.attack.ruler_fell_succeeded_suffix",
                rulerNameZh,
                GetOfficerDisplayName(successor, GameLanguage.TraditionalChinese),
                factionNameZh) ?? string.Empty,
            _localization?.FormatForLanguage(
                GameLanguage.English,
                "cmd.attack.ruler_fell_succeeded_suffix",
                rulerNameEn,
                GetOfficerDisplayName(successor, GameLanguage.English),
                factionNameEn) ?? string.Empty);
    }

    private void CollapseFaction(WorldState world, int factionId)
    {
        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return;
        }

        var cityIds = world.Cities
            .Where(city => city.OwnerFactionId == factionId)
            .Select(city => city.Id)
            .ToList();
        var officerIds = faction.OfficerIds.ToList();

        foreach (var cityId in cityIds)
        {
            var city = world.GetCity(cityId);
            if (city == null)
            {
                continue;
            }

            city.OwnerFactionId = 0;
            ClearCityPrefectAuthorization(city);
            city.Loyalty = 50;
            foreach (var officerId in city.OfficerIds.ToList())
            {
                var officer = world.GetOfficer(officerId);
                if (officer == null)
                {
                    continue;
                }

                officer.CityId = city.Id;
                officer.FreeOfficerStayMonths = Math.Max(officer.FreeOfficerStayMonths, 2);
            }
        }

        foreach (var officerId in officerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (officer.CityId > 0)
            {
                officer.FreeOfficerStayMonths = Math.Max(officer.FreeOfficerStayMonths, 2);
            }
        }

        faction.OfficerIds.Clear();
        faction.RulerOfficerId = 0;
        faction.ChancellorOfficerId = 0;
        faction.ChiefStrategistOfficerId = 0;
        foreach (var item in world.Items.Where(item => item.OwnerFactionId == factionId).ToList())
        {
            item.OwnerFactionId = 0;
            item.OwnerCityId = 0;
            item.EquippedOfficerId = 0;
        }

        world.DiplomacyRelations.RemoveAll(relation => relation.FactionAId == factionId || relation.FactionBId == factionId);
        world.PendingCommands.RemoveAll(command => command.ActorFactionId == factionId);
        world.InternalAffairsSchedules.RemoveAll(schedule =>
        {
            var city = world.GetCity(schedule.CityId);
            return city != null && cityIds.Contains(city.Id);
        });
        world.PendingSuccessionRecords.RemoveAll(record => record.FactionId == factionId);
    }

    private static bool IsOfficerAlive(WorldState world, OfficerData? officer)
    {
        if (officer == null)
        {
            return false;
        }

        return officer.DeathYear <= 0 || world.Year <= officer.DeathYear;
    }

    private static void ClearFactionAdvisorPosts(WorldState world, int officerId)
    {
        var officer = world.GetOfficer(officerId);
        if (officer != null)
        {
            OfficerAppointmentRules.RemoveAppointment(officer, OfficerAppointmentRules.Chancellor);
            OfficerAppointmentRules.RemoveAppointment(officer, OfficerAppointmentRules.ChiefStrategist);
        }

        foreach (var faction in world.Factions)
        {
            if (faction.ChancellorOfficerId == officerId)
            {
                faction.ChancellorOfficerId = 0;
            }

            if (faction.ChiefStrategistOfficerId == officerId)
            {
                faction.ChiefStrategistOfficerId = 0;
            }
        }
    }

    private static OfficerData? GetCityPrefect(WorldState world, CityData city)
    {
        foreach (var officerId in city.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (HasOfficerAppointment(officer, OfficerAppointmentRules.Governor))
            {
                return officer;
            }
        }

        return null;
    }

    private PrefectAutoAppointmentOutcome? EnsureCityPrefectAppointment(WorldState world, CityData city)
    {
        if (city.OwnerFactionId <= 0)
        {
            ClearCityPrefectAuthorization(city);
            return null;
        }

        var faction = world.GetFaction(city.OwnerFactionId);
        if (faction == null)
        {
            ClearCityPrefectAuthorization(city);
            return null;
        }

        var currentPrefect = GetCityPrefect(world, city);
        if (currentPrefect != null && IsEligibleAutoPrefectCandidate(world, city, faction, currentPrefect))
        {
            RemoveDuplicateCityGovernorAppointments(world, city, currentPrefect.Id);
            return null;
        }

        foreach (var officerId in city.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer != null && HasOfficerAppointment(officer, OfficerAppointmentRules.Governor))
            {
                ClearOfficerAppointment(officer, OfficerAppointmentRules.Governor);
            }
        }

        var replacement = GetBestAutoPrefectCandidate(world, city, faction);
        if (replacement == null)
        {
            ClearCityPrefectAuthorization(city);
            return new PrefectAutoAppointmentOutcome
            {
                City = city
            };
        }

        AssignOfficerAppointment(replacement, OfficerAppointmentRules.Governor);
        RemoveDuplicateCityGovernorAppointments(world, city, replacement.Id);
        ResetAuthorizedPlanForNewPrefect(world, city, replacement);
        return new PrefectAutoAppointmentOutcome
        {
            City = city,
            NewPrefect = replacement
        };
    }

    private static bool IsEligibleAutoPrefectCandidate(WorldState world, CityData city, FactionData faction, OfficerData officer)
    {
        return officer.CityId == city.Id &&
               city.OfficerIds.Contains(officer.Id) &&
               faction.OfficerIds.Contains(officer.Id) &&
               !IsFactionRuler(world, officer.Id) &&
               IsOfficerAlive(world, officer);
    }

    private static OfficerData? GetBestAutoPrefectCandidate(WorldState world, CityData city, FactionData faction)
    {
        return city.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null && IsEligibleAutoPrefectCandidate(world, city, faction, officer))
            .OrderByDescending(officer => ScoreAutoPrefectCandidate(officer!))
            .ThenByDescending(officer => officer!.Loyalty)
            .ThenBy(officer => officer!.Id)
            .FirstOrDefault();
    }

    private static int ScoreAutoPrefectCandidate(OfficerData officer)
    {
        return officer.Politics * 3 +
               officer.Intelligence * 2 +
               officer.Charm +
               officer.Leadership +
               officer.FarmRank * 15 +
               officer.CommercialRank * 15 +
               officer.DefendRank * 10 +
               officer.DisasterPreventionRank * 10 +
               officer.ConstructionRank * 10;
    }

    private static void RemoveDuplicateCityGovernorAppointments(WorldState world, CityData city, int keeperOfficerId)
    {
        foreach (var officerId in city.OfficerIds)
        {
            if (officerId == keeperOfficerId)
            {
                continue;
            }

            var officer = world.GetOfficer(officerId);
            if (officer != null && HasOfficerAppointment(officer, OfficerAppointmentRules.Governor))
            {
                ClearOfficerAppointment(officer, OfficerAppointmentRules.Governor);
            }
        }
    }

    private void ResetAuthorizedPlanForNewPrefect(WorldState world, CityData city, OfficerData prefect)
    {
        RemoveAuthorizedPlanSchedule(world, city);

        if (city.PrefectAuthorizationType == PrefectAuthorizationType.Full)
        {
            var plannedJob = ChooseAuthorizedPlanJob(world, city, prefect);
            if (plannedJob.HasValue)
            {
                city.PrefectPlanJobType = plannedJob.Value;
                city.PrefectPlanTotalMonths = ChooseAuthorizedPlanDuration(city, plannedJob.Value, prefect);
                city.PrefectPlanRemainingMonths = city.PrefectPlanTotalMonths;
                city.PrefectPlanInvestedGold = GetRecommendedInternalAffairsGold(plannedJob.Value, city.PrefectPlanTotalMonths);
                city.PrefectPlanIsPlayerDirected = false;
                return;
            }
        }

        if (city.PrefectAuthorizationType == PrefectAuthorizationType.None)
        {
            ClearCityAuthorizedPlan(city);
        }
    }

    private static void ClearCityPrefectAuthorization(CityData city)
    {
        city.PrefectAuthorizationType = PrefectAuthorizationType.None;
        ClearCityAuthorizedPlan(city);
    }

    private static bool RequiresAppointedPrefect(PrefectAuthorizationType authorizationType)
    {
        return authorizationType is PrefectAuthorizationType.Half or PrefectAuthorizationType.Full;
    }

    private static void ClearOfficerAppointment(OfficerData officer, string appointment)
    {
        OfficerAppointmentRules.RemoveAppointment(officer, appointment);
    }

    private static void ClearAllOfficerAppointments(OfficerData officer)
    {
        officer.Appointments.Clear();
    }

    private static void AssignOfficerAppointment(OfficerData officer, string appointment)
    {
        OfficerAppointmentRules.AddAppointment(officer, appointment);
    }

    private static bool HasOfficerAppointment(OfficerData officer, string appointment)
    {
        return OfficerAppointmentRules.HasAppointment(officer, appointment);
    }

    private static bool IsValidAdvisorPosition(string position)
    {
        return position.Equals("Chancellor", StringComparison.OrdinalIgnoreCase) ||
               position.Equals("ChiefStrategist", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidClearableAppointment(string appointment)
    {
        return OfficerAppointmentRules.IsValidOfficerAppointment(appointment) ||
               appointment.Equals(OfficerAppointmentRules.Chancellor, StringComparison.OrdinalIgnoreCase) ||
               appointment.Equals(OfficerAppointmentRules.ChiefStrategist, StringComparison.OrdinalIgnoreCase);
    }

    private string GetAppointmentName(string position, GameLanguage language)
    {
        if (_localization == null)
        {
            return position;
        }

        return position.ToLowerInvariant() switch
        {
            "lord" => _localization.TForLanguage(language, "role.lord"),
            "governor" => _localization.TForLanguage(language, "role.governor"),
            "strategist" => _localization.TForLanguage(language, "role.strategist"),
            "chancellor" => _localization.TForLanguage(language, "ui.chancellor"),
            "chiefstrategist" => _localization.TForLanguage(language, "ui.chief_strategist"),
            _ => position
        };
    }

    private string GetPrefectAuthorizationTypeName(PrefectAuthorizationType authorizationType, GameLanguage language)
    {
        if (_localization == null)
        {
            return authorizationType.ToString();
        }

        return authorizationType switch
        {
            PrefectAuthorizationType.None => _localization.TForLanguage(language, "ui.prefect_authorization.none"),
            PrefectAuthorizationType.Half => _localization.TForLanguage(language, "ui.prefect_authorization.half"),
            PrefectAuthorizationType.Full => _localization.TForLanguage(language, "ui.prefect_authorization.full"),
            _ => authorizationType.ToString()
        };
    }

    private static DiplomacyRelationData GetOrCreateDiplomacyRelation(WorldState world, int factionAId, int factionBId)
    {
        var low = Math.Min(factionAId, factionBId);
        var high = Math.Max(factionAId, factionBId);
        var existing = world.DiplomacyRelations.FirstOrDefault(relation =>
            relation.FactionAId == low &&
            relation.FactionBId == high);
        if (existing != null)
        {
            return existing;
        }

        var relation = new DiplomacyRelationData
        {
            FactionAId = low,
            FactionBId = high
        };
        world.DiplomacyRelations.Add(relation);
        return relation;
    }

    private static DiplomacyRelationData? FindDiplomacyRelation(WorldState world, int factionAId, int factionBId)
    {
        var low = Math.Min(factionAId, factionBId);
        var high = Math.Max(factionAId, factionBId);
        return world.DiplomacyRelations.FirstOrDefault(relation =>
            relation.FactionAId == low &&
            relation.FactionBId == high);
    }

    private static bool HasActiveDiplomacyBlock(WorldState world, int factionAId, int factionBId)
    {
        var relation = FindDiplomacyRelation(world, factionAId, factionBId);
        return relation != null &&
               relation.Status is DiplomacyStatusType.Truce or DiplomacyStatusType.Alliance &&
               relation.RemainingMonths > 0;
    }

    private static bool TryBreakDiplomacyBlockForAttack(WorldState world, int attackerFactionId, int defenderFactionId)
    {
        var relation = FindDiplomacyRelation(world, attackerFactionId, defenderFactionId);
        if (relation == null ||
            relation.Status is not (DiplomacyStatusType.Truce or DiplomacyStatusType.Alliance) ||
            relation.RemainingMonths <= 0)
        {
            return false;
        }

        relation.Status = DiplomacyStatusType.Neutral;
        relation.RemainingMonths = 0;
        relation.RelationScore = Math.Clamp(relation.RelationScore - 18, -100, 100);
        relation.LastUpdatedYear = world.Year;
        relation.LastUpdatedMonth = world.Month;
        return true;
    }

    private static bool IsValidOfficerAppointment(string role)
    {
        return OfficerAppointmentRules.IsValidOfficerAppointment(role);
    }

    private static int GetNextInternalAffairsScheduleId(WorldState world)
    {
        return world.InternalAffairsSchedules.Count == 0
            ? 1
            : world.InternalAffairsSchedules.Max(schedule => schedule.Id) + 1;
    }

    private static void ReleaseInternalAffairsOfficerAssignment(WorldState world, int officerId)
    {
        var officer = world.GetOfficer(officerId);
        if (officer == null)
        {
            return;
        }

        if (officer.LastAssignedYear == world.Year &&
            officer.LastAssignedMonth == world.Month &&
            officer.LastAssignedCommand == CommandType.InternalAffairs)
        {
            officer.LastAssignedYear = -1;
            officer.LastAssignedMonth = -1;
            officer.LastAssignedCommand = CommandType.Pass;
        }
    }

    private static void ClearCityAuthorizedPlan(CityData city)
    {
        city.PrefectPlanJobType = InternalAffairsJobType.Farm;
        city.PrefectPlanConstructionProjectType = ConstructionProjectType.None;
        city.PrefectPlanInvestedGold = 0;
        city.PrefectPlanTotalMonths = 0;
        city.PrefectPlanRemainingMonths = 0;
        city.PrefectPlanIsPlayerDirected = false;
    }

    private static InternalAffairsScheduleData? GetAuthorizedPlanSchedule(WorldState world, int cityId)
    {
        return world.InternalAffairsSchedules.FirstOrDefault(schedule =>
            schedule.CityId == cityId &&
            schedule.IsAuthorizedPlan &&
            schedule.State is not (InternalAffairsScheduleState.Terminated or InternalAffairsScheduleState.Interrupted or InternalAffairsScheduleState.Completed));
    }

    private static void RemoveAuthorizedPlanSchedule(WorldState world, CityData city)
    {
        var schedule = GetAuthorizedPlanSchedule(world, city.Id);
        if (schedule == null)
        {
            return;
        }

        ReleaseInternalAffairsOfficerAssignment(world, schedule.OfficerId);
        schedule.State = InternalAffairsScheduleState.Terminated;
    }

    private static InternalAffairsJobType? ChooseAuthorizedPlanJob(WorldState world, CityData city, OfficerData? prefect = null)
    {
        var activeJobs = new HashSet<InternalAffairsJobType>(
            world.InternalAffairsSchedules
                .Where(schedule => schedule.State == InternalAffairsScheduleState.Active && schedule.CityId == city.Id)
                .Select(schedule => schedule.JobType));
        var activeConstructionProjects = new HashSet<ConstructionProjectType>(
            world.InternalAffairsSchedules
                .Where(schedule =>
                    schedule.State == InternalAffairsScheduleState.Active &&
                    schedule.CityId == city.Id &&
                    schedule.JobType == InternalAffairsJobType.Construction)
                .Select(schedule => schedule.ConstructionProjectType));

        var politics = prefect?.Politics ?? 50;
        var intelligence = prefect?.Intelligence ?? 50;
        var leadership = prefect?.Leadership ?? 50;
        var ambition = prefect?.Ambition ?? 50;
        var loyalty = prefect?.Loyalty ?? 50;
        var balancedCivil = (politics + intelligence) / 2;
        var defensiveBias = (leadership + intelligence + Math.Max(0, 100 - ambition / 2)) / 3;
        var growthBias = (politics + Math.Max(0, ambition) + loyalty / 2) / 3;
        var constructionBias = (leadership + politics + intelligence) / 3;

        var candidates = new (InternalAffairsJobType JobType, int Score)[]
        {
            (
                InternalAffairsJobType.Farm,
                city.Farm - (balancedCivil / 6) - ((city.Population < 42000 ? 10 : 0)) - ((city.Food < 1800 ? 14 : 0))
            ),
            (
                InternalAffairsJobType.Commercial,
                city.Commercial - (growthBias / 6) - ((city.Gold < 700 ? 18 : 0))
            ),
            (
                InternalAffairsJobType.Defend,
                city.Defense - (defensiveBias / 6) - ((city.Defense < 55 ? 14 : 0)) - ((city.Troops < 1800 ? 6 : 0))
            ),
            (
                InternalAffairsJobType.WaterControl,
                city.DisasterPrevention - (intelligence / 6) - ((city.DisasterPrevention < 45 ? 12 : 0))
            ),
            (
                InternalAffairsJobType.Construction,
                ((city.Commercial + city.Defense) / 2) - (constructionBias / 7) - (GetMissingFacilityCount(city) > 0 ? 8 : 0)
            )
        };

        var selectedJob = candidates
            .Where(candidate => !activeJobs.Contains(candidate.JobType))
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => (int)candidate.JobType)
            .Select(candidate => (InternalAffairsJobType?)candidate.JobType)
            .FirstOrDefault();

        if (selectedJob == InternalAffairsJobType.Construction)
        {
            var projectType = AiConstructionRules.ChooseConstructionProjectType(world, city);
            if (activeConstructionProjects.Contains(projectType))
            {
                return candidates
                    .Where(candidate => candidate.JobType != InternalAffairsJobType.Construction && !activeJobs.Contains(candidate.JobType))
                    .OrderBy(candidate => candidate.Score)
                    .ThenBy(candidate => (int)candidate.JobType)
                    .Select(candidate => (InternalAffairsJobType?)candidate.JobType)
                    .FirstOrDefault();
            }
        }

        return selectedJob;
    }

    private static int ChooseAuthorizedPlanDuration(CityData city, InternalAffairsJobType jobType, OfficerData? prefect = null)
    {
        var politics = prefect?.Politics ?? 50;
        var intelligence = prefect?.Intelligence ?? 50;
        var leadership = prefect?.Leadership ?? 50;
        var ambition = prefect?.Ambition ?? 50;

        return jobType switch
        {
            InternalAffairsJobType.Farm when city.Farm < 45 => 4,
            InternalAffairsJobType.Commercial when city.Commercial < 45 => 4,
            InternalAffairsJobType.Defend when city.Defense < 50 => 4,
            InternalAffairsJobType.WaterControl when city.DisasterPrevention < 35 => 4,
            InternalAffairsJobType.Construction when GetMissingFacilityCount(city) > 0 => 3,
            InternalAffairsJobType.Defend when leadership >= 75 => 3,
            InternalAffairsJobType.Commercial when politics >= 75 && ambition >= 60 => 3,
            InternalAffairsJobType.Farm when intelligence >= 70 => 3,
            _ => 2
        };
    }

    private InternalAffairsScheduleData CreateAuthorizedPlanSchedule(WorldState world, CityData city, int officerId)
    {
        var projectType = city.PrefectPlanJobType == InternalAffairsJobType.Construction
            ? ResolveConstructionProjectType(world, city, city.PrefectPlanConstructionProjectType)
            : ConstructionProjectType.None;
        var investedGold = city.PrefectPlanInvestedGold > 0
            ? city.PrefectPlanInvestedGold
            : GetRecommendedInternalAffairsGold(city.PrefectPlanJobType, city.PrefectPlanRemainingMonths);
        return new InternalAffairsScheduleData
        {
            Id = GetNextInternalAffairsScheduleId(world),
            CityId = city.Id,
            OfficerId = officerId,
            IsAuthorizedPlan = true,
            JobType = city.PrefectPlanJobType,
            ConstructionProjectType = projectType,
            InvestedGold = investedGold,
            RemainingMonths = city.PrefectPlanRemainingMonths,
            TotalMonths = city.PrefectPlanTotalMonths > 0 ? city.PrefectPlanTotalMonths : city.PrefectPlanRemainingMonths,
            StartedYear = world.Year,
            StartedMonth = world.Month,
            State = InternalAffairsScheduleState.Active,
            SkipExecutionYear = -1,
            SkipExecutionMonth = -1
        };
    }

    private static OfficerData? TrySelectInternalAffairsOfficerForSchedule(WorldState world, CityData city, InternalAffairsJobType jobType)
    {
        return city.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer =>
                officer != null &&
                !IsOfficerAssignedThisMonth(world, officer) &&
                !HasActiveInternalAffairsSchedule(world, officer.Id))
            .OrderByDescending(officer => ScoreInternalAffairsOfficer(officer!, jobType))
            .ThenByDescending(officer => officer!.Loyalty)
            .ThenBy(officer => officer!.Id)
            .FirstOrDefault();
    }

    private static int GetMissingFacilityCount(CityData city)
    {
        var count = 0;
        if (city.BowWorkshopLevel <= 0)
        {
            count += 1;
        }

        if (city.SiegeWorkshopLevel <= 0)
        {
            count += 1;
        }

        if (city.HorsePastureLevel <= 0)
        {
            count += 1;
        }

        return count;
    }

    private static ConstructionProjectType ResolveConstructionProjectType(WorldState world, CityData city, ConstructionProjectType requestedProjectType)
    {
        if (requestedProjectType != ConstructionProjectType.None)
        {
            return requestedProjectType;
        }

        return AiConstructionRules.ChooseConstructionProjectType(world, city);
    }

    private static int ScoreInternalAffairsOfficer(OfficerData officer, InternalAffairsJobType jobType)
    {
        return jobType switch
        {
            InternalAffairsJobType.Farm => officer.Politics * 3 + officer.Charm + officer.Intelligence + officer.FarmRank * 25,
            InternalAffairsJobType.Commercial => officer.Politics * 3 + officer.Intelligence * 2 + officer.CommercialRank * 25,
            InternalAffairsJobType.Defend => officer.Leadership * 2 + officer.Politics * 2 + officer.DefendRank * 25,
            InternalAffairsJobType.WaterControl => officer.Intelligence * 2 + officer.Politics * 2 + officer.DisasterPreventionRank * 25,
            InternalAffairsJobType.Construction => officer.Politics * 2 + officer.Leadership + officer.Intelligence + officer.ConstructionRank * 25,
            _ => officer.Politics + officer.Intelligence + officer.Charm
        };
    }

    private static (int Farm, int Commercial, int Defense, int DisasterPrevention, int Loyalty, int ConstructionPoints) ApplyInternalAffairsJob(
        WorldState world,
        CityData city,
        OfficerData officer,
        InternalAffairsJobType jobType,
        ConstructionProjectType constructionProjectType,
        int investedGold,
        int totalMonths)
    {
        var intelligence = GetEffectiveStat(world, officer, data => data.Intelligence, item => item.IntelligenceBonus, OfficerProgressionStat.Intelligence);
        var politics = GetEffectiveStat(world, officer, data => data.Politics, item => item.PoliticsBonus, OfficerProgressionStat.Politics);
        var charm = GetEffectiveStat(world, officer, data => data.Charm, item => item.CharmBonus, OfficerProgressionStat.Charm);
        var leadership = GetEffectiveStat(world, officer, data => data.Leadership, item => item.LeadershipBonus, OfficerProgressionStat.Leadership);
        var officerBonus = Math.Max(0, (intelligence + politics + charm) / 90);
        var progressionBonus = OfficerProgressionRules.GetInternalAffairsOutputBonus(officer, jobType);
        var monthlyInvestment = investedGold;
        var goldBonus = 1 + Math.Min(4, Math.Max(0, (monthlyInvestment - 50) / 100));
        var primaryGain = 2 + officerBonus + progressionBonus + goldBonus;
        var secondaryGain = 1 + Math.Max(0, progressionBonus / 2);
        (int Farm, int Commercial, int Defense, int DisasterPrevention, int Loyalty, int ConstructionPoints) gains = jobType switch
        {
            InternalAffairsJobType.Farm => (primaryGain, 0, 0, 0, 0, 0),
            InternalAffairsJobType.Commercial => (0, primaryGain, 0, 0, 0, 0),
            InternalAffairsJobType.Defend => (0, 0, primaryGain, 0, 0, 0),
            InternalAffairsJobType.WaterControl => (0, 0, 0, primaryGain, secondaryGain, 0),
            InternalAffairsJobType.Construction => (0, secondaryGain, secondaryGain, secondaryGain, 0, ConstructionRules.GetConstructionPoints(politics, intelligence, leadership, monthlyInvestment, progressionBonus)),
            _ => (0, 0, 0, 0, 0, 0)
        };

        city.Farm = ClampStat(city.Farm + gains.Farm);
        city.Commercial = ClampStat(city.Commercial + gains.Commercial);
        city.Defense = ClampStat(city.Defense + gains.Defense);
        city.DisasterPrevention = ClampStat(city.DisasterPrevention + gains.DisasterPrevention);
        city.Loyalty = ClampStat(city.Loyalty + gains.Loyalty);
        if (jobType == InternalAffairsJobType.Construction)
        {
            ConstructionRules.ApplyProgress(city, constructionProjectType, gains.ConstructionPoints);
        }

        OfficerProgressionRules.AwardInternalAffairsExperience(officer, jobType, 40);
        OfficerProgressionRules.AwardCivilExperience(officer, 12);
        return gains;
    }

    private static int GetInternalAffairsMonthlyGoldCost(InternalAffairsScheduleData schedule)
    {
        return GetInternalAffairsMonthlyGoldCost(schedule.InvestedGold, schedule.TotalMonths, schedule.RemainingMonths);
    }

    private static int GetInternalAffairsMonthlyGoldCost(int investedGold, int totalMonths, int remainingMonths)
    {
        if (investedGold <= 0 || totalMonths <= 0 || remainingMonths <= 0)
        {
            return 0;
        }

        return investedGold;
    }

    private static int GetMinimumInternalAffairsGold(int months)
    {
        return 1;
    }

    private static int GetRecommendedInternalAffairsGold(InternalAffairsJobType jobType, int months)
    {
        return jobType switch
        {
            InternalAffairsJobType.Farm => 60,
            InternalAffairsJobType.Commercial => 70,
            InternalAffairsJobType.Defend => 80,
            InternalAffairsJobType.WaterControl => 70,
            InternalAffairsJobType.Construction => 100,
            _ => 60
        };
    }

    private string GetInternalAffairsJobName(InternalAffairsJobType jobType, GameLanguage language)
    {
        var key = jobType switch
        {
            InternalAffairsJobType.Farm => "command.internal_affairs.farm",
            InternalAffairsJobType.Commercial => "command.internal_affairs.commercial",
            InternalAffairsJobType.Defend => "command.internal_affairs.defend",
            InternalAffairsJobType.WaterControl => "command.internal_affairs.disaster_prevention",
            InternalAffairsJobType.Construction => "command.internal_affairs.construction",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(key)
            ? jobType.ToString()
            : _localization?.FormatForLanguage(language, key) ?? jobType.ToString();
    }

    private string GetConstructionProjectName(ConstructionProjectType projectType, GameLanguage language)
    {
        var key = projectType switch
        {
            ConstructionProjectType.BowWorkshop => "construction_project.bow_workshop",
            ConstructionProjectType.SiegeWorkshop => "construction_project.siege_workshop",
            ConstructionProjectType.HorsePasture => "construction_project.horse_pasture",
            ConstructionProjectType.Ram => "construction_project.ram",
            ConstructionProjectType.Catapult => "construction_project.catapult",
            ConstructionProjectType.Ladder => "construction_project.ladder",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(key)
            ? projectType.ToString()
            : _localization?.TForLanguage(language, key) ?? projectType.ToString();
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

    private int TransferOfficers(
        WorldState world,
        CityData sourceCity,
        CityData targetCity,
        List<int> requestedOfficerIds,
        out PrefectAutoAppointmentOutcome? sourceCityPrefectOutcome)
    {
        var movedOfficerCount = 0;
        var sourcePrefectMoved = false;
        sourceCityPrefectOutcome = null;
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

            if (HasOfficerAppointment(officer, OfficerAppointmentRules.Governor))
            {
                ClearOfficerAppointment(officer, OfficerAppointmentRules.Governor);
                sourcePrefectMoved = true;
            }

            sourceCity.OfficerIds.Remove(officerId);
            if (!targetCity.OfficerIds.Contains(officerId))
            {
                targetCity.OfficerIds.Add(officerId);
            }

            officer.CityId = targetCity.Id;
            movedOfficerCount += 1;
        }

        if (sourcePrefectMoved)
        {
            sourceCityPrefectOutcome = EnsureCityPrefectAppointment(world, sourceCity);
        }

        return movedOfficerCount;
    }

    private static int GetEffectiveStat(
        WorldState world,
        OfficerData officer,
        Func<OfficerData, int> selector,
        Func<ItemData, int> bonusSelector,
        OfficerProgressionStat progressionStat)
    {
        var baseValue = selector(officer);
        var itemBonus = 0;
        foreach (var item in GetEquippedItems(world, officer.Id))
        {
            itemBonus += bonusSelector(item);
        }

        return ClampStat(baseValue + itemBonus + OfficerProgressionRules.GetStatBonus(officer, progressionStat));
    }

    private static IEnumerable<ItemData> GetEquippedItems(WorldState world, int officerId)
    {
        return world.Items.Where(item => item.EquippedOfficerId == officerId);
    }

    private static ItemData? GetEquippedItemInSlot(WorldState world, int officerId, ItemType itemType)
    {
        return world.Items.FirstOrDefault(item =>
            item.EquippedOfficerId == officerId &&
            AreItemsInSameSlot(item.ItemType, itemType));
    }

    private static bool AreItemsInSameSlot(ItemType a, ItemType b)
    {
        return GetItemSlotKey(a) == GetItemSlotKey(b);
    }

    private static string GetItemSlotKey(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Weapon => "weapon",
            ItemType.Horse => "horse",
            ItemType.Book => "special",
            ItemType.Treasure => "special",
            _ => "special"
        };
    }

    private static bool IsItemOwnedByFactionInventory(ItemData item, int factionId)
    {
        return item.OwnerFactionId == factionId &&
               item.EquippedOfficerId <= 0;
    }

    private static void MoveItemToFactionInventory(ItemData item, int factionId)
    {
        item.OwnerFactionId = factionId;
        item.OwnerCityId = 0;
        item.EquippedOfficerId = 0;
    }

    private static void EquipItemToOfficer(ItemData item, int factionId, int officerId)
    {
        item.OwnerFactionId = factionId;
        item.OwnerCityId = 0;
        item.EquippedOfficerId = officerId;
    }

    private static void AssignItemToOfficer(WorldState world, ItemData item, int factionId, int officerId)
    {
        var existingItem = GetEquippedItemInSlot(world, officerId, item.ItemType);
        if (existingItem != null && existingItem.Id != item.Id)
        {
            MoveItemToFactionInventory(existingItem, factionId);
        }

        EquipItemToOfficer(item, factionId, officerId);
    }

    private static void TransferEquippedItemsToFaction(WorldState world, int officerId, int factionId)
    {
        foreach (var item in world.Items.Where(item => item.EquippedOfficerId == officerId))
        {
            item.OwnerFactionId = factionId;
            item.OwnerCityId = 0;
        }
    }

    private static void AwardBattleExperience(WorldState world, IEnumerable<int> officerIds, int amount)
    {
        foreach (var officerId in officerIds.Distinct())
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            OfficerProgressionRules.AwardBattleExperience(officer, amount);
        }
    }

    private ItemData? TryFindDiscoverableItem(WorldState world, int cityId)
    {
        var candidates = world.Items
            .Where(item => item.OwnerFactionId <= 0 && item.OwnerCityId == cityId && item.EquippedOfficerId <= 0)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[_random.Next(candidates.Count)];
    }

    private static int GetItemGiftAcceptanceBonus(ItemData? item)
    {
        if (item == null)
        {
            return 0;
        }

        var rarityBonus = item.Rarity.ToLowerInvariant() switch
        {
            "epic" => 10,
            "rare" => 6,
            _ => 3
        };

        return rarityBonus +
               item.CharmBonus +
               item.LoyaltyBonus +
               (item.StrengthBonus + item.IntelligenceBonus + item.LeadershipBonus + item.PoliticsBonus + item.CombatBonus) / 2;
    }

    private string GetItemDisplayName(ItemData item, GameLanguage language)
    {
        if (language == GameLanguage.TraditionalChinese)
        {
            return !string.IsNullOrWhiteSpace(item.NameZhHant) ? item.NameZhHant : item.NameEn;
        }

        return !string.IsNullOrWhiteSpace(item.NameEn) ? item.NameEn : item.NameZhHant;
    }

    private OfficerData? TryFindDiscoverableOfficer(WorldState world, int factionId, int cityId)
    {
        var candidates = new List<OfficerData>();
        foreach (var officer in world.Officers)
        {
            if (!FreeOfficerMovement.IsFreeOfficer(world, officer))
            {
                continue;
            }

            if (officer.CityId > 0 && officer.CityId != cityId)
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

    private void RevealFreeOfficerAtCity(CityData city, OfficerData officer)
    {
        officer.CityId = city.Id;
        officer.FreeOfficerStayMonths = Math.Max(officer.FreeOfficerStayMonths, 1);
    }

    private void RecruitFreeOfficerToCity(WorldState world, CityData city, OfficerData officer)
    {
        officer.CityId = city.Id;
        officer.FreeOfficerStayMonths = 0;
        officer.Loyalty = ClampStat(65 + _random.Next(0, 16));

        if (!city.OfficerIds.Contains(officer.Id))
        {
            city.OfficerIds.Add(officer.Id);
        }

        var faction = world.GetFaction(city.OwnerFactionId);
        if (faction != null && !faction.OfficerIds.Contains(officer.Id))
        {
            faction.OfficerIds.Add(officer.Id);
        }
    }

    private static bool DoesFreeOfficerAcceptHire(CityData city, OfficerData officer, int rulerCharm, int goldOffer, int foodOffer, ItemData? giftedItem)
    {
        var offerBonus = goldOffer / 50 + foodOffer / 250;
        return city.Loyalty + officer.Charm + rulerCharm / 2 + offerBonus + GetItemGiftAcceptanceBonus(giftedItem) - officer.Ambition >= 80;
    }

    private static bool DoesEmployedOfficerAcceptHire(OfficerData officer, int rulerCharm, int goldOffer, int foodOffer, ItemData? giftedItem)
    {
        var offerBonus = goldOffer / 40 + foodOffer / 200;
        return rulerCharm + officer.Charm + offerBonus + GetItemGiftAcceptanceBonus(giftedItem) - officer.Loyalty - officer.Ambition / 2 >= 40;
    }

    private static int GetRulerCharm(WorldState world, int factionId)
    {
        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return 50;
        }

        var ruler = world.GetOfficer(faction.RulerOfficerId);
        return ruler?.Charm ?? 50;
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
        if (officer.DeathYear > 0 && world.Year > officer.DeathYear)
        {
            return false;
        }

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

    private static string GetFactionName(FactionData faction, GameLanguage language)
    {
        if (language == GameLanguage.TraditionalChinese)
        {
            return !string.IsNullOrWhiteSpace(faction.NameZhHant)
                ? faction.NameZhHant
                : faction.NameEn;
        }

        return !string.IsNullOrWhiteSpace(faction.NameEn)
            ? faction.NameEn
            : faction.NameZhHant;
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

    private string GetDiplomacyActionName(DiplomacyActionType actionType, GameLanguage language)
    {
        var key = actionType switch
        {
            DiplomacyActionType.Alliance => "command.diplomacy.alliance",
            DiplomacyActionType.Truce => "command.diplomacy.truce",
            DiplomacyActionType.Gift => "command.diplomacy.gift",
            DiplomacyActionType.Demand => "command.diplomacy.demand",
            DiplomacyActionType.BreakPact => "command.diplomacy.break_pact",
            _ => "command.diplomacy.alliance"
        };
        return _localization?.TForLanguage(language, key) ?? actionType.ToString();
    }

    private string GetSpyActionName(SpyActionType actionType, GameLanguage language)
    {
        var key = actionType switch
        {
            SpyActionType.Reconnaissance => "command.spy.reconnaissance",
            SpyActionType.Sabotage => "command.spy.sabotage",
            SpyActionType.Incite => "command.spy.incite",
            SpyActionType.Assassination => "command.spy.assassination",
            _ => "command.spy.reconnaissance"
        };
        return _localization?.TForLanguage(language, key) ?? actionType.ToString();
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

    private PrefectAutoAppointmentOutcome? ResolveCapturedCityOfficers(WorldState world, CityData capturedCity, int previousFactionId)
    {
        if (capturedCity.OfficerIds.Count == 0)
        {
            return null;
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

            if (HasOfficerAppointment(officer, OfficerAppointmentRules.Governor))
            {
                ClearOfficerAppointment(officer, OfficerAppointmentRules.Governor);
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

        if (retreatCity != null)
        {
            return EnsureCityPrefectAppointment(world, retreatCity);
        }

        return null;
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

    private static bool HasUsedCivilRelief(WorldState world, CityData city)
    {
        return city.LastCivilReliefYear == world.Year && city.LastCivilReliefMonth == world.Month;
    }

    private static void MarkCivilReliefUsed(WorldState world, CityData city)
    {
        city.LastCivilReliefYear = world.Year;
        city.LastCivilReliefMonth = world.Month;
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
