using System.Linq;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private const string UnknownInfoText = "??";

    private bool IsGodModeEnabled()
    {
        return _turnManager?.World?.ViewAllInformationEnabled ?? false;
    }

    private string BuildGodModeButtonText()
    {
        return IsGodModeEnabled() ? "God Mode: On" : "God Mode: Off";
    }

    private bool CanViewCityFullInformation(CityData? city)
    {
        if (_turnManager?.World == null || city == null)
        {
            return false;
        }

        if (IsGodModeEnabled())
        {
            return true;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        if (city.OwnerFactionId == playerFactionId)
        {
            return true;
        }

        return _turnManager.World.HasActiveCityIntel(playerFactionId, city.Id);
    }

    private bool CanViewOfficerFullInformation(OfficerData? officer)
    {
        if (_turnManager?.World == null || officer == null)
        {
            return false;
        }

        if (IsGodModeEnabled())
        {
            return true;
        }

        if (FreeOfficerMovement.IsVisibleFreeOfficer(_turnManager.World, officer))
        {
            return true;
        }

        var city = _turnManager.World.GetCity(officer.CityId);
        return CanViewCityFullInformation(city);
    }

    private bool CanInspectSelectedFaction()
    {
        return CanViewCityFullInformation(_selectedCity);
    }

    private WorldState.CityIntelData? GetVisibleCityIntel(CityData? city)
    {
        if (_turnManager?.World == null || city == null || IsGodModeEnabled())
        {
            return null;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        if (city.OwnerFactionId == playerFactionId)
        {
            return null;
        }

        return _turnManager.World.GetCityIntel(playerFactionId, city.Id);
    }

    private string BuildCityIntelDurationText(CityData? city)
    {
        var intel = GetVisibleCityIntel(city);
        if (intel == null || _localization == null)
        {
            return string.Empty;
        }

        return _localization.Format("fmt.city_intel_duration", _localization.T("ui.city_intel"), intel.RemainingMonths);
    }

    private string MaskedText(bool isVisible, string value)
    {
        return isVisible ? value : UnknownInfoText;
    }

    private string MaskedNumberText(bool isVisible, int value)
    {
        return isVisible ? value.ToString() : UnknownInfoText;
    }

    private string BuildMaskedOfficerName(OfficerData officer)
    {
        return MaskedText(CanViewOfficerFullInformation(officer), _localization?.GetOfficerName(officer) ?? officer.Name);
    }

    private string BuildMaskedOfficerRole(OfficerData officer)
    {
        return MaskedText(CanViewOfficerFullInformation(officer), _localization?.GetOfficerRole(officer) ?? officer.Role);
    }

    private string BuildMaskedOfficerAppointments(OfficerData officer)
    {
        return MaskedText(CanViewOfficerFullInformation(officer), BuildOfficerAppointmentsText(officer));
    }

    private string BuildMaskedOfficerStatus(WorldState world, OfficerData officer)
    {
        return MaskedText(
            CanViewOfficerFullInformation(officer),
            FreeOfficerMovement.IsFreeOfficer(world, officer)
                ? _localization?.T("ui.free_officer") ?? "Free Officer"
                : _localization?.GetOfficerStatus(world, officer) ?? "Idle");
    }

    private string BuildMaskedOfficerLoyalty(WorldState world, OfficerData officer)
    {
        if (IsFactionRuler(world, officer) || FreeOfficerMovement.IsFreeOfficer(world, officer))
        {
            return "-";
        }

        return MaskedNumberText(CanViewOfficerFullInformation(officer), officer.Loyalty);
    }

    private bool HasVisibleOfficerInCity(CityData city)
    {
        if (_turnManager?.World == null)
        {
            return false;
        }

        return city.OfficerIds.Any(officerId =>
        {
            var officer = _turnManager.World.GetOfficer(officerId);
            return officer != null && CanViewOfficerFullInformation(officer);
        });
    }
}
