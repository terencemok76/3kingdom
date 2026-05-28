using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    public CommandResult ScheduleInternalAffairs(
        int actorFactionId,
        int cityId,
        int officerId,
        InternalAffairsJobType jobType,
        int months,
        ConstructionProjectType constructionProjectType = ConstructionProjectType.None,
        int investedGold = 0)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var city = world.GetCity(cityId);
        if (city == null)
        {
            return LocalizedResult(false, "cmd.source_city_not_found");
        }

        if (city.OwnerFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.city_not_controlled");
        }

        if (months <= 0)
        {
            return LocalizedResult(false, "cmd.internal_affairs.invalid_duration");
        }

        var officer = world.GetOfficer(officerId);
        if (officer == null || officer.CityId != city.Id || !city.OfficerIds.Contains(officerId))
        {
            return LocalizedResult(false, "cmd.internal_affairs.officer_required", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        }

        if (IsOfficerAssignedThisMonth(world, officer) || HasActiveInternalAffairsSchedule(world, officer.Id))
        {
            return LocalizedResult(false, "cmd.internal_affairs.officer_unavailable", GetOfficerArgs(officer, GameLanguage.TraditionalChinese), GetOfficerArgs(officer, GameLanguage.English));
        }

        if (HasActiveInternalAffairsJob(world, city.Id, jobType))
        {
            return LocalizedResult(
                false,
                "cmd.internal_affairs.job_already_active",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), GetInternalAffairsJobName(jobType, GameLanguage.TraditionalChinese) },
                new object[] { GetCityName(city, GameLanguage.English), GetInternalAffairsJobName(jobType, GameLanguage.English) });
        }

        if (investedGold <= 0)
        {
            investedGold = GetRecommendedInternalAffairsGold(jobType, months);
        }

        var minimumGold = GetMinimumInternalAffairsGold(months);
        if (investedGold < minimumGold)
        {
            return LocalizedResult(
                false,
                "cmd.internal_affairs.invalid_gold",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), minimumGold },
                new object[] { GetCityName(city, GameLanguage.English), minimumGold });
        }

        var monthlyGold = GetInternalAffairsMonthlyGoldCost(investedGold, Math.Min(months, 24), Math.Min(months, 24));
        if (city.Gold < monthlyGold)
        {
            return LocalizedResult(
                false,
                "cmd.internal_affairs.not_enough_gold",
                new object[] { GetCityName(city, GameLanguage.TraditionalChinese), monthlyGold },
                new object[] { GetCityName(city, GameLanguage.English), monthlyGold });
        }

        if (jobType == InternalAffairsJobType.Construction)
        {
            constructionProjectType = ResolveConstructionProjectType(city, constructionProjectType);
        }
        else
        {
            constructionProjectType = ConstructionProjectType.None;
        }

        var schedule = new InternalAffairsScheduleData
        {
            Id = GetNextInternalAffairsScheduleId(world),
            CityId = city.Id,
            OfficerId = officer.Id,
            IsAuthorizedPlan = false,
            JobType = jobType,
            ConstructionProjectType = constructionProjectType,
            InvestedGold = investedGold,
            RemainingMonths = Math.Min(months, 24),
            TotalMonths = Math.Min(months, 24),
            StartedYear = world.Year,
            StartedMonth = world.Month,
            State = InternalAffairsScheduleState.Active,
            SkipExecutionYear = -1,
            SkipExecutionMonth = -1
        };
        world.InternalAffairsSchedules.Add(schedule);
        MarkOfficerAssigned(world, officer, CommandType.InternalAffairs);

        return LocalizedResult(
            true,
            "cmd.internal_affairs.scheduled",
            new object[]
            {
                GetCityName(city, GameLanguage.TraditionalChinese),
                GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                GetInternalAffairsJobName(jobType, GameLanguage.TraditionalChinese),
                schedule.RemainingMonths,
                schedule.InvestedGold,
                schedule.InvestedGold * schedule.TotalMonths
            },
            new object[]
            {
                GetCityName(city, GameLanguage.English),
                GetOfficerDisplayName(officer, GameLanguage.English),
                GetInternalAffairsJobName(jobType, GameLanguage.English),
                schedule.RemainingMonths,
                schedule.InvestedGold,
                schedule.InvestedGold * schedule.TotalMonths
            });
    }

    public CommandResult TerminateInternalAffairsSchedule(int actorFactionId, int scheduleId)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var schedule = world.InternalAffairsSchedules.FirstOrDefault(item => item.Id == scheduleId);
        if (schedule == null || schedule.State is not (InternalAffairsScheduleState.Active or InternalAffairsScheduleState.Paused))
        {
            return LocalizedResult(false, "cmd.internal_affairs.schedule_not_found");
        }

        var city = world.GetCity(schedule.CityId);
        if (city == null)
        {
            return LocalizedResult(false, "cmd.source_city_not_found");
        }

        if (city.OwnerFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.city_not_controlled");
        }

        if (schedule.IsAuthorizedPlan)
        {
            ClearCityAuthorizedPlan(city);
        }

        schedule.State = InternalAffairsScheduleState.Terminated;
        return LocalizedResult(
            true,
            "cmd.internal_affairs.terminated",
            new object[] { GetCityName(city, GameLanguage.TraditionalChinese), GetInternalAffairsJobName(schedule.JobType, GameLanguage.TraditionalChinese) },
            new object[] { GetCityName(city, GameLanguage.English), GetInternalAffairsJobName(schedule.JobType, GameLanguage.English) });
    }

    public CommandResult PauseInternalAffairsSchedule(int actorFactionId, int scheduleId)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var schedule = world.InternalAffairsSchedules.FirstOrDefault(item => item.Id == scheduleId);
        if (schedule == null || schedule.State != InternalAffairsScheduleState.Active)
        {
            return LocalizedResult(false, "cmd.internal_affairs.pause_not_available");
        }

        var city = world.GetCity(schedule.CityId);
        if (city == null)
        {
            return LocalizedResult(false, "cmd.source_city_not_found");
        }

        if (city.OwnerFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.city_not_controlled");
        }

        ReleaseInternalAffairsOfficerAssignment(world, schedule.OfficerId);
        schedule.State = InternalAffairsScheduleState.Paused;
        schedule.OfficerId = 0;

        return LocalizedResult(
            true,
            "cmd.internal_affairs.paused",
            new object[] { GetCityName(city, GameLanguage.TraditionalChinese), GetInternalAffairsJobName(schedule.JobType, GameLanguage.TraditionalChinese) },
            new object[] { GetCityName(city, GameLanguage.English), GetInternalAffairsJobName(schedule.JobType, GameLanguage.English) });
    }

    public int GetRecommendedInternalAffairsOfficerId(int actorFactionId, int cityId, InternalAffairsJobType jobType)
    {
        if (_turnManager?.World == null)
        {
            return 0;
        }

        var world = _turnManager.World;
        var city = world.GetCity(cityId);
        if (city == null || city.OwnerFactionId != actorFactionId)
        {
            return 0;
        }

        return TrySelectInternalAffairsOfficerForSchedule(world, city, jobType)?.Id ?? 0;
    }

    public CommandResult ResumeInternalAffairsSchedule(int actorFactionId, int scheduleId, int officerId = 0)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var schedule = world.InternalAffairsSchedules.FirstOrDefault(item => item.Id == scheduleId);
        if (schedule == null || schedule.State != InternalAffairsScheduleState.Paused)
        {
            return LocalizedResult(false, "cmd.internal_affairs.resume_not_available");
        }

        var city = world.GetCity(schedule.CityId);
        if (city == null)
        {
            return LocalizedResult(false, "cmd.source_city_not_found");
        }

        if (city.OwnerFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.city_not_controlled");
        }

        OfficerData? officer = null;
        if (officerId > 0)
        {
            officer = world.GetOfficer(officerId);
            if (officer == null || officer.CityId != city.Id || !city.OfficerIds.Contains(officerId))
            {
                return LocalizedResult(false, "cmd.internal_affairs.officer_required", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
            }

            if (IsOfficerAssignedThisMonth(world, officer) || HasActiveInternalAffairsSchedule(world, officer.Id))
            {
                return LocalizedResult(false, "cmd.internal_affairs.officer_unavailable", GetOfficerArgs(officer, GameLanguage.TraditionalChinese), GetOfficerArgs(officer, GameLanguage.English));
            }
        }
        else
        {
            officer = TrySelectInternalAffairsOfficerForSchedule(world, city, schedule.JobType);
        }

        if (officer == null)
        {
            return LocalizedResult(
                false,
                "cmd.internal_affairs.resume_no_officer",
                GetCityArgs(city, GameLanguage.TraditionalChinese),
                GetCityArgs(city, GameLanguage.English));
        }

        schedule.State = InternalAffairsScheduleState.Active;
        schedule.OfficerId = officer.Id;
        schedule.SkipExecutionYear = -1;
        schedule.SkipExecutionMonth = -1;
        MarkOfficerAssigned(world, officer, CommandType.InternalAffairs);

        return LocalizedResult(
            true,
            "cmd.internal_affairs.resumed",
            new object[]
            {
                GetCityName(city, GameLanguage.TraditionalChinese),
                GetInternalAffairsJobName(schedule.JobType, GameLanguage.TraditionalChinese),
                GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese)
            },
            new object[]
            {
                GetCityName(city, GameLanguage.English),
                GetInternalAffairsJobName(schedule.JobType, GameLanguage.English),
                GetOfficerDisplayName(officer, GameLanguage.English)
            });
    }

    public CommandResult CancelCurrentMonthInternalAffairsSchedule(int actorFactionId, int scheduleId)
    {
        if (_turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.world_not_initialized");
        }

        var world = _turnManager.World;
        var schedule = world.InternalAffairsSchedules.FirstOrDefault(item => item.Id == scheduleId);
        if (schedule == null || schedule.State != InternalAffairsScheduleState.Active)
        {
            return LocalizedResult(false, "cmd.internal_affairs.cancel_month_not_available");
        }

        var city = world.GetCity(schedule.CityId);
        if (city == null)
        {
            return LocalizedResult(false, "cmd.source_city_not_found");
        }

        if (city.OwnerFactionId != actorFactionId)
        {
            return LocalizedResult(false, "cmd.city_not_controlled");
        }

        ReleaseInternalAffairsOfficerAssignment(world, schedule.OfficerId);
        schedule.OfficerId = 0;
        schedule.SkipExecutionYear = world.Year;
        schedule.SkipExecutionMonth = world.Month;

        return LocalizedResult(
            true,
            "cmd.internal_affairs.cancelled_this_month",
            new object[] { GetCityName(city, GameLanguage.TraditionalChinese), GetInternalAffairsJobName(schedule.JobType, GameLanguage.TraditionalChinese) },
            new object[] { GetCityName(city, GameLanguage.English), GetInternalAffairsJobName(schedule.JobType, GameLanguage.English) });
    }

    public List<CommandResult> ResolveInternalAffairsSchedules()
    {
        var results = new List<CommandResult>();
        if (_turnManager?.World == null)
        {
            return results;
        }

        var world = _turnManager.World;
        results.AddRange(EnsureAuthorizedPrefectPlans(world));
        results.AddRange(TryAutoResumeGoldPausedSchedules(world));
        foreach (var schedule in world.InternalAffairsSchedules.Where(item => item.State == InternalAffairsScheduleState.Active).ToList())
        {
            if (schedule.SkipExecutionYear == world.Year && schedule.SkipExecutionMonth == world.Month)
            {
                continue;
            }

            var city = world.GetCity(schedule.CityId);
            if (city == null)
            {
                schedule.State = InternalAffairsScheduleState.Interrupted;
                schedule.InterruptedReason = "missing_city";
                results.Add(LocalizedResult(false, "cmd.internal_affairs.interrupted"));
                continue;
            }

            var isPlayerRelated = city.OwnerFactionId == _turnManager.GetPlayerFactionId();

            var officer = world.GetOfficer(schedule.OfficerId);
            if (officer == null || officer.CityId != city.Id)
            {
                officer = TrySelectInternalAffairsOfficerForSchedule(world, city, schedule.JobType);
                if (officer != null)
                {
                    schedule.OfficerId = officer.Id;
                    MarkOfficerAssigned(world, officer, CommandType.InternalAffairs);
                }
            }

            if (officer == null || officer.CityId != city.Id)
            {
                schedule.OfficerId = 0;
                continue;
            }

            var monthlyGoldCost = GetInternalAffairsMonthlyGoldCost(schedule);
            if (city.Gold < monthlyGoldCost)
            {
                ReleaseInternalAffairsOfficerAssignment(world, schedule.OfficerId);
                schedule.State = InternalAffairsScheduleState.Paused;
                schedule.OfficerId = 0;
                schedule.InterruptedReason = "insufficient_gold";
                var insufficientGoldResult = LocalizedResult(
                    true,
                    "cmd.internal_affairs.paused_insufficient_gold",
                    new object[]
                    {
                        GetCityName(city, GameLanguage.TraditionalChinese),
                        GetInternalAffairsJobName(schedule.JobType, GameLanguage.TraditionalChinese),
                        monthlyGoldCost
                    },
                    new object[]
                    {
                        GetCityName(city, GameLanguage.English),
                        GetInternalAffairsJobName(schedule.JobType, GameLanguage.English),
                        monthlyGoldCost
                    });
                insufficientGoldResult.IsPlayerRelated = isPlayerRelated;
                results.Add(insufficientGoldResult);
                continue;
            }

            city.Gold -= monthlyGoldCost;

            var gains = ApplyInternalAffairsJob(world, city, officer, schedule.JobType, schedule.ConstructionProjectType, schedule.InvestedGold, schedule.TotalMonths);
            schedule.RemainingMonths -= 1;
            if (schedule.IsAuthorizedPlan)
            {
                city.PrefectPlanJobType = schedule.JobType;
                city.PrefectPlanConstructionProjectType = schedule.ConstructionProjectType;
                city.PrefectPlanInvestedGold = schedule.InvestedGold;
                city.PrefectPlanTotalMonths = schedule.TotalMonths;
                city.PrefectPlanRemainingMonths = Math.Max(schedule.RemainingMonths, 0);
            }

            if (schedule.RemainingMonths <= 0)
            {
                schedule.State = InternalAffairsScheduleState.Completed;
                if (schedule.IsAuthorizedPlan)
                {
                    results.Add(BuildAuthorizedPlanCompletedResult(city, officer, schedule, isPlayerRelated));
                    ClearCityAuthorizedPlan(city);
                }
            }

            var resolveResult = LocalizedResult(
                true,
                "cmd.internal_affairs.resolved",
                new object[]
                {
                    GetCityName(city, GameLanguage.TraditionalChinese),
                    GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                    GetInternalAffairsJobName(schedule.JobType, GameLanguage.TraditionalChinese),
                    gains.Farm,
                    gains.Commercial,
                    gains.Defense,
                    gains.DisasterPrevention,
                    gains.Loyalty,
                    Math.Max(schedule.RemainingMonths, 0),
                    monthlyGoldCost
                },
                new object[]
                {
                    GetCityName(city, GameLanguage.English),
                    GetOfficerDisplayName(officer, GameLanguage.English),
                    GetInternalAffairsJobName(schedule.JobType, GameLanguage.English),
                    gains.Farm,
                    gains.Commercial,
                    gains.Defense,
                    gains.DisasterPrevention,
                    gains.Loyalty,
                    Math.Max(schedule.RemainingMonths, 0),
                    monthlyGoldCost
                });
            resolveResult.IsPlayerRelated = isPlayerRelated;
            results.Add(resolveResult);
        }

        world.InternalAffairsSchedules.RemoveAll(item =>
            item.State is InternalAffairsScheduleState.Terminated or
                InternalAffairsScheduleState.Interrupted or
                InternalAffairsScheduleState.Completed);
        return results;
    }

    private List<CommandResult> TryAutoResumeGoldPausedSchedules(WorldState world)
    {
        var results = new List<CommandResult>();
        var playerFactionId = _turnManager?.GetPlayerFactionId() ?? -1;
        foreach (var schedule in world.InternalAffairsSchedules.Where(item =>
                     item.State == InternalAffairsScheduleState.Paused &&
                     string.Equals(item.InterruptedReason, "insufficient_gold", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var city = world.GetCity(schedule.CityId);
            if (city == null)
            {
                continue;
            }

            var monthlyGoldCost = GetInternalAffairsMonthlyGoldCost(schedule);
            if (city.Gold < monthlyGoldCost)
            {
                continue;
            }

            var officer = TrySelectInternalAffairsOfficerForSchedule(world, city, schedule.JobType);
            if (officer == null)
            {
                continue;
            }

            schedule.State = InternalAffairsScheduleState.Active;
            schedule.OfficerId = officer.Id;
            schedule.InterruptedReason = string.Empty;
            schedule.SkipExecutionYear = -1;
            schedule.SkipExecutionMonth = -1;
            MarkOfficerAssigned(world, officer, CommandType.InternalAffairs);

            var resumeResult = LocalizedResult(
                true,
                "cmd.internal_affairs.auto_resumed",
                new object[]
                {
                    GetCityName(city, GameLanguage.TraditionalChinese),
                    GetInternalAffairsJobName(schedule.JobType, GameLanguage.TraditionalChinese),
                    GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese),
                    monthlyGoldCost
                },
                new object[]
                {
                    GetCityName(city, GameLanguage.English),
                    GetInternalAffairsJobName(schedule.JobType, GameLanguage.English),
                    GetOfficerDisplayName(officer, GameLanguage.English),
                    monthlyGoldCost
                });
            resumeResult.IsPlayerRelated = city.OwnerFactionId == playerFactionId;
            results.Add(resumeResult);
        }

        return results;
    }

    private List<CommandResult> EnsureAuthorizedPrefectPlans(WorldState world)
    {
        var results = new List<CommandResult>();
        var playerFactionId = _turnManager?.GetPlayerFactionId() ?? -1;
        foreach (var city in world.Cities)
        {
            if (city.OwnerFactionId <= 0)
            {
                continue;
            }

            var isPlayerCity = city.OwnerFactionId == playerFactionId;
            var prefect = GetCityPrefect(world, city);
            var effectiveAuthorizationType = city.PrefectAuthorizationType;
            if (!isPlayerCity &&
                effectiveAuthorizationType == PrefectAuthorizationType.None &&
                prefect != null)
            {
                effectiveAuthorizationType = PrefectAuthorizationType.Full;
            }

            if (effectiveAuthorizationType == PrefectAuthorizationType.None)
            {
                ClearCityAuthorizedPlan(city);
                continue;
            }

            if (prefect == null)
            {
                ClearCityAuthorizedPlan(city);
                RemoveAuthorizedPlanSchedule(world, city);
                continue;
            }

            var schedule = GetAuthorizedPlanSchedule(world, city.Id);
            if (schedule != null)
            {
                city.PrefectPlanJobType = schedule.JobType;
                city.PrefectPlanConstructionProjectType = schedule.ConstructionProjectType;
                city.PrefectPlanInvestedGold = schedule.InvestedGold;
                city.PrefectPlanTotalMonths = schedule.TotalMonths;
                city.PrefectPlanRemainingMonths = schedule.RemainingMonths;
                continue;
            }

            if (effectiveAuthorizationType == PrefectAuthorizationType.Full && city.PrefectPlanRemainingMonths <= 0)
            {
                var plannedJob = ChooseAuthorizedPlanJob(world, city, prefect);
                if (!plannedJob.HasValue)
                {
                    continue;
                }

                city.PrefectPlanJobType = plannedJob.Value;
                city.PrefectPlanConstructionProjectType = plannedJob.Value == InternalAffairsJobType.Construction
                    ? ResolveConstructionProjectType(city, city.PrefectPlanConstructionProjectType)
                    : ConstructionProjectType.None;
                city.PrefectPlanInvestedGold = GetRecommendedInternalAffairsGold(plannedJob.Value, ChooseAuthorizedPlanDuration(city, plannedJob.Value, prefect));
                city.PrefectPlanTotalMonths = ChooseAuthorizedPlanDuration(city, plannedJob.Value, prefect);
                city.PrefectPlanRemainingMonths = city.PrefectPlanTotalMonths;
                city.PrefectPlanIsPlayerDirected = false;
            }

            if (city.PrefectPlanRemainingMonths <= 0)
            {
                continue;
            }

            var planGold = city.PrefectPlanInvestedGold > 0
                ? city.PrefectPlanInvestedGold
                : GetRecommendedInternalAffairsGold(city.PrefectPlanJobType, city.PrefectPlanRemainingMonths);
            if (city.Gold < planGold)
            {
                continue;
            }

            city.Gold -= planGold;
            city.PrefectPlanInvestedGold = planGold;
            var officer = TrySelectInternalAffairsOfficerForSchedule(world, city, city.PrefectPlanJobType);
            var newSchedule = CreateAuthorizedPlanSchedule(world, city, officer?.Id ?? 0);
            world.InternalAffairsSchedules.Add(newSchedule);
            if (officer != null)
            {
                MarkOfficerAssigned(world, officer, CommandType.InternalAffairs);
            }

            results.Add(BuildAuthorizedPlanStartedResult(
                city,
                officer,
                newSchedule,
                city.OwnerFactionId == playerFactionId));
        }

        return results;
    }

    private CommandResult BuildAuthorizedPlanStartedResult(
        CityData city,
        OfficerData? officer,
        InternalAffairsScheduleData schedule,
        bool isPlayerRelated)
    {
        var officerNameZh = officer != null
            ? GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese)
            : _localization?.TForLanguage(GameLanguage.TraditionalChinese, "ui.unassigned") ?? "-";
        var officerNameEn = officer != null
            ? GetOfficerDisplayName(officer, GameLanguage.English)
            : _localization?.TForLanguage(GameLanguage.English, "ui.unassigned") ?? "-";
        var result = LocalizedResult(
            true,
            "cmd.prefect_plan.started",
            new object[]
            {
                GetCityName(city, GameLanguage.TraditionalChinese),
                GetInternalAffairsJobName(schedule.JobType, GameLanguage.TraditionalChinese),
                schedule.TotalMonths,
                officerNameZh
            },
            new object[]
            {
                GetCityName(city, GameLanguage.English),
                GetInternalAffairsJobName(schedule.JobType, GameLanguage.English),
                schedule.TotalMonths,
                officerNameEn
            });
        result.IsPlayerRelated = isPlayerRelated;
        return result;
    }

    private CommandResult BuildAuthorizedPlanCompletedResult(
        CityData city,
        OfficerData officer,
        InternalAffairsScheduleData schedule,
        bool isPlayerRelated)
    {
        var result = LocalizedResult(
            true,
            "cmd.prefect_plan.completed",
            new object[]
            {
                GetCityName(city, GameLanguage.TraditionalChinese),
                GetInternalAffairsJobName(schedule.JobType, GameLanguage.TraditionalChinese),
                schedule.TotalMonths,
                GetOfficerDisplayName(officer, GameLanguage.TraditionalChinese)
            },
            new object[]
            {
                GetCityName(city, GameLanguage.English),
                GetInternalAffairsJobName(schedule.JobType, GameLanguage.English),
                schedule.TotalMonths,
                GetOfficerDisplayName(officer, GameLanguage.English)
            });
        result.IsPlayerRelated = isPlayerRelated;
        return result;
    }


}
