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

    public static void NormalizeOfficer(OfficerData officer)
    {
        officer.Appointments ??= new List<string>();

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
        return appointment.Equals(Strategist, StringComparison.OrdinalIgnoreCase) ||
               appointment.Equals(Governor, StringComparison.OrdinalIgnoreCase);
    }
}
