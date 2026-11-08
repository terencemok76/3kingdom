using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class OfficerAppointmentRules
{
    public const string Lord = "Lord";
    public const string Governor = "Governor";
    public const string Strategist = "Strategist";
    public const string Chancellor = "Chancellor";
    public const string ChiefStrategist = "ChiefStrategist";
    public const string GrandGeneral = "GrandGeneral";
    public const string VanguardGeneral = "VanguardGeneral";
    public const string RearGeneral = "RearGeneral";
    public static int GetStrategistQuota(WorldState world, FactionData faction)
    {
        var cityCount = world.Cities.Count(city => city.OwnerFactionId == faction.Id);
        return Math.Min(6, 2 + cityCount / 3);
    }
    public static readonly string[] MilitaryAppointments = { GrandGeneral, VanguardGeneral, RearGeneral };

    public static bool IsMilitaryAppointment(string appointment) => MilitaryAppointments.Any(item => item.Equals(appointment, StringComparison.OrdinalIgnoreCase));
    public static void ClearMilitaryAppointments(OfficerData officer) => officer.Appointments.RemoveAll(IsMilitaryAppointment);

    public static int CountStrategists(WorldState world, FactionData faction) => faction.OfficerIds
        .Distinct().Select(world.GetOfficer)
        .Count(officer => officer != null && HasAppointment(officer, Strategist));

    public static int GetStrategySuccessBonus(OfficerData? officer, FactionData? faction, bool chiefPresentOnSameSide = false)
    {
        if (officer == null || faction == null || !faction.OfficerIds.Contains(officer.Id)) return 0;
        if (faction.ChiefStrategistOfficerId == officer.Id) return 8;
        return Math.Max(HasAppointment(officer, Strategist) ? 5 : 0, chiefPresentOnSameSide ? 3 : 0);
    }

    public static bool IsChiefStrategistPresent(FactionData? faction, string teamName, IEnumerable<(int OfficerId, string TeamName, int TroopCount)> activeUnits)
    {
        return faction != null && faction.ChiefStrategistOfficerId > 0 &&
            faction.OfficerIds.Contains(faction.ChiefStrategistOfficerId) &&
            activeUnits.Any(unit => unit.OfficerId == faction.ChiefStrategistOfficerId && unit.TroopCount > 0 &&
                string.Equals(unit.TeamName, teamName, StringComparison.Ordinal));
    }

    public static void NormalizeOfficer(OfficerData officer)
    {
        officer.Appointments ??= new List<string>();
        if (string.IsNullOrWhiteSpace(officer.Role))
        {
            officer.Role = "Common";
        }

        if (officer.Role.Equals(Strategist, StringComparison.OrdinalIgnoreCase))
        {
            AddAppointment(officer, Strategist);
            officer.Role = "Advisor";
        }
        else if (officer.Role.Equals(Governor, StringComparison.OrdinalIgnoreCase))
        {
            AddAppointment(officer, Governor);
            officer.Role = "General";
        }
        else if (officer.Role.Equals(Lord, StringComparison.OrdinalIgnoreCase) ||
                 officer.Role.Equals("Ruler", StringComparison.OrdinalIgnoreCase))
        {
            AddAppointment(officer, Lord);
            officer.Role = Lord;
        }

        officer.Appointments = officer.Appointments
            .Where(static appointment => !string.IsNullOrWhiteSpace(appointment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool HasAppointment(OfficerData officer, string appointment)
    {
        return officer.Appointments.Any(existing =>
            existing.Equals(appointment, StringComparison.OrdinalIgnoreCase));
    }

    public static void AddAppointment(OfficerData officer, string appointment)
    {
        if (string.IsNullOrWhiteSpace(appointment) || HasAppointment(officer, appointment))
        {
            return;
        }

        officer.Appointments.Add(appointment);
    }

    public static void RemoveAppointment(OfficerData officer, string appointment)
    {
        officer.Appointments.RemoveAll(existing =>
            existing.Equals(appointment, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsValidOfficerAppointment(string appointment)
    {
        return IsMilitaryAppointment(appointment) || appointment.Equals(Strategist, StringComparison.OrdinalIgnoreCase) ||
               appointment.Equals(Governor, StringComparison.OrdinalIgnoreCase);
    }
}
