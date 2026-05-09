using System;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public enum OfficerProgressionStat
{
    Strength,
    Intelligence,
    Charm,
    Leadership,
    Politics,
    Combat
}

public static class OfficerProgressionRules
{
    public static int GetStatBonus(OfficerData officer, OfficerProgressionStat stat)
    {
        var roleBonus = GetRoleStatBonus(officer.Role, stat);
        var titleBonus = GetTitleStatBonus(officer.GeneralTitle, stat) +
                         GetTitleStatBonus(officer.StrategistTitle, stat) +
                         GetTitleStatBonus(officer.SpyTitle, stat) +
                         GetTitleStatBonus(officer.DiplomacyTitle, stat) +
                         GetTitleStatBonus(officer.CivilTitle, stat);
        return roleBonus + titleBonus;
    }

    public static int GetInternalAffairsOutputBonus(OfficerData officer, InternalAffairsJobType jobType)
    {
        return Math.Max(0, GetJobRank(officer, jobType) - 1);
    }

    public static void AwardInternalAffairsExperience(OfficerData officer, InternalAffairsJobType jobType, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        switch (jobType)
        {
            case InternalAffairsJobType.Farm:
                officer.FarmExperience += amount;
                officer.FarmRank = CalculateRank(officer.FarmExperience);
                officer.FarmTitle = GetJobTitleKey(jobType, officer.FarmRank);
                break;
            case InternalAffairsJobType.Commercial:
                officer.CommercialExperience += amount;
                officer.CommercialRank = CalculateRank(officer.CommercialExperience);
                officer.CommercialTitle = GetJobTitleKey(jobType, officer.CommercialRank);
                break;
            case InternalAffairsJobType.Defend:
                officer.DefendExperience += amount;
                officer.DefendRank = CalculateRank(officer.DefendExperience);
                officer.DefendTitle = GetJobTitleKey(jobType, officer.DefendRank);
                break;
            case InternalAffairsJobType.WaterControl:
                officer.DisasterPreventionExperience += amount;
                officer.DisasterPreventionRank = CalculateRank(officer.DisasterPreventionExperience);
                officer.DisasterPreventionTitle = GetJobTitleKey(jobType, officer.DisasterPreventionRank);
                break;
            case InternalAffairsJobType.Construction:
                officer.ConstructionExperience += amount;
                officer.ConstructionRank = CalculateRank(officer.ConstructionExperience);
                officer.ConstructionTitle = GetJobTitleKey(jobType, officer.ConstructionRank);
                break;
        }
    }

    public static void AwardBattleExperience(OfficerData officer, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        officer.BattleExperience += amount;
        officer.MilitaryRank = CalculateRank(officer.BattleExperience);
        officer.GeneralTitle = GetGeneralTitleKey(officer.MilitaryRank);
    }

    public static void AwardStrategistExperience(OfficerData officer, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        officer.StrategistExperience += amount;
        officer.StrategistRank = CalculateRank(officer.StrategistExperience);
        officer.StrategistTitle = GetStrategistTitleKey(officer.StrategistRank);
    }

    public static void AwardSpyExperience(OfficerData officer, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        officer.SpyExperience += amount;
        officer.SpyRank = CalculateRank(officer.SpyExperience);
        officer.SpyTitle = GetSpyTitleKey(officer.SpyRank);
    }

    public static void AwardDiplomacyExperience(OfficerData officer, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        officer.DiplomacyExperience += amount;
        officer.DiplomacyRank = CalculateRank(officer.DiplomacyExperience);
        officer.DiplomacyTitle = GetDiplomacyTitleKey(officer.DiplomacyRank);
    }

    public static int GetSpySuccessBonus(OfficerData officer)
    {
        return Math.Max(0, officer.SpyRank) * 4;
    }

    public static int GetDiplomacySuccessBonus(OfficerData officer)
    {
        return Math.Max(0, officer.DiplomacyRank) * 4;
    }

    public static void AwardCivilExperience(OfficerData officer, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        officer.CivilExperience += amount;
        officer.CivilRank = CalculateRank(officer.CivilExperience);
        officer.CivilTitle = GetCivilTitleKey(officer.CivilRank);
    }

    public static int GetJobRank(OfficerData officer, InternalAffairsJobType jobType)
    {
        return jobType switch
        {
            InternalAffairsJobType.Farm => officer.FarmRank,
            InternalAffairsJobType.Commercial => officer.CommercialRank,
            InternalAffairsJobType.Defend => officer.DefendRank,
            InternalAffairsJobType.WaterControl => officer.DisasterPreventionRank,
            InternalAffairsJobType.Construction => officer.ConstructionRank,
            _ => 0
        };
    }

    public static string GetJobTitleKey(OfficerData officer, InternalAffairsJobType jobType)
    {
        return jobType switch
        {
            InternalAffairsJobType.Farm => officer.FarmTitle,
            InternalAffairsJobType.Commercial => officer.CommercialTitle,
            InternalAffairsJobType.Defend => officer.DefendTitle,
            InternalAffairsJobType.WaterControl => officer.DisasterPreventionTitle,
            InternalAffairsJobType.Construction => officer.ConstructionTitle,
            _ => string.Empty
        };
    }

    private static int GetRoleStatBonus(string role, OfficerProgressionStat stat)
    {
        return role.ToLowerInvariant() switch
        {
            "general" when stat is OfficerProgressionStat.Leadership or OfficerProgressionStat.Combat => 2,
            "strategist" when stat == OfficerProgressionStat.Intelligence => 4,
            "strategist" when stat == OfficerProgressionStat.Charm => 2,
            "advisor" when stat is OfficerProgressionStat.Intelligence or OfficerProgressionStat.Politics => 2,
            "governor" when stat == OfficerProgressionStat.Politics => 4,
            "governor" when stat == OfficerProgressionStat.Charm => 2,
            _ => 0
        };
    }

    private static int GetTitleStatBonus(string titleKey, OfficerProgressionStat stat)
    {
        return titleKey switch
        {
            "progression.general.rank1" when stat == OfficerProgressionStat.Combat => 2,
            "progression.general.rank2" when stat is OfficerProgressionStat.Leadership or OfficerProgressionStat.Combat => 2,
            "progression.general.rank3" when stat == OfficerProgressionStat.Strength => 2,
            "progression.general.rank3" when stat == OfficerProgressionStat.Combat => 3,
            "progression.general.rank4" when stat == OfficerProgressionStat.Leadership => 4,
            "progression.general.rank4" when stat == OfficerProgressionStat.Combat => 3,
            "progression.general.rank5" when stat == OfficerProgressionStat.Leadership => 6,
            "progression.general.rank5" when stat == OfficerProgressionStat.Combat => 4,
            "progression.general.rank5" when stat == OfficerProgressionStat.Strength => 2,

            "progression.strategist.rank1" when stat == OfficerProgressionStat.Intelligence => 2,
            "progression.strategist.rank2" when stat == OfficerProgressionStat.Intelligence => 3,
            "progression.strategist.rank2" when stat == OfficerProgressionStat.Charm => 1,
            "progression.strategist.rank3" when stat == OfficerProgressionStat.Intelligence => 4,
            "progression.strategist.rank3" when stat == OfficerProgressionStat.Charm => 2,
            "progression.strategist.rank4" when stat == OfficerProgressionStat.Intelligence => 5,
            "progression.strategist.rank4" when stat == OfficerProgressionStat.Charm => 2,
            "progression.strategist.rank5" when stat == OfficerProgressionStat.Intelligence => 6,
            "progression.strategist.rank5" when stat == OfficerProgressionStat.Charm => 3,

            "progression.spy.rank1" when stat == OfficerProgressionStat.Intelligence => 1,
            "progression.spy.rank1" when stat == OfficerProgressionStat.Charm => 1,
            "progression.spy.rank2" when stat == OfficerProgressionStat.Intelligence => 2,
            "progression.spy.rank2" when stat == OfficerProgressionStat.Charm => 1,
            "progression.spy.rank3" when stat == OfficerProgressionStat.Intelligence => 3,
            "progression.spy.rank3" when stat == OfficerProgressionStat.Charm => 2,
            "progression.spy.rank4" when stat == OfficerProgressionStat.Intelligence => 4,
            "progression.spy.rank4" when stat == OfficerProgressionStat.Charm => 2,
            "progression.spy.rank5" when stat == OfficerProgressionStat.Intelligence => 5,
            "progression.spy.rank5" when stat == OfficerProgressionStat.Charm => 3,

            "progression.diplomacy.rank1" when stat == OfficerProgressionStat.Charm => 1,
            "progression.diplomacy.rank1" when stat == OfficerProgressionStat.Politics => 1,
            "progression.diplomacy.rank2" when stat == OfficerProgressionStat.Charm => 2,
            "progression.diplomacy.rank2" when stat == OfficerProgressionStat.Politics => 1,
            "progression.diplomacy.rank3" when stat == OfficerProgressionStat.Charm => 3,
            "progression.diplomacy.rank3" when stat == OfficerProgressionStat.Politics => 2,
            "progression.diplomacy.rank4" when stat == OfficerProgressionStat.Charm => 4,
            "progression.diplomacy.rank4" when stat == OfficerProgressionStat.Politics => 2,
            "progression.diplomacy.rank5" when stat == OfficerProgressionStat.Charm => 5,
            "progression.diplomacy.rank5" when stat == OfficerProgressionStat.Politics => 3,

            "progression.civil.rank1" when stat == OfficerProgressionStat.Politics => 2,
            "progression.civil.rank2" when stat == OfficerProgressionStat.Politics => 3,
            "progression.civil.rank2" when stat == OfficerProgressionStat.Charm => 1,
            "progression.civil.rank3" when stat == OfficerProgressionStat.Politics => 4,
            "progression.civil.rank3" when stat == OfficerProgressionStat.Charm => 2,
            "progression.civil.rank4" when stat == OfficerProgressionStat.Politics => 5,
            "progression.civil.rank4" when stat == OfficerProgressionStat.Leadership => 2,
            "progression.civil.rank5" when stat == OfficerProgressionStat.Politics => 6,
            "progression.civil.rank5" when stat == OfficerProgressionStat.Leadership => 3,
            _ => 0
        };
    }

    private static int CalculateRank(int experience)
    {
        return experience switch
        {
            >= 500 => 5,
            >= 300 => 4,
            >= 180 => 3,
            >= 80 => 2,
            >= 40 => 1,
            _ => 0
        };
    }

    private static string GetGeneralTitleKey(int rank)
    {
        if (rank <= 0)
        {
            return string.Empty;
        }

        return $"progression.general.rank{ClampRank(rank)}";
    }

    private static string GetStrategistTitleKey(int rank)
    {
        if (rank <= 0)
        {
            return string.Empty;
        }

        return $"progression.strategist.rank{ClampRank(rank)}";
    }

    private static string GetCivilTitleKey(int rank)
    {
        if (rank <= 0)
        {
            return string.Empty;
        }

        return $"progression.civil.rank{ClampRank(rank)}";
    }

    private static string GetSpyTitleKey(int rank)
    {
        if (rank <= 0)
        {
            return string.Empty;
        }

        return $"progression.spy.rank{ClampRank(rank)}";
    }

    private static string GetDiplomacyTitleKey(int rank)
    {
        if (rank <= 0)
        {
            return string.Empty;
        }

        return $"progression.diplomacy.rank{ClampRank(rank)}";
    }

    private static string GetJobTitleKey(InternalAffairsJobType jobType, int rank)
    {
        if (rank <= 0)
        {
            return string.Empty;
        }

        var suffix = $"rank{ClampRank(rank)}";
        return jobType switch
        {
            InternalAffairsJobType.Farm => $"progression.job.farm.{suffix}",
            InternalAffairsJobType.Commercial => $"progression.job.commercial.{suffix}",
            InternalAffairsJobType.Defend => $"progression.job.defend.{suffix}",
            InternalAffairsJobType.WaterControl => $"progression.job.disaster_prevention.{suffix}",
            InternalAffairsJobType.Construction => $"progression.job.construction.{suffix}",
            _ => string.Empty
        };
    }

    private static int ClampRank(int rank)
    {
        return Math.Clamp(rank, 1, 5);
    }
}
