using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class AppointmentOverviewRules
{
    public sealed record Row(string CategoryKey, string Appointment, int Used, int Quota, int OfficerId, int CityId, string EffectKey);

    public static List<Row> Build(WorldState world, FactionData faction)
    {
        var rows = new List<Row>();
        var officers = faction.OfficerIds.Distinct().Select(world.GetOfficer).Where(officer => officer != null).Cast<OfficerData>().OrderBy(officer => officer.Id).ToList();

        void AddFactionPost(string category, string appointment, int quota, IEnumerable<OfficerData> holders, string effect)
        {
            var assigned = holders.ToList();
            if (assigned.Count == 0)
            {
                rows.Add(new Row(category, appointment, 0, quota, 0, 0, effect));
                return;
            }
            foreach (var holder in assigned)
                rows.Add(new Row(category, appointment, assigned.Count, quota, holder.Id, holder.CityId, effect));
        }

        AddFactionPost("ui.appointment_category_central", OfficerAppointmentRules.Chancellor, 1,
            officers.Where(officer => officer.Id == faction.ChancellorOfficerId), "ui.appointment_effect_chancellor");
        AddFactionPost("ui.appointment_category_strategist", OfficerAppointmentRules.ChiefStrategist, 1,
            officers.Where(officer => officer.Id == faction.ChiefStrategistOfficerId), "ui.chief_strategist_effect");
        AddFactionPost("ui.appointment_category_military", OfficerAppointmentRules.GrandGeneral, 1,
            officers.Where(officer => OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.GrandGeneral)), "ui.military_effect_grand_general");
        AddFactionPost("ui.appointment_category_military", OfficerAppointmentRules.VanguardGeneral, 1,
            officers.Where(officer => OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.VanguardGeneral)), "ui.military_effect_vanguard_general");
        AddFactionPost("ui.appointment_category_military", OfficerAppointmentRules.RearGeneral, 1,
            officers.Where(officer => OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.RearGeneral)), "ui.military_effect_rear_general");
        AddFactionPost("ui.appointment_category_strategist", OfficerAppointmentRules.Strategist,
            OfficerAppointmentRules.GetStrategistQuota(world, faction),
            officers.Where(officer => OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Strategist)), "ui.strategist_effect");

        foreach (var city in world.Cities.Where(city => city.OwnerFactionId == faction.Id).OrderBy(city => city.Id))
        {
            var holders = officers.Where(officer => officer.CityId == city.Id && city.OfficerIds.Contains(officer.Id) &&
                OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Governor)).ToList();
            if (holders.Count == 0)
                rows.Add(new Row("ui.appointment_category_local", OfficerAppointmentRules.Governor, 0, 1, 0, city.Id, "ui.appointment_effect_governor"));
            else
                foreach (var holder in holders)
                    rows.Add(new Row("ui.appointment_category_local", OfficerAppointmentRules.Governor, holders.Count, 1, holder.Id, city.Id, "ui.appointment_effect_governor"));
        }
        return rows;
    }
}
