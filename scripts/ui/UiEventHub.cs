namespace ThreeKingdom.UI;

internal sealed class UiEventHub
{
    internal sealed class CityStateChangedEvent
    {
        public required int CityId { get; init; }
        public required int FactionId { get; init; }
    }

    internal sealed class OfficerStateChangedEvent
    {
        public required int OfficerId { get; init; }
        public required int CityId { get; init; }
        public required int FactionId { get; init; }
    }

    internal sealed class OfficerAppointmentsChangedEvent
    {
        public required int OfficerId { get; init; }
        public required int CityId { get; init; }
        public required int FactionId { get; init; }
    }

    internal sealed class FactionLeadershipChangedEvent
    {
        public required int FactionId { get; init; }
        public required int CityId { get; init; }
    }

    public event System.Action<CityStateChangedEvent>? CityStateChanged;
    public event System.Action<OfficerStateChangedEvent>? OfficerStateChanged;
    public event System.Action<OfficerAppointmentsChangedEvent>? OfficerAppointmentsChanged;
    public event System.Action<FactionLeadershipChangedEvent>? FactionLeadershipChanged;

    public void PublishCityStateChanged(int cityId, int factionId)
    {
        CityStateChanged?.Invoke(new CityStateChangedEvent
        {
            CityId = cityId,
            FactionId = factionId
        });
    }

    public void PublishOfficerStateChanged(int officerId, int cityId, int factionId)
    {
        OfficerStateChanged?.Invoke(new OfficerStateChangedEvent
        {
            OfficerId = officerId,
            CityId = cityId,
            FactionId = factionId
        });
    }

    public void PublishOfficerAppointmentsChanged(int officerId, int cityId, int factionId)
    {
        OfficerAppointmentsChanged?.Invoke(new OfficerAppointmentsChangedEvent
        {
            OfficerId = officerId,
            CityId = cityId,
            FactionId = factionId
        });
    }

    public void PublishFactionLeadershipChanged(int factionId, int cityId)
    {
        FactionLeadershipChanged?.Invoke(new FactionLeadershipChangedEvent
        {
            FactionId = factionId,
            CityId = cityId
        });
    }
}
