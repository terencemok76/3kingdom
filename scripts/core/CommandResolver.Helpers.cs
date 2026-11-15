using static ThreeKingdom.Core.CommandOfficerAssignmentRules;
using static ThreeKingdom.Core.InternalAffairsPlanningRules;
using static ThreeKingdom.Core.OfficerSuccessionAndPrefectRules;
using static ThreeKingdom.Core.OfficerItemRules;
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
        if (requestedAmount <= 0 || availableAmount <= 0)
        {
            return 0;
        }

        return Math.Min(requestedAmount, availableAmount);
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
            TroopType.Engineer => "troop_type.engineer",
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

            if (!IsConstructionProjectJob(jobType))
            {
                return true;
            }

            return IsConstructionProjectJob(schedule.JobType) && schedule.ConstructionProjectType == constructionProjectType;
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

    private void ResolveRulerDeath(WorldState world, int factionId, int previousRulerOfficerId = 0, bool triggeredByCapture = false)
    {
        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return;
        }

        var previousRuler = previousRulerOfficerId > 0 ? world.GetOfficer(previousRulerOfficerId) : null;
        var candidateIds = GetSuccessionCandidates(world, faction, previousRuler);

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
                PreviousRulerOfficerId = previousRulerOfficerId,
                TriggeredByCapture = triggeredByCapture,
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

        OfficerAppointmentRules.ClearMilitaryAppointments(successor);
        OfficerAppointmentRules.RemoveAppointment(successor, OfficerAppointmentRules.Strategist);
        AssignOfficerAppointment(successor, OfficerAppointmentRules.Lord);
        successor.Belongs = faction.Id.ToString();
        var successorNameZh = !string.IsNullOrWhiteSpace(successor.NameZhHant) ? successor.NameZhHant : successor.Name;
        var successorNameEn = !string.IsNullOrWhiteSpace(successor.Name) ? successor.Name : successor.NameZhHant;
        faction.NameZhHant = string.IsNullOrWhiteSpace(successorNameZh) ? faction.NameZhHant : successorNameZh;
        faction.NameEn = string.IsNullOrWhiteSpace(successorNameEn) ? faction.NameEn : successorNameEn;

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

    private sealed record BattleRulerDeathData(int FactionId, int RulerOfficerId, string FactionNameZh, string FactionNameEn, string RulerNameZh, string RulerNameEn);

    private (string ZhHant, string En)? TryResolveBattleRulerDeath(
        WorldState world,
        int officerId,
        float casualtyRatio,
        IDictionary<int, int>? deferredRulerSuccessions = null,
        ICollection<BattleRulerDeathData>? deferredBattleRulerDeaths = null,
        ISet<int>? battleDeathOfficerIds = null)
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
        battleDeathOfficerIds?.Add(officer.Id);
        if (deferredRulerSuccessions != null && deferredBattleRulerDeaths != null)
        {
            deferredRulerSuccessions.TryAdd(factionId, officer.Id);
            deferredBattleRulerDeaths.Add(new BattleRulerDeathData(
                factionId,
                officer.Id,
                factionNameZh,
                factionNameEn,
                rulerNameZh,
                rulerNameEn));
            return null;
        }

        ResolveRulerDeath(world, factionId, officer.Id);

        return BuildBattleRulerDeathSummary(world, new BattleRulerDeathData(
            factionId,
            officer.Id,
            factionNameZh,
            factionNameEn,
            rulerNameZh,
            rulerNameEn));
    }

    private void ResolveDeferredRulerSuccessions(WorldState world, IReadOnlyDictionary<int, int> deferredRulerSuccessions)
    {
        foreach (var (factionId, previousRulerOfficerId) in deferredRulerSuccessions)
        {
            ResolveRulerDeath(world, factionId, previousRulerOfficerId, triggeredByCapture: true);
        }
    }

    private List<(string ZhHant, string En)> BuildDeferredBattleRulerDeathSummaries(WorldState world, IEnumerable<BattleRulerDeathData> deaths)
    {
        return deaths
            .Select(death => BuildBattleRulerDeathSummary(world, death))
            .Where(summary => summary.HasValue)
            .Select(summary => summary!.Value)
            .ToList();
    }

    private (string ZhHant, string En)? BuildBattleRulerDeathSummary(WorldState world, BattleRulerDeathData death)
    {
        var factionId = death.FactionId;
        var factionNameZh = death.FactionNameZh;
        var factionNameEn = death.FactionNameEn;
        var rulerNameZh = death.RulerNameZh;
        var rulerNameEn = death.RulerNameEn;

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
            "grandgeneral" => _localization.TForLanguage(language, "role.grand_general"),
            "vanguardgeneral" => _localization.TForLanguage(language, "role.vanguard_general"),
            "reargeneral" => _localization.TForLanguage(language, "role.rear_general"),
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

    private InternalAffairsScheduleData CreateAuthorizedPlanSchedule(WorldState world, CityData city, int officerId)
    {
        var projectType = IsConstructionProjectJob(city.PrefectPlanJobType)
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

    private static ConstructionProjectType ResolveConstructionProjectType(WorldState world, CityData city, ConstructionProjectType requestedProjectType)
    {
        if (requestedProjectType != ConstructionProjectType.None)
        {
            return requestedProjectType;
        }

        return AiConstructionRules.ChooseConstructionProjectType(world, city);
    }

    private (int Farm, int Commercial, int Defense, int DisasterPrevention, int Loyalty, int ConstructionPoints, ConstructionRules.ConstructionProgressResult ConstructionResult, ResourceRules.ExtractionResult ResourceOutput, ResourceRules.SurveyResult SurveyOutput) ApplyInternalAffairsJob(
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
        var constructionPoints = ConstructionRules.GetConstructionPoints(politics, intelligence, leadership, monthlyInvestment, progressionBonus);
        if (jobType == InternalAffairsJobType.Manufacturing)
        {
            constructionPoints = ConstructionRules.ApplySiegeWorkshopManufacturingBonus(city, constructionPoints);
        }

        (int Farm, int Commercial, int Defense, int DisasterPrevention, int Loyalty, int ConstructionPoints, ConstructionRules.ConstructionProgressResult ConstructionResult, ResourceRules.ExtractionResult ResourceOutput, ResourceRules.SurveyResult SurveyOutput) gains = jobType switch
        {
            InternalAffairsJobType.Farm => (primaryGain, 0, 0, 0, 0, 0, default, default, default),
            InternalAffairsJobType.Commercial => (0, primaryGain, 0, 0, 0, 0, default, default, default),
            InternalAffairsJobType.Defend => (0, 0, primaryGain, 0, 0, 0, default, default, default),
            InternalAffairsJobType.WaterControl => (0, 0, 0, primaryGain, secondaryGain, 0, default, default, default),
            InternalAffairsJobType.Construction or InternalAffairsJobType.Manufacturing => (0, secondaryGain, secondaryGain, secondaryGain, 0, constructionPoints, default, default, default),
            InternalAffairsJobType.Extraction => (0, 0, 0, 0, 0, 0, default, default, default),
            InternalAffairsJobType.Survey => (0, 0, 0, 0, 0, 0, default, default, default),
            _ => (0, 0, 0, 0, 0, 0, default, default, default)
        };

        if (jobType == InternalAffairsJobType.Defend)
        {
            gains.Defense = ConstructionRules.ApplyDefenseRepair(city, gains.Defense);
        }
        else if (jobType == InternalAffairsJobType.WaterControl)
        {
            gains.DisasterPrevention = ConstructionRules.ApplyDisasterPrevention(city, gains.DisasterPrevention);
        }

        city.Farm = ClampStat(city.Farm + gains.Farm);
        city.Commercial = ClampStat(city.Commercial + gains.Commercial);
        city.Defense = ClampStat(city.Defense + gains.Defense);
        city.DisasterPrevention = ClampStat(city.DisasterPrevention + gains.DisasterPrevention);
        city.Loyalty = ClampStat(city.Loyalty + gains.Loyalty);
        if (IsConstructionProjectJob(jobType))
        {
            gains.ConstructionResult = ConstructionRules.ApplyProgress(city, constructionProjectType, gains.ConstructionPoints);
        }
        else if (jobType == InternalAffairsJobType.Extraction)
        {
            gains.ResourceOutput = ResourceRules.ApplyExtraction(city, primaryGain);
        }
        else if (jobType == InternalAffairsJobType.Survey)
        {
            gains.SurveyOutput = ResourceRules.ApplySurvey(world, city, primaryGain, _random);
        }

        OfficerProgressionRules.AwardInternalAffairsExperience(officer, jobType, 40);
        OfficerProgressionRules.AwardCivilExperience(officer, 12);
        return gains;
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
            InternalAffairsJobType.Manufacturing => "command.internal_affairs.manufacturing",
            InternalAffairsJobType.Extraction => "command.internal_affairs.extraction",
            InternalAffairsJobType.Survey => "command.internal_affairs.survey",
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
            ConstructionProjectType.ResourceDepot => "construction_project.resource_depot",
            ConstructionProjectType.Granary => "construction_project.granary",
            ConstructionProjectType.HorseStable => "construction_project.horse_stable",
            ConstructionProjectType.Ram => "construction_project.ram",
            ConstructionProjectType.Catapult => "construction_project.catapult",
            ConstructionProjectType.Ladder => "construction_project.ladder",
            ConstructionProjectType.SupplyCart => "construction_project.supply_cart",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(key)
            ? projectType.ToString()
            : _localization?.TForLanguage(language, key) ?? projectType.ToString();
    }

    private string FormatConstructionProjectProgress(CityData city, ConstructionProjectType projectType, GameLanguage language)
    {
        var projectValue = ConstructionRules.IsFacilityProject(projectType)
            ? ConstructionRules.GetLevel(city, projectType)
            : projectType == ConstructionProjectType.SupplyCart ? city.SupplyCartCount : city.GetSiegeEngineCount(ConstructionRules.GetSiegeEngineType(projectType));
        var projectProgress = ConstructionRules.IsFacilityProject(projectType)
            ? ConstructionRules.GetProgress(city, projectType)
            : projectType == ConstructionProjectType.SupplyCart ? city.SupplyCartProgress : city.GetSiegeEngineProgress(ConstructionRules.GetSiegeEngineType(projectType));
        var required = ConstructionRules.GetRequiredPointsForNextValue(projectType, projectValue);

        return _localization?.FormatForLanguage(language, "fmt.facility_level_progress", projectValue, projectProgress, required)
               ?? $"{projectValue} ({projectProgress}/{required})";
    }

    private string BuildConstructionProgressSuffix(
        CityData city,
        ConstructionProjectType projectType,
        int constructionPoints,
        ConstructionRules.ConstructionProgressResult progressResult,
        bool isManufacturing,
        GameLanguage language)
    {
        var progressText = _localization?.FormatForLanguage(
                   language,
                   isManufacturing ? "fmt.internal_affairs_manufacturing_progress" : "fmt.internal_affairs_construction_progress",
                   GetConstructionProjectName(projectType, language),
                   constructionPoints,
                   FormatConstructionProjectProgress(city, projectType, language),
                   progressResult.ValuesGained)
               ?? string.Empty;
        if (progressResult.WaitingForMaterials)
        {
            return $"{progressText} {_localization?.FormatForLanguage(language, "fmt.internal_affairs_construction_waiting_materials")}".Trim();
        }

        if (progressResult.MaterialsSpent.IsEmpty)
        {
            return progressText;
        }

        return $"{progressText} {_localization?.FormatForLanguage(language, "fmt.internal_affairs_materials_spent", progressResult.MaterialsSpent.Wood, progressResult.MaterialsSpent.Metal, progressResult.MaterialsSpent.Stone)}".Trim();
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

    internal static int GetHireAcceptanceScore(WorldState world, CityData city, OfficerData officer, int actorFactionId, int goldOffer, int foodOffer, ItemData? giftedItem, int? observedLoyalty = null)
    {
        var rulerCharm = GetRulerCharm(world, actorFactionId);
        var relationshipBonus = GetHireRelationshipBonus(world, officer, actorFactionId) + (world.GetOfficerAffection(actorFactionId, officer.Id)?.Score ?? 0) / 5;
        if (FreeOfficerMovement.IsFreeOfficer(world, officer))
        {
            var offerBonus = goldOffer / 50 + foodOffer / 250;
            return city.Loyalty + officer.Charm + rulerCharm / 2 + offerBonus + GetItemGiftAcceptanceBonus(giftedItem) + relationshipBonus - officer.Ambition;
        }

        var employedOfferBonus = goldOffer / 40 + foodOffer / 200;
        return rulerCharm + officer.Charm + employedOfferBonus + GetItemGiftAcceptanceBonus(giftedItem) + relationshipBonus - (observedLoyalty ?? officer.Loyalty) - officer.Ambition / 2;
    }

    internal static int GetHireAcceptanceThreshold(WorldState world, OfficerData officer) =>
        FreeOfficerMovement.IsFreeOfficer(world, officer) ? 80 : 40;

    internal static int GetHireRelationshipBonus(WorldState world, OfficerData officer, int actorFactionId) =>
        GetHireRelationshipType(world, officer, actorFactionId) switch
        {
            "family,blood" => 20,
            "family,non-blood" => 10,
            "family" => 8,
            _ => 0
        };

    internal static string? GetHireRelationshipType(WorldState world, OfficerData officer, int actorFactionId)
    {
        var ruler = world.GetOfficer(world.GetFaction(actorFactionId)?.RulerOfficerId ?? 0);
        return OfficerRelationshipRules.GetRelationshipType(officer, ruler);
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

        return world.Year - officer.BirthYear >= 14;
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
            DiplomacyActionType.Surrender => "command.diplomacy.surrender",
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
            SpyActionType.SowDiscord => "command.spy.sow_discord",
            SpyActionType.RecruitCovertOfficer => "command.spy.recruit_covert",
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
        // Military and officer-contact orders can stack per city; core city actions stay one-pending-per-type.
        if (pendingCommand.Type == CommandType.Move || pendingCommand.Type == CommandType.Attack || pendingCommand.Type == CommandType.ContactOfficer)
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
