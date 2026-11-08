using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

/// <summary>Bounded troop-combat effects for an appointed officer's own unit.</summary>
public static class MilitaryAppointmentCombatRules
{
    public static int GetAttackBonusPercent(OfficerData? officer)
    {
        if (officer == null) return 0;
        if (OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.VanguardGeneral)) return 5;
        return OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.GrandGeneral) ? 3 : 0;
    }

    public static int GetDamageReductionPercent(OfficerData? officer)
    {
        if (officer == null) return 0;
        if (OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.RearGeneral)) return 5;
        return OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.GrandGeneral) ? 3 : 0;
    }
}
