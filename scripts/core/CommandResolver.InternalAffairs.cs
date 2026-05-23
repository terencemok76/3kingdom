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
        int months)
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

        var schedule = new InternalAffairsScheduleData
        {
            Id = GetNextInternalAffairsScheduleId(world),
            CityId = city.Id,
            OfficerId = officer.Id,
            JobType = jobType,
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
                schedule.RemainingMonths
            },
            new object[]
            {
                GetCityName(city, GameLanguage.English),
                GetOfficerDisplayName(officer, GameLanguage.English),
                GetInternalAffairsJobName(jobType, GameLanguage.English),
                schedule.RemainingMonths
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

            var gains = ApplyInternalAffairsJob(world, city, officer, schedule.JobType);
            schedule.RemainingMonths -= 1;
            if (schedule.RemainingMonths <= 0)
            {
                schedule.State = InternalAffairsScheduleState.Completed;
            }

            results.Add(LocalizedResult(
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
                    Math.Max(schedule.RemainingMonths, 0)
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
                    Math.Max(schedule.RemainingMonths, 0)
                }));
        }

        world.InternalAffairsSchedules.RemoveAll(item =>
            item.State is InternalAffairsScheduleState.Terminated or
                InternalAffairsScheduleState.Interrupted or
                InternalAffairsScheduleState.Completed);
        return results;
    }


}
